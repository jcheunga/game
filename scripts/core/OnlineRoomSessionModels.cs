public sealed class OnlineRoomSessionSnapshot
{
	public string ProviderId { get; set; } = "";
	public string ProviderDisplayName { get; set; } = "";
	public string Status { get; set; } = "";
	public string Summary { get; set; } = "";
	public long FetchedAtUnixSeconds { get; set; }
	public MultiplayerRoomSnapshot RoomSnapshot { get; set; } = new();
}

public interface IOnlineRoomSessionProvider
{
	string Id { get; }
	string DisplayName { get; }
	string BuildLocationSummary();
	OnlineRoomSessionSnapshot FetchRoomSession(OnlineRoomJoinTicket ticket);
}
