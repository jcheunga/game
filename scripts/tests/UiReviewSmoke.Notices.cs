using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

public partial class UiReviewSmoke
{
    private async Task ReviewMapNotices(MapMenu menu, MapPathCanvas canvas)
    {
        var state = GameState.Instance;
        var baseline = state.BuildSaveData();
        var select = typeof(MapMenu).GetMethod("SelectSite", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var refresh = typeof(MapMenu).GetMethod("RefreshUi", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var boss = AdventureMapCatalog.ForMap("city").Last(site => site.Kind == AdventureSiteKind.Leader);
        var panel = menu.GetNode<PanelContainer>("HomeHud/SelectedSite");
        var fixture = state.BuildSaveData();
        fixture.AdventureOpenTiles = fixture.AdventureOpenTiles.Append(boss.Id).ToArray();
        typeof(GameState).GetMethod("ApplySavedData", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(state, new object[] { fixture });
        select.Invoke(menu, new object[] { boss }); await Wait(.15);
        Check(panel.Visible && Walk(panel).OfType<Button>().Any(button => button.Disabled && button.Text == "Boss gate sealed"), "The locked boss still explains its gate and blocks entry");
        AuditText("Home / boss gate details"); await Capture("boss-gate-sealed");
        var close = Walk(panel).OfType<Button>().Single(button => button.AccessibilityName == "Close site details");
        // The compact close takes a fingertip: the tap lands just above its drawn rim.
        var reach = TouchReach.Rect(close).Size * close.GetGlobalTransformWithCanvas().Scale;
        var drawn = close.GetGlobalRect();
        var above = new Vector2(drawn.GetCenter().X, drawn.Position.Y - 6);
        Send(new InputEventMouseButton { Pressed = true, ButtonIndex = MouseButton.Left, Position = above, GlobalPosition = above });
        Send(new InputEventMouseButton { Pressed = false, ButtonIndex = MouseButton.Left, Position = above, GlobalPosition = above });
        await Wait(.3);
        refresh.Invoke(menu, null); await Wait(.1);
        Check(reach.X >= TouchReach.Size - .5f && reach.Y >= TouchReach.Size - .5f && !panel.Visible,
            "A tap just outside the compact close still closes boss details, and the sealed-gate warning stays away through refreshes");
        await Capture("boss-gate-dismissed");
        select.Invoke(menu, new object[] { boss }); await Wait(.1);
        Send(new InputEventKey { Pressed = true, Keycode = Key.Escape }); await Wait(.1);
        refresh.Invoke(menu, null);
        Check(!panel.Visible, "Escape dismisses the boss warning without it returning");
        select.Invoke(menu, new object[] { boss }); await Wait(.1);
        SceneRouter.Instance.GoToSettings(); await Wait(.15); menu.CloseHomeModal();
        Check(!panel.Visible, "Opening and closing settings cannot revive the sealed-gate popup");

        var food = state.Food; var knowledge = state.AdventureKnowledgeRevision;
        canvas.TravelToTile(AdventureTileCatalog.Stage("city", 9), null); await Wait(.1);
        Check(state.Food == food && state.AdventureKnowledgeRevision == knowledge, "Rejected travel changes no resources or exploration");
        AuditText("Home / rejected travel"); await Capture("map-rejected-travel");
        Send(new InputEventKey { Pressed = true, Keycode = Key.Escape }); await Wait(.1);
        Check(state.Food == food && state.AdventureKnowledgeRevision == knowledge && !state.IsCampaignStageUnlocked(boss.Stage), "Rejected travel leaves resources, exploration and the boss gate unchanged");

        typeof(GameState).GetMethod("ApplySavedData", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(state, new object[] { baseline });
        // Back to the city map with nothing selected (selecting a playable stage would open its preparation).
        canvas.ShowMap("city", "");
        close.EmitSignal(BaseButton.SignalName.Pressed);
        refresh.Invoke(menu, null);
        await Wait(.15); // Let ShowMap's deferred tile focus finish before the next drag check.
    }
}
