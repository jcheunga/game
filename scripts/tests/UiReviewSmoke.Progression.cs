using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

public partial class UiReviewSmoke
{
    private async Task ReviewProgression()
    {
        _output = ProjectSettings.GlobalizePath("res://artifacts/fun-pass-20260925/ui");
        if (OS.GetCmdlineUserArgs().Contains("--small-window")) _output += "/small";
        System.IO.Directory.CreateDirectory(_output);
        var state = GameState.Instance;
        foreach (var stage in new[] { 10, 58, 60 })
        {
            var node = AdventureMapCatalog.Leader(stage);
            // Stage details only describe open tiles, so clear the earlier stages and open this leader's tile.
            var progress = state.BuildSaveData();
            progress.StageStars = Enumerable.Range(1, state.MaxStage).Select(s => s < stage ? 1 : 0).ToArray();
            progress.AdventureOpenTiles = progress.AdventureOpenTiles.Append(AdventureTileCatalog.Find(node.MapId, node.Id).Id).Distinct().ToArray();
            typeof(GameState).GetMethod("ApplySavedData", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(state, new object[] { progress });
            state.SetSelectedStage(stage);
            state.MoveAdventureHero(node.MapId, node.Point);
            await Open("MapMenu");
            typeof(MapMenu).GetMethod("SelectSite", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(GetTree().CurrentScene, new object[] { node });
            // A playable stage opens its preparation straight away, which shows the configured victory
            // rewards and the battle entry cost.
            await FinishTravel(); await Wait(.3);
            var text = PreparationText();
            Check(text.Any(t => t.StartsWith($"{GameData.GetStage(stage).RewardGold:N0} GOLD")), $"Stage {stage} displays its victory reward");
            Check(text.Contains($"{state.GetStageEntryFoodCost(stage)} FOOD"), $"Stage {stage} displays its battle entry cost");
            AuditText($"Progression / stage {stage}");
            await Capture($"stage-{stage}-rewards");
        }
        GD.Print($"PROGRESSION_UI_RESULT: {_failures} failures");
        QuitAfterAudio(_failures == 0 ? 0 : 1);
    }
}
