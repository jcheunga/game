using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;

public sealed class HttpApiOnlineRoomActionProvider : IOnlineRoomActionProvider
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

	public HttpApiOnlineRoomActionProvider(string endpointUrl)
	{
		_endpointUrl = endpointUrl?.Trim() ?? "";
	}

	public string Id => ChallengeSyncProviderCatalog.HttpApiId;
	public string DisplayName => "HTTP Action";

	public string BuildLocationSummary()
	{
		return string.IsNullOrWhiteSpace(_endpointUrl)
			? "Endpoint: not configured"
			: $"Endpoint: {_endpointUrl}";
	}

	public OnlineRoomActionResult SendAction(OnlineRoomJoinTicket ticket, OnlineRoomActionRequest request)
	{
		if (string.IsNullOrWhiteSpace(_endpointUrl))
		{
			throw new InvalidOperationException("HTTP room-action endpoint is not configured.");
		}

		var requestBody = new
		{
			action = request
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
			throw new InvalidOperationException("HTTP room-action response was empty.");
		}

		using var document = JsonDocument.Parse(responseBody);
		var root = document.RootElement;
		return new OnlineRoomActionResult
		{
			ProviderId = Id,
			ProviderDisplayName = DisplayName,
			RoomId = GetString(root, "roomId", ticket.RoomId),
			BoardCode = GetString(root, "boardCode", ticket.BoardCode),
			TicketId = GetString(root, "ticketId", ticket.TicketId),
			ActionId = GetString(root, "actionId", request.ActionId),
			Status = GetString(root, "status", "accepted"),
			Summary = GetString(root, "message", $"Processed room action for {ticket.RoomTitle}."),
			ReadyState = GetBool(root, "readyState", request.ReadyState),
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

	private static bool GetBool(JsonElement element, string propertyName, bool fallback)
	{
		return TryGetProperty(element, propertyName, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
			? value.GetBoolean()
			: fallback;
	}
}
