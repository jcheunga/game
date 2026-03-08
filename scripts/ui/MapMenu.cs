using System;
using System.Collections.Generic;
using Godot;

public partial class MapMenu : Control
{
    private readonly struct RoutePresentation
    {
        public RoutePresentation(string title, string subtitle, Color accent, Color panelColor)
        {
            Title = title;
            Subtitle = subtitle;
            Accent = accent;
            PanelColor = panelColor;
        }

        public string Title { get; }
        public string Subtitle { get; }
        public Color Accent { get; }
        public Color PanelColor { get; }
    }

    private readonly Dictionary<int, Button> _stageButtons = new();
    private readonly Dictionary<string, Button> _deckButtons = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Button> _upgradeButtons = new(StringComparer.OrdinalIgnoreCase);

    private OptionButton _mapSelector = null!;
    private Label _resourcesLabel = null!;
    private Label _resultLabel = null!;
    private Label _deckStatusLabel = null!;
    private Label _stageNameLabel = null!;
    private Label _stageDescriptionLabel = null!;
    private Label _stageStatusLabel = null!;
    private Label _stageRewardLabel = null!;
    private Label _stageObjectivesLabel = null!;
    private Label _stageModifiersLabel = null!;
    private Label _stageIntelLabel = null!;
    private PanelContainer _routeBannerPanel = null!;
    private Label _routeTitleLabel = null!;
    private Label _routeSubtitleLabel = null!;
    private Label _routeProgressLabel = null!;
    private Button _deployButton = null!;
    private MapPathCanvas _mapCanvas = null!;

    private int _selectedStage = 1;
    private string _activeMapId = "city";
    private string _deckStatusMessage = "Choose the squad cards that enter battle.";

    public override void _Ready()
    {
        _selectedStage = Mathf.Clamp(GameState.Instance.SelectedStage, 1, GameState.Instance.MaxStage);
        _activeMapId = GetMapIdForStage(_selectedStage);
        BuildUi();
        BuildMapSelectorItems();
        RefreshUi();
    }

    private void BuildUi()
    {
        var background = new ColorRect
        {
            Color = new Color("1b263b")
        };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var topBar = new PanelContainer
        {
            Position = new Vector2(20, 18),
            Size = new Vector2(1240, 72)
        };
        AddChild(topBar);

        var topRow = new HBoxContainer();
        topRow.AddThemeConstantOverride("separation", 12);
        topBar.AddChild(topRow);

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

        _resourcesLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        topRow.AddChild(_resourcesLabel);

        var mapPanel = new PanelContainer
        {
            Position = new Vector2(20, 106),
            Size = new Vector2(820, 592)
        };
        AddChild(mapPanel);

        var mapArea = new Control();
        mapArea.SetAnchorsPreset(LayoutPreset.FullRect);
        mapPanel.AddChild(mapArea);

        _mapCanvas = new MapPathCanvas();
        _mapCanvas.SetAnchorsPreset(LayoutPreset.FullRect);
        mapArea.AddChild(_mapCanvas);

        _routeBannerPanel = new PanelContainer
        {
            Position = new Vector2(18f, 18f),
            Size = new Vector2(324f, 120f)
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

        _routeProgressLabel = new Label();
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

        var sidePanel = new PanelContainer
        {
            Position = new Vector2(860, 106),
            Size = new Vector2(400, 592)
        };
        AddChild(sidePanel);

        var sidePadding = new MarginContainer();
        sidePadding.AddThemeConstantOverride("margin_left", 18);
        sidePadding.AddThemeConstantOverride("margin_right", 18);
        sidePadding.AddThemeConstantOverride("margin_top", 18);
        sidePadding.AddThemeConstantOverride("margin_bottom", 18);
        sidePanel.AddChild(sidePadding);

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

        _stageRewardLabel = new Label();
        sideContent.AddChild(_stageRewardLabel);

        _stageObjectivesLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        sideContent.AddChild(_stageObjectivesLabel);

        _stageModifiersLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        sideContent.AddChild(_stageModifiersLabel);

        _stageIntelLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        sideContent.AddChild(_stageIntelLabel);

        var deckTitle = new Label
        {
            Text = $"Squad & Upgrades ({GameState.Instance.DeckSizeLimit} cards)"
        };
        sideContent.AddChild(deckTitle);

        _deckStatusLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0f, 48f)
        };
        sideContent.AddChild(_deckStatusLabel);

