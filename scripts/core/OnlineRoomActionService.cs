using System;
using System.Linq;
using System.Text;

public static class OnlineRoomActionService
{
	private const string SetReadyActionId = "set_ready";
	private const string LaunchRoundActionId = "launch_round";
	private const string ResetRoundActionId = "reset_round";
	private const string LeaveRoomActionId = "leave_room";

	public static bool IsAvailable => true;

	private static readonly IOnlineRoomActionProvider LocalProvider = new LocalOnlineRoomActionProvider();
	private static OnlineRoomActionResult _lastResult;
	private static string _lastStatus = "Online room action not sent yet.";

	public static bool CanToggleReady()
	{
		var ticket = OnlineRoomJoinService.GetCachedTicket();
		return ticket != null &&
			!string.Equals(ticket.Status, "spectate", StringComparison.OrdinalIgnoreCase) &&
			!string.Equals(ticket.Status, "waitlist", StringComparison.OrdinalIgnoreCase);
	}

	public static bool CanLaunchRound()
	{
		var ticket = OnlineRoomJoinService.GetCachedTicket();
		if (ticket == null ||
			!string.Equals(ticket.Status, "hosted", StringComparison.OrdinalIgnoreCase) &&
			(ticket.SeatLabel?.IndexOf("host", StringComparison.OrdinalIgnoreCase) ?? -1) < 0)
		{
			return false;
		}

		var sessionSnapshot = OnlineRoomSessionService.GetCachedSnapshot()?.RoomSnapshot;
		if (sessionSnapshot == null || !sessionSnapshot.HasRoom || sessionSnapshot.RoundLocked)
		{
			return false;
		}

		var launchEligiblePeers = sessionSnapshot.Peers.Where(peer => peer.IsLaunchEligible).ToArray();
		if (launchEligiblePeers.Length == 0)
		{
			return false;
		}

		if (launchEligiblePeers.Any(peer => !peer.IsReady))
		{
			return false;
		}

		if (!sessionSnapshot.UsesLockedDeck && launchEligiblePeers.Any(peer => !peer.HasFullDeck))
		{
			return false;
		}

		return true;
	}

	public static bool CanResetRound()
	{
		var ticket = OnlineRoomJoinService.GetCachedTicket();
		if (ticket == null ||
			!string.Equals(ticket.Status, "hosted", StringComparison.OrdinalIgnoreCase) &&
			(ticket.SeatLabel?.IndexOf("host", StringComparison.OrdinalIgnoreCase) ?? -1) < 0)
		{
			return false;
		}

		var sessionSnapshot = OnlineRoomSessionService.GetCachedSnapshot()?.RoomSnapshot;
		if (sessionSnapshot == null || !sessionSnapshot.HasRoom || sessionSnapshot.RoundLocked)
		{
			return false;
		}

		return sessionSnapshot.RoundComplete || OnlineRoomScoreboardService.GetCachedSnapshot()?.Entries.Count > 0;
	}

	public static string BuildToggleReadyLabel()
	{
		var ticket = OnlineRoomJoinService.GetCachedTicket();
		if (ticket == null)
		{
			return "Ready Up Online";
		}

		if (string.Equals(ticket.Status, "spectate", StringComparison.OrdinalIgnoreCase))
		{
			return "Spectator Seat";
		}

		if (string.Equals(ticket.Status, "waitlist", StringComparison.OrdinalIgnoreCase))
		{
			return "Waitlisted";
		}

		return GetDesiredReadyState(ticket) ? "Stand Down Online" : "Ready Up Online";
	}

	public static string BuildLaunchRoundLabel()
	{
		var ticket = OnlineRoomJoinService.GetCachedTicket();
		if (ticket == null)
		{
			return "Launch Online Room";
		}

		if (!string.Equals(ticket.Status, "hosted", StringComparison.OrdinalIgnoreCase) &&
			(ticket.SeatLabel?.IndexOf("host", StringComparison.OrdinalIgnoreCase) ?? -1) < 0)
		{
			return "Host Seat Only";
		}

		var sessionSnapshot = OnlineRoomSessionService.GetCachedSnapshot()?.RoomSnapshot;
		if (sessionSnapshot?.RoundLocked == true)
		{
			return "Round Live";
		}

		return "Launch Online Room";
	}

