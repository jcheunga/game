using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public static class OnlineRoomCreateService
{
	public static bool IsAvailable => true;

	private static readonly IOnlineRoomCreateProvider LocalProvider = new LocalOnlineRoomCreateProvider();
	private static OnlineRoomCreateResult _hostedRoom;
	private static string _lastStatus = "Online room host not published yet.";

	public static bool HostSelectedChallenge(out string message)
	{
		var gameState = GameState.Instance;
		if (gameState == null)
		{
			message = "Game state is unavailable.";
			_lastStatus = message;
			return false;
		}

		var challenge = gameState.GetSelectedAsyncChallenge();
		if (!AsyncChallengeCatalog.TryParse(challenge.Code, out _, out _))
		{
			message = "Selected async challenge is invalid.";
			_lastStatus = message;
			return false;
		}

		var request = BuildRequest(gameState, challenge);
		var provider = ResolveProvider();
		try
		{
			_hostedRoom = NormalizeResult(provider.CreateRoom(request), request);
			var adoptedHostSeat = OnlineRoomJoinService.AdoptCreatedHostTicket(_hostedRoom.HostTicket, out var adoptMessage);
			_lastStatus = $"{provider.DisplayName}: {_hostedRoom.Summary}";
			message =
				$"Hosted online room {_hostedRoom.Title} via {provider.DisplayName}.\n" +
				adoptMessage;
			return adoptedHostSeat;
		}
		catch (Exception ex)
		{
			_lastStatus = $"{provider.DisplayName} room host failed: {ex.Message}";
			message = _lastStatus;
			return false;
		}
	}

	public static OnlineRoomCreateResult GetHostedRoom()
	{
		return _hostedRoom;
	}

	public static void ClearHostedRoom(string roomId = "", string reason = "")
	{
		if (_hostedRoom != null &&
			(string.IsNullOrWhiteSpace(roomId) ||
			 _hostedRoom.RoomId.Equals(roomId, StringComparison.OrdinalIgnoreCase)))
		{
			_hostedRoom = null;
		}

		if (!string.IsNullOrWhiteSpace(reason))
		{
			_lastStatus = reason;
		}
	}

	public static OnlineRoomDirectoryEntry GetHostedRoomEntry()
	{
		if (_hostedRoom == null || string.IsNullOrWhiteSpace(_hostedRoom.RoomId))
		{
			return null;
		}

		return new OnlineRoomDirectoryEntry
		{
			RoomId = _hostedRoom.RoomId,
			Title = _hostedRoom.Title,
			Summary = _hostedRoom.Summary,
			HostCallsign = _hostedRoom.HostCallsign,
			BoardCode = _hostedRoom.BoardCode,
			BoardTitle = _hostedRoom.BoardTitle,
			CurrentPlayers = _hostedRoom.CurrentPlayers,
			MaxPlayers = _hostedRoom.MaxPlayers,
			SpectatorCount = _hostedRoom.SpectatorCount,
			Status = _hostedRoom.Status,
			Region = _hostedRoom.Region,
			UsesLockedDeck = _hostedRoom.UsesLockedDeck,
			LockedDeckUnitIds = _hostedRoom.UsesLockedDeck ? _hostedRoom.LockedDeckUnitIds ?? [] : []
		};
	}

	public static string BuildStatusSummary()
	{
		if (_hostedRoom == null)
		{
			return
				"Online room host:\n" +
				"No hosted internet room published yet. Use `Host Online Room` to publish the selected async board.\n" +
				$"Provider status: {_lastStatus}";
		}

		var builder = new StringBuilder();
		builder.AppendLine($"Online room host ({_hostedRoom.ProviderDisplayName}):");
		builder.AppendLine(_hostedRoom.Summary);
		builder.AppendLine($"Room: {_hostedRoom.Title}  |  ID: {_hostedRoom.RoomId}  |  Status: {_hostedRoom.Status}");
		builder.AppendLine($"Board: {_hostedRoom.BoardCode}  |  Host: {_hostedRoom.HostCallsign}  |  Region: {_hostedRoom.Region}");
		builder.AppendLine($"Players: {_hostedRoom.CurrentPlayers}/{_hostedRoom.MaxPlayers}  |  Spectators: {_hostedRoom.SpectatorCount}");
		builder.AppendLine($"Transport: {_hostedRoom.TransportHint}  |  Relay: {_hostedRoom.RelayEndpoint}");
		builder.Append($"Deck mode: {(_hostedRoom.UsesLockedDeck ? BuildLockedDeckSummary(_hostedRoom.LockedDeckUnitIds) : "player squad seats")}");
		return builder.ToString();
	}

	private static OnlineRoomCreateRequest BuildRequest(GameState gameState, AsyncChallengeDefinition challenge)
	{
		var stage = GameData.GetStage(challenge.Stage);
		var lockedDeckUnitIds = gameState.HasSelectedAsyncChallengeLockedDeck
			? gameState.GetSelectedAsyncChallengeDeckUnits().Select(unit => unit.Id).Distinct(StringComparer.OrdinalIgnoreCase).Take(3).ToArray()
			: [];
		return new OnlineRoomCreateRequest
		{
			BoardCode = challenge.Code,
			BoardTitle = $"{stage.MapName} S{stage.StageNumber} {stage.StageName}",
			PlayerProfileId = gameState.PlayerProfileId,
			PlayerCallsign = gameState.PlayerCallsign,
			Region = "global",
			UsesLockedDeck = gameState.HasSelectedAsyncChallengeLockedDeck,
			LockedDeckUnitIds = lockedDeckUnitIds,
			RequestedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
		};
	}

	private static OnlineRoomCreateResult NormalizeResult(OnlineRoomCreateResult result, OnlineRoomCreateRequest request)
	{
		var boardCode = AsyncChallengeCatalog.NormalizeCode(string.IsNullOrWhiteSpace(result?.BoardCode) ? request.BoardCode : result.BoardCode);
		var roomId = string.IsNullOrWhiteSpace(result?.RoomId)
			? $"ROOM-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}"
			: result.RoomId.Trim();
		var lockedDeckUnitIds = NormalizeLockedDeck(result?.UsesLockedDeck == true
			? (result.LockedDeckUnitIds?.Length > 0 ? result.LockedDeckUnitIds : request.LockedDeckUnitIds)
			: request.LockedDeckUnitIds);
		var usesLockedDeck = result?.UsesLockedDeck == true || lockedDeckUnitIds.Count > 0;
		var title = string.IsNullOrWhiteSpace(result?.Title) ? $"{request.PlayerCallsign} Relay" : result.Title.Trim();
		var hostTicket = NormalizeHostTicket(result?.HostTicket, roomId, title, boardCode, request, usesLockedDeck, lockedDeckUnitIds);
		return new OnlineRoomCreateResult
		{
			ProviderId = string.IsNullOrWhiteSpace(result?.ProviderId) ? ResolveProvider().Id : result.ProviderId,
			ProviderDisplayName = string.IsNullOrWhiteSpace(result?.ProviderDisplayName) ? ResolveProvider().DisplayName : result.ProviderDisplayName,
			RoomId = roomId,
			Title = title,
			Summary = string.IsNullOrWhiteSpace(result?.Summary)
				? $"Hosted online room {title} for {boardCode}."
				: result.Summary.Trim(),
			HostCallsign = string.IsNullOrWhiteSpace(result?.HostCallsign) ? request.PlayerCallsign : result.HostCallsign.Trim(),
			BoardCode = boardCode,
			BoardTitle = string.IsNullOrWhiteSpace(result?.BoardTitle) ? request.BoardTitle : result.BoardTitle.Trim(),
			CurrentPlayers = Math.Max(1, result?.CurrentPlayers ?? 1),
			MaxPlayers = Math.Max(Math.Max(1, result?.MaxPlayers ?? 4), Math.Max(1, result?.CurrentPlayers ?? 1)),
			SpectatorCount = Math.Max(0, result?.SpectatorCount ?? 0),
			Status = string.IsNullOrWhiteSpace(result?.Status) ? "lobby" : result.Status.Trim(),
			Region = string.IsNullOrWhiteSpace(result?.Region) ? request.Region : result.Region.Trim(),
			TransportHint = string.IsNullOrWhiteSpace(result?.TransportHint) ? hostTicket.TransportHint : result.TransportHint.Trim(),
			RelayEndpoint = string.IsNullOrWhiteSpace(result?.RelayEndpoint) ? hostTicket.RelayEndpoint : result.RelayEndpoint.Trim(),
			UsesLockedDeck = usesLockedDeck,
			LockedDeckUnitIds = usesLockedDeck ? lockedDeckUnitIds.ToArray() : [],
			HostTicket = hostTicket
		};
	}

	private static OnlineRoomJoinTicket NormalizeHostTicket(
		OnlineRoomJoinTicket ticket,
		string roomId,
		string roomTitle,
		string boardCode,
		OnlineRoomCreateRequest request,
		bool usesLockedDeck,
		IReadOnlyList<string> lockedDeckUnitIds)
	{
		var requestedAt = ticket?.RequestedAtUnixSeconds > 0
			? ticket.RequestedAtUnixSeconds
			: request.RequestedAtUnixSeconds;
		return new OnlineRoomJoinTicket
		{
			ProviderId = string.IsNullOrWhiteSpace(ticket?.ProviderId) ? ResolveProvider().Id : ticket.ProviderId,
			ProviderDisplayName = string.IsNullOrWhiteSpace(ticket?.ProviderDisplayName) ? ResolveProvider().DisplayName : ticket.ProviderDisplayName,
			RoomId = string.IsNullOrWhiteSpace(ticket?.RoomId) ? roomId : ticket.RoomId.Trim(),
			RoomTitle = string.IsNullOrWhiteSpace(ticket?.RoomTitle) ? roomTitle : ticket.RoomTitle.Trim(),
			BoardCode = boardCode,
			Status = string.IsNullOrWhiteSpace(ticket?.Status) ? "hosted" : ticket.Status.Trim().ToLowerInvariant(),
			Summary = string.IsNullOrWhiteSpace(ticket?.Summary)
				? $"Hosted room {roomTitle} is ready to accept join requests."
				: ticket.Summary.Trim(),
			TicketId = string.IsNullOrWhiteSpace(ticket?.TicketId) ? $"HOST-{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}" : ticket.TicketId.Trim(),
			JoinToken = string.IsNullOrWhiteSpace(ticket?.JoinToken) ? $"host-{Guid.NewGuid():N}" : ticket.JoinToken.Trim(),
			TransportHint = string.IsNullOrWhiteSpace(ticket?.TransportHint) ? "internet_room_hosted" : ticket.TransportHint.Trim(),
			RelayEndpoint = string.IsNullOrWhiteSpace(ticket?.RelayEndpoint) ? "pending relay" : ticket.RelayEndpoint.Trim(),
			SeatLabel = string.IsNullOrWhiteSpace(ticket?.SeatLabel) ? "host seat" : ticket.SeatLabel.Trim(),
			RequestedAtUnixSeconds = requestedAt,
			ExpiresAtUnixSeconds = ticket?.ExpiresAtUnixSeconds > 0 ? ticket.ExpiresAtUnixSeconds : requestedAt + 3600,
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

		return normalized;
	}

	private static string BuildLockedDeckSummary(IEnumerable<string> lockedDeckUnitIds)
	{
		var labels = (lockedDeckUnitIds ?? Array.Empty<string>())
			.Select(unitId => GameData.GetUnit(unitId).DisplayName)
			.Where(label => !string.IsNullOrWhiteSpace(label))
			.ToArray();
		return labels.Length == 0
			? "locked shared squad"
			: $"locked shared squad ({string.Join(", ", labels)})";
	}

	private static IOnlineRoomCreateProvider ResolveProvider()
	{
		var providerId = ChallengeSyncProviderCatalog.NormalizeId(GameState.Instance?.ChallengeSyncProviderId ?? "");
		return providerId == ChallengeSyncProviderCatalog.HttpApiId
			? new HttpApiOnlineRoomCreateProvider(BuildHttpEndpoint(GameState.Instance?.ChallengeSyncEndpoint ?? ""))
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
			return normalized[..^"/challenge-sync".Length] + "/challenge-room-create";
		}

		return normalized.TrimEnd('/') + "/challenge-room-create";
	}
}
