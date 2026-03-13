using System;

public sealed class LocalPlayerProfileSyncProvider : IPlayerProfileSyncProvider
{
	public string Id => ChallengeSyncProviderCatalog.LocalJournalId;
	public string DisplayName => "Local Profile Stub";

	public string BuildLocationSummary()
	{
		return "Source: generated local player profile";
	}

	public PlayerProfileSyncSnapshot SyncProfile(PlayerProfileSyncRequest request)
	{
		var syncedAt = request.RequestedAtUnixSeconds > 0
			? request.RequestedAtUnixSeconds
			: DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		var normalizedProfileId = string.IsNullOrWhiteSpace(request.PlayerProfileId)
			? "CVY-LOCAL"
			: request.PlayerProfileId.Trim();
		return new PlayerProfileSyncSnapshot
		{
			ProviderId = Id,
			ProviderDisplayName = DisplayName,
			Status = "ok",
			Summary = $"Local profile stub refreshed {request.PlayerCallsign}.",
			PlayerProfileId = normalizedProfileId,
			PlayerCallsign = string.IsNullOrWhiteSpace(request.PlayerCallsign) ? "Convoy" : request.PlayerCallsign.Trim(),
			AuthState = "local_stub",
			SessionToken = $"LOCAL-{normalizedProfileId}",
			CanSubmitChallenges = true,
			CanJoinRooms = true,
			RelayEnabled = true,
			SyncedAtUnixSeconds = syncedAt
		};
	}
}
