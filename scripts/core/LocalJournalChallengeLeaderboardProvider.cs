using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;

public sealed class LocalJournalChallengeLeaderboardProvider : IChallengeLeaderboardProvider
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = false
	};

	public string Id => ChallengeSyncProviderCatalog.LocalJournalId;
	public string DisplayName => "Local Journal Stub";

	public string BuildLocationSummary()
	{
		var localizedPath = ProjectSettings.LocalizePath(ProjectSettings.GlobalizePath(LocalJournalChallengeSyncProvider.BatchJournalPath));
		return $"Source: {localizedPath}";
	}

	public ChallengeLeaderboardSnapshot FetchLeaderboard(string code, int limit)
	{
		var normalizedCode = AsyncChallengeCatalog.NormalizeCode(code);
		var globalPath = ProjectSettings.GlobalizePath(LocalJournalChallengeSyncProvider.BatchJournalPath);
		if (!File.Exists(globalPath))
		{
			return new ChallengeLeaderboardSnapshot
			{
				Code = normalizedCode,
				ProviderId = Id,
				ProviderDisplayName = DisplayName,
				Status = "empty",
				Summary = "No buffered leaderboard data found yet.",
				FetchedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
				Entries = []
			};
		}

		var bestByProfile = new Dictionary<string, ChallengeLeaderboardEntry>(StringComparer.OrdinalIgnoreCase);
		foreach (var line in File.ReadLines(globalPath))
		{
			if (string.IsNullOrWhiteSpace(line))
			{
				continue;
			}

			ChallengeSyncBatchEnvelope batch;
			try
			{
				batch = JsonSerializer.Deserialize<ChallengeSyncBatchEnvelope>(line, JsonOptions);
			}
			catch
			{
				continue;
			}

			if (batch?.Submissions == null)
			{
				continue;
			}

			foreach (var submission in batch.Submissions)
			{
				if (submission == null || !submission.Code.Equals(normalizedCode, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				var key = string.IsNullOrWhiteSpace(submission.PlayerProfileId)
					? submission.SubmissionId
					: submission.PlayerProfileId;
				var entry = new ChallengeLeaderboardEntry
				{
					Code = normalizedCode,
					PlayerCallsign = string.IsNullOrWhiteSpace(submission.PlayerCallsign) ? "Lantern" : submission.PlayerCallsign,
					PlayerProfileId = submission.PlayerProfileId,
					Score = submission.Score,
					StarsEarned = submission.StarsEarned,
					HullPercent = submission.HullPercent,
					ElapsedSeconds = submission.ElapsedSeconds,
					UsedLockedDeck = submission.UsedLockedDeck,
					PlayedAtUnixSeconds = Math.Max(submission.LastUploadAttemptUnixSeconds, batch.SubmittedAtUnixSeconds)
				};

				if (!bestByProfile.TryGetValue(key, out var existing) || IsBetterEntry(entry, existing))
				{
					bestByProfile[key] = entry;
				}
			}
		}

		var ranked = bestByProfile.Values
			.OrderByDescending(entry => entry.Score)
			.ThenByDescending(entry => entry.StarsEarned)
			.ThenByDescending(entry => entry.HullPercent)
			.ThenBy(entry => entry.ElapsedSeconds)
			.Take(Math.Max(1, limit))
			.ToList();
		for (var i = 0; i < ranked.Count; i++)
		{
			ranked[i].Rank = i + 1;
		}

		return new ChallengeLeaderboardSnapshot
		{
			Code = normalizedCode,
			ProviderId = Id,
			ProviderDisplayName = DisplayName,
			Status = ranked.Count == 0 ? "empty" : "ok",
			Summary = ranked.Count == 0
				? "No matching entries buffered for this code yet."
				: $"Loaded {ranked.Count} buffered leaderboard entr{(ranked.Count == 1 ? "y" : "ies")}.",
			FetchedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
			Entries = ranked
		};
	}

	private static bool IsBetterEntry(ChallengeLeaderboardEntry candidate, ChallengeLeaderboardEntry current)
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

		return candidate.PlayedAtUnixSeconds > current.PlayedAtUnixSeconds;
	}
}
