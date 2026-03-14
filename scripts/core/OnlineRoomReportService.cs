using System;
using System.Linq;
using System.Text;

public static class OnlineRoomReportService
{
	public static bool IsAvailable => true;

	private static readonly IOnlineRoomReportProvider LocalProvider = new LocalOnlineRoomReportProvider();
	private static OnlineRoomReportResult _lastResult;
	private static string _lastStatus = "Online room moderation report not submitted yet.";

	public static bool CanSubmitJoinedRoomReport()
	{
		var ticket = OnlineRoomJoinService.GetCachedTicket();
		return ticket != null && !OnlineRoomJoinService.IsTicketExpired(ticket);
	}

	public static bool SubmitJoinedRoomReport(string reasonId, out string message)
	{
		var ticket = OnlineRoomJoinService.GetCachedTicket();
		if (ticket == null)
		{
			message = "No cached join ticket. Join or host a room before submitting a moderation report.";
			_lastStatus = message;
			return false;
		}

		if (OnlineRoomJoinService.IsTicketExpired(ticket))
		{
			message = $"Room seat for {ticket.RoomTitle} has expired. Renew the seat before filing a room report.";
			_lastStatus = message;
			return false;
		}

		var resolvedReasonId = OnlineRoomReportReasonCatalog.NormalizeId(reasonId);
		var (subjectType, subjectLabel, notes) = ResolveSubject(ticket);
		var request = new OnlineRoomReportRequest
		{
			RoomId = ticket.RoomId,
			BoardCode = ticket.BoardCode,
			TicketId = ticket.TicketId,
			JoinToken = ticket.JoinToken,
			PlayerProfileId = GameState.Instance?.PlayerProfileId ?? "",
			PlayerCallsign = GameState.Instance?.PlayerCallsign ?? "Lantern",
			SubjectType = subjectType,
			SubjectLabel = subjectLabel,
			ReasonId = resolvedReasonId,
			Notes = notes,
			RequestedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
		};

		var provider = ResolveProvider();
		try
		{
			_lastResult = provider.SubmitReport(ticket, request);
			_lastStatus = $"{provider.DisplayName}: {_lastResult.Summary}";
			message =
				$"Submitted room report via {provider.DisplayName}.\n" +
				$"{_lastResult.Summary}";
			return true;
		}
		catch (Exception ex)
		{
			_lastStatus = $"{provider.DisplayName} room report failed: {ex.Message}";
			message = _lastStatus;
			return false;
		}
	}

	public static string BuildStatusSummary()
	{
		var ticket = OnlineRoomJoinService.GetCachedTicket();
		var currentResult = GetScopedResult(ticket);
		var defaultReason = OnlineRoomReportReasonCatalog.Get(OnlineRoomReportReasonCatalog.SuspiciousScoreId);
		if (ticket == null)
		{
			return
				"Online room moderation:\n" +
				"No joined room ticket is active.\n" +
				$"Default reason: {defaultReason.Title}\n" +
				$"Provider status: {_lastStatus}";
		}

		if (currentResult == null)
		{
			var (subjectType, subjectLabel, _) = ResolveSubject(ticket);
			return
				"Online room moderation:\n" +
				$"Next subject: {subjectType}  |  {subjectLabel}\n" +
				$"Default reason: {defaultReason.Title}\n" +
				$"Provider status: {_lastStatus}";
		}

		var reason = OnlineRoomReportReasonCatalog.Get(currentResult.ReasonId);
		var builder = new StringBuilder();
		builder.AppendLine($"Online room moderation ({currentResult.ProviderDisplayName}):");
		builder.AppendLine(currentResult.Summary);
		builder.AppendLine($"Subject: {currentResult.SubjectType}  |  {currentResult.SubjectLabel}");
		builder.AppendLine($"Reason: {reason.Title}");
		builder.Append($"Status: {currentResult.Status}");
		return builder.ToString();
	}

	public static void ClearLastReport(string reason = "")
	{
		_lastResult = null;
		if (!string.IsNullOrWhiteSpace(reason))
		{
			_lastStatus = reason;
		}
	}

	private static (string SubjectType, string SubjectLabel, string Notes) ResolveSubject(OnlineRoomJoinTicket ticket)
	{
		var localCallsign = GameState.Instance?.PlayerCallsign ?? "";
		var scoreboardEntry = OnlineRoomScoreboardService.GetCachedSnapshot()?.Entries?
			.FirstOrDefault(entry => !string.IsNullOrWhiteSpace(entry.PlayerCallsign) &&
				!entry.PlayerCallsign.Equals(localCallsign, StringComparison.OrdinalIgnoreCase));
		if (scoreboardEntry != null)
		{
			return (
				"player",
				scoreboardEntry.PlayerCallsign,
				$"Scoreboard target on {ticket.BoardCode}: score {scoreboardEntry.Score}, rank {scoreboardEntry.Rank}, hull {scoreboardEntry.HullPercent}%.");
		}

		var remotePeer = OnlineRoomSessionService.GetCachedSnapshot()?.RoomSnapshot?.Peers?
			.FirstOrDefault(peer => !string.IsNullOrWhiteSpace(peer.Label) &&
				!peer.Label.Equals(localCallsign, StringComparison.OrdinalIgnoreCase));
		if (remotePeer != null)
		{
			return (
				"player",
				remotePeer.Label,
				$"Room peer target on {ticket.BoardCode}: phase {remotePeer.Phase}, detail {remotePeer.PresenceText}.");
		}

		return ("room", ticket.RoomTitle, $"Room-level report for {ticket.RoomTitle} on {ticket.BoardCode}.");
	}

	private static IOnlineRoomReportProvider ResolveProvider()
	{
		var providerId = ChallengeSyncProviderCatalog.NormalizeId(GameState.Instance?.ChallengeSyncProviderId ?? "");
		return providerId == ChallengeSyncProviderCatalog.HttpApiId
			? new HttpApiOnlineRoomReportProvider(BuildHttpEndpoint(GameState.Instance?.ChallengeSyncEndpoint ?? ""))
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
			return normalized[..^"/challenge-sync".Length] + "/challenge-room-report";
		}

		return normalized.TrimEnd('/') + "/challenge-room-report";
	}

	private static OnlineRoomReportResult GetScopedResult(OnlineRoomJoinTicket ticket)
	{
		return MatchesRoom(_lastResult, ticket) ? _lastResult : null;
	}

	private static bool MatchesRoom(OnlineRoomReportResult result, OnlineRoomJoinTicket ticket)
	{
		if (result == null || ticket == null)
		{
			return false;
		}

		if (!string.IsNullOrWhiteSpace(result.RoomId) &&
			!string.IsNullOrWhiteSpace(ticket.RoomId) &&
			!result.RoomId.Equals(ticket.RoomId, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		return string.IsNullOrWhiteSpace(result.BoardCode) ||
			string.IsNullOrWhiteSpace(ticket.BoardCode) ||
			AsyncChallengeCatalog.NormalizeCode(result.BoardCode)
				.Equals(AsyncChallengeCatalog.NormalizeCode(ticket.BoardCode), StringComparison.OrdinalIgnoreCase);
	}
}
