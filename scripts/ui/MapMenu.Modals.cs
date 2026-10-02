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
        if (path == SceneRouter.BattleScene || path == SceneRouter.BattleSummaryScene) return false;
        CloseSiteDetails();
        if (_modal == null)
        {
            _modalReturnFocus = GetViewport().GuiGetFocusOwner();
            _modal = new RealmModal(); AddChild(_modal);
            _modal.Closed = CloseHomeModal; _modal.Back = BackHomeModal;
        }
        var tab = path == SceneRouter.ShopScene ? SceneRouter.Instance.InitialShopTab : 0;
        if (remember) _modalHistory.Add((path, tab));
        RealmUi.Clear(_modal.Content);
        Control content;
        if (path == "achievements") content = new AchievementsPanel();
        else if (path == "more") content = BuildActivitiesPanel();
        else content = ResourceLoader.Load<PackedScene>(path).Instantiate<Control>();
        content.SetMeta("home_modal", true);
        _modal.Content.AddChild(content);
        content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        if (content is not ShopMenu && content is not SettingsMenu && content is not CodexMenu && content is not CashShopMenu && path != "achievements" && path != "more")
            RealmModal.AdaptActivity(content);
        var title = path switch {
            SceneRouter.ShopScene => tab == 0 ? "Warband" : tab == 1 ? "Spells" : "Upgrades",
            SceneRouter.SettingsScene => "Settings", SceneRouter.CodexScene => "The Crownroad codex",
            SceneRouter.EndlessScene => "Endless survival", SceneRouter.TowerScene => "Challenge tower",
            SceneRouter.BountyScene => "Daily bounties", SceneRouter.RaidScene => "Weekly raid",
            SceneRouter.EventScene => "Seasonal event", SceneRouter.ExpeditionScene => "Expeditions",
            SceneRouter.ForgeScene => "Relic forge", SceneRouter.CashShopScene => "Royal storehouse",
            SceneRouter.LoginCalendarScene => "Daily gifts", SceneRouter.SeasonPassScene => "Season rewards",
            SceneRouter.MultiplayerScene => "Multiplayer challenges", SceneRouter.LanRaceScene => "LAN race",
            SceneRouter.ArenaScene => "Arena", SceneRouter.GuildScene => "Warband guild", SceneRouter.FriendsScene => "Friends",
            SceneRouter.LeaderboardScene => "Rankings", SceneRouter.ProfileScene => "Player profile",
            SceneRouter.SkillTreeScene => "Warband talents", SceneRouter.LoadoutScene => "Prepare for battle",
            "achievements" => "Achievements", "more" => "The lantern caravan", _ => "Crownroad" };
        var subtitle = path == "achievements" ? $"{GameState.Instance.GetUnlockedAchievementCount()}/{AchievementCatalog.GetAll().Count} COMPLETE · {GameState.Instance.GetUnclaimedAchievementRewardCount()} REWARDS READY" : path == SceneRouter.CodexScene ? "FIELD NOTES · CREATURES, ALLIES & RELICS" : path == "more" ? "ADVENTURE · CARAVAN · COMMUNITY" : path == SceneRouter.SettingsScene ? "SOUND · GAMEPLAY · ONLINE · ACCOUNT" : path == SceneRouter.ShopScene ? $"{GameState.Instance.ActiveDeckUnitIds.Count}/{GameState.Instance.DeckSizeLimit} ALLIES · {GameState.Instance.ActiveDeckSpellIds.Count}/{GameState.Instance.SpellDeckSizeLimit} SPELLS EQUIPPED" : "CROWNROAD · YOUR JOURNEY";
        _modal.Present(path, title, subtitle, _modalHistory.Count > 1, path == SceneRouter.SettingsScene ? 840 : 1120);

        return true;
    }

    public void CloseHomeModal()
    {
        if (_modal == null) return;
        var modal = _modal; _modal = null; RemoveChild(modal); modal.QueueFree(); _modalHistory.Clear();
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

    private Control BuildActivitiesPanel()
    {
        var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 16);
        var sections = RealmUi.Tabs(root, ShowDestinations, "Adventure", "Caravan", "Community");
        RealmModal.Polish(sections);
        var scroll = RealmUi.Scroll(root);
        _destinations = new GridContainer { Columns = 3 };
        _destinations.AddThemeConstantOverride("h_separation", 14); _destinations.AddThemeConstantOverride("v_separation", 14);
        scroll.AddChild(_destinations);
        var footer = new HBoxContainer(); footer.AddThemeConstantOverride("separation", 12); root.AddChild(footer);
        footer.AddChild(RealmUi.Button("people", "Account", () => AccountDialog.Show(this)));
        footer.AddChild(RealmUi.Button("star", "Player profile", () => SceneRouter.Instance.GoToProfile()));
        footer.AddChild(RealmUi.Button("book", "How to explore", ShowExplorationHelp));
        footer.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        footer.AddChild(HomeMapUi.IconButton("close", "Quit game", () => MedievalUi.ShowConfirmation(this, "Leave Crownroad?", "Your progress is saved.", "Quit", () => GetTree().Quit())));
        RealmModal.Polish(footer); ShowDestinations(0); return root;
    }
}