	public static string BuildResetRoundLabel()
	{
		var ticket = OnlineRoomJoinService.GetCachedTicket();
		if (ticket == null)
		{
			return "Reset Room Round";
		}

		if (!string.Equals(ticket.Status, "hosted", StringComparison.OrdinalIgnoreCase) &&
			(ticket.SeatLabel?.IndexOf("host", StringComparison.OrdinalIgnoreCase) ?? -1) < 0)
		{
			return "Host Seat Only";
		}

		return "Reset Room Round";
	}

	public static string BuildLeaveRoomLabel()
	{
		return "Leave Online Room";
	}

	public static bool ToggleReady(out string message)
	{
		var ticket = OnlineRoomJoinService.GetCachedTicket();
		if (ticket == null)
		{
			message = "No cached join ticket. Request Join on a room listing first.";
			_lastStatus = message;
			return false;
		}

		if (!CanToggleReady())
		{
			message = "This join ticket does not have an active runner seat.";
			_lastStatus = message;
			return false;
		}

		return SetReady(!GetDesiredReadyState(ticket), out message);
	}

	public static bool LaunchRound(out string message)
	{
		var ticket = OnlineRoomJoinService.GetCachedTicket();
		if (ticket == null)
		{
			message = "No cached join ticket. Request Join on a room listing first.";
			_lastStatus = message;
			return false;
		}

		if (!CanLaunchRound())
		{
			message = "The current room is not launch-ready yet. Make sure every active runner is ready and all deck blockers are cleared.";
			_lastStatus = message;
			return false;
		}

		return SendAction(LaunchRoundActionId, GetDesiredReadyState(ticket), out message);
	}

	public static bool ResetRound(out string message)
	{
		var ticket = OnlineRoomJoinService.GetCachedTicket();
		if (ticket == null)
		{
			message = "No cached join ticket. Request Join on a room listing first.";
			_lastStatus = message;
			return false;
		}

		if (!CanResetRound())
		{
			message = "The current room is not ready for rematch reset yet. Wait for the round to finish or for room results to land.";
			_lastStatus = message;
			return false;
		}

		return SendAction(ResetRoundActionId, false, out message);
	}

	public static bool LeaveRoom(out string message)
	{
		var ticket = OnlineRoomJoinService.GetCachedTicket();
		if (ticket == null)
		{
			message = "No cached join ticket. Join or host a room first.";
			_lastStatus = message;
			return false;
		}

		var roomId = ticket.RoomId;
		var roomTitle = ticket.RoomTitle;
		if (!SendAction(LeaveRoomActionId, false, out var actionMessage))
		{
			message = actionMessage;
			return false;
		}

		var clearReason = $"Left online room {roomTitle}.";
		OnlineRoomCreateService.ClearHostedRoom(roomId, clearReason);
		OnlineRoomJoinService.ClearCachedTicket(clearReason);
		OnlineRoomSessionService.ClearCachedSnapshot(clearReason);
		OnlineRoomResultService.ClearLastSubmission(clearReason);
		OnlineRoomScoreboardService.ClearCachedSnapshot(clearReason);
		OnlineRoomTelemetryService.ClearLastSubmission(clearReason);
		message = $"{actionMessage}\nCleared local room state for {roomTitle}.";
		return true;
	}

	public static string BuildStatusSummary()
	{
		var ticket = OnlineRoomJoinService.GetCachedTicket();
		if (ticket == null)
		{
			return
				"Online room action:\n" +
				"No active room action yet. Request Join first, then use the room action controls to change ready state.\n" +
				$"Provider status: {_lastStatus}";
		}

		if (_lastResult == null || !MatchesTicket(ticket, _lastResult.TicketId))
		{
			return
				"Online room action:\n" +
				"Ready-state control is idle for the current join ticket.\n" +
				$"Current seat: {ticket.SeatLabel}\n" +
				$"Suggested action: {BuildToggleReadyLabel()}\n" +
				$"Provider status: {_lastStatus}";
		}

		var builder = new StringBuilder();
		builder.AppendLine($"Online room action ({_lastResult.ProviderDisplayName}):");
		builder.AppendLine(_lastResult.Summary);
		builder.AppendLine($"Action: {_lastResult.ActionId}  |  Ready: {(_lastResult.ReadyState ? "yes" : "no")}  |  Status: {_lastResult.Status}");
		builder.AppendLine($"Next toggle: {BuildToggleReadyLabel()}");
		if (BuildLaunchRoundLabel() == "Launch Online Room" || BuildLaunchRoundLabel() == "Round Live")
		{
			builder.AppendLine($"Host action: {BuildLaunchRoundLabel()}");
		}
		if (CanResetRound() || string.Equals(_lastResult.ActionId, ResetRoundActionId, StringComparison.OrdinalIgnoreCase))
		{
			builder.AppendLine($"Host reset: {BuildResetRoundLabel()}");
		}
		builder.Append($"Exit: {BuildLeaveRoomLabel()}");
		return builder.ToString();
	}

