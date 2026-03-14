using System;
using System.Collections.Generic;
using System.Linq;

public sealed class LocalOnlineRoomSessionProvider : IOnlineRoomSessionProvider
{
	public string Id => ChallengeSyncProviderCatalog.LocalJournalId;
	public string DisplayName => "Local Session Stub";

	public string BuildLocationSummary()
	{
		return "Source: generated local session snapshot";
	}

	public OnlineRoomSessionSnapshot FetchRoomSession(OnlineRoomJoinTicket ticket)
	{
		var localCallsign = GameState.Instance?.PlayerCallsign ?? "Convoy";
		var boardTitle = BuildBoardTitle(ticket.BoardCode);
		var roomId = string.IsNullOrWhiteSpace(ticket.RoomId) ? ticket.BoardCode : ticket.RoomId;
		var localSeatStatus = ticket.Status?.Trim().ToLowerInvariant() ?? "";
		var localSeatActive = localSeatStatus != "spectate" && localSeatStatus != "waitlist";
		var localSeatIsHost = localSeatStatus == "hosted" ||
			(!string.IsNullOrWhiteSpace(ticket.SeatLabel) && ticket.SeatLabel.IndexOf("host", StringComparison.OrdinalIgnoreCase) >= 0);
		var localReady = localSeatActive && (OnlineRoomActionService.GetReadyStateHint() ?? false);
		var roundLaunched = LocalOnlineRoomStubState.IsRoundLaunched(roomId);
		var roundComplete = LocalOnlineRoomStubState.IsRoundComplete(roomId);
		var submittedCallsigns = LocalOnlineRoomStubState.GetSubmittedCallsigns(roomId);
		var telemetrySnapshots = LocalOnlineRoomStubState.GetTelemetrySnapshots(roomId);
		var rankedEntries = LocalOnlineRoomResultProvider.GetRankedEntries(roomId, 8);
		var raceLive = roundLaunched && telemetrySnapshots.Count > 0 && !roundComplete;
		var localTelemetry = telemetrySnapshots.FirstOrDefault(snapshot =>
			snapshot.PlayerCallsign.Equals(localCallsign, StringComparison.OrdinalIgnoreCase));
		var localPresence = !localSeatActive
			? "spectating via join ticket"
			: roundComplete && submittedCallsigns.Contains(localCallsign, StringComparer.OrdinalIgnoreCase)
				? BuildSubmittedPresenceText(localCallsign, rankedEntries)
			: raceLive && localTelemetry != null
				? $"racing live: {localTelemetry.ElapsedDeciseconds / 10f:0.0}s, hull {localTelemetry.HullPercent}%"
			: roundComplete
				? "round complete, standing by"
			: roundLaunched
				? "loading into battle"
			: localSeatIsHost
				? localReady
					? "hosting, ready to launch"
					: "hosting, waiting on ready"
			: localReady
				? "joined and ready"
				: "joined, standing by";
		var localMonitor = !localSeatActive
			? $"{localCallsign}  |  spectating  |  {ticket.SeatLabel}"
			: roundComplete && submittedCallsigns.Contains(localCallsign, StringComparer.OrdinalIgnoreCase)
				? BuildSubmittedMonitorText(localCallsign, rankedEntries)
			: raceLive && localTelemetry != null
				? $"{localCallsign}  |  racing  |  {localTelemetry.ElapsedDeciseconds / 10f:0.0}s  |  Hull {localTelemetry.HullPercent}%  |  Defeats {localTelemetry.EnemyDefeats}"
			: roundComplete
				? $"{localCallsign}  |  prep  |  rematch standby"
			: roundLaunched
				? $"{localCallsign}  |  loading  |  round launch"
			: localSeatIsHost
				? $"{localCallsign}  |  prep  |  {(localReady ? "ready" : "host")}  |  host"
			: $"{localCallsign}  |  prep  |  {(localReady ? "ready" : "not ready")}";
		var peerSnapshots = localSeatIsHost
			? BuildHostPeerSnapshots(ticket, localCallsign, localSeatActive, localReady, localPresence, localMonitor, roundLaunched, raceLive, roundComplete, submittedCallsigns, telemetrySnapshots, rankedEntries)
			: BuildJoinedPeerSnapshots(ticket, localCallsign, localSeatActive, localReady, localPresence, localMonitor, roundLaunched, raceLive, roundComplete, submittedCallsigns, telemetrySnapshots, rankedEntries);

		return new OnlineRoomSessionSnapshot
		{
			ProviderId = Id,
			ProviderDisplayName = DisplayName,
			Status = "ok",
			Summary = $"Generated local session snapshot for {ticket.RoomTitle}.",
			FetchedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
			RoomSnapshot = new MultiplayerRoomSnapshot
			{
				HasRoom = true,
				RoomId = roomId,
				RoomTitle = string.IsNullOrWhiteSpace(ticket.RoomTitle) ? boardTitle : ticket.RoomTitle,
				TransportLabel = "Internet Relay",
				RoleLabel = localSeatStatus == "spectate"
					? "Online spectator"
					: localSeatIsHost
						? "Online host"
						: "Online contender",
				PeerCount = peerSnapshots.Count,
				SharedChallengeCode = ticket.BoardCode,
				SharedChallengeTitle = boardTitle,
				LocalCallsign = localCallsign,
				DeckModeSummary = ticket.UsesLockedDeck
					? "Deck mode: locked shared squad negotiated by join ticket."
					: "Deck mode: player convoy seats negotiated by join ticket.",
				JoinAddressSummary = ticket.RelayEndpoint,
				UsesLockedDeck = ticket.UsesLockedDeck,
				RoundLocked = roundLaunched && !roundComplete,
				RoundComplete = roundComplete,
				RaceCountdownActive = roundLaunched && !roundComplete && !raceLive,
				RaceCountdownRemainingSeconds = roundLaunched && !roundComplete && !raceLive ? 4f : 0f,
				SelectedBoardCode = ticket.BoardCode,
				SelectedBoardDeckMode = ticket.UsesLockedDeck ? "locked shared squad" : "player convoy",
				Peers = peerSnapshots
			}
		};
	}

