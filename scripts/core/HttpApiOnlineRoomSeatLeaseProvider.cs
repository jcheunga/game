using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;

public sealed class HttpApiOnlineRoomSeatLeaseProvider : IOnlineRoomSeatLeaseProvider
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

	public HttpApiOnlineRoomSeatLeaseProvider(string endpointUrl)
	{
		_endpointUrl = endpointUrl?.Trim() ?? "";
	}

	public string Id => ChallengeSyncProviderCatalog.HttpApiId;
	public string DisplayName => "HTTP Seat Lease";

	public string BuildLocationSummary()
	{
		return string.IsNullOrWhiteSpace(_endpointUrl)
			? "Endpoint: not configured"
			: $"Endpoint: {_endpointUrl}";
	}

	public OnlineRoomSeatLeaseResult RenewSeat(OnlineRoomJoinTicket ticket, OnlineRoomSeatLeaseRequest request)
	{
		if (string.IsNullOrWhiteSpace(_endpointUrl))
		{
			throw new InvalidOperationException("HTTP room-seat lease endpoint is not configured.");
		}

		var requestBody = new OnlineRoomSeatLeaseApiRequest
		{
			Lease = request
		};
		var requestJson = JsonSerializer.Serialize(requestBody, JsonOptions);

		using var message = new HttpRequestMessage(HttpMethod.Post, _endpointUrl);
		message.Headers.TryAddWithoutValidation("X-Convoy-Profile", request.PlayerProfileId);
		message.Headers.TryAddWithoutValidation("X-Join-Ticket", request.TicketId);
		message.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

		using var response = Client.Send(message);
		var responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
		}

		var parsed = string.IsNullOrWhiteSpace(responseBody)
			? null
			: JsonSerializer.Deserialize<OnlineRoomSeatLeaseApiResponse>(responseBody, JsonOptions);
		return new OnlineRoomSeatLeaseResult
		{
			ProviderId = Id,
			ProviderDisplayName = DisplayName,
			RoomId = string.IsNullOrWhiteSpace(parsed?.RoomId) ? ticket.RoomId : parsed.RoomId,
			BoardCode = string.IsNullOrWhiteSpace(parsed?.BoardCode) ? ticket.BoardCode : parsed.BoardCode,
			TicketId = string.IsNullOrWhiteSpace(parsed?.TicketId) ? ticket.TicketId : parsed.TicketId,
			JoinToken = string.IsNullOrWhiteSpace(parsed?.JoinToken) ? ticket.JoinToken : parsed.JoinToken,
			Status = string.IsNullOrWhiteSpace(parsed?.Status) ? "accepted" : parsed.Status,
			Summary = string.IsNullOrWhiteSpace(parsed?.Message)
				? $"Refreshed room seat lease for {ticket.RoomTitle}."
				: parsed.Message,
			ExpiresAtUnixSeconds = parsed?.ExpiresAtUnixSeconds ?? request.RequestedAtUnixSeconds,
			RenewedAtUnixSeconds = parsed?.RenewedAtUnixSeconds ?? request.RequestedAtUnixSeconds
		};
	}
}
