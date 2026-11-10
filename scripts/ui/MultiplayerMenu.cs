using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

public partial class MultiplayerMenu : Control
{
    // The session payload carries the compact scoreboard too. Ten seconds is
    // responsive enough for the prep monitor without turning every waiting
    // player into a database poller.
    private const float OnlineRoomAutoRefreshIntervalSeconds = 10f;

    private OptionButton _stageSelector = null!;
    private OptionButton _mutatorSelector = null!;
    private LineEdit _codeEdit = null!;
    private Label _summaryLabel = null!;
    private Label _recordLabel = null!;
    private Label _tapeLabel = null!;
    private Label _historyLabel = null!;
    private Label _rulesLabel = null!;
    private Label _statusLabel = null!;

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
        BuildRoyalLayout();
        RefreshUi();
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
        RefreshRoyal();
    }

    private void BuildUi()
    {
        MedievalUi.Apply(this);

        // The royal layout (MultiplayerMenu.Royal.cs) lays these selectors and the code field over the plate.
        _stageSelector = new OptionButton { FitToLongestItem = false };
        _stageSelector.ItemSelected += OnStageSelected;
        for (var stageNumber = 1; stageNumber <= GameState.Instance.MaxStage; stageNumber++)
        {
            var stage = GameData.GetStage(stageNumber);
            var index = _stageSelector.ItemCount;
            _stageSelector.AddItem($"{stage.MapName} - S{stage.StageNumber} {stage.StageName}");
            _stageSelector.SetItemMetadata(index, stage.StageNumber);
        }

        _mutatorSelector = new OptionButton { FitToLongestItem = false };
        _mutatorSelector.ItemSelected += OnMutatorSelected;
        foreach (var mutator in AsyncChallengeCatalog.GetAll())
        {
            var index = _mutatorSelector.ItemCount;
            _mutatorSelector.AddItem(mutator.Title);
            _mutatorSelector.SetItemMetadata(index, mutator.Id);
        }

        _codeEdit = new LineEdit { PlaceholderText = "CH-04-PRS-4821" };

        // The code menu's briefing and records dialogs read these; the status line is kept but not shown.
        Label Hidden()
        {
            var label = new Label { Visible = false };
            AddChild(label);
            return label;
        }
        _summaryLabel = Hidden();
        _recordLabel = Hidden();
        _tapeLabel = Hidden();
        _historyLabel = Hidden();
        _rulesLabel = Hidden();
        _statusLabel = Hidden();
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

        ComposePage();

        var canStart = GameState.Instance.CanStartAsyncChallenge(out var readinessMessage);
        var startButtonText = canStart ? $"Start {challenge.Code}" : "Challenge not ready";
        if (TryBuildOnlineRoomStartState(challenge, canStart, readinessMessage, out var onlineRoomCanStart, out var onlineRoomButtonText, out var onlineRoomMessage))
        {
            canStart = onlineRoomCanStart;
            startButtonText = onlineRoomButtonText;
            readinessMessage = onlineRoomMessage;
        }

        var statusMessage = string.IsNullOrWhiteSpace(_lastStatusMessage)
            ? readinessMessage
            : _lastStatusMessage;
        _statusLabel.Text = statusMessage;
        _royalStart.Disabled = !canStart;
        _royalStart.TooltipText = startButtonText;
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
            $"Previewed room board {room.BoardCode} from {room.Title}. Request join to take a seat; previewing only loads the board here.");
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
        buttonText = baseCanStart ? $"Start {challenge.Code}" : "Challenge not ready";
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
            buttonText = "Recover seat";
            message =
                $"Joined room seat for {ticket.RoomTitle} has expired.\n" +
                "Recover your seat or use Match before deploying into this room race.";
            return true;
        }

        if (string.Equals(ticket.Status, "spectate", StringComparison.OrdinalIgnoreCase))
        {
            canStart = false;
            buttonText = "Spectating";
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
            buttonText = "Room board not ready";
            message =
                $"{baseReadinessMessage}\n" +
                $"Room seat is armed for {ticket.RoomTitle}, but the selected board is not startable yet.";
            return true;
        }

        var roomSnapshot = OnlineRoomSessionService.GetCachedSnapshot()?.RoomSnapshot;
        if (roomSnapshot == null || !roomSnapshot.HasRoom)
        {
            canStart = false;
            buttonText = "Refresh room";
            message =
                $"Room seat is armed for {ticket.RoomTitle}, but no live room snapshot is cached yet.\n" +
                "Refresh the room before entering the race.";
            return true;
        }

        if (roomSnapshot.RoundComplete)
        {
            canStart = false;
            buttonText = "Round complete";
            message =
                $"The current room round for {ticket.RoomTitle} is already complete.\n" +
                "Wait for the host to reset the round, or leave and join a fresh room board.";
            return true;
        }

        if (!roomSnapshot.RoundLocked)
        {
            canStart = false;
            buttonText = OnlineRoomActionService.CanLaunchRound() ? "Launch round" : "Waiting for launch";
            message = OnlineRoomActionService.CanLaunchRound()
                ? $"Host seat is armed for {ticket.RoomTitle}. Launch the room round first, then deploy into the shared race."
                : $"Joined room {ticket.RoomTitle} is armed for {challenge.Code}, but the host has not launched the round yet.\nReady up in the room controls and wait for the race countdown.";
            return true;
        }

        canStart = true;
        buttonText = roomSnapshot.RaceCountdownActive ? "Enter race" : "Deploy into room";
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

        if (LaunchFeatures.InternetRooms)
        {
            OnlineRoomDirectoryService.RefreshRooms(
                GameState.Instance.HighestUnlockedStage,
                GameState.Instance.MaxStage,
                4,
                out var roomMessage);
            statusParts.Add(roomMessage);
        }

        if (LaunchFeatures.InternetRooms && OnlineRoomJoinService.GetCachedTicket() != null)
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
            _onlineRoomAutoRefreshStatus = "Joined room auto refresh paused: the current room seat has expired. Recover your seat or refresh to continue.";
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
            return "Quick match";
        }

        return OnlineRoomJoinService.HasActiveTicket()
            ? "Refresh seat"
            : "Recover seat";
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
        var medal = "No medal";
        if (AsyncChallengeCatalog.TryParse(record.Code, out var challenge, out _))
        {
            medal = AsyncChallengeCatalog.ResolveMedalLabel(challenge, record.Score);
        }

        var suffix = includeCode
            ? $" · {record.Code}"
            : $" · {record.StarsEarned}/3 stars · {record.EnemyDefeats} defeats";
        var rawScore = Math.Max(0, record.RawScore);
        if (rawScore == 0)
        {
            rawScore = Math.Max(0, record.Score);
        }

        var multiplier = record.ScoreMultiplier > 0f ? record.ScoreMultiplier : 1f;
        var hullPercent = Mathf.RoundToInt(Mathf.Clamp(record.BusHullRatio, 0f, 1f) * 100f);
        var deckMode = record.UsedLockedDeck ? "Locked deck" : "Player deck";
        return
            $"{stamp} · {outcome} · {record.Score} pts · {medal} · {record.ElapsedSeconds:0.0}s{suffix}\n" +
            $"  Raw {rawScore} x{multiplier:0.##} · Hull {hullPercent}% · Deploys {record.PlayerDeployments} · {deckMode}";
    }
}
