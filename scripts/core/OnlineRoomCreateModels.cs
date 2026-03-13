public sealed class OnlineRoomCreateRequest
{
	public string BoardCode { get; set; } = "";
	public string BoardTitle { get; set; } = "";
	public string PlayerProfileId { get; set; } = "";
	public string PlayerCallsign { get; set; } = "";
	public string Region { get; set; } = "";
	public bool UsesLockedDeck { get; set; }
	public string[] LockedDeckUnitIds { get; set; } = [];
	public long RequestedAtUnixSeconds { get; set; }
}

public sealed class OnlineRoomCreateResult
{
	public string ProviderId { get; set; } = "";
	public string ProviderDisplayName { get; set; } = "";
	public string RoomId { get; set; } = "";
	public string Title { get; set; } = "";
	public string Summary { get; set; } = "";
	public string HostCallsign { get; set; } = "";
	public string BoardCode { get; set; } = "";
	public string BoardTitle { get; set; } = "";
	public int CurrentPlayers { get; set; }
	public int MaxPlayers { get; set; }
	public int SpectatorCount { get; set; }
	public string Status { get; set; } = "lobby";
	public string Region { get; set; } = "";
	public string TransportHint { get; set; } = "";
	public string RelayEndpoint { get; set; } = "";
	public bool UsesLockedDeck { get; set; }
	public string[] LockedDeckUnitIds { get; set; } = [];
	public OnlineRoomJoinTicket HostTicket { get; set; } = new();
}

public interface IOnlineRoomCreateProvider
{
	string Id { get; }
	string DisplayName { get; }
	string BuildLocationSummary();
	OnlineRoomCreateResult CreateRoom(OnlineRoomCreateRequest request);
}
