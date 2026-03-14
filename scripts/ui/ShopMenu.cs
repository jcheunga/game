using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class ShopMenu : Control
{
    private sealed class ShopRecommendation
    {
        public ShopRecommendation(string id, string title, string summary, string actionLabel, Action execute, bool disabled = false)
        {
            Id = id;
            Title = title;
            Summary = summary;
            ActionLabel = actionLabel;
            Execute = execute;
            Disabled = disabled;
        }

        public string Id { get; }
        public string Title { get; }
        public string Summary { get; }
        public string ActionLabel { get; }
        public Action Execute { get; }
        public bool Disabled { get; }
    }

    private ColorRect _backgroundTop = null!;
    private ColorRect _backgroundBottom = null!;
    private ColorRect _accentBand = null!;
    private PanelContainer _titlePanel = null!;
    private PanelContainer _summaryPanel = null!;
    private PanelContainer _unitsPanel = null!;
    private PanelContainer _basePanel = null!;
    private Label _resourcesLabel = null!;
    private Label _statusLabel = null!;
    private Label _summaryLabel = null!;
    private Label _deckLabel = null!;
    private Label _routeIntelLabel = null!;
    private VBoxContainer _recommendationStack = null!;
    private VBoxContainer _unitStack = null!;
    private VBoxContainer _baseStack = null!;

    public override void _Ready()
    {
        BuildUi();
        RefreshUi();
        AnimateEntrance(new Control[] { _titlePanel, _summaryPanel, _unitsPanel, _basePanel });
    }

    private void AnimateEntrance(Control[] panels)
    {
        for (var i = 0; i < panels.Length; i++)
        {
            var panel = panels[i];
            if (panel == null) continue;
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
        _backgroundTop = new ColorRect
        {
            Color = new Color("14213d")
        };
        _backgroundTop.Position = Vector2.Zero;
        _backgroundTop.Size = new Vector2(1280f, 360f);
        AddChild(_backgroundTop);

        _backgroundBottom = new ColorRect
        {
            Color = new Color("0d1b2a"),
            Position = new Vector2(0f, 360f),
            Size = new Vector2(1280f, 360f)
        };
        AddChild(_backgroundBottom);

        _accentBand = new ColorRect
        {
            Color = new Color("ffd166"),
            Position = new Vector2(0f, 104f),
            Size = new Vector2(1280f, 6f)
        };
        AddChild(_accentBand);

        _titlePanel = new PanelContainer
        {
            Position = new Vector2(24f, 20f),
            Size = new Vector2(1232f, 82f)
        };
        AddChild(_titlePanel);

        var titleRow = new HBoxContainer();
        titleRow.AddThemeConstantOverride("separation", 16);
        _titlePanel.AddChild(titleRow);

        titleRow.AddChild(new Label
        {
            Text = "Caravan Armory",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center
        });

        _resourcesLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        titleRow.AddChild(_resourcesLabel);

        _summaryPanel = new PanelContainer
        {
            Position = new Vector2(24f, 122f),
            Size = new Vector2(360f, 520f)
        };
        AddChild(_summaryPanel);

        var summaryPadding = new MarginContainer();
        summaryPadding.AddThemeConstantOverride("margin_left", 18);
        summaryPadding.AddThemeConstantOverride("margin_right", 18);
        summaryPadding.AddThemeConstantOverride("margin_top", 18);
        summaryPadding.AddThemeConstantOverride("margin_bottom", 18);
        _summaryPanel.AddChild(summaryPadding);

        var summaryScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        summaryPadding.AddChild(summaryScroll);

        var summaryStack = new VBoxContainer();
        summaryStack.AddThemeConstantOverride("separation", 12);
        summaryScroll.AddChild(summaryStack);

        summaryStack.AddChild(new Label
        {
            Text = "Economy"
        });

        _summaryLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0f, 140f)
        };
        summaryStack.AddChild(_summaryLabel);

        summaryStack.AddChild(new Label
        {
            Text = "Active Squad"
        });

        _deckLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0f, 110f)
        };
        summaryStack.AddChild(_deckLabel);

        summaryStack.AddChild(new Label
        {
            Text = "Route Intel"
        });

        _routeIntelLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0f, 150f)
        };
        summaryStack.AddChild(_routeIntelLabel);

        summaryStack.AddChild(new Label
        {
            Text = "Action Board"
        });

        _recommendationStack = new VBoxContainer();
        _recommendationStack.AddThemeConstantOverride("separation", 10);
        summaryStack.AddChild(_recommendationStack);

        _statusLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0f, 92f)
        };
        summaryStack.AddChild(_statusLabel);

        _unitsPanel = new PanelContainer
        {
            Position = new Vector2(408f, 122f),
            Size = new Vector2(500f, 520f)
        };
        AddChild(_unitsPanel);

        var unitsPadding = new MarginContainer();
        unitsPadding.AddThemeConstantOverride("margin_left", 18);
        unitsPadding.AddThemeConstantOverride("margin_right", 18);
        unitsPadding.AddThemeConstantOverride("margin_top", 18);
        unitsPadding.AddThemeConstantOverride("margin_bottom", 18);
        _unitsPanel.AddChild(unitsPadding);

        var unitsScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        unitsPadding.AddChild(unitsScroll);

        _unitStack = new VBoxContainer();
        _unitStack.AddThemeConstantOverride("separation", 12);
        unitsScroll.AddChild(_unitStack);

        _basePanel = new PanelContainer
        {
            Position = new Vector2(932f, 122f),
            Size = new Vector2(324f, 520f)
        };
        AddChild(_basePanel);

        var basePadding = new MarginContainer();
        basePadding.AddThemeConstantOverride("margin_left", 18);
        basePadding.AddThemeConstantOverride("margin_right", 18);
        basePadding.AddThemeConstantOverride("margin_top", 18);
        basePadding.AddThemeConstantOverride("margin_bottom", 18);
        _basePanel.AddChild(basePadding);

        var baseScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        basePadding.AddChild(baseScroll);

        _baseStack = new VBoxContainer();
        _baseStack.AddThemeConstantOverride("separation", 12);
        baseScroll.AddChild(_baseStack);

        var bottomPanel = new PanelContainer
        {
            Position = new Vector2(24f, 660f),
            Size = new Vector2(1232f, 56f)
        };
        AddChild(bottomPanel);

        var bottomRow = new HBoxContainer();
        bottomRow.AddThemeConstantOverride("separation", 12);
        bottomPanel.AddChild(bottomRow);

        var titleButton = new Button
        {
            Text = "Back To Title",
            CustomMinimumSize = new Vector2(180f, 0f)
        };
        titleButton.Pressed += () => SceneRouter.Instance.GoToMainMenu();
        bottomRow.AddChild(titleButton);

        var mapButton = new Button
        {
            Text = "Back To Map",
            CustomMinimumSize = new Vector2(180f, 0f)
        };
        mapButton.Pressed += () => SceneRouter.Instance.GoToMap();
        bottomRow.AddChild(mapButton);

        var briefingButton = new Button
        {
            Text = $"Stage {GameState.Instance.SelectedStage} Briefing",
            CustomMinimumSize = new Vector2(190f, 0f),
            Disabled = GameState.Instance.SelectedStage > GameState.Instance.HighestUnlockedStage
        };
        briefingButton.Pressed += () => SceneRouter.Instance.GoToLoadout();
        bottomRow.AddChild(briefingButton);

        var multiplayerButton = new Button
        {
            Text = "Multiplayer",
            CustomMinimumSize = new Vector2(160f, 0f)
        };
        multiplayerButton.Pressed += () => SceneRouter.Instance.GoToMultiplayer();
        bottomRow.AddChild(multiplayerButton);

        var settingsButton = new Button
        {
            Text = "Settings",
            CustomMinimumSize = new Vector2(140f, 0f)
        };
        settingsButton.Pressed += () => SceneRouter.Instance.GoToSettings();
        bottomRow.AddChild(settingsButton);

        bottomRow.AddChild(new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        });

        var endlessButton = new Button
        {
            Text = "Endless Prep",
            CustomMinimumSize = new Vector2(180f, 0f)
        };
        endlessButton.Pressed += () => SceneRouter.Instance.GoToEndless();
        bottomRow.AddChild(endlessButton);
    }

    private void RefreshUi()
    {
        ApplyRouteTheme();
        _resourcesLabel.Text = $"Gold: {GameState.Instance.Gold}  |  Food: {GameState.Instance.Food}";
        _summaryLabel.Text = BuildSummaryText();
        _deckLabel.Text = BuildDeckSummaryText();
        _routeIntelLabel.Text = BuildRouteIntelText();
        RebuildRecommendations();
        _statusLabel.Text = $"Last report:\n{GameState.Instance.LastResultMessage}";
        RebuildUnitPanels();
        RebuildBaseUpgradePanels();
    }

    private void ApplyRouteTheme()
    {
        var stage = GameState.Instance.BuildConfiguredCampaignStage(Mathf.Clamp(GameState.Instance.SelectedStage, 1, GameState.Instance.MaxStage));
        var route = RouteCatalog.Get(stage.MapId);
        _backgroundTop.Color = route.BackgroundTop;
        _backgroundBottom.Color = route.BackgroundBottom;
        _accentBand.Color = route.BannerAccent;
        _titlePanel.SelfModulate = route.BannerPanel.Lightened(0.08f);
        _summaryPanel.SelfModulate = route.BannerPanel;
        _unitsPanel.SelfModulate = route.BannerPanel.Darkened(0.02f);
        _basePanel.SelfModulate = route.BannerPanel.Lightened(0.02f);
    }

    private string BuildSummaryText()
    {
        var ownedUnits = GameState.Instance.GetOwnedPlayerUnits().Count;
        var ownedSpells = GameState.Instance.GetOwnedPlayerSpells().Count;
        var nextExploreLine = GameState.Instance.CanExploreNextStage(out var nextStage, out var exploreMessage)
            ? $"Next exploration: Stage {nextStage.StageNumber} for {GameState.Instance.GetStageExploreFoodCost(nextStage.StageNumber)} food."
            : exploreMessage;

        return
            $"Owned units: {ownedUnits}/{GameData.PlayerRosterIds.Length}\n" +
            $"Owned spells: {ownedSpells}/{GameData.PlayerSpellIds.Length}\n" +
            $"Heroic directives secured: {GameState.Instance.ClaimedCampaignDirectiveCount}/{GameState.Instance.MaxStage}\n" +
            $"{GameState.Instance.BuildCampaignReadinessInlineSummary(GameState.Instance.SelectedStage)}\n" +
            $"War wagon plating level: {GameState.Instance.GetBaseUpgradeLevel(BaseUpgradeCatalog.HullPlatingId)}/{GameState.Instance.MaxBaseUpgradeLevel}\n" +
            $"Stores level: {GameState.Instance.GetBaseUpgradeLevel(BaseUpgradeCatalog.PantryId)}/{GameState.Instance.MaxBaseUpgradeLevel}\n" +
            $"March drum level: {GameState.Instance.GetBaseUpgradeLevel(BaseUpgradeCatalog.DispatchConsoleId)}/{GameState.Instance.MaxBaseUpgradeLevel}\n" +
            $"Rune beacon level: {GameState.Instance.GetBaseUpgradeLevel(BaseUpgradeCatalog.SignalRelayId)}/{GameState.Instance.MaxBaseUpgradeLevel}\n\n" +
            "Economy rules:\n" +
            "- Gold buys units, spells, unit levels, spell levels, and war wagon upgrades.\n" +
            "- Food pays for stage entry and map exploration.\n\n" +
            nextExploreLine;
    }

    private string BuildRouteIntelText()
    {
        var selectedStage = GameState.Instance.BuildConfiguredCampaignStage(Mathf.Clamp(GameState.Instance.SelectedStage, 1, GameState.Instance.MaxStage));
        var route = RouteCatalog.Get(selectedStage.MapId);
        var upcomingStages = GameData.GetStagesForMap(selectedStage.MapId)
            .Where(stage => stage.StageNumber >= selectedStage.StageNumber)
            .Take(3)
            .ToArray();

        var intel =
            $"Selected route: {route.Title}\n" +
            $"{route.CampaignSubtitle}\n" +
            $"Pressure profile: {route.PressureSummary}\n" +
            $"Current target: Stage {selectedStage.StageNumber} - {selectedStage.StageName}\n" +
            $"Deploy cost: {GameState.Instance.GetStageEntryFoodCost(selectedStage.StageNumber)} food  |  Clear reward: +{selectedStage.RewardGold} gold, +{selectedStage.RewardFood} food\n" +
            $"{GameState.Instance.BuildCampaignDirectiveStatusText(selectedStage.StageNumber)}\n" +
            $"{GameState.Instance.BuildCampaignReadinessDetailedSummary(selectedStage.StageNumber)}\n" +
            $"{StageMissionEvents.BuildSummaryText(selectedStage)}";

        if (TryGetNextStageForMap(selectedStage.MapId, out var nextRouteStage))
        {
            intel += $"\nNext route exploration: Stage {nextRouteStage.StageNumber} for {GameState.Instance.GetStageExploreFoodCost(nextRouteStage.StageNumber)} food";
        }
        else
        {
            intel += "\nNext route exploration: Route fully explored";
        }

        if (upcomingStages.Length > 0)
        {
            intel += "\n\nUpcoming route stops:";
            foreach (var stage in upcomingStages)
            {
                var unlocked = stage.StageNumber <= GameState.Instance.HighestUnlockedStage ? "Ready" : "Locked";
                intel +=
                    $"\nS{stage.StageNumber} {stage.StageName}  |  {unlocked}" +
                    $"\n  Entry {GameState.Instance.GetStageEntryFoodCost(stage.StageNumber)} food  |  Reward +{stage.RewardGold}g / +{stage.RewardFood}f";
            }
        }

        var pendingUnits = GameData.GetPlayerUnits()
            .Where(unit => !GameState.Instance.IsUnitOwned(unit.Id))
            .OrderBy(unit => unit.UnlockStage)
            .Take(2)
            .ToArray();

        if (pendingUnits.Length > 0)
        {
            intel += "\n\nNext unit unlocks:";
            foreach (var unit in pendingUnits)
            {
                var unlockStage = GameData.GetStage(Mathf.Clamp(unit.UnlockStage, 1, GameState.Instance.MaxStage));
                var unlockState = GameState.Instance.IsUnitAvailableForPurchase(unit.Id)
                    ? $"Shop unlocked  |  {GameState.Instance.GetUnitPurchaseCost(unit.Id)} gold"
                    : $"Explore stage {unit.UnlockStage}";
                intel += $"\n{unit.DisplayName} - {unlockStage.MapName} S{unit.UnlockStage}  |  {unlockState}";
            }
        }

        var pendingSpells = GameData.GetPlayerSpells()
            .Where(spell => !GameState.Instance.IsSpellOwned(spell.Id))
            .OrderBy(spell => spell.UnlockStage)
            .Take(2)
            .ToArray();
        if (pendingSpells.Length > 0)
        {
            intel += "\n\nNext spell unlocks:";
            foreach (var spell in pendingSpells)
            {
                var unlockStage = GameData.GetStage(Mathf.Clamp(spell.UnlockStage, 1, GameState.Instance.MaxStage));
                var unlockState = GameState.Instance.IsSpellAvailableForPurchase(spell.Id)
                    ? $"Archive open  |  {GameState.Instance.GetSpellPurchaseCost(spell.Id)} gold"
                    : $"Explore stage {spell.UnlockStage}";
                intel += $"\n{spell.DisplayName} - {unlockStage.MapName} S{spell.UnlockStage}  |  {unlockState}";
            }
        }

        return intel;
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

    private void RebuildRecommendations()
    {
        foreach (var child in _recommendationStack.GetChildren())
        {
            child.QueueFree();
        }

        var recommendations = BuildRecommendations();
        if (recommendations.Count == 0)
        {
            _recommendationStack.AddChild(new Label
            {
                Text = "No urgent armory actions. The caravan is broadly ready for the selected stage.",
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            });
            return;
        }

        foreach (var recommendation in recommendations)
        {
            _recommendationStack.AddChild(BuildRecommendationPanel(recommendation));
        }
    }

    private List<ShopRecommendation> BuildRecommendations()
    {
        var stage = GameState.Instance.BuildConfiguredCampaignStage(Mathf.Clamp(GameState.Instance.SelectedStage, 1, GameState.Instance.MaxStage));
        var recommendations = new List<ShopRecommendation>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var counts = BuildStageEnemyCounts(stage);
        var runnerCount = counts.TryGetValue(GameData.EnemyRunnerId, out var runnerValue) ? runnerValue : 0;
        var saboteurCount = counts.TryGetValue(GameData.EnemySaboteurId, out var saboteurValue) ? saboteurValue : 0;
        var howlerCount = counts.TryGetValue(GameData.EnemyHowlerId, out var howlerValue) ? howlerValue : 0;
        var jammerCount = counts.TryGetValue(GameData.EnemyJammerId, out var jammerValue) ? jammerValue : 0;
        var spitterCount = counts.TryGetValue(GameData.EnemySpitterId, out var spitterValue) ? spitterValue : 0;
        var splitterCount = counts.TryGetValue(GameData.EnemySplitterId, out var splitterValue) ? splitterValue : 0;
        var walkerCount = counts.TryGetValue(GameData.EnemyWalkerId, out var walkerValue) ? walkerValue : 0;
        var busSensitiveObjective = stage.Objectives.Any(objective =>
            objective != null &&
            objective.Type.Equals("bus_hull_ratio", StringComparison.OrdinalIgnoreCase));
        var hazardHeavyStage = StageHazards.HasHazards(stage);
        var primaryMissionEvent = StageMissionEvents.GetPrimaryEvent(stage);
        var primaryMissionType = primaryMissionEvent?.NormalizedType ?? "";
        var barricadeHeavyStage =
            stage.EnemyBaseHealth >= 680f ||
            stage.Modifiers.Any(modifier =>
                modifier != null &&
                modifier.Type.Equals("reinforced_barricade", StringComparison.OrdinalIgnoreCase));
        var heavyCount =
            (counts.TryGetValue(GameData.EnemyBruteId, out var bruteValue) ? bruteValue : 0) +
            (counts.TryGetValue(GameData.EnemyCrusherId, out var crusherValue) ? crusherValue : 0) +
            (counts.TryGetValue(GameData.EnemyBossId, out var bossValue) ? bossValue : 0);

        if (!GameState.Instance.HasFullDeck)
        {
            var reserveUnit = GameState.Instance.GetOwnedPlayerUnits()
                .FirstOrDefault(unit => !GameState.Instance.IsUnitInActiveDeck(unit.Id));
            if (reserveUnit != null)
            {
                TryAddRecommendation(
                    recommendations,
                    seen,
                    new ShopRecommendation(
                        $"deck:{reserveUnit.Id}",
                        "Fill the active squad",
                        $"{reserveUnit.DisplayName} is already owned and can fill the empty squad slot immediately.",
                        $"Add {reserveUnit.DisplayName}",
                        () =>
                        {
                            GameState.Instance.ToggleDeckUnit(reserveUnit.Id, out var message);
                            _statusLabel.Text = $"Last report:\n{message}";
                        }));
            }
        }

        var directive = GameState.Instance.GetCampaignDirective(stage.StageNumber);
        if (GameState.Instance.IsCampaignDirectiveUnlocked(stage.StageNumber) &&
            directive != null &&
            !GameState.Instance.IsCampaignDirectiveArmed(stage.StageNumber))
        {
            TryAddRecommendation(
                recommendations,
                seen,
                new ShopRecommendation(
                    $"directive:{stage.StageNumber}",
                    $"Arm {directive.Title}",
                    $"{directive.Summary}\n{CampaignDirectiveCatalog.BuildRewardSummary(directive)}",
                    "Arm directive",
                    () =>
                    {
                        GameState.Instance.ToggleCampaignDirective(stage.StageNumber, out var message);
                        _statusLabel.Text = $"Last report:\n{message}";
                    }));
        }

        foreach (var unit in GameState.Instance.GetActiveDeckUnits())
        {
            if (recommendations.Count >= 3)
            {
                break;
            }

            var targetDoctrineId = ResolveRecommendedDoctrineId(
                unit,
                supportPressure: howlerCount > 0 || jammerCount > 0 || spitterCount > 0,
                breachPressure: barricadeHeavyStage || heavyCount > 0,
                hullSensitive: busSensitiveObjective || hazardHeavyStage,
                crowdPressure: splitterCount >= 2 || walkerCount >= 8,
                rushPressure: runnerCount >= 3 || saboteurCount > 0);
            if (string.IsNullOrWhiteSpace(targetDoctrineId))
            {
                continue;
            }

            TryAddDoctrineRecommendation(
                recommendations,
                seen,
                unit,
                targetDoctrineId,
                stage);
        }

        switch (primaryMissionType)
        {
            case "ritual_site":
                TryAddUnitRecommendation(
                    recommendations,
                    seen,
                    GameData.PlayerCoordinatorId,
                    "Hold the ritual circle",
                    $"{StageMissionEvents.ResolveTitle(primaryMissionEvent)} needs steady allied presence. Battle Monk helps stacked defenders trade better while the caravan sits on the circle.");

                TryAddSpellRecommendation(
                    recommendations,
                    seen,
                    GameData.SpellBarrierWardId,
                    "Fortify the ritual hold",
                    "Barrier Ward buys time on shrine and seal circles where the caravan has to hold ground instead of only racing the next wave.");
                break;
            case "relic_escort":
                TryAddUnitRecommendation(
                    recommendations,
                    seen,
                    GameData.PlayerDefenderId,
                    "Anchor the escort lane",
                    $"{StageMissionEvents.ResolveTitle(primaryMissionEvent)} rewards a stable hold more than raw burst. Shield Knight gives the escort lane a frontline that can actually stand in the aisle.");

                TryAddSpellRecommendation(
                    recommendations,
                    seen,
                    GameData.SpellHealId,
                    "Patch the escort lane",
                    "Heal keeps the war wagon and escort window alive when the relic convoy needs one more clean push.");
                break;
            case "gate_breach":
                TryAddUnitRecommendation(
                    recommendations,
                    seen,
                    GameData.PlayerBreacherId,
                    "Exploit the breach window",
                    $"{StageMissionEvents.ResolveTitle(primaryMissionEvent)} turns lane control into direct siege progress. Halberdier converts that window into real gatehouse damage.");

                TryAddBaseRecommendation(
                    recommendations,
                    seen,
                    BaseUpgradeCatalog.DispatchConsoleId,
                    "Cycle the breach line faster",
                    "March Drum helps the caravan refill the breach lane before the wall team loses its opening.");
                break;
        }

        if (spitterCount > 0)
        {
            TryAddUnitRecommendation(
                recommendations,
                seen,
                GameData.PlayerMarksmanId,
                "Counter ranged pressure",
                $"Stage {stage.StageNumber} fields {spitterCount} blight caster contacts. A long-range card helps clean them up before they chip the war wagon.");

            TryAddUnitRecommendation(
                recommendations,
                seen,
                GameData.PlayerRangerId,
                "Add mobile ranged support",
                "Crossbowman gives the caravan another projectile unit for stages that stack blight casters and mixed backline pressure.");

            TryAddSpellRecommendation(
                recommendations,
                seen,
                GameData.SpellLightningStrikeId,
                "Crack priority backliners",
                "Lightning Strike tags ranged or support threats before they sit safely behind the front.");
        }

        if (howlerCount > 0)
        {
            TryAddUnitRecommendation(
                recommendations,
                seen,
                GameData.PlayerMarksmanId,
                "Delete support heralds early",
                $"Stage {stage.StageNumber} includes {howlerCount} dread herald contacts that buff nearby undead speed and damage. Mage helps remove them before the lane spikes.");

            TryAddUnitRecommendation(
                recommendations,
                seen,
                GameData.PlayerRangerId,
                "Pressure the howl lane",
                "Crossbowman gives the caravan a second fast ranged answer when support undead sit behind heavier bodies.");

            TryAddSpellRecommendation(
                recommendations,
                seen,
                GameData.SpellLightningStrikeId,
                "Punish exposed supports",
                "Lightning Strike gives the caravan a direct answer when dread heralds or hexers hide behind heavier bodies.");
        }

        if (howlerCount > 0 ||
            jammerCount > 0 ||
            (spitterCount >= 3 && heavyCount >= 2) ||
            stage.MapId.Equals(RouteCatalog.QuarantineId, StringComparison.OrdinalIgnoreCase))
        {
            TryAddUnitRecommendation(
                recommendations,
                seen,
                GameData.PlayerCoordinatorId,
                "Force-multiply the caravan",
                stage.MapId.Equals(RouteCatalog.QuarantineId, StringComparison.OrdinalIgnoreCase)
                    ? "Ashen Ward stages pile ranged support and breach dives into the same lane. Battle Monk buffs nearby allies so the caravan trades better through long late-game pushes."
                    : "Battle Monk adds a live attack and speed aura, which helps the whole lane keep up once support undead and heavy bodies start stacking together.");
        }

        if (jammerCount > 0)
        {
            TryAddBaseRecommendation(
                recommendations,
                seen,
                BaseUpgradeCatalog.SignalRelayId,
                "Harden caravan wards",
                $"Stage {stage.StageNumber} includes {jammerCount} hexer contacts that stall courage flow and drag card recovery. Rune Beacon cuts jam uptime and blunts the suppression window.");

            TryAddUnitRecommendation(
                recommendations,
                seen,
                GameData.PlayerMarksmanId,
                "Remove hexers early",
                "Mage helps pick off hexer supports before they chain signal disruption into the next surge.");

            TryAddSpellRecommendation(
                recommendations,
                seen,
                GameData.SpellBarrierWardId,
                "Stabilize a jammed lane",
                "Barrier Ward buys time through suppression windows when the caravan cannot answer immediately with normal deploy tempo.");
        }

        if (splitterCount >= 2 || walkerCount >= 8 || (howlerCount > 0 && splitterCount > 0))
        {
            TryAddUnitRecommendation(
                recommendations,
                seen,
                GameData.PlayerGrenadierId,
                "Break clustered waves",
                $"Stage {stage.StageNumber} stacks grouped contacts and support bodies. Alchemist splash helps clear bone nests and buffed crowds before they snowball.");

            TryAddSpellRecommendation(
                recommendations,
                seen,
                GameData.SpellFireballId,
                "Burn down crowd spikes",
                "Fireball is the fastest answer when grouped waves start stacking faster than the unit line can chew through them.");
        }

        if (heavyCount > 0)
        {
            TryAddUnitRecommendation(
                recommendations,
                seen,
                GameData.PlayerDefenderId,
                "Brace for heavy undead",
                $"Stage {stage.StageNumber} includes {heavyCount} heavy contacts. Shield Knight upgrades help the line survive bone juggernauts and grave brutes.");

            TryAddUnitRecommendation(
                recommendations,
                seen,
                GameData.PlayerSpearId,
                "Trade safely into heavies",
                "Spearman reach lets the frontline trade with brutes and juggernauts at a safer distance than shorter melee cards.");

            TryAddBaseRecommendation(
                recommendations,
                seen,
                BaseUpgradeCatalog.HullPlatingId,
                "Reinforce the war wagon",
                "War Wagon Plating buys more margin against heavy pressure and missed contact pickups.");
        }

        if (barricadeHeavyStage)
        {
            TryAddUnitRecommendation(
                recommendations,
                seen,
                GameData.PlayerBreacherId,
                "Punch through the gatehouse",
                "This district hardens the enemy objective. Halberdier gives the caravan a stronger base-damage card for reinforced late-game stages.");
        }

        if (busSensitiveObjective || StageEncounterIntel.ResolveThreatRating(stage) is "Severe" or "Extreme")
        {
            TryAddUnitRecommendation(
                recommendations,
                seen,
                GameData.PlayerMechanicId,
                "Protect the war wagon hull",
                "This stage cares about hull preservation. Siege Engineer can patch the war wagon between surges when the lane is briefly stable.");

            TryAddSpellRecommendation(
                recommendations,
                seen,
                GameData.SpellHealId,
                "Patch the caravan on demand",
                "Heal lets the run recover a cracked lane or war wagon hull immediately instead of waiting for a safe Siege Engineer window.");
        }

        if (hazardHeavyStage)
        {
            TryAddBaseRecommendation(
                recommendations,
                seen,
                BaseUpgradeCatalog.HullPlatingId,
                "Buffer hazard pulses",
                "This stage has live battlefield hazards. Extra hull buys time when vents or bursts clip the caravan line.");

            TryAddSpellRecommendation(
                recommendations,
                seen,
                GameData.SpellFrostBurstId,
                "Slow hazard pileups",
                "Frost Burst holds dense pushes in telegraphed hazard zones so the caravan has more time to reposition and recover.");
        }

        if (runnerCount >= 3 || saboteurCount > 0)
        {
            TryAddUnitRecommendation(
                recommendations,
                seen,
                GameData.PlayerBrawlerId,
                "Meet fast rushes early",
                saboteurCount > 0
                    ? $"Stage {stage.StageNumber} includes {saboteurCount} sapper contacts that dive the war wagon. Swordsman upgrades help intercept them before they cash in base damage."
                    : $"Stage {stage.StageNumber} opens with {runnerCount} fast contacts. Swordsman upgrades stabilize the front line.");

            TryAddUnitRecommendation(
                recommendations,
                seen,
                GameData.PlayerSpearId,
                "Extend the intercept line",
                "Spearman reach lets the frontline catch runners and sappers further up the lane before they slip past shorter melee cards.");

            TryAddUnitRecommendation(
                recommendations,
                seen,
                GameData.PlayerRaiderId,
                "Add a fast skirmisher",
                saboteurCount > 0
                    ? "Cavalry Rider helps run down sappers and peel pressure off the war wagon before they convert into gatehouse damage."
                    : "Cavalry Rider helps cover ghoul-heavy stages and rotate pressure away from the war wagon.");
        }

        TryAddBaseRecommendation(
            recommendations,
            seen,
            BaseUpgradeCatalog.DispatchConsoleId,
            "Speed up card recovery",
            "March Drum shortens card recovery so the caravan can answer waves with fewer dead turns.");

        TryAddBaseRecommendation(
            recommendations,
            seen,
            BaseUpgradeCatalog.PantryId,
            "Expand courage economy",
            "Caravan Stores let the caravan front-load bigger defenses and recover faster after expensive drops.");

        if (recommendations.Count < 3)
        {
            foreach (var unit in GameState.Instance.GetActiveDeckUnits().OrderBy(unit => GameState.Instance.GetUnitLevel(unit.Id)))
            {
                if (!TryAddUnitRecommendation(
                    recommendations,
                    seen,
                    unit.Id,
                    $"Sharpen {unit.DisplayName}",
                    $"{unit.DisplayName} is already in the active squad, so upgrading it has immediate value on the next deployment."))
                {
                    continue;
                }

                if (recommendations.Count >= 3)
                {
                    break;
                }
            }
        }

        return recommendations.Take(3).ToList();
    }

    private Control BuildRecommendationPanel(ShopRecommendation recommendation)
    {
        var panel = new PanelContainer
        {
            SelfModulate = new Color("22333b")
        };

        var padding = new MarginContainer();
        padding.AddThemeConstantOverride("margin_left", 12);
        padding.AddThemeConstantOverride("margin_right", 12);
        padding.AddThemeConstantOverride("margin_top", 10);
        padding.AddThemeConstantOverride("margin_bottom", 10);
        panel.AddChild(padding);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 6);
        padding.AddChild(stack);

        stack.AddChild(new Label
        {
            Text = recommendation.Title
        });

        stack.AddChild(new Label
        {
            Text = recommendation.Summary,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        var actionButton = new Button
        {
            Text = recommendation.ActionLabel,
            Disabled = recommendation.Disabled,
            CustomMinimumSize = new Vector2(0f, 34f)
        };
        actionButton.Pressed += () =>
        {
            recommendation.Execute();
            RefreshUi();
        };
        stack.AddChild(actionButton);

        return panel;
    }

    private bool TryAddUnitRecommendation(
        List<ShopRecommendation> recommendations,
        HashSet<string> seen,
        string unitId,
        string title,
        string rationale)
    {
        var unit = GameData.GetUnit(unitId);
        var available = GameState.Instance.IsUnitAvailableForPurchase(unit.Id);
        var owned = GameState.Instance.IsUnitOwned(unit.Id);
        var inDeck = owned && GameState.Instance.IsUnitInActiveDeck(unit.Id);
        var level = GameState.Instance.GetUnitLevel(unit.Id);
        var canAddToDeck = owned && !inDeck && !GameState.Instance.HasFullDeck;

        if (!available)
        {
            return false;
        }

        if (!owned)
        {
            var purchaseCost = GameState.Instance.GetUnitPurchaseCost(unit.Id);
            return TryAddRecommendation(
                recommendations,
                seen,
                new ShopRecommendation(
                    $"buy:{unit.Id}",
                    title,
                    $"{rationale}\nCost: {purchaseCost} gold.",
                    $"Buy {unit.DisplayName}",
                    () =>
                    {
                        GameState.Instance.TryPurchaseUnit(unit.Id, out var message);
                        _statusLabel.Text = $"Last report:\n{message}";
                    },
                    GameState.Instance.Gold < purchaseCost));
        }

        if (canAddToDeck)
        {
            return TryAddRecommendation(
                recommendations,
                seen,
                new ShopRecommendation(
                    $"deck:{unit.Id}",
                    title,
                    $"{rationale}\n{unit.DisplayName} is owned and ready to slot into the active squad.",
                    $"Add {unit.DisplayName}",
                    () =>
                    {
                        GameState.Instance.ToggleDeckUnit(unit.Id, out var message);
                        _statusLabel.Text = $"Last report:\n{message}";
                    }));
        }

        if (level < GameState.Instance.MaxUnitLevel)
        {
            var upgradeCost = GameState.Instance.GetUnitUpgradeCost(unit.Id);
            return TryAddRecommendation(
                recommendations,
                seen,
                new ShopRecommendation(
                    $"upgrade_unit:{unit.Id}",
                    title,
                    $"{rationale}\nUpgrade cost: {upgradeCost} gold.",
                    $"Upgrade {unit.DisplayName}",
                    () =>
                    {
                        if (GameState.Instance.TryUpgradeUnit(unit.Id, out var message))
                        {
                            AudioDirector.Instance?.PlayUpgradeConfirm();
                        }
                        _statusLabel.Text = $"Last report:\n{message}";
                    },
                    GameState.Instance.Gold < upgradeCost));
        }

        return false;
    }

    private bool TryAddDoctrineRecommendation(
        List<ShopRecommendation> recommendations,
        HashSet<string> seen,
        UnitDefinition unit,
        string doctrineId,
        StageDefinition stage)
    {
        if (unit == null ||
            !GameState.Instance.IsUnitDoctrineUnlocked(unit.Id) ||
            GameState.Instance.GetUnitDoctrineId(unit.Id).Equals(doctrineId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var doctrine = UnitDoctrineCatalog.GetOrNull(doctrineId);
        if (doctrine == null)
        {
            return false;
        }

        var retrainCost = GameState.Instance.GetUnitDoctrineRetrainCost(unit.Id);
        return TryAddRecommendation(
            recommendations,
            seen,
            new ShopRecommendation(
                $"doctrine:{unit.Id}:{doctrine.Id}",
                $"Forge {doctrine.Title}",
                $"Stage {stage.StageNumber} pressure favors {doctrine.Title} on {unit.DisplayName}. {doctrine.Summary}\n" +
                (retrainCost > 0
                    ? $"Retrain cost: {retrainCost} gold."
                    : "First doctrine choice is ready."),
                retrainCost > 0 ? $"Retrain {unit.DisplayName}" : $"Forge {unit.DisplayName}",
                () =>
                {
                    GameState.Instance.TrySelectUnitDoctrine(unit.Id, doctrine.Id, out var message);
                    _statusLabel.Text = $"Last report:\n{message}";
                },
                retrainCost > 0 && GameState.Instance.Gold < retrainCost));
    }

    private bool TryAddSpellRecommendation(
        List<ShopRecommendation> recommendations,
        HashSet<string> seen,
        string spellId,
        string title,
        string rationale)
    {
        var spell = GameData.GetSpell(spellId);
        var available = GameState.Instance.IsSpellAvailableForPurchase(spell.Id);
        var owned = GameState.Instance.IsSpellOwned(spell.Id);
        var equipped = owned && GameState.Instance.IsSpellInActiveDeck(spell.Id);
        var canEquip = owned && !equipped && GameState.Instance.ActiveDeckSpellIds.Count < GameState.Instance.SpellDeckSizeLimit;

        if (!available)
        {
            return false;
        }

        if (!owned)
        {
            var purchaseCost = GameState.Instance.GetSpellPurchaseCost(spell.Id);
            return TryAddRecommendation(
                recommendations,
                seen,
                new ShopRecommendation(
                    $"buy_spell:{spell.Id}",
                    title,
                    $"{rationale}\nCost: {purchaseCost} gold.",
                    $"Scribe {spell.DisplayName}",
                    () =>
                    {
                        GameState.Instance.TryPurchaseSpell(spell.Id, out var message);
                        _statusLabel.Text = $"Last report:\n{message}";
                    },
                    GameState.Instance.Gold < purchaseCost));
        }

        if (canEquip)
        {
            return TryAddRecommendation(
                recommendations,
                seen,
                new ShopRecommendation(
                    $"deck_spell:{spell.Id}",
                    title,
                    $"{rationale}\n{spell.DisplayName} is owned and can fill an empty spell slot immediately.",
                    $"Equip {spell.DisplayName}",
                    () =>
                    {
                        GameState.Instance.ToggleDeckSpell(spell.Id, out var message);
                        _statusLabel.Text = $"Last report:\n{message}";
                    }));
        }

        var spellLevel = GameState.Instance.GetSpellLevel(spell.Id);
        if (owned && spellLevel < GameState.Instance.MaxSpellLevel)
        {
            var upgradeCost = GameState.Instance.GetSpellUpgradeCost(spell.Id);
            return TryAddRecommendation(
                recommendations,
                seen,
                new ShopRecommendation(
                    $"upgrade_spell:{spell.Id}",
                    title,
                    $"{rationale}\nLv{spellLevel} -> Lv{spellLevel + 1} upgrade costs {upgradeCost} gold.",
                    $"Upgrade {spell.DisplayName}",
                    () =>
                    {
                        if (GameState.Instance.TryUpgradeSpell(spell.Id, out var message))
                        {
                            AudioDirector.Instance?.PlayUpgradeConfirm();
                        }
                        _statusLabel.Text = $"Last report:\n{message}";
                    },
                    GameState.Instance.Gold < upgradeCost));
        }

        return false;
    }

    private bool TryAddBaseRecommendation(
        List<ShopRecommendation> recommendations,
        HashSet<string> seen,
        string upgradeId,
        string title,
        string rationale)
    {
        var definition = BaseUpgradeCatalog.Get(upgradeId);
        var level = GameState.Instance.GetBaseUpgradeLevel(upgradeId);
        if (level >= definition.MaxLevel)
        {
            return false;
        }

        var cost = GameState.Instance.GetBaseUpgradeCost(upgradeId);
        return TryAddRecommendation(
            recommendations,
            seen,
            new ShopRecommendation(
                $"upgrade_base:{upgradeId}",
                title,
                $"{rationale}\nUpgrade cost: {cost} gold.",
                $"Upgrade {definition.Title}",
                () =>
                {
                    if (GameState.Instance.TryUpgradeBase(upgradeId, out var message))
                    {
                        AudioDirector.Instance?.PlayUpgradeConfirm();
                    }
                    _statusLabel.Text = $"Last report:\n{message}";
                },
                GameState.Instance.Gold < cost));
    }

    private static bool TryAddRecommendation(
        List<ShopRecommendation> recommendations,
        HashSet<string> seen,
        ShopRecommendation recommendation)
    {
        if (!seen.Add(recommendation.Id))
        {
            return false;
        }

        recommendations.Add(recommendation);
        return true;
    }

    private static string ResolveRecommendedDoctrineId(
        UnitDefinition unit,
        bool supportPressure,
        bool breachPressure,
        bool hullSensitive,
        bool crowdPressure,
        bool rushPressure)
    {
        var tag = SquadSynergyCatalog.NormalizeTag(unit?.SquadTag);
        if (string.IsNullOrWhiteSpace(tag))
        {
            return "";
        }

        if (hullSensitive)
        {
            return tag switch
            {
                SquadSynergyCatalog.FrontlineTag => "frontline_bastion",
                SquadSynergyCatalog.SupportTag => "support_ward_circle",
                SquadSynergyCatalog.BreachTag => "breach_iron_vanguard",
                SquadSynergyCatalog.ReconTag => "recon_trailblazer",
                _ => ""
            };
        }

        if (supportPressure || breachPressure)
        {
            return tag switch
            {
                SquadSynergyCatalog.FrontlineTag => "frontline_duelist",
                SquadSynergyCatalog.SupportTag => "support_quick_chant",
                SquadSynergyCatalog.BreachTag => "breach_siegebreaker",
                SquadSynergyCatalog.ReconTag => "recon_deadeye",
                _ => ""
            };
        }

        if (crowdPressure || rushPressure)
        {
            return tag switch
            {
                SquadSynergyCatalog.FrontlineTag => "frontline_bastion",
                SquadSynergyCatalog.SupportTag => "support_quick_chant",
                SquadSynergyCatalog.BreachTag => "breach_iron_vanguard",
                SquadSynergyCatalog.ReconTag => "recon_trailblazer",
                _ => ""
            };
        }

        return "";
    }

    private static Dictionary<string, int> BuildStageEnemyCounts(StageDefinition stage)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (stage?.Waves == null)
        {
            return counts;
        }

        foreach (var wave in stage.Waves)
        {
            foreach (var entry in wave.Entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.UnitId))
                {
                    continue;
                }

                counts[entry.UnitId] = counts.TryGetValue(entry.UnitId, out var current)
                    ? current + Mathf.Max(1, entry.Count)
                    : Mathf.Max(1, entry.Count);
            }
        }

        return counts;
    }

    private string BuildDeckSummaryText()
    {
        var deckUnits = GameState.Instance.GetActiveDeckUnits();
        var deckSpells = GameState.Instance.GetActiveDeckSpells();
        if (deckUnits.Count == 0)
        {
            return "No units in the active deck.";
        }

        var lines =
            $"Cards: {deckUnits.Count}/{GameState.Instance.DeckSizeLimit}\n" +
            $"Synergy: {GameState.Instance.BuildActiveDeckSynergyInlineSummary()}\n" +
            $"Magic: {(deckSpells.Count == 0 ? "none equipped" : string.Join(", ", deckSpells.Select(spell => spell.DisplayName)))}\n" +
            $"{GameState.Instance.BuildCampaignReadinessDetailedSummary(GameState.Instance.SelectedStage)}";
        for (var i = 0; i < deckUnits.Count; i++)
        {
            var unit = deckUnits[i];
            lines +=
                $"\n{i + 1}. {unit.DisplayName} Lv{GameState.Instance.GetUnitLevel(unit.Id)}" +
                $"  |  {SquadSynergyCatalog.GetTagDisplayName(unit.SquadTag)}" +
                $"  |  {GameState.Instance.BuildUnitDoctrineInlineText(unit.Id)}";
        }

        return lines;
    }

    private string BuildUnitPreviewText(UnitDefinition unit, bool owned, int level, bool isMaxLevel)
    {
        var currentStats = GameState.Instance.BuildPlayerUnitStatsAtLevel(unit, level);
        var effectiveDeployCooldown = GameState.Instance.ApplyPlayerDeployCooldownUpgrade(unit.DeployCooldown);
        var summary =
            $"HP {Mathf.RoundToInt(currentStats.MaxHealth)}  |  ATK {currentStats.AttackDamage:0.#}  |  Range {currentStats.AttackRange:0.#}\n" +
            $"Deploy CD {effectiveDeployCooldown:0.#}s  |  Base {currentStats.BaseDamage}" +
            UnitStatText.BuildInlineTraits(currentStats);
        var doctrineSummary = owned
            ? GameState.Instance.BuildUnitDoctrineStatusText(unit.Id)
            : $"Doctrine unlocks at Lv{GameState.Instance.UnitDoctrineUnlockLevel}.";

        if (!owned)
        {
            return summary + $"\n{doctrineSummary}";
        }

        if (isMaxLevel)
        {
            return summary + "\nNext upgrade: max level reached." + $"\n{doctrineSummary}";
        }

        var nextStats = GameState.Instance.BuildPlayerUnitStatsAtLevel(unit, level + 1);
        summary +=
            $"\nNext Lv{level + 1}: " +
            $"HP +{Mathf.RoundToInt(nextStats.MaxHealth - currentStats.MaxHealth)}  |  " +
            $"ATK +{(nextStats.AttackDamage - currentStats.AttackDamage):0.#}  |  " +
            $"Base +{nextStats.BaseDamage - currentStats.BaseDamage}" +
            (currentStats.AttackSplashRadius > 0.05f || nextStats.AttackSplashRadius > 0.05f
                ? $"  |  Splash {nextStats.AttackSplashRadius:0.#}"
                : "") +
            (currentStats.BusRepairAmount > 0.05f || nextStats.BusRepairAmount > 0.05f
                ? $"  |  Repair +{(nextStats.BusRepairAmount - currentStats.BusRepairAmount):0.#}"
                : "") +
            (UnitStatText.HasAura(nextStats) ? $"  |  {UnitStatText.BuildAuraSummary(nextStats)}" : "");
        return summary + $"\n{doctrineSummary}";
    }

    private string BuildBaseUpgradeEffectText(BaseUpgradeDefinition upgrade, int level)
    {
        return upgrade.Id switch
        {
            BaseUpgradeCatalog.HullPlatingId =>
                $"+{Mathf.RoundToInt((GameState.Instance.GetPlayerBaseHealthScaleAtLevel(level) - 1f) * 100f)}% war wagon hull",
            BaseUpgradeCatalog.PantryId =>
                $"+{GameState.Instance.GetPlayerCourageMaxBonusAtLevel(level):0} max courage  |  " +
                $"+{Mathf.RoundToInt((GameState.Instance.GetPlayerCourageGainScaleAtLevel(level) - 1f) * 100f)}% gain",
            BaseUpgradeCatalog.DispatchConsoleId =>
                $"-{Mathf.RoundToInt((1f - GameState.Instance.GetPlayerDeployCooldownScaleAtLevel(level)) * 100f)}% card recovery",
            BaseUpgradeCatalog.SignalRelayId =>
                $"-{Mathf.RoundToInt((1f - GameState.Instance.GetPlayerSignalJamDurationScaleAtLevel(level)) * 100f)}% jam time  |  " +
                $"-{Mathf.RoundToInt((1f - GameState.Instance.GetPlayerSignalJamCooldownPenaltyScaleAtLevel(level)) * 100f)}% jam cooldown hit  |  " +
                $"+{Mathf.RoundToInt(GameState.Instance.GetPlayerSignalJamSuppressionMitigationAtLevel(level) * 100f)}% jam resist",
            _ => upgrade.Summary
        };
    }

    private void RebuildUnitPanels()
    {
        foreach (var child in _unitStack.GetChildren())
        {
            child.QueueFree();
        }

        _unitStack.AddChild(new Label
        {
            Text = "Units"
        });

        foreach (var unit in GameData.GetPlayerUnits())
        {
            _unitStack.AddChild(BuildUnitPanel(unit));
        }

        _unitStack.AddChild(new Label
        {
            Text = "Spells"
        });

        foreach (var spell in GameData.GetPlayerSpells())
        {
            _unitStack.AddChild(BuildSpellPanel(spell));
        }
    }

    private Control BuildUnitPanel(UnitDefinition unit)
    {
        var owned = GameState.Instance.IsUnitOwned(unit.Id);
        var available = GameState.Instance.IsUnitAvailableForPurchase(unit.Id);
        var inDeck = owned && GameState.Instance.IsUnitInActiveDeck(unit.Id);
        var level = GameState.Instance.GetUnitLevel(unit.Id);
        var purchaseCost = GameState.Instance.GetUnitPurchaseCost(unit.Id);
        var upgradeCost = GameState.Instance.GetUnitUpgradeCost(unit.Id);
        var isMaxLevel = level >= GameState.Instance.MaxUnitLevel;
        var stats = GameState.Instance.BuildPlayerUnitStats(unit);
        var effectiveDeployCooldown = GameState.Instance.ApplyPlayerDeployCooldownUpgrade(unit.DeployCooldown);
        var doctrineOptions = GameState.Instance.GetUnitDoctrineOptions(unit.Id);
        var currentDoctrineId = GameState.Instance.GetUnitDoctrineId(unit.Id);
        var doctrineUnlocked = owned && GameState.Instance.IsUnitDoctrineUnlocked(unit.Id);
        var doctrineRetrainCost = GameState.Instance.GetUnitDoctrineRetrainCost(unit.Id);

        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0f, doctrineUnlocked ? 228f : 190f),
            SelfModulate = unit.GetTint().Darkened(0.15f)
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

        var statusLine = !available
            ? $"Locked until stage {unit.UnlockStage}"
            : !owned
                ? $"For sale: {purchaseCost} gold"
                : inDeck
                    ? $"Owned  |  Lv{level}  |  In active deck"
                    : $"Owned  |  Lv{level}  |  Reserve";

        stack.AddChild(new Label
        {
            Text =
                $"{unit.DisplayName}  |  {SquadSynergyCatalog.GetTagDisplayName(unit.SquadTag)}  |  " +
                $"Deploy {unit.Cost} courage  |  {GameState.Instance.BuildUnitDoctrineInlineText(unit.Id)}"
        });

        stack.AddChild(new Label
        {
            Text = statusLine,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        stack.AddChild(new Label
        {
            Text = BuildUnitPreviewText(unit, owned, level, isMaxLevel),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        stack.AddChild(new Label
        {
            Text =
                $"Move {stats.Speed:0.#}  |  Attack CD {stats.AttackCooldown:0.##}s  |  Effective deploy {effectiveDeployCooldown:0.#}s" +
                UnitStatText.BuildInlineTraits(stats),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        stack.AddChild(row);

        var deckButton = new Button
        {
            Text = !owned
                ? "Buy First"
                : inDeck
                    ? "Remove From Deck"
                    : "Add To Deck",
            CustomMinimumSize = new Vector2(170f, 0f),
            Disabled = !owned
        };
        deckButton.Pressed += () =>
        {
            GameState.Instance.ToggleDeckUnit(unit.Id, out var message);
            GameState.Instance.SetSelectedStage(GameState.Instance.SelectedStage);
            _statusLabel.Text = $"Last report:\n{message}";
            RefreshUi();
        };
        row.AddChild(deckButton);

        var actionButton = new Button
        {
            CustomMinimumSize = new Vector2(180f, 0f)
        };

        if (!available)
        {
            actionButton.Text = $"Explore S{unit.UnlockStage}";
            actionButton.Disabled = true;
        }
        else if (!owned)
        {
            actionButton.Text = $"Buy {purchaseCost} gold";
            actionButton.Disabled = GameState.Instance.Gold < purchaseCost;
            actionButton.Pressed += () =>
            {
                GameState.Instance.TryPurchaseUnit(unit.Id, out var message);
                _statusLabel.Text = $"Last report:\n{message}";
                RefreshUi();
            };
        }
        else if (isMaxLevel)
        {
            actionButton.Text = "Max Level";
            actionButton.Disabled = true;
        }
        else
        {
            actionButton.Text = $"Upgrade {upgradeCost} gold";
            actionButton.Disabled = GameState.Instance.Gold < upgradeCost;
            actionButton.Pressed += () =>
            {
                if (GameState.Instance.TryUpgradeUnit(unit.Id, out var message))
                {
                    AudioDirector.Instance?.PlayUpgradeConfirm();
                }
                _statusLabel.Text = $"Last report:\n{message}";
                RefreshUi();
            };
        }

        row.AddChild(actionButton);

        if (doctrineUnlocked && doctrineOptions.Count > 0)
        {
            var doctrineRow = new HBoxContainer();
            doctrineRow.AddThemeConstantOverride("separation", 8);
            stack.AddChild(doctrineRow);

            foreach (var doctrine in doctrineOptions)
            {
                var isSelected = currentDoctrineId.Equals(doctrine.Id, StringComparison.OrdinalIgnoreCase);
                var doctrineButton = new Button
                {
                    Text = isSelected
                        ? $"{doctrine.Title} Selected"
                        : string.IsNullOrWhiteSpace(currentDoctrineId)
                            ? $"Choose {doctrine.Title}"
                            : $"{doctrine.Title} ({doctrineRetrainCost} gold)",
                    Disabled = isSelected || (doctrineRetrainCost > 0 && GameState.Instance.Gold < doctrineRetrainCost),
                    SizeFlagsHorizontal = SizeFlags.ExpandFill
                };
                doctrineButton.Pressed += () =>
                {
                    GameState.Instance.TrySelectUnitDoctrine(unit.Id, doctrine.Id, out var message);
                    _statusLabel.Text = $"Last report:\n{message}";
                    RefreshUi();
                };
                doctrineRow.AddChild(doctrineButton);
            }
        }

        return panel;
    }

    private Control BuildSpellPanel(SpellDefinition spell)
    {
        var owned = GameState.Instance.IsSpellOwned(spell.Id);
        var available = GameState.Instance.IsSpellAvailableForPurchase(spell.Id);
        var equipped = owned && GameState.Instance.IsSpellInActiveDeck(spell.Id);
        var purchaseCost = GameState.Instance.GetSpellPurchaseCost(spell.Id);

        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0f, 158f),
            SelfModulate = spell.GetTint().Darkened(0.12f)
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

        var spellLevelLabel = owned ? $"Lv{GameState.Instance.GetSpellLevel(spell.Id)}/{GameState.Instance.MaxSpellLevel}" : "";
        var statusLine = !available
            ? $"Locked until stage {spell.UnlockStage}"
            : !owned
                ? $"Archive price: {purchaseCost} gold"
                : equipped
                    ? $"Owned  |  Equipped  |  {spellLevelLabel}"
                    : $"Owned  |  Reserve  |  {spellLevelLabel}";

        stack.AddChild(new Label
        {
            Text = $"{spell.DisplayName}  |  Magic Card"
        });

        stack.AddChild(new Label
        {
            Text = statusLine,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        stack.AddChild(new Label
        {
            Text = SpellText.BuildInlineSummary(spell),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        stack.AddChild(new Label
        {
            Text = spell.Description,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        stack.AddChild(row);

        var deckButton = new Button
        {
            Text = !owned
                ? "Scribe First"
                : equipped
                    ? "Remove Spell"
                    : "Equip Spell",
            CustomMinimumSize = new Vector2(170f, 0f),
            Disabled = !owned
        };
        deckButton.Pressed += () =>
        {
            GameState.Instance.ToggleDeckSpell(spell.Id, out var message);
            _statusLabel.Text = $"Last report:\n{message}";
            RefreshUi();
        };
        row.AddChild(deckButton);

        var actionButton = new Button
        {
            CustomMinimumSize = new Vector2(180f, 0f)
        };

        if (!available)
        {
            actionButton.Text = $"Explore S{spell.UnlockStage}";
            actionButton.Disabled = true;
        }
        else if (!owned)
        {
            actionButton.Text = purchaseCost > 0 ? $"Scribe {purchaseCost} gold" : "Prepare Spell";
            actionButton.Disabled = GameState.Instance.Gold < purchaseCost;
            actionButton.Pressed += () =>
            {
                GameState.Instance.TryPurchaseSpell(spell.Id, out var message);
                _statusLabel.Text = $"Last report:\n{message}";
                RefreshUi();
            };
        }
        else
        {
            var spellLevel = GameState.Instance.GetSpellLevel(spell.Id);
            if (spellLevel < GameState.Instance.MaxSpellLevel)
            {
                var upgradeCost = GameState.Instance.GetSpellUpgradeCost(spell.Id);
                actionButton.Text = $"Upgrade Lv{spellLevel + 1}  {upgradeCost} gold";
                actionButton.Disabled = GameState.Instance.Gold < upgradeCost;
                actionButton.Pressed += () =>
                {
                    if (GameState.Instance.TryUpgradeSpell(spell.Id, out var message))
                    {
                        AudioDirector.Instance?.PlayUpgradeConfirm();
                    }
                    _statusLabel.Text = $"Last report:\n{message}";
                    RefreshUi();
                };
            }
            else
            {
                actionButton.Text = "Max Level";
                actionButton.Disabled = true;
            }
        }

        row.AddChild(actionButton);
        return panel;
    }

    private void RebuildBaseUpgradePanels()
    {
        foreach (var child in _baseStack.GetChildren())
        {
            child.QueueFree();
        }

        _baseStack.AddChild(new Label
        {
            Text = "War Wagon Upgrades"
        });

        foreach (var upgrade in BaseUpgradeCatalog.GetAll())
        {
            _baseStack.AddChild(BuildBaseUpgradePanel(upgrade));
        }
    }

    private Control BuildBaseUpgradePanel(BaseUpgradeDefinition upgrade)
    {
        var level = GameState.Instance.GetBaseUpgradeLevel(upgrade.Id);
        var isMaxLevel = level >= upgrade.MaxLevel;
        var cost = GameState.Instance.GetBaseUpgradeCost(upgrade.Id);

        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0f, 150f),
            SelfModulate = new Color("264653")
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
            Text = $"{upgrade.Title}  |  Lv{level}/{upgrade.MaxLevel}"
        });

        stack.AddChild(new Label
        {
            Text =
                $"{upgrade.Summary}\n" +
                $"Current: {BuildBaseUpgradeEffectText(upgrade, level)}" +
                (isMaxLevel ? "\nNext: maxed" : $"\nNext Lv{level + 1}: {BuildBaseUpgradeEffectText(upgrade, level + 1)}"),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        var button = new Button
        {
            Text = isMaxLevel ? "Maxed" : $"Upgrade {cost} gold",
            Disabled = isMaxLevel || GameState.Instance.Gold < cost,
            CustomMinimumSize = new Vector2(0f, 38f)
        };
        button.Pressed += () =>
        {
            if (GameState.Instance.TryUpgradeBase(upgrade.Id, out var message))
            {
                AudioDirector.Instance?.PlayUpgradeConfirm();
            }
            _statusLabel.Text = $"Last report:\n{message}";
            RefreshUi();
        };
        stack.AddChild(button);

        return panel;
    }
}
