using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

public partial class UiReviewSmoke
{
    private async Task ReviewStageStars()
    {
        _output = ProjectSettings.GlobalizePath("res://artifacts/stage-stars");
        if (OS.GetCmdlineUserArgs().Contains("--small-window")) _output += "/small";
        System.IO.Directory.CreateDirectory(_output);
        var state = GameState.Instance;
        state.PrepareCampaignBattle();
        state.ApplyVictory(1, 0, 0, 3);
        state.ApplyVictory(2, 0, 0, 2);
        state.ApplyVictory(3, 0, 0, 1);
        state.ReloadFromDisk();
        foreach (var stage in GameData.GetStagesForMap("city"))
            state.MoveAdventureHero("city", AdventureMapCatalog.Leader(stage.StageNumber).Point);
        state.SetSelectedStage(3);
        await Open("MapMenu");
        var canvas = Walk(GetTree().CurrentScene).OfType<MapPathCanvas>().Single();
        canvas.ChangeZoom(.1f);
        canvas.FocusOverview();
        await Wait(.2);
        var tokens = Walk(canvas).OfType<AdventureMapToken>().Where(x => x.Site.Kind == AdventureSiteKind.Leader).ToArray();
        Check(tokens.Length == GameData.GetStagesForMap("city").Count, "Every mission, including the boss, has a map marker with a rating");
        foreach (var token in tokens)
        {
            var stars = state.GetStageStars(token.Site.Stage);
            Check(token.AccessibilityName.Contains($"{stars}/3 stars") && token.AccessibilityName.Contains($"stage {token.Site.Stage}"),
                $"Stage {token.Site.Stage} names its stage and saved rating for accessibility");
            if (!token.IsVisibleInTree()) continue;
            var center = token.Position + token.MarkerCenter * token.Scale;
            var tile = AdventureTileCatalog.Find(token.Site.MapId, token.Site.Id);
            Check(center.DistanceTo(tile.Point * canvas.Zoom + canvas.MapOffset) < .01f,
                $"Stage {token.Site.Stage} stays aligned to the map beneath its stars");
        }
        AuditText("Stage stars / map overview");
        await Capture("01-map-ratings");
        canvas.ChangeZoom(1.6f);
        canvas.FocusSite("leader-1"); await Wait(.1);
        await Capture("02-three-stars-close");
        var squad = state.BuildSaveData();
        squad.OwnedPlayerUnitIds = squad.ActiveDeckUnitIds = GameData.GetPlayerUnits().Take(state.DeckSizeLimit).Select(unit => unit.Id).ToArray();
        squad.OwnedPlayerSpellIds = squad.ActiveDeckSpellIds = GameData.GetPlayerSpells().Take(state.SpellDeckSizeLimit).Select(spell => spell.Id).ToArray();
        squad.HighestUnlockedStage = state.MaxStage;
        typeof(GameState).GetMethod("ApplySavedData", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(state, new object[] { squad });
        SceneRouter.Instance.GoToLoadout(); await Wait(.3);
        var preparation = Walk(GetTree().CurrentScene).OfType<LoadoutMenu>().Single();
        Check(PreparationText().Any(text => text.StartsWith($"{GameData.GetStage(state.SelectedStage).RewardGold:N0} GOLD"))
            && !Walk(preparation).OfType<ScrollContainer>().Any(scroll => scroll.GetVScrollBar().IsVisibleInTree()),
            "Battle preparation fits its rewards and squad without vertical scrolling");
        AuditText("Stage stars / preparation rewards");
        await Capture("03-preparation-rewards");
        state.PrepareCampaignBattle();
        await Open("Battle");
        var battle = (BattleController)GetTree().CurrentScene;
        battle.SetPhysicsProcess(false);
        const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (var (unit, index) in state.GetActiveDeckUnits().Select((unit, index) => (unit, index)))
            typeof(BattleController).GetMethod("SpawnUnit", hidden)!.Invoke(battle, new object[] {
                Team.Player, state.BuildPlayerUnitStats(unit), new Vector2(330 + index * 40, 360) });
        var earnedBefore = state.BuildSaveData();
        typeof(BattleController).GetMethod("DamageBusByRatio", hidden)!.Invoke(battle, new object[] { .1f, Colors.White });
        typeof(BattleController).GetMethod("EndBattle", hidden)!.Invoke(battle, new object[] { true });
        await Wait(.5);
        RoyalResult Board() => Walk(battle).OfType<RoyalResult>().Single(board => board.IsVisibleInTree() && !board.IsQueuedForDeletion());
        Check(Board().Stars == 2, "The victory board displays this run's rating even when the saved best is three stars");
        var paid = BattleSummaryData.Current;
        Check(paid.GoldEarned == state.Gold - earnedBefore.Gold && paid.SeasonXPEarned == state.SeasonPassXP - earnedBefore.SeasonPassXP
            && paid.MasteryXPPerUnit.Count == state.ActiveDeckUnitIds.Count,
            "Victory cards report the gold, experience and squad mastery actually awarded");
        Check(Board().Rewards.Count == paid.Rewards.Count && !Walk(battle).OfType<Label>().Any(label => label.IsVisibleInTree() && label.Text.Contains("Victory on stage")),
            "A full squad's victory rewards show on the board without the old battle report");
        var earnedAfter = state.BuildSaveData();
        typeof(BattleController).GetMethod("EndBattle", hidden)!.Invoke(battle, new object[] { true });
        Check(state.Gold == earnedAfter.Gold && state.SeasonPassXP == earnedAfter.SeasonPassXP,
            "Showing the reward panel again cannot award the victory twice");
        AuditText("Victory rewards");
        await Capture("04-victory-rating");

        // A defeat uses the same card: title, rating, any earnings, and both actions side by side.
        state.PrepareCampaignBattle();
        await Open("Battle");
        battle = (BattleController)GetTree().CurrentScene;
        battle.SetPhysicsProcess(false);
        typeof(BattleController).GetMethod("DamageBusByRatio", hidden)!.Invoke(battle, new object[] { 1f, Colors.White });
        typeof(BattleController).GetMethod("EndBattle", hidden)!.Invoke(battle, new object[] { false });
        await Wait(.5);
        var defeat = Walk(battle).OfType<RoyalResult>().Single(board => board.IsVisibleInTree() && !board.IsQueuedForDeletion());
        Check(!defeat.Won && defeat.Title == "Defeat" && !Walk(battle).OfType<Label>().Any(label => label.IsVisibleInTree() && label.Text.Contains("Defeat on stage")),
            "Defeat shows a titled result board instead of the battle report");
        Check(defeat.LeaveButton.IsVisibleInTree() && defeat.RetryButton.IsVisibleInTree()
            && Mathf.Abs(defeat.LeaveButton.GetGlobalRect().GetCenter().Y - defeat.RetryButton.GetGlobalRect().GetCenter().Y) < 4,
            "Defeat actions sit side by side");
        AuditText("Defeat result");
        await Capture("05-defeat");
        await Open("MainMenu");
        GD.Print($"STAGE_STAR_UI_RESULT: {_failures} failures");
        QuitAfterAudio(_failures == 0 ? 0 : 1);
    }
}
