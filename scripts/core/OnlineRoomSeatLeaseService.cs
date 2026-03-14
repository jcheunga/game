using System;
using System.Text;

public static class OnlineRoomSeatLeaseService
{
	private const long AutoRenewLeadSeconds = 45;
	private const long RenewAttemptCooldownSeconds = 12;

	public static bool IsAvailable => true;

	private static readonly IOnlineRoomSeatLeaseProvider LocalProvider = new LocalOnlineRoomSeatLeaseProvider();
	private static OnlineRoomSeatLeaseResult _lastResult;
	private static string _lastStatus = "Online room seat lease not refreshed yet.";
	private static long _lastRenewAttemptUnixSeconds;

	public static bool ShouldAutoRenew()
	{
		var ticket = OnlineRoomJoinService.GetCachedTicket();
		if (ticket == null || OnlineRoomJoinService.IsTicketExpired(ticket))
		{
			return false;
		}

		var remainingSeconds = OnlineRoomJoinService.GetRemainingLeaseSeconds(ticket);
		return remainingSeconds >= 0 && remainingSeconds <= AutoRenewLeadSeconds;
	}

	public static bool RenewSeat(out string message)
	{
		return RenewSeat(forceRefresh: true, out message);
	}

	public static bool TryAutoRenewIfNeeded(out string message)
	{
		if (!ShouldAutoRenew())
		{
			message = "Room seat lease does not need renewal yet.";
			return false;
		}

		return RenewSeat(forceRefresh: false, out message);
	}

	public static string BuildStatusSummary()
	{
		var ticket = OnlineRoomJoinService.GetCachedTicket();
		var currentResult = GetScopedResult(ticket);
		if (ticket == null)
		{
			return
				"Online room seat lease:\n" +
				"No joined room ticket is active.\n" +
				$"Provider status: {_lastStatus}";
		}

		if (currentResult == null)
		{
			return
				"Online room seat lease:\n" +
				$"Seat for {ticket.RoomTitle} expires in {FormatRemainingSeconds(OnlineRoomJoinService.GetRemainingLeaseSeconds(ticket))}.\n" +
				$"Auto renew: {(ShouldAutoRenew() ? "armed now" : $"armed under {AutoRenewLeadSeconds}s")}\n" +
				$"Provider status: {_lastStatus}";
		}

		var builder = new StringBuilder();
		builder.AppendLine($"Online room seat lease ({currentResult.ProviderDisplayName}):");
		builder.AppendLine(currentResult.Summary);
		builder.AppendLine($"Seat: {ticket.RoomTitle}  |  Ticket: {MaskToken(ticket.TicketId)}");
		builder.AppendLine($"Remaining: {FormatRemainingSeconds(OnlineRoomJoinService.GetRemainingLeaseSeconds(ticket))}");
		builder.AppendLine($"Auto renew: {(ShouldAutoRenew() ? "armed now" : $"armed under {AutoRenewLeadSeconds}s")}");
		builder.Append($"Status: {currentResult.Status}");
		return builder.ToString();
	}

	public static void ClearLastLease(string reason = "")
	{
		_lastResult = null;
		_lastRenewAttemptUnixSeconds = 0;
		if (!string.IsNullOrWhiteSpace(reason))
		{
			_lastStatus = reason;
		}
	}

	private static bool RenewSeat(bool forceRefresh, out string message)
	{
		var ticket = OnlineRoomJoinService.GetCachedTicket();
		if (ticket == null)
		{
			message = "No cached join ticket. Join or host a room first.";
			_lastStatus = message;
			return false;
		}

		if (OnlineRoomJoinService.IsTicketExpired(ticket))
		{
			message = $"Room seat for {ticket.RoomTitle} has already expired. Renew it through quick match or a fresh join.";
			_lastStatus = message;
			return false;
		}

		var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		if (!forceRefresh && _lastRenewAttemptUnixSeconds > 0 && now - _lastRenewAttemptUnixSeconds < RenewAttemptCooldownSeconds)
		{
			message = "Room seat lease renewal was attempted recently. Waiting before the next automatic ping.";
			return false;
		}

		var request = new OnlineRoomSeatLeaseRequest
		{
			RoomId = ticket.RoomId,
			BoardCode = ticket.BoardCode,
			TicketId = ticket.TicketId,
			JoinToken = ticket.JoinToken,
			PlayerProfileId = GameState.Instance?.PlayerProfileId ?? "",
			PlayerCallsign = GameState.Instance?.PlayerCallsign ?? "Lantern",
			ForceRefresh = forceRefresh,
			RequestedAtUnixSeconds = now
		};

		var provider = ResolveProvider();
		try
		{
			_lastRenewAttemptUnixSeconds = now;
			_lastResult = provider.RenewSeat(ticket, request);
			OnlineRoomJoinService.UpdateCachedTicketLease(
				_lastResult.ProviderDisplayName,
				string.IsNullOrWhiteSpace(_lastResult.Status) ? ticket.Status : _lastResult.Status,
				_lastResult.Summary,
				string.IsNullOrWhiteSpace(_lastResult.JoinToken) ? ticket.JoinToken : _lastResult.JoinToken,
				_lastResult.ExpiresAtUnixSeconds,
				out var leaseUpdateMessage);
			_lastStatus = $"{provider.DisplayName}: {_lastResult.Summary}";
			message =
				$"Renewed room seat lease for {ticket.RoomTitle} via {provider.DisplayName}.\n" +
				leaseUpdateMessage;
			return true;
		}
		catch (Exception ex)
		{
			_lastStatus = $"{provider.DisplayName} seat lease failed: {ex.Message}";
			message = _lastStatus;
			return false;
		}
	}

	private static IOnlineRoomSeatLeaseProvider ResolveProvider()
	{
		var providerId = ChallengeSyncProviderCatalog.NormalizeId(GameState.Instance?.ChallengeSyncProviderId ?? "");
		return providerId == ChallengeSyncProviderCatalog.HttpApiId
			? new HttpApiOnlineRoomSeatLeaseProvider(BuildHttpEndpoint(GameState.Instance?.ChallengeSyncEndpoint ?? ""))
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
			return normalized[..^"/challenge-sync".Length] + "/challenge-room-lease";
		}

		return normalized.TrimEnd('/') + "/challenge-room-lease";
	}

	private static string FormatRemainingSeconds(long remainingSeconds)
	{
		if (remainingSeconds < 0)
		{
			return "expired";
		}

		if (remainingSeconds == long.MaxValue)
		{
			return "open";
		}

		if (remainingSeconds == 0)
		{
			return "now";
		}

		return $"{remainingSeconds}s";
	}

	private static string MaskToken(string token)
	{
		if (string.IsNullOrWhiteSpace(token))
		{
			return "n/a";
		}

		if (token.Length <= 8)
		{
			return token;
		}

		return $"{token[..4]}...{token[^4..]}";
	}

	private static OnlineRoomSeatLeaseResult GetScopedResult(OnlineRoomJoinTicket ticket)
	{
		return MatchesTicket(_lastResult, ticket) ? _lastResult : null;
	}

	private static bool MatchesTicket(OnlineRoomSeatLeaseResult result, OnlineRoomJoinTicket ticket)
	{
		if (result == null || ticket == null)
		{
			return false;
		}

		if (!string.IsNullOrWhiteSpace(result.TicketId) &&
			!string.IsNullOrWhiteSpace(ticket.TicketId) &&
			!result.TicketId.Equals(ticket.TicketId, StringComparison.OrdinalIgnoreCase))
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
