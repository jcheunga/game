public sealed class PlayerProfileSyncRequest
{
	public string PlayerProfileId { get; set; } = "";
	public string PlayerCallsign { get; set; } = "";
	public string SyncProviderId { get; set; } = "";
	public long RequestedAtUnixSeconds { get; set; }
}

public sealed class PlayerProfileSyncSnapshot
{
	public string ProviderId { get; set; } = "";
	public string ProviderDisplayName { get; set; } = "";
	public string Status { get; set; } = "ok";
	public string Summary { get; set; } = "";
	public string PlayerProfileId { get; set; } = "";
	public string PlayerCallsign { get; set; } = "";
	public string AuthState { get; set; } = "local";
	public string SessionToken { get; set; } = "";
	public bool CanSubmitChallenges { get; set; } = true;
	public bool CanJoinRooms { get; set; } = true;
	public bool RelayEnabled { get; set; } = true;
	public long SyncedAtUnixSeconds { get; set; }
}

public interface IPlayerProfileSyncProvider
{
	string Id { get; }
	string DisplayName { get; }
	string BuildLocationSummary();
	PlayerProfileSyncSnapshot SyncProfile(PlayerProfileSyncRequest request);
}
