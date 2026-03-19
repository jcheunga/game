using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;

public sealed class HttpApiAchievementSyncProvider
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

	public HttpApiAchievementSyncProvider(string endpointUrl)
	{
		_endpointUrl = endpointUrl?.Trim() ?? "";
	}

	public string Id => ChallengeSyncProviderCatalog.HttpApiId;
	public string DisplayName => "HTTP Achievements";

	public string BuildLocationSummary()
	{
		return string.IsNullOrWhiteSpace(_endpointUrl)
			? "Endpoint: not configured"
			: $"Endpoint: {_endpointUrl}";
	}

	public AchievementSyncResult SyncAchievements(string profileId, string[] achievementIds)
	{
		if (string.IsNullOrWhiteSpace(_endpointUrl))
		{
			throw new InvalidOperationException("HTTP achievements endpoint is not configured.");
		}

		var requestBody = new
		{
			profileId,
			achievementIds
		};
		var requestJson = JsonSerializer.Serialize(requestBody, JsonOptions);

		using var request = new HttpRequestMessage(HttpMethod.Post, _endpointUrl);
		request.Headers.TryAddWithoutValidation("X-Convoy-Profile", profileId);
		request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

		using var response = Client.Send(request);
		var responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
		}

		var synced = 0;
		var total = 0;
		if (!string.IsNullOrWhiteSpace(responseBody))
		{
			using var document = JsonDocument.Parse(responseBody);
			var root = document.RootElement;
			synced = GetInt(root, "synced", 0);
			total = GetInt(root, "total", 0);
		}

		return new AchievementSyncResult
		{
			Synced = synced,
			Total = total
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

	private static int GetInt(JsonElement element, string propertyName, int fallback)
	{
		return TryGetProperty(element, propertyName, out var value) && value.TryGetInt32(out var parsed)
			? parsed
			: fallback;
	}
}

public class AchievementSyncResult
{
	public int Synced { get; set; }
	public int Total { get; set; }
}
