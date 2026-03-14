using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public sealed class LocalOnlineRoomResultProvider : IOnlineRoomResultProvider
{
	private static readonly Dictionary<string, List<OnlineRoomScoreboardEntry>> EntriesByRoomId = new(StringComparer.OrdinalIgnoreCase);

	public string Id => ChallengeSyncProviderCatalog.LocalJournalId;
	public string DisplayName => "Local Room Result Stub";

	public string BuildLocationSummary()
	{
		return "Source: generated local room-result cache";
	}

	public OnlineRoomResultSubmission SubmitResult(OnlineRoomJoinTicket ticket, OnlineRoomResultRequest request)
	{
		var submittedAt = request.RequestedAtUnixSeconds > 0
			? request.RequestedAtUnixSeconds
			: DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		var roomId = string.IsNullOrWhiteSpace(ticket.RoomId) ? request.RoomId : ticket.RoomId;
		var boardCode = AsyncChallengeCatalog.NormalizeCode(string.IsNullOrWhiteSpace(ticket.BoardCode) ? request.BoardCode : ticket.BoardCode);
		var entry = new OnlineRoomScoreboardEntry
		{
			RoomId = roomId,
			BoardCode = boardCode,
			PlayerCallsign = string.IsNullOrWhiteSpace(request.PlayerCallsign) ? "Lantern" : request.PlayerCallsign.Trim(),
			PlayerProfileId = request.PlayerProfileId ?? "",
			Score = Math.Max(0, request.Score),
			StarsEarned = Math.Max(0, request.StarsEarned),
			HullPercent = Math.Max(0, request.HullPercent),
			ElapsedSeconds = Math.Max(0f, request.ElapsedSeconds),
			EnemyDefeats = Math.Max(0, request.EnemyDefeats),
			Won = request.Won,
			Retreated = request.Retreated,
			UsedLockedDeck = request.UsedLockedDeck,
			SubmittedAtUnixSeconds = submittedAt
		};
		StoreEntry(roomId, entry);
		LocalOnlineRoomStubState.MarkResultSubmitted(roomId, entry.PlayerCallsign);
		var rank = BuildRankedEntries(roomId, 8)
			.FindIndex(item => item.PlayerProfileId.Equals(entry.PlayerProfileId, StringComparison.OrdinalIgnoreCase));
		return new OnlineRoomResultSubmission
		{
			ProviderId = Id,
			ProviderDisplayName = DisplayName,
			RoomId = roomId,
			BoardCode = boardCode,
			TicketId = ticket.TicketId,
			Status = "accepted",
			Summary = $"Stub room result accepted for {ticket.RoomTitle}.",
			Score = entry.Score,
			ProvisionalRank = rank >= 0 ? rank + 1 : 0,
			SubmittedAtUnixSeconds = submittedAt
		};
	}

	public static IReadOnlyList<OnlineRoomScoreboardEntry> GetRankedEntries(string roomId, int limit)
	{
		return BuildRankedEntries(roomId, limit);
	}

	public static void ClearRoom(string roomId)
	{
		if (string.IsNullOrWhiteSpace(roomId))
		{
			return;
		}

		EntriesByRoomId.Remove(roomId.Trim());
	}

	private static void StoreEntry(string roomId, OnlineRoomScoreboardEntry entry)
	{
		if (!EntriesByRoomId.TryGetValue(roomId, out var entries))
		{
			entries = [];
			EntriesByRoomId[roomId] = entries;
		}

		entries.RemoveAll(existing =>
			!string.IsNullOrWhiteSpace(existing.PlayerProfileId) &&
			existing.PlayerProfileId.Equals(entry.PlayerProfileId, StringComparison.OrdinalIgnoreCase));
		entries.Add(entry);
	}

	private static List<OnlineRoomScoreboardEntry> BuildRankedEntries(string roomId, int limit)
	{
		var ranked = new List<OnlineRoomScoreboardEntry>();
		if (EntriesByRoomId.TryGetValue(roomId, out var localEntries))
		{
			ranked.AddRange(localEntries);
		}

		var bestByProfile = new Dictionary<string, OnlineRoomScoreboardEntry>(StringComparer.OrdinalIgnoreCase);
		foreach (var entry in ranked)
		{
			var key = string.IsNullOrWhiteSpace(entry.PlayerProfileId)
				? entry.PlayerCallsign
				: entry.PlayerProfileId;
			if (!bestByProfile.TryGetValue(key, out var existing) || IsBetterEntry(entry, existing))
			{
				bestByProfile[key] = entry;
			}
		}

		var ordered = bestByProfile.Values
			.OrderByDescending(entry => entry.Score)
			.ThenByDescending(entry => entry.StarsEarned)
			.ThenByDescending(entry => entry.HullPercent)
			.ThenBy(entry => entry.ElapsedSeconds)
			.ThenByDescending(entry => entry.SubmittedAtUnixSeconds)
			.Take(Math.Max(1, limit))
			.ToList();
		for (var index = 0; index < ordered.Count; index++)
		{
			ordered[index].Rank = index + 1;
		}

		return ordered;
	}

	private static bool IsBetterEntry(OnlineRoomScoreboardEntry candidate, OnlineRoomScoreboardEntry current)
	{
		if (candidate.Score != current.Score)
		{
			return candidate.Score > current.Score;
		}

		if (candidate.StarsEarned != current.StarsEarned)
		{
			return candidate.StarsEarned > current.StarsEarned;
		}

		if (candidate.HullPercent != current.HullPercent)
		{
			return candidate.HullPercent > current.HullPercent;
		}

		if (!Mathf.IsEqualApprox(candidate.ElapsedSeconds, current.ElapsedSeconds))
		{
			return candidate.ElapsedSeconds < current.ElapsedSeconds;
		}

		return candidate.SubmittedAtUnixSeconds > current.SubmittedAtUnixSeconds;
	}
}