        foreach (var unit in GameData.GetPlayerUnits())
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            sideContent.AddChild(row);

            var deckButton = new Button
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0f, 42f)
            };
            deckButton.Pressed += () => ToggleDeckUnit(unit);
            row.AddChild(deckButton);
            _deckButtons[unit.Id] = deckButton;

            var upgradeButton = new Button
            {
                CustomMinimumSize = new Vector2(112f, 42f)
            };
            upgradeButton.Pressed += () => UpgradeUnit(unit);
            row.AddChild(upgradeButton);
            _upgradeButtons[unit.Id] = upgradeButton;
        }

        _resultLabel = new Label
        {
            Text = "",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0f, 80f)
        };
        sideContent.AddChild(_resultLabel);

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

        _resourcesLabel.Text = $"Scrap: {GameState.Instance.Scrap}  |  Fuel: {GameState.Instance.Fuel}";
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

        var stage = GameData.GetStage(_selectedStage);
        var stageUnlocked = _selectedStage <= GameState.Instance.HighestUnlockedStage;
        var bestStars = GameState.Instance.GetStageStars(_selectedStage);
        _stageNameLabel.Text = $"{stage.MapName} - Stage {_selectedStage}: {stage.StageName}";
        _stageDescriptionLabel.Text = stage.Description;
        _stageStatusLabel.Text = BuildStageStatusText(stage, bestStars, stageUnlocked);
        _stageRewardLabel.Text =
            $"Reward on clear: +{stage.RewardScrap} scrap, +{GameData.Combat.VictoryFuelReward} fuel   |   Terrain: {stage.TerrainId}";
        _stageObjectivesLabel.Text = StageObjectives.BuildSummaryText(stage, bestStars);
        _stageModifiersLabel.Text = StageModifiers.BuildSummaryText(stage);
        _stageIntelLabel.Text = StageEncounterIntel.BuildCompactSummary(stage);
        _deckStatusLabel.Text =
            $"{_deckStatusMessage}\nActive cards: {GameState.Instance.ActiveDeckUnitIds.Count}/{GameState.Instance.DeckSizeLimit}";
        _deployButton.Text = $"Deploy To Stage {_selectedStage}";
        var canStartBattle = GameState.Instance.CanStartBattle(out var deployValidationMessage);
        if (!canStartBattle)
        {
            _deckStatusLabel.Text += $"\n{deployValidationMessage}";
        }

        _deployButton.Disabled =
            _selectedStage > GameState.Instance.HighestUnlockedStage ||
            !canStartBattle;

        foreach (var pair in _deckButtons)
        {
            var unit = GameData.GetUnit(pair.Key);
            var unlocked = GameState.Instance.IsUnitUnlocked(pair.Key);
            var inDeck = GameState.Instance.IsUnitInActiveDeck(pair.Key);
            var level = GameState.Instance.GetUnitLevel(pair.Key);
            var unitTint = unit.GetTint();
            pair.Value.Text = !unlocked
                ? $"LOCKED  S{unit.UnlockStage}  {unit.DisplayName}"
                : inDeck
                    ? $"ACTIVE  Lv{level}  {unit.DisplayName}"
                    : $"RESERVE  Lv{level}  {unit.DisplayName}";
            pair.Value.Disabled = !unlocked;
            pair.Value.SelfModulate = !unlocked
                ? new Color("5c677d")
                : inDeck
                    ? unitTint.Lightened(0.25f)
                    : unitTint.Darkened(0.1f);
            pair.Value.TooltipText = BuildUnitTooltip(unit, level);
            pair.Value.AddThemeColorOverride("font_color", Colors.White);
            pair.Value.AddThemeColorOverride("font_hover_color", Colors.White);
            pair.Value.AddThemeColorOverride("font_pressed_color", Colors.White);
            pair.Value.AddThemeColorOverride("font_disabled_color", new Color(1f, 1f, 1f, 0.5f));

            if (_upgradeButtons.TryGetValue(pair.Key, out var upgradeButton))
            {
                var isMaxLevel = level >= GameState.Instance.MaxUnitLevel;
                var upgradeCost = GameState.Instance.GetUnitUpgradeCost(pair.Key);
                upgradeButton.Text = !unlocked
                    ? $"S{unit.UnlockStage}"
                    : isMaxLevel
                        ? "MAX"
                        : $"Up {upgradeCost}";
                upgradeButton.Disabled = !unlocked || isMaxLevel || GameState.Instance.Scrap < upgradeCost;
                upgradeButton.SelfModulate = !unlocked
                    ? new Color("4f5d75")
                    : isMaxLevel
                        ? new Color("588157")
                        : unitTint.Lerp(Colors.White, 0.35f);
            }
        }
    }

    private void StartSelectedStage()
    {
        if (_selectedStage > GameState.Instance.HighestUnlockedStage)
        {
            return;
        }

        if (!GameState.Instance.CanStartBattle(out var message))
        {
            _deckStatusMessage = message;
            RefreshUi();
            return;
        }

        GameState.Instance.SetSelectedStage(_selectedStage);
        GameState.Instance.PrepareCampaignBattle();
        SceneRouter.Instance.GoToLoadout();
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

    private void ToggleDeckUnit(UnitDefinition unit)
    {
        GameState.Instance.ToggleDeckUnit(unit.Id, out var message);
        _deckStatusMessage = message;
        RefreshUi();
    }

    private void UpgradeUnit(UnitDefinition unit)
    {
        GameState.Instance.TryUpgradeUnit(unit.Id, out var message);
        _deckStatusMessage = message;
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
        return string.IsNullOrWhiteSpace(mapId) ? "city" : mapId;
    }

    private void RefreshRouteBanner()
    {
        var route = ResolveRoutePresentation(_activeMapId);
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
        _routeSubtitleLabel.Text = route.Subtitle;
        _routeSubtitleLabel.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 0.82f));
        _routeProgressLabel.Text =
            $"Route progress: {completedStages}/{Mathf.Max(1, totalStages)} cleared   |   " +
            $"{unlockedStages}/{Mathf.Max(1, totalStages)} unlocked   |   " +
            $"Stars: {earnedStars}/{Mathf.Max(1, totalStages) * 3}";
        _routeProgressLabel.AddThemeColorOverride("font_color", route.Accent);
        _routeBannerPanel.SelfModulate = route.PanelColor;
    }

    private void ApplyStageButtonStyle(Button button, StageDefinition stage, int stars, bool unlocked, bool selected)
    {
        var route = ResolveRoutePresentation(stage.MapId);
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
        button.TooltipText =
            $"{stage.MapName} - Stage {stage.StageNumber}: {stage.StageName}\n" +
            $"Threat: {StageEncounterIntel.ResolveThreatRating(stage)}  |  Stars: {stars}/3\n" +
            $"{stage.Description.Split('\n')[0]}";

        button.SelfModulate = !unlocked
            ? route.PanelColor.Darkened(0.35f)
            : selected
                ? route.Accent
                : stars > 0
                    ? route.Accent.Lerp(Colors.White, 0.22f)
                    : route.PanelColor.Lightened(0.22f);
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

        return
            $"Stage status: {stageState}  |  Best stars: {bestStars}/3\n" +
            $"Threat rating: {StageEncounterIntel.ResolveThreatRating(stage)}  |  Pressure: {waveStatus}";
    }

    private string BuildUnitTooltip(UnitDefinition definition, int level)
    {
        var stats = GameState.Instance.BuildPlayerUnitStats(definition);
        return
            $"Lv{level} {definition.DisplayName}\n" +
            $"Cost {definition.Cost}  |  HP {Mathf.RoundToInt(stats.MaxHealth)}  |  ATK {stats.AttackDamage:0.#}\n" +
            $"Range {stats.AttackRange:0.#}  |  Deploy CD {definition.DeployCooldown:0.#}s";
    }

    private RoutePresentation ResolveRoutePresentation(string mapId)
    {
        return NormalizeMapId(mapId).ToLowerInvariant() switch
        {
            "harbor" => new RoutePresentation(
                "Harbor Front",
                "Flooded terminals, cranes, and shipbreak lanes. Heavier zombie density and late-battle pressure.",
                new Color("80ed99"),
                new Color("1d3557")),
            _ => new RoutePresentation(
                "City Route",
                "Suburban highways and metro choke points. Faster pacing, mixed infected, and earlier ranged pressure.",
                new Color("ffd166"),
                new Color("243b53"))
        };
    }
}
