using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class LanChallengeService : Node
{
	[Signal]
	public delegate void StateChangedEventHandler();

	private sealed class LanRaceSubmission
	{
		public int PeerId { get; init; }
		public string DisplayName { get; init; } = "";
		public int Score { get; init; }
		public float ElapsedSeconds { get; init; }
		public int StarsEarned { get; init; }
		public int EnemyDefeats { get; init; }
		public int HullPercent { get; init; }
		public bool Won { get; init; }
		public bool Retreated { get; init; }
		public bool UsedLockedDeck { get; init; }
		public bool Disconnected { get; init; }
	}

	private sealed class LanRaceTelemetry
	{
		public int PeerId { get; init; }
		public int ElapsedDeciseconds { get; init; }
		public int EnemyDefeats { get; init; }
		public int HullPercent { get; init; }
	}

	private sealed class LanSessionStanding
	{
		public int PeerId { get; init; }
		public string DisplayName { get; set; } = "";
		public int Races { get; set; }
		public int Wins { get; set; }
		public int Retreats { get; set; }
		public int Disconnects { get; set; }
		public int TotalScore { get; set; }
		public int BestScore { get; set; }
		public float BestTimeSeconds { get; set; } = float.MaxValue;
	}

	public static LanChallengeService Instance { get; private set; }

	public const int DefaultPort = 24680;
	public const int MinPort = 1024;
	public const int MaxPort = 65535;

	public bool HasRoom => Multiplayer.MultiplayerPeer != null;
	public bool IsHosting => HasRoom && Multiplayer.IsServer();
	public bool IsClient => HasRoom && !Multiplayer.IsServer();
	public int RoomPort { get; private set; } = DefaultPort;
	public string SharedChallengeCode { get; private set; } = "";
	public string SharedChallengeTitle { get; private set; } = "";
	public string SessionStatus { get; private set; } = "No LAN race room active.";
	public string ScoreboardSummary { get; private set; } = "LAN scoreboard: no race submissions yet.";
	public string SessionStandingsSummary { get; private set; } = "LAN session standings: no completed LAN races yet.";
	public IReadOnlyList<string> SharedLockedDeckUnitIds => _sharedLockedDeckUnitIds;
	public bool RoundLocked => IsRoundLocked();
	public bool RoundComplete => IsRoundCompleteAwaitingReset();
	public bool RaceCountdownActive => _raceCountdownRemainingSeconds > 0.001f;
	public float RaceCountdownRemainingSeconds => Mathf.Max(0f, _raceCountdownRemainingSeconds);
	public bool RaceCombatReleased { get; private set; }
	public bool LocalReady =>
		HasRoom &&
		_readyPeers.TryGetValue(Multiplayer.GetUniqueId(), out var ready) &&
		ready;
	public bool LocalSpectating =>
		HasRoom &&
		ResolvePeerPhase(Multiplayer.GetUniqueId()) == PeerPhaseSpectating;
	public bool AllPeersReady =>
		GetLaunchEligiblePeerIds().Length > 0 &&
		GetLaunchEligiblePeerIds().All(peerId => _readyPeers.TryGetValue(peerId, out var ready) && ready);
	public bool AllPeersDecksReady =>
		HasRoom &&
		(HasSharedLockedDeck() || (GetLaunchEligiblePeerIds().Length > 0 && GetLaunchEligiblePeerIds().All(ResolvePeerDeckIsFull)));

	private const string PeerPhaseLobby = "lobby";
	private const string PeerPhaseSpectating = "spectating";
	private const string PeerPhaseLoading = "loading";
	private const string PeerPhaseRacing = "racing";
	private const string PeerPhaseSubmitted = "submitted";
	private const float RaceStartCountdownSeconds = 3f;

	private readonly List<string> _sharedLockedDeckUnitIds = new();
	private readonly Dictionary<int, LanRaceSubmission> _submissions = new();
	private readonly Dictionary<int, bool> _readyPeers = new();
	private readonly Dictionary<int, string> _peerNames = new();
	private readonly Dictionary<int, string> _peerPhases = new();
	private readonly Dictionary<int, string[]> _peerDecks = new();
	private readonly Dictionary<int, LanRaceTelemetry> _peerTelemetry = new();
	private readonly Dictionary<int, LanSessionStanding> _sessionStandings = new();
	private readonly Dictionary<int, bool> _loadedPeers = new();
	private float _raceCountdownRemainingSeconds;

	public override void _EnterTree()
	{
		Instance = this;
	}

	public override void _ExitTree()
	{
		if (Instance == this)
		{
			Instance = null;
		}
	}

	public override void _Ready()
	{
		Multiplayer.PeerConnected += OnPeerConnected;
		Multiplayer.PeerDisconnected += OnPeerDisconnected;
		Multiplayer.ConnectedToServer += OnConnectedToServer;
		Multiplayer.ConnectionFailed += OnConnectionFailed;
		Multiplayer.ServerDisconnected += OnServerDisconnected;
	}

	public override void _Process(double delta)
	{
		if (_raceCountdownRemainingSeconds <= 0.001f)
		{
			return;
		}

		_raceCountdownRemainingSeconds = Mathf.Max(0f, _raceCountdownRemainingSeconds - (float)delta);
		if (_raceCountdownRemainingSeconds <= 0.001f)
		{
			_raceCountdownRemainingSeconds = 0f;
			RaceCombatReleased = true;
			NotifyStateChanged();
		}
	}

	public void ConfigureRoomPort(int port)
	{
		RoomPort = Mathf.Clamp(port, MinPort, MaxPort);
	}

	public void ResetRoomPort()
	{
		RoomPort = DefaultPort;
	}

	public bool HostSelectedBoard(out string message)
	{
		if (IsHosting && IsRoundLocked())
		{
			message = "Cannot rehost while the current LAN race is still in progress.";
			return false;
		}

		var challenge = GameState.Instance.GetSelectedAsyncChallenge();
		var stage = GameData.GetStage(Mathf.Clamp(challenge.Stage, 1, GameState.Instance.MaxStage));
		var mutator = AsyncChallengeCatalog.GetMutator(challenge.MutatorId);
		var boardTitle = $"{stage.MapName} S{stage.StageNumber}  |  {mutator.Title}";
		var lockedDeckIds = GameState.Instance.HasSelectedAsyncChallengeLockedDeck
			? GameState.Instance.GetSelectedAsyncChallengeDeckUnits().Select(unit => unit.Id)
			: Array.Empty<string>();

		if (!GameState.Instance.TrySetSelectedAsyncChallengeBoard(challenge.Code, lockedDeckIds, out message))
		{
			return false;
		}

		CloseRoomInternal();

		var peer = new ENetMultiplayerPeer();
		var error = peer.CreateServer(RoomPort, 3);
		if (error != Error.Ok)
		{
			message = $"Could not host LAN race on port {RoomPort}: {error}.";
			SessionStatus = message;
			NotifyStateChanged();
			return false;
		}

		Multiplayer.MultiplayerPeer = peer;
		ApplySharedBoard(challenge.Code, boardTitle, lockedDeckIds);
		_readyPeers.Clear();
		_readyPeers[Multiplayer.GetUniqueId()] = false;
		_peerNames.Clear();
		_peerNames[Multiplayer.GetUniqueId()] = GameState.Instance.PlayerCallsign;
		_peerPhases.Clear();
		_peerPhases[Multiplayer.GetUniqueId()] = PeerPhaseLobby;
		_peerDecks.Clear();
		_peerDecks[Multiplayer.GetUniqueId()] = BuildLocalDeckProfile();
		_loadedPeers.Clear();
		_loadedPeers[Multiplayer.GetUniqueId()] = false;
		ResetRoomRaceProgress(false);
		SessionStatus = $"Hosting {challenge.Code} on port {RoomPort}. Share IP {BuildJoinAddressSummary()}.";
		message = SessionStatus;
		NotifyStateChanged();
		return true;
	}

	public bool RefreshHostedBoard(out string message)
	{
		if (!IsHosting)
		{
			message = "Host a LAN room before broadcasting a board.";
			return false;
		}

		if (IsRoundLocked())
		{
			message = "Cannot broadcast a new board while the current LAN race is still in progress.";
			return false;
		}

		var challenge = GameState.Instance.GetSelectedAsyncChallenge();
		var stage = GameData.GetStage(Mathf.Clamp(challenge.Stage, 1, GameState.Instance.MaxStage));
		var mutator = AsyncChallengeCatalog.GetMutator(challenge.MutatorId);
		var boardTitle = $"{stage.MapName} S{stage.StageNumber}  |  {mutator.Title}";
		var lockedDeckIds = GameState.Instance.HasSelectedAsyncChallengeLockedDeck
			? GameState.Instance.GetSelectedAsyncChallengeDeckUnits().Select(unit => unit.Id)
			: Array.Empty<string>();

		if (!GameState.Instance.TrySetSelectedAsyncChallengeBoard(challenge.Code, lockedDeckIds, out message))
		{
			return false;
		}

		ApplySharedBoard(challenge.Code, boardTitle, lockedDeckIds);
		ResetRoomRaceProgress(true);
		Rpc(nameof(ReceiveBoardState), SharedChallengeCode, SharedChallengeTitle, string.Join(",", _sharedLockedDeckUnitIds));
		Rpc(nameof(ReceiveScoreboardSummary), ScoreboardSummary);
		SessionStatus = $"Broadcast updated LAN board {SharedChallengeCode}.";
		BroadcastReadySnapshot();
		BroadcastPhaseSnapshot();
		BroadcastDeckSnapshot();
		BroadcastTelemetrySnapshot();
		message = SessionStatus;
		NotifyStateChanged();
		return true;
	}

	public bool JoinRoom(string address, out string message)
	{
		var normalizedAddress = string.IsNullOrWhiteSpace(address)
			? "127.0.0.1"
			: address.Trim();

		CloseRoomInternal();

		var peer = new ENetMultiplayerPeer();
		var error = peer.CreateClient(normalizedAddress, RoomPort);
		if (error != Error.Ok)
		{
			message = $"Could not join {normalizedAddress}:{RoomPort}: {error}.";
			SessionStatus = message;
			NotifyStateChanged();
			return false;
		}

		Multiplayer.MultiplayerPeer = peer;
		_submissions.Clear();
		_readyPeers.Clear();
		_peerPhases.Clear();
		_peerDecks.Clear();
		_loadedPeers.Clear();
		ScoreboardSummary = "LAN scoreboard: waiting for host data.";
		SessionStatus = $"Connecting to {normalizedAddress}:{RoomPort}...";
		message = SessionStatus;
		NotifyStateChanged();
		return true;
	}

	public void CloseRoom()
	{
		CloseRoomInternal();
		SessionStatus = "LAN room closed.";
		NotifyStateChanged();
	}

	public bool LaunchRace(out string message)
	{
		if (!IsHosting)
		{
			message = "Only the host can launch a LAN race.";
			return false;
		}

		if (IsRoundLocked())
		{
			message = "LAN round already in progress. Wait for the current race to finish.";
			return false;
		}

		if (string.IsNullOrWhiteSpace(SharedChallengeCode))
		{
			message = "No shared LAN board is armed.";
			return false;
		}

		var launchEligiblePeerIds = GetLaunchEligiblePeerIds();
		if (launchEligiblePeerIds.Length == 0)
		{
			message = "Cannot launch yet. No runners have joined the rematch pool.";
			return false;
		}

		if (!HasSharedLockedDeck())
		{
			var deckBlockerPeerIds = launchEligiblePeerIds.Where(peerId => !ResolvePeerDeckIsFull(peerId)).ToArray();
			if (deckBlockerPeerIds.Length > 0)
			{
				message = $"Cannot launch yet. Deck blockers: {BuildPeerListText(deckBlockerPeerIds)}.";
				return false;
			}
		}

		var unreadyPeerIds = launchEligiblePeerIds.Where(peerId => !LocalReadyState(peerId)).ToArray();
		if (unreadyPeerIds.Length > 0)
		{
			message = $"Cannot launch yet. Waiting on ready: {BuildPeerListText(unreadyPeerIds)}.";
			return false;
		}

		ResetRoomRaceProgress(true);
		SetAllPeerPhases(PeerPhaseLoading, preserveSpectators: true);
		Rpc(nameof(ReceiveScoreboardSummary), ScoreboardSummary);
		BroadcastReadySnapshot();
		BroadcastPhaseSnapshot();
		BroadcastTelemetrySnapshot();
		var localPeerId = Multiplayer.GetUniqueId();
		var lockedDeckCsv = string.Join(",", _sharedLockedDeckUnitIds);
		if (launchEligiblePeerIds.Contains(localPeerId))
		{
			BeginLanRace(SharedChallengeCode, lockedDeckCsv);
		}

		foreach (var peerId in launchEligiblePeerIds)
		{
			if (peerId == localPeerId)
			{
				continue;
			}

			RpcId(peerId, nameof(BeginLanRace), SharedChallengeCode, lockedDeckCsv);
		}

		SessionStatus = $"LAN race launched on {SharedChallengeCode}.";
		message = SessionStatus;
		NotifyStateChanged();
		return true;
	}

	public bool ToggleLocalReady(out string message)
	{
		if (!HasRoom)
		{
			message = "Host or join a LAN room first.";
			return false;
		}

		if (ResolvePeerPhase(Multiplayer.GetUniqueId()) == PeerPhaseSpectating)
		{
			if (!IsRoundCompleteAwaitingReset())
			{
				message = "You are spectating the current LAN race. Join the rematch pool once the round completes.";
				return false;
			}
		}

		if (!HasSharedLockedDeck() && !ResolvePeerDeckIsFull(Multiplayer.GetUniqueId()))
		{
			message = $"Fill a {GameState.Instance.DeckSizeLimit}-card convoy deck before readying for LAN launch.";
			return false;
		}

		var localPeerId = Multiplayer.GetUniqueId();
		var localPhase = ResolvePeerPhase(localPeerId);
		var nextReady = !LocalReady;
		if (IsHosting)
		{
			if (nextReady &&
				(localPhase == PeerPhaseSpectating ||
				(localPhase == PeerPhaseSubmitted && IsRoundCompleteAwaitingReset())))
			{
				_peerPhases[localPeerId] = PeerPhaseLobby;
				BroadcastPhaseSnapshot();
			}

			_readyPeers[localPeerId] = nextReady;
			SessionStatus = nextReady ? "Host marked ready." : "Host cleared ready state.";
			BroadcastReadySnapshot();
		}
		else
		{
			if (nextReady &&
				(localPhase == PeerPhaseSpectating ||
				(localPhase == PeerPhaseSubmitted && IsRoundCompleteAwaitingReset())))
			{
				_peerPhases[localPeerId] = PeerPhaseLobby;
				RpcId(1, nameof(SetRemotePeerPhase), PeerPhaseLobby);
			}

			_readyPeers[localPeerId] = nextReady;
			RpcId(1, nameof(SetRemoteReadyState), nextReady);
			SessionStatus = nextReady
				? "Marked ready for the next LAN rematch."
				: "Cleared LAN ready state.";
			NotifyStateChanged();
		}

		message = SessionStatus;
		return true;
	}

	public void RefreshLocalProfile()
	{
		if (!HasRoom)
		{
			return;
		}

		var localPeerId = Multiplayer.GetUniqueId();
		_peerNames[localPeerId] = GameState.Instance.PlayerCallsign;
		if (IsHosting)
		{
			SessionStatus = $"Updated host convoy callsign to {GameState.Instance.PlayerCallsign}.";
			BroadcastNameSnapshot();
		}
		else
		{
			SessionStatus = $"Updated local convoy callsign to {GameState.Instance.PlayerCallsign}.";
			RpcId(1, nameof(SetRemotePeerProfile), GameState.Instance.PlayerCallsign);
			NotifyStateChanged();
		}
	}

	public void RefreshLocalDeckProfile()
	{
		if (!HasRoom)
		{
			return;
		}

		var localPeerId = Multiplayer.GetUniqueId();
		_peerDecks[localPeerId] = BuildLocalDeckProfile();
		_readyPeers[localPeerId] = false;
		if (IsHosting)
		{
			SessionStatus = "Updated host convoy deck. Ready state cleared.";
			BroadcastDeckSnapshot();
			BroadcastReadySnapshot();
		}
		else
		{
			SessionStatus = "Updated local convoy deck. Ready state cleared.";
			RpcId(1, nameof(SetRemotePeerDeckProfile), string.Join(",", BuildLocalDeckProfile()));
			RpcId(1, nameof(SetRemoteReadyState), false);
			NotifyStateChanged();
		}
	}

	public void SubmitChallengeResult(
		AsyncChallengeDefinition challenge,
		AsyncChallengeScoreBreakdown scoreBreakdown,
		float elapsedSeconds,
		int starsEarned,
		int enemyDefeats,
		float busHullRatio,
		bool won,
		bool retreated,
		bool usedLockedDeck)
	{
		if (!HasRoom ||
			challenge == null ||
			string.IsNullOrWhiteSpace(SharedChallengeCode) ||
			!challenge.Code.Equals(SharedChallengeCode, StringComparison.OrdinalIgnoreCase))
		{
			return;
		}

		var hullPercent = Mathf.RoundToInt(Mathf.Clamp(busHullRatio, 0f, 1f) * 100f);
		var score = scoreBreakdown?.FinalScore ?? 0;
		var telemetry = BuildTelemetry(Multiplayer.GetUniqueId(), elapsedSeconds, enemyDefeats, hullPercent);

		if (IsHosting)
		{
			StoreSubmission(new LanRaceSubmission
			{
				PeerId = Multiplayer.GetUniqueId(),
				Score = score,
				ElapsedSeconds = elapsedSeconds,
				StarsEarned = starsEarned,
				EnemyDefeats = enemyDefeats,
				HullPercent = hullPercent,
				Won = won,
				Retreated = retreated,
				UsedLockedDeck = usedLockedDeck
			});
			StoreTelemetry(telemetry);
			_peerPhases[Multiplayer.GetUniqueId()] = PeerPhaseSubmitted;
			BroadcastScoreboard();
			BroadcastSessionStandings();
			BroadcastPhaseSnapshot();
			BroadcastTelemetrySnapshot();
			return;
		}

		RpcId(
			1,
			nameof(ReceiveRaceSubmission),
			score,
			elapsedSeconds,
			starsEarned,
			enemyDefeats,
			hullPercent,
			won,
			retreated,
			usedLockedDeck);
		StoreTelemetry(telemetry);
		_peerPhases[Multiplayer.GetUniqueId()] = PeerPhaseSubmitted;
		SessionStatus = $"Submitted LAN result for {challenge.Code}. Waiting for host scoreboard.";
		NotifyStateChanged();
	}

	public void UpdateLocalRaceTelemetry(float elapsedSeconds, int enemyDefeats, float busHullRatio)
	{
		if (!HasRoom || ResolvePeerPhase(Multiplayer.GetUniqueId()) != PeerPhaseRacing)
		{
			return;
		}

		var telemetry = BuildTelemetry(
			Multiplayer.GetUniqueId(),
			elapsedSeconds,
			enemyDefeats,
			Mathf.RoundToInt(Mathf.Clamp(busHullRatio, 0f, 1f) * 100f));
		StoreTelemetry(telemetry);
		if (IsHosting)
		{
			BroadcastTelemetrySnapshot();
		}
		else
		{
			RpcId(1, nameof(SetRemoteRaceTelemetry), telemetry.ElapsedDeciseconds, telemetry.EnemyDefeats, telemetry.HullPercent);
			NotifyStateChanged();
		}
	}

	public void ReportLocalBattleLoaded()
	{
		if (!HasRoom)
		{
			return;
		}

		var localPeerId = Multiplayer.GetUniqueId();
		_loadedPeers[localPeerId] = true;
		if (IsHosting)
		{
			TryStartRaceCountdown();
		}
		else
		{
			RpcId(1, nameof(SetRemoteBattleLoaded));
		}
	}

	public MultiplayerRoomSnapshot BuildRoomSnapshot()
	{
		var challenge = GameState.Instance.GetSelectedAsyncChallenge();
		var selectedDeckMode = GameState.Instance.HasSelectedAsyncChallengeLockedDeck ? "locked squad" : "player squad";
		if (!HasRoom)
		{
			return new MultiplayerRoomSnapshot
			{
				HasRoom = false,
				TransportLabel = "LAN ENet",
				SelectedBoardCode = challenge.Code,
				SelectedBoardDeckMode = selectedDeckMode
			};
		}

		var deckMode = HasSharedLockedDeck()
			? $"Locked squad: {string.Join(", ", _sharedLockedDeckUnitIds.Select(unitId => GameData.GetUnit(unitId).DisplayName))}"
			: "Uses each player's current async squad.";
		var peerSnapshots = GetKnownPeerIds()
			.Select(BuildPeerSnapshot)
			.ToArray();
		return new MultiplayerRoomSnapshot
		{
			HasRoom = true,
			TransportLabel = "LAN ENet",
			RoleLabel = IsHosting ? "Host" : "Runner",
			PeerCount = Multiplayer.GetPeers().Length + 1,
			SharedChallengeCode = SharedChallengeCode,
			SharedChallengeTitle = SharedChallengeTitle,
			LocalCallsign = GameState.Instance.PlayerCallsign,
			DeckModeSummary = deckMode,
			JoinAddressSummary = IsHosting ? BuildJoinAddressSummary() : "",
			UsesLockedDeck = HasSharedLockedDeck(),
			RoundLocked = RoundLocked,
			RoundComplete = RoundComplete,
			RaceCountdownActive = RaceCountdownActive,
			RaceCountdownRemainingSeconds = RaceCountdownRemainingSeconds,
			SelectedBoardCode = challenge.Code,
			SelectedBoardDeckMode = selectedDeckMode,
			Peers = peerSnapshots
		};
	}

	public string BuildRoomSummary()
	{
		return MultiplayerRoomFormatter.BuildRoomSummary(BuildRoomSnapshot());
	}

	public string BuildLaunchReadinessSummary()
	{
		return MultiplayerRoomFormatter.BuildLaunchReadinessSummary(BuildRoomSnapshot());
	}

	public string BuildRaceMonitorSummary()
	{
		return MultiplayerRoomFormatter.BuildRaceMonitorSummary(BuildRoomSnapshot());
	}

	private void ApplySharedBoard(string code, string title, IEnumerable<string> lockedDeckIds)
	{
		SharedChallengeCode = AsyncChallengeCatalog.NormalizeCode(code);
		SharedChallengeTitle = string.IsNullOrWhiteSpace(title)
			? SharedChallengeCode
			: title.Trim();
		_sharedLockedDeckUnitIds.Clear();
		if (lockedDeckIds != null)
		{
			foreach (var unitId in lockedDeckIds)
			{
				if (!string.IsNullOrWhiteSpace(unitId))
				{
					_sharedLockedDeckUnitIds.Add(unitId.Trim());
				}
			}
		}
	}

	private string BuildJoinAddressSummary()
	{
		var localAddresses = IP.GetLocalAddresses()
			.Where(address =>
				!string.IsNullOrWhiteSpace(address) &&
				address.Contains('.') &&
				!address.StartsWith("127.", StringComparison.OrdinalIgnoreCase))
			.Distinct()
			.Take(2)
			.ToArray();
		return localAddresses.Length > 0
			? string.Join(" / ", localAddresses)
			: "127.0.0.1";
	}

	private MultiplayerRoomPeerSnapshot BuildPeerSnapshot(int peerId)
	{
		var phase = ResolvePeerPhase(peerId);
		return new MultiplayerRoomPeerSnapshot
		{
			PeerId = peerId,
			Label = ResolvePeerLabel(peerId),
			Phase = phase,
			IsReady = LocalReadyState(peerId),
			IsLoaded = ResolvePeerLoaded(peerId),
			IsLaunchEligible = phase != PeerPhaseSpectating,
			HasFullDeck = ResolvePeerDeckIsFull(peerId),
			MonitorRank = ResolvePeerMonitorRank(peerId),
			PresenceText = ResolvePeerPresenceText(peerId),
			MonitorText = BuildPeerMonitorLine(peerId),
			DeckText = BuildPeerDeckSummaryLine(peerId)
		};
	}

	private void StoreSubmission(LanRaceSubmission submission)
	{
		var normalized = new LanRaceSubmission
		{
			PeerId = submission.PeerId,
			DisplayName = string.IsNullOrWhiteSpace(submission.DisplayName)
				? ResolvePeerLabel(submission.PeerId)
				: submission.DisplayName.Trim(),
			Score = submission.Score,
			ElapsedSeconds = submission.ElapsedSeconds,
			StarsEarned = submission.StarsEarned,
			EnemyDefeats = submission.EnemyDefeats,
			HullPercent = submission.HullPercent,
			Won = submission.Won,
			Retreated = submission.Retreated,
			UsedLockedDeck = submission.UsedLockedDeck,
			Disconnected = submission.Disconnected
		};
		_submissions[normalized.PeerId] = normalized;
		_peerPhases[normalized.PeerId] = PeerPhaseSubmitted;
		RecordSessionStanding(normalized);
		ScoreboardSummary = BuildScoreboardText();
		SessionStandingsSummary = BuildSessionStandingsText();
		SessionStatus = normalized.Disconnected
			? $"{normalized.DisplayName} disconnected during LAN race."
			: $"{normalized.DisplayName} posted {normalized.Score} pts on {SharedChallengeCode}.";
		if (IsRoundCompleteAwaitingReset())
		{
			SessionStatus = $"LAN round complete on {SharedChallengeCode}. Review the room board and arm the next rematch.";
		}
	}

	private void RecordSessionStanding(LanRaceSubmission submission)
	{
		if (!_sessionStandings.TryGetValue(submission.PeerId, out var standing))
		{
			standing = new LanSessionStanding
			{
				PeerId = submission.PeerId
			};
			_sessionStandings[submission.PeerId] = standing;
		}

		standing.DisplayName = string.IsNullOrWhiteSpace(submission.DisplayName)
			? ResolvePeerLabel(submission.PeerId)
			: submission.DisplayName.Trim();
		standing.Races++;
		standing.TotalScore += Math.Max(0, submission.Score);
		standing.BestScore = Math.Max(standing.BestScore, Math.Max(0, submission.Score));
		if (submission.Won)
		{
			standing.Wins++;
			standing.BestTimeSeconds = Mathf.Min(standing.BestTimeSeconds, Mathf.Max(0f, submission.ElapsedSeconds));
		}

		if (submission.Retreated)
		{
			standing.Retreats++;
		}

		if (submission.Disconnected)
		{
			standing.Disconnects++;
		}
	}

	private void StoreTelemetry(LanRaceTelemetry telemetry)
	{
		_peerTelemetry[telemetry.PeerId] = telemetry;
	}

	private void TryStartRaceCountdown()
	{
		if (!IsHosting || RaceCountdownActive || RaceCombatReleased)
		{
			return;
		}

		var peerIds = GetLaunchEligiblePeerIds();
		if (peerIds.Length == 0 || !peerIds.All(ResolvePeerLoaded))
		{
			return;
		}

		SetAllPeerPhases(PeerPhaseRacing);
		BroadcastPhaseSnapshot();
		Rpc(nameof(BeginRaceCountdown), RaceStartCountdownSeconds);
		SessionStatus = $"All runners loaded. LAN countdown started for {SharedChallengeCode}.";
		NotifyStateChanged();
	}

	private LanRaceTelemetry BuildTelemetry(int peerId, float elapsedSeconds, int enemyDefeats, int hullPercent)
	{
		return new LanRaceTelemetry
		{
			PeerId = peerId,
			ElapsedDeciseconds = Mathf.Max(0, Mathf.RoundToInt(Mathf.Max(0f, elapsedSeconds) * 10f)),
			EnemyDefeats = Math.Max(0, enemyDefeats),
			HullPercent = Mathf.Clamp(hullPercent, 0, 100)
		};
	}

	private string BuildWaitingScoreboardText()
	{
		return string.IsNullOrWhiteSpace(SharedChallengeCode)
			? "LAN scoreboard: waiting for race submissions."
			: $"LAN scoreboard for {SharedChallengeCode}: waiting for race submissions.";
	}

	private string BuildSessionStandingsText()
	{
		if (_sessionStandings.Count == 0)
		{
			return "LAN session standings: no completed LAN races yet.";
		}

		var lines = new List<string>
		{
			"LAN session standings:"
		};
		var ordered = _sessionStandings.Values
			.OrderByDescending(entry => entry.Wins)
			.ThenByDescending(entry => entry.TotalScore)
			.ThenByDescending(entry => entry.BestScore)
			.ThenBy(entry => entry.BestTimeSeconds)
			.ToArray();

		for (var i = 0; i < ordered.Length; i++)
		{
			var entry = ordered[i];
			var bestTimeText = entry.BestTimeSeconds >= float.MaxValue * 0.5f
				? "no clear"
				: $"{entry.BestTimeSeconds:0.0}s best";
			lines.Add(
				$"{i + 1}. {entry.DisplayName}  |  Wins {entry.Wins}  |  Races {entry.Races}  |  Total {entry.TotalScore} pts  |  Best {entry.BestScore} pts  |  {bestTimeText}  |  RET {entry.Retreats}  |  DC {entry.Disconnects}");
		}

		return string.Join("\n", lines);
	}

	private string BuildScoreboardText()
	{
		if (_submissions.Count == 0)
		{
			return "LAN scoreboard: no race submissions yet.";
		}

		var lines = new List<string>
		{
			$"LAN scoreboard for {SharedChallengeCode}:"
		};
		var ordered = _submissions.Values
			.OrderByDescending(entry => entry.Score)
			.ThenBy(entry => entry.ElapsedSeconds)
			.ThenByDescending(entry => entry.HullPercent)
			.ToArray();

		for (var i = 0; i < ordered.Length; i++)
		{
			var entry = ordered[i];
			var outcome = entry.Disconnected
				? "DC"
				: entry.Retreated
				? "RET"
				: entry.Won
					? "WIN"
					: "FAIL";
			lines.Add(
				$"{i + 1}. {entry.DisplayName}  |  {outcome}  |  {entry.Score} pts  |  {entry.ElapsedSeconds:0.0}s  |  Hull {entry.HullPercent}%  |  Stars {entry.StarsEarned}/3  |  Defeats {entry.EnemyDefeats}  |  {(entry.UsedLockedDeck ? "locked" : "player")} deck");
		}

		return string.Join("\n", lines);
	}

	private string BuildPeerListText(IEnumerable<int> peerIds)
	{
		var labels = peerIds
			.Select(ResolvePeerLabel)
			.Where(label => !string.IsNullOrWhiteSpace(label))
			.ToArray();
		return labels.Length == 0 ? "none" : string.Join(", ", labels);
	}

	private string ResolvePeerLabel(int peerId)
	{
		if (_submissions.TryGetValue(peerId, out var submission) && !string.IsNullOrWhiteSpace(submission.DisplayName))
		{
			return submission.DisplayName;
		}

		if (_peerNames.TryGetValue(peerId, out var callsign) && !string.IsNullOrWhiteSpace(callsign))
		{
			return peerId == 1
				? $"Host {callsign}"
				: callsign;
		}

		return peerId == 1 ? "Host" : $"Runner {peerId}";
	}

	private void BroadcastScoreboard()
	{
		Rpc(nameof(ReceiveScoreboardSummary), ScoreboardSummary);
		NotifyStateChanged();
	}

	private void BroadcastSessionStandings()
	{
		Rpc(nameof(ReceiveSessionStandingsSummary), SessionStandingsSummary);
		NotifyStateChanged();
	}

	private void CloseRoomInternal()
	{
		if (Multiplayer.MultiplayerPeer != null)
		{
			Multiplayer.MultiplayerPeer = null;
		}

		SharedChallengeCode = "";
		SharedChallengeTitle = "";
		_sharedLockedDeckUnitIds.Clear();
		_submissions.Clear();
		_readyPeers.Clear();
		_peerNames.Clear();
		_peerPhases.Clear();
		_peerDecks.Clear();
		_peerTelemetry.Clear();
		_sessionStandings.Clear();
		_loadedPeers.Clear();
		ScoreboardSummary = "LAN scoreboard: no race submissions yet.";
		SessionStandingsSummary = "LAN session standings: no completed LAN races yet.";
		RaceCombatReleased = false;
		_raceCountdownRemainingSeconds = 0f;
	}

	private void BroadcastReadySnapshot()
	{
		Rpc(nameof(ReceiveReadySnapshot), BuildReadySnapshot());
		NotifyStateChanged();
	}

	private void BroadcastNameSnapshot()
	{
		Rpc(nameof(ReceivePeerNameSnapshot), BuildNameSnapshot());
		NotifyStateChanged();
	}

	private void BroadcastPhaseSnapshot()
	{
		Rpc(nameof(ReceivePeerPhaseSnapshot), BuildPhaseSnapshot());
		NotifyStateChanged();
	}

	private void BroadcastDeckSnapshot()
	{
		Rpc(nameof(ReceivePeerDeckSnapshot), BuildDeckSnapshot());
		NotifyStateChanged();
	}

	private void BroadcastTelemetrySnapshot()
	{
		Rpc(nameof(ReceivePeerTelemetrySnapshot), BuildTelemetrySnapshot());
		NotifyStateChanged();
	}

	private void ResetAllReadyStates()
	{
		var peerIds = _readyPeers.Keys.ToArray();
		_readyPeers.Clear();
		foreach (var peerId in peerIds)
		{
			_readyPeers[peerId] = false;
		}

		if (HasRoom && !_readyPeers.ContainsKey(Multiplayer.GetUniqueId()))
		{
			_readyPeers[Multiplayer.GetUniqueId()] = false;
		}
	}

	private void ResetRoomRaceProgress(bool resetReadyStates)
	{
		_submissions.Clear();
		_peerTelemetry.Clear();
		ResetLoadedStates();
		ScoreboardSummary = BuildWaitingScoreboardText();
		SessionStandingsSummary = BuildSessionStandingsText();
		SetAllPeerPhases(PeerPhaseLobby, preserveSpectators: true);
		RaceCombatReleased = false;
		_raceCountdownRemainingSeconds = 0f;
		if (resetReadyStates)
		{
			ResetAllReadyStates();
		}
	}

	private void ResetLoadedStates()
	{
		var peerIds = GetKnownPeerIds();
		_loadedPeers.Clear();
		foreach (var peerId in peerIds)
		{
			_loadedPeers[peerId] = false;
		}

		if (HasRoom && !_loadedPeers.ContainsKey(Multiplayer.GetUniqueId()))
		{
			_loadedPeers[Multiplayer.GetUniqueId()] = false;
		}
	}

	private string BuildReadySnapshot()
	{
		return string.Join(
			";",
			_readyPeers
				.OrderBy(pair => pair.Key)
				.Select(pair => $"{pair.Key}:{(pair.Value ? 1 : 0)}"));
	}

	private string BuildNameSnapshot()
	{
		return string.Join(
			";",
			_peerNames
				.OrderBy(pair => pair.Key)
				.Select(pair => $"{pair.Key}:{pair.Value}"));
	}

	private string BuildPhaseSnapshot()
	{
		return string.Join(
			";",
			_peerPhases
				.OrderBy(pair => pair.Key)
				.Select(pair => $"{pair.Key}:{pair.Value}"));
	}

	private string BuildDeckSnapshot()
	{
		return string.Join(
			";",
			_peerDecks
				.OrderBy(pair => pair.Key)
				.Select(pair => $"{pair.Key}:{string.Join(",", pair.Value ?? Array.Empty<string>())}"));
	}

	private string BuildTelemetrySnapshot()
	{
		return string.Join(
			";",
			_peerTelemetry
				.OrderBy(pair => pair.Key)
				.Select(pair =>
					$"{pair.Key}:{pair.Value.ElapsedDeciseconds}:{pair.Value.EnemyDefeats}:{pair.Value.HullPercent}"));
	}

	private void SetAllPeerPhases(string phase, bool preserveSpectators = false)
	{
		var peerIds = GetKnownPeerIds();
		var spectatorPeerIds = preserveSpectators
			? peerIds.Where(peerId => ResolvePeerPhase(peerId) == PeerPhaseSpectating).ToHashSet()
			: null;
		_peerPhases.Clear();
		foreach (var peerId in peerIds)
		{
			_peerPhases[peerId] = spectatorPeerIds != null && spectatorPeerIds.Contains(peerId)
				? PeerPhaseSpectating
				: phase;
		}

		if (HasRoom && !_peerPhases.ContainsKey(Multiplayer.GetUniqueId()))
		{
			_peerPhases[Multiplayer.GetUniqueId()] = phase;
		}
	}

	private string ResolvePeerPhase(int peerId)
	{
		return _peerPhases.TryGetValue(peerId, out var phase) &&
			!string.IsNullOrWhiteSpace(phase)
			? phase
			: PeerPhaseLobby;
	}

	private string ResolvePeerPresenceText(int peerId)
	{
		var telemetryText = BuildPeerTelemetryText(peerId);
		return ResolvePeerPhase(peerId) switch
		{
			PeerPhaseSpectating => "spectating current race",
			PeerPhaseLoading => ResolvePeerLoaded(peerId)
				? "loaded  |  waiting for countdown"
				: "loading battle",
			PeerPhaseRacing => string.IsNullOrWhiteSpace(telemetryText)
				? "in battle"
				: $"in battle  |  {telemetryText}",
			PeerPhaseSubmitted => string.IsNullOrWhiteSpace(telemetryText)
				? "result submitted"
				: $"result submitted  |  {telemetryText}",
			_ => _readyPeers.TryGetValue(peerId, out var ready) && ready ? "ready" : "not ready"
		};
	}

	private string BuildPeerTelemetryText(int peerId)
	{
		if (!_peerTelemetry.TryGetValue(peerId, out var telemetry))
		{
			return "";
		}

		return $"{telemetry.ElapsedDeciseconds / 10f:0.0}s  |  Hull {telemetry.HullPercent}%  |  Defeats {telemetry.EnemyDefeats}";
	}

	private int ResolvePeerMonitorRank(int peerId)
	{
		return ResolvePeerPhase(peerId) switch
		{
			PeerPhaseSubmitted => 0,
			PeerPhaseRacing => 1,
			PeerPhaseLoading => 2,
			PeerPhaseSpectating => 3,
			_ => 4
		};
	}

	private string BuildPeerMonitorLine(int peerId)
	{
		var phase = ResolvePeerPhase(peerId);
		if (phase == PeerPhaseSubmitted && _submissions.TryGetValue(peerId, out var submission))
		{
			var outcome = submission.Disconnected
				? "DC"
				: submission.Retreated
				? "RET"
				: submission.Won
					? "WIN"
					: "FAIL";
			return
				$"{submission.DisplayName}  |  {outcome}  |  {submission.Score} pts  |  {submission.ElapsedSeconds:0.0}s  |  Hull {submission.HullPercent}%  |  Defeats {submission.EnemyDefeats}";
		}

		if (phase == PeerPhaseRacing)
		{
			var telemetryText = BuildPeerTelemetryText(peerId);
			return string.IsNullOrWhiteSpace(telemetryText)
				? $"{ResolvePeerLabel(peerId)}  |  LIVE  |  telemetry pending"
				: $"{ResolvePeerLabel(peerId)}  |  LIVE  |  {telemetryText}";
		}

		if (phase == PeerPhaseLoading)
		{
			return ResolvePeerLoaded(peerId)
				? $"{ResolvePeerLabel(peerId)}  |  LOADED  |  waiting for shared countdown"
				: $"{ResolvePeerLabel(peerId)}  |  LOADING  |  entering battle scene";
		}

		if (phase == PeerPhaseSpectating)
		{
			return $"{ResolvePeerLabel(peerId)}  |  SPECTATE  |  waiting for next rematch";
		}

		var deckState = HasSharedLockedDeck()
			? "locked board"
			: ResolvePeerDeckIsFull(peerId)
				? "deck synced"
				: "deck incomplete";
		return $"{ResolvePeerLabel(peerId)}  |  {(LocalReadyState(peerId) ? "READY" : "WAIT")}  |  {deckState}";
	}

	private bool LocalReadyState(int peerId)
	{
		return _readyPeers.TryGetValue(peerId, out var ready) && ready;
	}

	private string[] BuildLocalDeckProfile()
	{
		return GameState.Instance.ActiveDeckUnitIds
			.Where(unitId => !string.IsNullOrWhiteSpace(unitId))
			.Take(GameState.Instance.DeckSizeLimit)
			.Select(unitId => unitId.Trim())
			.ToArray();
	}

	private bool HasSharedLockedDeck()
	{
		return _sharedLockedDeckUnitIds.Count >= GameState.Instance.DeckSizeLimit;
	}

	private string BuildPeerDeckSummaryLine(int peerId)
	{
		if (!_peerDecks.TryGetValue(peerId, out var deckIds) || deckIds == null || deckIds.Length == 0)
		{
			return $"{ResolvePeerLabel(peerId)}: deck sync pending";
		}

		var deckUnits = GameData.GetUnitsByIds(deckIds);
		if (deckUnits.Count == 0)
		{
			return $"{ResolvePeerLabel(peerId)}: deck sync pending";
		}

		var deckNames = string.Join(", ", deckUnits.Select(unit => unit.DisplayName));
		if (deckUnits.Count < GameState.Instance.DeckSizeLimit)
		{
			return $"{ResolvePeerLabel(peerId)}: {deckNames}  |  incomplete deck ({deckUnits.Count}/{GameState.Instance.DeckSizeLimit})";
		}

		var synergy = GameState.Instance.BuildDeckSynergyInlineSummary(deckUnits);
		return $"{ResolvePeerLabel(peerId)}: {deckNames}  |  {synergy}";
	}

	private int[] GetKnownPeerIds()
	{
		return _readyPeers.Keys
			.Concat(_submissions.Keys)
			.Concat(_peerPhases.Keys)
			.Concat(_peerNames.Keys)
			.Concat(_peerDecks.Keys)
			.Concat(_peerTelemetry.Keys)
			.Concat(_loadedPeers.Keys)
			.Distinct()
			.OrderBy(id => id)
			.ToArray();
	}

	private int[] GetLaunchEligiblePeerIds()
	{
		return GetKnownPeerIds()
			.Where(peerId => ResolvePeerPhase(peerId) != PeerPhaseSpectating)
			.ToArray();
	}

	private bool ResolvePeerLoaded(int peerId)
	{
		return _loadedPeers.TryGetValue(peerId, out var loaded) && loaded;
	}

	private bool IsActiveRacePeer(int peerId)
	{
		return ResolvePeerPhase(peerId) != PeerPhaseSpectating;
	}

	private bool HasInFlightRace()
	{
		return RaceCountdownActive ||
			RaceCombatReleased ||
			_submissions.Count > 0 ||
			_peerPhases.Values.Any(phase =>
				phase == PeerPhaseLoading ||
				phase == PeerPhaseRacing ||
				phase == PeerPhaseSubmitted);
	}

	private bool IsRoundLocked()
	{
		return RaceCountdownActive ||
			_peerPhases.Values.Any(phase =>
				phase == PeerPhaseLoading ||
				phase == PeerPhaseRacing);
	}

	private bool IsRoundCompleteAwaitingReset()
	{
		return _submissions.Count > 0 && !IsRoundLocked();
	}

	private bool ResolvePeerDeckIsFull(int peerId)
	{
		return _peerDecks.TryGetValue(peerId, out var deckIds) &&
			deckIds != null &&
			deckIds.Length >= GameState.Instance.DeckSizeLimit;
	}

	private void NotifyStateChanged()
	{
		EmitSignal(SignalName.StateChanged);
	}

	private void OnPeerConnected(long id)
	{
		if (!IsHosting)
		{
			return;
		}

		_readyPeers[(int)id] = false;
		_peerNames[(int)id] = $"Runner {id}";
		_peerPhases[(int)id] = HasInFlightRace() ? PeerPhaseSpectating : PeerPhaseLobby;
		_peerDecks[(int)id] = Array.Empty<string>();
		_loadedPeers[(int)id] = false;
		RpcId((int)id, nameof(ReceiveBoardState), SharedChallengeCode, SharedChallengeTitle, string.Join(",", _sharedLockedDeckUnitIds));
		RpcId((int)id, nameof(ReceiveScoreboardSummary), ScoreboardSummary);
		RpcId((int)id, nameof(ReceiveSessionStandingsSummary), SessionStandingsSummary);
		RpcId((int)id, nameof(ReceiveReadySnapshot), BuildReadySnapshot());
		RpcId((int)id, nameof(ReceivePeerNameSnapshot), BuildNameSnapshot());
		RpcId((int)id, nameof(ReceivePeerPhaseSnapshot), BuildPhaseSnapshot());
		RpcId((int)id, nameof(ReceivePeerDeckSnapshot), BuildDeckSnapshot());
		RpcId((int)id, nameof(ReceivePeerTelemetrySnapshot), BuildTelemetrySnapshot());
		SessionStatus = $"Peer {id} joined LAN room {SharedChallengeCode}.";
		NotifyStateChanged();
	}

	private void OnPeerDisconnected(long id)
	{
		var peerId = (int)id;
		var phase = ResolvePeerPhase(peerId);
		var label = ResolvePeerLabel(peerId);
		var preserveSubmission = false;
		if (IsHosting &&
			!_submissions.ContainsKey(peerId) &&
			(phase == PeerPhaseLoading || phase == PeerPhaseRacing))
		{
			_peerTelemetry.TryGetValue(peerId, out var telemetry);
			StoreSubmission(new LanRaceSubmission
			{
				PeerId = peerId,
				DisplayName = label,
				Score = 0,
				ElapsedSeconds = telemetry == null ? 0f : telemetry.ElapsedDeciseconds / 10f,
				StarsEarned = 0,
				EnemyDefeats = telemetry?.EnemyDefeats ?? 0,
				HullPercent = telemetry?.HullPercent ?? 0,
				Won = false,
				Retreated = false,
				UsedLockedDeck = HasSharedLockedDeck(),
				Disconnected = true
			});
			preserveSubmission = true;
		}

		_readyPeers.Remove(peerId);
		_peerDecks.Remove(peerId);
		_loadedPeers.Remove(peerId);
		if (!preserveSubmission && !_submissions.ContainsKey(peerId))
		{
			_peerNames.Remove(peerId);
			_peerPhases.Remove(peerId);
			_peerTelemetry.Remove(peerId);
		}
		if (IsHosting)
		{
			ScoreboardSummary = BuildScoreboardText();
			SessionStandingsSummary = BuildSessionStandingsText();
			BroadcastReadySnapshot();
			BroadcastNameSnapshot();
			BroadcastPhaseSnapshot();
			BroadcastDeckSnapshot();
			BroadcastTelemetrySnapshot();
			BroadcastSessionStandings();
			TryStartRaceCountdown();
		}

		SessionStatus = preserveSubmission
			? $"{label} disconnected during LAN race."
			: $"Peer {id} left the LAN room.";
		NotifyStateChanged();
	}

	private void OnConnectedToServer()
	{
		_readyPeers.Clear();
		_readyPeers[Multiplayer.GetUniqueId()] = false;
		_peerNames.Clear();
		_peerNames[Multiplayer.GetUniqueId()] = GameState.Instance.PlayerCallsign;
		_peerPhases.Clear();
		_peerPhases[Multiplayer.GetUniqueId()] = PeerPhaseLobby;
		_peerDecks.Clear();
		_peerDecks[Multiplayer.GetUniqueId()] = BuildLocalDeckProfile();
		_peerTelemetry.Clear();
		_loadedPeers.Clear();
		_loadedPeers[Multiplayer.GetUniqueId()] = false;
		SessionStatus = "Connected to host. Waiting for shared challenge board.";
		RpcId(1, nameof(SetRemotePeerProfile), GameState.Instance.PlayerCallsign);
		RpcId(1, nameof(SetRemotePeerDeckProfile), string.Join(",", BuildLocalDeckProfile()));
		NotifyStateChanged();
	}

	private void OnConnectionFailed()
	{
		CloseRoomInternal();
		SessionStatus = "LAN join failed. Verify the host IP and that the port is reachable.";
		NotifyStateChanged();
	}

	private void OnServerDisconnected()
	{
		CloseRoomInternal();
		SessionStatus = "Host disconnected. LAN room closed.";
		NotifyStateChanged();
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ReceiveBoardState(string challengeCode, string boardTitle, string lockedDeckCsv)
	{
		var lockedDeckIds = string.IsNullOrWhiteSpace(lockedDeckCsv)
			? Array.Empty<string>()
			: lockedDeckCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		ApplySharedBoard(challengeCode, boardTitle, lockedDeckIds);
		if (GameState.Instance.TrySetSelectedAsyncChallengeBoard(SharedChallengeCode, lockedDeckIds, out var message))
		{
			SessionStatus = message;
		}
		else
		{
			SessionStatus = $"Received LAN board {SharedChallengeCode}.";
		}

		ScoreboardSummary = BuildWaitingScoreboardText();
		_readyPeers[Multiplayer.GetUniqueId()] = false;
		_peerPhases[Multiplayer.GetUniqueId()] = PeerPhaseLobby;
		_peerTelemetry.Clear();
		_loadedPeers[Multiplayer.GetUniqueId()] = false;

		NotifyStateChanged();
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void BeginLanRace(string challengeCode, string lockedDeckCsv)
	{
		var lockedDeckIds = string.IsNullOrWhiteSpace(lockedDeckCsv)
			? Array.Empty<string>()
			: lockedDeckCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		if (!GameState.Instance.TrySetSelectedAsyncChallengeBoard(challengeCode, lockedDeckIds, out var message) ||
			!GameState.Instance.PrepareAsyncChallenge(challengeCode, out message))
		{
			SessionStatus = $"LAN launch failed: {message}";
			NotifyStateChanged();
			return;
		}

		_peerPhases[Multiplayer.GetUniqueId()] = PeerPhaseLoading;
		_peerTelemetry[Multiplayer.GetUniqueId()] = BuildTelemetry(Multiplayer.GetUniqueId(), 0f, 0, 100);
		SessionStatus = $"LAN race deployed on {challengeCode}.";
		NotifyStateChanged();
		if (SceneRouter.Instance != null)
		{
			SceneRouter.Instance.GoToBattle();
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ReceiveRaceSubmission(
		int score,
		float elapsedSeconds,
		int starsEarned,
		int enemyDefeats,
		int hullPercent,
		bool won,
		bool retreated,
		bool usedLockedDeck)
	{
		if (!IsHosting)
		{
			return;
		}

		var senderId = Multiplayer.GetRemoteSenderId();
		StoreSubmission(new LanRaceSubmission
		{
			PeerId = senderId,
			Score = score,
			ElapsedSeconds = elapsedSeconds,
			StarsEarned = starsEarned,
			EnemyDefeats = enemyDefeats,
			HullPercent = hullPercent,
			Won = won,
			Retreated = retreated,
			UsedLockedDeck = usedLockedDeck
		});
		StoreTelemetry(BuildTelemetry(senderId, elapsedSeconds, enemyDefeats, hullPercent));
		_peerPhases[senderId] = PeerPhaseSubmitted;
		BroadcastScoreboard();
		BroadcastSessionStandings();
		BroadcastPhaseSnapshot();
		BroadcastTelemetrySnapshot();
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void SetRemoteReadyState(bool ready)
	{
		if (!IsHosting)
		{
			return;
		}

		var senderId = Multiplayer.GetRemoteSenderId();
		var senderPhase = ResolvePeerPhase(senderId);
		if (ready &&
			(senderPhase == PeerPhaseSpectating ||
			(senderPhase == PeerPhaseSubmitted && IsRoundCompleteAwaitingReset())) &&
			IsRoundCompleteAwaitingReset())
		{
			_peerPhases[senderId] = PeerPhaseLobby;
			BroadcastPhaseSnapshot();
		}

		_readyPeers[senderId] = ready;
		SessionStatus = $"{ResolvePeerLabel(senderId)} {(ready ? "is ready." : "cleared ready state.")}";
		BroadcastReadySnapshot();
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void SetRemotePeerPhase(string phase)
	{
		if (!IsHosting)
		{
			return;
		}

		var senderId = Multiplayer.GetRemoteSenderId();
		_peerPhases[senderId] = string.IsNullOrWhiteSpace(phase)
			? PeerPhaseLobby
			: phase.Trim();
		BroadcastPhaseSnapshot();
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ReceiveReadySnapshot(string snapshot)
	{
		_readyPeers.Clear();
		if (!string.IsNullOrWhiteSpace(snapshot))
		{
			var entries = snapshot.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			foreach (var entry in entries)
			{
				var parts = entry.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
				if (parts.Length != 2 ||
					!int.TryParse(parts[0], out var peerId) ||
					!int.TryParse(parts[1], out var readyInt))
				{
					continue;
				}

				_readyPeers[peerId] = readyInt > 0;
			}
		}

		if (HasRoom && !_readyPeers.ContainsKey(Multiplayer.GetUniqueId()))
		{
			_readyPeers[Multiplayer.GetUniqueId()] = false;
		}

		NotifyStateChanged();
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void SetRemotePeerProfile(string callsign)
	{
		if (!IsHosting)
		{
			return;
		}

		var senderId = Multiplayer.GetRemoteSenderId();
		_peerNames[senderId] = string.IsNullOrWhiteSpace(callsign)
			? $"Runner {senderId}"
			: callsign.Trim();
		SessionStatus = $"{ResolvePeerLabel(senderId)} updated their convoy callsign.";
		BroadcastNameSnapshot();
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void SetRemotePeerDeckProfile(string deckCsv)
	{
		if (!IsHosting)
		{
			return;
		}

		var senderId = Multiplayer.GetRemoteSenderId();
		_peerDecks[senderId] = ParseDeckProfileCsv(deckCsv);
		_readyPeers[senderId] = false;
		SessionStatus = $"{ResolvePeerLabel(senderId)} updated their convoy deck. Ready cleared.";
		BroadcastDeckSnapshot();
		BroadcastReadySnapshot();
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
	private void SetRemoteRaceTelemetry(int elapsedDeciseconds, int enemyDefeats, int hullPercent)
	{
		if (!IsHosting)
		{
			return;
		}

		var senderId = Multiplayer.GetRemoteSenderId();
		StoreTelemetry(new LanRaceTelemetry
		{
			PeerId = senderId,
			ElapsedDeciseconds = Math.Max(0, elapsedDeciseconds),
			EnemyDefeats = Math.Max(0, enemyDefeats),
			HullPercent = Mathf.Clamp(hullPercent, 0, 100)
		});
		BroadcastTelemetrySnapshot();
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void SetRemoteBattleLoaded()
	{
		if (!IsHosting)
		{
			return;
		}

		var senderId = Multiplayer.GetRemoteSenderId();
		_loadedPeers[senderId] = true;
		TryStartRaceCountdown();
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void BeginRaceCountdown(float seconds)
	{
		_raceCountdownRemainingSeconds = Mathf.Max(0f, seconds);
		RaceCombatReleased = false;
		NotifyStateChanged();
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ReceivePeerNameSnapshot(string snapshot)
	{
		_peerNames.Clear();
		if (!string.IsNullOrWhiteSpace(snapshot))
		{
			var entries = snapshot.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			foreach (var entry in entries)
			{
				var parts = entry.Split(':', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
				if (parts.Length != 2 || !int.TryParse(parts[0], out var peerId))
				{
					continue;
				}

				_peerNames[peerId] = parts[1];
			}
		}

		if (HasRoom && !_peerNames.ContainsKey(Multiplayer.GetUniqueId()))
		{
			_peerNames[Multiplayer.GetUniqueId()] = GameState.Instance.PlayerCallsign;
		}

		NotifyStateChanged();
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ReceivePeerPhaseSnapshot(string snapshot)
	{
		_peerPhases.Clear();
		if (!string.IsNullOrWhiteSpace(snapshot))
		{
			var entries = snapshot.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			foreach (var entry in entries)
			{
				var parts = entry.Split(':', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
				if (parts.Length != 2 || !int.TryParse(parts[0], out var peerId))
				{
					continue;
				}

				_peerPhases[peerId] = parts[1];
			}
		}

		if (HasRoom && !_peerPhases.ContainsKey(Multiplayer.GetUniqueId()))
		{
			_peerPhases[Multiplayer.GetUniqueId()] = PeerPhaseLobby;
		}

		NotifyStateChanged();
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ReceivePeerDeckSnapshot(string snapshot)
	{
		_peerDecks.Clear();
		if (!string.IsNullOrWhiteSpace(snapshot))
		{
			var entries = snapshot.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			foreach (var entry in entries)
			{
				var parts = entry.Split(':', 2, StringSplitOptions.TrimEntries);
				if (parts.Length != 2 || !int.TryParse(parts[0], out var peerId))
				{
					continue;
				}

				_peerDecks[peerId] = ParseDeckProfileCsv(parts[1]);
			}
		}

		if (HasRoom && !_peerDecks.ContainsKey(Multiplayer.GetUniqueId()))
		{
			_peerDecks[Multiplayer.GetUniqueId()] = BuildLocalDeckProfile();
		}

		NotifyStateChanged();
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
	private void ReceivePeerTelemetrySnapshot(string snapshot)
	{
		_peerTelemetry.Clear();
		if (!string.IsNullOrWhiteSpace(snapshot))
		{
			var entries = snapshot.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			foreach (var entry in entries)
			{
				var parts = entry.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
				if (parts.Length != 4 ||
					!int.TryParse(parts[0], out var peerId) ||
					!int.TryParse(parts[1], out var elapsedDeciseconds) ||
					!int.TryParse(parts[2], out var enemyDefeats) ||
					!int.TryParse(parts[3], out var hullPercent))
				{
					continue;
				}

				_peerTelemetry[peerId] = new LanRaceTelemetry
				{
					PeerId = peerId,
					ElapsedDeciseconds = Math.Max(0, elapsedDeciseconds),
					EnemyDefeats = Math.Max(0, enemyDefeats),
					HullPercent = Mathf.Clamp(hullPercent, 0, 100)
				};
			}
		}

		NotifyStateChanged();
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ReceiveScoreboardSummary(string summary)
	{
		ScoreboardSummary = string.IsNullOrWhiteSpace(summary)
			? "LAN scoreboard: no race submissions yet."
			: summary;
		NotifyStateChanged();
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ReceiveSessionStandingsSummary(string summary)
	{
		SessionStandingsSummary = string.IsNullOrWhiteSpace(summary)
			? "LAN session standings: no completed LAN races yet."
			: summary;
		NotifyStateChanged();
	}

	private string[] ParseDeckProfileCsv(string deckCsv)
	{
		if (string.IsNullOrWhiteSpace(deckCsv))
		{
			return Array.Empty<string>();
		}

		return deckCsv
			.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Where(unitId => !string.IsNullOrWhiteSpace(unitId))
			.Take(GameState.Instance.DeckSizeLimit)
			.ToArray();
	}
}
