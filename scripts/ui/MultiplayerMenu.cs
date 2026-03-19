using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

public partial class MultiplayerMenu : Control
{
    private const float OnlineRoomAutoRefreshIntervalSeconds = 4f;

    private OptionButton _stageSelector = null!;
    private OptionButton _mutatorSelector = null!;
    private OptionButton _roomReportReasonSelector = null!;
    private LineEdit _codeEdit = null!;
    private Label _summaryLabel = null!;
    private Label _recordLabel = null!;
    private Label _tapeLabel = null!;
    private Label _historyLabel = null!;
    private Label _rulesLabel = null!;
    private Label _statusLabel = null!;
    private VBoxContainer _squadStack = null!;
    private Button _refreshOnlineButton = null!;
    private Button _syncButton = null!;
    private Button _startButton = null!;

    private readonly List<Control> _entrancePanels = new();

    private int _selectedStage = 1;
    private string _selectedMutatorId = AsyncChallengeCatalog.PressureSpikeId;
    private string _selectedRoomReportReasonId = OnlineRoomReportReasonCatalog.SuspiciousScoreId;
    private string _lastStatusMessage = "";
    private bool _onlineRoomAutoRefreshEnabled = true;
    private float _onlineRoomAutoRefreshTimer = 0.5f;
    private string _onlineRoomAutoRefreshStatus = "Joined room auto refresh armed.";
    private DailyLeaderboardSnapshot _cachedDailyLeaderboard = null;

    public override void _Ready()
    {
        var challenge = GameState.Instance.GetSelectedAsyncChallenge();
        _selectedStage = challenge.Stage;
        _selectedMutatorId = challenge.MutatorId;
        if (AppLifecycleService.Instance != null)
        {
            AppLifecycleService.Instance.StateChanged += OnAppLifecycleStateChanged;
        }
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

    public override void _ExitTree()
    {
        if (AppLifecycleService.Instance != null)
        {
            AppLifecycleService.Instance.StateChanged -= OnAppLifecycleStateChanged;
        }
    }

    public override void _Process(double delta)
    {
        TickOnlineRoomAutoRefresh((float)delta);
    }

    private void BuildUi()
    {
        var background = new ColorRect
        {
            Color = new Color("1f2041")
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
            Text = "Multiplayer Challenge",
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
            Text = "Challenge stage"
        });

        _stageSelector = new OptionButton
        {
            CustomMinimumSize = new Vector2(240f, 0f)
        };
        _stageSelector.ItemSelected += OnStageSelected;
        missionStack.AddChild(_stageSelector);

        for (var stageNumber = 1; stageNumber <= GameState.Instance.MaxStage; stageNumber++)
        {
            var stage = GameData.GetStage(stageNumber);
            var index = _stageSelector.ItemCount;
            _stageSelector.AddItem($"{stage.MapName} - S{stage.StageNumber} {stage.StageName}");
            _stageSelector.SetItemMetadata(index, stage.StageNumber);
        }

        missionStack.AddChild(new Label
        {
            Text = "Challenge mutator"
        });

        _mutatorSelector = new OptionButton
        {
            CustomMinimumSize = new Vector2(240f, 0f)
        };
        _mutatorSelector.ItemSelected += OnMutatorSelected;
        missionStack.AddChild(_mutatorSelector);

        foreach (var mutator in AsyncChallengeCatalog.GetAll())
        {
            var index = _mutatorSelector.ItemCount;
            _mutatorSelector.AddItem(mutator.Title);
            _mutatorSelector.SetItemMetadata(index, mutator.Id);
        }

        missionStack.AddChild(new Label
        {
            Text = "Challenge code"
        });

        var codeRow = new HBoxContainer();
        codeRow.AddThemeConstantOverride("separation", 8);
        missionStack.AddChild(codeRow);

        _codeEdit = new LineEdit
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            PlaceholderText = "CH-04-PRS-4821"
        };
        codeRow.AddChild(_codeEdit);

        var loadCodeButton = new Button
        {
            Text = "Load",
            CustomMinimumSize = new Vector2(92f, 0f)
        };
        loadCodeButton.Pressed += LoadCode;
        codeRow.AddChild(loadCodeButton);

        var actionRow = new HBoxContainer();
        actionRow.AddThemeConstantOverride("separation", 8);
        missionStack.AddChild(actionRow);

        var rollButton = new Button
        {
            Text = "Roll Code",
            CustomMinimumSize = new Vector2(0f, 42f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        rollButton.Pressed += GenerateChallengeCode;
        actionRow.AddChild(rollButton);

        var copyButton = new Button
        {
            Text = "Copy Code",
            CustomMinimumSize = new Vector2(0f, 42f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        copyButton.Pressed += () =>
        {
            DisplayServer.ClipboardSet(_codeEdit.Text);
            SetStatusMessage($"Copied {_codeEdit.Text} to the clipboard.");
        };
        actionRow.AddChild(copyButton);

        var shareButton = new Button
        {
            Text = "Share Link",
            CustomMinimumSize = new Vector2(0f, 42f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        shareButton.Pressed += () =>
        {
            var code = _codeEdit.Text;
            if (string.IsNullOrWhiteSpace(code))
            {
                SetStatusMessage("No challenge code to share.");
                return;
            }
            var url = DeepLinkHandler.BuildShareUrl(code);
            DisplayServer.ClipboardSet(url);
            SetStatusMessage($"Link copied: {url}");
        };
        actionRow.AddChild(shareButton);

        _summaryLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0f, 132f)
        };
        missionStack.AddChild(_summaryLabel);

        _recordLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        missionStack.AddChild(_recordLabel);

        _tapeLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0f, 88f)
        };
        missionStack.AddChild(_tapeLabel);

        _historyLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0f, 108f)
        };
        missionStack.AddChild(_historyLabel);

        _rulesLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        missionStack.AddChild(_rulesLabel);

