using System;
using System.Collections.Generic;
using Godot;

public partial class MapMenu : Control
{
    private readonly Dictionary<int, Button> _stageButtons = new();

    private MenuBackdropSet _menuBackdrop = null!;
    private ColorRect _backgroundTop = null!;
    private ColorRect _backgroundBottom = null!;
    private ColorRect _accentBand = null!;
    private ColorRect _mapBackdropFallback = null!;
    private TextureRect _mapBackdropTexture = null!;
    private ColorRect _mapBackdropScrim = null!;
    private PanelContainer _topBar = null!;
    private PanelContainer _mapPanel = null!;
    private PanelContainer _sidePanel = null!;
    private OptionButton _mapSelector = null!;
    private HBoxContainer _resourcesRow = null!;
    private Label _resultLabel = null!;
    private Label _convoySummaryLabel = null!;
    private Label _squadSummaryLabel = null!;
    private Label _deployStatusLabel = null!;
    private Label _stageNameLabel = null!;
    private Label _stageDescriptionLabel = null!;
    private Label _stageStatusLabel = null!;
    private HBoxContainer _stageRewardRow = null!;
    private Label _stageRewardLabel = null!;
    private Label _stageObjectivesLabel = null!;
    private Label _stageMissionLabel = null!;
    private Label _stageModifiersLabel = null!;
    private Label _stageWeatherLabel = null!;
    private Label _stageIntelLabel = null!;
    private PanelContainer _routeBannerPanel = null!;
    private Label _routeTitleLabel = null!;
    private Label _routeSubtitleLabel = null!;
    private Label _routeCampaignLabel = null!;
    private Label _routeProgressLabel = null!;
    private Button _directiveButton = null!;
    private Button _exploreButton = null!;
    private Button _deployButton = null!;
    private MapPathCanvas _mapCanvas = null!;

    private int _selectedStage = 1;
    private string _activeMapId = "city";
    private string _convoyStatusMessage = "Use Caravan Armory to recruit units, fortify the war wagon, and set a 3-card squad.";

    public override void _Ready()
    {
        _selectedStage = Mathf.Clamp(GameState.Instance.SelectedStage, 1, GameState.Instance.MaxStage);
        _activeMapId = GetMapIdForStage(_selectedStage);
        BuildUi();
        BuildMapSelectorItems();
        TryShowMenuHint();
        RefreshUi();
        AppRatingPrompt.TryShowOn(this);
    }

    private void TryShowMenuHint()
    {
        if (!GameState.Instance.ShowHints)
        {
            return;
        }

        var hints = TutorialHintCatalog.GetByContext("first_map");
        foreach (var hint in hints)
        {
            if (GameState.Instance.HasSeenHint(hint.Id))
            {
                continue;
            }

            _convoyStatusMessage = $"[{hint.Title}] {hint.Body}";
            GameState.Instance.MarkHintSeen(hint.Id);
        }
    }

    private void BuildUi()
    {
        _menuBackdrop = MenuBackdropComposer.AddSplitBackdrop(
            this,
            "map",
            new Color("1b263b"),
            new Color("0d1b2a"),
            new Color("ffd166"),
            92f,
            _activeMapId);
        _backgroundTop = _menuBackdrop.PrimaryRect;
        _backgroundBottom = _menuBackdrop.SecondaryRect;
        _accentBand = _menuBackdrop.AccentBand;

        _topBar = new PanelContainer
        {
            Position = new Vector2(20, 18),
            Size = new Vector2(1240, 72)
        };
        AddChild(_topBar);

        var topRow = new HBoxContainer();
        topRow.AddThemeConstantOverride("separation", 12);
        _topBar.AddChild(topRow);

        var titleLabel = new Label
        {
            Text = "Campaign Routes",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center
        };
        topRow.AddChild(titleLabel);

        _mapSelector = new OptionButton
        {
            CustomMinimumSize = new Vector2(220f, 0f)
        };
        _mapSelector.ItemSelected += OnMapSelected;
        topRow.AddChild(_mapSelector);

        _resourcesRow = new HBoxContainer();
        _resourcesRow.AddThemeConstantOverride("separation", 12);
        topRow.AddChild(_resourcesRow);

        _mapPanel = new PanelContainer
        {
            Position = new Vector2(20, 106),
            Size = new Vector2(820, 592)
        };
        AddChild(_mapPanel);

        var mapArea = new Control();
        mapArea.SetAnchorsPreset(LayoutPreset.FullRect);
        _mapPanel.AddChild(mapArea);

        _mapBackdropFallback = new ColorRect
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _mapBackdropFallback.SetAnchorsPreset(LayoutPreset.FullRect);
        mapArea.AddChild(_mapBackdropFallback);

        _mapBackdropTexture = new TextureRect
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            Visible = false
        };
        _mapBackdropTexture.SetAnchorsPreset(LayoutPreset.FullRect);
        mapArea.AddChild(_mapBackdropTexture);