	private static List<MultiplayerRoomPeerSnapshot> BuildHostPeerSnapshots(
		OnlineRoomJoinTicket ticket,
		string localCallsign,
		bool localSeatActive,
		bool localReady,
		string localPresence,
		string localMonitor,
		bool roundLaunched,
		bool raceLive,
		bool roundComplete,
		IReadOnlyList<string> submittedCallsigns,
		IReadOnlyList<LocalOnlineRoomStubState.TelemetrySnapshot> telemetrySnapshots,
		IReadOnlyList<OnlineRoomScoreboardEntry> rankedEntries)
	{
		return
		[
			new MultiplayerRoomPeerSnapshot
			{
				PeerId = 1,
				Label = localCallsign,
				IsLocalPlayer = true,
				Phase = ResolvePhase(localCallsign, localSeatActive, roundLaunched, raceLive, roundComplete, submittedCallsigns),
				IsReady = !roundComplete && localReady,
				IsLoaded = (roundLaunched || raceLive) && localSeatActive,
				IsLaunchEligible = localSeatActive,
				HasFullDeck = true,
				MonitorRank = 1,
				RaceElapsedSeconds = FindTelemetrySeconds(localCallsign, telemetrySnapshots),
				HullPercent = FindTelemetryHullPercent(localCallsign, telemetrySnapshots),
				EnemyDefeats = FindTelemetryEnemyDefeats(localCallsign, telemetrySnapshots),
				PostedScore = FindSubmittedScore(localCallsign, rankedEntries),
				PostedRank = FindSubmittedRank(localCallsign, rankedEntries),
				PresenceText = localPresence,
				MonitorText = localMonitor,
				DeckText = ticket.UsesLockedDeck
					? $"{localCallsign}  |  locked squad"
					: $"{localCallsign}  |  player convoy"
			},
			new MultiplayerRoomPeerSnapshot
			{
				PeerId = 2,
				Label = "IronBell",
				IsLocalPlayer = false,
				Phase = ResolvePhase("IronBell", true, roundLaunched, raceLive, roundComplete, submittedCallsigns),
				IsReady = !roundComplete,
				IsLoaded = roundLaunched || raceLive,
				IsLaunchEligible = true,
				HasFullDeck = true,
				MonitorRank = 2,
				RaceElapsedSeconds = FindTelemetrySeconds("IronBell", telemetrySnapshots),
				HullPercent = FindTelemetryHullPercent("IronBell", telemetrySnapshots),
				EnemyDefeats = FindTelemetryEnemyDefeats("IronBell", telemetrySnapshots),
				PostedScore = FindSubmittedScore("IronBell", rankedEntries),
				PostedRank = FindSubmittedRank("IronBell", rankedEntries),
				PresenceText = ResolveRemotePresenceText("IronBell", ticket, roundLaunched, raceLive, roundComplete, submittedCallsigns, telemetrySnapshots, rankedEntries, true),
				MonitorText = ResolveRemoteMonitorText("IronBell", ticket, roundLaunched, raceLive, roundComplete, submittedCallsigns, telemetrySnapshots, rankedEntries, true),
				DeckText = ticket.UsesLockedDeck
					? "IronBell  |  locked squad"
					: "IronBell  |  Brawler, Shooter, Defender"
			},
			new MultiplayerRoomPeerSnapshot
			{
				PeerId = 3,
				Label = "Northgate",
				IsLocalPlayer = false,
				Phase = ResolvePhase("Northgate", true, roundLaunched, raceLive, roundComplete, submittedCallsigns),
				IsReady = !roundComplete,
				IsLoaded = roundLaunched || raceLive,
				IsLaunchEligible = true,
				HasFullDeck = true,
				MonitorRank = 3,
				RaceElapsedSeconds = FindTelemetrySeconds("Northgate", telemetrySnapshots),
				HullPercent = FindTelemetryHullPercent("Northgate", telemetrySnapshots),
				EnemyDefeats = FindTelemetryEnemyDefeats("Northgate", telemetrySnapshots),
				PostedScore = FindSubmittedScore("Northgate", rankedEntries),
				PostedRank = FindSubmittedRank("Northgate", rankedEntries),
				PresenceText = ResolveRemotePresenceText("Northgate", ticket, roundLaunched, raceLive, roundComplete, submittedCallsigns, telemetrySnapshots, rankedEntries, false),
				MonitorText = ResolveRemoteMonitorText("Northgate", ticket, roundLaunched, raceLive, roundComplete, submittedCallsigns, telemetrySnapshots, rankedEntries, false),
				DeckText = ticket.UsesLockedDeck
					? "Northgate  |  locked squad"
					: "Northgate  |  Raider, Ranger, Defender"
			}
		];
	}

