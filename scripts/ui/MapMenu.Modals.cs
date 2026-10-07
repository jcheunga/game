using System.Collections.Generic;
using Godot;

public partial class MapMenu
{
    private RealmModal _modal;
    private Control _modalReturnFocus;
    private readonly List<(string Path, int Tab)> _modalHistory = new();
    public bool HasHomeModal => _modal != null;
    public string HomeModalDestination => _modal?.Destination ?? "";

    public bool OpenHomeDestination(string path, bool remember = true)
    {
        if (path == SceneRouter.MainMenuScene || path == SceneRouter.MapScene)
        {
            if (!HasHomeModal) return false;
            CloseHomeModal(); return true;
        }
        if (path == SceneRouter.BattleScene) return false;
        CloseSiteDetails();
        if (_modal == null)
        {
            AudioDirector.Instance?.PlayModalOpen();
            _modalReturnFocus = GetViewport().GuiGetFocusOwner();
            _modal = new RealmModal(); AddChild(_modal);
            if (MobilePresentation.Enabled) _modal.UseMobileCanvas();
            _modal.Closed = CloseHomeModal; _modal.Back = BackHomeModal;
        }
        // Destinations with a score of their own (shop, loadout, endless, tourney) bring it with them.
        MusicPlayer.Instance?.PlayOverlay(path);
        var tab = path == SceneRouter.ShopScene ? SceneRouter.Instance.InitialShopTab : 0;
        if (remember) _modalHistory.Add((path, tab));
        // Closing a screen opened from another one (Edit squad from preparation) returns to that screen.
        _modal.Closed = _modalHistory.Count > 1 ? BackHomeModal : CloseHomeModal;
        RealmUi.Clear(_modal.Content);
        Control content;
        if (path == "achievements") content = new AchievementsPanel();
        else if (path == "more") content = new CaravanHub(this);
        else content = ResourceLoader.Load<PackedScene>(path).Instantiate<Control>();
        content.SetMeta("home_modal", true);
        if (content.HasMeta("royal_screen")) { _modal.PresentRoyal(path, content); _hud.Visible = false; return true; }
        // Screens without a concept of their own share the generic modal chrome.
        _modal.Content.AddChild(content);
        content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        if (content is not CashShopMenu) RealmModal.AdaptActivity(content);
        var title = path switch {
            SceneRouter.TowerScene => "Challenge tower", SceneRouter.BountyScene => "Daily bounties", SceneRouter.RaidScene => "Weekly raid",
            SceneRouter.EventScene => "Seasonal event", SceneRouter.ExpeditionScene => "Expeditions", SceneRouter.CashShopScene => "Royal storehouse",
            SceneRouter.LoginCalendarScene => "Daily gifts", SceneRouter.LanRaceScene => "LAN race", SceneRouter.ArenaScene => "Arena",
            SceneRouter.GuildScene => "Warband guild", SceneRouter.FriendsScene => "Friends", SceneRouter.LeaderboardScene => "Rankings",
            SceneRouter.ProfileScene => "Player profile", SceneRouter.SkillTreeScene => "Warband talents", _ => "Crownroad" };
        _modal.Present(path, title, "", _modalHistory.Count > 1, 1232);
        // The home controls would peek out around the frame's corners (the settings ring above its close button).
        _hud.Visible = false;

        return true;
    }

    public void CloseHomeModal()
    {
        if (_modal == null) return;
        var modal = _modal; _modal = null; RemoveChild(modal); modal.QueueFree(); _modalHistory.Clear();
        AudioDirector.Instance?.PlayModalClose();
        MusicPlayer.Instance?.EndOverlay();
        _hud.Visible = true;
        RefreshUi();
        if (GodotObject.IsInstanceValid(_modalReturnFocus) && _modalReturnFocus.IsInsideTree() && _modalReturnFocus.IsVisibleInTree()) _modalReturnFocus.GrabFocus();
        _modalReturnFocus = null;
    }

    public void BackHomeModal()
    {
        if (_modalHistory.Count <= 1) { CloseHomeModal(); return; }
        _modalHistory.RemoveAt(_modalHistory.Count - 1);
        var previous = _modalHistory[_modalHistory.Count - 1];
        SceneRouter.Instance.SetInitialShopTab(previous.Tab);
        OpenHomeDestination(previous.Path, false);
    }

}
