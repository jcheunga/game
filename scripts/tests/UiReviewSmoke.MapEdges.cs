using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// Drags and flicks each charted zone map toward all eight edges with touch, as a phone sends it, and captures where
/// it stops into artifacts/map-edges: the view's centre stays on the island, the screen stays on the painting, and
/// the sea beyond the coast fades into its flat silhouette.
///   --map-edges [--zones=city,harbor]
/// </summary>
public partial class UiReviewSmoke
{
    private async Task ReviewMapEdges()
    {
        _output = ProjectSettings.GlobalizePath("res://artifacts/map-edges");
        System.IO.Directory.CreateDirectory(_output);
        var state = GameState.Instance;
        state.ResetProgress(); state.SetShowHints(false);
        // Every tile open, so the edge is judged against land rather than the fog bank.
        var charted = state.BuildSaveData();
        charted.AdventureOpenTiles = GameData.Stages.Select(stage => stage.MapId).Distinct().SelectMany(AdventureTileCatalog.ForMap).Select(tile => tile.Id).ToArray();
        typeof(GameState).GetMethod("ApplySavedData", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(state, new object[] { charted });
        await Open("MainMenu");
        var canvas = Walk(GetTree().CurrentScene).OfType<MapPathCanvas>().Single();
        var centre = canvas.GlobalPosition + new Vector2(canvas.Size.X * .5f, canvas.Size.Y * .42f);
        var zones = OS.GetCmdlineUserArgs().FirstOrDefault(arg => arg.StartsWith("--zones="))?["--zones=".Length..].Split(',')
            ?? new[] { "city", "harbor", "foundry", "gloamwood" };
        var compass = new[] { ("n", new Vector2(0, -1)), ("ne", new Vector2(1, -1)), ("e", new Vector2(1, 0)), ("se", new Vector2(1, 1)),
            ("s", new Vector2(0, 1)), ("sw", new Vector2(-1, 1)), ("w", new Vector2(-1, 0)), ("nw", new Vector2(-1, -1)) };
        void Stroke(Vector2 from, Vector2 to, int steps)
        {
            Send(new InputEventScreenTouch { Index = 0, Pressed = true, Position = from });
            for (var step = 1; step <= steps; step++)
                Send(new InputEventScreenDrag { Index = 0, Position = from.Lerp(to, step / (float)steps), Relative = (to - from) / steps });
            Send(new InputEventScreenTouch { Index = 0, Pressed = false, Position = to });
        }
        void Hold(string label)
        {
            var (off, onPainting, water) = canvas.MeasureView();
            Check(off < 1 && onPainting, $"{label}: the view stays on the island ({off:0} off) and the painting ({onPainting}), {water:P0} open water");
        }
        foreach (var zone in zones)
        {
            canvas.ShowMap(zone, AdventureTileCatalog.Starting(zone).Site.Id); await Wait(.2);
            canvas.ChangeZoom(.85f / canvas.Zoom);
            foreach (var (name, heading) in compass)
            {
                // Swipe after swipe toward the edge: the land under the finger moves away from the heading.
                canvas.FocusOverview(); await Wait(.05);
                var direction = heading.Normalized();
                for (var swipe = 0; swipe < 10; swipe++) Stroke(centre + direction * 170, centre - direction * 170, 12);
                await Wait(.1); Hold($"{zone} {name}"); await Capture($"edge-{zone}-{name}");
            }
            // At the edge the map answers a drag back at once, with no slack to take up first (past the tap's first step).
            var before = canvas.MapOffset;
            Stroke(centre, centre + new Vector2(-90, -90), 6);
            Check((canvas.MapOffset - before).IsEqualApprox(new Vector2(-75, -75)), $"{zone}: a drag back from the edge moves the map at once ({canvas.MapOffset - before})");
            // A hard flick glides out to the edge and stops there.
            canvas.FocusOverview(); await Wait(.05);
            var at = centre;
            Send(new InputEventScreenTouch { Index = 0, Pressed = true, Position = at });
            for (var step = 0; step < 6; step++) { Send(new InputEventScreenDrag { Index = 0, Position = at + new Vector2(60, 40), Relative = new Vector2(60, 40) }); at += new Vector2(60, 40); await Wait(.016); }
            Send(new InputEventScreenTouch { Index = 0, Pressed = false, Position = at });
            await Wait(1.2); Hold($"{zone} flick"); await Capture($"flick-{zone}");
            canvas.ChangeZoom(.001f); canvas.FocusOverview(); await Wait(.1); Hold($"{zone} overview"); await Capture($"overview-{zone}");
            canvas.ChangeZoom(100); canvas.FocusSite(AdventureTileCatalog.Stage(zone, 9).Id);
            for (var swipe = 0; swipe < 12; swipe++) Stroke(centre - new Vector2(170, 0), centre + new Vector2(170, 0), 12);
            await Wait(.1); Hold($"{zone} close west"); await Capture($"close-{zone}-w");
        }
        GD.Print($"MAP_EDGES_RESULT: {_failures} failures"); QuitAfterAudio(_failures == 0 ? 0 : 1);
    }
}
