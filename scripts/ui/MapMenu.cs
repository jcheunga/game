using System.Linq;
using Godot;

/// <summary>The adventure map presents world sites; progression and rewards remain in GameState.</summary>
public partial class MapMenu : Control
{
    private MapPathCanvas _mapCanvas;
    private AdventureMapNode _selected;
    // Every other stage in the zone must fall before its boss gate opens.
    private int BossGateLeaders => GameData.GetStagesForMap(GameData.GetStage(_selected.Stage).MapId).Count - 1;
    private AdventureDiscovery _selectedDiscovery;
    // A plain frontier tile chosen to be opened (its panel only shows when it can't be).
    private AdventureTile _selectedGround;
    private string _activeMapId;
    private RoyalLabel _mapTitle, _zoneProgress;
    private Label _siteName, _siteEyebrow, _siteStatus, _description;
    private VBoxContainer _rewards;
    private TextureRect _portrait;
    private Button _action, _previousZone, _nextZone;
    private PanelContainer _sitePanel;
    private VBoxContainer _overview;

    public override void _Ready()
    {
        MedievalUi.Apply(this);
        if (GameData.LoadFailed)
        {
            ShowDataLoadError();
            return;
        }
        _selected = AdventureMapCatalog.Leader(GameState.Instance.SelectedStage);
        _activeMapId = _selected.MapId;
        if (!GameState.Instance.IsAdventureZoneUnlocked(_activeMapId))
        {
            _activeMapId = GameData.Stages.First().MapId;
            _selected = AdventureTileCatalog.Starting(_activeMapId).Site;
        }
        if (!GameState.Instance.IsAdventureSiteDiscovered(_selected.Id))
            _selected = GameState.Instance.GetAdventureCaravanTile(_activeMapId).Site ?? AdventureTileCatalog.Starting(_activeMapId).Site;
        BuildUi();
        _mapCanvas.ShowMap(_activeMapId, _selected.Id);
        RefreshUi();
    }
    public string ActiveMapId => _activeMapId;

    /// <summary>Pages the atlas to the neighbouring zone (used by the caravan hub's zone plaque).</summary>
    public void StepZone(int direction) => ChangeZone(direction);

