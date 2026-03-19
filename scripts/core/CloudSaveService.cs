using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Godot;

public sealed class CloudSaveSnapshot
{
	public string Status { get; set; } = "";
	public string Message { get; set; } = "";
	public string SaveData { get; set; } = "";
	public int SaveVersion { get; set; }
	public string SaveHash { get; set; } = "";
	public long UploadedAtUnixSeconds { get; set; }
	public long SizeBytes { get; set; }
}

public static class CloudSaveService
{
	private static readonly System.Net.Http.HttpClient Client = new()
	{
		Timeout = TimeSpan.FromSeconds(15)
	};

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = false,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		PropertyNameCaseInsensitive = true
	};

	private static readonly JsonSerializerOptions SaveJsonOptions = new()
	{
		WriteIndented = true
	};

	private static string _lastStatus = "";
	private static long _lastSyncTime;

	public static string LastStatus => _lastStatus;
	public static long LastSyncTimeUnix => _lastSyncTime;

	public static bool Upload(out string message)
	{
		message = "";
		var endpoint = GameState.Instance?.PurchaseValidationEndpoint ?? "";
		if (string.IsNullOrWhiteSpace(endpoint))
		{
			message = "Server endpoint not configured.";
			_lastStatus = message;
			return false;
		}

		var profileId = GameState.Instance?.PlayerProfileId ?? "";
		if (string.IsNullOrWhiteSpace(profileId))
		{
			message = "No player profile ID.";
			_lastStatus = message;
			return false;
		}

		try
		{
			var saveData = BuildSaveJson();
			if (string.IsNullOrWhiteSpace(saveData))
			{
				message = "Failed to serialize save data.";
				_lastStatus = message;
				return false;
			}

			var requestBody = new
			{
				profileId,
				saveData,
				saveVersion = 31
			};
			var requestJson = JsonSerializer.Serialize(requestBody, JsonOptions);

			using var msg = new HttpRequestMessage(HttpMethod.Post, $"{endpoint.TrimEnd('/')}/cloud-save/upload");
			msg.Headers.TryAddWithoutValidation("X-Convoy-Profile", profileId);
			msg.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

			using var response = Client.Send(msg);
			var responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

			if (!response.IsSuccessStatusCode)
			{
				message = $"Upload failed: HTTP {(int)response.StatusCode}";
				_lastStatus = message;
				return false;
			}

			using var doc = JsonDocument.Parse(responseBody);
			var root = doc.RootElement;
			var hash = GetString(root, "saveHash", "");
			_lastSyncTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			_lastStatus = $"Uploaded (hash: {hash})";
			message = "Save uploaded to cloud.";
			return true;
		}
		catch (Exception e)
		{
			message = $"Upload error: {e.Message}";
			_lastStatus = message;
			return false;
		}
	}

	public static bool Download(out string message)
	{
		message = "";
		var endpoint = GameState.Instance?.PurchaseValidationEndpoint ?? "";
		if (string.IsNullOrWhiteSpace(endpoint))
		{
			message = "Server endpoint not configured.";
			_lastStatus = message;
			return false;
		}

		var profileId = GameState.Instance?.PlayerProfileId ?? "";
		if (string.IsNullOrWhiteSpace(profileId))
		{
			message = "No player profile ID.";
			_lastStatus = message;
			return false;
		}

		try
		{
			using var msg = new HttpRequestMessage(HttpMethod.Get,
				$"{endpoint.TrimEnd('/')}/cloud-save/download?profileId={Uri.EscapeDataString(profileId)}");
			msg.Headers.TryAddWithoutValidation("X-Convoy-Profile", profileId);

			using var response = Client.Send(msg);
			var responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

			if (!response.IsSuccessStatusCode)
			{
				message = $"Download failed: HTTP {(int)response.StatusCode}";
				_lastStatus = message;
				return false;
			}

			using var doc = JsonDocument.Parse(responseBody);
			var root = doc.RootElement;
			var status = GetString(root, "status", "");

			if (status == "empty")
			{
				message = "No cloud save found for this profile.";
				_lastStatus = message;
				return false;
			}

			var saveData = GetString(root, "saveData", "");
			if (string.IsNullOrWhiteSpace(saveData))
			{
				message = "Cloud save was empty.";
				_lastStatus = message;
				return false;
			}

			var parsed = JsonSerializer.Deserialize<GameSaveData>(saveData, SaveJsonOptions);
			if (parsed == null)
			{
				message = "Failed to parse cloud save data.";
				_lastStatus = message;
				return false;
			}

			SaveSystem.Instance?.Save(parsed);
			GameState.Instance?.ReloadFromDisk();

			_lastSyncTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			_lastStatus = "Restored from cloud.";
			message = "Save restored from cloud.";
			return true;
		}
		catch (Exception e)
		{
			message = $"Download error: {e.Message}";
			_lastStatus = message;
			return false;
		}
	}

	public static CloudSaveSnapshot GetInfo()
	{
		var endpoint = GameState.Instance?.PurchaseValidationEndpoint ?? "";
		if (string.IsNullOrWhiteSpace(endpoint))
		{
			return new CloudSaveSnapshot { Status = "error", Message = "Endpoint not configured." };
		}

		var profileId = GameState.Instance?.PlayerProfileId ?? "";
		if (string.IsNullOrWhiteSpace(profileId))
		{
			return new CloudSaveSnapshot { Status = "error", Message = "No profile ID." };
		}

		try
		{
			using var msg = new HttpRequestMessage(HttpMethod.Get,
				$"{endpoint.TrimEnd('/')}/cloud-save/info?profileId={Uri.EscapeDataString(profileId)}");
			msg.Headers.TryAddWithoutValidation("X-Convoy-Profile", profileId);

			using var response = Client.Send(msg);
			var responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

			if (!response.IsSuccessStatusCode)
			{
				return new CloudSaveSnapshot { Status = "error", Message = $"HTTP {(int)response.StatusCode}" };
			}

			using var doc = JsonDocument.Parse(responseBody);
			var root = doc.RootElement;
			return new CloudSaveSnapshot
			{
				Status = GetString(root, "status", "error"),
				Message = GetString(root, "message", ""),
				SaveVersion = GetInt(root, "saveVersion", 0),
				SaveHash = GetString(root, "saveHash", ""),
				UploadedAtUnixSeconds = GetLong(root, "uploadedAtUnixSeconds", 0),
				SizeBytes = GetLong(root, "sizeBytes", 0)
			};
		}
		catch (Exception e)
		{
			return new CloudSaveSnapshot { Status = "error", Message = e.Message };
		}
	}

	private static string BuildSaveJson()
	{
		if (SaveSystem.Instance == null) return "";
		var path = SaveSystem.Instance.ActiveSaveFilePath;
		if (!FileAccess.FileExists(path)) return "";

		using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		return file?.GetAsText() ?? "";
	}

	private static string GetString(JsonElement el, string prop, string fallback) =>
		el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? fallback : fallback;

	private static int GetInt(JsonElement el, string prop, int fallback) =>
		el.TryGetProperty(prop, out var v) && v.TryGetInt32(out var n) ? n : fallback;

	private static long GetLong(JsonElement el, string prop, long fallback) =>
		el.TryGetProperty(prop, out var v) && v.TryGetInt64(out var n) ? n : fallback;
}