	public static bool? GetReadyStateHint()
	{
		var ticket = OnlineRoomJoinService.GetCachedTicket();
		if (ticket == null)
		{
			return null;
		}

		if (_lastResult != null && MatchesTicket(ticket, _lastResult.TicketId))
		{
			return _lastResult.ReadyState;
		}

		var sessionSnapshot = OnlineRoomSessionService.GetCachedSnapshot()?.RoomSnapshot;
		if (sessionSnapshot == null || !sessionSnapshot.HasRoom)
		{
			return null;
		}

		var localCallsign = GameState.Instance?.PlayerCallsign ?? "";
		foreach (var peer in sessionSnapshot.Peers)
		{
			if (peer.Label.Equals(localCallsign, StringComparison.OrdinalIgnoreCase))
			{
				return peer.IsReady;
			}
		}

		return null;
	}

	public static bool GetLaunchRoundHint()
	{
		var ticket = OnlineRoomJoinService.GetCachedTicket();
		return ticket != null &&
			_lastResult != null &&
			MatchesTicket(ticket, _lastResult.TicketId) &&
			string.Equals(_lastResult.ActionId, LaunchRoundActionId, StringComparison.OrdinalIgnoreCase) &&
			string.Equals(_lastResult.Status, "accepted", StringComparison.OrdinalIgnoreCase);
	}

	private static bool SetReady(bool desiredReadyState, out string message)
	{
		return SendAction(SetReadyActionId, desiredReadyState, out message);
	}

	private static bool GetDesiredReadyState(OnlineRoomJoinTicket ticket)
	{
		return GetReadyStateHint() ?? false;
	}

	private static bool MatchesTicket(OnlineRoomJoinTicket ticket, string ticketId)
	{
		return ticket != null &&
			!string.IsNullOrWhiteSpace(ticketId) &&
			ticket.TicketId.Equals(ticketId, StringComparison.OrdinalIgnoreCase);
	}

	private static IOnlineRoomActionProvider ResolveProvider()
	{
		var providerId = ChallengeSyncProviderCatalog.NormalizeId(GameState.Instance?.ChallengeSyncProviderId ?? "");
		return providerId == ChallengeSyncProviderCatalog.HttpApiId
			? new HttpApiOnlineRoomActionProvider(BuildHttpEndpoint(GameState.Instance?.ChallengeSyncEndpoint ?? ""))
			: LocalProvider;
	}

	private static string BuildHttpEndpoint(string syncEndpoint)
	{
		var normalized = string.IsNullOrWhiteSpace(syncEndpoint) ? "" : syncEndpoint.Trim();
		if (string.IsNullOrWhiteSpace(normalized))
		{
			return "";
		}

		if (normalized.EndsWith("/challenge-sync", StringComparison.OrdinalIgnoreCase))
		{
			return normalized[..^"/challenge-sync".Length] + "/challenge-room-action";
		}

		return normalized.TrimEnd('/') + "/challenge-room-action";
	}

	private static bool SendAction(string actionId, bool desiredReadyState, out string message)
	{
		var ticket = OnlineRoomJoinService.GetCachedTicket();
		if (ticket == null)
		{
			message = "No cached join ticket. Request Join on a room listing first.";
			_lastStatus = message;
			return false;
		}

		var request = new OnlineRoomActionRequest
		{
			RoomId = ticket.RoomId,
			BoardCode = ticket.BoardCode,
			TicketId = ticket.TicketId,
			JoinToken = ticket.JoinToken,
			PlayerProfileId = GameState.Instance?.PlayerProfileId ?? "",
			PlayerCallsign = GameState.Instance?.PlayerCallsign ?? "Convoy",
			ActionId = actionId,
			ReadyState = desiredReadyState,
			RequestedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
		};

		var provider = ResolveProvider();
		try
		{
			_lastResult = provider.SendAction(ticket, request);
			_lastStatus = $"{provider.DisplayName}: {_lastResult.Summary}";
			message = $"Sent online room action via {provider.DisplayName}.";
			return true;
		}
		catch (Exception ex)
		{
			_lastStatus = $"{provider.DisplayName} room action failed: {ex.Message}";
			message = _lastStatus;
			return false;
		}
	}
}
