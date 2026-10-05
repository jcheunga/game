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
        await ChooseAdventureSite("leader-1");
        canvas.ChangeZoom(1.6f);
        canvas.FocusSite("leader-1");
        await Capture("02-three-stars-close");
        Check(!Walk(GetTree().CurrentScene).OfType<Button>().Any(button => button.IsVisibleInTree() && button.Text == "Intel"), "Stage details do not contain an Intel tab");
        var squad = state.BuildSaveData();
        squad.OwnedPlayerUnitIds = squad.ActiveDeckUnitIds = GameData.GetPlayerUnits().Take(state.DeckSizeLimit).Select(unit => unit.Id).ToArray();
        squad.OwnedPlayerSpellIds = squad.ActiveDeckSpellIds = GameData.GetPlayerSpells().Take(state.SpellDeckSizeLimit).Select(spell => spell.Id).ToArray();
        squad.HighestUnlockedStage = state.MaxStage;
        typeof(GameState).GetMethod("ApplySavedData", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(state, new object[] { squad });
        SceneRouter.Instance.GoToLoadout(); await Wait(.3);
        var preparation = Walk(GetTree().CurrentScene).OfType<LoadoutMenu>().Single();
        Check(!Walk(GetTree().CurrentScene).OfType<Label>().Any(x => x.Text.Contains(StageStarScore.RulesText))
            && Walk(preparation).OfType<Label>().Any(x => x.Text == $"+{GameData.GetStage(state.SelectedStage).RewardGold:N0}")
            && !Walk(preparation).OfType<Button>().Any(button => button.Text is "Goals" or "Foes" or "Field" or "Brief")
            && !Walk(preparation).OfType<ScrollContainer>().Any(scroll => scroll.GetVScrollBar().IsVisibleInTree()),
            "Battle preparation fits its rewards and squad without briefing tabs or vertical scrolling");
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
        Check(Walk(battle).OfType<StageStarRating>().Single(x => x.IsVisibleInTree()).Stars == 2,
            "The victory panel displays this run's rating even when the saved best is three stars");
        var paid = BattleSummaryData.Current;
        Check(paid.GoldEarned == state.Gold - earnedBefore.Gold && paid.SeasonXPEarned == state.SeasonPassXP - earnedBefore.SeasonPassXP
            && paid.MasteryXPPerUnit.Count == state.ActiveDeckUnitIds.Count,
            "Victory cards report the gold, experience and squad mastery actually awarded");
        var loot = Walk(battle).OfType<ScrollContainer>().Single(scroll => scroll.Name == "VictoryRewards");
        Check(!loot.GetVScrollBar().IsVisibleInTree() && !Walk(battle).OfType<Label>().Any(label => label.IsVisibleInTree() && label.Text.Contains("Victory on stage")),
            "A full squad's victory rewards fit without the old battle report or a vertical scrollbar");
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
        var labels = Walk(battle).OfType<Label>().Where(label => label.IsVisibleInTree()).Select(label => label.Text).ToArray();
        Check(labels.Contains("Defeat") && !labels.Any(text => text.Contains("Defeat on stage") || text.Contains("[X]") || text.Contains("Clear reward")),
            "Defeat shows a titled result card instead of the battle report");
        var actions = Walk(battle).OfType<Button>().Where(button => button.IsVisibleInTree() && (button.Text == "Back to map" || button.Text.StartsWith("Restart"))).ToArray();
        Check(actions.Length == 2 && actions[0].GetParent() == actions[1].GetParent() && actions[0].GetParent() is HBoxContainer,
            "Defeat actions sit side by side");
        AuditText("Defeat result");
        await Capture("05-defeat");
        await Open("MainMenu");
        GD.Print($"STAGE_STAR_UI_RESULT: {_failures} failures");
        QuitAfterAudio(_failures == 0 ? 0 : 1);
    }
}
