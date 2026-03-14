using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;

public sealed class HttpApiOnlineRoomMatchmakeProvider : IOnlineRoomMatchmakeProvider
{
	private static readonly HttpClient Client = new()
	{
		Timeout = TimeSpan.FromSeconds(6)
	};

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = false,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		PropertyNameCaseInsensitive = true
	};

	private readonly string _endpointUrl;

	public HttpApiOnlineRoomMatchmakeProvider(string endpointUrl)
	{
		_endpointUrl = endpointUrl?.Trim() ?? "";
	}

	public string Id => ChallengeSyncProviderCatalog.HttpApiId;
	public string DisplayName => "HTTP Matchmaker";

	public string BuildLocationSummary()
	{
		return string.IsNullOrWhiteSpace(_endpointUrl)
			? "Endpoint: not configured"
			: $"Endpoint: {_endpointUrl}";
	}

	public OnlineRoomMatchmakeResult Matchmake(AsyncChallengeDefinition challenge, OnlineRoomMatchmakeRequest request)
	{
		if (string.IsNullOrWhiteSpace(_endpointUrl))
		{
			throw new InvalidOperationException("HTTP room-matchmake endpoint is not configured.");
		}

		var requestBody = new
		{
			matchmake = request
		};
		var requestJson = JsonSerializer.Serialize(requestBody, JsonOptions);

		using var message = new HttpRequestMessage(HttpMethod.Post, _endpointUrl);
		message.Headers.TryAddWithoutValidation("X-Convoy-Profile", request.PlayerProfileId);
		message.Headers.TryAddWithoutValidation("X-Convoy-Callsign", request.PlayerCallsign);
		message.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

		using var response = Client.Send(message);
		var responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
		}

		if (string.IsNullOrWhiteSpace(responseBody))
		{
			throw new InvalidOperationException("HTTP room-matchmake response was empty.");
		}

		using var document = JsonDocument.Parse(responseBody);
		var root = document.RootElement;
		var room = ParseRoom(root, challenge, request);
		var joinTicket = ParseJoinTicket(root, room, request);
		return new OnlineRoomMatchmakeResult
		{
			ProviderId = Id,
			ProviderDisplayName = DisplayName,
			Status = GetString(root, "status", "accepted"),
			Summary = GetString(root, "message", $"Matched into {room.Title}."),
			CreatedNewRoom = GetBool(root, "createdNewRoom", false),
			Room = room,
			JoinTicket = joinTicket
		};
	}

	private static OnlineRoomDirectoryEntry ParseRoom(JsonElement root, AsyncChallengeDefinition challenge, OnlineRoomMatchmakeRequest request)
	{
		if (!TryGetProperty(root, "room", out var roomElement) || roomElement.ValueKind != JsonValueKind.Object)
		{
			var boardTitleFallback = $"Stage {challenge.Stage} {challenge.Code}";
			return new OnlineRoomDirectoryEntry
			{
				RoomId = $"ROOM-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
				Title = request.WantsLockedDeckSeat ? "Matched Lockstep Room" : "Matched Relay Room",
				Summary = "Matched room listing.",
				HostCallsign = "MatchRelay",
				BoardCode = challenge.Code,
				BoardTitle = boardTitleFallback,
				CurrentPlayers = 2,
				MaxPlayers = 4,
				SpectatorCount = 0,
				Status = "lobby",
				Region = string.IsNullOrWhiteSpace(request.Region) ? "global" : request.Region,
				UsesLockedDeck = request.WantsLockedDeckSeat,
				LockedDeckUnitIds = []
			};
		}

		return new OnlineRoomDirectoryEntry
		{
			RoomId = GetString(roomElement, "roomId", $"ROOM-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}"),
			Title = GetString(roomElement, "title", request.WantsLockedDeckSeat ? "Matched Lockstep Room" : "Matched Relay Room"),
			Summary = GetString(roomElement, "summary", "Matched room listing."),
			HostCallsign = GetString(roomElement, "hostCallsign", "MatchRelay"),
			BoardCode = GetString(roomElement, "boardCode", challenge.Code),
			BoardTitle = GetString(roomElement, "boardTitle", $"Stage {challenge.Stage} {challenge.Code}"),
			CurrentPlayers = GetInt(roomElement, "currentPlayers", 2),
			MaxPlayers = GetInt(roomElement, "maxPlayers", 4),
			SpectatorCount = GetInt(roomElement, "spectatorCount", 0),
			Status = GetString(roomElement, "status", "lobby"),
			Region = GetString(roomElement, "region", string.IsNullOrWhiteSpace(request.Region) ? "global" : request.Region),
			UsesLockedDeck = GetBool(roomElement, "usesLockedDeck", request.WantsLockedDeckSeat),
			LockedDeckUnitIds = GetStringArray(roomElement, "lockedDeckUnitIds")
		};
	}

	private static OnlineRoomJoinTicket ParseJoinTicket(JsonElement root, OnlineRoomDirectoryEntry room, OnlineRoomMatchmakeRequest request)
	{
		if (!TryGetProperty(root, "joinTicket", out var ticketElement) || ticketElement.ValueKind != JsonValueKind.Object)
		{
			return new OnlineRoomJoinTicket
			{
				ProviderId = ChallengeSyncProviderCatalog.HttpApiId,
				ProviderDisplayName = "HTTP Matchmaker",
				RoomId = room.RoomId,
				RoomTitle = room.Title,
				BoardCode = room.BoardCode,
				Status = "accepted",
				Summary = $"Matched into {room.Title}.",
				TicketId = $"JOIN-{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}",
				JoinToken = $"matched-{Guid.NewGuid():N}",
				TransportHint = "internet_room_pending",
				RelayEndpoint = "pending relay",
				SeatLabel = room.UsesLockedDeck ? "locked-squad seat" : "player-squad seat",
				RequestedAtUnixSeconds = request.RequestedAtUnixSeconds,
				ExpiresAtUnixSeconds = request.RequestedAtUnixSeconds + 180,
				UsesLockedDeck = room.UsesLockedDeck,
				LockedDeckUnitIds = room.UsesLockedDeck ? room.LockedDeckUnitIds : []
			};
		}

		return new OnlineRoomJoinTicket
		{
			ProviderId = ChallengeSyncProviderCatalog.HttpApiId,
			ProviderDisplayName = "HTTP Matchmaker",
			RoomId = GetString(ticketElement, "roomId", room.RoomId),
			RoomTitle = GetString(ticketElement, "roomTitle", room.Title),
			BoardCode = GetString(ticketElement, "boardCode", room.BoardCode),
			Status = GetString(ticketElement, "status", "accepted"),
			Summary = GetString(ticketElement, "message", $"Matched into {room.Title}."),
			TicketId = GetString(ticketElement, "ticketId", $"JOIN-{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}"),
			JoinToken = GetString(ticketElement, "joinToken", $"matched-{Guid.NewGuid():N}"),
			TransportHint = GetString(ticketElement, "transportHint", "internet_room_pending"),
			RelayEndpoint = GetString(ticketElement, "relayEndpoint", "pending relay"),
			SeatLabel = GetString(ticketElement, "seatLabel", room.UsesLockedDeck ? "locked-squad seat" : "player-squad seat"),
			RequestedAtUnixSeconds = GetLong(ticketElement, "requestedAtUnixSeconds", request.RequestedAtUnixSeconds),
			ExpiresAtUnixSeconds = GetLong(ticketElement, "expiresAtUnixSeconds", request.RequestedAtUnixSeconds + 180),
			UsesLockedDeck = GetBool(ticketElement, "usesLockedDeck", room.UsesLockedDeck),
			LockedDeckUnitIds = GetStringArray(ticketElement, "lockedDeckUnitIds")
		};
	}

	private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
	{
		foreach (var property in element.EnumerateObject())
		{
			if (property.NameEquals(propertyName) || property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
			{
				value = property.Value;
				return true;
			}
		}

		value = default;
		return false;
	}

	private static string GetString(JsonElement element, string propertyName, string fallback)
	{
		return TryGetProperty(element, propertyName, out var value) && value.ValueKind == JsonValueKind.String
			? value.GetString() ?? fallback
			: fallback;
	}

	private static int GetInt(JsonElement element, string propertyName, int fallback)
	{
		return TryGetProperty(element, propertyName, out var value) && value.TryGetInt32(out var parsed)
			? parsed
			: fallback;
	}

	private static long GetLong(JsonElement element, string propertyName, long fallback)
	{
		return TryGetProperty(element, propertyName, out var value) && value.TryGetInt64(out var parsed)
			? parsed
			: fallback;
	}

	private static bool GetBool(JsonElement element, string propertyName, bool fallback)
	{
		return TryGetProperty(element, propertyName, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
			? value.GetBoolean()
			: fallback;
	}

	private static string[] GetStringArray(JsonElement element, string propertyName)
	{
		if (!TryGetProperty(element, propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
		{
			return [];
		}

		var result = new System.Collections.Generic.List<string>();
		foreach (var item in value.EnumerateArray())
		{
			if (item.ValueKind != JsonValueKind.String)
			{
				continue;
			}

			var text = item.GetString();
			if (!string.IsNullOrWhiteSpace(text))
			{
				result.Add(text);
			}
		}

		return result.ToArray();
	}
}
