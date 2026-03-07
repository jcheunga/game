using System;
using System.Collections.Generic;
using Godot;

public partial class MapMenu : Control
{
    private readonly Dictionary<int, Button> _stageButtons = new();
    private readonly Dictionary<string, Button> _deckButtons = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Button> _upgradeButtons = new(StringComparer.OrdinalIgnoreCase);

    private OptionButton _mapSelector = null!;
    private Label _resourcesLabel = null!;
    private Label _resultLabel = null!;
    private Label _deckStatusLabel = null!;
    private Label _stageNameLabel = null!;
    private Label _stageDescriptionLabel = null!;
    private Label _stageRewardLabel = null!;
    private Label _stageObjectivesLabel = null!;
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
            Text = "Campaign Map",
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

        foreach (var stage in GameData.Stages)
        {
            var stageId = stage.StageNumber;
            var point = stage.MapPoint;
            _mapCanvas.StagePoints[stageId] = point;
            _mapCanvas.StageMapIds[stageId] = NormalizeMapId(stage.MapId);

            var stageButton = new Button
            {
                Text = stageId.ToString(),
                Position = point - new Vector2(40f, 24f),
                Size = new Vector2(80f, 48f)
            };

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

        _stageRewardLabel = new Label();
        sideContent.AddChild(_stageRewardLabel);

        _stageObjectivesLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        sideContent.AddChild(_stageObjectivesLabel);

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

            button.Visible = onActiveMap;
            button.Disabled = stageId > GameState.Instance.HighestUnlockedStage;
            var stageLabel = stars > 0 ? $"{stageId} ({stars}*)" : stageId.ToString();
            button.Text = stageId == _selectedStage ? $"[{stageLabel}]" : stageLabel;
        }

        var stage = GameData.GetStage(_selectedStage);
        var bestStars = GameState.Instance.GetStageStars(_selectedStage);
        _stageNameLabel.Text = $"{stage.MapName} - Stage {_selectedStage}: {stage.StageName}";
        _stageDescriptionLabel.Text = stage.Description;
        _stageRewardLabel.Text = $"Estimated reward: +{stage.RewardScrap} scrap   |   Terrain: {stage.TerrainId}";
        _stageObjectivesLabel.Text =
            $"Objectives:\n" +
            $"1* Clear the route\n" +
            $"2* Finish with bus hull >= {Mathf.RoundToInt(stage.TwoStarBusHullRatio * 100f)}%\n" +
            $"3* Clear within {stage.ThreeStarTimeLimitSeconds:0}s\n" +
            $"Best: {(bestStars > 0 ? $"{bestStars}/3" : "none")}";
        _deckStatusLabel.Text =
            $"{_deckStatusMessage}\nActive cards: {GameState.Instance.ActiveDeckUnitIds.Count}/{GameState.Instance.DeckSizeLimit}";
        _deployButton.Text = $"Deploy To Stage {_selectedStage}";
        _deployButton.Disabled = _selectedStage > GameState.Instance.HighestUnlockedStage;

        foreach (var pair in _deckButtons)
        {
            var unit = GameData.GetUnit(pair.Key);
            var unlocked = GameState.Instance.IsUnitUnlocked(pair.Key);
            var inDeck = GameState.Instance.IsUnitInActiveDeck(pair.Key);
            var level = GameState.Instance.GetUnitLevel(pair.Key);
            pair.Value.Text = !unlocked
                ? $"LOCKED  S{unit.UnlockStage}  {unit.DisplayName}"
                : inDeck
                    ? $"ACTIVE  Lv{level}  {unit.DisplayName}"
                    : $"RESERVE  Lv{level}  {unit.DisplayName}";
            pair.Value.Disabled = !unlocked;

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
            }
        }
    }

    private void StartSelectedStage()
    {
        if (_selectedStage > GameState.Instance.HighestUnlockedStage)
        {
            return;
        }

        GameState.Instance.SetSelectedStage(_selectedStage);
        SceneRouter.Instance.GoToBattle();
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
}