	private static List<MultiplayerRoomPeerSnapshot> BuildJoinedPeerSnapshots(
		OnlineRoomJoinTicket ticket,
		string localCallsign,
		bool localSeatActive,
		bool localReady,
		string localPresence,
		string localMonitor,
		bool roundLaunched,
		bool raceLive,
		bool roundComplete,
		IReadOnlyList<string> submittedCallsigns,
		IReadOnlyList<LocalOnlineRoomStubState.TelemetrySnapshot> telemetrySnapshots,
		IReadOnlyList<OnlineRoomScoreboardEntry> rankedEntries)
	{
		return
		[
			new MultiplayerRoomPeerSnapshot
			{
				PeerId = 1,
				Label = "IronBell",
				IsLocalPlayer = false,
				Phase = ResolvePhase("IronBell", true, roundLaunched, raceLive, roundComplete, submittedCallsigns),
				IsReady = !roundComplete,
				IsLoaded = roundLaunched || raceLive,
				IsLaunchEligible = true,
				HasFullDeck = true,
				MonitorRank = 1,
				RaceElapsedSeconds = FindTelemetrySeconds("IronBell", telemetrySnapshots),
				HullPercent = FindTelemetryHullPercent("IronBell", telemetrySnapshots),
				EnemyDefeats = FindTelemetryEnemyDefeats("IronBell", telemetrySnapshots),
				PostedScore = FindSubmittedScore("IronBell", rankedEntries),
				PostedRank = FindSubmittedRank("IronBell", rankedEntries),
				PresenceText = ResolveRemotePresenceText("IronBell", ticket, roundLaunched, raceLive, roundComplete, submittedCallsigns, telemetrySnapshots, rankedEntries, true),
				MonitorText = ResolveRemoteMonitorText("IronBell", ticket, roundLaunched, raceLive, roundComplete, submittedCallsigns, telemetrySnapshots, rankedEntries, true),
				DeckText = ticket.UsesLockedDeck
					? "IronBell  |  locked squad"
					: "IronBell  |  Brawler, Shooter, Defender"
			},
			new MultiplayerRoomPeerSnapshot
			{
				PeerId = 2,
				Label = localCallsign,
				IsLocalPlayer = true,
				Phase = ResolvePhase(localCallsign, localSeatActive, roundLaunched, raceLive, roundComplete, submittedCallsigns),
				IsReady = !roundComplete && localReady,
				IsLoaded = (roundLaunched || raceLive) && localSeatActive,
				IsLaunchEligible = localSeatActive,
				HasFullDeck = true,
				MonitorRank = 2,
				RaceElapsedSeconds = FindTelemetrySeconds(localCallsign, telemetrySnapshots),
				HullPercent = FindTelemetryHullPercent(localCallsign, telemetrySnapshots),
				EnemyDefeats = FindTelemetryEnemyDefeats(localCallsign, telemetrySnapshots),
				PostedScore = FindSubmittedScore(localCallsign, rankedEntries),
				PostedRank = FindSubmittedRank(localCallsign, rankedEntries),
				PresenceText = localPresence,
				MonitorText = localMonitor,
				DeckText = ticket.UsesLockedDeck
					? $"{localCallsign}  |  locked squad"
					: $"{localCallsign}  |  player convoy"
			},
			new MultiplayerRoomPeerSnapshot
			{
				PeerId = 3,
				Label = "Northgate",
				IsLocalPlayer = false,
				Phase = ResolvePhase("Northgate", true, roundLaunched, raceLive, roundComplete, submittedCallsigns),
				IsReady = false,
				IsLoaded = roundLaunched || raceLive,
				IsLaunchEligible = true,
				HasFullDeck = true,
				MonitorRank = 3,
				RaceElapsedSeconds = FindTelemetrySeconds("Northgate", telemetrySnapshots),
				HullPercent = FindTelemetryHullPercent("Northgate", telemetrySnapshots),
				EnemyDefeats = FindTelemetryEnemyDefeats("Northgate", telemetrySnapshots),
				PostedScore = FindSubmittedScore("Northgate", rankedEntries),
				PostedRank = FindSubmittedRank("Northgate", rankedEntries),
				PresenceText = ResolveRemotePresenceText("Northgate", ticket, roundLaunched, raceLive, roundComplete, submittedCallsigns, telemetrySnapshots, rankedEntries, false),
				MonitorText = ResolveRemoteMonitorText("Northgate", ticket, roundLaunched, raceLive, roundComplete, submittedCallsigns, telemetrySnapshots, rankedEntries, false),
				DeckText = ticket.UsesLockedDeck
					? "Northgate  |  locked squad"
					: "Northgate  |  Raider, Ranger, Defender"
			}
		];
	}

