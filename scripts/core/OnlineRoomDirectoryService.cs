using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

public static class OnlineRoomDirectoryService
{
	public static bool IsAvailable => true;

	private static readonly IOnlineRoomDirectoryProvider LocalProvider = new LocalOnlineRoomDirectoryProvider();
	private static OnlineRoomDirectorySnapshot _cachedSnapshot;
	private static string _lastStatus = "Online room directory not fetched yet.";

	public static bool RefreshRooms(int highestUnlockedStage, int maxStage, int limit, out string message)
	{
		var provider = ResolveProvider();
		try
		{
			var snapshot = provider.FetchRooms(highestUnlockedStage, maxStage, limit);
			snapshot.Entries = NormalizeEntries(snapshot.Entries).ToList();
			_cachedSnapshot = snapshot;
			_lastStatus = $"{provider.DisplayName}: {snapshot.Summary}";
			message = $"Refreshed online room directory via {provider.DisplayName}.";
			return true;
		}
		catch (Exception ex)
		{
			_lastStatus = $"{provider.DisplayName} fetch failed: {ex.Message}";
			message = _lastStatus;
			return false;
		}
	}

	public static OnlineRoomDirectorySnapshot GetCachedSnapshot()
	{
		return _cachedSnapshot;
	}

	public static IReadOnlyList<OnlineRoomDirectoryEntry> GetCachedRooms()
	{
		var merged = new List<OnlineRoomDirectoryEntry>();
		var seenRoomIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var hostedEntry = OnlineRoomCreateService.GetHostedRoomEntry();
		if (hostedEntry != null && !string.IsNullOrWhiteSpace(hostedEntry.RoomId))
		{
			merged.Add(hostedEntry);
			seenRoomIds.Add(hostedEntry.RoomId);
		}

		foreach (var entry in _cachedSnapshot?.Entries ?? [])
		{
			if (string.IsNullOrWhiteSpace(entry.RoomId))
			{
				continue;
			}

			if (seenRoomIds.Add(entry.RoomId))
			{
				merged.Add(entry);
			}
		}

		return merged;
	}

	public static void InjectRoom(OnlineRoomDirectoryEntry entry)
	{
		if (entry == null || string.IsNullOrWhiteSpace(entry.RoomId))
		{
			return;
		}

		if (_cachedSnapshot == null)
		{
			_cachedSnapshot = new OnlineRoomDirectorySnapshot
			{
				ProviderId = ResolveProvider().Id,
				ProviderDisplayName = ResolveProvider().DisplayName,
				Status = "ok",
				Summary = "Injected room listing cache.",
				FetchedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
			};
		}

		_cachedSnapshot.Entries.RemoveAll(existing => existing.RoomId.Equals(entry.RoomId, StringComparison.OrdinalIgnoreCase));
		_cachedSnapshot.Entries.Insert(0, entry);
	}

	public static string BuildSnapshotSummary()
	{
		if (_cachedSnapshot == null)
		{
			return
				"Online room directory:\n" +
				"Not fetched yet. Use `Refresh Online` to pull internet room listings.\n" +
				$"Provider status: {_lastStatus}";
		}

		var builder = new StringBuilder();
		builder.AppendLine($"Online room directory ({_cachedSnapshot.ProviderDisplayName}):");
		builder.AppendLine(_cachedSnapshot.Summary);
		builder.AppendLine($"Cached rooms: {GetCachedRooms().Count}");
		builder.Append("Directory cache now feeds room-board preload, backend join/session polling, and any hosted room published from this client.");
		return builder.ToString();
	}

	private static IOnlineRoomDirectoryProvider ResolveProvider()
	{
		var providerId = ChallengeSyncProviderCatalog.NormalizeId(GameState.Instance?.ChallengeSyncProviderId ?? "");
		return providerId == ChallengeSyncProviderCatalog.HttpApiId
			? new HttpApiOnlineRoomDirectoryProvider(BuildHttpEndpoint(GameState.Instance?.ChallengeSyncEndpoint ?? ""))
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
			return normalized[..^"/challenge-sync".Length] + "/challenge-rooms";
		}