    private void SwitchRegion(string mapId)
    {
        if (_mapCanvas.IsTravelling || !GameState.Instance.IsAdventureZoneUnlocked(mapId)) return;
        _activeMapId = mapId;
        _selectedDiscovery = null; _selectedGround = null;
        _selected = GameState.Instance.GetAdventureCaravanTile(mapId).Site ?? AdventureTileCatalog.Starting(mapId).Site;
        GameState.Instance.SetSelectedStage(_selected.Stage);
        CloseSiteDetails();
        _mapCanvas.ShowMap(mapId, _selected.Id);
        RefreshUi();
    }
    private void SelectSite(AdventureMapNode site)
    {
        if (_mapCanvas.IsTravelling || !GameState.Instance.IsAdventureSiteDiscovered(site.Id)) return;
        var reward = site.Kind is AdventureSiteKind.Gold or AdventureSiteKind.Food;
        if (reward && GameState.Instance.HasVisitedAdventureSite(site.Id)) return;
        _selectedDiscovery = null; _selectedGround = null;
        _selected = site;
        if (site.Kind == AdventureSiteKind.Leader && GameState.Instance.CanVisitAdventureSite(site.Id)) GameState.Instance.SetSelectedStage(site.Stage);
        _mapCanvas.SelectSite(site.Id);
        RefreshUi();
        // A site that can be entered is entered at once: the caravan travels there and a battle opens its
        // preparation (which shows the rewards and entry cost). The details panel only appears to explain
        // why a site cannot be entered yet, such as a sealed boss gate.
        _sitePanel.Visible = _action.Disabled;
        if (!_action.Disabled) VisitSelected();
    }
    private void SelectDiscovery(AdventureDiscovery discovery)
    {
        var tile = AdventureTileCatalog.Find(_activeMapId, discovery.Id);
        var state = GameState.Instance;
        if (_mapCanvas.IsTravelling || !state.IsAdventureTileRevealed(tile) || state.HasClaimedAdventureDiscovery(discovery.Id)) return;
        _selectedDiscovery = discovery; _selectedGround = null;
        _mapCanvas.SelectSite(discovery.Id);
        var canTravel = state.CanTravelToAdventureTile(tile, out _);
        _sitePanel.Visible = !canTravel;
        RefreshUi();
        if (canTravel) VisitSelected();
    }
    /// <summary>A plain frontier tile opens at once for its food; without the food its panel explains the cost.</summary>
    private void SelectGround(AdventureTile tile)
    {
        var state = GameState.Instance;
        if (_mapCanvas.IsTravelling || !state.IsAdventureTileFrontier(tile)) return;
        _selectedGround = tile; _selectedDiscovery = null;
        _mapCanvas.SelectSite(tile.Id);
        var canOpen = state.CanTravelToAdventureTile(tile, out _);
        _sitePanel.Visible = !canOpen;
        RefreshUi();
        if (canOpen) VisitSelected();
    }
    private void RefreshGroundDetails()
    {
        var state = GameState.Instance;
        var canOpen = state.CanTravelToAdventureTile(_selectedGround, out var reason);
        _portrait.Texture = HomeMapArt.Icon("map");
        _siteEyebrow.Text = "Frontier";
        RealmUi.SetDisplayText(_siteName, _selectedGround.Title);
        _siteStatus.Text = state.IsAdventureTileOpened(_selectedGround) ? "Charted" : $"Costs {GameState.AdventureTileFoodCost} food to open";
        RealmUi.Clear(_rewards);
        _rewards.AddChild(RealmUi.Label("Reveals the land around it", 18, true));
        _description.Text = reason; _description.Visible = !canOpen;
        _action.Text = _mapCanvas.IsTravelling ? "Opening…" : "Open";
        _action.Icon = HomeMapArt.Icon("food");
        _action.Disabled = _mapCanvas.IsTravelling || !canOpen;
    }
    private void RefreshDiscoveryDetails()
    {
        var state = GameState.Instance;
        var tile = AdventureTileCatalog.Find(_activeMapId, _selectedDiscovery.Id);
        var canTravel = state.CanTravelToAdventureTile(tile, out var reason);
        _portrait.Texture = HomeMapArt.Icon(_selectedDiscovery.Icon);
        _siteEyebrow.Text = "Resource tile";
        RealmUi.SetDisplayText(_siteName, _selectedDiscovery.Title);
        _siteStatus.Text = state.HasClaimedAdventureDiscovery(tile.Id) ? "Collected" : $"Frontier · costs {GameState.AdventureTileFoodCost} food to open";
        ShowDiscoveryRewards();
        _description.Text = reason; _description.Visible = !canTravel;
        _action.Text = _mapCanvas.IsTravelling ? "Collecting…" : "Collect";
        _action.Icon = HomeMapArt.Icon(_selectedDiscovery.Icon);
        _action.Disabled = _mapCanvas.IsTravelling || !canTravel;
    }
    private void RefreshUi()
    {
        QueueSitePanelFit();
        var state = GameState.Instance;
        var known = state.IsAdventureSiteDiscovered(_selected.Id);
        var boss = _selected.Kind == AdventureSiteKind.Leader && state.IsAdventureBoss(_selected.Stage);
        var bossLocked = boss && !state.IsCampaignStageUnlocked(_selected.Stage);
        var leader = _selected.Kind == AdventureSiteKind.Leader;
        var visited = state.HasVisitedAdventureSite(_selected.Id);
        var stage = GameData.GetStage(_selected.Stage);
        var tile = AdventureTileCatalog.Find(_activeMapId, _selected.Id);
        _mapTitle.Text = RouteCatalog.Get(_activeMapId).Title.Replace("'", "’");
        RefreshZoneNavigation();
        _resources.SetValues(state.Gold.ToString("N0"), $"{state.Food} / {GameState.FoodRechargeCap}", state.TotalStarsEarned.ToString());
        _foodHint.TooltipText = state.FoodRechargeText;
        if (_developerPanel != null) _developerPanel.Visible = state.DeveloperModeEnabled;
        if (_selectedGround != null)
        {
            RefreshGroundDetails();
            _mapCanvas.RefreshKnowledge();
            return;
        }
        if (_selectedDiscovery != null)
        {
            RefreshDiscoveryDetails();
            _mapCanvas.RefreshKnowledge();
            return;
        }
        _portrait.Texture = !known ? RealmUi.Icon("lock") : leader ? AdventureMapArt.Leader(_selected.Portrait) : AdventureMapArt.Miniature(_selected.Kind);
        _siteEyebrow.Text = !known ? "Uncharted" : leader ? $"{(boss ? "Boss" : "Rival")} · Stage {_selected.Stage}" : "Landmark";
        RealmUi.SetDisplayText(_siteName, known ? _selected.Title : "Beyond the mist");
        _siteStatus.Text = !known ? "Open the land beside this tile to reach it" : bossLocked ? $"Boss gate · {BossGateLeaders - state.GetAdventureBossRemainingLeaders(_selected.Stage)}/{BossGateLeaders} leaders defeated" : leader ? $"{stage.StageName} · {state.GetStageStars(_selected.Stage)}/3 stars" : visited ? "Visited · rewards collected" : GameState.IsAdventureResourceTile(tile) ? $"Frontier · costs {GameState.AdventureTileFoodCost} food to open" : "Open tile · ready to visit";
        _description.Text = ""; _description.Visible = false;
        ShowSiteRewards(known, leader, stage);
        _action.Text = _mapCanvas.IsTravelling ? "Travelling…" : !known ? "Tile unopened" : bossLocked ? "Boss gate sealed" : leader ? "Prepare battle" : "Collect";
        _action.Disabled = !known || bossLocked || _mapCanvas.IsTravelling || (!string.IsNullOrEmpty(_selected.RequiredVisit) && !state.HasVisitedAdventureSite(_selected.RequiredVisit));
        if (known && !state.CanTravelToAdventureTile(tile, out var reason)) {
            _action.Disabled = true;
            // Keep battle entry requirements inside the selected site's details.
            _description.Text = reason; _description.Visible = true;
        }
        if (known && leader && !state.CanStartCampaignBattle(_selected.Stage, out var battleReason))
        {
            _action.Disabled = true;
            _description.Text = battleReason; _description.Visible = true;
        }
        _action.Icon = RealmUi.Icon(leader ? "sword" : _selected.Icon);

        _mapCanvas.RefreshKnowledge();
    }
    private void VisitSelected()
    {
        if (_action.Disabled) return;
        if (_selectedGround != null)
        {
            var ground = _selectedGround;
            _mapCanvas.TravelToTile(ground, () => { GameState.Instance.TryCollectAdventureTile(ground, out _); _selectedGround = null; RefreshUi(); });
            RefreshUi();
            return;
        }
        if (_selectedDiscovery != null)
        {
            var discoveryTile = AdventureTileCatalog.Find(_activeMapId, _selectedDiscovery.Id);
            _mapCanvas.TravelToTile(discoveryTile, () => { GameState.Instance.TryCollectAdventureTile(discoveryTile, out _); RefreshUi(); });
            RefreshUi();
            return;
        }
        var destination = _selected;
        if (!GameState.Instance.IsAdventureSiteDiscovered(destination.Id))
        {
            return;
        }
        _mapCanvas.TravelTo(destination, () => {
            if (!GameState.Instance.TryCollectAdventureTile(AdventureTileCatalog.Find(_activeMapId, destination.Id), out _)) { RefreshUi(); return; }
            if (destination.Kind == AdventureSiteKind.Leader)
            {
                GameState.Instance.PrepareCampaignBattle(); SceneRouter.Instance.GoToLoadout();
            }
            else { AudioDirector.Instance?.PlayUpgradeConfirm(); RefreshUi(); }
        });
        RefreshUi();
    }
    public override void _ExitTree()
    {
        if (GameState.Instance == null) return;
        GameState.Instance.FoodChanged -= RefreshUi;
        GameState.Instance.DeveloperStateChanged -= RefreshUi;
    }
    private void CloseSiteDetails()
    {
        _sitePanel.Hide();
        _selectedGround = null;
    }

