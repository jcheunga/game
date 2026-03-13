using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

public partial class ChallengeBoardFeedService : Node
{
	public static ChallengeBoardFeedService Instance { get; private set; }

	private readonly IChallengeBoardFeedProvider _localProvider = new LocalChallengeBoardFeedProvider();
	private ChallengeBoardFeedSnapshot _cachedSnapshot;
	private string _lastStatus = "Remote challenge feed not fetched yet.";

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

	public bool RefreshFeed(int highestUnlockedStage, int maxStage, int limit, out string message)
	{
		var provider = ResolveProvider();
		try
		{
			_cachedSnapshot = provider.FetchFeed(highestUnlockedStage, maxStage, limit);
			_lastStatus = $"{provider.DisplayName}: {_cachedSnapshot.Summary}";
			message = $"Refreshed remote challenge feed via {provider.DisplayName}.";
			return true;
		}
		catch (Exception ex)
		{
			_lastStatus = $"{provider.DisplayName} fetch failed: {ex.Message}";
			message = _lastStatus;
			return false;
		}
	}

	public ChallengeBoardFeedSnapshot GetCachedSnapshot()
	{
		return _cachedSnapshot;
	}

	public IReadOnlyList<FeaturedChallengeDefinition> GetCachedFeaturedChallenges()
	{
		if (_cachedSnapshot?.Items == null || _cachedSnapshot.Items.Count == 0)
		{
			return [];
		}

		var result = new List<FeaturedChallengeDefinition>();
		foreach (var item in _cachedSnapshot.Items)
		{
			if (!AsyncChallengeCatalog.TryParse(item.Code, out var challenge, out _))
			{
				continue;
			}

			result.Add(new FeaturedChallengeDefinition(
				string.IsNullOrWhiteSpace(item.Id) ? item.Code : item.Id,
				string.IsNullOrWhiteSpace(item.Title) ? "Remote Board" : item.Title,
				string.IsNullOrWhiteSpace(item.Summary) ? "Backend-authored async board." : item.Summary,
				challenge,
				NormalizeLockedDeck(item.LockedDeckUnitIds)));
		}

		return result;
	}

	public string BuildSnapshotSummary()
	{
		if (_cachedSnapshot == null)
		{
			return
				"Remote featured feed:\n" +
				"Not fetched yet. Use `Refresh Online` to pull backend-authored boards.\n" +
				$"Provider status: {_lastStatus}";
		}

		var builder = new StringBuilder();
		builder.AppendLine($"Remote featured feed ({_cachedSnapshot.ProviderDisplayName}):");
		builder.AppendLine(_cachedSnapshot.Summary);
		builder.Append($"Cached boards: {_cachedSnapshot.Items.Count}");
		return builder.ToString();
	}

	private IChallengeBoardFeedProvider ResolveProvider()
	{
		var providerId = ChallengeSyncProviderCatalog.NormalizeId(GameState.Instance?.ChallengeSyncProviderId ?? "");
		return providerId == ChallengeSyncProviderCatalog.HttpApiId
			? new HttpApiChallengeBoardFeedProvider(BuildHttpEndpoint(GameState.Instance?.ChallengeSyncEndpoint ?? ""))
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
			return normalized[..^"/challenge-sync".Length] + "/challenge-feed";
		}

		return normalized.TrimEnd('/') + "/challenge-feed";
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
}
