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
		var raceLive = roundLaunched && telemetrySnapshots.Count > 0 && !roundComplete;
		var localTelemetry = telemetrySnapshots.FirstOrDefault(snapshot =>
			snapshot.PlayerCallsign.Equals(localCallsign, StringComparison.OrdinalIgnoreCase));
		var localPresence = !localSeatActive
			? "spectating via join ticket"
			: roundComplete && submittedCallsigns.Contains(localCallsign, StringComparer.OrdinalIgnoreCase)
				? "result submitted, awaiting rematch"
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
				? $"{localCallsign}  |  submitted  |  awaiting rematch"
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
			? BuildHostPeerSnapshots(ticket, localCallsign, localSeatActive, localReady, localPresence, localMonitor, roundLaunched, raceLive, roundComplete, submittedCallsigns, telemetrySnapshots)
			: BuildJoinedPeerSnapshots(ticket, localCallsign, localSeatActive, localReady, localPresence, localMonitor, roundLaunched, raceLive, roundComplete, submittedCallsigns, telemetrySnapshots);

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
		IReadOnlyList<LocalOnlineRoomStubState.TelemetrySnapshot> telemetrySnapshots)
	{
		return
		[
			new MultiplayerRoomPeerSnapshot
			{
				PeerId = 1,
				Label = localCallsign,
				Phase = ResolvePhase(localCallsign, localSeatActive, roundLaunched, raceLive, roundComplete, submittedCallsigns),
				IsReady = !roundComplete && localReady,
				IsLoaded = (roundLaunched || raceLive) && localSeatActive,
				IsLaunchEligible = localSeatActive,
				HasFullDeck = true,
				MonitorRank = 1,
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
				Phase = ResolvePhase("IronBell", true, roundLaunched, raceLive, roundComplete, submittedCallsigns),
				IsReady = !roundComplete,
				IsLoaded = roundLaunched || raceLive,
				IsLaunchEligible = true,
				HasFullDeck = true,
				MonitorRank = 2,
				PresenceText = ResolveRemotePresenceText("IronBell", ticket, roundLaunched, raceLive, roundComplete, submittedCallsigns, telemetrySnapshots, true),
				MonitorText = ResolveRemoteMonitorText("IronBell", ticket, roundLaunched, raceLive, roundComplete, submittedCallsigns, telemetrySnapshots, true),
				DeckText = ticket.UsesLockedDeck
					? "IronBell  |  locked squad"
					: "IronBell  |  Brawler, Shooter, Defender"
			},
			new MultiplayerRoomPeerSnapshot
			{
				PeerId = 3,
				Label = "Northgate",
				Phase = ResolvePhase("Northgate", true, roundLaunched, raceLive, roundComplete, submittedCallsigns),
				IsReady = !roundComplete,
				IsLoaded = roundLaunched || raceLive,
				IsLaunchEligible = true,
				HasFullDeck = true,
				MonitorRank = 3,
				PresenceText = ResolveRemotePresenceText("Northgate", ticket, roundLaunched, raceLive, roundComplete, submittedCallsigns, telemetrySnapshots, false),
				MonitorText = ResolveRemoteMonitorText("Northgate", ticket, roundLaunched, raceLive, roundComplete, submittedCallsigns, telemetrySnapshots, false),
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
		IReadOnlyList<LocalOnlineRoomStubState.TelemetrySnapshot> telemetrySnapshots)
	{
		return
		[
			new MultiplayerRoomPeerSnapshot
			{
				PeerId = 1,
				Label = "IronBell",
				Phase = ResolvePhase("IronBell", true, roundLaunched, raceLive, roundComplete, submittedCallsigns),
				IsReady = !roundComplete,
				IsLoaded = roundLaunched || raceLive,
				IsLaunchEligible = true,
				HasFullDeck = true,
				MonitorRank = 1,
				PresenceText = ResolveRemotePresenceText("IronBell", ticket, roundLaunched, raceLive, roundComplete, submittedCallsigns, telemetrySnapshots, true),
				MonitorText = ResolveRemoteMonitorText("IronBell", ticket, roundLaunched, raceLive, roundComplete, submittedCallsigns, telemetrySnapshots, true),
				DeckText = ticket.UsesLockedDeck
					? "IronBell  |  locked squad"
					: "IronBell  |  Brawler, Shooter, Defender"
			},
			new MultiplayerRoomPeerSnapshot
			{
				PeerId = 2,
				Label = localCallsign,
				Phase = ResolvePhase(localCallsign, localSeatActive, roundLaunched, raceLive, roundComplete, submittedCallsigns),
				IsReady = !roundComplete && localReady,
				IsLoaded = (roundLaunched || raceLive) && localSeatActive,
				IsLaunchEligible = localSeatActive,
				HasFullDeck = true,
				MonitorRank = 2,
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
				Phase = ResolvePhase("Northgate", true, roundLaunched, raceLive, roundComplete, submittedCallsigns),
				IsReady = false,
				IsLoaded = roundLaunched || raceLive,
				IsLaunchEligible = true,
				HasFullDeck = true,
				MonitorRank = 3,
				PresenceText = ResolveRemotePresenceText("Northgate", ticket, roundLaunched, raceLive, roundComplete, submittedCallsigns, telemetrySnapshots, false),
				MonitorText = ResolveRemoteMonitorText("Northgate", ticket, roundLaunched, raceLive, roundComplete, submittedCallsigns, telemetrySnapshots, false),
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
		bool isHost)
	{
		if (roundComplete && submittedCallsigns.Contains(callsign, StringComparer.OrdinalIgnoreCase))
		{
			return "result submitted, awaiting rematch";
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
		bool isHost)
	{
		if (roundComplete && submittedCallsigns.Contains(callsign, StringComparer.OrdinalIgnoreCase))
		{
			return $"{callsign}  |  submitted  |  awaiting rematch";
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
