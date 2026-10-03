using System;
using System.Linq;
using System.Collections.Generic;
using Godot;

/// <summary>The adventure map presents world sites; progression and rewards remain in GameState.</summary>
public partial class MapMenu : Control
{
    private MapPathCanvas _mapCanvas;
    private AdventureMapNode _selected;
    private AdventureDiscovery _selectedDiscovery;
    private string _activeMapId;
    private Label _mapTitle, _zoneProgress, _gold, _food, _stars, _siteName, _siteEyebrow, _siteStatus, _description;
    private VBoxContainer _rewards;
    private TextureRect _portrait;
    private Button _action, _directive, _previousZone, _nextZone;
    private PanelContainer _sitePanel;
    private VBoxContainer _overview;

    public override void _Ready()
    {
        MedievalUi.Apply(this);
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
    private void SwitchRegion(string mapId)
    {
        if (_mapCanvas.IsTravelling || !GameState.Instance.IsAdventureZoneUnlocked(mapId)) return;
        _activeMapId = mapId;
        _selectedDiscovery = null;
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
        _selectedDiscovery = null;
        _selected = site;
        var tile = AdventureTileCatalog.Find(site.MapId, site.Id);
        if (reward && GameState.Instance.CanTravelToAdventureTile(tile, out _)) _sitePanel.Hide();
        else _sitePanel.Show();
        if (site.Kind == AdventureSiteKind.Leader && GameState.Instance.CanVisitAdventureSite(site.Id)) GameState.Instance.SetSelectedStage(site.Stage);
        _mapCanvas.SelectSite(site.Id);
        RefreshUi();
        if (reward && GameState.Instance.IsAdventureSiteDiscovered(site.Id)) VisitSelected();
    }
    private void SelectDiscovery(AdventureDiscovery discovery)
    {
        var tile = AdventureTileCatalog.Find(_activeMapId, discovery.Id);
        var state = GameState.Instance;
        if (_mapCanvas.IsTravelling || !state.IsAdventureTileOpen(tile) || state.HasClaimedAdventureDiscovery(discovery.Id)) return;
        _selectedDiscovery = discovery;
        _mapCanvas.SelectSite(discovery.Id);
        var canTravel = state.CanTravelToAdventureTile(tile, out _);
        _sitePanel.Visible = !canTravel;
        RefreshUi();
        if (canTravel) VisitSelected();
    }
    private void RefreshDiscoveryDetails()
    {
        var state = GameState.Instance;
        var tile = AdventureTileCatalog.Find(_activeMapId, _selectedDiscovery.Id);
        var canTravel = state.CanTravelToAdventureTile(tile, out var reason);
        _portrait.Texture = HomeMapArt.Icon(_selectedDiscovery.Icon);
        _siteEyebrow.Text = "RESOURCE TILE";
        _siteName.Text = _selectedDiscovery.Title;
        _siteStatus.Text = state.HasClaimedAdventureDiscovery(tile.Id) ? "Collected" : "Open tile · ready to gather";
        ShowDiscoveryRewards();
        _description.Text = reason; _description.Visible = !canTravel;
        _directive.Visible = false;
        _action.Text = _mapCanvas.IsTravelling ? "Collecting…" : "Collect";
        _action.Icon = HomeMapArt.Icon(_selectedDiscovery.Icon);
        _action.Disabled = _mapCanvas.IsTravelling || !canTravel;
    }
    private void RefreshUi()
    {
        var state = GameState.Instance;
        var known = state.IsAdventureSiteDiscovered(_selected.Id);
        var boss = _selected.Kind == AdventureSiteKind.Leader && state.IsAdventureBoss(_selected.Stage);
        var bossLocked = boss && !state.IsCampaignStageUnlocked(_selected.Stage);
        var leader = _selected.Kind == AdventureSiteKind.Leader;
        var visited = state.HasVisitedAdventureSite(_selected.Id);
        var stage = state.BuildConfiguredCampaignStage(_selected.Stage);
        var tile = AdventureTileCatalog.Find(_activeMapId, _selected.Id);
        _mapTitle.Text = RouteCatalog.Get(_activeMapId).Title;
        RefreshZoneNavigation();
        _gold.Text = state.Gold.ToString("N0");
        _food.Text = $"{state.Food}/{GameState.FoodRechargeCap}";
        _food.GetParent().GetParent<Button>().TooltipText = state.FoodRechargeText;
        _stars.Text = state.TotalStarsEarned.ToString();
        if (_developerPanel != null) _developerPanel.Visible = state.DeveloperModeEnabled;
        if (_selectedDiscovery != null)
        {
            RefreshDiscoveryDetails();
            _mapCanvas.RefreshKnowledge();
            return;
        }
        _portrait.Texture = !known ? RealmUi.Icon("lock") : leader ? AdventureMapArt.Leader(_selected.Portrait) : AdventureMapArt.Miniature(_selected.Kind);
        _siteEyebrow.Text = !known ? "UNCHARTED" : leader ? $"{(boss ? "BOSS" : "RIVAL")} · STAGE {_selected.Stage:00}" : "LANDMARK";
        _siteName.Text = known ? _selected.Title : "Beyond the mist";
        _siteStatus.Text = !known ? "Complete a nearby site to open this tile" : bossLocked ? $"Boss gate · {5 - state.GetAdventureBossRemainingLeaders(_selected.Stage)}/5 leaders defeated" : leader ? $"{stage.StageName} · {state.GetStageStars(_selected.Stage)}/3 stars" : visited ? "Visited · rewards collected" : "Open tile · ready to visit";
        _description.Text = ""; _description.Visible = false;
        ShowSiteRewards(known, leader, stage);
        _directive.Visible = known && leader && state.IsCampaignDirectiveUnlocked(_selected.Stage);
        _directive.Disabled = _mapCanvas.IsTravelling || bossLocked;
        _directive.Text = state.IsCampaignDirectiveArmed(_selected.Stage) ? "Stand down directive" : "Heroic directive";
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
    private void ToggleDirective()
    {
        GameState.Instance.ToggleCampaignDirective(_selected.Stage, out _);
        RefreshUi();
    }

    private void CloseSiteDetails()
    {
        _sitePanel.Hide();
    }
}
