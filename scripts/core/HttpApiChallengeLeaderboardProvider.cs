using System;
using System.Net.Http;
using System.Text.Json;

public sealed class HttpApiChallengeLeaderboardProvider : IChallengeLeaderboardProvider
{
	private static readonly HttpClient Client = new()
	{
		Timeout = TimeSpan.FromSeconds(6)
	};

	private readonly string _endpointUrl;

	public HttpApiChallengeLeaderboardProvider(string endpointUrl)
	{
		_endpointUrl = endpointUrl?.Trim() ?? "";
	}

	public string Id => ChallengeSyncProviderCatalog.HttpApiId;
	public string DisplayName => "HTTP API";

	public string BuildLocationSummary()
	{
		return string.IsNullOrWhiteSpace(_endpointUrl)
			? "Endpoint: not configured"
			: $"Endpoint: {_endpointUrl}";
	}

	public ChallengeLeaderboardSnapshot FetchLeaderboard(string code, int limit)
	{
		if (string.IsNullOrWhiteSpace(_endpointUrl))
		{
			throw new InvalidOperationException("HTTP leaderboard endpoint is not configured.");
		}

		var normalizedCode = AsyncChallengeCatalog.NormalizeCode(code);
		var encodedCode = Uri.EscapeDataString(normalizedCode);
		var response = Client.GetAsync($"{_endpointUrl}?code={encodedCode}&limit={Math.Max(1, limit)}").GetAwaiter().GetResult();
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
		}

		var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
		var entries = new System.Collections.Generic.List<ChallengeLeaderboardEntry>();
		var status = "ok";
		var message = "";
		if (!string.IsNullOrWhiteSpace(body))
		{
			using var document = JsonDocument.Parse(body);
			var root = document.RootElement;
			status = GetString(root, "status", "ok");
			message = GetString(root, "message", "");
			if (TryGetProperty(root, "entries", out var entriesElement) && entriesElement.ValueKind == JsonValueKind.Array)
			{
				var rank = 1;
				foreach (var item in entriesElement.EnumerateArray())
				{
					entries.Add(new ChallengeLeaderboardEntry
					{
						Rank = GetInt(item, "rank", rank),
						Code = normalizedCode,
						PlayerCallsign = GetString(item, "playerCallsign", "Convoy"),
						PlayerProfileId = GetString(item, "playerProfileId", ""),
						Score = GetInt(item, "score", 0),
						StarsEarned = GetInt(item, "starsEarned", 0),
						HullPercent = GetInt(item, "hullPercent", 0),
						ElapsedSeconds = GetFloat(item, "elapsedSeconds", 0f),
						UsedLockedDeck = GetBool(item, "usedLockedDeck", false),
						PlayedAtUnixSeconds = GetLong(item, "playedAtUnixSeconds", 0L)
					});
					rank++;
				}
			}
		}

		return new ChallengeLeaderboardSnapshot
		{
			Code = normalizedCode,
			ProviderId = Id,
			ProviderDisplayName = DisplayName,
			Status = status,
			Summary = string.IsNullOrWhiteSpace(message)
				? $"Fetched {entries.Count} remote leaderboard entr{(entries.Count == 1 ? "y" : "ies")}."
				: message,
			FetchedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
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

	private static long GetLong(JsonElement element, string propertyName, long fallback)
	{
		return TryGetProperty(element, propertyName, out var value) && value.TryGetInt64(out var parsed)
			? parsed
			: fallback;
	}

	private static float GetFloat(JsonElement element, string propertyName, float fallback)
	{
		if (!TryGetProperty(element, propertyName, out var value))
		{
			return fallback;
		}

		if (value.TryGetSingle(out var parsedSingle))
		{
			return parsedSingle;
		}

		return value.TryGetDouble(out var parsedDouble)
			? (float)parsedDouble
			: fallback;
	}

	private static bool GetBool(JsonElement element, string propertyName, bool fallback)
	{
		return TryGetProperty(element, propertyName, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
			? value.GetBoolean()
			: fallback;
	}
}
