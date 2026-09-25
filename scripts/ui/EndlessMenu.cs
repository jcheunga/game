using System;
using System.Linq;
using Godot;

public partial class EndlessMenu : Control
{
    private MenuBackdropSet _menuBackdrop = null!;
    private OptionButton _routeSelector = null!;
    private OptionButton _boonSelector = null!;
    private HBoxContainer _resourcesRow = null!;
    private Label _routeTitleLabel = null!;
    private Label _routeSummaryLabel = null!;
    private Label _recordLabel = null!;
    private Label _rulesLabel = null!;
    private VBoxContainer _historyStack = null!;
    private Label _deckStatusLabel = null!;
    private VBoxContainer _squadStack = null!;
    private Button _deployButton = null!;

    private string _selectedRouteId = "city";
    private string _selectedBoonId = EndlessBoonCatalog.SurplusCourageId;

    private readonly System.Collections.Generic.List<Control> _entrancePanels = new();

    public override void _Ready()
    {
        _selectedRouteId = NormalizeRouteId(GameState.Instance.SelectedEndlessRouteId);
        _selectedBoonId = EndlessBoonCatalog.Normalize(GameState.Instance.SelectedEndlessBoonId);
        BuildUi();
        RefreshUi();
        AnimateEntrance();
    }

    private void AnimateEntrance()
    {
        for (var i = 0; i < _entrancePanels.Count; i++)
        {
            var panel = _entrancePanels[i];
            panel.Modulate = new Color(1f, 1f, 1f, 0f);
            var delay = 0.06f + (i * 0.05f);
            var tween = CreateTween();
            tween.TweenProperty(panel, "modulate:a", 1f, 0.22f)
                .SetDelay(delay)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);
        }
    }

    private void BuildUi()
    {
        var route = RouteCatalog.Get(_selectedRouteId);
        _menuBackdrop = MenuBackdropComposer.AddSolidBackdrop(this, "endless", route.BackgroundTop, route.Id);

        var titlePanel = new PanelContainer
        {
            Position = new Vector2(24f, 20f),
            Size = new Vector2(1232f, 82f)
        };
        AddChild(titlePanel);
        _entrancePanels.Add(titlePanel);

        var titleRow = new HBoxContainer();
        titleRow.AddThemeConstantOverride("separation", 16);
        titlePanel.AddChild(titleRow);

        titleRow.AddChild(new Label
        {
            Text = "Endless March",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center
        });

        _resourcesRow = new HBoxContainer();
        _resourcesRow.AddThemeConstantOverride("separation", 12);
        titleRow.AddChild(_resourcesRow);

        var missionPanel = new PanelContainer
        {
            Position = new Vector2(24f, 122f),
            Size = new Vector2(520f, 520f)
        };
        AddChild(missionPanel);
        _entrancePanels.Add(missionPanel);

        var missionPadding = new MarginContainer();
        missionPadding.AddThemeConstantOverride("margin_left", 18);
        missionPadding.AddThemeConstantOverride("margin_right", 18);
        missionPadding.AddThemeConstantOverride("margin_top", 18);
        missionPadding.AddThemeConstantOverride("margin_bottom", 18);
        missionPanel.AddChild(missionPadding);

        var missionScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        missionPadding.AddChild(missionScroll);

        var missionStack = new VBoxContainer();
        missionStack.AddThemeConstantOverride("separation", 12);
        missionStack.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        missionScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        missionScroll.AddChild(missionStack);

        missionStack.AddChild(new Label
        {
            Text = "Route"
        });

        _routeSelector = new OptionButton
        {
            CustomMinimumSize = new Vector2(240f, 0f)
        };
        _routeSelector.ItemSelected += OnRouteSelected;
        missionStack.AddChild(_routeSelector);

        foreach (var stage in GameData.Stages)
        {
            var mapId = NormalizeRouteId(stage.MapId);
            var alreadyAdded = false;
            for (var i = 0; i < _routeSelector.ItemCount; i++)
            {
                if (NormalizeRouteId(_routeSelector.GetItemMetadata(i).AsString()) == mapId)
                {
                    alreadyAdded = true;
                    break;
                }
            }

            if (alreadyAdded)
            {
                continue;
            }

            var index = _routeSelector.ItemCount;
            _routeSelector.AddItem(stage.MapName);
            _routeSelector.SetItemMetadata(index, mapId);
        }

        missionStack.AddChild(new Label
        {
            Text = "Opening boon"
        });

        _boonSelector = new OptionButton
        {
            CustomMinimumSize = new Vector2(240f, 0f)
        };
        _boonSelector.ItemSelected += OnBoonSelected;
        missionStack.AddChild(_boonSelector);

        foreach (var boon in EndlessBoonCatalog.GetAll())
        {
            var index = _boonSelector.ItemCount;
            _boonSelector.AddItem(boon.Title);
            _boonSelector.SetItemMetadata(index, boon.Id);
        }

        _routeTitleLabel = new Label();
        missionStack.AddChild(_routeTitleLabel);

        _routeSummaryLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0f, 148f)
        };
        missionStack.AddChild(_routeSummaryLabel);

        _recordLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        missionStack.AddChild(_recordLabel);

        _rulesLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        missionStack.AddChild(_rulesLabel);

        _historyStack = new VBoxContainer();
        _historyStack.AddThemeConstantOverride("separation", 4);
        missionStack.AddChild(_historyStack);
        _historyStack.Visible = false;
        _rulesLabel.Visible = false;
        missionStack.AddChild(RealmUi.Button("book", "Field guide", () => RealmUi.Details(this, "Endless march", _routeSummaryLabel.TooltipText + "\n\n" + _rulesLabel.Text)));
        missionStack.AddChild(RealmUi.Button("clock", "Run history", () => RealmUi.Details(this, "Run history", string.Join("\n", _historyStack.GetChildren().OfType<Label>().Select(x => x.Text)))));

        var squadPanel = new PanelContainer
        {
            Position = new Vector2(568f, 122f),
            Size = new Vector2(688f, 520f)
        };
        AddChild(squadPanel);
        _entrancePanels.Add(squadPanel);

        var squadPadding = new MarginContainer();
        squadPadding.AddThemeConstantOverride("margin_left", 18);
        squadPadding.AddThemeConstantOverride("margin_right", 18);
        squadPadding.AddThemeConstantOverride("margin_top", 18);
        squadPadding.AddThemeConstantOverride("margin_bottom", 18);
        squadPanel.AddChild(squadPadding);

        var squadScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        squadPadding.AddChild(squadScroll);

        _squadStack = new VBoxContainer();
        _squadStack.AddThemeConstantOverride("separation", 12);
        _squadStack.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        squadScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        squadScroll.AddChild(_squadStack);

        var bottomPanel = new PanelContainer
        {
            Position = new Vector2(24f, 660f),
            Size = new Vector2(1232f, 56f)
        };
        AddChild(bottomPanel);
        _entrancePanels.Add(bottomPanel);

        var bottomRow = new HBoxContainer();
        bottomRow.AddThemeConstantOverride("separation", 12);
        bottomPanel.AddChild(bottomRow);

        var backButton = new RealmButton
        {
            Text = "Back To Title",
            CustomMinimumSize = new Vector2(180f, 0f)
        };
        backButton.Pressed += () => SceneRouter.Instance.GoToMainMenu();
        bottomRow.AddChild(backButton);

        var editSquadButton = new RealmButton
        {
            Text = "Caravan Armory",
            CustomMinimumSize = new Vector2(220f, 0f)
        };
        editSquadButton.Pressed += () => SceneRouter.Instance.GoToShop();
        bottomRow.AddChild(editSquadButton);

        var settingsButton = new RealmButton
        {
            Text = "Settings",
            CustomMinimumSize = new Vector2(150f, 0f)
        };
        settingsButton.Pressed += () => SceneRouter.Instance.GoToSettings();
        bottomRow.AddChild(settingsButton);

        bottomRow.AddChild(new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        });

        _deployButton = new RealmButton
        {
            Text = "Start Endless Run",
            CustomMinimumSize = new Vector2(240f, 0f)
        };
        _deployButton.Pressed += StartRun;
        bottomRow.AddChild(_deployButton);
    }

    private void RefreshUi()
    {
        SyncSelector();
        SyncBoonSelector();
        RebuildResourcesRow();

        var route = RouteCatalog.Get(_selectedRouteId);
        var templateStage = GameData.GetLatestStageForMap(_selectedRouteId);
        var routeStages = GameData.GetStagesForMap(_selectedRouteId);
        var selectedBoon = EndlessBoonCatalog.Get(_selectedBoonId);
        var bossCheckpoint = EndlessBossCheckpointCatalog.GetForRoute(_selectedRouteId);
        _menuBackdrop.PrimaryRect.Color = route.BackgroundTop;
        _menuBackdrop.SetTexture(UiTextureLoader.TryLoadScreenBackground("endless", route.Id));
        _routeTitleLabel.Text = $"{templateStage.MapName} Endless Run";
        _routeSummaryLabel.Text =
            $"{BuildRouteDescription(_selectedRouteId)}\n\n" +
            $"District stages in campaign: {routeStages.Count}\n" +
            $"{StageEncounterIntel.BuildCompactSummary(templateStage)}\n\n" +
            $"Boss checkpoint: wave {EndlessBossCheckpointCatalog.BossCheckpointInterval} - {bossCheckpoint.Title}\n" +
            $"{bossCheckpoint.Summary}\n" +
            $"{bossCheckpoint.RewardSummary}\n\n" +
            $"Opening boon: {selectedBoon.Title}\n{selectedBoon.Summary}";
        _routeSummaryLabel.TooltipText = _routeSummaryLabel.Text;
        _routeSummaryLabel.Text = $"{BuildRouteDescription(_selectedRouteId)}\n\n{selectedBoon.Summary}";
        _recordLabel.Text = $"Best wave {GameState.Instance.BestEndlessWave}   ·   {GameState.Instance.EndlessRuns} runs";
        _rulesLabel.Text =
            $"Run rules:\n" +
            "- Waves scale up continuously.\n" +
            $"- Every {EndlessBossCheckpointCatalog.BossCheckpointInterval}th wave is a boss checkpoint with extra rewards.\n" +
            "- Pick one temporary opening boon before deploying.\n" +
            "- Use Caravan Armory to change the active squad or buy upgrades.\n" +
            "- Retreat to bank the gold and food recovered so far.\n" +
            "- Gold and food rewards scale with wave reached, time alive, and kills.";

        RebuildRunHistory();
        RebuildSquadPanels();

        var canStartBattle = GameState.Instance.CanStartBattle(out var deployMessage);
        _deckStatusLabel.Text = deployMessage;
        _deployButton.Disabled = !canStartBattle;
        _deployButton.Text = canStartBattle ? "Begin Endless March" : "Caravan Not Ready";
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

    private void RebuildRunHistory()
    {
        foreach (var child in _historyStack.GetChildren())
        {
            child.QueueFree();
        }

        var history = GameState.Instance.GetEndlessRunHistory();
        if (history.Count == 0)
        {
            return;
        }

        _historyStack.AddChild(new Label
        {
            Text = "Run History"
        });

        var bestWave = GameState.Instance.BestEndlessWave;
        var displayCount = Math.Min(history.Count, 10);
        for (var i = 0; i < displayCount; i++)
        {
            var run = history[i];
            var minutes = (int)(run.TimeSeconds / 60f);
            var seconds = (int)(run.TimeSeconds % 60f);
            var routeName = GameData.GetLatestStageForMap(run.RouteId).MapName;
            var diffTitle = DifficultyCatalog.GetById(run.DifficultyId).Title;
            var line = $"#{i + 1}  Wave {run.Wave}  |  {minutes}:{seconds:D2}  |  {routeName}  |  +{run.GoldEarned} gold  |  {diffTitle}";

            var isBestWave = run.Wave >= bestWave && bestWave > 0;
            var label = new Label
            {
                Text = line,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };

            if (isBestWave)
            {
                label.AddThemeColorOverride("font_color", new Color("ffd700"));
            }

            _historyStack.AddChild(label);
        }
    }

    private void RebuildSquadPanels()
    {
        RealmUi.Clear(_squadStack);
        _squadStack.AddChild(RealmUi.Heading("Your warband", 28));
        var cards = new HBoxContainer(); _squadStack.AddChild(cards);
        foreach (var unit in GameState.Instance.GetActiveDeckUnits())
        {
            var card = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            cards.AddChild(card);
            card.AddChild(UiBadgeFactory.CreateUnitBadge(unit, new Vector2(150, 170)));
            card.AddChild(RealmUi.Label(unit.DisplayName, 17));
            card.AddChild(RealmUi.Label($"Level {GameState.Instance.GetUnitLevel(unit.Id)} · {unit.Cost} courage", 13, true));
        }
        _squadStack.AddChild(RealmUi.Label(GameState.Instance.BuildActiveDeckSynergyInlineSummary(), 14, true));
        var spells = new HBoxContainer(); _squadStack.AddChild(spells);
        foreach (var spell in GameState.Instance.GetActiveDeckSpells())
            spells.AddChild(RealmUi.Button("bolt", spell.DisplayName, () => RealmUi.Details(this, spell.DisplayName, SpellText.BuildInlineSummary(spell))));
        _squadStack.AddChild(RealmUi.Button("sword", "Edit warband", () => SceneRouter.Instance.GoToShop()));
        _deckStatusLabel = RealmUi.Label("", 14, true); _squadStack.AddChild(_deckStatusLabel);
    }

    private void OnRouteSelected(long index)
    {
        if (index < 0 || index >= _routeSelector.ItemCount)
        {
            return;
        }

        _selectedRouteId = NormalizeRouteId(_routeSelector.GetItemMetadata((int)index).AsString());
        GameState.Instance.SetSelectedEndlessRoute(_selectedRouteId);
        RefreshUi();
    }

    private void OnBoonSelected(long index)
    {
        if (index < 0 || index >= _boonSelector.ItemCount)
        {
            return;
        }

        _selectedBoonId = EndlessBoonCatalog.Normalize(_boonSelector.GetItemMetadata((int)index).AsString());
        GameState.Instance.SetSelectedEndlessBoon(_selectedBoonId);
        RefreshUi();
    }

    private void StartRun()
    {
        if (!GameState.Instance.CanStartBattle(out _))
        {
            RefreshUi();
            return;
        }

        GameState.Instance.PrepareEndlessBattle(_selectedRouteId);
        SceneRouter.Instance.GoToBattle();
    }

    private void SyncSelector()
    {
        for (var i = 0; i < _routeSelector.ItemCount; i++)
        {
            if (NormalizeRouteId(_routeSelector.GetItemMetadata(i).AsString()) != _selectedRouteId)
            {
                continue;
            }

            _routeSelector.Select(i);
            return;
        }

        if (_routeSelector.ItemCount > 0)
        {
            _routeSelector.Select(0);
            _selectedRouteId = NormalizeRouteId(_routeSelector.GetItemMetadata(0).AsString());
        }
    }

    private void SyncBoonSelector()
    {
        for (var i = 0; i < _boonSelector.ItemCount; i++)
        {
            if (EndlessBoonCatalog.Normalize(_boonSelector.GetItemMetadata(i).AsString()) != _selectedBoonId)
            {
                continue;
            }

            _boonSelector.Select(i);
            return;
        }

        if (_boonSelector.ItemCount > 0)
        {
            _boonSelector.Select(0);
            _selectedBoonId = EndlessBoonCatalog.Normalize(_boonSelector.GetItemMetadata(0).AsString());
        }
    }

    private static string BuildRouteDescription(string routeId)
    {
        return RouteCatalog.Get(routeId).EndlessSummary;
    }

    private static string NormalizeRouteId(string routeId)
    {
        return RouteCatalog.Normalize(routeId);
    }
}