		return normalized.TrimEnd('/') + "/challenge-rooms";
	}

	private static IEnumerable<OnlineRoomDirectoryEntry> NormalizeEntries(IEnumerable<OnlineRoomDirectoryEntry> entries)
	{
		var roomIndex = 0;
		foreach (var entry in entries ?? Array.Empty<OnlineRoomDirectoryEntry>())
		{
			var code = AsyncChallengeCatalog.NormalizeCode(entry.BoardCode);
			if (!AsyncChallengeCatalog.TryParse(code, out var challenge, out _))
			{
				continue;
			}

			roomIndex++;
			var normalizedLockedDeck = NormalizeLockedDeck(entry.LockedDeckUnitIds);
			var usesLockedDeck = entry.UsesLockedDeck || normalizedLockedDeck.Count > 0;
			var currentPlayers = Mathf.Max(1, entry.CurrentPlayers);
			var maxPlayers = Mathf.Max(currentPlayers, entry.MaxPlayers <= 0 ? 4 : entry.MaxPlayers);
			yield return new OnlineRoomDirectoryEntry
			{
				RoomId = string.IsNullOrWhiteSpace(entry.RoomId) ? $"ROOM-{roomIndex:000}" : entry.RoomId.Trim(),
				Title = string.IsNullOrWhiteSpace(entry.Title) ? "Remote Room" : entry.Title.Trim(),
				Summary = string.IsNullOrWhiteSpace(entry.Summary) ? "Internet room listing." : entry.Summary.Trim(),
				HostCallsign = string.IsNullOrWhiteSpace(entry.HostCallsign) ? "Lantern Host" : entry.HostCallsign.Trim(),
				BoardCode = code,
				BoardTitle = string.IsNullOrWhiteSpace(entry.BoardTitle)
					? BuildBoardTitle(challenge.Stage)
					: entry.BoardTitle.Trim(),
				CurrentPlayers = currentPlayers,
				MaxPlayers = maxPlayers,
				SpectatorCount = Math.Max(0, entry.SpectatorCount),
				Status = string.IsNullOrWhiteSpace(entry.Status) ? "lobby" : entry.Status.Trim(),
				Region = string.IsNullOrWhiteSpace(entry.Region) ? "global" : entry.Region.Trim(),
				UsesLockedDeck = usesLockedDeck,
				LockedDeckUnitIds = usesLockedDeck ? normalizedLockedDeck.ToArray() : []
			};
		}
	}

	private static IReadOnlyList<string> NormalizeLockedDeck(IEnumerable<string> lockedDeckUnitIds)
	{
		var validIds = new HashSet<string>(GameData.PlayerRosterIds, StringComparer.OrdinalIgnoreCase);
		var normalized = new List<string>();
		foreach (var unitId in lockedDeckUnitIds ?? Array.Empty<string>())
		{
			if (string.IsNullOrWhiteSpace(unitId) || !validIds.Contains(unitId))
			{
				continue;
			}

			if (normalized.Contains(unitId, StringComparer.OrdinalIgnoreCase))
			{
				continue;
			}

			normalized.Add(unitId);
			if (normalized.Count >= 3)
			{
				break;
			}
		}

		foreach (var fallbackId in new[] { GameData.PlayerBrawlerId, GameData.PlayerShooterId, GameData.PlayerDefenderId })
		{
			if (normalized.Count >= 3)
			{
				break;
			}

			if (!normalized.Contains(fallbackId, StringComparer.OrdinalIgnoreCase))
			{
				normalized.Add(fallbackId);
			}
		}

		return normalized;
	}

	private static string BuildBoardTitle(int stageNumber)
	{
		var stage = GameData.GetStage(stageNumber);
		return $"{stage.MapName} S{stage.StageNumber} {stage.StageName}";
	}
}
