using Godot;

public partial class LoadoutMenu : Control
{
    private StageDefinition _stage = null!;

    public override void _Ready()
    {
        _stage = GameData.GetStage(Mathf.Clamp(GameState.Instance.SelectedStage, 1, GameState.Instance.MaxStage));
        BuildUi();
    }

    private void BuildUi()
    {
        var background = new ColorRect
        {
            Color = new Color("0d1b2a")
        };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var titlePanel = new PanelContainer
        {
            Position = new Vector2(24f, 20f),
            Size = new Vector2(1232f, 80f)
        };
        AddChild(titlePanel);

        var titleRow = new HBoxContainer();
        titleRow.AddThemeConstantOverride("separation", 16);
        titlePanel.AddChild(titleRow);

        var titleLabel = new Label
        {
            Text = $"Loadout Briefing  |  Stage {_stage.StageNumber}: {_stage.StageName}",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center
        };
        titleRow.AddChild(titleLabel);

        var resourcesLabel = new Label
        {
            Text = $"Gold: {GameState.Instance.Gold}  |  Food: {GameState.Instance.Food}",
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        titleRow.AddChild(resourcesLabel);

        var missionPanel = new PanelContainer
        {
            Position = new Vector2(24f, 122f),
            Size = new Vector2(472f, 520f)
        };
        AddChild(missionPanel);

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
            Text = $"{_stage.MapName}  |  Terrain: {_stage.TerrainId}"
        });

        missionStack.AddChild(new Label
        {
            Text = _stage.Description,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0f, 120f)
        });

        missionStack.AddChild(new Label
        {
            Text = StageObjectives.BuildSummaryText(
                _stage,
                GameState.Instance.GetStageStars(_stage.StageNumber)),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        missionStack.AddChild(new Label
        {
            Text = $"Reward on clear: +{_stage.RewardGold} gold, +{_stage.RewardFood} food  |  Entry cost: -{GameState.Instance.GetStageEntryFoodCost(_stage.StageNumber)} food"
        });

        missionStack.AddChild(new Label
        {
            Text = StageModifiers.BuildSummaryText(_stage),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        missionStack.AddChild(new Label
        {
            Text = StageEncounterIntel.BuildEncounterIntel(_stage),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        missionStack.AddChild(new Label
        {
            Text = StageEncounterIntel.BuildWaveSummary(_stage),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        var rosterPanel = new PanelContainer
        {
            Position = new Vector2(520f, 122f),
            Size = new Vector2(736f, 520f)
        };
        AddChild(rosterPanel);

        var rosterPadding = new MarginContainer();
        rosterPadding.AddThemeConstantOverride("margin_left", 18);
        rosterPadding.AddThemeConstantOverride("margin_right", 18);
        rosterPadding.AddThemeConstantOverride("margin_top", 18);
        rosterPadding.AddThemeConstantOverride("margin_bottom", 18);
        rosterPanel.AddChild(rosterPadding);

        var rosterStack = new VBoxContainer();
        rosterStack.AddThemeConstantOverride("separation", 12);
        rosterPadding.AddChild(rosterStack);

        rosterStack.AddChild(new Label
        {
            Text = $"Active Squad ({GameState.Instance.ActiveDeckUnitIds.Count}/{GameState.Instance.DeckSizeLimit})"
        });

        foreach (var definition in GameState.Instance.GetActiveDeckUnits())
        {
            rosterStack.AddChild(BuildUnitPanel(definition));
        }

        var canStartBattle = GameState.Instance.CanStartCampaignBattle(_stage.StageNumber, out var deployValidationMessage);
        rosterStack.AddChild(new Label
        {
            Text = deployValidationMessage,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        var bottomPanel = new PanelContainer
        {
            Position = new Vector2(24f, 660f),
            Size = new Vector2(1232f, 56f)
        };
        AddChild(bottomPanel);

        var bottomRow = new HBoxContainer();
        bottomRow.AddThemeConstantOverride("separation", 12);
        bottomPanel.AddChild(bottomRow);

        var backButton = new Button
        {
            Text = "Back To Map",
            CustomMinimumSize = new Vector2(180f, 0f)
        };
        backButton.Pressed += () => SceneRouter.Instance.GoToMap();
        bottomRow.AddChild(backButton);

        bottomRow.AddChild(new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        });

        var deployButton = new Button
        {
            Text = canStartBattle
                ? $"Deploy Convoy (-{GameState.Instance.GetStageEntryFoodCost(_stage.StageNumber)} food)"
                : "Convoy Not Ready",
            CustomMinimumSize = new Vector2(220f, 0f)
        };
        deployButton.Disabled = !canStartBattle;
        deployButton.Pressed += () =>
        {
            if (!GameState.Instance.TrySpendStageEntryFood(_stage.StageNumber, out _))
            {
                return;
            }

            GameState.Instance.PrepareCampaignBattle();
            SceneRouter.Instance.GoToBattle();
        };
        bottomRow.AddChild(deployButton);
    }

    private Control BuildUnitPanel(UnitDefinition definition)
    {
        var stats = GameState.Instance.BuildPlayerUnitStats(definition);
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0f, 110f)
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
            Text = $"Lv{GameState.Instance.GetUnitLevel(definition.Id)}  {definition.DisplayName}"
        });

        stack.AddChild(new Label
        {
            Text =
                $"Cost {definition.Cost}  |  HP {Mathf.RoundToInt(stats.MaxHealth)}  |  ATK {stats.AttackDamage:0.#}  |  Base {stats.BaseDamage}  |  Deploy CD {definition.DeployCooldown:0.#}s",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        stack.AddChild(new Label
        {
            Text =
                $"Speed {stats.Speed:0.#}  |  Range {stats.AttackRange:0.#}  |  Attack CD {stats.AttackCooldown:0.##}s" +
                (stats.UsesProjectile ? $"  |  Projectile {stats.ProjectileSpeed:0.#}" : ""),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        return panel;
    }
}
