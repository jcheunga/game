using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;

public sealed class HttpApiDailyLeaderboardProvider
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

	private readonly string _baseUrl;

	public HttpApiDailyLeaderboardProvider(string baseUrl)
	{
		_baseUrl = baseUrl?.Trim().TrimEnd('/') ?? "";
	}

	public string Id => ChallengeSyncProviderCatalog.HttpApiId;
	public string DisplayName => "HTTP Daily";

	public string BuildLocationSummary()
	{
		return string.IsNullOrWhiteSpace(_baseUrl)
			? "Endpoint: not configured"
			: $"Endpoint: {_baseUrl}";
	}

	public DailyCompleteResult SubmitCompletion(string profileId, string date, int score)
	{
		if (string.IsNullOrWhiteSpace(_baseUrl))
		{
			throw new InvalidOperationException("HTTP daily endpoint is not configured.");
		}

		var requestBody = new
		{
			profileId,
			date,
			score
		};
		var requestJson = JsonSerializer.Serialize(requestBody, JsonOptions);

		using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/daily/complete");
		request.Headers.TryAddWithoutValidation("X-Convoy-Profile", profileId);
		request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

		using var response = Client.Send(request);
		var responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
		}

		var personalBest = 0;
		var isNewBest = false;
		if (!string.IsNullOrWhiteSpace(responseBody))
		{
			using var document = JsonDocument.Parse(responseBody);
			var root = document.RootElement;
			personalBest = GetInt(root, "personalBest", 0);
			isNewBest = GetBool(root, "isNewBest", false);
		}

		return new DailyCompleteResult
		{
			PersonalBest = personalBest,
			IsNewBest = isNewBest
		};
	}

	public DailyLeaderboardSnapshot FetchLeaderboard(string date)
	{
		if (string.IsNullOrWhiteSpace(_baseUrl))
		{
			throw new InvalidOperationException("HTTP daily leaderboard endpoint is not configured.");
		}

		var encodedDate = Uri.EscapeDataString(date);
		var response = Client.GetAsync($"{_baseUrl}/daily/leaderboard/{encodedDate}").GetAwaiter().GetResult();
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
		}

		var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
		var entries = new List<DailyLeaderboardEntry>();
		var responseDate = date;

		if (!string.IsNullOrWhiteSpace(body))
		{
			using var document = JsonDocument.Parse(body);
			var root = document.RootElement;
			responseDate = GetString(root, "date", date);
			if (TryGetProperty(root, "entries", out var entriesElement) && entriesElement.ValueKind == JsonValueKind.Array)
			{
				foreach (var item in entriesElement.EnumerateArray())
				{
					entries.Add(new DailyLeaderboardEntry
					{
						ProfileId = GetString(item, "profileId", ""),
						Score = GetInt(item, "score", 0),
						CompletedAt = GetString(item, "completedAt", "")
					});
				}
			}
		}

		return new DailyLeaderboardSnapshot
		{
			Date = responseDate,
			Entries = entries
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
}

public class DailyCompleteResult
{
	public int PersonalBest { get; set; }
	public bool IsNewBest { get; set; }
}

public class DailyLeaderboardSnapshot
{
	public string Date { get; set; } = "";
	public List<DailyLeaderboardEntry> Entries { get; set; } = new();
}

public class DailyLeaderboardEntry
{
	public string ProfileId { get; set; } = "";
	public int Score { get; set; }
	public string CompletedAt { get; set; } = "";
}
