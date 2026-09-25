using System;
using System.Linq;
using Godot;

/// <summary>The adventure map presents world sites; progression and rewards remain in GameState.</summary>
public partial class MapMenu : Control
{
    private MapPathCanvas _mapCanvas;
    private AdventureMapNode _selected;
    private string _activeMapId;
    private Label _mapTitle, _siteName, _siteEyebrow, _siteStatus, _description, _rewardText, _feedback, _intelText;
    private HBoxContainer _resources, _tabs;
    private TextureRect _portrait;
    private Button _action, _scout, _directive;
    private OptionButton _regions;
    private VBoxContainer _overview, _intel;
    private string _message = "Choose a leader or a landmark. Click terrain to roam; choose any rival.";

    public override void _Ready()
    {
        MedievalUi.Apply(this);
        _selected = AdventureMapCatalog.Leader(GameState.Instance.SelectedStage);
        _activeMapId = _selected.MapId;
        BuildUi();
        _mapCanvas.ShowMap(_activeMapId, _selected.Id);
        RefreshUi();
    }
    private void BuildUi()
    {
        var background = new ColorRect { Color = new Color("101c22"), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(background); background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var header = new HBoxContainer { Position = new Vector2(24,18), Size = new Vector2(1232,72) };
        header.AddThemeConstantOverride("separation", 14); AddChild(header);
        header.AddChild(RealmUi.IconButton("back", "Return to camp", () => SceneRouter.Instance.GoToMainMenu()));
        header.AddChild(new HeraldicEmblem { Symbol = "map" });
        var title = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        title.AddThemeConstantOverride("separation", 0); header.AddChild(title);
        title.AddChild(RealmUi.Label("THE LANTERN ATLAS", 12, true));
        _mapTitle = RealmUi.Heading("", 27); title.AddChild(_mapTitle);
        _regions = new OptionButton { CustomMinimumSize = new Vector2(245,48) };
        foreach (var map in GameData.Stages.Select(x => x.MapId).Distinct())
        {
            _regions.AddItem(RouteCatalog.Get(map).Title);
            _regions.SetItemMetadata(_regions.ItemCount - 1, map);
        }
        _regions.ItemSelected += index => SwitchRegion(_regions.GetItemMetadata((int)index).AsString());
        header.AddChild(_regions);
        _resources = new HBoxContainer(); header.AddChild(_resources);
        header.AddChild(RealmUi.IconButton("gear", "Settings", () => SceneRouter.Instance.GoToSettings()));

        var mapFrame = new PanelContainer { Position = new Vector2(20,106), Size = new Vector2(876,512) };
        mapFrame.AddThemeStyleboxOverride("panel", MedievalUi.Engraved("engraved_panel", 5, 5)); AddChild(mapFrame);
        _mapCanvas = new MapPathCanvas(); mapFrame.AddChild(_mapCanvas);
        _mapCanvas.SiteSelected += SelectSite;
        _mapCanvas.TravelStateChanged += RefreshUi;

        var side = RealmUi.Panel(this, new Rect2(912,106,348,512), out _);
        side.AddThemeConstantOverride("separation", 10);
        var encounter = new HBoxContainer(); encounter.AddThemeConstantOverride("separation", 12); side.AddChild(encounter);
        _portrait = new TextureRect { CustomMinimumSize = new Vector2(90,104), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, SizeFlagsVertical = SizeFlags.ShrinkBegin };
        encounter.AddChild(_portrait);
        var naming = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; naming.AddThemeConstantOverride("separation", 4); encounter.AddChild(naming);
        _siteEyebrow = RealmUi.Label("", 12, true); naming.AddChild(_siteEyebrow);
        _siteName = RealmUi.Heading("", 22); naming.AddChild(_siteName);
        _siteStatus = RealmUi.Label("", 14, true); side.AddChild(_siteStatus);
        _tabs = RealmUi.Tabs(side, SelectPage, "Overview", "Intel");
        _overview = RealmUi.Scroll(side); _overview.AddThemeConstantOverride("separation", 12);
        _description = RealmUi.Label(""); _overview.AddChild(_description);
        _rewardText = RealmUi.Label(""); _rewardText.AddThemeColorOverride("font_color", RealmUi.Gold); _overview.AddChild(_rewardText);
        _feedback = RealmUi.Label("", 14, true);
        _intel = RealmUi.Scroll(side); _intel.GetParent<ScrollContainer>().Visible = false;
        _intelText = RealmUi.Label("", 14); _intel.AddChild(_intelText);
        _directive = RealmUi.Button("shield", "Heroic directive", ToggleDirective); _intel.AddChild(_directive);
        side.AddChild(_feedback);
        _action = RealmUi.Button("flag", "Prepare battle", VisitSelected, true);
        _action.CustomMinimumSize = new Vector2(0,48); side.AddChild(_action);

        var footer = RealmUi.Panel(this, new Rect2(20,632,1240,72), out _);
        var controls = new HBoxContainer(); controls.AddThemeConstantOverride("separation", 10); footer.AddChild(controls);
        controls.AddChild(RealmUi.IconButton("sword", "Caravan armory", () => SceneRouter.Instance.GoToShop()));
        controls.AddChild(RealmUi.IconButton("flag", "Find my caravan", () => _mapCanvas.FocusCaravan()));
        controls.AddChild(RealmUi.IconButton("eye", "Zoom in", () => _mapCanvas.ChangeZoom(1.2f)));
        controls.AddChild(RealmUi.IconButton("map", "Zoom out", () => _mapCanvas.ChangeZoom(1 / 1.2f)));
        controls.AddChild(RealmUi.Label("Click to roam · Drag to pan", 14, true));
        controls.AddChild(RealmUi.IconButton("book", "How to explore", () => RealmUi.Details(this, "Explore the fallen kingdom", "Drag the terrain to pan. Scroll or pinch to zoom.\n\nClick anywhere on the terrain to move your caravan and lift the fog. All districts can be explored freely.\n\nChallenge regular leaders in any order. If a fight is too hard, explore another path or district. Defeat the five regular leaders in a district to open its final boss gate.\n\nGold and ration sites grant supplies once. Shrines grant +3 starting courage per shrine in this district. Watchtowers reveal a wider area.\n\nThe blue pennant is your caravan. All map travel is free. Only entering a battle costs food. Your position, rewards and revealed terrain are saved.")));
        _scout = RealmUi.Button("eye", "Explore", ScoutNextArea); _scout.CustomMinimumSize = new Vector2(215,48); controls.AddChild(_scout);
    }
    private void SelectPage(int index)
    {
        _overview.GetParent<ScrollContainer>().Visible = index == 0;
        _intel.GetParent<ScrollContainer>().Visible = index == 1;
    }
    private void SwitchRegion(string mapId)
    {
        if (_mapCanvas.IsTravelling) return;
        _activeMapId = mapId;
        _selected = GameState.Instance.GetAdventureHeroNode(mapId);
        _message = "Click terrain to explore. Challenge leaders in any order.";
        _mapCanvas.ShowMap(mapId, _selected.Id); SelectPage(0); _tabs.GetChild<Button>(0).ButtonPressed = true;
        RefreshUi();
    }
    private void SelectSite(AdventureMapNode site)
    {
        if (_mapCanvas.IsTravelling) return;
        _selected = site; _message = "";
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
        for (var i = 0; i < _regions.ItemCount; i++) if (_regions.GetItemMetadata(i).AsString() == _activeMapId) _regions.Select(i);
        _regions.Disabled = _mapCanvas.IsTravelling;
        RealmUi.Clear(_resources);
        _resources.AddChild(UiBadgeFactory.CreateRewardMetric("gold", "", state.Gold.ToString("N0"), new Vector2(24,24)));
        _resources.AddChild(UiBadgeFactory.CreateRewardMetric("food", "", state.Food.ToString("N0"), new Vector2(24,24)));
        _portrait.Texture = !known ? RealmUi.Icon("lock") : leader ? AdventureMapArt.Leader(_selected.Portrait) : AdventureMapArt.Miniature(_selected.Kind);
        _siteEyebrow.Text = !known ? "UNCHARTED" : leader ? $"{(boss ? "BOSS" : "RIVAL")} · STAGE {_selected.Stage:00}" : "LANDMARK";
        _siteName.Text = known ? _selected.Title : "Beyond the mist";
        _siteStatus.Text = !known ? "Travel here to discover it" : bossLocked ? $"Boss gate · {5 - state.GetAdventureBossRemainingLeaders(_selected.Stage)}/5 leaders defeated" : leader ? $"{stage.StageName} · {state.GetStageStars(_selected.Stage)}/3 stars" : visited ? "Visited · rewards collected" : "Discovered · ready to visit";
        _description.Text = known ? _selected.Description : "Travel through the mist to discover this location. You do not need to win a battle to explore.";
        _rewardText.Text = !known ? "" : leader ? $"Victory · {stage.RewardGold} gold + {stage.RewardFood} food\nEntry · {state.GetStageEntryFoodCost(_selected.Stage)} food\nShrine blessing · +{state.GetAdventureStartingCourageBonus(_selected.Stage)} courage" : _selected.Kind switch {
            AdventureSiteKind.Gold => $"{_selected.GoldReward} gold", AdventureSiteKind.Food => $"{_selected.FoodReward} food",
            AdventureSiteKind.Shrine => "+3 starting courage · this district", AdventureSiteKind.Watchtower => "Reveal nearby terrain", _ => "Safe haven · travel is free" };
        _feedback.Text = _message;
        if (known && leader)
        {
            _description.Text = $"Suggested core: level {CampaignProgressionCatalog.SuggestedLevel(_selected.Stage)}.\n\n" + _selected.Description;
            var progression = state.BuildProgressionRewardPreview(_selected.Stage);
            if (progression.Length > 0) _rewardText.Text = progression + "\n\n" + _rewardText.Text;
        }
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
        _action.Icon = RealmUi.Icon(leader ? "sword" : _selected.Icon);
        var unexplored = NextUnexploredSite();
        _scout.Disabled = _mapCanvas.IsTravelling || unexplored == null;
        _scout.Text = unexplored == null ? "Region charted" : "Explore";
        _scout.TooltipText = "Travel to nearby unexplored terrain for free. You can also click anywhere on the map.";
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
    private AdventureMapNode NextUnexploredSite() => AdventureMapCatalog.ForMap(_activeMapId)
        .Where(x => !GameState.Instance.IsAdventureSiteDiscovered(x.Id) && (string.IsNullOrEmpty(x.RequiredVisit) || GameState.Instance.HasVisitedAdventureSite(x.RequiredVisit)))
        .OrderBy(x => x.Point.DistanceSquaredTo(GameState.Instance.GetAdventureHeroPosition(_activeMapId))).FirstOrDefault();
    private void ScoutNextArea()
    {
        if (_mapCanvas.IsTravelling || NextUnexploredSite() is not { } destination) return;
        _selected = destination; _mapCanvas.SelectSite(destination.Id);
        _message = "Exploring new terrain…";
        _mapCanvas.TravelToPoint(destination.Point, () => { _message = "New terrain discovered. Choose your next challenge."; });
    }
    private void ToggleDirective()
    {
        GameState.Instance.ToggleCampaignDirective(_selected.Stage, out _message); RefreshUi();
    }
}