        _statusLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0f, 72f)
        };
        missionStack.AddChild(_statusLabel);

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

        _syncButton = new Button
        {
            Text = "Flush Outbox",
            CustomMinimumSize = new Vector2(170f, 0f)
        };
        _syncButton.Pressed += FlushOutbox;
        bottomRow.AddChild(_syncButton);

        _refreshOnlineButton = new Button
        {
            Text = "Refresh Online",
            CustomMinimumSize = new Vector2(170f, 0f)
        };
        _refreshOnlineButton.Pressed += RefreshOnlineData;
        bottomRow.AddChild(_refreshOnlineButton);

        var quickMatchButton = new Button
        {
            Text = "Quick Match",
            CustomMinimumSize = new Vector2(170f, 0f)
        };
        quickMatchButton.Pressed += QuickMatchOnlineRoom;
        bottomRow.AddChild(quickMatchButton);

        var hostOnlineButton = new Button
        {
            Text = "Host Online Room",
            CustomMinimumSize = new Vector2(190f, 0f)
        };
        hostOnlineButton.Pressed += HostOnlineRoom;
        bottomRow.AddChild(hostOnlineButton);

        var lanButton = new Button
        {
            Text = "LAN Race",
            CustomMinimumSize = new Vector2(160f, 0f)
        };
        lanButton.Pressed += () => SceneRouter.Instance.GoToLanRace();
        bottomRow.AddChild(lanButton);

        bottomRow.AddChild(new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        });

        _startButton = new Button
        {
            Text = "Start Challenge",
            CustomMinimumSize = new Vector2(240f, 0f)
        };
        _startButton.Pressed += StartChallenge;
        bottomRow.AddChild(_startButton);
    }

    private void RefreshUi()
    {
        SyncStageSelector();
        SyncMutatorSelector();

        var challenge = GameState.Instance.GetSelectedAsyncChallenge();
        var mutator = AsyncChallengeCatalog.GetMutator(challenge.MutatorId);
        var stage = GameData.GetStage(Mathf.Clamp(challenge.Stage, 1, GameState.Instance.MaxStage));
        var previewDeck = GameState.Instance.GetSelectedAsyncChallengeDeckUnits();
        var ghostRun = GameState.Instance.GetChallengeGhostRun(challenge.Code, GameState.Instance.HasSelectedAsyncChallengeLockedDeck);
        var deckModeLabel = GameState.Instance.HasSelectedAsyncChallengeLockedDeck
            ? $"Locked featured squad: {string.Join(", ", previewDeck.Select(unit => unit.DisplayName))}"
            : $"Active squad: {string.Join(", ", previewDeck.Select(unit => unit.DisplayName))}";
        _codeEdit.Text = challenge.Code;
        _summaryLabel.Text =
            $"Challenge target: {stage.MapName} - Stage {stage.StageNumber}: {stage.StageName}\n" +
            $"{AsyncChallengeCatalog.BuildSummary(challenge)}\n\n" +
            $"{deckModeLabel}\n" +
            $"{GameState.Instance.BuildSelectedAsyncChallengeDeckSynergyInlineSummary()}\n" +
            $"{GameState.Instance.BuildSelectedAsyncChallengeSpellSummary()}\n\n" +
            $"{StageEncounterIntel.BuildCompactSummary(stage)}";
        _recordLabel.Text =
            $"Local records:\n" +
            $"Best for this code: {GameState.Instance.GetAsyncChallengeBestScore(challenge.Code)}\n" +
            $"Best tier: {AsyncChallengeCatalog.ResolveMedalLabel(challenge, GameState.Instance.GetAsyncChallengeBestScore(challenge.Code))}\n" +
            $"Challenge runs logged: {GameState.Instance.ChallengeRuns}\n" +
            $"Route lock: {(challenge.Stage <= GameState.Instance.HighestUnlockedStage ? "ready" : $"explore stage {challenge.Stage} first")}\n" +
            $"Deck mode: {(GameState.Instance.HasSelectedAsyncChallengeLockedDeck ? "featured locked squad" : "player active squad")}\n" +
            $"{GameState.Instance.BuildChallengeGhostSummary(ghostRun)}\n" +
            $"{AsyncChallengeCatalog.BuildTargetSummary(challenge, GameState.Instance.GetAsyncChallengeBestScore(challenge.Code))}\n\n" +
            $"{PlayerProfileSyncService.BuildStatusSummary()}\n\n" +
            $"{GameState.Instance.BuildChallengeSyncSummary(2)}\n\n" +
            $"{(ChallengeSyncService.Instance?.BuildStatusSummary() ?? "Sync service unavailable.")}";
        _tapeLabel.Text = GameState.Instance.BuildChallengeRunTapeSummary(GameState.Instance.GetLatestChallengeRun(challenge.Code));
        _historyLabel.Text = BuildHistoryText(challenge);
        _rulesLabel.Text =
            "Challenge rules:\n" +
            "- Runs use the exact same challenge code, stage, mutator, and seeded spawn pattern.\n" +
            "- No food is spent and no campaign stars or stage unlocks are awarded.\n" +
            "- Compare score, time, stars, and hull preservation on the same code.\n" +
            "- Daily featured boards rotate from the unlocked campaign range.\n" +
            "- Featured boards can also lock every runner to the same 3-card squad for fairer score races.\n" +
            "- Pinned codes stay saved until you clear them from the board.\n" +
            $"- Active mutator: {mutator.Title}.\n\n" +
            $"{AsyncChallengeCatalog.BuildScoringGuide(challenge)}\n\n" +
            $"{AsyncChallengeCatalog.BuildTargetSummary(challenge)}";

        RebuildSquadPanels(stage);

        var canStart = GameState.Instance.CanStartAsyncChallenge(out var readinessMessage);
        var startButtonText = canStart ? $"Start {challenge.Code}" : "Challenge Not Ready";
        if (TryBuildOnlineRoomStartState(challenge, canStart, readinessMessage, out var onlineRoomCanStart, out var onlineRoomButtonText, out var onlineRoomMessage))
        {
            canStart = onlineRoomCanStart;
            startButtonText = onlineRoomButtonText;
            readinessMessage = onlineRoomMessage;
        }

        var statusMessage = string.IsNullOrWhiteSpace(_lastStatusMessage)
            ? readinessMessage
            : _lastStatusMessage;
        _statusLabel.Text = $"Status:\n{statusMessage}";
        _syncButton.Disabled = GameState.Instance.PendingChallengeSubmissionCount <= 0 || ChallengeSyncService.Instance == null;
        _syncButton.Text = GameState.Instance.PendingChallengeSubmissionCount <= 0
            ? "Outbox Empty"
            : $"Flush Outbox ({GameState.Instance.PendingChallengeSubmissionCount})";
        _refreshOnlineButton.Disabled = ChallengeLeaderboardService.Instance == null &&
            ChallengeBoardFeedService.Instance == null &&
            !OnlineRoomDirectoryService.IsAvailable;
        _startButton.Disabled = !canStart;
        _startButton.Text = startButtonText;
    }

    private void RebuildSquadPanels(StageDefinition stage)
    {
        foreach (var child in _squadStack.GetChildren())
        {
            child.QueueFree();
        }

        _squadStack.AddChild(new Label
        {
            Text = "Online Room Directory"
        });

        _squadStack.AddChild(new Label
        {
            Text = OnlineRoomDirectoryService.BuildSnapshotSummary(),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        _squadStack.AddChild(new Label
        {
            Text = OnlineRoomCreateService.BuildStatusSummary(),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        _squadStack.AddChild(new Label
        {
            Text = OnlineRoomJoinService.BuildStatusSummary(),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        _squadStack.AddChild(new Label
        {
            Text = OnlineRoomMatchmakeService.BuildStatusSummary(),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        _squadStack.AddChild(new Label
        {
            Text = OnlineRoomSessionService.BuildStatusSummary(),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        _squadStack.AddChild(new Label
        {
            Text = OnlineRoomResultService.BuildStatusSummary(),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        _squadStack.AddChild(new Label
        {
            Text = OnlineRoomScoreboardService.BuildStatusSummary(),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        _squadStack.AddChild(new Label
        {
            Text = OnlineRoomTelemetryService.BuildStatusSummary(),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        var joinedRoomTicket = OnlineRoomJoinService.GetCachedTicket();
        if (joinedRoomTicket != null)
        {
            _squadStack.AddChild(BuildOnlineRoomActionPanel());
        }

        var onlineRooms = OnlineRoomDirectoryService.GetCachedRooms();
        if (onlineRooms.Count == 0)
        {
            _squadStack.AddChild(new Label
            {
                Text = "No online room listings cached yet.",
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            });
        }
        else
        {
            foreach (var room in onlineRooms)
            {
                _squadStack.AddChild(BuildOnlineRoomPanel(room));
            }
        }

        _squadStack.AddChild(new Label
        {
            Text = "Remote Featured Feed"
        });

        _squadStack.AddChild(new Label
        {
            Text = ChallengeBoardFeedService.Instance?.BuildSnapshotSummary() ??
                "Remote challenge feed service unavailable.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        var remoteFeed = ChallengeBoardFeedService.Instance?.GetCachedFeaturedChallenges() ?? [];
        if (remoteFeed.Count == 0)
        {
            _squadStack.AddChild(new Label
            {
                Text = "No remote featured boards cached yet.",
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            });
        }
        else
        {
            foreach (var featured in remoteFeed)
            {
                _squadStack.AddChild(BuildFeaturedChallengePanel(featured, "Load Remote"));
            }
        }

        var dailyChallenge = GameState.GetDailyChallenge();
        var dailyStage = GameData.GetStage(Mathf.Clamp(dailyChallenge.StageIndex, 1, GameState.Instance.MaxStage));
        var dailyCompleted = GameState.Instance.HasCompletedDailyChallenge();
        var dailyLabel = new Label
        {
            Text = dailyCompleted ? "Daily Challenge (Completed)" : "Daily Challenge"
        };
        dailyLabel.AddThemeColorOverride("font_color", new Color("f5c542"));
        _squadStack.AddChild(dailyLabel);

        var dailyPanel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0f, 100f),
            SelfModulate = new Color("3b2e10")
        };
        _squadStack.AddChild(dailyPanel);

        var dailyPadding = new MarginContainer();
        dailyPadding.AddThemeConstantOverride("margin_left", 14);
        dailyPadding.AddThemeConstantOverride("margin_right", 14);
        dailyPadding.AddThemeConstantOverride("margin_top", 12);
        dailyPadding.AddThemeConstantOverride("margin_bottom", 12);
        dailyPanel.AddChild(dailyPadding);

        var dailyStack = new VBoxContainer();
        dailyStack.AddThemeConstantOverride("separation", 8);
        dailyPadding.AddChild(dailyStack);

        var dailyTitleLabel = new Label
        {
            Text = $"{dailyChallenge.Date}  |  {dailyChallenge.BoardLabel}  |  {dailyStage.MapName} S{dailyStage.StageNumber}: {dailyStage.StageName}"
        };
        dailyTitleLabel.AddThemeColorOverride("font_color", new Color("f5c542"));
        dailyStack.AddChild(dailyTitleLabel);

        var dailySquadLine = dailyChallenge.LockedSquad && dailyChallenge.LockedDeckUnitIds.Length > 0
            ? $"Locked squad: {string.Join(", ", dailyChallenge.LockedDeckUnitIds.Select(unitId => GameData.GetUnit(unitId).DisplayName))}"
            : "Free squad (bring your own)";
        var dailyStatusLine = dailyCompleted
            ? "Status: Completed"
            : dailyStage.StageNumber <= GameState.Instance.HighestUnlockedStage
                ? "Status: Ready"
                : $"Status: Locked (explore stage {dailyStage.StageNumber} first)";

        dailyStack.AddChild(new Label
        {
            Text =
                $"Seed: {dailyChallenge.Seed}  |  {dailySquadLine}\n" +
                $"{dailyStatusLine}\n" +
                $"Stage {dailyStage.StageNumber} must be unlocked to participate.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        var dailyButtonRow = new HBoxContainer();
        dailyButtonRow.AddThemeConstantOverride("separation", 8);
        dailyStack.AddChild(dailyButtonRow);

        var playDailyButton = new Button
        {
            Text = dailyCompleted ? "Replay Daily Challenge" : "Play Daily Challenge",
            CustomMinimumSize = new Vector2(0f, 38f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        playDailyButton.Pressed += () => LoadDailyChallenge(dailyChallenge);
        dailyButtonRow.AddChild(playDailyButton);

        // Daily Leaderboard display
        var dailyBaseUrl = BuildDailyLeaderboardBaseUrl();
        if (!string.IsNullOrWhiteSpace(dailyBaseUrl))
        {
            dailyStack.AddChild(new HSeparator());

            var leaderboardHeader = new Label
            {
                Text = "Daily Leaderboard"
            };
            leaderboardHeader.AddThemeColorOverride("font_color", new Color("f5c542"));
            dailyStack.AddChild(leaderboardHeader);

            if (_cachedDailyLeaderboard == null || _cachedDailyLeaderboard.Entries.Count == 0)
            {
                dailyStack.AddChild(new Label
                {
                    Text = "No scores yet",
                    AutowrapMode = TextServer.AutowrapMode.WordSmart
                });
            }
            else
            {
                var sb = new StringBuilder();
                var rank = 1;
                var entriesToShow = _cachedDailyLeaderboard.Entries.Take(10);
                foreach (var entry in entriesToShow)
                {
                    var truncatedId = entry.ProfileId.Length > 12
                        ? entry.ProfileId[..12] + ".."
                        : entry.ProfileId;
                    sb.AppendLine($"#{rank}  {truncatedId}  —  {entry.Score}");
                    rank++;
                }

                dailyStack.AddChild(new Label
                {
                    Text = sb.ToString().TrimEnd(),
                    AutowrapMode = TextServer.AutowrapMode.WordSmart
                });
            }
        }

        _squadStack.AddChild(new Label
        {
            Text = $"Daily Featured Queue ({FeaturedChallengeCatalog.GetDailyRotationStamp()})"
        });

        foreach (var featured in FeaturedChallengeCatalog.GetDailyRotation(
                     GameState.Instance.HighestUnlockedStage,
                     GameState.Instance.MaxStage))
        {
            _squadStack.AddChild(BuildFeaturedChallengePanel(featured));
        }

        _squadStack.AddChild(new Label
        {
            Text = $"Pinned Codes ({GameState.Instance.GetPinnedChallengeCodes().Count})"
        });

        var pinnedCodes = GameState.Instance.GetPinnedChallengeCodes();
        if (pinnedCodes.Count == 0)
        {
            _squadStack.AddChild(new Label
            {
                Text = "Pin any challenge code to keep it on the board for rematches.",
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            });
        }
        else
        {
            foreach (var code in pinnedCodes)
            {
                _squadStack.AddChild(BuildPinnedChallengePanel(code));
            }
        }

        _squadStack.AddChild(BuildLeaderboardPanel(GameState.Instance.GetSelectedAsyncChallenge().Code));

        var previewDeck = GameState.Instance.GetSelectedAsyncChallengeDeckUnits();
        _squadStack.AddChild(new Label
        {
            Text = GameState.Instance.HasSelectedAsyncChallengeLockedDeck
                ? $"Featured Squad Lock ({previewDeck.Count}/{GameState.Instance.DeckSizeLimit})"
                : $"Active Squad ({previewDeck.Count}/{GameState.Instance.DeckSizeLimit})"
        });

        _squadStack.AddChild(new Label
        {
            Text = GameState.Instance.BuildSelectedAsyncChallengeDeckSynergySummary(),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        _squadStack.AddChild(new Label
        {
            Text = GameState.Instance.HasSelectedAsyncChallengeLockedDeck
                ? "Spell Layer"
                : $"Active Magic ({GameState.Instance.GetSelectedAsyncChallengeDeckSpells().Count}/{GameState.Instance.SpellDeckSizeLimit})"
        });

        _squadStack.AddChild(new Label
        {
            Text = GameState.Instance.BuildSelectedAsyncChallengeSpellSummary(),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        foreach (var definition in previewDeck)
        {
            _squadStack.AddChild(BuildUnitPanel(definition, previewDeck));
        }

        _squadStack.AddChild(new Label
        {
            Text = StageEncounterIntel.BuildEncounterIntel(stage),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });
    }

    private Control BuildLeaderboardPanel(string code)
    {
        var panel = new PanelContainer();
        var padding = new MarginContainer();
        padding.AddThemeConstantOverride("margin_left", 12);
        padding.AddThemeConstantOverride("margin_right", 12);
        padding.AddThemeConstantOverride("margin_top", 12);
        padding.AddThemeConstantOverride("margin_bottom", 12);
        panel.AddChild(padding);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 8);
        padding.AddChild(stack);

        stack.AddChild(new Label
        {
            Text = "Remote Board"
        });

        stack.AddChild(new Label
        {
            Text = ChallengeLeaderboardService.Instance?.BuildSnapshotSummary(code, 5) ??
                "Remote leaderboard service unavailable.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        return panel;
    }

    private Control BuildOnlineRoomPanel(OnlineRoomDirectoryEntry room)
    {
        AsyncChallengeCatalog.TryParse(room.BoardCode, out var challenge, out _);
        var stage = challenge != null
            ? GameData.GetStage(Mathf.Clamp(challenge.Stage, 1, GameState.Instance.MaxStage))
            : null;
        var mutator = challenge != null
            ? AsyncChallengeCatalog.GetMutator(challenge.MutatorId)
            : null;
        var deckSummary = room.UsesLockedDeck
            ? $"Locked squad: {string.Join(", ", room.LockedDeckUnitIds.Select(unitId => GameData.GetUnit(unitId).DisplayName))}"
            : "Deck mode: player squads";

        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0f, 156f)
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
            Text = $"{room.Title}  |  {room.CurrentPlayers}/{room.MaxPlayers} runners  |  {room.Status}"
        });

        stack.AddChild(new Label
        {
            Text =
                $"{room.BoardCode}  |  Host {room.HostCallsign}  |  Region {room.Region}\n" +
                $"{room.BoardTitle}\n" +
                $"{room.Summary}\n" +
                $"{deckSummary}\n" +
                $"Spectators: {room.SpectatorCount}" +
                (stage == null || mutator == null
                    ? ""
                    : $"\nStage: {stage.MapName} S{stage.StageNumber}  |  Mutator: {mutator.Title}"),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        stack.AddChild(row);

        var loadButton = new Button
        {
            Text = "Preview Board",
            CustomMinimumSize = new Vector2(0f, 38f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        loadButton.Pressed += () => LoadOnlineRoomBoard(room);
        row.AddChild(loadButton);

        var joinButton = new Button
        {
            Text = OnlineRoomCreateService.GetHostedRoom()?.RoomId == room.RoomId ? "Host Seat Active" : "Request Join",
            CustomMinimumSize = new Vector2(0f, 38f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Disabled = OnlineRoomCreateService.GetHostedRoom()?.RoomId == room.RoomId
        };
        joinButton.Pressed += () => RequestOnlineRoomJoin(room);
        row.AddChild(joinButton);

        var copyButton = new Button
        {
            Text = "Copy Room ID",
            CustomMinimumSize = new Vector2(0f, 38f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        copyButton.Pressed += () =>
        {
            DisplayServer.ClipboardSet(string.IsNullOrWhiteSpace(room.RoomId) ? room.BoardCode : room.RoomId);
            SetStatusMessage($"Copied {(string.IsNullOrWhiteSpace(room.RoomId) ? room.BoardCode : room.RoomId)} to the clipboard.");
            RefreshUi();
        };
        row.AddChild(copyButton);

        return panel;
    }

    private Control BuildOnlineRoomActionPanel()
    {
        var panel = new PanelContainer();

        var padding = new MarginContainer();
        padding.AddThemeConstantOverride("margin_left", 12);
        padding.AddThemeConstantOverride("margin_right", 12);
        padding.AddThemeConstantOverride("margin_top", 12);
        padding.AddThemeConstantOverride("margin_bottom", 12);
        panel.AddChild(padding);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 8);
        padding.AddChild(stack);

        stack.AddChild(new Label
        {
            Text = "Online Room Controls"
        });

        stack.AddChild(new Label
        {
            Text = OnlineRoomActionService.BuildStatusSummary(),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        stack.AddChild(new Label
        {
            Text = OnlineRoomSeatLeaseService.BuildStatusSummary(),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        stack.AddChild(new Label
        {
            Text = OnlineRoomReportService.BuildStatusSummary(),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        stack.AddChild(new Label
        {
            Text = OnlineRoomRecoveryService.BuildStatusSummary(),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        stack.AddChild(new Label
        {
            Text = BuildOnlineRoomAutoRefreshSummary(),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        stack.AddChild(row);

        var readyButton = new Button
        {
            Text = OnlineRoomActionService.BuildToggleReadyLabel(),
            CustomMinimumSize = new Vector2(0f, 38f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Disabled = !OnlineRoomActionService.CanToggleReady()
        };
        readyButton.Pressed += ToggleOnlineRoomReady;
        row.AddChild(readyButton);

        var refreshButton = new Button
        {
            Text = "Refresh Joined Room",
            CustomMinimumSize = new Vector2(0f, 38f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        refreshButton.Pressed += RefreshJoinedOnlineRoom;
        row.AddChild(refreshButton);

        var autoRefreshButton = new Button
        {
            Text = _onlineRoomAutoRefreshEnabled ? "Pause Auto Refresh" : "Resume Auto Refresh",
            CustomMinimumSize = new Vector2(0f, 38f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        autoRefreshButton.Pressed += ToggleOnlineRoomAutoRefresh;
        row.AddChild(autoRefreshButton);

        var renewSeatButton = new Button
        {
            Text = BuildRenewSeatButtonLabel(),
            CustomMinimumSize = new Vector2(0f, 38f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        renewSeatButton.Pressed += RenewOnlineRoomSeat;
        row.AddChild(renewSeatButton);

        var launchButton = new Button
        {
            Text = OnlineRoomActionService.BuildLaunchRoundLabel(),
            CustomMinimumSize = new Vector2(0f, 38f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Disabled = !OnlineRoomActionService.CanLaunchRound()
        };
        launchButton.Pressed += LaunchOnlineRoomRound;
        row.AddChild(launchButton);

        var scoreRow = new HBoxContainer();
        scoreRow.AddThemeConstantOverride("separation", 8);
        stack.AddChild(scoreRow);

        var refreshScoreboardButton = new Button
        {
            Text = "Refresh Room Scoreboard",
            CustomMinimumSize = new Vector2(0f, 38f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        refreshScoreboardButton.Pressed += RefreshOnlineRoomScoreboard;
        scoreRow.AddChild(refreshScoreboardButton);

        var resetRoundButton = new Button
        {
            Text = OnlineRoomActionService.BuildResetRoundLabel(),
            CustomMinimumSize = new Vector2(0f, 38f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Disabled = !OnlineRoomActionService.CanResetRound()
        };
        resetRoundButton.Pressed += ResetOnlineRoomRound;
        scoreRow.AddChild(resetRoundButton);

        var leaveRoomButton = new Button
        {
            Text = OnlineRoomActionService.BuildLeaveRoomLabel(),
            CustomMinimumSize = new Vector2(0f, 38f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        leaveRoomButton.Pressed += LeaveOnlineRoom;
        scoreRow.AddChild(leaveRoomButton);

        var reportRow = new HBoxContainer();
        reportRow.AddThemeConstantOverride("separation", 8);
        stack.AddChild(reportRow);

        _roomReportReasonSelector = new OptionButton
        {
            CustomMinimumSize = new Vector2(0f, 38f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        foreach (var reason in OnlineRoomReportReasonCatalog.GetAll())
        {
            var index = _roomReportReasonSelector.ItemCount;
            _roomReportReasonSelector.AddItem(reason.Title);
            _roomReportReasonSelector.SetItemMetadata(index, reason.Id);
            if (reason.Id == _selectedRoomReportReasonId)
            {
                _roomReportReasonSelector.Select(index);
            }
        }
        _roomReportReasonSelector.ItemSelected += OnRoomReportReasonSelected;
        reportRow.AddChild(_roomReportReasonSelector);

        var reportButton = new Button
        {
            Text = "Submit Room Report",
            CustomMinimumSize = new Vector2(0f, 38f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Disabled = !OnlineRoomReportService.CanSubmitJoinedRoomReport()
        };
        reportButton.Pressed += SubmitOnlineRoomReport;
        reportRow.AddChild(reportButton);

        return panel;
    }

    private Control BuildUnitPanel(UnitDefinition definition, IReadOnlyList<UnitDefinition> deckUnits)
    {
        var stats = GameState.Instance.BuildPlayerUnitStatsForDeck(definition, deckUnits);
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

    private Control BuildFeaturedChallengePanel(FeaturedChallengeDefinition featured, string loadButtonText = "Load Featured")
    {
        var challenge = featured.Challenge;
        var stage = GameData.GetStage(Mathf.Clamp(challenge.Stage, 1, GameState.Instance.MaxStage));
        var mutator = AsyncChallengeCatalog.GetMutator(challenge.MutatorId);
        var best = GameState.Instance.GetAsyncChallengeBestScore(challenge.Code);
        var isPinned = GameState.Instance.IsChallengeCodePinned(challenge.Code);
        var lockedLabel = challenge.Stage <= GameState.Instance.HighestUnlockedStage
            ? "Ready"
            : $"Locked until stage {challenge.Stage}";
        var deckSummary = string.Join(
            ", ",
            featured.LockedDeckUnitIds.Select(unitId => GameData.GetUnit(unitId).DisplayName));

        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0f, 148f)
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
            Text = $"{featured.Title}  |  {stage.MapName} S{stage.StageNumber}"
        });

        stack.AddChild(new Label
        {
            Text =
                $"{challenge.Code}  |  {mutator.Title}  |  Best {best} ({AsyncChallengeCatalog.ResolveMedalLabel(challenge, best)})\n" +
                $"{featured.Summary}\n" +
                $"Locked squad: {deckSummary}\n" +
                $"Status: {lockedLabel}\n" +
                $"{AsyncChallengeCatalog.BuildTargetSummary(challenge)}",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        stack.AddChild(row);

        var loadButton = new Button
        {
            Text = loadButtonText,
            CustomMinimumSize = new Vector2(0f, 38f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        loadButton.Pressed += () => LoadFeaturedChallenge(featured);
        row.AddChild(loadButton);

        var pinButton = new Button
        {
            Text = isPinned ? "Unpin" : "Pin",
            CustomMinimumSize = new Vector2(0f, 38f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        pinButton.Pressed += () => TogglePinnedChallenge(challenge.Code);
        row.AddChild(pinButton);

        return panel;
    }

    private Control BuildPinnedChallengePanel(string code)
    {
        if (!AsyncChallengeCatalog.TryParse(code, out var challenge, out _))
        {
            return new Label
            {
                Text = code,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
        }

        var stage = GameData.GetStage(Mathf.Clamp(challenge.Stage, 1, GameState.Instance.MaxStage));
        var mutator = AsyncChallengeCatalog.GetMutator(challenge.MutatorId);
        var best = GameState.Instance.GetAsyncChallengeBestScore(challenge.Code);
        var recent = GameState.Instance.GetRecentChallengeHistory(1, challenge.Code);
        var recentLine = recent.Count > 0
            ? $"Latest: {FormatHistoryEntry(recent[0], false)}"
            : "Latest: no local attempts yet.";

        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0f, 136f)
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
            Text = $"{challenge.Code}  |  {stage.MapName} S{stage.StageNumber}  |  {mutator.Title}"
        });

        stack.AddChild(new Label
        {
            Text =
                $"Pinned rematch board  |  Best {best} ({AsyncChallengeCatalog.ResolveMedalLabel(challenge, best)})\n" +
                $"{recentLine}\n" +
                $"{AsyncChallengeCatalog.BuildTargetSummary(challenge)}",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        stack.AddChild(row);

        var loadButton = new Button
        {
            Text = "Load Pinned",
            CustomMinimumSize = new Vector2(0f, 38f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        loadButton.Pressed += () => LoadChallengeCode(challenge.Code, $"Loaded pinned code {challenge.Code}.");
        row.AddChild(loadButton);

        var removeButton = new Button
        {
            Text = "Remove Pin",
            CustomMinimumSize = new Vector2(0f, 38f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        removeButton.Pressed += () => TogglePinnedChallenge(challenge.Code);
        row.AddChild(removeButton);

        return panel;
    }

    private void OnStageSelected(long index)
    {
        if (index < 0 || index >= _stageSelector.ItemCount)
        {
            return;
        }

        _selectedStage = (int)_stageSelector.GetItemMetadata((int)index).AsInt32();
        GenerateChallengeCode();
    }

    private void OnMutatorSelected(long index)
    {
        if (index < 0 || index >= _mutatorSelector.ItemCount)
        {
            return;
        }

        _selectedMutatorId = _mutatorSelector.GetItemMetadata((int)index).AsString();
        GenerateChallengeCode();
    }

    private void GenerateChallengeCode()
    {
        GameState.Instance.GenerateAsyncChallenge(_selectedStage, _selectedMutatorId);
        SetStatusMessage($"Rolled {GameState.Instance.SelectedAsyncChallengeCode}.");
        RefreshUi();
    }

    private void LoadCode()
    {
        if (!GameState.Instance.TrySetSelectedAsyncChallengeCode(_codeEdit.Text, out var message))
        {
            SetStatusMessage(message);
            return;
        }

        var challenge = GameState.Instance.GetSelectedAsyncChallenge();
        _selectedStage = challenge.Stage;
        _selectedMutatorId = challenge.MutatorId;
        SetStatusMessage(message);
        RefreshUi();
    }

    private void LoadChallengeCode(string code, string successPrefix)
    {
        if (!GameState.Instance.TrySetSelectedAsyncChallengeCode(code, out var message))
        {
            SetStatusMessage(message);
            return;
        }

        var challenge = GameState.Instance.GetSelectedAsyncChallenge();
        _selectedStage = challenge.Stage;
        _selectedMutatorId = challenge.MutatorId;
        SetStatusMessage(successPrefix);
        RefreshUi();
    }

    private void LoadFeaturedChallenge(FeaturedChallengeDefinition featured)
    {
        GameState.Instance.SetSelectedFeaturedChallenge(featured);
        var challenge = GameState.Instance.GetSelectedAsyncChallenge();
        _selectedStage = challenge.Stage;
        _selectedMutatorId = challenge.MutatorId;
        SetStatusMessage($"Loaded featured board {featured.Title} with its locked squad.");
        RefreshUi();
    }

    private void LoadDailyChallenge(DailyChallenge daily)
    {
        var clampedStage = Mathf.Clamp(daily.StageIndex, 1, GameState.Instance.MaxStage);
        var asyncChallenge = AsyncChallengeCatalog.Create(
            clampedStage,
            AsyncChallengeCatalog.PressureSpikeId,
            daily.Seed);

        if (daily.LockedSquad && daily.LockedDeckUnitIds.Length > 0)
        {
            var featured = new FeaturedChallengeDefinition(
                $"daily_{daily.Date}",
                $"Daily {daily.BoardLabel}",
                $"Daily challenge for {daily.Date}.",
                asyncChallenge,
                daily.LockedDeckUnitIds);
            GameState.Instance.SetSelectedFeaturedChallenge(featured);
        }
        else
        {
            if (!GameState.Instance.TrySetSelectedAsyncChallengeCode(asyncChallenge.Code, out var message))
            {
                SetStatusMessage(message);
                return;
            }
        }

        var challenge = GameState.Instance.GetSelectedAsyncChallenge();
        _selectedStage = challenge.Stage;
        _selectedMutatorId = challenge.MutatorId;
        SetStatusMessage($"Loaded daily challenge {daily.BoardLabel} ({daily.Date}).");
        RefreshDailyLeaderboard();
        RefreshUi();
    }

    private void RefreshDailyLeaderboard()
    {
        var baseUrl = BuildDailyLeaderboardBaseUrl();
        if (string.IsNullOrWhiteSpace(baseUrl))
            return;

        try
        {
            var provider = new HttpApiDailyLeaderboardProvider(baseUrl);
            var todayDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
            _cachedDailyLeaderboard = provider.FetchLeaderboard(todayDate);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Daily leaderboard fetch failed: {ex.Message}");
        }
    }

    private static string BuildDailyLeaderboardBaseUrl()
    {
        var syncEndpoint = GameState.Instance?.ChallengeSyncEndpoint ?? "";
        if (string.IsNullOrWhiteSpace(syncEndpoint))
            return "";

        var normalized = syncEndpoint.Trim();
        if (normalized.EndsWith("/challenge-sync", StringComparison.OrdinalIgnoreCase))
            return normalized[..^"/challenge-sync".Length];

        return normalized.TrimEnd('/');
    }

    private void LoadOnlineRoomBoard(OnlineRoomDirectoryEntry room)
    {
        if (!AsyncChallengeCatalog.TryParse(room.BoardCode, out var challenge, out var message))
        {
            SetStatusMessage(message);
            return;
        }

        if (room.UsesLockedDeck)
        {
            GameState.Instance.SetSelectedFeaturedChallenge(new FeaturedChallengeDefinition(
                string.IsNullOrWhiteSpace(room.RoomId) ? room.BoardCode : room.RoomId,
                string.IsNullOrWhiteSpace(room.Title) ? "Online Room" : room.Title,
                string.IsNullOrWhiteSpace(room.Summary) ? "Remote room board." : room.Summary,
                challenge,
                room.LockedDeckUnitIds ?? []));
            var selected = GameState.Instance.GetSelectedAsyncChallenge();
            _selectedStage = selected.Stage;
            _selectedMutatorId = selected.MutatorId;
        }
        else
        {
            if (!GameState.Instance.TrySetSelectedAsyncChallengeCode(room.BoardCode, out message))
            {
                SetStatusMessage(message);
                return;
            }

            _selectedStage = challenge.Stage;
            _selectedMutatorId = challenge.MutatorId;
        }

        SetStatusMessage(
            $"Previewed room board {room.BoardCode} from {room.Title}. Use `Request Join` if you want to negotiate backend room access; this action only preloads the board locally.");
        RefreshUi();
    }

    private void RequestOnlineRoomJoin(OnlineRoomDirectoryEntry room)
    {
        if (!OnlineRoomJoinService.RequestJoin(room, out var message))
        {
            SetStatusMessage(message);
            RefreshUi();
            return;
        }

        var statusParts = new List<string> { message };
        if (OnlineRoomSessionService.RefreshJoinedRoom(out var sessionMessage))
        {
            statusParts.Add(sessionMessage);
        }
        else
        {
            statusParts.Add(sessionMessage);
        }
        OnlineRoomScoreboardService.RefreshJoinedRoomScoreboard(5, out var scoreboardMessage);
        statusParts.Add(scoreboardMessage);

        SetStatusMessage(string.Join("\n", statusParts));
        RefreshUi();
    }

    private void HostOnlineRoom()
    {
        var statusParts = new List<string>();
        if (!OnlineRoomCreateService.HostSelectedChallenge(out var hostMessage))
        {
            SetStatusMessage(hostMessage);
            RefreshUi();
            return;
        }

        statusParts.Add(hostMessage);
        OnlineRoomSessionService.RefreshJoinedRoom(out var sessionMessage);
        statusParts.Add(sessionMessage);
        OnlineRoomScoreboardService.RefreshJoinedRoomScoreboard(5, out var scoreboardMessage);
        statusParts.Add(scoreboardMessage);
        SetStatusMessage(string.Join("\n", statusParts));
        RefreshUi();
    }

    private void QuickMatchOnlineRoom()
    {
        if (!OnlineRoomMatchmakeService.QuickMatchSelectedChallenge(out var message))
        {
            SetStatusMessage(message);
            RefreshUi();
            return;
        }

        SetStatusMessage(message);
        RefreshUi();
    }

    private void RenewOnlineRoomSeat()
    {
        var ticket = OnlineRoomJoinService.GetCachedTicket();
        if (ticket == null)
        {
            QuickMatchOnlineRoom();
            return;
        }

        if (OnlineRoomJoinService.HasActiveTicket())
        {
            var statusParts = new List<string>();
            if (!OnlineRoomSeatLeaseService.RenewSeat(out var leaseMessage))
            {
                SetStatusMessage(leaseMessage);
                RefreshUi();
                return;
            }

            statusParts.Add(leaseMessage);
            OnlineRoomSessionService.RefreshJoinedRoom(out var sessionMessage);
            statusParts.Add(sessionMessage);
            OnlineRoomScoreboardService.RefreshJoinedRoomScoreboard(5, out var scoreboardMessage);
            statusParts.Add(scoreboardMessage);
            _onlineRoomAutoRefreshStatus = $"Seat renewal at {DateTimeOffset.Now:HH:mm:ss}\n{string.Join("\n", statusParts)}";
            _onlineRoomAutoRefreshTimer = OnlineRoomAutoRefreshIntervalSeconds;
            SetStatusMessage(string.Join("\n", statusParts));
            RefreshUi();
            return;
        }

        if (!OnlineRoomRecoveryService.TryRecoverExpiredSeat(out var recoveryMessage))
        {
            SetStatusMessage(recoveryMessage);
            RefreshUi();
            return;
        }

        _onlineRoomAutoRefreshStatus = $"Seat recovery at {DateTimeOffset.Now:HH:mm:ss}\n{recoveryMessage}";
        _onlineRoomAutoRefreshTimer = OnlineRoomAutoRefreshIntervalSeconds;
        SetStatusMessage(recoveryMessage);
        RefreshUi();
    }

    private void ToggleOnlineRoomReady()
    {
        var statusParts = new List<string>();
        if (!OnlineRoomActionService.ToggleReady(out var actionMessage))
        {
            SetStatusMessage(actionMessage);
            RefreshUi();
            return;
        }

        statusParts.Add(actionMessage);
        if (OnlineRoomSessionService.RefreshJoinedRoom(out var sessionMessage))
        {
            statusParts.Add(sessionMessage);
        }
        else
        {
            statusParts.Add(sessionMessage);
        }
        OnlineRoomScoreboardService.RefreshJoinedRoomScoreboard(5, out var scoreboardMessage);
        statusParts.Add(scoreboardMessage);

        SetStatusMessage(string.Join("\n", statusParts));
        RefreshUi();
    }

    private void LaunchOnlineRoomRound()
    {
        var statusParts = new List<string>();
        if (!OnlineRoomActionService.LaunchRound(out var actionMessage))
        {
            SetStatusMessage(actionMessage);
            RefreshUi();
            return;
        }

        statusParts.Add(actionMessage);
        OnlineRoomSessionService.RefreshJoinedRoom(out var sessionMessage);
        statusParts.Add(sessionMessage);
        OnlineRoomScoreboardService.RefreshJoinedRoomScoreboard(5, out var scoreboardMessage);
        statusParts.Add(scoreboardMessage);
        SetStatusMessage(string.Join("\n", statusParts));
        RefreshUi();
    }

    private void RefreshJoinedOnlineRoom()
    {
        var statusParts = new List<string>();
        if (OnlineRoomSeatLeaseService.TryAutoRenewIfNeeded(out var leaseMessage))
        {
            statusParts.Add(leaseMessage);
        }
        OnlineRoomSessionService.RefreshJoinedRoom(out var message);
        statusParts.Add(message);
        OnlineRoomScoreboardService.RefreshJoinedRoomScoreboard(5, out var scoreboardMessage);
        statusParts.Add(scoreboardMessage);
        _onlineRoomAutoRefreshStatus = $"Manual refresh at {DateTimeOffset.Now:HH:mm:ss}\n{string.Join("\n", statusParts)}";
        _onlineRoomAutoRefreshTimer = OnlineRoomAutoRefreshIntervalSeconds;
        SetStatusMessage(string.Join("\n", statusParts));
        RefreshUi();
    }

    private void RefreshOnlineRoomScoreboard()
    {
        OnlineRoomScoreboardService.RefreshJoinedRoomScoreboard(5, out var message);
        _onlineRoomAutoRefreshStatus = $"Scoreboard refresh at {DateTimeOffset.Now:HH:mm:ss}\n{message}";
        SetStatusMessage(message);
        RefreshUi();
    }

    private void SubmitOnlineRoomReport()
    {
        if (!OnlineRoomReportService.SubmitJoinedRoomReport(_selectedRoomReportReasonId, out var message))
        {
            SetStatusMessage(message);
            RefreshUi();
            return;
        }

        SetStatusMessage(message);
        RefreshUi();
    }

    private void ResetOnlineRoomRound()
    {
        var statusParts = new List<string>();
        if (!OnlineRoomActionService.ResetRound(out var actionMessage))
        {
            SetStatusMessage(actionMessage);
            RefreshUi();
            return;
        }

        statusParts.Add(actionMessage);
        OnlineRoomSessionService.RefreshJoinedRoom(out var sessionMessage);
        statusParts.Add(sessionMessage);
        OnlineRoomScoreboardService.RefreshJoinedRoomScoreboard(5, out var scoreboardMessage);
        statusParts.Add(scoreboardMessage);
        SetStatusMessage(string.Join("\n", statusParts));
        RefreshUi();
    }

    private void LeaveOnlineRoom()
    {
        if (!OnlineRoomActionService.LeaveRoom(out var message))
        {
            SetStatusMessage(message);
            RefreshUi();
            return;
        }

        _onlineRoomAutoRefreshStatus = "Joined room auto refresh idle. No active room ticket.";
        _onlineRoomAutoRefreshTimer = 0.5f;
        SetStatusMessage(message);
        RefreshUi();
    }

    private void ToggleOnlineRoomAutoRefresh()
    {
        _onlineRoomAutoRefreshEnabled = !_onlineRoomAutoRefreshEnabled;
        _onlineRoomAutoRefreshTimer = 0.5f;
        _onlineRoomAutoRefreshStatus = _onlineRoomAutoRefreshEnabled
            ? "Joined room auto refresh resumed."
            : "Joined room auto refresh paused.";
        RefreshUi();
    }

    private void OnRoomReportReasonSelected(long index)
    {
        if (_roomReportReasonSelector == null || index < 0 || index >= _roomReportReasonSelector.ItemCount)
        {
            return;
        }

        _selectedRoomReportReasonId = OnlineRoomReportReasonCatalog.NormalizeId(
            _roomReportReasonSelector.GetItemMetadata((int)index).AsString());
        RefreshUi();
    }

    private void TogglePinnedChallenge(string code)
    {
        if (!GameState.Instance.TogglePinnedChallengeCode(code, out _, out var message))
        {
            SetStatusMessage(message);
            return;
        }

        SetStatusMessage(message);
        RefreshUi();
    }

    private void StartChallenge()
    {
        var selectedChallenge = GameState.Instance.GetSelectedAsyncChallenge();
        if (TryBuildOnlineRoomStartState(
                selectedChallenge,
                GameState.Instance.CanStartAsyncChallenge(out var baseMessage),
                baseMessage,
                out var canEnterOnlineRoomRace,
                out _,
                out var onlineRoomMessage) &&
            !canEnterOnlineRoomRace)
        {
            SetStatusMessage(onlineRoomMessage);
            RefreshUi();
            return;
        }

        if (!GameState.Instance.PrepareAsyncChallenge(_codeEdit.Text, out var message))
        {
            SetStatusMessage(message);
            return;
        }

        SceneRouter.Instance.GoToBattle();
    }

    private bool TryBuildOnlineRoomStartState(
        AsyncChallengeDefinition challenge,
        bool baseCanStart,
        string baseReadinessMessage,
        out bool canStart,
        out string buttonText,
        out string message)
    {
        canStart = baseCanStart;
        buttonText = baseCanStart ? $"Start {challenge.Code}" : "Challenge Not Ready";
        message = baseReadinessMessage;

        var ticket = OnlineRoomJoinService.GetCachedTicket();
        if (ticket == null || challenge == null)
        {
            return false;
        }

        if (!AsyncChallengeCatalog.NormalizeCode(ticket.BoardCode)
                .Equals(AsyncChallengeCatalog.NormalizeCode(challenge.Code), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (OnlineRoomJoinService.IsTicketExpired(ticket))
        {
            canStart = false;
            buttonText = "Recover Room Seat";
            message =
                $"Joined room seat for {ticket.RoomTitle} has expired.\n" +
                "Use `Recover Seat` or `Quick Match` before trying to deploy into this room race.";
            return true;
        }

        if (string.Equals(ticket.Status, "spectate", StringComparison.OrdinalIgnoreCase))
        {
            canStart = false;
            buttonText = "Spectating Room";
            message =
                $"This room seat for {ticket.RoomTitle} is spectate-only.\n" +
                "Leave the room or negotiate a runner seat before deploying on this board.";
            return true;
        }

        if (string.Equals(ticket.Status, "waitlist", StringComparison.OrdinalIgnoreCase))
        {
            canStart = false;
            buttonText = "Waitlisted";
            message =
                $"This room seat for {ticket.RoomTitle} is still waitlisted.\n" +
                "Refresh the joined room or renew the seat once a runner slot opens.";
            return true;
        }

        if (!baseCanStart)
        {
            canStart = false;
            buttonText = "Room Board Not Ready";
            message =
                $"{baseReadinessMessage}\n" +
                $"Room seat is armed for {ticket.RoomTitle}, but the selected board is not startable yet.";
            return true;
        }

        var roomSnapshot = OnlineRoomSessionService.GetCachedSnapshot()?.RoomSnapshot;
        if (roomSnapshot == null || !roomSnapshot.HasRoom)
        {
            canStart = false;
            buttonText = "Refresh Room State";
            message =
                $"Room seat is armed for {ticket.RoomTitle}, but no live room snapshot is cached yet.\n" +
                "Use `Refresh Joined Room` or `Refresh Online` before entering the room race.";
            return true;
        }

        if (roomSnapshot.RoundComplete)
        {
            canStart = false;
            buttonText = "Room Round Complete";
            message =
                $"The current room round for {ticket.RoomTitle} is already complete.\n" +
                "Wait for the host to reset the round, or leave and join a fresh room board.";
            return true;
        }

        if (!roomSnapshot.RoundLocked)
        {
            canStart = false;
            buttonText = OnlineRoomActionService.CanLaunchRound() ? "Launch Room Round" : "Waiting For Room Launch";
            message = OnlineRoomActionService.CanLaunchRound()
                ? $"Host seat is armed for {ticket.RoomTitle}. Launch the room round first, then deploy into the shared race."
                : $"Joined room {ticket.RoomTitle} is armed for {challenge.Code}, but the host has not launched the round yet.\nReady up in the room controls and wait for the race countdown.";
            return true;
        }

        canStart = true;
        buttonText = roomSnapshot.RaceCountdownActive ? "Enter Online Room Race" : "Deploy Into Online Room";
        message = roomSnapshot.RaceCountdownActive
            ? $"Room launch countdown is live for {ticket.RoomTitle}: {roomSnapshot.RaceCountdownRemainingSeconds:0.0}s remaining.\nDeploy now to enter the shared room race on {challenge.Code}."
            : $"Room round is live for {ticket.RoomTitle}.\nDeploy now to post your result into the active internet-room scoreboard for {challenge.Code}.";
        return true;
    }

    private void FlushOutbox()
    {
        if (ChallengeSyncService.Instance == null)
        {
            SetStatusMessage("Sync service is unavailable.");
            RefreshUi();
            return;
        }

        ChallengeSyncService.Instance.RefreshStatusFromState();
        ChallengeSyncService.Instance.FlushPendingSubmissions(out var message);
        SetStatusMessage(message);
        RefreshUi();
    }

    private void RefreshOnlineData()
    {
        var challenge = GameState.Instance.GetSelectedAsyncChallenge();
        var statusParts = new System.Collections.Generic.List<string>();

        if (PlayerProfileSyncService.RefreshProfile(out var profileMessage))
        {
            statusParts.Add(profileMessage);
        }
        else
        {
            statusParts.Add(profileMessage);
        }

        OnlineRoomDirectoryService.RefreshRooms(
            GameState.Instance.HighestUnlockedStage,
            GameState.Instance.MaxStage,
            4,
            out var roomMessage);
        statusParts.Add(roomMessage);

        if (OnlineRoomJoinService.GetCachedTicket() != null)
        {
            if (!OnlineRoomJoinService.HasActiveTicket())
            {
                if (OnlineRoomRecoveryService.TryRecoverExpiredSeat(out var recoveryMessage))
                {
                    statusParts.Add(recoveryMessage);
                }
                else
                {
                    statusParts.Add(recoveryMessage);
                }
            }
            else
            {
                if (OnlineRoomSeatLeaseService.TryAutoRenewIfNeeded(out var leaseMessage))
                {
                    statusParts.Add(leaseMessage);
                }
                OnlineRoomSessionService.RefreshJoinedRoom(out var sessionStatus);
                statusParts.Add(sessionStatus);
                OnlineRoomScoreboardService.RefreshJoinedRoomScoreboard(5, out var scoreboardStatus);
                statusParts.Add(scoreboardStatus);
            }
        }

        if (ChallengeBoardFeedService.Instance != null)
        {
            ChallengeBoardFeedService.Instance.RefreshFeed(
                GameState.Instance.HighestUnlockedStage,
                GameState.Instance.MaxStage,
                3,
                out var feedMessage);
            statusParts.Add(feedMessage);
        }

        if (ChallengeLeaderboardService.Instance != null)
        {
            ChallengeLeaderboardService.Instance.RefreshBoard(challenge.Code, 5, out var boardMessage);
            statusParts.Add(boardMessage);
        }

        RefreshDailyLeaderboard();

        if (statusParts.Count == 0)
        {
            statusParts.Add("Remote online services are unavailable.");
        }

        SetStatusMessage(string.Join("\n", statusParts));
        _onlineRoomAutoRefreshStatus = $"Online refresh at {DateTimeOffset.Now:HH:mm:ss}\n{string.Join("\n", statusParts)}";
        _onlineRoomAutoRefreshTimer = OnlineRoomAutoRefreshIntervalSeconds;
        RefreshUi();
    }

    private void TickOnlineRoomAutoRefresh(float delta)
    {
        if (!_onlineRoomAutoRefreshEnabled)
        {
            return;
        }

        if (AppLifecycleService.Instance?.ShouldPauseOnlineRoomTraffic == true)
        {
            _onlineRoomAutoRefreshStatus = "Joined room auto refresh paused: application is backgrounded or unfocused.";
            _onlineRoomAutoRefreshTimer = 0.5f;
            return;
        }

        if (OnlineRoomJoinService.GetCachedTicket() == null)
        {
            _onlineRoomAutoRefreshTimer = 0.5f;
            return;
        }

        if (!OnlineRoomJoinService.HasActiveTicket())
        {
            _onlineRoomAutoRefreshStatus = "Joined room auto refresh paused: the current room seat has expired. Use `Recover Seat` or `Refresh Online` to restore room play.";
            _onlineRoomAutoRefreshTimer = 0.5f;
            return;
        }

        _onlineRoomAutoRefreshTimer -= delta;
        if (_onlineRoomAutoRefreshTimer > 0f)
        {
            return;
        }

        _onlineRoomAutoRefreshTimer = OnlineRoomAutoRefreshIntervalSeconds;
        var statusParts = new List<string>();
        if (OnlineRoomSeatLeaseService.TryAutoRenewIfNeeded(out var leaseMessage))
        {
            statusParts.Add(leaseMessage);
        }
        OnlineRoomSessionService.RefreshJoinedRoom(out var sessionMessage);
        statusParts.Add(sessionMessage);
        OnlineRoomScoreboardService.RefreshJoinedRoomScoreboard(5, out var scoreboardMessage);
        statusParts.Add(scoreboardMessage);
        _onlineRoomAutoRefreshStatus = $"Auto refresh at {DateTimeOffset.Now:HH:mm:ss}\n{string.Join("\n", statusParts)}";
        RefreshUi();
    }

    private string BuildOnlineRoomAutoRefreshSummary()
    {
        var joinedRoomTicket = OnlineRoomJoinService.GetCachedTicket();
        var lifecycleSummary = AppLifecycleService.Instance?.BuildStatusSummary() ?? "App lifecycle service unavailable.";
        if (joinedRoomTicket == null)
        {
            return
                "Online room polling:\n" +
                "No joined room ticket is active.\n" +
                $"Mode: {(_onlineRoomAutoRefreshEnabled ? "armed" : "paused")}\n\n" +
                lifecycleSummary;
        }

        return
            "Online room polling:\n" +
            $"Mode: {(_onlineRoomAutoRefreshEnabled ? $"auto every {OnlineRoomAutoRefreshIntervalSeconds:0}s" : "paused")}\n" +
            $"Seat health: {(OnlineRoomJoinService.HasActiveTicket() ? "active" : OnlineRoomJoinService.GetCachedTicket() == null ? "none" : "expired")}\n" +
            _onlineRoomAutoRefreshStatus + "\n\n" +
            lifecycleSummary;
    }

    private string BuildRenewSeatButtonLabel()
    {
        var ticket = OnlineRoomJoinService.GetCachedTicket();
        if (ticket == null)
        {
            return "Quick Match";
        }

        return OnlineRoomJoinService.HasActiveTicket()
            ? "Refresh Seat Lease"
            : "Recover Seat";
    }

    private void OnAppLifecycleStateChanged()
    {
        if (!IsInsideTree())
        {
            return;
        }

        RefreshUi();
    }

    private void SyncStageSelector()
    {
        for (var i = 0; i < _stageSelector.ItemCount; i++)
        {
            if ((int)_stageSelector.GetItemMetadata(i).AsInt32() != _selectedStage)
            {
                continue;
            }

            _stageSelector.Select(i);
            return;
        }
    }

    private void SyncMutatorSelector()
    {
        for (var i = 0; i < _mutatorSelector.ItemCount; i++)
        {
            if (!_mutatorSelector.GetItemMetadata(i).AsString().Equals(_selectedMutatorId))
            {
                continue;
            }

            _mutatorSelector.Select(i);
            return;
        }
    }

    private string BuildHistoryText(AsyncChallengeDefinition challenge)
    {
        var sameCodeHistory = GameState.Instance.GetRecentChallengeHistory(3, challenge.Code);
        var recentHistory = GameState.Instance.GetRecentChallengeHistory(5);
        var builder = new StringBuilder();
        builder.AppendLine("Recent challenge history:");

        if (sameCodeHistory.Count == 0)
        {
            builder.AppendLine($"This code has no local attempts yet: {challenge.Code}");
        }
        else
        {
            builder.AppendLine("This code:");
            foreach (var entry in sameCodeHistory)
            {
                builder.AppendLine(FormatHistoryEntry(entry, false));
            }
        }

        if (recentHistory.Count == 0)
        {
            builder.Append("Recent queue: no challenge runs logged yet.");
        }
        else
        {
            builder.AppendLine("Recent queue:");
            foreach (var entry in recentHistory)
            {
                builder.AppendLine(FormatHistoryEntry(entry, true));
            }
        }

        return builder.ToString().TrimEnd();
    }

    private void SetStatusMessage(string message)
    {
        _lastStatusMessage = string.IsNullOrWhiteSpace(message)
            ? ""
            : message.Trim();
        _statusLabel.Text = $"Status:\n{_lastStatusMessage}";
    }

    private static string FormatHistoryEntry(ChallengeRunRecord record, bool includeCode)
    {
        var stamp = record.PlayedAtUnixSeconds > 0
            ? DateTimeOffset.FromUnixTimeSeconds(record.PlayedAtUnixSeconds).ToLocalTime().ToString("MM-dd HH:mm")
            : "--";
        var outcome = record.Retreated ? "RET" : record.Won ? "WIN" : "FAIL";
        var medal = "No Medal";
        if (AsyncChallengeCatalog.TryParse(record.Code, out var challenge, out _))
        {
            medal = AsyncChallengeCatalog.ResolveMedalLabel(challenge, record.Score);
        }

        var suffix = includeCode
            ? $" | {record.Code}"
            : $" | {record.StarsEarned}/3 stars | {record.EnemyDefeats} defeats";
        var rawScore = Math.Max(0, record.RawScore);
        if (rawScore == 0)
        {
            rawScore = Math.Max(0, record.Score);
        }

        var multiplier = record.ScoreMultiplier > 0f ? record.ScoreMultiplier : 1f;
        var hullPercent = Mathf.RoundToInt(Mathf.Clamp(record.BusHullRatio, 0f, 1f) * 100f);
        var deckMode = record.UsedLockedDeck ? "Locked deck" : "Player deck";
        return
            $"{stamp} | {outcome} | {record.Score} pts | {medal} | {record.ElapsedSeconds:0.0}s{suffix}\n" +
            $"  Raw {rawScore} x{multiplier:0.##} | Hull {hullPercent}% | Deploys {record.PlayerDeployments} | {deckMode}";
    }
}