        _mapBackdropScrim = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0.22f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
        _mapBackdropScrim.SetAnchorsPreset(LayoutPreset.FullRect);
        mapArea.AddChild(_mapBackdropScrim);

        _mapCanvas = new MapPathCanvas();
        _mapCanvas.SetAnchorsPreset(LayoutPreset.FullRect);
        mapArea.AddChild(_mapCanvas);

        _routeBannerPanel = new PanelContainer
        {
            Position = new Vector2(18f, 18f),
            Size = new Vector2(324f, 182f)
        };
        mapArea.AddChild(_routeBannerPanel);

        var routeBannerPadding = new MarginContainer();
        routeBannerPadding.AddThemeConstantOverride("margin_left", 16);
        routeBannerPadding.AddThemeConstantOverride("margin_right", 16);
        routeBannerPadding.AddThemeConstantOverride("margin_top", 14);
        routeBannerPadding.AddThemeConstantOverride("margin_bottom", 14);
        _routeBannerPanel.AddChild(routeBannerPadding);

        var routeBannerStack = new VBoxContainer();
        routeBannerStack.AddThemeConstantOverride("separation", 6);
        routeBannerPadding.AddChild(routeBannerStack);

        _routeTitleLabel = new Label();
        routeBannerStack.AddChild(_routeTitleLabel);

        _routeSubtitleLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        routeBannerStack.AddChild(_routeSubtitleLabel);

        _routeCampaignLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        routeBannerStack.AddChild(_routeCampaignLabel);

        _routeProgressLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        routeBannerStack.AddChild(_routeProgressLabel);

        foreach (var stage in GameData.Stages)
        {
            var stageId = stage.StageNumber;
            var point = stage.MapPoint;
            _mapCanvas.StagePoints[stageId] = point;
            _mapCanvas.StageMapIds[stageId] = NormalizeMapId(stage.MapId);

            var stageButton = new Button
            {
                Text = stageId.ToString(),
                Position = point - new Vector2(34f, 34f),
                Size = new Vector2(68f, 68f)
            };
            stageButton.AddThemeColorOverride("font_color", Colors.White);
            stageButton.AddThemeColorOverride("font_hover_color", Colors.White);
            stageButton.AddThemeColorOverride("font_pressed_color", Colors.White);
            stageButton.AddThemeColorOverride("font_disabled_color", new Color(1f, 1f, 1f, 0.5f));

            stageButton.Pressed += () => SelectStage(stageId);
            mapArea.AddChild(stageButton);
            _stageButtons[stageId] = stageButton;
        }

        _sidePanel = new PanelContainer
        {
            Position = new Vector2(860, 106),
            Size = new Vector2(400, 592)
        };
        AddChild(_sidePanel);

        var sidePadding = new MarginContainer();
        sidePadding.AddThemeConstantOverride("margin_left", 18);
        sidePadding.AddThemeConstantOverride("margin_right", 18);
        sidePadding.AddThemeConstantOverride("margin_top", 18);
        sidePadding.AddThemeConstantOverride("margin_bottom", 18);
        _sidePanel.AddChild(sidePadding);

        var sideScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        sidePadding.AddChild(sideScroll);

        var sideContent = new VBoxContainer();
        sideContent.AddThemeConstantOverride("separation", 12);
        sideContent.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        sideScroll.AddChild(sideContent);

        _stageNameLabel = new Label();
        sideContent.AddChild(_stageNameLabel);

