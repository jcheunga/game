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
        await Wait(.6); await Capture("fog-fresh"); CheckFogCover(canvas, "city fresh");
        canvas.ChangeZoom(.1f); canvas.FocusOverview(); await Wait(.3); await Capture("fog-overview");
        canvas.ChangeZoom(10f); canvas.FocusSite(AdventureTileCatalog.Stage("city", 1).Id); await Wait(.3); await Capture("fog-close");
        // Mid-campaign: explored land, the frontier's mist and the storm bank beyond it.
        SeedConceptSave(); await Open("MainMenu");
        canvas = Walk(GetTree().CurrentScene).OfType<MapPathCanvas>().First();
        canvas.ChangeZoom(.1f); canvas.FocusOverview(); await Wait(.3); await Capture("fog-progress"); CheckFogCover(canvas, "city progress");
        canvas.ChangeZoom(1.3f); canvas.FocusSite(AdventureTileCatalog.Stage("city", 7).Id); await Wait(.3); await Capture("fog-frontier");
        var zones = OS.GetCmdlineUserArgs().FirstOrDefault(arg => arg.StartsWith("--zones="))?["--zones=".Length..].Split(',')
            ?? new[] { "harbor", "foundry", "thornwall", "gloamwood" };
        GameState.Instance.ResetProgress();
        foreach (var zone in zones)
        {
            canvas.ShowMap(zone, AdventureTileCatalog.Starting(zone).Site.Id); await Wait(.3);
            canvas.ChangeZoom(.1f); canvas.FocusOverview(); await Wait(.3); await Capture("fog-" + zone); CheckFogCover(canvas, zone + " fresh");
        }
        GD.Print($"FOG_RESULT: {_failures} failures"); QuitAfterAudio(_failures == 0 ? 0 : 1);
    }

    /// <summary>Nothing beyond the frontier may show: every hidden tile lies wholly under the white, while the cloud's
    /// soft edge stays on the outer rim of the frontier tiles, so the land one tile out still reads.</summary>
    private void CheckFogCover(MapPathCanvas canvas, string label)
    {
        var (showing, covered, centres) = canvas.MeasureFog();
        Check(showing <= .01f, $"{label}: hidden land showing through the cloud {showing:P0} (worst tile)");
        Check(centres == 0 && covered <= .55f, $"{label}: frontier land under the cloud {covered:P0} (worst tile), {centres} centres covered");
    }
}
