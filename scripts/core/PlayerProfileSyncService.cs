using System;
using System.Text;

public static class PlayerProfileSyncService
{
	public static bool IsAvailable => true;

	private static readonly IPlayerProfileSyncProvider LocalProvider = new LocalPlayerProfileSyncProvider();
	private static PlayerProfileSyncSnapshot _cachedSnapshot;
	private static string _lastStatus = "Player profile not synced yet.";

	public static bool RefreshProfile(out string message)
	{
		var gameState = GameState.Instance;
		if (gameState == null)
		{
			message = "Game state is unavailable.";
			_lastStatus = message;
			return false;
		}

		var request = new PlayerProfileSyncRequest
		{
			PlayerProfileId = gameState.PlayerProfileId,
			PlayerCallsign = gameState.PlayerCallsign,
			SyncProviderId = gameState.ChallengeSyncProviderId,
			RequestedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
		};

		var provider = ResolveProvider();
		try
		{
			_cachedSnapshot = NormalizeSnapshot(provider.SyncProfile(request), request);
			_lastStatus = $"{provider.DisplayName}: {_cachedSnapshot.Summary}";
			gameState.ApplyPlayerProfileSession(
				_cachedSnapshot.PlayerProfileId,
				_cachedSnapshot.PlayerCallsign,
				_cachedSnapshot.SessionToken,
				_cachedSnapshot.SyncedAtUnixSeconds);
			message = $"Refreshed player profile via {provider.DisplayName}.";
			return true;
		}
		catch (Exception ex)
		{
			_lastStatus = $"{provider.DisplayName} player profile failed: {ex.Message}";
			message = _lastStatus;
			return false;
		}
	}

	public static PlayerProfileSyncSnapshot GetCachedSnapshot()
	{
		return _cachedSnapshot;
	}

	public static void InvalidateFromState(string reason = "")
	{
		_cachedSnapshot = null;
		if (!string.IsNullOrWhiteSpace(reason))
		{
			_lastStatus = reason;
		}
	}

	public static string BuildStatusSummary()
	{
		var gameState = GameState.Instance;
		if (gameState == null)
		{
			return "Player profile sync:\nGame state unavailable.";
		}

		if (_cachedSnapshot == null)
		{
			return
				"Player profile sync:\n" +
				$"Profile {gameState.PlayerProfileId} is local only.\n" +
				$"Callsign: {gameState.PlayerCallsign}\n" +
				$"Auth token: {(string.IsNullOrWhiteSpace(gameState.PlayerAuthToken) ? "none" : MaskToken(gameState.PlayerAuthToken))}\n" +
				$"Last sync: {FormatUnixTime(gameState.LastPlayerProfileSyncAtUnixSeconds)}\n" +
				$"Provider status: {_lastStatus}";
		}

		var builder = new StringBuilder();
		builder.AppendLine($"Player profile sync ({_cachedSnapshot.ProviderDisplayName}):");
		builder.AppendLine(_cachedSnapshot.Summary);
		builder.AppendLine($"Profile: {_cachedSnapshot.PlayerProfileId}  |  Callsign: {_cachedSnapshot.PlayerCallsign}");
		builder.AppendLine($"Auth: {_cachedSnapshot.AuthState}  |  Token: {MaskToken(_cachedSnapshot.SessionToken)}");
		builder.AppendLine($"Rooms: {(_cachedSnapshot.CanJoinRooms ? "enabled" : "blocked")}  |  Relay: {(_cachedSnapshot.RelayEnabled ? "enabled" : "blocked")}");
		builder.Append($"Challenges: {(_cachedSnapshot.CanSubmitChallenges ? "enabled" : "blocked")}  |  Synced: {FormatUnixTime(_cachedSnapshot.SyncedAtUnixSeconds)}");
		return builder.ToString();
	}

	private static PlayerProfileSyncSnapshot NormalizeSnapshot(PlayerProfileSyncSnapshot snapshot, PlayerProfileSyncRequest request)
	{
		return new PlayerProfileSyncSnapshot
		{
			ProviderId = string.IsNullOrWhiteSpace(snapshot?.ProviderId) ? ResolveProvider().Id : snapshot.ProviderId,
			ProviderDisplayName = string.IsNullOrWhiteSpace(snapshot?.ProviderDisplayName) ? ResolveProvider().DisplayName : snapshot.ProviderDisplayName,
			Status = string.IsNullOrWhiteSpace(snapshot?.Status) ? "ok" : snapshot.Status.Trim(),
			Summary = string.IsNullOrWhiteSpace(snapshot?.Summary) ? "Profile sync completed." : snapshot.Summary.Trim(),
			PlayerProfileId = string.IsNullOrWhiteSpace(snapshot?.PlayerProfileId) ? request.PlayerProfileId : snapshot.PlayerProfileId.Trim(),
			PlayerCallsign = string.IsNullOrWhiteSpace(snapshot?.PlayerCallsign) ? request.PlayerCallsign : snapshot.PlayerCallsign.Trim(),
			AuthState = string.IsNullOrWhiteSpace(snapshot?.AuthState) ? "verified" : snapshot.AuthState.Trim(),
			SessionToken = snapshot?.SessionToken?.Trim() ?? "",
			CanSubmitChallenges = snapshot?.CanSubmitChallenges ?? true,
			CanJoinRooms = snapshot?.CanJoinRooms ?? true,
			RelayEnabled = snapshot?.RelayEnabled ?? true,
			SyncedAtUnixSeconds = snapshot?.SyncedAtUnixSeconds > 0
				? snapshot.SyncedAtUnixSeconds
				: request.RequestedAtUnixSeconds
		};
	}

	private static IPlayerProfileSyncProvider ResolveProvider()
	{
		var providerId = ChallengeSyncProviderCatalog.NormalizeId(GameState.Instance?.ChallengeSyncProviderId ?? "");
		return providerId == ChallengeSyncProviderCatalog.HttpApiId
			? new HttpApiPlayerProfileSyncProvider(BuildHttpEndpoint(GameState.Instance?.ChallengeSyncEndpoint ?? ""))
			: LocalProvider;
	}

	private static string BuildHttpEndpoint(string syncEndpoint)
	{
		var normalized = string.IsNullOrWhiteSpace(syncEndpoint) ? "" : syncEndpoint.Trim();
		if (string.IsNullOrWhiteSpace(normalized))
		{
			return "";
		}

		if (normalized.EndsWith("/challenge-sync", StringComparison.OrdinalIgnoreCase))
		{
			return normalized[..^"/challenge-sync".Length] + "/player-profile";
		}

		return normalized.TrimEnd('/') + "/player-profile";
	}

	private static string MaskToken(string token)
	{
		if (string.IsNullOrWhiteSpace(token))
		{
			return "none";
		}

		var trimmed = token.Trim();
		return trimmed.Length <= 10
			? trimmed
			: $"{trimmed[..4]}...{trimmed[^4..]}";
	}

	private static string FormatUnixTime(long unixSeconds)
	{
		return unixSeconds <= 0
			? "never"
			: DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime().ToString("MM-dd HH:mm:ss");
	}
}
