using System;
using System.Linq;
using System.Collections.Generic;
using Godot;

/// <summary>The adventure map presents world sites; progression and rewards remain in GameState.</summary>
public partial class MapMenu : Control
{
    private MapPathCanvas _mapCanvas;
    private AdventureMapNode _selected;
    private string _activeMapId;
    private Label _mapTitle, _zoneProgress, _gold, _food, _stars, _siteName, _siteEyebrow, _siteStatus, _description, _rewardText, _feedback, _intelText;
    private HBoxContainer _tabs;
    private TextureRect _portrait;
    private Button _action, _scout, _directive, _previousZone, _nextZone;
    private PanelContainer _sitePanel, _feedbackPanel;
    private VBoxContainer _overview, _intel;
    private string _message = "";

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
        _selected = GameState.Instance.GetAdventureHeroNode(mapId);
        GameState.Instance.SetSelectedStage(_selected.Stage);
        _message = "";
        _sitePanel.Hide();
        _mapCanvas.ShowMap(mapId, _selected.Id); SelectPage(0); _tabs.GetChild<Button>(0).ButtonPressed = true;
        RefreshUi();
    }
    private void SelectSite(AdventureMapNode site)
    {
        if (_mapCanvas.IsTravelling) return;
        _selected = site; _message = "";
        _sitePanel.Show();
        if (site.Kind == AdventureSiteKind.Leader && GameState.Instance.CanVisitAdventureSite(site.Id)) GameState.Instance.SetSelectedStage(site.Stage);
        _mapCanvas.SelectSite(site.Id); SelectPage(0); _tabs.GetChild<Button>(0).ButtonPressed = true;
        RefreshUi();
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
        _mapTitle.Text = RouteCatalog.Get(_activeMapId).Title;
        RefreshZoneNavigation();
        _gold.Text = state.Gold.ToString("N0");
        _food.Text = $"{state.Food}/{GameState.FoodRechargeCap}";
        _food.GetParent().GetParent<Button>().TooltipText = state.FoodRechargeText;
        _stars.Text = state.TotalStarsEarned.ToString();
        _portrait.Texture = !known ? RealmUi.Icon("lock") : leader ? AdventureMapArt.Leader(_selected.Portrait) : AdventureMapArt.Miniature(_selected.Kind);
        _siteEyebrow.Text = !known ? "UNCHARTED" : leader ? $"{(boss ? "BOSS" : "RIVAL")} · STAGE {_selected.Stage:00}" : "LANDMARK";
        _siteName.Text = known ? _selected.Title : "Beyond the mist";
        _siteStatus.Text = !known ? "Travel here to discover it" : bossLocked ? $"Boss gate · {5 - state.GetAdventureBossRemainingLeaders(_selected.Stage)}/5 leaders defeated" : leader ? $"{stage.StageName} · {state.GetStageStars(_selected.Stage)}/3 stars" : visited ? "Visited · rewards collected" : "Discovered · ready to visit";
        _description.Text = ""; _description.Visible = false;
        _rewardText.Text = !known ? "" : leader ? $"Victory · {stage.RewardGold} gold\nEntry · {state.GetStageEntryFoodCost(_selected.Stage)} food" : _selected.Kind switch {
            AdventureSiteKind.Gold => $"{_selected.GoldReward} gold", AdventureSiteKind.Food => $"{_selected.FoodReward} food",
            AdventureSiteKind.Shrine => "+3 starting courage · this district", AdventureSiteKind.Watchtower => "Reveal nearby terrain", _ => "Safe haven · walked routes free" };
        _feedback.Text = _message;
        _overview.MoveChild(_rewardText, 0);
        _intelText.Text = !known ? "Explore this district to learn about its inhabitants." : !leader ? _selected.Description + "\n\n" + (visited ? "This landmark remains charted on your map." : "Travel here to claim its benefit. This site can be claimed once per campaign.") :
            $"{CampaignProgressionCatalog.Preparation(_selected.Stage)}\n\n{stage.Description}\n\n{StageObjectives.BuildSummaryText(stage, state.GetStageStars(stage.StageNumber))}\n\n{StageMissionEvents.BuildCampaignSummaryText(stage)}\n\n{StageModifiers.BuildSummaryText(stage)}\n\n{WeatherCatalog.BuildInlineSummary(stage)}\n\n{StageEncounterIntel.BuildEncounterIntel(stage)}\n\n{state.BuildCampaignDirectiveStatusText(stage.StageNumber)}\n\n{state.BuildCampaignScoutStatusText(stage.StageNumber)}";
        _directive.Visible = known && leader;
        _directive.Disabled = !state.IsCampaignDirectiveUnlocked(_selected.Stage);
        _directive.Text = state.IsCampaignDirectiveArmed(_selected.Stage) ? "Stand down directive" : "Heroic directive";
        _action.Text = _mapCanvas.IsTravelling ? "Travelling…" : !known ? "Explore here" : bossLocked ? "Boss gate sealed" : leader ? "Prepare battle" : visited ? "Travel here" : _selected.Kind switch {
            AdventureSiteKind.Gold or AdventureSiteKind.Food => "Travel & gather", AdventureSiteKind.Shrine => "Kindle shrine", AdventureSiteKind.Watchtower => "Scout from tower", _ => "Return to camp" };
        _action.Disabled = bossLocked || _mapCanvas.IsTravelling || (!string.IsNullOrEmpty(_selected.RequiredVisit) && !state.HasVisitedAdventureSite(_selected.RequiredVisit));
        if (leader && known && !state.CanStartCampaignBattle(_selected.Stage, out var reason)) { _action.Disabled = true; _feedback.Text = reason; }
        _feedback.Visible = !string.IsNullOrWhiteSpace(_feedback.Text);
        _feedbackPanel.Visible = _feedback.Visible;
        _action.Icon = RealmUi.Icon(leader ? "sword" : _selected.Icon);
        var frontier = NextFrontierCell();
        _scout.Disabled = _mapCanvas.IsTravelling || frontier < 0;
        var path = frontier < 0 ? Array.Empty<int>() : AdventureTerrain.Path(_activeMapId, AdventureTerrain.Cell(state.GetAdventureHeroPosition(_activeMapId)), frontier);
        var cost = state.GetAdventureTravelFoodCost(_activeMapId, path);
        _scout.Text = frontier < 0 ? "Charted" : $"Explore · {cost} food";

        _mapCanvas.RefreshKnowledge();
    }
    private void VisitSelected()
    {
        if (_action.Disabled) return;
        var destination = _selected;
        if (!GameState.Instance.IsAdventureSiteDiscovered(destination.Id))
        {
            _mapCanvas.TravelToPoint(destination.Point, () => { _message = "Location discovered. Choose whether to visit or challenge it."; });
            return;
        }
        _mapCanvas.TravelTo(destination, () => {
            if (!GameState.Instance.TryVisitAdventureSite(destination.Id, out _message)) { RefreshUi(); return; }
            if (destination.Kind == AdventureSiteKind.Leader)
            {
                GameState.Instance.PrepareCampaignBattle(); SceneRouter.Instance.GoToLoadout();
            }
            else { AudioDirector.Instance?.PlayUpgradeConfirm(); RefreshUi(); }
        });
        RefreshUi();
    }
    // Seek the nearest edge of actual knowledge, without exposing hidden landmark positions.
    private int NextFrontierCell()
    {
        var state = GameState.Instance;
        var start = AdventureTerrain.Cell(state.GetAdventureHeroPosition(_activeMapId));
        var seen = new HashSet<int> { start }; var queue = new Queue<int>(); queue.Enqueue(start);
        while (queue.TryDequeue(out var cell))
        {
            if (!state.IsAdventureCellRevealed(_activeMapId,cell)) return cell;
            foreach (var next in AdventureTerrain.Neighbors(cell).OrderBy(n => n))
                if (AdventureTerrain.Walkable(_activeMapId,next) && seen.Add(next)) queue.Enqueue(next);
        }
        return -1;
    }
    private void ScoutNextArea()
    {
        if (_mapCanvas.IsTravelling) return;
        var frontier = NextFrontierCell(); if (frontier < 0) return;
        _message = "";
        _mapCanvas.TravelToPoint(AdventureTerrain.Point(frontier), () => {
            if (string.IsNullOrEmpty(_message)) _message = "New ground charted.";
        });
    }
    public override void _ExitTree() { if (GameState.Instance != null) GameState.Instance.FoodChanged -= RefreshUi; }
    private void ToggleDirective()
    {
        GameState.Instance.ToggleCampaignDirective(_selected.Stage, out _message); RefreshUi();
    }
}
