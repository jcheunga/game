using System.Linq;
using Godot;

public partial class MainMenu : Control
{
    private Label _summaryLabel = null!;
    private Control _panel = null!;
    private readonly System.Collections.Generic.List<Control> _animatedElements = new();

    public override void _Ready()
    {
        BuildUi();
        PlayEntranceAnimations();
        TryShowConsentPrompt();
        TryHandleDeepLink();
    }

    private void TryHandleDeepLink()
    {
        if (DeepLinkHandler.Instance == null || !DeepLinkHandler.Instance.HasPendingChallenge())
            return;

        var code = DeepLinkHandler.Instance.ConsumePendingChallenge();
        if (string.IsNullOrWhiteSpace(code)) return;

        GameState.Instance?.TrySetSelectedAsyncChallengeCode(code, out _);
        SceneRouter.Instance?.GoToMultiplayer();
    }

    private void TryShowConsentPrompt()
    {
        if (GameState.Instance == null || GameState.Instance.HasShownConsentPrompt)
            return;

        var overlay = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0.7f)
        };
        overlay.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(overlay);

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(520f, 280f)
        };
        center.AddChild(panel);

        var padding = new MarginContainer();
        padding.AddThemeConstantOverride("margin_left", 24);
        padding.AddThemeConstantOverride("margin_right", 24);
        padding.AddThemeConstantOverride("margin_top", 24);
        padding.AddThemeConstantOverride("margin_bottom", 24);
        panel.AddChild(padding);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 14);
        padding.AddChild(stack);

        stack.AddChild(new Label
        {
            Text = "Privacy & Analytics",
            HorizontalAlignment = HorizontalAlignment.Center
        });

        stack.AddChild(new Label
        {
            Text = "Crownroad can collect anonymous gameplay data to help improve balance, difficulty, and game quality.\n\nNo personal information is collected. You can change this at any time in Settings.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        var buttonRow = new HBoxContainer();
        buttonRow.AddThemeConstantOverride("separation", 16);
        stack.AddChild(buttonRow);

        var acceptButton = new Button
        {
            Text = "Allow Analytics",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, 48f)
        };
        acceptButton.Pressed += () =>
        {
            GameState.Instance.SetAnalyticsConsent(true);
            overlay.QueueFree();
            center.QueueFree();
        };
        buttonRow.AddChild(acceptButton);

        var declineButton = new Button
        {
            Text = "No Thanks",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, 48f)
        };
        declineButton.Pressed += () =>
        {
            GameState.Instance.SetAnalyticsConsent(false);
            overlay.QueueFree();
            center.QueueFree();
        };
        buttonRow.AddChild(declineButton);
    }

    private void BuildUi()
    {
        var background = new ColorRect
        {
            Color = new Color("1d2d44")
        };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var ambientParticles = new CpuParticles2D
        {
            Amount = 14,
            Lifetime = 5f,
            Position = new Vector2(640f, 720f),
            EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
            EmissionRectExtents = new Vector2(580f, 10f),
            Direction = new Vector2(0.15f, -1f),
            Spread = 18f,
            InitialVelocityMin = 8f,
            InitialVelocityMax = 22f,
            Gravity = new Vector2(4f, -5f),
            ScaleAmountMin = 2f,
            ScaleAmountMax = 4f,
            Emitting = true
        };
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(1f, 0.85f, 0.4f, 0f));
        gradient.AddPoint(0.15f, new Color(1f, 0.8f, 0.35f, 0.3f));
        gradient.AddPoint(0.6f, new Color(1f, 0.65f, 0.25f, 0.15f));
        gradient.AddPoint(1f, new Color(0.9f, 0.5f, 0.15f, 0f));
        ambientParticles.ColorRamp = gradient;
        AddChild(ambientParticles);

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(560, 620)
        };
        _panel = panel;
        center.AddChild(panel);

        var content = new MarginContainer();
        content.AddThemeConstantOverride("margin_left", 24);
        content.AddThemeConstantOverride("margin_top", 24);
        content.AddThemeConstantOverride("margin_right", 24);
        content.AddThemeConstantOverride("margin_bottom", 24);
        panel.AddChild(content);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 14);
        content.AddChild(stack);

        var title = new Label
        {
            Text = "CROWNROAD: SIEGE OF ASH",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        stack.AddChild(title);
        _animatedElements.Add(title);

        var subtitle = new Label
        {
            Text = "Medieval fantasy siege campaign\nBuild a warband, hold the lane, break the gate.",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        stack.AddChild(subtitle);
        _animatedElements.Add(subtitle);

        _summaryLabel = new Label
        {
            Text = BuildProgressSummary(),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0f, 136f)
        };
        stack.AddChild(_summaryLabel);
        _animatedElements.Add(_summaryLabel);

        var startButton = BuildButton(GameState.Instance.HighestUnlockedStage > 1 ? "Resume Campaign" : "Start Campaign");
        startButton.Pressed += () => SceneRouter.Instance.GoToMap();
        stack.AddChild(startButton);
        _animatedElements.Add(startButton);

        var shopButton = BuildButton("Caravan Armory");
        shopButton.Pressed += () => SceneRouter.Instance.GoToShop();
        stack.AddChild(shopButton);
        _animatedElements.Add(shopButton);

        var endlessButton = BuildButton("Endless Run");
        endlessButton.Pressed += () => SceneRouter.Instance.GoToEndless();
        stack.AddChild(endlessButton);
        _animatedElements.Add(endlessButton);

        if (GameState.Instance.HighestUnlockedStage >= 10)
        {
            var bossRushButton = BuildButton("Boss Rush");
            bossRushButton.Pressed += () =>
            {
                if (GameState.Instance.PrepareBossRush(out var msg))
                {
                    SceneRouter.Instance.GoToLoadout();
                }
                else
                {
                    _summaryLabel.Text = msg;
                }
            };
            stack.AddChild(bossRushButton);
            _animatedElements.Add(bossRushButton);
        }

        var expeditionButton = BuildButton("Expeditions");
        expeditionButton.Pressed += () => SceneRouter.Instance.GoToExpeditions();
        stack.AddChild(expeditionButton);
        _animatedElements.Add(expeditionButton);

        var seasonPassButton = BuildButton("Season Pass");
        seasonPassButton.Pressed += () => SceneRouter.Instance.GoToSeasonPass();
        stack.AddChild(seasonPassButton);
        _animatedElements.Add(seasonPassButton);

        var calendarButton = BuildButton("Login Calendar");
        calendarButton.Pressed += () => SceneRouter.Instance.GoToLoginCalendar();
        stack.AddChild(calendarButton);
        _animatedElements.Add(calendarButton);

        var bountyButton = BuildButton("Bounty Board");
        bountyButton.Pressed += () => SceneRouter.Instance.GoToBounty();
        stack.AddChild(bountyButton);
        _animatedElements.Add(bountyButton);

        var towerButton = BuildButton("Challenge Tower");
        towerButton.Pressed += () => SceneRouter.Instance.GoToTower();
        stack.AddChild(towerButton);
        _animatedElements.Add(towerButton);

        var codexButton = BuildButton("Codex");
        codexButton.Pressed += () => SceneRouter.Instance.GoToCodex();
        stack.AddChild(codexButton);
        _animatedElements.Add(codexButton);

        if (GameState.Instance.HighestUnlockedStage >= ArenaCatalog.MinRequiredStage)
        {
            var arenaButton = BuildButton("PvP Arena");
            arenaButton.Pressed += () => SceneRouter.Instance.GoToArena();
            stack.AddChild(arenaButton);
            _animatedElements.Add(arenaButton);
        }

        var activeEvent = GameState.Instance.GetActiveEvent();
        if (activeEvent != null)
        {
            var eventButton = BuildButton(activeEvent.Title);
            eventButton.Pressed += () => SceneRouter.Instance.GoToEvent();
            stack.AddChild(eventButton);
            _animatedElements.Add(eventButton);
        }

        var guildButton = BuildButton("Warband");
        guildButton.Pressed += () => SceneRouter.Instance.GoToGuild();
        stack.AddChild(guildButton);
        _animatedElements.Add(guildButton);

        if (GameState.Instance.HighestUnlockedStage >= 5)
        {
            var raidButton = BuildButton("Weekly Raid");
            raidButton.Pressed += () => SceneRouter.Instance.GoToRaid();
            stack.AddChild(raidButton);
            _animatedElements.Add(raidButton);
        }

        var friendsButton = BuildButton("Friends");
        friendsButton.Pressed += () => SceneRouter.Instance.GoToFriends();
        stack.AddChild(friendsButton);
        _animatedElements.Add(friendsButton);

        var leaderboardButton = BuildButton("Leaderboards");
        leaderboardButton.Pressed += () => SceneRouter.Instance.GoToLeaderboard();
        stack.AddChild(leaderboardButton);
        _animatedElements.Add(leaderboardButton);

        var profileButton = BuildButton("Player Profile");
        profileButton.Pressed += () => SceneRouter.Instance.GoToProfile();
        stack.AddChild(profileButton);
        _animatedElements.Add(profileButton);

        var multiplayerButton = BuildButton("Multiplayer Challenge");
        multiplayerButton.Pressed += () => SceneRouter.Instance.GoToMultiplayer();
        stack.AddChild(multiplayerButton);
        _animatedElements.Add(multiplayerButton);

        var settingsButton = BuildButton("Settings");
        settingsButton.Pressed += () => SceneRouter.Instance.GoToSettings();
        stack.AddChild(settingsButton);
        _animatedElements.Add(settingsButton);

        if (GameState.Instance.CanPrestige)
        {
            var prestigeButton = BuildButton($"Prestige (New Game+)");
            prestigeButton.Pressed += () =>
            {
                if (GameState.Instance.TryPrestige(out var msg))
                {
                    _summaryLabel.Text = msg;
                }
            };
            stack.AddChild(prestigeButton);
            _animatedElements.Add(prestigeButton);
        }

        var resetButton = BuildButton("Reset Progress");
        resetButton.Pressed += () =>
        {
            GameState.Instance.ResetProgress();
            SceneRouter.Instance.GoToMap();
        };
        stack.AddChild(resetButton);
        _animatedElements.Add(resetButton);

        var quitButton = BuildButton("Quit");
        quitButton.Pressed += () => GetTree().Quit();
        stack.AddChild(quitButton);
        _animatedElements.Add(quitButton);

        var netLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center
        };
        if (NetworkStatus.Instance != null)
        {
            netLabel.Text = NetworkStatus.Instance.GetStatusLabel();
            netLabel.AddThemeColorOverride("font_color", NetworkStatus.Instance.GetStatusColor());
        }
        else
        {
            netLabel.Text = "Offline mode";
            netLabel.AddThemeColorOverride("font_color", new Color("8b949e"));
        }
        stack.AddChild(netLabel);
        _animatedElements.Add(netLabel);
    }

    private void PlayEntranceAnimations()
    {
        if (_panel != null)
        {
            _panel.Modulate = new Color(1f, 1f, 1f, 0f);
            _panel.Scale = new Vector2(0.96f, 0.96f);
            _panel.PivotOffset = _panel.CustomMinimumSize * 0.5f;
            var panelTween = CreateTween();
            panelTween.SetParallel(true);
            panelTween.TweenProperty(_panel, "modulate:a", 1f, 0.3f)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);
            panelTween.TweenProperty(_panel, "scale", Vector2.One, 0.35f)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);
        }

        for (var i = 0; i < _animatedElements.Count; i++)
        {
            var element = _animatedElements[i];
            element.Modulate = new Color(1f, 1f, 1f, 0f);
            var delay = 0.08f + (i * 0.04f);
            var tween = CreateTween();
            tween.TweenProperty(element, "modulate:a", 1f, 0.22f)
                .SetDelay(delay)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);
        }
    }

    private static Button BuildButton(string text)
    {
        return new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(0, 52)
        };
    }

    private string BuildProgressSummary()
    {
        var nextStage = GameData.GetStage(Mathf.Clamp(GameState.Instance.SelectedStage, 1, GameState.Instance.MaxStage));
        var totalStars = 0;

        foreach (var stage in GameData.Stages)
        {
            totalStars += GameState.Instance.GetStageStars(stage.StageNumber);
        }

        var ownedUnits = GameState.Instance.GetOwnedPlayerUnits().Count;
        var ownedSpells = GameState.Instance.GetOwnedPlayerSpells().Count;
        var eligibleDoctrineCount = GameState.Instance.GetEligibleUnitDoctrineCount();
        var nextDirective = GameState.Instance.GetCampaignDirective(nextStage.StageNumber);
        var hullLevel = GameState.Instance.GetBaseUpgradeLevel(BaseUpgradeCatalog.HullPlatingId);
        var pantryLevel = GameState.Instance.GetBaseUpgradeLevel(BaseUpgradeCatalog.PantryId);
        var dispatchLevel = GameState.Instance.GetBaseUpgradeLevel(BaseUpgradeCatalog.DispatchConsoleId);
        var relayLevel = GameState.Instance.GetBaseUpgradeLevel(BaseUpgradeCatalog.SignalRelayId);
        var nextExploreLine = GameState.Instance.CanExploreNextStage(out var nextExploreStage, out _)
            ? $"Next exploration: Stage {nextExploreStage.StageNumber} for {GameState.Instance.GetStageExploreFoodCost(nextExploreStage.StageNumber)} food"
            : "Route exploration complete";

        var squadSummary = GameState.Instance.GetActiveDeckUnits()
            .Select(unit =>
            {
                var doctrine = GameState.Instance.GetUnitDoctrineDefinition(unit.Id);
                return doctrine == null
                    ? $"{unit.DisplayName} Lv{GameState.Instance.GetUnitLevel(unit.Id)}"
                    : $"{unit.DisplayName} Lv{GameState.Instance.GetUnitLevel(unit.Id)} [{doctrine.Title}]";
            });
        var squadLine = string.Join(", ", squadSummary);
        if (string.IsNullOrWhiteSpace(squadLine))
        {
            squadLine = "No active squad configured.";
        }

        var spellLine = GameState.Instance.GetActiveDeckSpells().Count == 0
            ? "No active magic prepared."
            : string.Join(", ", GameState.Instance.GetActiveDeckSpells().Select(spell => spell.DisplayName));

        var selectedChallenge = GameState.Instance.GetSelectedAsyncChallenge();
        var bestChallengeScore = GameState.Instance.GetAsyncChallengeBestScore(selectedChallenge.Code);

        var prestigeText = GameState.Instance.PrestigeLevel > 0
            ? $"  |  Prestige: {GameState.Instance.GetPrestigeLabel()} (+{GameState.Instance.PrestigeLevel * 10}% gold, +{GameState.Instance.PrestigeLevel * 5}% HP)"
            : "";

        return
            "Caravan status:\n" +
            $"Unlocked stages: {GameState.Instance.HighestUnlockedStage}/{GameState.Instance.MaxStage}  |  Stars: {totalStars}{prestigeText}\n" +
            $"{CampaignPlanCatalog.BuildCampaignStatusSummary()}\n" +
            $"District rewards claimed: {GameState.Instance.ClaimedDistrictRewardCount}/{CampaignPlanCatalog.GetTargetDistrictCount()}\n" +
            $"Unit doctrines forged: {GameState.Instance.ClaimedUnitDoctrineCount}/{eligibleDoctrineCount}\n" +
            $"Heroic directives secured: {GameState.Instance.ClaimedCampaignDirectiveCount}/{GameState.Instance.MaxStage}\n" +
            $"Resources: {GameState.Instance.Gold} gold  |  {GameState.Instance.Food} food  |  Owned units: {ownedUnits}/{GameData.PlayerRosterIds.Length}  |  Owned spells: {ownedSpells}/{GameData.PlayerSpellIds.Length}\n" +
            $"War wagon upgrades: Plating {hullLevel}/{GameState.Instance.MaxBaseUpgradeLevel}  |  Stores {pantryLevel}/{GameState.Instance.MaxBaseUpgradeLevel}  |  Drum {dispatchLevel}/{GameState.Instance.MaxBaseUpgradeLevel}  |  Beacon {relayLevel}/{GameState.Instance.MaxBaseUpgradeLevel}\n" +
            $"Best endless: wave {GameState.Instance.BestEndlessWave}  |  {GameState.Instance.BestEndlessTimeSeconds:0.0}s survived\n" +
            $"Boss rush: {GameState.Instance.BestBossRushWave}/{BossRushCatalog.TotalWaves} bosses  |  {GameState.Instance.BossRushRuns} runs\n" +
            $"Selected challenge: {selectedChallenge.Code}  |  Best score {bestChallengeScore}\n" +
            $"Next deployment: {nextStage.MapName} - Stage {nextStage.StageNumber}: {nextStage.StageName}\n" +
            $"{(nextDirective == null ? "Next directive: none" : GameState.Instance.BuildCampaignDirectiveInlineText(nextStage.StageNumber))}\n" +
            $"{GameState.Instance.BuildCampaignReadinessInlineSummary(nextStage.StageNumber)}\n" +
            $"{nextExploreLine}\n" +
            $"Active squad: {squadLine}\n" +
            $"Active magic: {spellLine}\n" +
            $"Deck synergy: {GameState.Instance.BuildActiveDeckSynergyInlineSummary()}";
    }
}
