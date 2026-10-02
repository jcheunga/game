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
    private Label _mapTitle, _zoneProgress, _gold, _food, _stars, _siteName, _siteEyebrow, _siteStatus, _description, _rewardText, _intelText;
    private HBoxContainer _tabs;
    private TextureRect _portrait;
    private Button _action, _scout, _directive, _previousZone, _nextZone;
    private PanelContainer _sitePanel;
    private VBoxContainer _overview, _intel;

    public override void _Ready()
    {
        MedievalUi.Apply(this);
        _selected = AdventureMapCatalog.Leader(GameState.Instance.SelectedStage);
        _activeMapId = _selected.MapId;
        if (!GameState.Instance.IsAdventureZoneUnlocked(_activeMapId))
        {
            _activeMapId = GameData.Stages.First().MapId;
            _selected = GameState.Instance.GetAdventureHeroNode(_activeMapId);
            GameState.Instance.SetSelectedStage(_selected.Stage);
        }
        if (!GameState.Instance.IsAdventureSiteDiscovered(_selected.Id)) _selected = GameState.Instance.GetAdventureHeroNode(_activeMapId);
        BuildUi();
        _mapCanvas.ShowMap(_activeMapId, _selected.Id);
        RefreshUi();
    }
    private void SelectPage(int index)
    {
        _overview.GetParent<ScrollContainer>().Visible = index == 0;
        _intel.GetParent<ScrollContainer>().Visible = index == 1;
    }
    private void SwitchRegion(string mapId)
    {
        if (_mapCanvas.IsTravelling || !GameState.Instance.IsAdventureZoneUnlocked(mapId)) return;
        _activeMapId = mapId;
        _selectedDiscovery = null;
        _selected = GameState.Instance.GetAdventureHeroNode(mapId);
        GameState.Instance.SetSelectedStage(_selected.Stage);
        CloseSiteDetails();
        _mapCanvas.ShowMap(mapId, _selected.Id); SelectPage(0); _tabs.GetChild<Button>(0).ButtonPressed = true;
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
        _mapCanvas.SelectSite(site.Id); SelectPage(0); _tabs.GetChild<Button>(0).ButtonPressed = true;
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
        SelectPage(0); _tabs.GetChild<Button>(0).ButtonPressed = true;
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
        _rewardText.Text = $"{_selectedDiscovery.RewardText}\nTravel · {state.GetAdventureTileTravelFoodCost(tile)} food\nOpens surrounding tiles";
        _description.Text = reason; _description.Visible = !canTravel;
        _intelText.Text = "Gather these supplies once to open the surrounding tiles. Travel to a new destination costs 1 food.";
        _directive.Visible = false;
        _action.Text = _mapCanvas.IsTravelling ? "Travelling…" : "Travel & gather";
        _action.Icon = RealmUi.Icon(_selectedDiscovery.Icon);
        _action.Disabled = _mapCanvas.IsTravelling || !canTravel;
        _scout.Disabled = _mapCanvas.IsTravelling;
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
        var travelCost = state.GetAdventureTileTravelFoodCost(tile);
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
        _rewardText.Text = !known ? "" : leader ? $"Victory · {stage.RewardGold} gold\nTravel {travelCost} · Entry {state.GetStageEntryFoodCost(_selected.Stage)} food" : _selected.Kind switch {
            AdventureSiteKind.Gold => $"{_selected.GoldReward} gold", AdventureSiteKind.Food => $"{_selected.FoodReward} food",
            AdventureSiteKind.Shrine => "+3 starting courage · this district", AdventureSiteKind.Watchtower => "Open two rings of nearby tiles", _ => "Safe haven · return travel is free" };
        if (known && !leader) _rewardText.Text += $"\nTravel · {travelCost} food\nOpens surrounding tiles";
        _overview.MoveChild(_rewardText, 0);
        _intelText.Text = !known ? "Explore this district to learn about its inhabitants." : !leader ? _selected.Description + "\n\n" + (visited ? "This landmark remains charted on your map." : "Travel here to claim its benefit. This site can be claimed once per campaign.") :
            $"{CampaignProgressionCatalog.Preparation(_selected.Stage)}\n\n{stage.Description}\n\n{StageObjectives.BuildSummaryText(stage, state.GetStageStars(stage.StageNumber))}\n\n{StageMissionEvents.BuildCampaignSummaryText(stage)}\n\n{StageModifiers.BuildSummaryText(stage)}\n\n{WeatherCatalog.BuildInlineSummary(stage)}\n\n{StageEncounterIntel.BuildEncounterIntel(stage)}\n\n{state.BuildCampaignDirectiveStatusText(stage.StageNumber)}\n\n{state.BuildCampaignScoutStatusText(stage.StageNumber)}";
        _directive.Visible = known && leader;
        _directive.Disabled = !state.IsCampaignDirectiveUnlocked(_selected.Stage);
        _directive.Text = state.IsCampaignDirectiveArmed(_selected.Stage) ? "Stand down directive" : "Heroic directive";
        _action.Text = _mapCanvas.IsTravelling ? "Travelling…" : !known ? "Tile unopened" : bossLocked ? "Boss gate sealed" : leader ? "Prepare battle" : visited ? "Travel here" : _selected.Kind switch {
            AdventureSiteKind.Gold or AdventureSiteKind.Food => "Travel & gather", AdventureSiteKind.Shrine => "Kindle shrine", AdventureSiteKind.Watchtower => "Scout from tower", _ => "Return to camp" };
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
        _scout.Disabled = _mapCanvas.IsTravelling;
        _scout.Text = "Map guide";

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
