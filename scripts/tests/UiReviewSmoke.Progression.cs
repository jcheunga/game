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
        foreach (var stage in new[] { 4, 43, 52 })
        {
            state.SetSelectedStage(stage);
            var node = AdventureMapCatalog.Leader(stage);
            state.MoveAdventureHero(node.MapId, node.Point);
            await Open("MapMenu");
            typeof(MapMenu).GetMethod("SelectSite", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(GetTree().CurrentScene, new object[] { node });
            await Wait(.15);
            Check(Walk(GetTree().CurrentScene).OfType<Label>().Any(l => l.Text.Contains("Suggested core")),
                $"Stage {stage} displays preparation advice");
            Check(Walk(GetTree().CurrentScene).OfType<Label>().Any(l => l.Text.Contains("First 3-star victory")),
                $"Stage {stage} displays its mastery reward");
            AuditText($"Progression / stage {stage}");
            await Capture($"stage-{stage}-rewards");
        }
        GD.Print($"PROGRESSION_UI_RESULT: {_failures} failures");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }
}
