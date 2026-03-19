using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;

public sealed class HttpApiOnlineRoomTelemetryProvider : IOnlineRoomTelemetryProvider
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

	public HttpApiOnlineRoomTelemetryProvider(string endpointUrl)
	{
		_endpointUrl = endpointUrl?.Trim() ?? "";
	}

	public string Id => ChallengeSyncProviderCatalog.HttpApiId;
	public string DisplayName => "HTTP Room Telemetry";

	public string BuildLocationSummary()
	{
		return string.IsNullOrWhiteSpace(_endpointUrl)
			? "Endpoint: not configured"
			: $"Endpoint: {_endpointUrl}";
	}

	public OnlineRoomTelemetrySubmission SubmitTelemetry(OnlineRoomJoinTicket ticket, OnlineRoomTelemetryRequest request)
	{
		if (string.IsNullOrWhiteSpace(_endpointUrl))
		{
			throw new InvalidOperationException("HTTP room-telemetry endpoint is not configured.");
		}

		var requestBody = new
		{
			telemetry = request
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

		if (string.IsNullOrWhiteSpace(responseBody))
		{
			throw new InvalidOperationException("HTTP room-telemetry response was empty.");
		}

		using var document = JsonDocument.Parse(responseBody);
		var root = document.RootElement;
		return new OnlineRoomTelemetrySubmission
		{
			ProviderId = Id,
			ProviderDisplayName = DisplayName,
			RoomId = GetString(root, "roomId", ticket.RoomId),
			BoardCode = GetString(root, "boardCode", ticket.BoardCode),
			TicketId = GetString(root, "ticketId", ticket.TicketId),
			Status = GetString(root, "status", "accepted"),
			Summary = GetString(root, "message", $"Accepted room telemetry for {ticket.RoomTitle}."),
			ProcessedAtUnixSeconds = GetLong(root, "processedAtUnixSeconds", request.RequestedAtUnixSeconds)
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

	private static long GetLong(JsonElement element, string propertyName, long fallback)
	{
		return TryGetProperty(element, propertyName, out var value) && value.TryGetInt64(out var parsed)
			? parsed
			: fallback;
	}
}