	private static string ResolvePhase(
		string callsign,
		bool launchEligible,
		bool roundLaunched,
		bool raceLive,
		bool roundComplete,
		IReadOnlyList<string> submittedCallsigns)
	{
		if (!launchEligible)
		{
			return "spectating";
		}

		if (roundComplete && submittedCallsigns.Contains(callsign, StringComparer.OrdinalIgnoreCase))
		{
			return "submitted";
		}

		if (raceLive)
		{
			return "racing";
		}

		if (roundLaunched)
		{
			return "loading";
		}

		return "prep";
	}

	private static string ResolveRemotePresenceText(
		string callsign,
		OnlineRoomJoinTicket ticket,
		bool roundLaunched,
		bool raceLive,
		bool roundComplete,
		IReadOnlyList<string> submittedCallsigns,
		IReadOnlyList<LocalOnlineRoomStubState.TelemetrySnapshot> telemetrySnapshots,
		IReadOnlyList<OnlineRoomScoreboardEntry> rankedEntries,
		bool isHost)
	{
		if (roundComplete && submittedCallsigns.Contains(callsign, StringComparer.OrdinalIgnoreCase))
		{
			return BuildSubmittedPresenceText(callsign, rankedEntries);
		}

		if (raceLive)
		{
			return BuildRacingPresenceText(callsign, telemetrySnapshots);
		}

		if (roundLaunched)
		{
			return "loading into battle";
		}

		if (ticket.UsesLockedDeck)
		{
			return "ready on locked squad";
		}

		return isHost ? "joined and ready" : "joined, waiting on ready";
	}

	private static string ResolveRemoteMonitorText(
		string callsign,
		OnlineRoomJoinTicket ticket,
		bool roundLaunched,
		bool raceLive,
		bool roundComplete,
		IReadOnlyList<string> submittedCallsigns,
		IReadOnlyList<LocalOnlineRoomStubState.TelemetrySnapshot> telemetrySnapshots,
		IReadOnlyList<OnlineRoomScoreboardEntry> rankedEntries,
		bool isHost)
	{
		if (roundComplete && submittedCallsigns.Contains(callsign, StringComparer.OrdinalIgnoreCase))
		{
			return BuildSubmittedMonitorText(callsign, rankedEntries);
		}

		if (raceLive)
		{
			return BuildRacingMonitorText(callsign, telemetrySnapshots);
		}

		if (roundLaunched)
		{
			return $"{callsign}  |  loading  |  round launch";
		}

		if (ticket.UsesLockedDeck)
		{
			return isHost
				? $"{callsign}  |  prep  |  ready on locked squad"
				: $"{callsign}  |  prep  |  ready on locked squad";
		}

		return isHost
			? $"{callsign}  |  prep  |  ready"
			: $"{callsign}  |  prep  |  waiting on ready";
	}

