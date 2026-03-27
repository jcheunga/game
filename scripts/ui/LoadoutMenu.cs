using Godot;

public partial class LoadoutMenu : Control
{
    private StageDefinition _stage = null!;

    private readonly System.Collections.Generic.List<Control> _animatedPanels = new();

    public override void _Ready()
    {
        _stage = GameState.Instance.BuildConfiguredCampaignStage(Mathf.Clamp(GameState.Instance.SelectedStage, 1, GameState.Instance.MaxStage));
        BuildUi();
        TryShowMenuHint();
        PlayEntranceAnimations();
    }

    private void TryShowMenuHint()
    {
        if (!GameState.Instance.ShowHints)
        {
            return;
        }

        var hints = TutorialHintCatalog.GetByContext("first_loadout");
        foreach (var hint in hints)
        {
            if (GameState.Instance.HasSeenHint(hint.Id))
            {
                continue;
            }

            var hintLabel = new Label
            {
                Text = $"[{hint.Title}] {hint.Body}",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                Position = new Vector2(24f, 648f),
                Size = new Vector2(1232f, 40f),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            hintLabel.AddThemeColorOverride("font_color", new Color("ffd166"));
            AddChild(hintLabel);
            GameState.Instance.MarkHintSeen(hint.Id);
        }
    }

    private void BuildUi()
    {
        var route = RouteCatalog.Get(_stage.MapId);
        MenuBackdropComposer.AddSplitBackdrop(this, "loadout", route.BackgroundTop, route.BackgroundBottom, route.BannerAccent, 104f, route.Id);

        var titlePanel = new PanelContainer
        {
            Position = new Vector2(24f, 20f),
            Size = new Vector2(1232f, 80f)
        };
        titlePanel.SelfModulate = route.BannerPanel.Lightened(0.08f);
        AddChild(titlePanel);
        _animatedPanels.Add(titlePanel);

        var titleRow = new HBoxContainer();
        titleRow.AddThemeConstantOverride("separation", 16);
        titlePanel.AddChild(titleRow);

        var titleLabel = new Label
        {
            Text = $"Loadout Briefing  |  {route.Title}  |  Stage {_stage.StageNumber}: {_stage.StageName}",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center
        };
        titleLabel.AddThemeColorOverride("font_color", route.BannerAccent);
        titleRow.AddChild(titleLabel);

        var resourcesRow = new HBoxContainer();
        resourcesRow.AddThemeConstantOverride("separation", 12);
        resourcesRow.AddChild(UiBadgeFactory.CreateRewardMetric("gold", "", GameState.Instance.Gold.ToString("N0"), new Vector2(24f, 24f)));
        resourcesRow.AddChild(UiBadgeFactory.CreateRewardMetric("food", "", GameState.Instance.Food.ToString("N0"), new Vector2(24f, 24f)));
        titleRow.AddChild(resourcesRow);

        var missionPanel = new PanelContainer
        {
            Position = new Vector2(24f, 122f),
            Size = new Vector2(472f, 520f)
        };
        missionPanel.SelfModulate = route.BannerPanel;
        AddChild(missionPanel);
        _animatedPanels.Add(missionPanel);

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
            Text = $"{route.Title}  |  Terrain: {_stage.TerrainId}"
        });

