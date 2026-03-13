using System.Collections.Generic;

public sealed class OnlineRoomDirectoryEntry
{
	public string RoomId { get; set; } = "";
	public string Title { get; set; } = "";
	public string Summary { get; set; } = "";
	public string HostCallsign { get; set; } = "";
	public string BoardCode { get; set; } = "";
	public string BoardTitle { get; set; } = "";
	public int CurrentPlayers { get; set; }
	public int MaxPlayers { get; set; }
	public int SpectatorCount { get; set; }
	public string Status { get; set; } = "";
	public string Region { get; set; } = "";
	public bool UsesLockedDeck { get; set; }
	public string[] LockedDeckUnitIds { get; set; } = [];
}

public sealed class OnlineRoomDirectorySnapshot
{
	public string ProviderId { get; set; } = "";
	public string ProviderDisplayName { get; set; } = "";
	public string Status { get; set; } = "";
	public string Summary { get; set; } = "";
	public long FetchedAtUnixSeconds { get; set; }
	public List<OnlineRoomDirectoryEntry> Entries { get; set; } = [];
}

public interface IOnlineRoomDirectoryProvider
{
	string Id { get; }
	string DisplayName { get; }
	string BuildLocationSummary();
	OnlineRoomDirectorySnapshot FetchRooms(int highestUnlockedStage, int maxStage, int limit);
}
