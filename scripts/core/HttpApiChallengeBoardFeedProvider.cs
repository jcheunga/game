using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;

public sealed class HttpApiChallengeBoardFeedProvider : IChallengeBoardFeedProvider
{
	private static readonly HttpClient Client = new()
	{
		Timeout = TimeSpan.FromSeconds(15)
	};

	private readonly string _endpointUrl;

	public HttpApiChallengeBoardFeedProvider(string endpointUrl)
	{
		_endpointUrl = endpointUrl?.Trim() ?? "";
	}

	public string Id => ChallengeSyncProviderCatalog.HttpApiId;
	public string DisplayName => "HTTP Feed";

	public string BuildLocationSummary()
	{
		return string.IsNullOrWhiteSpace(_endpointUrl)
			? "Endpoint: not configured"
			: $"Endpoint: {_endpointUrl}";
	}

	public ChallengeBoardFeedSnapshot FetchFeed(int highestUnlockedStage, int maxStage, int limit)
	{
		if (string.IsNullOrWhiteSpace(_endpointUrl))
		{
			throw new InvalidOperationException("HTTP challenge feed endpoint is not configured.");
		}

		var url =
			$"{_endpointUrl}?stageCap={Math.Max(1, highestUnlockedStage)}&maxStage={Math.Max(1, maxStage)}&limit={Math.Max(1, limit)}";
		var response = Client.GetAsync(url).GetAwaiter().GetResult();
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
		}

		var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
		var items = new List<ChallengeBoardFeedItem>();
		var status = "ok";
		var message = "";
		if (!string.IsNullOrWhiteSpace(body))
		{
			using var document = JsonDocument.Parse(body);
			var root = document.RootElement;
			status = GetString(root, "status", "ok");
			message = GetString(root, "message", "");
			if (TryGetProperty(root, "items", out var itemsElement) && itemsElement.ValueKind == JsonValueKind.Array)
			{
				foreach (var item in itemsElement.EnumerateArray())
				{
					items.Add(new ChallengeBoardFeedItem
					{
						Id = GetString(item, "id", Guid.NewGuid().ToString("N")),
						Title = GetString(item, "title", "Remote Board"),
						Summary = GetString(item, "summary", "Backend-authored async challenge board."),
						Code = GetString(item, "code", ""),
						LockedDeckUnitIds = GetStringArray(item, "lockedDeckUnitIds")
					});
				}
			}
		}

		return new ChallengeBoardFeedSnapshot
		{
			ProviderId = Id,
			ProviderDisplayName = DisplayName,
			Status = status,
			Summary = string.IsNullOrWhiteSpace(message)
				? $"Fetched {items.Count} remote feed entr{(items.Count == 1 ? "y" : "ies")}."
				: message,
			FetchedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
			Items = items
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

	private static string[] GetStringArray(JsonElement element, string propertyName)
	{
		if (!TryGetProperty(element, propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
		{
			return [];
		}

		var result = new List<string>();
		foreach (var item in value.EnumerateArray())
		{
			if (item.ValueKind == JsonValueKind.String)
			{
				var text = item.GetString();
				if (!string.IsNullOrWhiteSpace(text))
				{
					result.Add(text);
				}
			}
		}

		return result.ToArray();
	}
}
