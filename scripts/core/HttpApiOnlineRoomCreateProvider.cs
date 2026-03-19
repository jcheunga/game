using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;

public sealed class HttpApiOnlineRoomCreateProvider : IOnlineRoomCreateProvider
{
	private static readonly HttpClient Client = new()
	{
		Timeout = TimeSpan.FromSeconds(15)
	};

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = false,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		PropertyNameCaseInsensitive = true
	};

	private readonly string _endpointUrl;

	public HttpApiOnlineRoomCreateProvider(string endpointUrl)
	{
		_endpointUrl = endpointUrl?.Trim() ?? "";
	}

	public string Id => ChallengeSyncProviderCatalog.HttpApiId;
	public string DisplayName => "HTTP Room Host";

	public string BuildLocationSummary()
	{
		return string.IsNullOrWhiteSpace(_endpointUrl)
			? "Endpoint: not configured"
			: $"Endpoint: {_endpointUrl}";
	}

	public OnlineRoomCreateResult CreateRoom(OnlineRoomCreateRequest request)
	{
		if (string.IsNullOrWhiteSpace(_endpointUrl))
		{
			throw new InvalidOperationException("HTTP room-create endpoint is not configured.");
		}

		var requestBody = new
		{
			room = request
		};
		var requestJson = JsonSerializer.Serialize(requestBody, JsonOptions);

		using var message = new HttpRequestMessage(HttpMethod.Post, _endpointUrl);
		message.Headers.TryAddWithoutValidation("X-Convoy-Profile", request.PlayerProfileId);
		message.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

		using var response = Client.Send(message);
		var responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
		}

		if (string.IsNullOrWhiteSpace(responseBody))
		{
			throw new InvalidOperationException("HTTP room-create response was empty.");
		}

		using var document = JsonDocument.Parse(responseBody);
		var root = document.RootElement;
		var roomId = GetString(root, "roomId", $"ROOM-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}");
		var title = GetString(root, "title", $"{request.PlayerCallsign} Relay");
		var boardCode = AsyncChallengeCatalog.NormalizeCode(GetString(root, "boardCode", request.BoardCode));
		var lockedDeckUnitIds = GetStringArray(root, "lockedDeckUnitIds");
		var usesLockedDeck = GetBool(root, "usesLockedDeck", request.UsesLockedDeck || lockedDeckUnitIds.Length > 0);
		var hostTicket = ParseHostTicket(root, roomId, title, boardCode, request, usesLockedDeck, lockedDeckUnitIds);

		return new OnlineRoomCreateResult
		{
			ProviderId = Id,
			ProviderDisplayName = DisplayName,
			RoomId = roomId,
			Title = title,
			Summary = GetString(root, "message", $"Hosted internet room {title}."),
			HostCallsign = GetString(root, "hostCallsign", request.PlayerCallsign),
			BoardCode = boardCode,
			BoardTitle = GetString(root, "boardTitle", request.BoardTitle),
			CurrentPlayers = GetInt(root, "currentPlayers", 1),
			MaxPlayers = GetInt(root, "maxPlayers", 4),
			SpectatorCount = GetInt(root, "spectatorCount", 0),
			Status = GetString(root, "status", "lobby"),
			Region = GetString(root, "region", string.IsNullOrWhiteSpace(request.Region) ? "global" : request.Region),
			TransportHint = GetString(root, "transportHint", hostTicket.TransportHint),
			RelayEndpoint = GetString(root, "relayEndpoint", hostTicket.RelayEndpoint),
			UsesLockedDeck = usesLockedDeck,
			LockedDeckUnitIds = usesLockedDeck ? lockedDeckUnitIds : [],
			HostTicket = hostTicket
		};
	}

	private OnlineRoomJoinTicket ParseHostTicket(
		JsonElement root,
		string roomId,
		string roomTitle,
		string boardCode,
		OnlineRoomCreateRequest request,
		bool usesLockedDeck,
		string[] lockedDeckUnitIds)
	{
		var requestedAt = request.RequestedAtUnixSeconds > 0
			? request.RequestedAtUnixSeconds
			: DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		if (!TryGetProperty(root, "hostTicket", out var hostTicketElement) || hostTicketElement.ValueKind != JsonValueKind.Object)
		{
			return new OnlineRoomJoinTicket
			{
				ProviderId = Id,
				ProviderDisplayName = DisplayName,
				RoomId = roomId,
				RoomTitle = roomTitle,
				BoardCode = boardCode,
				Status = "hosted",
				Summary = $"Hosted room {roomTitle} is ready to accept join requests.",
				TicketId = $"HOST-{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}",
				JoinToken = $"host-{Guid.NewGuid():N}",
				TransportHint = GetString(root, "transportHint", "internet_room_hosted"),
				RelayEndpoint = GetString(root, "relayEndpoint", "pending relay"),
				SeatLabel = "host seat",
				RequestedAtUnixSeconds = requestedAt,
				ExpiresAtUnixSeconds = requestedAt + 3600,
				UsesLockedDeck = usesLockedDeck,
				LockedDeckUnitIds = usesLockedDeck ? lockedDeckUnitIds : []
			};
		}

		var nestedLockedDeck = GetStringArray(hostTicketElement, "lockedDeckUnitIds");
		return new OnlineRoomJoinTicket
		{
			ProviderId = Id,
			ProviderDisplayName = DisplayName,
			RoomId = roomId,
			RoomTitle = roomTitle,
			BoardCode = boardCode,
			Status = GetString(hostTicketElement, "status", "hosted"),
			Summary = GetString(hostTicketElement, "message", $"Hosted room {roomTitle} is ready to accept join requests."),
			TicketId = GetString(hostTicketElement, "ticketId", $"HOST-{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}"),
			JoinToken = GetString(hostTicketElement, "joinToken", $"host-{Guid.NewGuid():N}"),
			TransportHint = GetString(hostTicketElement, "transportHint", GetString(root, "transportHint", "internet_room_hosted")),
			RelayEndpoint = GetString(hostTicketElement, "relayEndpoint", GetString(root, "relayEndpoint", "pending relay")),
			SeatLabel = GetString(hostTicketElement, "seatLabel", "host seat"),
			RequestedAtUnixSeconds = GetLong(hostTicketElement, "requestedAtUnixSeconds", requestedAt),
			ExpiresAtUnixSeconds = GetLong(hostTicketElement, "expiresAtUnixSeconds", requestedAt + 3600),
			UsesLockedDeck = GetBool(hostTicketElement, "usesLockedDeck", usesLockedDeck || nestedLockedDeck.Length > 0),
			LockedDeckUnitIds = GetBool(hostTicketElement, "usesLockedDeck", usesLockedDeck || nestedLockedDeck.Length > 0)
				? (nestedLockedDeck.Length > 0 ? nestedLockedDeck : lockedDeckUnitIds)
				: []
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