        _stageDescriptionLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0f, 96f)
        };
        sideContent.AddChild(_stageDescriptionLabel);

        _stageStatusLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        sideContent.AddChild(_stageStatusLabel);

        _stageRewardRow = new HBoxContainer();
        _stageRewardRow.AddThemeConstantOverride("separation", 8);
        sideContent.AddChild(_stageRewardRow);

        _stageRewardLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        sideContent.AddChild(_stageRewardLabel);

        _stageObjectivesLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        sideContent.AddChild(_stageObjectivesLabel);

        _stageMissionLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        sideContent.AddChild(_stageMissionLabel);

        _stageModifiersLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        sideContent.AddChild(_stageModifiersLabel);

        _stageWeatherLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        sideContent.AddChild(_stageWeatherLabel);

        _stageIntelLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        sideContent.AddChild(_stageIntelLabel);

        var convoyTitle = new Label
        {
            Text = "Caravan Readiness"
        };
        sideContent.AddChild(convoyTitle);

        _convoySummaryLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0f, 112f)
        };
        sideContent.AddChild(_convoySummaryLabel);

        _squadSummaryLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0f, 112f)
        };
        sideContent.AddChild(_squadSummaryLabel);

        _deployStatusLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0f, 76f)
        };
        sideContent.AddChild(_deployStatusLabel);

        _resultLabel = new Label
        {
            Text = "",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0f, 80f)
        };
        sideContent.AddChild(_resultLabel);

        var shopButton = new Button
        {
            Text = "Open Caravan Armory",
            CustomMinimumSize = new Vector2(0, 46)
        };
        shopButton.Pressed += () => SceneRouter.Instance.GoToShop();
        sideContent.AddChild(shopButton);

        var cashShopButton = new Button
        {
            Text = "Royal Storehouse",
            CustomMinimumSize = new Vector2(0, 42)
        };
        cashShopButton.Pressed += () => SceneRouter.Instance.GoToCashShop();
        sideContent.AddChild(cashShopButton);

        var settingsButton = new Button
        {
            Text = "Settings",
            CustomMinimumSize = new Vector2(0, 42)
        };
        settingsButton.Pressed += () => SceneRouter.Instance.GoToSettings();
        sideContent.AddChild(settingsButton);

        _directiveButton = new Button
        {
            Text = "Heroic Directive",
            CustomMinimumSize = new Vector2(0, 42)
        };
        _directiveButton.Pressed += ToggleSelectedDirective;
        sideContent.AddChild(_directiveButton);

        _exploreButton = new Button
        {
            Text = "Explore Next Stage",
            CustomMinimumSize = new Vector2(0, 46)
        };
        _exploreButton.Pressed += ExploreNextStage;
        sideContent.AddChild(_exploreButton);

        _deployButton = new Button
        {
            Text = "Deploy",
            CustomMinimumSize = new Vector2(0, 56)
        };
        _deployButton.Pressed += StartSelectedStage;
        sideContent.AddChild(_deployButton);

        var titleButton = new Button
        {
            Text = "Back To Title",
            CustomMinimumSize = new Vector2(0, 44)
        };
        titleButton.Pressed += () => SceneRouter.Instance.GoToMainMenu();
        sideContent.AddChild(titleButton);
    }

    private void SelectStage(int stageId)
    {
        _selectedStage = Mathf.Clamp(stageId, 1, GameState.Instance.MaxStage);
        _activeMapId = GetMapIdForStage(_selectedStage);
        SyncMapSelectorSelection();
        GameState.Instance.SetSelectedStage(_selectedStage);
        RefreshUi();
    }

    private void RefreshUi()
    {
        _selectedStage = Mathf.Clamp(_selectedStage, 1, GameState.Instance.MaxStage);

        if (!IsStageOnMap(_selectedStage, _activeMapId))
        {
            _selectedStage = FindPreferredStageForActiveMap();
        }

        RebuildResourcesRow();
        _resultLabel.Text = $"Last report:\n{GameState.Instance.LastResultMessage}";
        RefreshRouteBanner();

        _mapCanvas.ActiveMapId = _activeMapId;
        _mapCanvas.HighestUnlockedStage = GameState.Instance.HighestUnlockedStage;
        _mapCanvas.SelectedStage = _selectedStage;
        _mapCanvas.QueueRedraw();

        foreach (var pair in _stageButtons)
        {
            var stageId = pair.Key;
            var button = pair.Value;
            var onActiveMap = IsStageOnMap(stageId, _activeMapId);
            var stars = GameState.Instance.GetStageStars(stageId);
            var stageDefinition = GameData.GetStage(stageId);

            button.Visible = onActiveMap;
            button.Disabled = stageId > GameState.Instance.HighestUnlockedStage;
            ApplyStageButtonStyle(button, stageDefinition, stars, stageId <= GameState.Instance.HighestUnlockedStage, stageId == _selectedStage);
        }

		AnimateSidePanelRefresh();
        var stage = GameState.Instance.BuildConfiguredCampaignStage(_selectedStage);
        ApplyRouteTheme(stage);
        var stageUnlocked = _selectedStage <= GameState.Instance.HighestUnlockedStage;
        var bestStars = GameState.Instance.GetStageStars(_selectedStage);
        var stageEntryFoodCost = GameState.Instance.GetStageEntryFoodCost(_selectedStage);
        _stageNameLabel.Text = $"{stage.MapName} - Stage {_selectedStage}: {stage.StageName}";
        _stageDescriptionLabel.Text = stage.Description;
        _stageStatusLabel.Text = BuildStageStatusText(stage, bestStars, stageUnlocked);
        RebuildStageRewardRow(stage.RewardGold, stage.RewardFood, stageEntryFoodCost);
        _stageRewardLabel.Text =
            $"Terrain: {stage.TerrainId}\n" +
            $"{GameState.Instance.BuildDistrictRewardStatusText(stage.MapId)}\n" +
            $"{GameState.Instance.BuildCampaignDirectiveStatusText(_selectedStage)}";
        _stageObjectivesLabel.Text = StageObjectives.BuildSummaryText(stage, bestStars);
        _stageMissionLabel.Text = StageMissionEvents.BuildCampaignSummaryText(stage);
        _stageModifiersLabel.Text = StageModifiers.BuildSummaryText(stage);
        _stageWeatherLabel.Text = WeatherCatalog.BuildStageSummary(stage);
        _stageIntelLabel.Text = StageEncounterIntel.BuildCampaignCompactSummary(stage);
        _convoySummaryLabel.Text = BuildConvoySummaryText();
        _squadSummaryLabel.Text = BuildSquadSummaryText();
        RefreshDirectiveButton();
        _deployButton.Text = $"Deploy To Stage {_selectedStage} (-{stageEntryFoodCost} food)";
        var canStartBattle = GameState.Instance.CanStartCampaignBattle(_selectedStage, out var deployValidationMessage);
        _deployStatusLabel.Text =
            $"Caravan orders:\n{_convoyStatusMessage}\n" +
            $"Deploy readiness: {deployValidationMessage}";

        _deployButton.Disabled =
            _selectedStage > GameState.Instance.HighestUnlockedStage ||
            !canStartBattle;

        if (GameState.Instance.CanExploreNextStage(out var nextStage, out var exploreMessage))
        {
            _exploreButton.Text = $"Explore Stage {nextStage.StageNumber} (-{GameState.Instance.GetStageExploreFoodCost(nextStage.StageNumber)} food)";
            _exploreButton.Disabled = false;
            _exploreButton.TooltipText = $"{nextStage.MapName}: {nextStage.StageName}\n{nextStage.Description}";
        }
        else
        {
            _exploreButton.Disabled = true;
            _exploreButton.TooltipText = exploreMessage;
            _exploreButton.Text = GameState.Instance.HighestUnlockedStage >= GameState.Instance.MaxStage
                ? "Route Fully Explored"
                : $"Explore Locked ({GameState.Instance.GetStageExploreFoodCost(GameState.Instance.HighestUnlockedStage + 1)} food)";
        }
    }

    private void StartSelectedStage()
    {
        if (_selectedStage > GameState.Instance.HighestUnlockedStage)
        {
            return;
        }

        if (!GameState.Instance.CanStartCampaignBattle(_selectedStage, out var message))
        {
            _convoyStatusMessage = message;
            RefreshUi();
            return;
        }

        GameState.Instance.SetSelectedStage(_selectedStage);
        GameState.Instance.PrepareCampaignBattle();
        SceneRouter.Instance.GoToLoadout();
    }

    private void RebuildStageRewardRow(int rewardGold, int rewardFood, int entryFoodCost)
    {
        foreach (var child in _stageRewardRow.GetChildren())
        {
            child.QueueFree();
        }

        if (rewardGold > 0)
        {
            _stageRewardRow.AddChild(CreateRewardChip("gold", "", $"+{rewardGold} Gold"));
        }

        if (rewardFood > 0)
        {
            _stageRewardRow.AddChild(CreateRewardChip("food", "", $"+{rewardFood} Food"));
        }

        if (entryFoodCost > 0)
        {
            _stageRewardRow.AddChild(CreateRewardChip("food", "", $"-{entryFoodCost} Food Entry"));
        }
    }

    private static HBoxContainer CreateRewardChip(string rewardType, string rewardItemId, string text)
    {
        var chip = new HBoxContainer();
        chip.AddThemeConstantOverride("separation", 6);
        chip.AddChild(UiBadgeFactory.CreateRewardBadge(rewardType, rewardItemId, text, new Vector2(28f, 28f)));
        chip.AddChild(new Label
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center
        });
        return chip;
    }

    private void BuildMapSelectorItems()
    {
        _mapSelector.Clear();

        var seenMapIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var stage in GameData.Stages)
        {
            var mapId = NormalizeMapId(stage.MapId);
            if (seenMapIds.Contains(mapId))
            {
                continue;
            }

            seenMapIds.Add(mapId);
            var label = string.IsNullOrWhiteSpace(stage.MapName) ? mapId : stage.MapName;
            var index = _mapSelector.ItemCount;
            _mapSelector.AddItem(label);
            _mapSelector.SetItemMetadata(index, mapId);
        }

        if (_mapSelector.ItemCount == 0)
        {
            return;
        }

        SyncMapSelectorSelection();
    }

    private void OnMapSelected(long index)
    {
        if (index < 0 || index >= _mapSelector.ItemCount)
        {
            return;
        }

        _activeMapId = NormalizeMapId(_mapSelector.GetItemMetadata((int)index).AsString());
        _selectedStage = FindPreferredStageForActiveMap();
        if (_selectedStage <= GameState.Instance.HighestUnlockedStage)
        {
            GameState.Instance.SetSelectedStage(_selectedStage);
        }

        RefreshUi();
    }

    private void ExploreNextStage()
    {
        GameState.Instance.TryExploreNextStage(out var message);
        _convoyStatusMessage = message;
        _selectedStage = Mathf.Clamp(GameState.Instance.SelectedStage, 1, GameState.Instance.MaxStage);
        _activeMapId = GetMapIdForStage(_selectedStage);
        SyncMapSelectorSelection();
        RefreshUi();
    }

    private int FindPreferredStageForActiveMap()
    {
        var firstInMap = 0;
        var highestUnlockedInMap = 0;

        foreach (var stage in GameData.Stages)
        {
            if (!IsStageOnMap(stage.StageNumber, _activeMapId))
            {
                continue;
            }

            if (firstInMap == 0)
            {
                firstInMap = stage.StageNumber;
            }

            if (stage.StageNumber <= GameState.Instance.HighestUnlockedStage)
            {
                highestUnlockedInMap = stage.StageNumber;
            }
        }

        if (highestUnlockedInMap > 0)
        {
            return highestUnlockedInMap;
        }

        if (firstInMap > 0)
        {
            return firstInMap;
        }

        return Mathf.Clamp(GameState.Instance.SelectedStage, 1, GameState.Instance.MaxStage);
    }

    private bool IsStageOnMap(int stageId, string mapId)
    {
        var stageMapId = GetMapIdForStage(stageId);
        return stageMapId.Equals(NormalizeMapId(mapId), StringComparison.OrdinalIgnoreCase);
    }

    private string GetMapIdForStage(int stageId)
    {
        var stage = GameData.GetStage(stageId);
        return NormalizeMapId(stage.MapId);
    }

    private void SyncMapSelectorSelection()
    {
        var normalizedActiveMap = NormalizeMapId(_activeMapId);
        for (var i = 0; i < _mapSelector.ItemCount; i++)
        {
            var itemMapId = NormalizeMapId(_mapSelector.GetItemMetadata(i).AsString());
            if (!itemMapId.Equals(normalizedActiveMap, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            _mapSelector.Select(i);
            _activeMapId = itemMapId;
            return;
        }

        _mapSelector.Select(0);
        _activeMapId = NormalizeMapId(_mapSelector.GetItemMetadata(0).AsString());
    }

    private static string NormalizeMapId(string mapId)
    {
        return RouteCatalog.Normalize(mapId);
    }

    private void AnimateSidePanelRefresh()
    {
        if (_sidePanel == null)
        {
            return;
        }

        _sidePanel.Modulate = new Color(1f, 1f, 1f, 0.5f);
        var tween = CreateTween();
        tween.TweenProperty(_sidePanel, "modulate:a", 1f, 0.15f)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
    }

    private void RebuildResourcesRow()
    {
        foreach (var child in _resourcesRow.GetChildren())
        {
            child.QueueFree();
        }

        _resourcesRow.AddChild(UiBadgeFactory.CreateRewardMetric("gold", "", GameState.Instance.Gold.ToString("N0"), new Vector2(24f, 24f)));
        _resourcesRow.AddChild(UiBadgeFactory.CreateRewardMetric("food", "", GameState.Instance.Food.ToString("N0"), new Vector2(24f, 24f)));
    }

    private void ApplyRouteTheme(StageDefinition stage)
    {
        var route = RouteCatalog.Get(stage?.MapId ?? _activeMapId);
        _backgroundTop.Color = route.BackgroundTop;
        _backgroundBottom.Color = route.BackgroundBottom;
        _accentBand.Color = route.BannerAccent;
        _menuBackdrop.SetTexture(UiTextureLoader.TryLoadScreenBackground("map", route.Id));
        _topBar.SelfModulate = route.BannerPanel.Lightened(0.08f);
        _mapPanel.SelfModulate = route.BannerPanel.Lerp(Colors.White, 0.06f);
        _sidePanel.SelfModulate = route.BannerPanel.Darkened(0.04f);
        _routeBannerPanel.SelfModulate = route.BannerPanel;
        UpdateMapBackdrop(route);

        _stageNameLabel.AddThemeColorOverride("font_color", route.BannerAccent);
        _stageRewardLabel.AddThemeColorOverride("font_color", route.Accent.Lightened(0.12f));
        _deployStatusLabel.AddThemeColorOverride("font_color", route.Accent.Lightened(0.26f));
    }

    private void UpdateMapBackdrop(RouteDefinition route)
    {
        _mapBackdropFallback.Color = route.BackgroundTop.Lerp(route.BackgroundBottom, 0.45f);
        var texture = UiTextureLoader.TryLoadMapBackground(route.Id);
        var hasTexture = texture != null;
        _mapBackdropTexture.Texture = texture;
        _mapBackdropTexture.Visible = hasTexture;
        _mapBackdropScrim.Visible = hasTexture;
    }

    private void RefreshRouteBanner()
    {
        var route = RouteCatalog.Get(_activeMapId);
        var totalStages = 0;
        var unlockedStages = 0;
        var completedStages = 0;
        var earnedStars = 0;

        foreach (var stage in GameData.Stages)
        {
            if (!IsStageOnMap(stage.StageNumber, _activeMapId))
            {
                continue;
            }

            totalStages++;
            if (stage.StageNumber <= GameState.Instance.HighestUnlockedStage)
            {
                unlockedStages++;
            }

            if (GameState.Instance.GetStageStars(stage.StageNumber) > 0)
            {
                completedStages++;
            }

            earnedStars += GameState.Instance.GetStageStars(stage.StageNumber);
        }

        _routeTitleLabel.Text = route.Title;
        _routeTitleLabel.AddThemeColorOverride("font_color", Colors.White);
        _routeSubtitleLabel.Text = route.CampaignSubtitle;
        _routeSubtitleLabel.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 0.82f));
        _routeCampaignLabel.Text =
            $"{CampaignPlanCatalog.BuildRoutePlanSummary(_activeMapId)}\n" +
            $"{GameState.Instance.BuildDistrictRewardStatusText(_activeMapId)}";
        _routeCampaignLabel.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 0.72f));
        var nextExploreText = TryGetNextStageForMap(_activeMapId, out var nextStage)
            ? $"   |   Next explore: S{nextStage.StageNumber} ({GameState.Instance.GetStageExploreFoodCost(nextStage.StageNumber)} food)"
            : "   |   Route fully explored";
        _routeProgressLabel.Text =
            $"Route progress: {completedStages}/{Mathf.Max(1, totalStages)} cleared   |   " +
            $"{unlockedStages}/{Mathf.Max(1, totalStages)} unlocked   |   " +
            $"Stars: {earnedStars}/{Mathf.Max(1, totalStages) * 3}" +
            nextExploreText;
        _routeProgressLabel.AddThemeColorOverride("font_color", route.BannerAccent);
        _routeBannerPanel.SelfModulate = route.BannerPanel;
    }

    private void ApplyStageButtonStyle(Button button, StageDefinition stage, int stars, bool unlocked, bool selected)
    {
        var route = RouteCatalog.Get(stage.MapId);
        var label = selected ? $"[{stage.StageNumber:00}]" : $"{stage.StageNumber:00}";
        if (stars > 0)
        {
            label += $"\n{new string('*', stars)}";
        }
        else if (!unlocked)
        {
            label += "\nLOCK";
        }
        else
        {
            label += "\nDEPLOY";
        }

        button.Text = label;
        var enemyCallouts = StageEncounterIntel.BuildNotableEnemyCallouts(stage);
        var bossPhaseWarning = StageEncounterIntel.BuildBossPhaseWarning(stage);
        var weatherInline = WeatherCatalog.BuildInlineSummary(stage);
        var missionInline = StageMissionEvents.BuildCampaignInlineSummary(stage);
        button.TooltipText =
            $"{stage.MapName} - Stage {stage.StageNumber}: {stage.StageName}\n" +
            $"Threat: {StageEncounterIntel.ResolveThreatRating(stage)}  |  Stars: {stars}/3\n" +
            $"{stage.Description.Split('\n')[0]}\n" +
            $"{StageEncounterIntel.BuildSupportPressureSummary(stage)}\n" +
            (bossPhaseWarning.Length > 0 ? $"{bossPhaseWarning}\n" : "") +
            $"Battlefield events: {missionInline}" +
            (weatherInline.Length > 0 ? $"\nWeather: {weatherInline}" : "") +
            (enemyCallouts.Length > 0 ? $"\n{enemyCallouts}" : "") +
            $"\n{GameState.Instance.BuildCampaignScoutStatusText(stage.StageNumber)}" +
            $"\n{GameState.Instance.BuildCampaignMissionOutcomeStatusText(stage.MapId)}" +
            $"\n{GameState.Instance.BuildCampaignFieldOrderStatusText(stage.MapId)}" +
            $"\n{GameState.Instance.BuildCampaignConvoyCommandStatusText(stage.MapId)}" +
            $"\n{GameState.Instance.BuildCampaignAdaptiveWaveStatusText(stage.StageNumber, stage.MapId)}" +
            $"\n{GameState.Instance.BuildCampaignLateConditionStatusText(stage.StageNumber, stage.MapId)}";

        button.SelfModulate = !unlocked
            ? route.BannerPanel.Darkened(0.35f)
            : selected
                ? route.BannerAccent
                : stars > 0
                    ? route.BannerAccent.Lerp(Colors.White, 0.22f)
                    : route.BannerPanel.Lightened(0.22f);
    }

    private string BuildStageStatusText(StageDefinition stage, int bestStars, bool unlocked)
    {
        var stageState = !unlocked
            ? "Locked"
            : bestStars > 0
                ? "Cleared"
                : "Ready";
        var waveStatus = stage.HasScriptedWaves
            ? $"{stage.Waves.Length} scripted waves"
            : "dynamic pressure";
        var readiness = GameState.Instance.BuildCampaignReadinessInlineSummary(stage.StageNumber);
        var bossPhaseWarning = StageEncounterIntel.BuildBossPhaseWarning(stage);

        return
            $"Stage status: {stageState}  |  Best stars: {bestStars}/3\n" +
            $"Threat rating: {StageEncounterIntel.ResolveThreatRating(stage)}  |  Pressure: {waveStatus}  |  Entry: {GameState.Instance.GetStageEntryFoodCost(stage.StageNumber)} food\n" +
            $"{StageEncounterIntel.BuildSupportPressureSummary(stage)}\n" +
            (bossPhaseWarning.Length > 0 ? $"{bossPhaseWarning}\n" : "") +
            $"{readiness}\n" +
            $"{GameState.Instance.BuildCampaignScoutStatusText(stage.StageNumber)}\n" +
            $"{GameState.Instance.BuildCampaignFieldOrderStatusText(stage.MapId)}\n" +
            $"{GameState.Instance.BuildCampaignMomentumStatusText()}\n" +
            $"{GameState.Instance.BuildCampaignConvoyCommandStatusText(stage.MapId)}\n" +
            $"{GameState.Instance.BuildCampaignRouteDoctrineStatusText(stage.StageNumber, stage.MapId)}\n" +
            $"{GameState.Instance.BuildCampaignMissionOutcomeStatusText(stage.MapId)}\n" +
            $"{GameState.Instance.BuildCampaignCounterSurgeStatusText(stage.MapId)}\n" +
            $"{GameState.Instance.BuildCampaignReserveStatusText()}\n" +
            $"{GameState.Instance.BuildCampaignRouteSupportStatusText(stage.MapId)}\n" +
            $"{GameState.Instance.BuildCampaignAdaptiveWaveStatusText(stage.StageNumber, stage.MapId)}\n" +
            $"{GameState.Instance.BuildCampaignLateConditionStatusText(stage.StageNumber, stage.MapId)}";
    }

    private string BuildConvoySummaryText()
    {
        var ownedUnits = GameState.Instance.GetOwnedPlayerUnits().Count;
        var eligibleDoctrineCount = GameState.Instance.GetEligibleUnitDoctrineCount();
        var hullLevel = GameState.Instance.GetBaseUpgradeLevel(BaseUpgradeCatalog.HullPlatingId);
        var pantryLevel = GameState.Instance.GetBaseUpgradeLevel(BaseUpgradeCatalog.PantryId);
        var dispatchLevel = GameState.Instance.GetBaseUpgradeLevel(BaseUpgradeCatalog.DispatchConsoleId);
        var relayLevel = GameState.Instance.GetBaseUpgradeLevel(BaseUpgradeCatalog.SignalRelayId);
        var nextExploreLine = TryGetNextStageForMap(_activeMapId, out var nextStage)
            ? $"Stage {nextStage.StageNumber} ({GameState.Instance.GetStageExploreFoodCost(nextStage.StageNumber)} food)"
            : "Route fully explored";

        return
            $"Owned units: {ownedUnits}/{GameData.PlayerRosterIds.Length}\n" +
            $"Unit doctrines: {GameState.Instance.ClaimedUnitDoctrineCount}/{eligibleDoctrineCount} forged\n" +
            $"Heroic directives: {GameState.Instance.ClaimedCampaignDirectiveCount}/{GameState.Instance.MaxStage} secured\n" +
            $"War wagon upgrades: Plating {hullLevel}/{GameState.Instance.MaxBaseUpgradeLevel}  |  Stores {pantryLevel}/{GameState.Instance.MaxBaseUpgradeLevel}  |  Drum {dispatchLevel}/{GameState.Instance.MaxBaseUpgradeLevel}  |  Beacon {relayLevel}/{GameState.Instance.MaxBaseUpgradeLevel}\n" +
            $"Next exploration: {nextExploreLine}\n" +
            "Use Caravan Armory for purchases, upgrades, and squad edits.";
    }

    private string BuildSquadSummaryText()
    {
        var deckUnits = GameState.Instance.GetActiveDeckUnits();
        var summary =
            $"Active squad: {deckUnits.Count}/{GameState.Instance.DeckSizeLimit}\n" +
            $"Synergy: {GameState.Instance.BuildActiveDeckSynergyInlineSummary()}\n" +
            $"{GameState.Instance.BuildCampaignReadinessDetailedSummary(_selectedStage)}\n";

        if (deckUnits.Count == 0)
        {
            return summary + "No active units. Open Caravan Armory and assign three cards.";
        }

        for (var i = 0; i < deckUnits.Count; i++)
        {
            var unit = deckUnits[i];
            summary +=
                $"\n{i + 1}. {unit.DisplayName} Lv{GameState.Instance.GetUnitLevel(unit.Id)}" +
                $"  |  {SquadSynergyCatalog.GetTagDisplayName(unit.SquadTag)}" +
                $"  |  {GameState.Instance.BuildUnitDoctrineInlineText(unit.Id)}";
        }

        if (deckUnits.Count < GameState.Instance.DeckSizeLimit)
        {
            summary += "\n\nDeck incomplete. Fill the remaining slots in Caravan Armory.";
        }

        return summary;
    }

    private void RefreshDirectiveButton()
    {
        var directive = GameState.Instance.GetCampaignDirective(_selectedStage);
        if (directive == null)
        {
            _directiveButton.Text = "No Heroic Directive";
            _directiveButton.Disabled = true;
            return;
        }

        var unlocked = GameState.Instance.IsCampaignDirectiveUnlocked(_selectedStage);
        var armed = GameState.Instance.IsCampaignDirectiveArmed(_selectedStage);
        var claimed = GameState.Instance.HasClaimedCampaignDirective(directive.Id);
        _directiveButton.Text = armed
            ? "Stand Down Directive"
            : claimed
                ? $"Replay {directive.Title}"
                : $"Arm {directive.Title}";
        _directiveButton.Disabled = !unlocked;
        _directiveButton.TooltipText =
            $"{directive.Title}\n{directive.Summary}\n{CampaignDirectiveCatalog.BuildRewardSummary(directive)}";
    }

    private void ToggleSelectedDirective()
    {
        GameState.Instance.ToggleCampaignDirective(_selectedStage, out var message);
        _convoyStatusMessage = message;
        RefreshUi();
    }

    private bool TryGetNextStageForMap(string mapId, out StageDefinition stage)
    {
        foreach (var routeStage in GameData.GetStagesForMap(mapId))
        {
            if (routeStage.StageNumber <= GameState.Instance.HighestUnlockedStage)
            {
                continue;
            }

            stage = routeStage;
            return true;
        }

        stage = GameData.GetLatestStageForMap(mapId);
        return false;
    }
}
