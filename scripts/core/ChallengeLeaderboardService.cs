using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

public partial class ChallengeLeaderboardService : Node
{
	public static ChallengeLeaderboardService Instance { get; private set; }

	private readonly Dictionary<string, ChallengeLeaderboardSnapshot> _cache = new(StringComparer.OrdinalIgnoreCase);
	private readonly IChallengeLeaderboardProvider _localProvider = new LocalJournalChallengeLeaderboardProvider();
	private string _lastStatus = "Remote board not fetched yet.";

	public override void _EnterTree()
	{
		Instance = this;
	}

	public override void _ExitTree()
	{
		if (Instance == this)
		{
			Instance = null;
		}
	}

	public bool RefreshBoard(string code, int limit, out string message)
	{
		var normalizedCode = AsyncChallengeCatalog.NormalizeCode(code);
		var provider = ResolveProvider();
		try
		{
			var snapshot = provider.FetchLeaderboard(normalizedCode, limit);
			_cache[normalizedCode] = snapshot;
			_lastStatus = $"{provider.DisplayName}: {snapshot.Summary}";
			message = $"Refreshed remote board for {normalizedCode} via {provider.DisplayName}.";
			return true;
		}
		catch (Exception ex)
		{
			_lastStatus = $"{provider.DisplayName} fetch failed: {ex.Message}";
			message = _lastStatus;
			return false;
		}
	}

	public ChallengeLeaderboardSnapshot GetCachedSnapshot(string code)
	{
		var normalizedCode = AsyncChallengeCatalog.NormalizeCode(code);
		return _cache.TryGetValue(normalizedCode, out var snapshot)
			? snapshot
			: null;
	}

	public string BuildSnapshotSummary(string code, int maxEntries = 5)
	{
		var normalizedCode = AsyncChallengeCatalog.NormalizeCode(code);
		var snapshot = GetCachedSnapshot(normalizedCode);
		if (snapshot == null)
		{
			return
				"Remote leaderboard:\n" +
				"Not fetched yet for this board. Use `Refresh Board` to pull standings.\n" +
				$"Provider status: {_lastStatus}";
		}

		var builder = new StringBuilder();
		builder.AppendLine($"Remote leaderboard ({snapshot.ProviderDisplayName}):");
		builder.AppendLine(snapshot.Summary);
		if (snapshot.Entries.Count == 0)
		{
			builder.Append($"No remote standings cached for {normalizedCode} yet.");
			return builder.ToString().TrimEnd();
		}

		foreach (var entry in snapshot.Entries.Take(Math.Max(1, maxEntries)))
		{
			builder.AppendLine(
				$"#{entry.Rank} {entry.PlayerCallsign}  |  {entry.Score} pts  |  Hull {entry.HullPercent}%  |  {entry.ElapsedSeconds:0.0}s  |  {(entry.UsedLockedDeck ? "locked" : "player")} deck");
		}

		return builder.ToString().TrimEnd();
	}

	private IChallengeLeaderboardProvider ResolveProvider()
	{
		var providerId = ChallengeSyncProviderCatalog.NormalizeId(GameState.Instance?.ChallengeSyncProviderId ?? "");
		return providerId == ChallengeSyncProviderCatalog.HttpApiId
			? new HttpApiChallengeLeaderboardProvider(BuildHttpEndpoint(GameState.Instance?.ChallengeSyncEndpoint ?? ""))
			: _localProvider;
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
			return normalized[..^"/challenge-sync".Length] + "/challenge-leaderboard";
		}

		return normalized.TrimEnd('/') + "/challenge-leaderboard";
	}
}
