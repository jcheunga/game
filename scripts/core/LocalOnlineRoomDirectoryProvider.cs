using System;
using System.Collections.Generic;
using System.Linq;

public sealed class LocalOnlineRoomDirectoryProvider : IOnlineRoomDirectoryProvider
{
	public string Id => ChallengeSyncProviderCatalog.LocalJournalId;
	public string DisplayName => "Local Directory Stub";

	public string BuildLocationSummary()
	{
		return "Source: generated local room listings";
	}

	public OnlineRoomDirectorySnapshot FetchRooms(int highestUnlockedStage, int maxStage, int limit)
	{
		var entries = new List<OnlineRoomDirectoryEntry>();
		var cappedLimit = Math.Max(1, limit);
		var selectedChallenge = GameState.Instance?.GetSelectedAsyncChallenge();
		if (selectedChallenge != null)
		{
			entries.Add(CreateRoom(
				"relay_open_alpha",
				"Relay Open Alpha",
				"Local internet-room stub seeded from the currently selected async board.",
				"Stonewatch",
				selectedChallenge.Code,
				BuildBoardTitle(selectedChallenge.Stage),
				2,
				4,
				1,
				"lobby",
				"global",
				false,
				[]));
		}

		var featuredRooms = FeaturedChallengeCatalog.GetDailyRotation(Math.Max(1, highestUnlockedStage), Math.Max(1, maxStage))
			.Take(Math.Max(2, cappedLimit))
			.ToArray();
		for (var index = 0; index < featuredRooms.Length && entries.Count < cappedLimit; index++)
		{
			var featured = featuredRooms[index];
			entries.Add(CreateRoom(
				$"daily_featured_{index + 1}",
				index == 0 ? "Featured Lockstep Lobby" : "Featured Open Relay",
				index == 0
					? "Local stub room using a locked daily squad for fair async rematches."
					: "Local stub room using the daily board with player convoy decks.",
				index == 0 ? "Ironhare" : "Northgate",
				featured.Challenge.Code,
				BuildBoardTitle(featured.Challenge.Stage),
				index == 0 ? 4 : 1,
				4,
				index == 0 ? 0 : 2,
				index == 0 ? "countdown" : "racing",
				index == 0 ? "us-east" : "eu-central",
				index == 0,
				index == 0 ? featured.LockedDeckUnitIds.ToArray() : []));
		}

		return new OnlineRoomDirectorySnapshot
		{
			ProviderId = Id,
			ProviderDisplayName = DisplayName,
			Status = "ok",
			Summary = $"Generated {entries.Count} local internet-room listing{(entries.Count == 1 ? "" : "s")}.",
			FetchedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
			Entries = entries.Take(cappedLimit).ToList()
		};
	}

	private static OnlineRoomDirectoryEntry CreateRoom(
		string roomId,
		string title,
		string summary,
		string hostCallsign,
		string boardCode,
		string boardTitle,
		int currentPlayers,
		int maxPlayers,
		int spectatorCount,
		string status,
		string region,
		bool usesLockedDeck,
		string[] lockedDeckUnitIds)
	{
		return new OnlineRoomDirectoryEntry
		{
			RoomId = roomId,
			Title = title,
			Summary = summary,
			HostCallsign = hostCallsign,
			BoardCode = boardCode,
			BoardTitle = boardTitle,
			CurrentPlayers = currentPlayers,
			MaxPlayers = maxPlayers,
			SpectatorCount = spectatorCount,
			Status = status,
			Region = region,
			UsesLockedDeck = usesLockedDeck,
			LockedDeckUnitIds = lockedDeckUnitIds ?? []
		};
	}

	private static string BuildBoardTitle(int stageNumber)
	{
		var stage = GameData.GetStage(stageNumber);
		return $"{stage.MapName} S{stage.StageNumber} {stage.StageName}";
	}
}