    // Replaces the atlas when data/*.json is missing or corrupt. GameState refuses to save meanwhile.
    private void ShowDataLoadError()
    {
        var backdrop = new ColorRect { Color = new Color("0d0f14") };
        backdrop.SetAnchorsPreset(LayoutPreset.FullRect);
        MedievalUi.MarkBackdrop(backdrop);
        AddChild(backdrop);
        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(580, 0) };
        center.AddChild(panel);
        var padding = new MarginContainer();
        foreach (var side in new[] { "left", "right" }) padding.AddThemeConstantOverride($"margin_{side}", 28);
        foreach (var side in new[] { "top", "bottom" }) padding.AddThemeConstantOverride($"margin_{side}", 22);
        panel.AddChild(padding);
        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 18);
        padding.AddChild(stack);
        stack.AddChild(RealmUi.EmptyState("book", "Game data could not load",
            "Your saved progress is safe and unchanged. Reinstall or update the game, then try again."));
        var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        actions.AddThemeConstantOverride("separation", 12);
        actions.AddChild(RealmUi.Button("book", "Details", () => RealmUi.Details(this, "Load error", GameData.LoadError)));
        actions.AddChild(RealmUi.Button("close", "Quit game", () => GetTree().Quit(), primary: true));
        stack.AddChild(actions);
    }
}