        missionStack.AddChild(new Label
        {
            Text = $"{route.CampaignSubtitle}\nPressure profile: {route.PressureSummary}",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
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
            Text = StageMissionEvents.BuildCampaignSummaryText(_stage),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        var rewardRow = new HBoxContainer();
        rewardRow.AddThemeConstantOverride("separation", 8);
        if (_stage.RewardGold > 0)
        {
            rewardRow.AddChild(CreateRewardChip("gold", "", $"+{_stage.RewardGold} Gold"));
        }
        if (_stage.RewardFood > 0)
        {
            rewardRow.AddChild(CreateRewardChip("food", "", $"+{_stage.RewardFood} Food"));
        }
        var entryFoodCost = GameState.Instance.GetStageEntryFoodCost(_stage.StageNumber);
        if (entryFoodCost > 0)
        {
            rewardRow.AddChild(CreateRewardChip("food", "", $"-{entryFoodCost} Food Entry"));
        }
        missionStack.AddChild(rewardRow);

        missionStack.AddChild(new Label
        {
            Text = GameState.Instance.BuildCampaignDirectiveStatusText(_stage.StageNumber),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        missionStack.AddChild(new Label
        {
            Text = GameState.Instance.BuildCampaignScoutStatusText(_stage.StageNumber),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        missionStack.AddChild(new Label
        {
            Text =
                $"{GameState.Instance.BuildCampaignFieldOrderStatusText(_stage.MapId)}\n" +
                $"{GameState.Instance.BuildCampaignMomentumStatusText()}\n" +
                $"{GameState.Instance.BuildCampaignConvoyCommandStatusText(_stage.MapId)}\n" +
                $"{GameState.Instance.BuildCampaignRouteDoctrineStatusText(_stage.StageNumber, _stage.MapId)}\n" +
                $"{GameState.Instance.BuildCampaignMissionOutcomeStatusText(_stage.MapId)}\n" +
                $"{GameState.Instance.BuildCampaignCounterSurgeStatusText(_stage.MapId)}\n" +
                $"{GameState.Instance.BuildCampaignReserveStatusText()}\n" +
                $"{GameState.Instance.BuildCampaignRouteSupportStatusText(_stage.MapId)}",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        missionStack.AddChild(new Label
        {
            Text =
                $"War wagon upgrades:\n" +
                $"  Plating Lv{GameState.Instance.GetBaseUpgradeLevel(BaseUpgradeCatalog.HullPlatingId)}/{GameState.Instance.MaxBaseUpgradeLevel}  |  " +
                $"Stores Lv{GameState.Instance.GetBaseUpgradeLevel(BaseUpgradeCatalog.PantryId)}/{GameState.Instance.MaxBaseUpgradeLevel}\n" +
                $"  March Drum Lv{GameState.Instance.GetBaseUpgradeLevel(BaseUpgradeCatalog.DispatchConsoleId)}/{GameState.Instance.MaxBaseUpgradeLevel}  |  " +
                $"Rune Beacon Lv{GameState.Instance.GetBaseUpgradeLevel(BaseUpgradeCatalog.SignalRelayId)}/{GameState.Instance.MaxBaseUpgradeLevel}",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        missionStack.AddChild(new Label
        {
            Text = StageModifiers.BuildSummaryText(_stage),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        missionStack.AddChild(new Label
        {
            Text = WeatherCatalog.BuildStageSummary(_stage),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        missionStack.AddChild(new Label
        {
            Text = StageHazards.BuildSummaryText(_stage),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        missionStack.AddChild(new Label
        {
            Text = StageEncounterIntel.BuildCampaignEncounterIntel(_stage),
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
        rosterPanel.SelfModulate = route.BannerPanel.Darkened(0.02f);
        AddChild(rosterPanel);
        _animatedPanels.Add(rosterPanel);

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

        rosterStack.AddChild(new Label
        {
            Text = GameState.Instance.BuildActiveDeckSynergySummary(),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        var deckUnitIds = GameState.Instance.ActiveDeckUnitIds;
        var deckIdSet = new System.Collections.Generic.HashSet<string>(deckUnitIds, System.StringComparer.OrdinalIgnoreCase);
        var comboPairLines = new System.Text.StringBuilder();
        foreach (var combo in ComboPairCatalog.GetAll())
        {
            if (deckIdSet.Contains(combo.UnitIdA) && deckIdSet.Contains(combo.UnitIdB))
            {
                var bonusPartsA = new System.Collections.Generic.List<string>();
                if (combo.HealthScaleA > 1f) bonusPartsA.Add($"HP +{Mathf.RoundToInt((combo.HealthScaleA - 1f) * 100f)}%");
                if (combo.DamageScaleA > 1f) bonusPartsA.Add($"ATK +{Mathf.RoundToInt((combo.DamageScaleA - 1f) * 100f)}%");
                if (combo.SpeedScaleA > 1f) bonusPartsA.Add($"SPD +{Mathf.RoundToInt((combo.SpeedScaleA - 1f) * 100f)}%");
                var bonusPartsB = new System.Collections.Generic.List<string>();
                if (combo.HealthScaleB > 1f) bonusPartsB.Add($"HP +{Mathf.RoundToInt((combo.HealthScaleB - 1f) * 100f)}%");
                if (combo.DamageScaleB > 1f) bonusPartsB.Add($"ATK +{Mathf.RoundToInt((combo.DamageScaleB - 1f) * 100f)}%");
                if (combo.SpeedScaleB > 1f) bonusPartsB.Add($"SPD +{Mathf.RoundToInt((combo.SpeedScaleB - 1f) * 100f)}%");
                var statSummary = string.Join(", ", bonusPartsA.Count >= bonusPartsB.Count ? bonusPartsA : bonusPartsB);
                comboPairLines.AppendLine($"Combo: {combo.Title} — {statSummary}");
            }
        }

        if (comboPairLines.Length > 0)
        {
            rosterStack.AddChild(new Label
            {
                Text = comboPairLines.ToString().TrimEnd(),
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            });
        }

        rosterStack.AddChild(new Label
        {
            Text = GameState.Instance.BuildCampaignReadinessDetailedSummary(_stage.StageNumber),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        foreach (var definition in GameState.Instance.GetActiveDeckUnits())
        {
            rosterStack.AddChild(BuildUnitPanel(definition));
        }

        rosterStack.AddChild(new Label
        {
            Text = $"Active Magic ({GameState.Instance.ActiveDeckSpellIds.Count}/{GameState.Instance.SpellDeckSizeLimit})"
        });

        rosterStack.AddChild(new Label
        {
            Text = GameState.Instance.BuildActiveSpellSummary(),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        foreach (var spell in GameState.Instance.GetActiveDeckSpells())
        {
            rosterStack.AddChild(BuildSpellPanel(spell));
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
        bottomPanel.SelfModulate = route.BannerPanel.Lightened(0.04f);
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

        var shopButton = new Button
        {
            Text = "Caravan Armory",
            CustomMinimumSize = new Vector2(180f, 0f)
        };
        shopButton.Pressed += () => SceneRouter.Instance.GoToShop();
        bottomRow.AddChild(shopButton);

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

        var deployButton = new Button
        {
            Text = canStartBattle
                ? $"Deploy Caravan (-{GameState.Instance.GetStageEntryFoodCost(_stage.StageNumber)} food)"
                : "Caravan Not Ready",
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

    private void PlayEntranceAnimations()
    {
        for (var i = 0; i < _animatedPanels.Count; i++)
        {
            var panel = _animatedPanels[i];
            panel.Modulate = new Color(1f, 1f, 1f, 0f);
            var delay = 0.06f + (i * 0.06f);
            var tween = CreateTween();
            tween.TweenProperty(panel, "modulate:a", 1f, 0.25f)
                .SetDelay(delay)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);
        }
    }

    private Control BuildUnitPanel(UnitDefinition definition)
    {
        var stats = GameState.Instance.BuildPlayerUnitStats(definition);
        var deployCooldown = GameState.Instance.ApplyPlayerDeployCooldownUpgrade(definition.DeployCooldown);
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0f, 118f)
        };

        var padding = new MarginContainer();
        padding.AddThemeConstantOverride("margin_left", 14);
        padding.AddThemeConstantOverride("margin_right", 14);
        padding.AddThemeConstantOverride("margin_top", 12);
        padding.AddThemeConstantOverride("margin_bottom", 12);
        panel.AddChild(padding);

        var stack = UiBadgeFactory.CreateStackWithLeadingBadge(
            padding,
            UiBadgeFactory.CreateUnitBadge(definition, new Vector2(72f, 72f)));

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
                $"Cost {definition.Cost}  |  HP {Mathf.RoundToInt(stats.MaxHealth)}  |  ATK {stats.AttackDamage:0.#}  |  Base {stats.BaseDamage}  |  Deploy CD {deployCooldown:0.#}s",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        stack.AddChild(new Label
        {
            Text =
                $"Speed {stats.Speed:0.#}  |  Range {stats.AttackRange:0.#}  |  Attack CD {stats.AttackCooldown:0.##}s" +
                (stats.UsesProjectile ? $"  |  Projectile {stats.ProjectileSpeed:0.#}" : "") +
                UnitStatText.BuildInlineTraits(stats),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        var unitLevel = GameState.Instance.GetUnitLevel(definition.Id);
        var activeAbility = UnitActiveAbilityCatalog.GetForUnit(definition.Id);
        if (activeAbility != null && unitLevel >= activeAbility.UnlockLevel - 1)
        {
            var abilityPrefix = unitLevel >= activeAbility.UnlockLevel ? "Ability" : $"Lv{activeAbility.UnlockLevel}+ Ability";
            stack.AddChild(new Label
            {
                Text = $"{abilityPrefix}: {activeAbility.Title} — {activeAbility.Description} (CD: {activeAbility.CooldownSeconds:0.#}s)",
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            });
        }

        return panel;
    }

    private Control BuildSpellPanel(SpellDefinition spell)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0f, 96f),
            SelfModulate = spell.GetTint().Darkened(0.08f)
        };

        var padding = new MarginContainer();
        padding.AddThemeConstantOverride("margin_left", 14);
        padding.AddThemeConstantOverride("margin_right", 14);
        padding.AddThemeConstantOverride("margin_top", 10);
        padding.AddThemeConstantOverride("margin_bottom", 10);
        panel.AddChild(padding);

        var stack = UiBadgeFactory.CreateStackWithLeadingBadge(
            padding,
            UiBadgeFactory.CreateSpellBadge(spell, new Vector2(64f, 64f)),
            stackSpacing: 6);

        var spellLevel = GameState.Instance.GetSpellLevel(spell.Id);
        stack.AddChild(new Label
        {
            Text = $"Lv{spellLevel} {spell.DisplayName}"
        });

        stack.AddChild(new Label
        {
            Text = SpellText.BuildInlineSummary(spell),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        return panel;
    }

    private static HBoxContainer CreateRewardChip(string rewardType, string rewardItemId, string text)
    {
        var chip = new HBoxContainer();
        chip.AddThemeConstantOverride("separation", 6);
        chip.AddChild(UiBadgeFactory.CreateRewardBadge(rewardType, rewardItemId, text, new Vector2(30f, 30f)));
        chip.AddChild(new Label
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center
        });
        return chip;
    }
}
