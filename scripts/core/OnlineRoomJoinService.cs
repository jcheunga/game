using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public static class OnlineRoomJoinService
{
	public static bool IsAvailable => true;

	private static readonly IOnlineRoomJoinProvider LocalProvider = new LocalOnlineRoomJoinProvider();
	private static OnlineRoomJoinTicket _cachedTicket;
	private static string _lastStatus = "Online room join not requested yet.";

	public static bool RequestJoin(OnlineRoomDirectoryEntry room, out string message)
	{
		var gameState = GameState.Instance;
		if (gameState == null)
		{
			message = "Game state is unavailable.";
			_lastStatus = message;
			return false;
		}

		if (room == null || string.IsNullOrWhiteSpace(room.RoomId) || string.IsNullOrWhiteSpace(room.BoardCode))
		{
			message = "Selected room listing is incomplete.";
			_lastStatus = message;
			return false;
		}

		var requestedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		var request = new OnlineRoomJoinRequest
		{
			RoomId = room.RoomId,
			BoardCode = room.BoardCode,
			PlayerProfileId = gameState.PlayerProfileId,
			PlayerCallsign = gameState.PlayerCallsign,
			WantsLockedDeckSeat = room.UsesLockedDeck,
			RequestedAtUnixSeconds = requestedAt
		};

		var provider = ResolveProvider();
		try
		{
			_cachedTicket = NormalizeTicket(provider.RequestJoin(room, request), room, request);
			_lastStatus = $"{provider.DisplayName}: {_cachedTicket.Summary}";
			message = $"Requested join ticket for {room.Title} via {provider.DisplayName}.";
			return true;
		}
		catch (Exception ex)
		{
			_lastStatus = $"{provider.DisplayName} join request failed: {ex.Message}";
			message = _lastStatus;
			return false;
		}
	}

	public static bool AdoptCreatedHostTicket(OnlineRoomJoinTicket ticket, out string message)
	{
		if (ticket == null || string.IsNullOrWhiteSpace(ticket.RoomId) || string.IsNullOrWhiteSpace(ticket.BoardCode))
		{
			message = "Hosted room ticket is incomplete.";
			_lastStatus = message;
			return false;
		}

		_cachedTicket = NormalizeHostedTicket(ticket);
		_lastStatus = $"{_cachedTicket.ProviderDisplayName}: {_cachedTicket.Summary}";
		message = $"Adopted host ticket for {_cachedTicket.RoomTitle}.";
		return true;
	}

	public static bool AdoptNegotiatedTicket(OnlineRoomJoinTicket ticket, out string message)
	{
		if (ticket == null || string.IsNullOrWhiteSpace(ticket.RoomId) || string.IsNullOrWhiteSpace(ticket.BoardCode))
		{
			message = "Matched room ticket is incomplete.";
			_lastStatus = message;
			return false;
		}

		_cachedTicket = NormalizeAdoptedTicket(ticket);
		_lastStatus = $"{_cachedTicket.ProviderDisplayName}: {_cachedTicket.Summary}";
		message = $"Adopted room seat for {_cachedTicket.RoomTitle}.";
		return true;
	}

	public static OnlineRoomJoinTicket GetCachedTicket()
	{
		return _cachedTicket;
	}

	public static void ClearCachedTicket(string reason = "")
	{
		_cachedTicket = null;
		if (!string.IsNullOrWhiteSpace(reason))
		{
			_lastStatus = reason;
		}
	}

	public static string BuildStatusSummary()
	{
		if (_cachedTicket == null)
		{
			return
				"Online room join:\n" +
				"No join ticket requested yet. Use `Request Join` on a room listing to negotiate backend access.\n" +
				$"Provider status: {_lastStatus}";
		}

		var expiresLabel = _cachedTicket.ExpiresAtUnixSeconds > 0
			? DateTimeOffset.FromUnixTimeSeconds(_cachedTicket.ExpiresAtUnixSeconds).ToLocalTime().ToString("MM-dd HH:mm:ss")
			: "pending";
		var builder = new StringBuilder();
		builder.AppendLine($"Online room join ({_cachedTicket.ProviderDisplayName}):");
		builder.AppendLine(_cachedTicket.Summary);
		builder.AppendLine($"Room: {_cachedTicket.RoomTitle}  |  Status: {_cachedTicket.Status}");
		builder.AppendLine($"Board: {_cachedTicket.BoardCode}  |  Seat: {_cachedTicket.SeatLabel}");
		builder.AppendLine($"Transport: {_cachedTicket.TransportHint}  |  Relay: {_cachedTicket.RelayEndpoint}");
		builder.AppendLine($"Ticket: {MaskToken(_cachedTicket.TicketId)}  |  Join token: {MaskToken(_cachedTicket.JoinToken)}");
		builder.Append($"Expires: {expiresLabel}");
		return builder.ToString();
	}

	private static IOnlineRoomJoinProvider ResolveProvider()
	{
		var providerId = ChallengeSyncProviderCatalog.NormalizeId(GameState.Instance?.ChallengeSyncProviderId ?? "");
		return providerId == ChallengeSyncProviderCatalog.HttpApiId
			? new HttpApiOnlineRoomJoinProvider(BuildHttpEndpoint(GameState.Instance?.ChallengeSyncEndpoint ?? ""))
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
			return normalized[..^"/challenge-sync".Length] + "/challenge-room-join";
		}

		return normalized.TrimEnd('/') + "/challenge-room-join";
	}

	private static OnlineRoomJoinTicket NormalizeHostedTicket(OnlineRoomJoinTicket ticket)
	{
		var boardCode = AsyncChallengeCatalog.NormalizeCode(ticket.BoardCode);
		if (!AsyncChallengeCatalog.TryParse(boardCode, out _, out _))
		{
			boardCode = ticket.BoardCode;
		}

		var usesLockedDeck = ticket.UsesLockedDeck || ticket.LockedDeckUnitIds?.Length > 0;
		var lockedDeck = usesLockedDeck ? NormalizeLockedDeck(ticket.LockedDeckUnitIds) : [];
		var requestedAt = ticket.RequestedAtUnixSeconds > 0
			? ticket.RequestedAtUnixSeconds
			: DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		return new OnlineRoomJoinTicket
		{
			ProviderId = string.IsNullOrWhiteSpace(ticket.ProviderId) ? ResolveProvider().Id : ticket.ProviderId,
			ProviderDisplayName = string.IsNullOrWhiteSpace(ticket.ProviderDisplayName) ? ResolveProvider().DisplayName : ticket.ProviderDisplayName,
			RoomId = ticket.RoomId.Trim(),
			RoomTitle = string.IsNullOrWhiteSpace(ticket.RoomTitle) ? "Hosted Room" : ticket.RoomTitle.Trim(),
			BoardCode = boardCode,
			Status = string.IsNullOrWhiteSpace(ticket.Status) ? "hosted" : ticket.Status.Trim().ToLowerInvariant(),
			Summary = string.IsNullOrWhiteSpace(ticket.Summary)
				? $"Hosted room {ticket.RoomTitle} is ready to accept join requests."
				: ticket.Summary.Trim(),
			TicketId = string.IsNullOrWhiteSpace(ticket.TicketId) ? $"HOST-{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}" : ticket.TicketId.Trim(),
			JoinToken = string.IsNullOrWhiteSpace(ticket.JoinToken) ? $"host-{Guid.NewGuid():N}" : ticket.JoinToken.Trim(),
			TransportHint = string.IsNullOrWhiteSpace(ticket.TransportHint) ? "internet_room_hosted" : ticket.TransportHint.Trim(),
			RelayEndpoint = string.IsNullOrWhiteSpace(ticket.RelayEndpoint) ? "pending relay" : ticket.RelayEndpoint.Trim(),
			SeatLabel = string.IsNullOrWhiteSpace(ticket.SeatLabel) ? "host seat" : ticket.SeatLabel.Trim(),
			RequestedAtUnixSeconds = requestedAt,
			ExpiresAtUnixSeconds = ticket.ExpiresAtUnixSeconds > 0 ? ticket.ExpiresAtUnixSeconds : requestedAt + 3600,
			UsesLockedDeck = usesLockedDeck,
			LockedDeckUnitIds = usesLockedDeck ? lockedDeck.ToArray() : []
		};
	}

	private static OnlineRoomJoinTicket NormalizeAdoptedTicket(OnlineRoomJoinTicket ticket)
	{
		var boardCode = AsyncChallengeCatalog.NormalizeCode(ticket.BoardCode);
		if (!AsyncChallengeCatalog.TryParse(boardCode, out _, out _))
		{
			boardCode = ticket.BoardCode;
		}

		var usesLockedDeck = ticket.UsesLockedDeck || ticket.LockedDeckUnitIds?.Length > 0;
		var lockedDeck = usesLockedDeck ? NormalizeLockedDeck(ticket.LockedDeckUnitIds) : [];
		var requestedAt = ticket.RequestedAtUnixSeconds > 0
			? ticket.RequestedAtUnixSeconds
			: DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		return new OnlineRoomJoinTicket
		{
			ProviderId = string.IsNullOrWhiteSpace(ticket.ProviderId) ? ResolveProvider().Id : ticket.ProviderId,
			ProviderDisplayName = string.IsNullOrWhiteSpace(ticket.ProviderDisplayName) ? ResolveProvider().DisplayName : ticket.ProviderDisplayName,
			RoomId = ticket.RoomId.Trim(),
			RoomTitle = string.IsNullOrWhiteSpace(ticket.RoomTitle) ? "Matched Room" : ticket.RoomTitle.Trim(),
			BoardCode = boardCode,
			Status = string.IsNullOrWhiteSpace(ticket.Status) ? "accepted" : ticket.Status.Trim().ToLowerInvariant(),
			Summary = string.IsNullOrWhiteSpace(ticket.Summary)
				? $"Backend room {ticket.RoomTitle} accepted this convoy."
				: ticket.Summary.Trim(),
			TicketId = string.IsNullOrWhiteSpace(ticket.TicketId) ? $"JOIN-{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}" : ticket.TicketId.Trim(),
			JoinToken = string.IsNullOrWhiteSpace(ticket.JoinToken) ? $"pending-{Guid.NewGuid():N}" : ticket.JoinToken.Trim(),
			TransportHint = string.IsNullOrWhiteSpace(ticket.TransportHint) ? "internet_room_pending" : ticket.TransportHint.Trim(),
			RelayEndpoint = string.IsNullOrWhiteSpace(ticket.RelayEndpoint) ? "pending relay" : ticket.RelayEndpoint.Trim(),
			SeatLabel = string.IsNullOrWhiteSpace(ticket.SeatLabel)
				? (usesLockedDeck ? "locked-squad seat" : "player-convoy seat")
				: ticket.SeatLabel.Trim(),
			RequestedAtUnixSeconds = requestedAt,
			ExpiresAtUnixSeconds = ticket.ExpiresAtUnixSeconds > 0 ? ticket.ExpiresAtUnixSeconds : requestedAt + 180,
			UsesLockedDeck = usesLockedDeck,
			LockedDeckUnitIds = usesLockedDeck ? lockedDeck.ToArray() : []
		};
	}

	private static OnlineRoomJoinTicket NormalizeTicket(OnlineRoomJoinTicket ticket, OnlineRoomDirectoryEntry room, OnlineRoomJoinRequest request)
	{
		var boardCode = AsyncChallengeCatalog.NormalizeCode(string.IsNullOrWhiteSpace(ticket?.BoardCode) ? room.BoardCode : ticket.BoardCode);
		if (!AsyncChallengeCatalog.TryParse(boardCode, out _, out _))
		{
			boardCode = room.BoardCode;
		}

		var usesLockedDeck = (ticket?.UsesLockedDeck ?? false) || room.UsesLockedDeck;
		var lockedDeckUnitIds = usesLockedDeck
			? NormalizeLockedDeck(ticket?.LockedDeckUnitIds?.Length > 0 ? ticket.LockedDeckUnitIds : room.LockedDeckUnitIds)
			: [];
		var status = string.IsNullOrWhiteSpace(ticket?.Status) ? "accepted" : ticket.Status.Trim().ToLowerInvariant();
		var requestedAt = ticket?.RequestedAtUnixSeconds > 0 ? ticket.RequestedAtUnixSeconds : request.RequestedAtUnixSeconds;
		var expiresAt = ticket?.ExpiresAtUnixSeconds > 0 ? ticket.ExpiresAtUnixSeconds : requestedAt + 180;
		return new OnlineRoomJoinTicket
		{
			ProviderId = ticket?.ProviderId ?? ResolveProvider().Id,
			ProviderDisplayName = string.IsNullOrWhiteSpace(ticket?.ProviderDisplayName) ? ResolveProvider().DisplayName : ticket.ProviderDisplayName,
			RoomId = string.IsNullOrWhiteSpace(ticket?.RoomId) ? room.RoomId : ticket.RoomId,
			RoomTitle = string.IsNullOrWhiteSpace(ticket?.RoomTitle) ? room.Title : ticket.RoomTitle,
			BoardCode = boardCode,
			Status = status,
			Summary = string.IsNullOrWhiteSpace(ticket?.Summary) ? BuildDefaultSummary(status, room.Title) : ticket.Summary,
			TicketId = string.IsNullOrWhiteSpace(ticket?.TicketId) ? $"JOIN-{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}" : ticket.TicketId,
			JoinToken = string.IsNullOrWhiteSpace(ticket?.JoinToken) ? $"pending-{Guid.NewGuid():N}" : ticket.JoinToken,
			TransportHint = string.IsNullOrWhiteSpace(ticket?.TransportHint) ? "internet_room_pending" : ticket.TransportHint,
			RelayEndpoint = string.IsNullOrWhiteSpace(ticket?.RelayEndpoint) ? "pending relay" : ticket.RelayEndpoint,
			SeatLabel = string.IsNullOrWhiteSpace(ticket?.SeatLabel)
				? (usesLockedDeck ? "locked-squad seat" : "player-convoy seat")
				: ticket.SeatLabel,
			RequestedAtUnixSeconds = requestedAt,
			ExpiresAtUnixSeconds = expiresAt,
			UsesLockedDeck = usesLockedDeck,
			LockedDeckUnitIds = usesLockedDeck ? lockedDeckUnitIds.ToArray() : []
		};
	}

	private static IReadOnlyList<string> NormalizeLockedDeck(IEnumerable<string> lockedDeckUnitIds)
	{
		var validIds = new HashSet<string>(GameData.PlayerRosterIds, StringComparer.OrdinalIgnoreCase);
		var normalized = new List<string>();
		foreach (var unitId in lockedDeckUnitIds ?? Array.Empty<string>())
		{
			if (string.IsNullOrWhiteSpace(unitId) || !validIds.Contains(unitId))
			{
				continue;
			}

			if (normalized.Contains(unitId, StringComparer.OrdinalIgnoreCase))
			{
				continue;
			}

			normalized.Add(unitId);
			if (normalized.Count >= 3)
			{
				break;
			}
		}

		foreach (var fallbackId in new[] { GameData.PlayerBrawlerId, GameData.PlayerShooterId, GameData.PlayerDefenderId })
		{
			if (normalized.Count >= 3)
			{
				break;
			}

			if (!normalized.Contains(fallbackId, StringComparer.OrdinalIgnoreCase))
			{
				normalized.Add(fallbackId);
			}
		}

		return normalized;
	}

	private static string BuildDefaultSummary(string status, string roomTitle)
	{
		return status switch
		{
			"spectate" => $"Room {roomTitle} is already live. Join ticket is spectator-only until the next round.",
			"waitlist" => $"Room {roomTitle} is full. Join ticket is on the waitlist.",
			_ => $"Join ticket reserved backend access for {roomTitle}."
		};
	}

	private static string MaskToken(string token)
	{
		if (string.IsNullOrWhiteSpace(token))
		{
			return "pending";
		}

		if (token.Length <= 8)
		{
			return token;
		}

		return $"{token[..4]}...{token[^4..]}";
	}
}
