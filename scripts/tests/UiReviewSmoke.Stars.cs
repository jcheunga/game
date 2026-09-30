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
        canvas.FocusPoint(AdventureMapCatalog.WorldSize / 2);
        await Wait(.2);
        var tokens = Walk(canvas).OfType<AdventureMapToken>().Where(x => x.Site.Kind == AdventureSiteKind.Leader).ToArray();
        Check(tokens.Length == GameData.GetStagesForMap("city").Count, "Every mission, including the boss, has a map marker with a rating");
        foreach (var token in tokens)
        {
            var stars = state.GetStageStars(token.Site.Stage);
            Check(token.TooltipText.Contains($"Best: {stars}/3 stars") && token.AccessibilityName.Contains($"{stars}/3 stars"),
                $"Stage {token.Site.Stage} exposes its saved rating and scoring rules");
            if (!token.IsVisibleInTree()) continue;
            var center = token.Position + token.MarkerCenter;
            Check(center.DistanceTo(token.Site.Point * canvas.Zoom + canvas.MapOffset) < .01f,
                $"Stage {token.Site.Stage} stays aligned to the map beneath its stars");
        }
        AuditText("Stage stars / map overview");
        await Capture("01-map-ratings");
        await ChooseAdventureSite("leader-1");
        canvas.ChangeZoom(1.6f);
        canvas.FocusPoint(AdventureMapCatalog.Leader(1).Point);
        await Capture("02-three-stars-close");
        await Press("Intel");
        Check(Walk(GetTree().CurrentScene).OfType<Label>().Any(x => x.Text.Contains(StageStarScore.RulesText)),
            "Mission intel explains all four rating thresholds");
        AuditText("Stage stars / mission intel");
        await Capture("03-scoring-rules");
        state.PrepareCampaignBattle();
        await Open("Battle");
        var battle = (BattleController)GetTree().CurrentScene;
        battle.SetPhysicsProcess(false);
        const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(BattleController).GetMethod("DamageBusByRatio", hidden)!.Invoke(battle, new object[] { .1f, Colors.White, "" });
        typeof(BattleController).GetMethod("EndBattle", hidden)!.Invoke(battle, new object[] { true });
        await Wait(.5);
        Check(Walk(battle).OfType<StageStarRating>().Single(x => x.IsVisibleInTree()).Stars == 2,
            "The victory panel displays this run's rating even when the saved best is three stars");
        await Capture("04-victory-rating");
        BattleSummaryData.Current = new BattleSummaryData { Won = true, StarsEarned = 2, Stage = 2 };
        await Open("BattleSummaryMenu");
        Check(Walk(GetTree().CurrentScene).OfType<StageStarRating>().Single().Stars == 2,
            "Battle summary uses the same textured star rating");
        await Capture("05-battle-summary");
        GD.Print($"STAGE_STAR_UI_RESULT: {_failures} failures");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }
}
