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
        bool NoBottomNotice() => !Walk(menu).OfType<Control>().Any(control => control.Name == "TravelNotice")
            && !Walk(menu).OfType<Button>().Any(button => button.AccessibilityName == "Dismiss map notice");
        Check(NoBottomNotice(), "The map does not create a bottom message popup");
        var fixture = state.BuildSaveData();
        fixture.AdventureOpenTiles = fixture.AdventureOpenTiles.Append(boss.Id).ToArray();
        typeof(GameState).GetMethod("ApplySavedData", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(state, new object[] { fixture });
        select.Invoke(menu, new object[] { boss }); await Wait(.15);
        Check(panel.Visible && Walk(panel).OfType<Button>().Any(button => button.Disabled && button.Text == "Boss gate sealed"), "The locked boss still explains its gate and blocks entry");
        AuditText("Home / boss gate details"); await Capture("boss-gate-sealed");
        var close = Walk(panel).OfType<Button>().Single(button => button.AccessibilityName == "Close site details");
        await TapModal(close);
        refresh.Invoke(menu, null); await Wait(.1);
        Check(!panel.Visible && NoBottomNotice(), "Closing boss details removes the sealed-gate warning through subsequent refreshes");
        await Capture("boss-gate-dismissed");
        select.Invoke(menu, new object[] { boss }); await Wait(.1);
        Send(new InputEventKey { Pressed = true, Keycode = Key.Escape }); await Wait(.1);
        refresh.Invoke(menu, null);
        Check(!panel.Visible && NoBottomNotice(), "Escape dismisses the boss warning without it returning");
        select.Invoke(menu, new object[] { boss }); await Wait(.1);
        SceneRouter.Instance.GoToSettings(); await Wait(.15); menu.CloseHomeModal();
        Check(!panel.Visible && NoBottomNotice(), "Opening and closing settings cannot revive the sealed-gate popup");

        var food = state.Food; var knowledge = state.AdventureKnowledgeRevision;
        canvas.TravelToPoint(new Vector2(-10000, -10000)); await Wait(.1);
        Check(NoBottomNotice() && state.Food == food && state.AdventureKnowledgeRevision == knowledge, "Rejected travel does not display a popup or change resources and exploration");
        AuditText("Home / no bottom messages"); await Capture("map-no-bottom-messages");
        refresh.Invoke(menu, null);
        canvas.TravelToPoint(new Vector2(-10000, -10000)); await Wait(.1);
        canvas.TravelToPoint(new Vector2(-10000, -10000)); await Wait(.1);
        refresh.Invoke(menu, null);
        Check(NoBottomNotice(), "Repeated travel messages and refreshes cannot recreate the bottom popup");
        Send(new InputEventKey { Pressed = true, Keycode = Key.Escape }); await Wait(.1);
        Check(state.Food == food && state.AdventureKnowledgeRevision == knowledge && !state.IsCampaignStageUnlocked(boss.Stage), "Removing messages leaves resources, exploration and the boss gate unchanged");

        typeof(GameState).GetMethod("ApplySavedData", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(state, new object[] { baseline });
        var camp = AdventureMapCatalog.ForMap("city").First();
        select.Invoke(menu, new object[] { camp });
        canvas.ShowMap("city", camp.Id);
        close.EmitSignal(BaseButton.SignalName.Pressed);
        refresh.Invoke(menu, null);
        await Wait(.15); // Let ShowMap's deferred tile focus finish before the next drag check.
    }
}
