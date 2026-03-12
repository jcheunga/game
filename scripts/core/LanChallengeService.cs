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
		public int Score { get; init; }
		public float ElapsedSeconds { get; init; }
		public int StarsEarned { get; init; }
		public int EnemyDefeats { get; init; }
		public int HullPercent { get; init; }
		public bool Won { get; init; }
		public bool Retreated { get; init; }
		public bool UsedLockedDeck { get; init; }
	}

	public static LanChallengeService Instance { get; private set; }

	public const int DefaultPort = 24680;

	public bool HasRoom => Multiplayer.MultiplayerPeer != null;
	public bool IsHosting => HasRoom && Multiplayer.IsServer();
	public bool IsClient => HasRoom && !Multiplayer.IsServer();
	public string SharedChallengeCode { get; private set; } = "";
	public string SharedChallengeTitle { get; private set; } = "";
	public string SessionStatus { get; private set; } = "No LAN race room active.";
	public string ScoreboardSummary { get; private set; } = "LAN scoreboard: no race submissions yet.";
	public IReadOnlyList<string> SharedLockedDeckUnitIds => _sharedLockedDeckUnitIds;

	private readonly List<string> _sharedLockedDeckUnitIds = new();
	private readonly Dictionary<int, LanRaceSubmission> _submissions = new();

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

	public bool HostSelectedBoard(out string message)
	{
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
		var error = peer.CreateServer(DefaultPort, 3);
		if (error != Error.Ok)
		{
			message = $"Could not host LAN race on port {DefaultPort}: {error}.";
			SessionStatus = message;
			NotifyStateChanged();
			return false;
		}

		Multiplayer.MultiplayerPeer = peer;
		ApplySharedBoard(challenge.Code, boardTitle, lockedDeckIds);
		_submissions.Clear();
		ScoreboardSummary = "LAN scoreboard: waiting for race submissions.";
		SessionStatus = $"Hosting {challenge.Code} on port {DefaultPort}. Share IP {BuildJoinAddressSummary()}.";
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
		Rpc(nameof(ReceiveBoardState), SharedChallengeCode, SharedChallengeTitle, string.Join(",", _sharedLockedDeckUnitIds));
		SessionStatus = $"Broadcast updated LAN board {SharedChallengeCode}.";
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
		var error = peer.CreateClient(normalizedAddress, DefaultPort);
		if (error != Error.Ok)
		{
			message = $"Could not join {normalizedAddress}:{DefaultPort}: {error}.";
			SessionStatus = message;
			NotifyStateChanged();
			return false;
		}

		Multiplayer.MultiplayerPeer = peer;
		_submissions.Clear();
		ScoreboardSummary = "LAN scoreboard: waiting for host data.";
		SessionStatus = $"Connecting to {normalizedAddress}:{DefaultPort}...";
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

		if (string.IsNullOrWhiteSpace(SharedChallengeCode))
		{
			message = "No shared LAN board is armed.";
			return false;
		}

		Rpc(nameof(BeginLanRace), SharedChallengeCode, string.Join(",", _sharedLockedDeckUnitIds));
		SessionStatus = $"LAN race launched on {SharedChallengeCode}.";
		message = SessionStatus;
		NotifyStateChanged();
		return true;
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
			BroadcastScoreboard();
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
		SessionStatus = $"Submitted LAN result for {challenge.Code}. Waiting for host scoreboard.";
		NotifyStateChanged();
	}

	public string BuildRoomSummary()
	{
		if (!HasRoom)
		{
			var challenge = GameState.Instance.GetSelectedAsyncChallenge();
			var mode = GameState.Instance.HasSelectedAsyncChallengeLockedDeck ? "locked squad" : "player squad";
			return
				"No LAN room active.\n" +
				$"Selected board: {challenge.Code}\n" +
				$"Board deck mode: {mode}\n" +
				"Host a room to broadcast the current board, or join a host IP to sync it.";
		}

		var peerCount = Multiplayer.GetPeers().Length + 1;
		var role = IsHosting ? "Host" : "Runner";
		var deckMode = _sharedLockedDeckUnitIds.Count >= GameState.Instance.DeckSizeLimit
			? $"Locked squad: {string.Join(", ", _sharedLockedDeckUnitIds.Select(unitId => GameData.GetUnit(unitId).DisplayName))}"
			: "Uses each player's current async squad.";
		var addressSummary = IsHosting ? $"\nShare IP: {BuildJoinAddressSummary()}" : "";
		return
			$"{role} room active  |  Peers: {peerCount}\n" +
			$"Board: {SharedChallengeCode}\n" +
			$"{SharedChallengeTitle}\n" +
			$"{deckMode}{addressSummary}";
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

	private void StoreSubmission(LanRaceSubmission submission)
	{
		_submissions[submission.PeerId] = submission;
		ScoreboardSummary = BuildScoreboardText();
		SessionStatus = $"{ResolvePeerLabel(submission.PeerId)} posted {submission.Score} pts on {SharedChallengeCode}.";
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
			var outcome = entry.Retreated
				? "RET"
				: entry.Won
					? "WIN"
					: "FAIL";
			lines.Add(
				$"{i + 1}. {ResolvePeerLabel(entry.PeerId)}  |  {outcome}  |  {entry.Score} pts  |  {entry.ElapsedSeconds:0.0}s  |  Hull {entry.HullPercent}%  |  Stars {entry.StarsEarned}/3  |  Defeats {entry.EnemyDefeats}  |  {(entry.UsedLockedDeck ? "locked" : "player")} deck");
		}

		return string.Join("\n", lines);
	}

	private string ResolvePeerLabel(int peerId)
	{
		return peerId == 1 ? "Host" : $"Runner {peerId}";
	}

	private void BroadcastScoreboard()
	{
		Rpc(nameof(ReceiveScoreboardSummary), ScoreboardSummary);
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
		ScoreboardSummary = "LAN scoreboard: no race submissions yet.";
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

		RpcId((int)id, nameof(ReceiveBoardState), SharedChallengeCode, SharedChallengeTitle, string.Join(",", _sharedLockedDeckUnitIds));
		RpcId((int)id, nameof(ReceiveScoreboardSummary), ScoreboardSummary);
		SessionStatus = $"Peer {id} joined LAN room {SharedChallengeCode}.";
		NotifyStateChanged();
	}

	private void OnPeerDisconnected(long id)
	{
		_submissions.Remove((int)id);
		if (IsHosting)
		{
			ScoreboardSummary = BuildScoreboardText();
		}

		SessionStatus = $"Peer {id} left the LAN room.";
		NotifyStateChanged();
	}

	private void OnConnectedToServer()
	{
		SessionStatus = "Connected to host. Waiting for shared challenge board.";
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
		BroadcastScoreboard();
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ReceiveScoreboardSummary(string summary)
	{
		ScoreboardSummary = string.IsNullOrWhiteSpace(summary)
			? "LAN scoreboard: no race submissions yet."
			: summary;
		NotifyStateChanged();
	}
}
