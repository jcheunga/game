using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;

public sealed class HttpApiOnlineRoomDirectoryProvider : IOnlineRoomDirectoryProvider
{
	private static readonly HttpClient Client = new()
	{
		Timeout = TimeSpan.FromSeconds(6)
	};

	private readonly string _endpointUrl;

	public HttpApiOnlineRoomDirectoryProvider(string endpointUrl)
	{
		_endpointUrl = endpointUrl?.Trim() ?? "";
	}

	public string Id => ChallengeSyncProviderCatalog.HttpApiId;
	public string DisplayName => "HTTP Rooms";

	public string BuildLocationSummary()
	{
		return string.IsNullOrWhiteSpace(_endpointUrl)
			? "Endpoint: not configured"
			: $"Endpoint: {_endpointUrl}";
	}

	public OnlineRoomDirectorySnapshot FetchRooms(int highestUnlockedStage, int maxStage, int limit)
	{
		if (string.IsNullOrWhiteSpace(_endpointUrl))
		{
			throw new InvalidOperationException("HTTP room-directory endpoint is not configured.");
		}

		var url =
			$"{_endpointUrl}?stageCap={Math.Max(1, highestUnlockedStage)}&maxStage={Math.Max(1, maxStage)}&limit={Math.Max(1, limit)}";
		var response = Client.GetAsync(url).GetAwaiter().GetResult();
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
		}

		var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
		var items = new List<OnlineRoomDirectoryEntry>();
		var status = "ok";
		var message = "";
		if (!string.IsNullOrWhiteSpace(body))
		{
			using var document = JsonDocument.Parse(body);
			var root = document.RootElement;
			status = GetString(root, "status", "ok");
			message = GetString(root, "message", "");
			if (TryGetProperty(root, "rooms", out var roomsElement) && roomsElement.ValueKind == JsonValueKind.Array)
			{
				foreach (var item in roomsElement.EnumerateArray())
				{
					items.Add(new OnlineRoomDirectoryEntry
					{
						RoomId = GetString(item, "roomId", Guid.NewGuid().ToString("N")),
						Title = GetString(item, "title", "Remote Room"),
						Summary = GetString(item, "summary", "Internet room listing from the backend."),
						HostCallsign = GetString(item, "hostCallsign", "Lantern Host"),
						BoardCode = GetString(item, "boardCode", ""),
						BoardTitle = GetString(item, "boardTitle", ""),
						CurrentPlayers = GetInt(item, "currentPlayers", 1),
						MaxPlayers = GetInt(item, "maxPlayers", 4),
						SpectatorCount = GetInt(item, "spectatorCount", 0),
						Status = GetString(item, "status", "lobby"),
						Region = GetString(item, "region", "global"),
						UsesLockedDeck = GetBool(item, "usesLockedDeck", false),
						LockedDeckUnitIds = GetStringArray(item, "lockedDeckUnitIds")
					});
				}
			}
		}

		return new OnlineRoomDirectorySnapshot
		{
			ProviderId = Id,
			ProviderDisplayName = DisplayName,
			Status = status,
			Summary = string.IsNullOrWhiteSpace(message)
				? $"Fetched {items.Count} remote room entr{(items.Count == 1 ? "y" : "ies")}."
				: message,
			FetchedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
			Entries = items
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

		var result = new List<string>();
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
