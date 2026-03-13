using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;

public sealed class HttpApiOnlineRoomJoinProvider : IOnlineRoomJoinProvider
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

	public HttpApiOnlineRoomJoinProvider(string endpointUrl)
	{
		_endpointUrl = endpointUrl?.Trim() ?? "";
	}

	public string Id => ChallengeSyncProviderCatalog.HttpApiId;
	public string DisplayName => "HTTP Join";

	public string BuildLocationSummary()
	{
		return string.IsNullOrWhiteSpace(_endpointUrl)
			? "Endpoint: not configured"
			: $"Endpoint: {_endpointUrl}";
	}

	public OnlineRoomJoinTicket RequestJoin(OnlineRoomDirectoryEntry room, OnlineRoomJoinRequest request)
	{
		if (string.IsNullOrWhiteSpace(_endpointUrl))
		{
			throw new InvalidOperationException("HTTP room-join endpoint is not configured.");
		}

		var requestBody = new OnlineRoomJoinApiRequest
		{
			Join = request
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

		var parsed = string.IsNullOrWhiteSpace(responseBody)
			? null
			: JsonSerializer.Deserialize<OnlineRoomJoinApiResponse>(responseBody, JsonOptions);
		return new OnlineRoomJoinTicket
		{
			ProviderId = Id,
			ProviderDisplayName = DisplayName,
			RoomId = string.IsNullOrWhiteSpace(parsed?.RoomId) ? room.RoomId : parsed.RoomId,
			RoomTitle = string.IsNullOrWhiteSpace(parsed?.RoomTitle) ? room.Title : parsed.RoomTitle,
			BoardCode = string.IsNullOrWhiteSpace(parsed?.BoardCode) ? room.BoardCode : parsed.BoardCode,
			Status = string.IsNullOrWhiteSpace(parsed?.Status) ? "accepted" : parsed.Status,
			Summary = string.IsNullOrWhiteSpace(parsed?.Message)
				? $"Requested online join ticket for {room.Title}."
				: parsed.Message,
			TicketId = parsed?.TicketId ?? "",
			JoinToken = parsed?.JoinToken ?? "",
			TransportHint = parsed?.TransportHint ?? "",
			RelayEndpoint = parsed?.RelayEndpoint ?? "",
			SeatLabel = parsed?.SeatLabel ?? "",
			RequestedAtUnixSeconds = request.RequestedAtUnixSeconds,
			ExpiresAtUnixSeconds = parsed?.ExpiresAtUnixSeconds ?? 0,
			UsesLockedDeck = parsed?.UsesLockedDeck ?? room.UsesLockedDeck,
			LockedDeckUnitIds = parsed?.LockedDeckUnitIds ?? []
		};
	}
}
