using System.Linq;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// Captures the campaign map's fog of war into artifacts/map-fog: a fresh zone, the whole zone zoomed out,
/// the cloud edge close up beside explored land, and fresh starts in zones with other palettes.
///   --fog [--zones=harbor,thornwall]
/// </summary>
public partial class UiReviewSmoke
{
    private async Task ReviewFog()
    {
        _output = ProjectSettings.GlobalizePath("res://artifacts/map-fog");
        System.IO.Directory.CreateDirectory(_output);
        GameState.Instance.ResetProgress(); GameState.Instance.SetShowHints(false);
        await Open("MainMenu");
        var canvas = Walk(GetTree().CurrentScene).OfType<MapPathCanvas>().First();
        await Wait(.6); await Capture("fog-fresh");
        canvas.ChangeZoom(.1f); canvas.FocusOverview(); await Wait(.3); await Capture("fog-overview");
        canvas.ChangeZoom(10f); canvas.FocusSite(AdventureTileCatalog.Stage("city", 1).Id); await Wait(.3); await Capture("fog-close");
        // Mid-campaign: explored land, the frontier's mist and the storm bank beyond it.
        SeedConceptSave(); await Open("MainMenu");
        canvas = Walk(GetTree().CurrentScene).OfType<MapPathCanvas>().First();
        canvas.ChangeZoom(.1f); canvas.FocusOverview(); await Wait(.3); await Capture("fog-progress");
        canvas.ChangeZoom(1.3f); canvas.FocusSite(AdventureTileCatalog.Stage("city", 7).Id); await Wait(.3); await Capture("fog-frontier");
        var zones = OS.GetCmdlineUserArgs().FirstOrDefault(arg => arg.StartsWith("--zones="))?["--zones=".Length..].Split(',')
            ?? new[] { "harbor", "foundry", "thornwall", "gloamwood" };
        GameState.Instance.ResetProgress();
        foreach (var zone in zones)
        {
            canvas.ShowMap(zone, AdventureTileCatalog.Starting(zone).Site.Id); await Wait(.3);
            canvas.ChangeZoom(.1f); canvas.FocusOverview(); await Wait(.3); await Capture("fog-" + zone);
        }
        GD.Print($"FOG_RESULT: {_failures} failures"); QuitAfterAudio(0);
    }
}