	private static string BuildRacingPresenceText(string callsign, IReadOnlyList<LocalOnlineRoomStubState.TelemetrySnapshot> telemetrySnapshots)
	{
		var telemetry = telemetrySnapshots.FirstOrDefault(snapshot => snapshot.PlayerCallsign.Equals(callsign, StringComparison.OrdinalIgnoreCase));
		return telemetry == null
			? "racing live"
			: $"racing live: {telemetry.ElapsedDeciseconds / 10f:0.0}s, hull {telemetry.HullPercent}%";
	}

	private static string BuildRacingMonitorText(string callsign, IReadOnlyList<LocalOnlineRoomStubState.TelemetrySnapshot> telemetrySnapshots)
	{
		var telemetry = telemetrySnapshots.FirstOrDefault(snapshot => snapshot.PlayerCallsign.Equals(callsign, StringComparison.OrdinalIgnoreCase));
		return telemetry == null
			? $"{callsign}  |  racing  |  live room telemetry"
			: $"{callsign}  |  racing  |  {telemetry.ElapsedDeciseconds / 10f:0.0}s  |  Hull {telemetry.HullPercent}%  |  Defeats {telemetry.EnemyDefeats}";
	}

	private static float FindTelemetrySeconds(string callsign, IReadOnlyList<LocalOnlineRoomStubState.TelemetrySnapshot> telemetrySnapshots)
	{
		var telemetry = telemetrySnapshots.FirstOrDefault(snapshot => snapshot.PlayerCallsign.Equals(callsign, StringComparison.OrdinalIgnoreCase));
		return telemetry == null ? -1f : telemetry.ElapsedDeciseconds / 10f;
	}

	private static int FindTelemetryHullPercent(string callsign, IReadOnlyList<LocalOnlineRoomStubState.TelemetrySnapshot> telemetrySnapshots)
	{
		var telemetry = telemetrySnapshots.FirstOrDefault(snapshot => snapshot.PlayerCallsign.Equals(callsign, StringComparison.OrdinalIgnoreCase));
		return telemetry?.HullPercent ?? -1;
	}

	private static int FindTelemetryEnemyDefeats(string callsign, IReadOnlyList<LocalOnlineRoomStubState.TelemetrySnapshot> telemetrySnapshots)
	{
		var telemetry = telemetrySnapshots.FirstOrDefault(snapshot => snapshot.PlayerCallsign.Equals(callsign, StringComparison.OrdinalIgnoreCase));
		return telemetry?.EnemyDefeats ?? -1;
	}

	private static string BuildSubmittedPresenceText(string callsign, IReadOnlyList<OnlineRoomScoreboardEntry> rankedEntries)
	{
		var entry = FindSubmittedEntry(callsign, rankedEntries);
		return entry == null
			? "result submitted, awaiting rematch"
			: entry.Rank > 0
				? $"result submitted, provisional #{entry.Rank}"
				: "result submitted, awaiting rematch";
	}

	private static string BuildSubmittedMonitorText(string callsign, IReadOnlyList<OnlineRoomScoreboardEntry> rankedEntries)
	{
		var entry = FindSubmittedEntry(callsign, rankedEntries);
		return entry == null
			? $"{callsign}  |  submitted  |  awaiting rematch"
			: entry.Rank > 0
				? $"{callsign}  |  submitted  |  #{entry.Rank}  |  {entry.Score} pts"
				: $"{callsign}  |  submitted  |  {entry.Score} pts";
	}

	private static int FindSubmittedScore(string callsign, IReadOnlyList<OnlineRoomScoreboardEntry> rankedEntries)
	{
		return FindSubmittedEntry(callsign, rankedEntries)?.Score ?? -1;
	}

	private static int FindSubmittedRank(string callsign, IReadOnlyList<OnlineRoomScoreboardEntry> rankedEntries)
	{
		return FindSubmittedEntry(callsign, rankedEntries)?.Rank ?? -1;
	}

	private static OnlineRoomScoreboardEntry FindSubmittedEntry(string callsign, IReadOnlyList<OnlineRoomScoreboardEntry> rankedEntries)
	{
		return rankedEntries.FirstOrDefault(entry =>
			entry.PlayerCallsign.Equals(callsign, StringComparison.OrdinalIgnoreCase));
	}

	private static string BuildBoardTitle(string boardCode)
	{
		if (!AsyncChallengeCatalog.TryParse(boardCode, out var challenge, out _))
		{
			return boardCode;
		}

		var stage = GameData.GetStage(challenge.Stage);
		return $"{stage.MapName} S{stage.StageNumber} {stage.StageName}";
	}
}
