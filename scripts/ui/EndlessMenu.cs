using System.Linq;
using Godot;

public partial class EndlessMenu : Control
{
    private OptionButton _routeSelector = null!;
    private OptionButton _boonSelector = null!;
    private Label _routeTitleLabel = null!;
    private Label _routeSummaryLabel = null!;
    private Label _recordLabel = null!;
    private Label _rulesLabel = null!;
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
        var background = new ColorRect
        {
            Color = new Color("102a43")
        };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(background);

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

        titleRow.AddChild(new Label
        {
            Text = $"Gold: {GameState.Instance.Gold}  |  Food: {GameState.Instance.Food}",
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        });

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

        var missionStack = new VBoxContainer();
        missionStack.AddThemeConstantOverride("separation", 12);
        missionPadding.AddChild(missionStack);

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

        var backButton = new Button
        {
            Text = "Back To Title",
            CustomMinimumSize = new Vector2(180f, 0f)
        };
        backButton.Pressed += () => SceneRouter.Instance.GoToMainMenu();
        bottomRow.AddChild(backButton);

        var editSquadButton = new Button
        {
            Text = "Caravan Armory",
            CustomMinimumSize = new Vector2(220f, 0f)
        };
        editSquadButton.Pressed += () => SceneRouter.Instance.GoToShop();
        bottomRow.AddChild(editSquadButton);

        var settingsButton = new Button
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

        _deployButton = new Button
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

        var templateStage = GameData.GetLatestStageForMap(_selectedRouteId);
        var routeStages = GameData.GetStagesForMap(_selectedRouteId);
        var selectedBoon = EndlessBoonCatalog.Get(_selectedBoonId);
        var bossCheckpoint = EndlessBossCheckpointCatalog.GetForRoute(_selectedRouteId);
        _routeTitleLabel.Text = $"{templateStage.MapName} Endless Run";
        _routeSummaryLabel.Text =
            $"{BuildRouteDescription(_selectedRouteId)}\n\n" +
            $"District stages in campaign: {routeStages.Count}\n" +
            $"{StageEncounterIntel.BuildCompactSummary(templateStage)}\n\n" +
            $"Boss checkpoint: wave {EndlessBossCheckpointCatalog.BossCheckpointInterval} - {bossCheckpoint.Title}\n" +
            $"{bossCheckpoint.Summary}\n" +
            $"{bossCheckpoint.RewardSummary}\n\n" +
            $"Opening boon: {selectedBoon.Title}\n{selectedBoon.Summary}";
        _recordLabel.Text =
            $"Run record:\n" +
            $"Best wave: {GameState.Instance.BestEndlessWave}\n" +
            $"Best survival: {GameState.Instance.BestEndlessTimeSeconds:0.0}s\n" +
            $"Runs completed: {GameState.Instance.EndlessRuns}";
        _rulesLabel.Text =
            $"Run rules:\n" +
            "- Waves scale up continuously.\n" +
            $"- Every {EndlessBossCheckpointCatalog.BossCheckpointInterval}th wave is a boss checkpoint with extra rewards.\n" +
            "- Pick one temporary opening boon before deploying.\n" +
            "- Use Caravan Armory to change the active squad or buy upgrades.\n" +
            "- Retreat to bank the gold and food recovered so far.\n" +
            "- Gold and food rewards scale with wave reached, time alive, and kills.";

        RebuildSquadPanels();

        var canStartBattle = GameState.Instance.CanStartBattle(out var deployMessage);
        _deckStatusLabel.Text = deployMessage;
        _deployButton.Disabled = !canStartBattle;
        _deployButton.Text = canStartBattle ? "Begin Endless March" : "Caravan Not Ready";
    }

    private void RebuildSquadPanels()
    {
        foreach (var child in _squadStack.GetChildren())
        {
            child.QueueFree();
        }

        _squadStack.AddChild(new Label
        {
            Text = $"Active Squad ({GameState.Instance.ActiveDeckUnitIds.Count}/{GameState.Instance.DeckSizeLimit})"
        });

        _squadStack.AddChild(new Label
        {
            Text = GameState.Instance.BuildActiveDeckSynergySummary(),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        foreach (var definition in GameState.Instance.GetActiveDeckUnits())
        {
            _squadStack.AddChild(BuildUnitPanel(definition));
        }

        _squadStack.AddChild(new Label
        {
            Text = $"Active Magic ({GameState.Instance.ActiveDeckSpellIds.Count}/{GameState.Instance.SpellDeckSizeLimit})"
        });

        _squadStack.AddChild(new Label
        {
            Text = GameState.Instance.BuildActiveSpellSummary(),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        foreach (var spell in GameState.Instance.GetActiveDeckSpells())
        {
            _squadStack.AddChild(BuildSpellPanel(spell));
        }

        _deckStatusLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _squadStack.AddChild(_deckStatusLabel);
    }

    private Control BuildUnitPanel(UnitDefinition definition)
    {
        var stats = GameState.Instance.BuildPlayerUnitStats(definition);
        var deployCooldown = GameState.Instance.ApplyPlayerDeployCooldownUpgrade(definition.DeployCooldown);
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0f, 108f)
        };

        var padding = new MarginContainer();
        padding.AddThemeConstantOverride("margin_left", 14);
        padding.AddThemeConstantOverride("margin_right", 14);
        padding.AddThemeConstantOverride("margin_top", 12);
        padding.AddThemeConstantOverride("margin_bottom", 12);
        panel.AddChild(padding);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 8);
        padding.AddChild(stack);

        stack.AddChild(new Label
        {
            Text =
                $"Lv{GameState.Instance.GetUnitLevel(definition.Id)}  {definition.DisplayName}  |  " +
                $"{SquadSynergyCatalog.GetTagDisplayName(definition.SquadTag)}  |  " +
                $"{GameState.Instance.BuildUnitDoctrineInlineText(definition.Id)}"
        });

        stack.AddChild(new Label
        {
            Text =
                $"Cost {definition.Cost}  |  HP {Mathf.RoundToInt(stats.MaxHealth)}  |  ATK {stats.AttackDamage:0.#}  |  Base {stats.BaseDamage}",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        stack.AddChild(new Label
        {
            Text =
                $"Range {stats.AttackRange:0.#}  |  Move {stats.Speed:0.#}  |  Deploy CD {deployCooldown:0.#}s" +
                UnitStatText.BuildInlineTraits(stats),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        return panel;
    }

    private Control BuildSpellPanel(SpellDefinition spell)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0f, 88f),
            SelfModulate = spell.GetTint().Darkened(0.08f)
        };

        var padding = new MarginContainer();
        padding.AddThemeConstantOverride("margin_left", 14);
        padding.AddThemeConstantOverride("margin_right", 14);
        padding.AddThemeConstantOverride("margin_top", 10);
        padding.AddThemeConstantOverride("margin_bottom", 10);
        panel.AddChild(padding);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 6);
        padding.AddChild(stack);

        stack.AddChild(new Label
        {
            Text = spell.DisplayName
        });

        stack.AddChild(new Label
        {
            Text = SpellText.BuildInlineSummary(spell),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        return panel;
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
