using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

public partial class GameState : Node
{
	private const int DefaultGold = 120;
	private const int DefaultFood = 12;
	private const int DefaultUnlockedStage = 1;
	private const int MaxDeckSize = 3;
	private const int MaxSpellDeckSize = 2;
	private const int DefaultUnitLevel = 1;
	private const int UnitDoctrineUnlockLevelValue = 3;
	private const int UnitDoctrineRetrainGoldCost = 75;
	private const int MaxPlayerUnitLevel = 5;
	private const int MaxPersistentBaseUpgradeLevel = 5;
	private const int MaxPinnedChallenges = 8;
	private const int MaxPendingChallengeSubmissions = 24;
	private const string DefaultEndlessRouteId = "city";
	private const string DefaultEndlessBoonId = EndlessBoonCatalog.SurplusCourageId;
	private const string DefaultReport = "Pick a district and clear the route.";
	private const string DefaultPlayerCallsign = "Lantern";
	private const string DefaultChallengeSyncProviderId = ChallengeSyncProviderCatalog.LocalJournalId;
	private const string DefaultChallengeSyncEndpoint = "";
	private const bool DefaultChallengeSyncAutoFlush = false;
	private const string DefaultPlayerAuthToken = "";
	private const bool DefaultShowDevUi = true;
	private const bool DefaultShowFpsCounter = true;
	private const bool DefaultAudioMuted = false;
	private const int DefaultEffectsVolumePercent = 85;
	private const int DefaultAmbienceVolumePercent = 65;
	private static readonly string DefaultAsyncChallengeCode =
		AsyncChallengeCatalog.Create(DefaultUnlockedStage, AsyncChallengeCatalog.PressureSpikeId, 1001).Code;
	private static readonly string[] DefaultDeckUnitIds =
	{
		GameData.PlayerBrawlerId,
		GameData.PlayerShooterId,
		GameData.PlayerDefenderId
	};
	private static readonly string[] DefaultDeckSpellIds =
	{
		GameData.SpellFireballId,
		GameData.SpellHealId
	};

	public static GameState Instance { get; private set; }

	public int Gold { get; private set; } = DefaultGold;
	public int Food { get; private set; } = DefaultFood;
	public int Scrap => Gold;
	public int Fuel => Food;
	public int HighestUnlockedStage { get; private set; } = DefaultUnlockedStage;
	public int SelectedStage { get; private set; } = DefaultUnlockedStage;
	public string SelectedAsyncChallengeCode { get; private set; } = DefaultAsyncChallengeCode;
	public string SelectedEndlessRouteId { get; private set; } = DefaultEndlessRouteId;
	public string SelectedEndlessBoonId { get; private set; } = DefaultEndlessBoonId;
	public string LastResultMessage { get; private set; } = DefaultReport;
	public string PlayerCallsign { get; private set; } = DefaultPlayerCallsign;
	public string PlayerProfileId { get; private set; } = "";
	public string PlayerAuthToken { get; private set; } = DefaultPlayerAuthToken;
	public long LastPlayerProfileSyncAtUnixSeconds { get; private set; }
	public long LastChallengeSyncAtUnixSeconds { get; private set; }
	public int TotalChallengeSubmissionsSynced { get; private set; }
	public string ChallengeSyncProviderId { get; private set; } = DefaultChallengeSyncProviderId;
	public string ChallengeSyncEndpoint { get; private set; } = DefaultChallengeSyncEndpoint;
	public bool ChallengeSyncAutoFlush { get; private set; } = DefaultChallengeSyncAutoFlush;
	public bool ShowDevUi { get; private set; } = DefaultShowDevUi;
	public bool ShowFpsCounter { get; private set; } = DefaultShowFpsCounter;
	public bool AudioMuted { get; private set; } = DefaultAudioMuted;
	public int EffectsVolumePercent { get; private set; } = DefaultEffectsVolumePercent;
	public int AmbienceVolumePercent { get; private set; } = DefaultAmbienceVolumePercent;
	public BattleRunMode CurrentBattleMode { get; private set; } = BattleRunMode.Campaign;
	public IReadOnlyList<string> ActiveDeckUnitIds => _activeDeckUnitIds;
	public IReadOnlyList<string> ActiveDeckSpellIds => _activeDeckSpellIds;
	public int BestEndlessWave { get; private set; }
	public float BestEndlessTimeSeconds { get; private set; }
	public int EndlessRuns { get; private set; }
	public int ChallengeRuns { get; private set; }
	public int PendingChallengeSubmissionCount => _pendingChallengeSubmissions.Count;
	public int ClaimedDistrictRewardCount => _claimedDistrictRewardIds.Count;
	public int ClaimedUnitDoctrineCount => _unitDoctrineSelections.Count;
	public int ClaimedCampaignDirectiveCount => _claimedCampaignDirectiveIds.Count;

	public int MaxStage => GameData.MaxStage;
	public int DeckSizeLimit => MaxDeckSize;
	public int SpellDeckSizeLimit => MaxSpellDeckSize;
	public int MaxUnitLevel => MaxPlayerUnitLevel;
	public int UnitDoctrineUnlockLevel => UnitDoctrineUnlockLevelValue;
	public int MaxBaseUpgradeLevel => MaxPersistentBaseUpgradeLevel;
	public bool HasFullDeck => _activeDeckUnitIds.Count >= MaxDeckSize;
	public bool HasAnySpellEquipped => _activeDeckSpellIds.Count > 0;
	public bool HasSelectedAsyncChallengeLockedDeck => _selectedAsyncChallengeLockedDeckUnitIds.Count >= MaxDeckSize;

	private readonly List<string> _activeDeckUnitIds = new();
	private readonly List<string> _activeDeckSpellIds = new();
	private readonly List<string> _selectedAsyncChallengeLockedDeckUnitIds = new();
	private readonly List<int> _stageStars = new();
	private readonly HashSet<string> _ownedPlayerUnitIds = new(StringComparer.OrdinalIgnoreCase);
	private readonly HashSet<string> _ownedPlayerSpellIds = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, int> _unitUpgradeLevels = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, int> _baseUpgradeLevels = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, string> _unitDoctrineSelections = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, int> _challengeBestScores = new(StringComparer.OrdinalIgnoreCase);
	private readonly List<ChallengeRunRecord> _challengeHistory = new();
	private readonly List<ChallengeSubmissionEnvelope> _pendingChallengeSubmissions = new();
	private readonly List<string> _pinnedChallengeCodes = new();
	private readonly HashSet<string> _claimedDistrictRewardIds = new(StringComparer.OrdinalIgnoreCase);
	private readonly HashSet<string> _claimedCampaignDirectiveIds = new(StringComparer.OrdinalIgnoreCase);
	private int _armedCampaignDirectiveStage;

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

	public override void _Ready()
	{
		LoadOrInitialize();
	}

	public void ResetProgress()
	{
		ApplyDefaults();
		LastResultMessage = "Progress reset. Route is clear for stage 1.";
		Persist();
	}

	public void SetSelectedStage(int stage)
	{
		SelectedStage = Mathf.Clamp(stage, 1, MaxStage);
		Persist();
	}

	public void SetSelectedEndlessRoute(string routeId)
	{
		SelectedEndlessRouteId = NormalizeRouteId(routeId);
		Persist();
	}

	public void SetPlayerCallsign(string callsign)
	{
		PlayerCallsign = NormalizePlayerCallsign(callsign);
		Persist();
		LanChallengeService.Instance?.RefreshLocalProfile();
		PlayerProfileSyncService.InvalidateFromState("Player callsign changed. Refresh profile sync when ready.");
	}

	public void SetChallengeSyncProvider(string providerId)
	{
		ChallengeSyncProviderId = ChallengeSyncProviderCatalog.NormalizeId(providerId);
		Persist();
		ChallengeSyncService.Instance?.RefreshStatusFromState();
		PlayerProfileSyncService.InvalidateFromState("Sync provider changed. Refresh profile sync when ready.");
	}

	public void SetChallengeSyncEndpoint(string endpoint)
	{
		ChallengeSyncEndpoint = NormalizeChallengeSyncEndpoint(endpoint);
		Persist();
		ChallengeSyncService.Instance?.RefreshStatusFromState();
		PlayerProfileSyncService.InvalidateFromState("Sync endpoint changed. Refresh profile sync when ready.");
	}

	public void ApplyPlayerProfileSession(string profileId, string callsign, string authToken, long syncedAtUnixSeconds)
	{
		PlayerProfileId = NormalizePlayerProfileId(profileId);
		PlayerCallsign = NormalizePlayerCallsign(callsign);
		PlayerAuthToken = NormalizePlayerAuthToken(authToken);
		LastPlayerProfileSyncAtUnixSeconds = Math.Max(0L, syncedAtUnixSeconds);
		Persist();
		LanChallengeService.Instance?.RefreshLocalProfile();
	}

	public void ClearPlayerProfileSession()
	{
		PlayerAuthToken = DefaultPlayerAuthToken;
		LastPlayerProfileSyncAtUnixSeconds = 0L;
		Persist();
		PlayerProfileSyncService.InvalidateFromState("Cleared cached profile session.");
	}

	public void SetChallengeSyncAutoFlush(bool enabled)
	{
		ChallengeSyncAutoFlush = enabled;
		Persist();
		ChallengeSyncService.Instance?.RefreshStatusFromState();
	}

	public void PrepareCampaignBattle()
	{
		CurrentBattleMode = BattleRunMode.Campaign;
	}

	public CampaignDirectiveDefinition GetCampaignDirective(int stage)
	{
		if (stage < 1 || stage > MaxStage)
		{
			return null;
		}

		return CampaignDirectiveCatalog.GetForStage(GameData.GetStage(stage));
	}

	public bool IsCampaignDirectiveUnlocked(int stage)
	{
		return stage >= 1 &&
			stage <= HighestUnlockedStage &&
			GetStageStars(stage) > 0 &&
			GetCampaignDirective(stage) != null;
	}

	public bool IsCampaignDirectiveArmed(int stage)
	{
		return _armedCampaignDirectiveStage == stage &&
			GetCampaignDirective(stage) != null;
	}

	public bool HasClaimedCampaignDirective(string directiveId)
	{
		return !string.IsNullOrWhiteSpace(directiveId) &&
			_claimedCampaignDirectiveIds.Contains(directiveId.Trim());
	}

	public bool HasClaimedCampaignDirectiveForStage(int stage)
	{
		var directive = GetCampaignDirective(stage);
		return directive != null && HasClaimedCampaignDirective(directive.Id);
	}

	public bool ToggleCampaignDirective(int stage, out string message)
	{
		var directive = GetCampaignDirective(stage);
		if (directive == null)
		{
			message = $"Stage {stage} has no heroic directive.";
			return false;
		}

		if (!IsCampaignDirectiveUnlocked(stage))
		{
			message = $"Clear stage {stage} once before arming its heroic directive.";
			return false;
		}

		if (_armedCampaignDirectiveStage == stage)
		{
			_armedCampaignDirectiveStage = 0;
			LastResultMessage = $"Heroic directive stood down on stage {stage}.";
		}
		else
		{
			_armedCampaignDirectiveStage = stage;
			LastResultMessage = $"Heroic directive armed for stage {stage}: {directive.Title}.";
		}

		Persist();
		message = LastResultMessage;
		return true;
	}

	public StageDefinition BuildConfiguredCampaignStage(int stage)
	{
		var baseStage = GameData.GetStage(Mathf.Clamp(stage, 1, MaxStage));
		var directive = IsCampaignDirectiveArmed(baseStage.StageNumber)
			? GetCampaignDirective(baseStage.StageNumber)
			: null;

		if (directive == null)
		{
			return baseStage;
		}

		return CloneStageWithDirective(baseStage, directive);
	}

	public string BuildCampaignDirectiveStatusText(int stage)
	{
		var directive = GetCampaignDirective(stage);
		if (directive == null)
		{
			return "Heroic directive: none";
		}

		return CampaignDirectiveCatalog.BuildStatusText(
			directive,
			IsCampaignDirectiveUnlocked(stage),
			IsCampaignDirectiveArmed(stage),
			HasClaimedCampaignDirective(directive.Id));
	}

	public string BuildCampaignDirectiveInlineText(int stage)
	{
		var directive = GetCampaignDirective(stage);
		if (directive == null)
		{
			return "Directive: none";
		}

		var status = HasClaimedCampaignDirective(directive.Id)
			? "claimed"
			: IsCampaignDirectiveArmed(stage)
				? "armed"
				: IsCampaignDirectiveUnlocked(stage)
					? "ready"
					: "locked";
		return $"Directive: {directive.Title} ({status})";
	}

	public string BuildCampaignDirectiveRewardSummary(int stage)
	{
		return CampaignDirectiveCatalog.BuildRewardSummary(GetCampaignDirective(stage));
	}

	public CampaignReadinessReport GetCampaignReadinessReport(int stage)
	{
		var resolvedStage = BuildConfiguredCampaignStage(stage);
		return CampaignReadinessEvaluator.Evaluate(
			resolvedStage,
			GetActiveDeckUnits(),
			GetActiveDeckSpells());
	}

	public string BuildCampaignReadinessInlineSummary(int stage)
	{
		return CampaignReadinessEvaluator.BuildInlineSummary(GetCampaignReadinessReport(stage));
	}

	public string BuildCampaignReadinessDetailedSummary(int stage)
	{
		return CampaignReadinessEvaluator.BuildDetailedSummary(GetCampaignReadinessReport(stage));
	}

	public void PrepareEndlessBattle(string routeId)
	{
		CurrentBattleMode = BattleRunMode.Endless;
		SelectedEndlessRouteId = NormalizeRouteId(routeId);
		Persist();
	}

	public AsyncChallengeDefinition GetSelectedAsyncChallenge()
	{
		if (!AsyncChallengeCatalog.TryParse(SelectedAsyncChallengeCode, out var challenge, out _))
		{
			challenge = AsyncChallengeCatalog.Create(DefaultUnlockedStage, AsyncChallengeCatalog.PressureSpikeId, 1001);
		}

		return AsyncChallengeCatalog.Create(
			Mathf.Clamp(challenge.Stage, 1, MaxStage),
			challenge.MutatorId,
			challenge.Seed);
	}

	public bool TrySetSelectedAsyncChallengeCode(string code, out string message)
	{
		if (!AsyncChallengeCatalog.TryParse(code, out var challenge, out message))
		{
			return false;
		}

		SelectedAsyncChallengeCode = AsyncChallengeCatalog.Create(
			Mathf.Clamp(challenge.Stage, 1, MaxStage),
			challenge.MutatorId,
			challenge.Seed).Code;
		_selectedAsyncChallengeLockedDeckUnitIds.Clear();
		Persist();
		message = $"Loaded challenge {SelectedAsyncChallengeCode}.";
		return true;
	}

	public bool TrySetSelectedAsyncChallengeBoard(string code, IEnumerable<string> lockedDeckUnitIds, out string message)
	{
		if (!TrySetSelectedAsyncChallengeCode(code, out message))
		{
			return false;
		}

		_selectedAsyncChallengeLockedDeckUnitIds.Clear();
		if (lockedDeckUnitIds != null)
		{
			foreach (var unitId in lockedDeckUnitIds)
			{
				if (!string.IsNullOrWhiteSpace(unitId))
				{
					_selectedAsyncChallengeLockedDeckUnitIds.Add(unitId.Trim());
				}
			}
		}

		NormalizeSelectedAsyncChallengeLockedDeck();
		Persist();
		message = HasSelectedAsyncChallengeLockedDeck
			? $"Loaded shared challenge {SelectedAsyncChallengeCode} with a locked LAN squad."
			: $"Loaded shared challenge {SelectedAsyncChallengeCode}.";
		return true;
	}

	public void SetSelectedFeaturedChallenge(FeaturedChallengeDefinition featured)
	{
		if (featured == null)
		{
			return;
		}

		SelectedAsyncChallengeCode = featured.Challenge.Code;
		_selectedAsyncChallengeLockedDeckUnitIds.Clear();
		foreach (var unitId in featured.LockedDeckUnitIds)
		{
			if (!string.IsNullOrWhiteSpace(unitId))
			{
				_selectedAsyncChallengeLockedDeckUnitIds.Add(unitId.Trim());
			}
		}

		NormalizeSelectedAsyncChallengeLockedDeck();
		Persist();
	}

	public void GenerateAsyncChallenge(int stage, string mutatorId)
	{
		var challenge = AsyncChallengeCatalog.Generate(Mathf.Clamp(stage, 1, MaxStage), mutatorId);
		SelectedAsyncChallengeCode = challenge.Code;
		_selectedAsyncChallengeLockedDeckUnitIds.Clear();
		Persist();
	}

	public bool CanStartAsyncChallenge(out string message)
	{
		if (!HasSelectedAsyncChallengeLockedDeck && !CanStartBattle(out message))
		{
			return false;
		}

		var challenge = GetSelectedAsyncChallenge();
		if (challenge.Stage > HighestUnlockedStage)
		{
			message = $"Challenge stage {challenge.Stage} is not explored yet.";
			return false;
		}

		message = HasSelectedAsyncChallengeLockedDeck
			? $"Featured challenge {challenge.Code} ready with a locked squad board."
			: $"Challenge {challenge.Code} ready on stage {challenge.Stage}.";
		return true;
	}

	public bool PrepareAsyncChallenge(string code, out string message)
	{
		var normalizedCode = AsyncChallengeCatalog.NormalizeCode(code);
		if (!normalizedCode.Equals(SelectedAsyncChallengeCode, StringComparison.OrdinalIgnoreCase) &&
			!TrySetSelectedAsyncChallengeCode(code, out message))
		{
			return false;
		}

		if (!CanStartAsyncChallenge(out message))
		{
			return false;
		}

		CurrentBattleMode = BattleRunMode.AsyncChallenge;
		LastResultMessage = $"Challenge {SelectedAsyncChallengeCode} queued for deployment.";
		Persist();
		message = LastResultMessage;
		return true;
	}

	public void SetSelectedEndlessBoon(string boonId)
	{
		SelectedEndlessBoonId = NormalizeEndlessBoonId(boonId);
		Persist();
	}

	public void UnlockNextStage(int clearedStage)
	{
		UnlockNextStageInternal(clearedStage);
		Persist();
	}

	public string ApplyVictory(int stage, int rewardGold, int rewardFood, int starsEarned)
	{
		Gold += Math.Max(0, rewardGold);
		Food += Math.Max(0, rewardFood);
		var bestStars = RecordStageStars(stage, starsEarned);
		var districtRewardSummary = TryClaimDistrictRewardForStage(stage);
		var directiveRewardSummary = TryClaimCampaignDirectiveReward(stage);
		var extraRewardSummary = BuildCombinedCampaignBonusSummary(districtRewardSummary, directiveRewardSummary);
		var nextStageHint = stage < MaxStage
			? $" Explore stage {stage + 1} for {GetStageExploreFoodCost(stage + 1)} food when the caravan is ready."
			: "";
		LastResultMessage =
			$"Stage {stage} cleared. +{Math.Max(0, rewardGold)} gold, +{Math.Max(0, rewardFood)} food. Stars: {bestStars}/3." +
			(string.IsNullOrWhiteSpace(extraRewardSummary) ? "" : $" {extraRewardSummary}") +
			nextStageHint;
		Persist();
		return extraRewardSummary;
	}

	public void ApplyDefeat(int stage)
	{
		LastResultMessage = $"Stage {stage} failed. The war wagon line was overrun.";
		Persist();
	}

	public void ApplyRetreat(int stage)
	{
		LastResultMessage = $"Retreated from stage {stage}. No rewards earned.";
		Persist();
	}

	public void ApplyEndlessResult(string routeId, int waveReached, float elapsedSeconds, int enemyDefeats, int rewardGold, int rewardFood, bool retreated)
	{
		SelectedEndlessRouteId = NormalizeRouteId(routeId);
		Gold += Math.Max(0, rewardGold);
		Food += Math.Max(0, rewardFood);
		BestEndlessWave = Math.Max(BestEndlessWave, Math.Max(0, waveReached));
		BestEndlessTimeSeconds = Math.Max(BestEndlessTimeSeconds, Math.Max(0f, elapsedSeconds));
		EndlessRuns++;

		var routeLabel = GameData.GetLatestStageForMap(SelectedEndlessRouteId).MapName;
		var outcome = retreated ? "withdrew" : "was overrun";
		LastResultMessage =
			$"Endless run {outcome} on {routeLabel}. Wave {Math.Max(0, waveReached)}, {elapsedSeconds:0.0}s, {enemyDefeats} defeats. " +
			$"+{Math.Max(0, rewardGold)} gold, +{Math.Max(0, rewardFood)} food.";
		Persist();
	}

	public int GetAsyncChallengeBestScore(string code)
	{
		var normalized = AsyncChallengeCatalog.NormalizeCode(code);
		return _challengeBestScores.TryGetValue(normalized, out var score)
			? Math.Max(0, score)
			: 0;
	}

	public IReadOnlyList<ChallengeRunRecord> GetRecentChallengeHistory(int maxCount = 6, string codeFilter = "")
	{
		var normalizedCode = string.IsNullOrWhiteSpace(codeFilter)
			? ""
			: AsyncChallengeCatalog.NormalizeCode(codeFilter);
		return _challengeHistory
			.Where(entry =>
				string.IsNullOrWhiteSpace(normalizedCode) ||
				entry.Code.Equals(normalizedCode, StringComparison.OrdinalIgnoreCase))
			.OrderByDescending(entry => entry.PlayedAtUnixSeconds)
			.ThenByDescending(entry => entry.Score)
			.Take(Math.Max(1, maxCount))
			.Select(CloneChallengeHistoryRecord)
			.ToArray();
	}

	public ChallengeRunRecord GetLatestChallengeRun(string codeFilter = "")
	{
		var normalizedCode = string.IsNullOrWhiteSpace(codeFilter)
			? ""
			: AsyncChallengeCatalog.NormalizeCode(codeFilter);
		foreach (var entry in _challengeHistory)
		{
			if (!string.IsNullOrWhiteSpace(normalizedCode) &&
				!entry.Code.Equals(normalizedCode, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			return CloneChallengeHistoryRecord(entry);
		}

		return null;
	}

	public ChallengeRunRecord GetChallengeGhostRun(string codeFilter = "", bool preferLockedDeck = false)
	{
		var normalizedCode = string.IsNullOrWhiteSpace(codeFilter)
			? ""
			: AsyncChallengeCatalog.NormalizeCode(codeFilter);
		ChallengeRunRecord preferred = null;
		ChallengeRunRecord fallback = null;

		foreach (var entry in _challengeHistory)
		{
			if (!string.IsNullOrWhiteSpace(normalizedCode) &&
				!entry.Code.Equals(normalizedCode, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			if (IsBetterChallengeGhostCandidate(entry, fallback))
			{
				fallback = entry;
			}

			if (entry.UsedLockedDeck == preferLockedDeck && IsBetterChallengeGhostCandidate(entry, preferred))
			{
				preferred = entry;
			}
		}

		return preferred != null
			? CloneChallengeHistoryRecord(preferred)
			: fallback != null
				? CloneChallengeHistoryRecord(fallback)
				: null;
	}

	public string BuildChallengeGhostSummary(ChallengeRunRecord record)
	{
		if (record == null)
		{
			return "Ghost benchmark: no local benchmark tape saved yet for this board.";
		}

		var outcome = record.Retreated
			? "Retreated"
			: record.Won
				? "Cleared"
				: "Failed";
		var medal = "No Medal";
		if (AsyncChallengeCatalog.TryParse(record.Code, out var challenge, out _))
		{
			medal = AsyncChallengeCatalog.ResolveMedalLabel(challenge, record.Score);
		}

		return
			$"Ghost benchmark: {outcome}  |  {record.Score} pts ({medal})  |  Hull {Mathf.RoundToInt(Mathf.Clamp(record.BusHullRatio, 0f, 1f) * 100f)}%  |  Deploys {record.PlayerDeployments}  |  {(record.UsedLockedDeck ? "locked deck" : "player deck")}";
	}

	public string BuildChallengeRunTapeSummary(ChallengeRunRecord record, int maxDeployments = 6)
	{
		if (record == null)
		{
			return "Latest run tape:\nNo local tape saved yet for this board.";
		}

		var builder = new StringBuilder();
		var deckNames = record.DeckUnitIds == null || record.DeckUnitIds.Length == 0
			? "No deck data saved."
			: string.Join(", ", record.DeckUnitIds.Select(unitId => GameData.GetUnit(unitId).DisplayName));
		builder.AppendLine("Latest run tape:");
		builder.AppendLine($"Deck ({(record.UsedLockedDeck ? "featured lock" : "player deck")}): {deckNames}");
		builder.AppendLine($"Deploys {record.PlayerDeployments}  |  War wagon hull {Mathf.RoundToInt(Mathf.Clamp(record.BusHullRatio, 0f, 1f) * 100f)}%  |  Stars {record.StarsEarned}/3");
		builder.AppendLine(AsyncChallengeCatalog.BuildScoreSummary(BuildChallengeRunScoreBreakdown(record)));

		if (record.Deployments == null || record.Deployments.Count == 0)
		{
			builder.Append("Deploy log: none captured.");
			return builder.ToString().TrimEnd();
		}

		var segments = new List<string>();
		for (var i = 0; i < record.Deployments.Count && i < maxDeployments; i++)
		{
			var deployment = record.Deployments[i];
			segments.Add($"{deployment.TimeSeconds:0.0}s {GameData.GetUnit(deployment.UnitId).DisplayName}@{deployment.LanePercent}%");
		}

		if (record.Deployments.Count > maxDeployments)
		{
			segments.Add($"+{record.Deployments.Count - maxDeployments} more");
		}

		builder.Append("Deploy log: ");
		builder.Append(string.Join("  |  ", segments));
		return builder.ToString().TrimEnd();
	}

	public IReadOnlyList<ChallengeSubmissionEnvelope> GetPendingChallengeSubmissions(int maxCount = 6, string codeFilter = "")
	{
		var normalizedCode = string.IsNullOrWhiteSpace(codeFilter)
			? ""
			: AsyncChallengeCatalog.NormalizeCode(codeFilter);
		return _pendingChallengeSubmissions
			.Where(entry =>
				string.IsNullOrWhiteSpace(normalizedCode) ||
				entry.Code.Equals(normalizedCode, StringComparison.OrdinalIgnoreCase))
			.OrderByDescending(entry => entry.QueuedAtUnixSeconds)
			.ThenByDescending(entry => entry.Score)
			.Take(Math.Max(1, maxCount))
			.Select(CloneChallengeSubmissionEnvelope)
			.ToArray();
	}

	public string BuildChallengeSyncSummary(int maxEntries = 3)
	{
		var builder = new StringBuilder();
		builder.AppendLine("Internet sync outbox:");
		builder.AppendLine($"Profile: {BuildPlayerProfileDisplayId()}");
		builder.AppendLine($"Provider: {ChallengeSyncProviderCatalog.GetDisplayName(ChallengeSyncProviderId)}");
		builder.AppendLine($"Auto flush: {(ChallengeSyncAutoFlush ? "On" : "Off")}");
		builder.AppendLine($"Pending packets: {_pendingChallengeSubmissions.Count}");
		builder.AppendLine($"Packets synced: {TotalChallengeSubmissionsSynced}");
		builder.AppendLine($"Last sync: {FormatUnixTimestamp(LastChallengeSyncAtUnixSeconds)}");
		builder.AppendLine("Queue mode: offline-first packet outbox.");
		if (_pendingChallengeSubmissions.Count == 0)
		{
			builder.Append("Latest packets: none queued yet.");
			return builder.ToString().TrimEnd();
		}

		builder.AppendLine("Latest packets:");
		foreach (var entry in _pendingChallengeSubmissions
			.OrderByDescending(record => record.QueuedAtUnixSeconds)
			.ThenByDescending(record => record.Score)
			.Take(Math.Max(1, maxEntries)))
		{
			builder.AppendLine(FormatChallengeSubmissionEnvelope(entry));
		}

		return builder.ToString().TrimEnd();
	}

	public void RecordChallengeSubmissionAttempt(IEnumerable<string> submissionIds, long attemptedAtUnixSeconds)
	{
		if (submissionIds == null || _pendingChallengeSubmissions.Count == 0)
		{
			return;
		}

		var normalizedIds = new HashSet<string>(
			submissionIds
				.Where(id => !string.IsNullOrWhiteSpace(id))
				.Select(NormalizeSubmissionId),
			StringComparer.OrdinalIgnoreCase);
		if (normalizedIds.Count == 0)
		{
			return;
		}

		var normalizedTimestamp = Math.Max(0L, attemptedAtUnixSeconds);
		var changed = false;
		foreach (var entry in _pendingChallengeSubmissions)
		{
			if (!normalizedIds.Contains(entry.SubmissionId))
			{
				continue;
			}

			entry.UploadAttempts = Math.Max(0, entry.UploadAttempts) + 1;
			entry.LastUploadAttemptUnixSeconds = normalizedTimestamp;
			changed = true;
		}

		if (!changed)
		{
			return;
		}

		NormalizePendingChallengeSubmissions();
		Persist();
		ChallengeSyncService.Instance?.RefreshStatusFromState();
	}

	public int CompleteChallengeSubmissions(IEnumerable<string> submissionIds, long syncedAtUnixSeconds)
	{
		if (submissionIds == null || _pendingChallengeSubmissions.Count == 0)
		{
			return 0;
		}

		var normalizedIds = new HashSet<string>(
			submissionIds
				.Where(id => !string.IsNullOrWhiteSpace(id))
				.Select(NormalizeSubmissionId),
			StringComparer.OrdinalIgnoreCase);
		if (normalizedIds.Count == 0)
		{
			return 0;
		}

		var removed = _pendingChallengeSubmissions.RemoveAll(entry => normalizedIds.Contains(entry.SubmissionId));
		if (removed <= 0)
		{
			return 0;
		}

		LastChallengeSyncAtUnixSeconds = Math.Max(0L, syncedAtUnixSeconds);
		TotalChallengeSubmissionsSynced = Math.Max(0, TotalChallengeSubmissionsSynced) + removed;
		NormalizePendingChallengeSubmissions();
		Persist();
		ChallengeSyncService.Instance?.RefreshStatusFromState();
		return removed;
	}

	public IReadOnlyList<string> GetPinnedChallengeCodes()
	{
		return _pinnedChallengeCodes.ToArray();
	}

	public bool IsChallengeCodePinned(string code)
	{
		var normalized = NormalizePinnedChallengeCode(code);
		return !string.IsNullOrWhiteSpace(normalized) &&
			_pinnedChallengeCodes.Contains(normalized, StringComparer.OrdinalIgnoreCase);
	}

	public bool TogglePinnedChallengeCode(string code, out bool pinnedNow, out string message)
	{
		var normalized = NormalizePinnedChallengeCode(code);
		if (string.IsNullOrWhiteSpace(normalized))
		{
			pinnedNow = false;
			message = "Challenge code could not be pinned.";
			return false;
		}

		var existingIndex = _pinnedChallengeCodes.FindIndex(entry =>
			entry.Equals(normalized, StringComparison.OrdinalIgnoreCase));
		if (existingIndex >= 0)
		{
			_pinnedChallengeCodes.RemoveAt(existingIndex);
			pinnedNow = false;
			message = $"Removed {normalized} from the pinned challenge board.";
			Persist();
			return true;
		}

		if (_pinnedChallengeCodes.Count >= MaxPinnedChallenges)
		{
			pinnedNow = false;
			message = $"Pinned board full. Remove a code before adding {normalized}.";
			return false;
		}

		_pinnedChallengeCodes.Insert(0, normalized);
		pinnedNow = true;
		message = $"Pinned {normalized} to the challenge board.";
		Persist();
		return true;
	}

	public void ApplyAsyncChallengeResult(
		string code,
		int score,
		float elapsedSeconds,
		int enemyDefeats,
		int starsEarned,
		bool won,
		bool retreated,
		IReadOnlyList<string> deckUnitIds = null,
		IReadOnlyList<ChallengeDeploymentRecord> deployments = null,
		int playerDeployments = 0,
		float busHullRatio = 0f,
		bool usedLockedDeck = false,
		AsyncChallengeScoreBreakdown scoreBreakdown = null)
	{
		AsyncChallengeCatalog.TryParse(code, out var challenge, out _);
		var normalizedCode = AsyncChallengeCatalog.NormalizeCode(challenge.Code);
		var previousBest = GetAsyncChallengeBestScore(normalizedCode);
		var newBest = Math.Max(previousBest, Math.Max(0, score));
		_challengeBestScores[normalizedCode] = newBest;
		ChallengeRuns++;
		_challengeHistory.Insert(0, new ChallengeRunRecord
		{
			Code = normalizedCode,
			Stage = Mathf.Clamp(challenge.Stage, 1, MaxStage),
			MutatorId = challenge.MutatorId,
			Score = Math.Max(0, score),
			Won = won,
			Retreated = retreated,
			ElapsedSeconds = Math.Max(0f, elapsedSeconds),
			EnemyDefeats = Math.Max(0, enemyDefeats),
			StarsEarned = Mathf.Clamp(starsEarned, 0, 3),
			CompletionBonus = Math.Max(0, scoreBreakdown?.CompletionBonus ?? 0),
			StarBonus = Math.Max(0, scoreBreakdown?.StarBonus ?? 0),
			KillBonus = Math.Max(0, scoreBreakdown?.KillBonus ?? 0),
			HullBonus = Math.Max(0, scoreBreakdown?.HullBonus ?? 0),
			TimeBonus = Math.Max(0, scoreBreakdown?.TimeBonus ?? 0),
			DeployPenalty = Math.Max(0, scoreBreakdown?.DeployPenalty ?? 0),
			RawScore = Math.Max(0, scoreBreakdown?.RawScore ?? score),
			ScoreMultiplier = Mathf.Max(0.1f, scoreBreakdown?.Multiplier ?? 1f),
			UsedLockedDeck = usedLockedDeck,
			DeckUnitIds = (deckUnitIds ?? Array.Empty<string>()).ToArray(),
			PlayerDeployments = Math.Max(0, playerDeployments),
			BusHullRatio = Mathf.Clamp(busHullRatio, 0f, 1f),
			Deployments = deployments == null
				? []
				: deployments.Select(CloneChallengeDeploymentRecord).ToList(),
			PlayedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
		});
		NormalizeChallengeHistory();
		QueueChallengeSubmission(
			normalizedCode,
			challenge.Stage,
			challenge.MutatorId,
			Math.Max(0, score),
			won,
			retreated,
			Math.Max(0f, elapsedSeconds),
			Math.Max(0, enemyDefeats),
			Mathf.Clamp(starsEarned, 0, 3),
			deckUnitIds,
			deployments,
			Math.Max(0, playerDeployments),
			Mathf.RoundToInt(Mathf.Clamp(busHullRatio, 0f, 1f) * 100f),
			usedLockedDeck,
			scoreBreakdown);

		if (retreated)
		{
			LastResultMessage =
				$"Challenge {normalizedCode} was abandoned. Best score remains {newBest}.";
		}
		else if (won)
		{
			LastResultMessage =
				$"Challenge {normalizedCode} cleared. Score {score}. Stars {starsEarned}/3. " +
				(newBest > previousBest ? "New personal best recorded." : $"Best remains {newBest}.");
		}
		else
		{
			LastResultMessage =
				$"Challenge {normalizedCode} failed after {elapsedSeconds:0.0}s and {enemyDefeats} defeats. " +
				$"Score {score}. Best: {newBest}.";
		}

		Persist();
		if (ChallengeSyncAutoFlush)
		{
			ChallengeSyncService.Instance?.TryAutoFlushPending();
		}
	}

	public void SetShowDevUi(bool enabled)
	{
		ShowDevUi = enabled;
		Persist();
	}

	public void SetShowFpsCounter(bool enabled)
	{
		ShowFpsCounter = enabled;
		Persist();
	}

	public void SetAudioMuted(bool muted)
	{
		AudioMuted = muted;
		Persist();
		AudioDirector.Instance?.RefreshMixFromState();
	}

	public void SetEffectsVolumePercent(int percent)
	{
		EffectsVolumePercent = Mathf.Clamp(percent, 0, 100);
		Persist();
		AudioDirector.Instance?.RefreshMixFromState();
	}

	public void SetAmbienceVolumePercent(int percent)
	{
		AmbienceVolumePercent = Mathf.Clamp(percent, 0, 100);
		Persist();
		AudioDirector.Instance?.RefreshMixFromState();
	}

	public IReadOnlyList<UnitDefinition> GetActiveDeckUnits()
	{
		return GameData.GetUnitsByIds(_activeDeckUnitIds);
	}

	public IReadOnlyList<SpellDefinition> GetActiveDeckSpells()
	{
		return GameData.GetSpellsByIds(_activeDeckSpellIds);
	}

	public IReadOnlyList<UnitDefinition> GetSelectedAsyncChallengeDeckUnits()
	{
		return HasSelectedAsyncChallengeLockedDeck
			? GameData.GetUnitsByIds(_selectedAsyncChallengeLockedDeckUnitIds)
			: GetActiveDeckUnits();
	}

	public IReadOnlyList<SpellDefinition> GetSelectedAsyncChallengeDeckSpells()
	{
		return HasSelectedAsyncChallengeLockedDeck
			? Array.Empty<SpellDefinition>()
			: GetActiveDeckSpells();
	}

	public IReadOnlyList<UnitDefinition> GetBattleDeckUnits()
	{
		return CurrentBattleMode == BattleRunMode.AsyncChallenge && HasSelectedAsyncChallengeLockedDeck
			? GetSelectedAsyncChallengeDeckUnits()
			: GetActiveDeckUnits();
	}

	public IReadOnlyList<SpellDefinition> GetBattleDeckSpells()
	{
		return CurrentBattleMode == BattleRunMode.AsyncChallenge && HasSelectedAsyncChallengeLockedDeck
			? Array.Empty<SpellDefinition>()
			: GetActiveDeckSpells();
	}

	public IReadOnlyList<SquadSynergyDefinition> GetActiveDeckSynergies()
	{
		return GetDeckSynergies(GetActiveDeckUnits());
	}

	public IReadOnlyList<SquadSynergyDefinition> GetSelectedAsyncChallengeDeckSynergies()
	{
		return GetDeckSynergies(GetSelectedAsyncChallengeDeckUnits());
	}

	public IReadOnlyList<SquadSynergyDefinition> GetBattleDeckSynergies()
	{
		return GetDeckSynergies(GetBattleDeckUnits());
	}

	public IReadOnlyList<SquadSynergyDefinition> GetDeckSynergies(IEnumerable<UnitDefinition> deckUnits)
	{
		return SquadSynergyCatalog.ResolveActive(deckUnits);
	}

	public string BuildActiveDeckSynergySummary()
	{
		return BuildDeckSynergySummary(GetActiveDeckUnits());
	}

	public string BuildSelectedAsyncChallengeDeckSynergySummary()
	{
		return BuildDeckSynergySummary(GetSelectedAsyncChallengeDeckUnits());
	}

	public string BuildBattleDeckSynergySummary()
	{
		return BuildDeckSynergySummary(GetBattleDeckUnits());
	}

	public string BuildDeckSynergySummary(IEnumerable<UnitDefinition> deckUnits)
	{
		var synergies = GetDeckSynergies(deckUnits);
		if (synergies.Count == 0)
		{
			return "Active synergies: none. Pair two cards from the same squad role to unlock one.";
		}

		return "Active synergies:\n" + string.Join(
			"\n",
			synergies.Select(synergy => $"- {synergy.Title}: {synergy.Summary}"));
	}

	public string BuildActiveDeckSynergyInlineSummary()
	{
		return BuildDeckSynergyInlineSummary(GetActiveDeckUnits());
	}

	public string BuildSelectedAsyncChallengeDeckSynergyInlineSummary()
	{
		return BuildDeckSynergyInlineSummary(GetSelectedAsyncChallengeDeckUnits());
	}

	public string BuildBattleDeckSynergyInlineSummary()
	{
		return BuildDeckSynergyInlineSummary(GetBattleDeckUnits());
	}

	public string BuildDeckSynergyInlineSummary(IEnumerable<UnitDefinition> deckUnits)
	{
		var synergies = GetDeckSynergies(deckUnits);
		return synergies.Count == 0
			? "No deck synergy active"
			: string.Join("  |  ", synergies.Select(synergy => synergy.Title));
	}

	public string BuildActiveSpellSummary()
	{
		return BuildSpellSummary(GetActiveDeckSpells());
	}

	public string BuildSelectedAsyncChallengeSpellSummary()
	{
		return HasSelectedAsyncChallengeLockedDeck
			? "Featured squad lock: spell cards disabled on this board."
			: BuildSpellSummary(GetSelectedAsyncChallengeDeckSpells());
	}

	public string BuildSpellSummary(IEnumerable<SpellDefinition> spells)
	{
		var resolvedSpells = spells?
			.Where(spell => spell != null)
			.ToArray() ?? Array.Empty<SpellDefinition>();
		if (resolvedSpells.Length == 0)
		{
			return "Active magic: none.";
		}

		return "Active magic: " + string.Join(
			", ",
			resolvedSpells.Select(spell => $"{spell.DisplayName} ({spell.CourageCost})"));
	}

	public IReadOnlyList<UnitDefinition> GetUnlockedPlayerUnits()
	{
		return GetOwnedPlayerUnits();
	}

	public IReadOnlyList<UnitDefinition> GetOwnedPlayerUnits()
	{
		return GameData.GetPlayerUnits()
			.Where(unit => IsUnitOwned(unit.Id))
			.ToArray();
	}

	public IReadOnlyList<SpellDefinition> GetOwnedPlayerSpells()
	{
		return GameData.GetPlayerSpells()
			.Where(spell => IsSpellOwned(spell.Id))
			.ToArray();
	}

	public int GetStageStars(int stage)
	{
		if (stage < 1 || stage > _stageStars.Count)
		{
			return 0;
		}

		return _stageStars[stage - 1];
	}

	public bool IsUnitUnlocked(string unitId)
	{
		return IsUnitOwned(unitId);
	}

	public bool IsUnitOwned(string unitId)
	{
		try
		{
			var definition = GameData.GetUnit(unitId);
			if (!definition.IsPlayerSide)
			{
				return true;
			}

			return _ownedPlayerUnitIds.Contains(definition.Id);
		}
		catch (Exception)
		{
			return false;
		}
	}

	public bool IsSpellOwned(string spellId)
	{
		try
		{
			var definition = GameData.GetSpell(spellId);
			return _ownedPlayerSpellIds.Contains(definition.Id);
		}
		catch (Exception)
		{
			return false;
		}
	}

	public bool IsSpellUnlocked(string spellId)
	{
		return IsSpellOwned(spellId);
	}

	public bool IsSpellAvailableForPurchase(string spellId)
	{
		try
		{
			var definition = GameData.GetSpell(spellId);
			return definition.UnlockStage <= HighestUnlockedStage;
		}
		catch (Exception)
		{
			return false;
		}
	}

	public bool IsUnitAvailableForPurchase(string unitId)
	{
		try
		{
			var definition = GameData.GetUnit(unitId);
			if (!definition.IsPlayerSide)
			{
				return false;
			}

			return HighestUnlockedStage >= Mathf.Max(1, definition.UnlockStage);
		}
		catch (Exception)
		{
			return false;
		}
	}

	public int GetUnitPurchaseCost(string unitId)
	{
		var definition = GameData.GetUnit(unitId);
		if (!definition.IsPlayerSide)
		{
			return 0;
		}

		if (definition.GoldCost > 0)
		{
			return definition.GoldCost;
		}

		if (DefaultDeckUnitIds.Contains(definition.Id, StringComparer.OrdinalIgnoreCase))
		{
			return 0;
		}

		return 120 + (definition.Cost * 2) + (Math.Max(0, definition.UnlockStage - 1) * 35);
	}

	public int GetSpellPurchaseCost(string spellId)
	{
		var definition = GameData.GetSpell(spellId);
		if (definition.GoldCost > 0)
		{
			return definition.GoldCost;
		}

		if (DefaultDeckSpellIds.Contains(definition.Id, StringComparer.OrdinalIgnoreCase))
		{
			return 0;
		}

		return 90 + (Math.Max(0, definition.UnlockStage - 1) * 24) + (definition.CourageCost * 2);
	}

	public bool TryPurchaseUnit(string unitId, out string message)
	{
		try
		{
			var definition = GameData.GetUnit(unitId);
			if (!definition.IsPlayerSide)
			{
				message = "Enemy units cannot be purchased.";
				return false;
			}

			if (IsUnitOwned(definition.Id))
			{
				message = $"{definition.DisplayName} is already owned.";
				return false;
			}

			if (!IsUnitAvailableForPurchase(definition.Id))
			{
				message = $"{definition.DisplayName} becomes available after exploring stage {definition.UnlockStage}.";
				return false;
			}

			var cost = GetUnitPurchaseCost(definition.Id);
			if (Gold < cost)
			{
				message = $"Need {cost} gold to buy {definition.DisplayName}.";
				return false;
			}

			Gold -= cost;
			_ownedPlayerUnitIds.Add(definition.Id);
			LastResultMessage = $"{definition.DisplayName} purchased for {cost} gold.";
			Persist();
			message = LastResultMessage;
			return true;
		}
		catch (Exception)
		{
			message = "Unit data was not found.";
			return false;
		}
	}

	public bool TryPurchaseSpell(string spellId, out string message)
	{
		try
		{
			var definition = GameData.GetSpell(spellId);
			if (IsSpellOwned(definition.Id))
			{
				message = $"{definition.DisplayName} is already prepared in the grimoire.";
				return false;
			}

			if (!IsSpellAvailableForPurchase(definition.Id))
			{
				message = $"{definition.DisplayName} becomes available after exploring stage {definition.UnlockStage}.";
				return false;
			}

			var cost = GetSpellPurchaseCost(definition.Id);
			if (Gold < cost)
			{
				message = $"Need {cost} gold to scribe {definition.DisplayName}.";
				return false;
			}

			Gold -= cost;
			_ownedPlayerSpellIds.Add(definition.Id);
			LastResultMessage = cost > 0
				? $"{definition.DisplayName} scribed for {cost} gold."
				: $"{definition.DisplayName} prepared for the caravan.";
			Persist();
			message = LastResultMessage;
			return true;
		}
		catch (Exception)
		{
			message = "Spell data was not found.";
			return false;
		}
	}

	public int GetUnitLevel(string unitId)
	{
		return _unitUpgradeLevels.TryGetValue(unitId, out var level)
			? level
			: DefaultUnitLevel;
	}

	public int GetUnitUpgradeCost(string unitId)
	{
		var definition = GameData.GetUnit(unitId);
		var level = GetUnitLevel(unitId);
		return definition.Cost + 35 + ((level - 1) * 30);
	}

	public int GetEligibleUnitDoctrineCount()
	{
		return GameData.GetPlayerUnits()
			.Count(definition => IsUnitOwned(definition.Id) && GetUnitLevel(definition.Id) >= UnitDoctrineUnlockLevelValue);
	}

	public string GetUnitDoctrineId(string unitId)
	{
		return _unitDoctrineSelections.TryGetValue(unitId, out var doctrineId)
			? doctrineId
			: "";
	}

	public UnitDoctrineDefinition GetUnitDoctrineDefinition(string unitId)
	{
		return UnitDoctrineCatalog.GetOrNull(GetUnitDoctrineId(unitId));
	}

	public IReadOnlyList<UnitDoctrineDefinition> GetUnitDoctrineOptions(string unitId)
	{
		try
		{
			var definition = GameData.GetUnit(unitId);
			return definition.IsPlayerSide
				? UnitDoctrineCatalog.GetForTag(definition.SquadTag)
				: Array.Empty<UnitDoctrineDefinition>();
		}
		catch (Exception)
		{
			return Array.Empty<UnitDoctrineDefinition>();
		}
	}

	public bool IsUnitDoctrineUnlocked(string unitId)
	{
		return !string.IsNullOrWhiteSpace(unitId) &&
			IsUnitOwned(unitId) &&
			GetUnitLevel(unitId) >= UnitDoctrineUnlockLevelValue;
	}

	public int GetUnitDoctrineRetrainCost(string unitId)
	{
		return string.IsNullOrWhiteSpace(GetUnitDoctrineId(unitId))
			? 0
			: UnitDoctrineRetrainGoldCost;
	}

	public bool TrySelectUnitDoctrine(string unitId, string doctrineId, out string message)
	{
		try
		{
			var definition = GameData.GetUnit(unitId);
			if (!definition.IsPlayerSide)
			{
				message = "Only player units can adopt doctrines.";
				return false;
			}

			if (!IsUnitOwned(definition.Id))
			{
				message = $"Buy {definition.DisplayName} in the shop before choosing a doctrine.";
				return false;
			}

			if (GetUnitLevel(definition.Id) < UnitDoctrineUnlockLevelValue)
			{
				message = $"{definition.DisplayName} unlocks doctrines at level {UnitDoctrineUnlockLevelValue}.";
				return false;
			}

			var normalizedDoctrineId = UnitDoctrineCatalog.NormalizeId(doctrineId);
			var doctrine = UnitDoctrineCatalog.GetOrNull(normalizedDoctrineId);
			if (doctrine == null)
			{
				message = "Doctrine data was not found.";
				return false;
			}

			if (!GetUnitDoctrineOptions(definition.Id)
				.Any(option => option.Id.Equals(doctrine.Id, StringComparison.OrdinalIgnoreCase)))
			{
				message = $"{doctrine.Title} does not fit {definition.DisplayName}.";
				return false;
			}

			var currentDoctrineId = GetUnitDoctrineId(definition.Id);
			if (currentDoctrineId.Equals(doctrine.Id, StringComparison.OrdinalIgnoreCase))
			{
				message = $"{definition.DisplayName} already follows {doctrine.Title}.";
				return false;
			}

			var retrainCost = GetUnitDoctrineRetrainCost(definition.Id);
			if (retrainCost > 0 && Gold < retrainCost)
			{
				message = $"Need {retrainCost} gold to retrain {definition.DisplayName}.";
				return false;
			}

			if (retrainCost > 0)
			{
				Gold -= retrainCost;
			}

			_unitDoctrineSelections[definition.Id] = doctrine.Id;
			LastResultMessage = retrainCost > 0
				? $"{definition.DisplayName} retrained to {doctrine.Title}. -{retrainCost} gold."
				: $"{definition.DisplayName} adopted {doctrine.Title}.";
			Persist();
			message = LastResultMessage;
			return true;
		}
		catch (Exception)
		{
			message = "Unit data was not found.";
			return false;
		}
	}

	public string BuildUnitDoctrineInlineText(string unitId)
	{
		var doctrine = GetUnitDoctrineDefinition(unitId);
		if (doctrine != null)
		{
			return $"Doctrine: {doctrine.Title}";
		}

		return IsUnitDoctrineUnlocked(unitId)
			? "Doctrine ready"
			: $"Doctrine unlocks Lv{UnitDoctrineUnlockLevelValue}";
	}

	public string BuildUnitDoctrineStatusText(string unitId)
	{
		var doctrine = GetUnitDoctrineDefinition(unitId);
		if (doctrine != null)
		{
			var retrainCost = GetUnitDoctrineRetrainCost(unitId);
			return retrainCost > 0
				? $"Doctrine: {doctrine.Title}. {doctrine.Summary} Retrain: {retrainCost} gold."
				: $"Doctrine: {doctrine.Title}. {doctrine.Summary}";
		}

		if (!IsUnitDoctrineUnlocked(unitId))
		{
			return $"Doctrine unlocks at Lv{UnitDoctrineUnlockLevelValue}.";
		}

		var options = GetUnitDoctrineOptions(unitId)
			.Select(option => option.Title)
			.ToArray();
		return options.Length == 0
			? "Doctrine ready."
			: $"Doctrine ready: choose {string.Join(" or ", options)}.";
	}

	public bool TryUpgradeUnit(string unitId, out string message)
	{
		try
		{
			var definition = GameData.GetUnit(unitId);
			if (!definition.IsPlayerSide)
			{
				message = "Only player units can be upgraded.";
				return false;
			}

			if (!IsUnitOwned(definition.Id))
			{
				message = $"Buy {definition.DisplayName} in the shop before upgrading it.";
				return false;
			}

			var currentLevel = GetUnitLevel(definition.Id);
			if (currentLevel >= MaxPlayerUnitLevel)
			{
				message = $"{definition.DisplayName} is already max level.";
				return false;
			}

			var cost = GetUnitUpgradeCost(definition.Id);
			if (Gold < cost)
			{
				message = $"Need {cost} gold to upgrade {definition.DisplayName}.";
				return false;
			}

			Gold -= cost;
			_unitUpgradeLevels[definition.Id] = currentLevel + 1;
			LastResultMessage = $"{definition.DisplayName} upgraded to level {currentLevel + 1}. -{cost} gold.";
			Persist();
			message = LastResultMessage;
			return true;
		}
		catch (Exception)
		{
			message = "Unit data was not found.";
			return false;
		}
	}

	public int GetBaseUpgradeLevel(string upgradeId)
	{
		return _baseUpgradeLevels.TryGetValue(upgradeId, out var level)
			? level
			: 0;
	}

	public int GetBaseUpgradeCost(string upgradeId)
	{
		var level = GetBaseUpgradeLevel(upgradeId);
		return upgradeId switch
		{
			BaseUpgradeCatalog.HullPlatingId => 90 + (level * 70),
			BaseUpgradeCatalog.PantryId => 80 + (level * 65),
			BaseUpgradeCatalog.DispatchConsoleId => 95 + (level * 75),
			BaseUpgradeCatalog.SignalRelayId => 100 + (level * 80),
			_ => 100 + (level * 60)
		};
	}

	public bool TryUpgradeBase(string upgradeId, out string message)
	{
		try
		{
			var definition = BaseUpgradeCatalog.Get(upgradeId);
			var currentLevel = GetBaseUpgradeLevel(upgradeId);
			if (currentLevel >= definition.MaxLevel)
			{
				message = $"{definition.Title} is already max level.";
				return false;
			}

			var cost = GetBaseUpgradeCost(upgradeId);
			if (Gold < cost)
			{
				message = $"Need {cost} gold to upgrade {definition.Title}.";
				return false;
			}

			Gold -= cost;
			_baseUpgradeLevels[upgradeId] = currentLevel + 1;
			LastResultMessage = $"{definition.Title} upgraded to level {currentLevel + 1}. -{cost} gold.";
			Persist();
			message = LastResultMessage;
			return true;
		}
		catch (Exception)
		{
			message = "Base upgrade data was not found.";
			return false;
		}
	}

	public float ApplyPlayerBaseHealthUpgrade(float baseHealth)
	{
		return baseHealth * GetPlayerBaseHealthScale();
	}

	public float ApplyPlayerCourageMaxUpgrade(float baseMax)
	{
		return baseMax + GetPlayerCourageMaxBonus();
	}

	public float ApplyPlayerCourageGainUpgrade(float baseGain)
	{
		return baseGain * GetPlayerCourageGainScale();
	}

	public float ApplyPlayerDeployCooldownUpgrade(float baseCooldown)
	{
		return Mathf.Max(1.5f, baseCooldown * GetPlayerDeployCooldownScale());
	}

	public float ApplyPlayerSignalJamDurationUpgrade(float baseDuration)
	{
		return Mathf.Max(1.8f, baseDuration * GetPlayerSignalJamDurationScale());
	}

	public float ApplyPlayerSignalJamCooldownPenaltyUpgrade(float basePenalty)
	{
		return Mathf.Max(0f, basePenalty * GetPlayerSignalJamCooldownPenaltyScale());
	}

	public float ApplyPlayerSignalJamCourageGainScaleUpgrade(float jamScale)
	{
		var clampedScale = Mathf.Clamp(jamScale, 0f, 1f);
		var mitigation = GetPlayerSignalJamSuppressionMitigation();
		return Mathf.Clamp(clampedScale + ((1f - clampedScale) * mitigation), 0f, 1f);
	}

	public UnitStats BuildPlayerUnitStats(UnitDefinition definition)
	{
		return BuildPlayerUnitStatsForDeck(definition, GetActiveDeckUnits());
	}

	public UnitStats BuildPlayerUnitStatsAtLevel(UnitDefinition definition, int level)
	{
		return BuildPlayerUnitStatsAtLevelForDeck(definition, level, GetActiveDeckUnits());
	}

	public UnitStats BuildPlayerUnitStats(
		UnitDefinition definition,
		float bonusHealthScale,
		float bonusDamageScale,
		float bonusCooldownReduction,
		int bonusBaseDamage)
	{
		return BuildPlayerUnitStatsForDeck(
			definition,
			GetActiveDeckUnits(),
			bonusHealthScale,
			bonusDamageScale,
			bonusCooldownReduction,
			bonusBaseDamage);
	}

	public UnitStats BuildPlayerUnitStatsForDeck(UnitDefinition definition, IEnumerable<UnitDefinition> deckUnits)
	{
		return BuildPlayerUnitStatsAtLevelForDeck(definition, GetUnitLevel(definition.Id), deckUnits);
	}

	public UnitStats BuildPlayerUnitStatsAtLevelForDeck(UnitDefinition definition, int level, IEnumerable<UnitDefinition> deckUnits)
	{
		return BuildPlayerUnitStatsAtLevelForDeck(definition, level, deckUnits, 1f, 1f, 0f, 0);
	}

	public UnitStats BuildPlayerUnitStatsForDeck(
		UnitDefinition definition,
		IEnumerable<UnitDefinition> deckUnits,
		float bonusHealthScale,
		float bonusDamageScale,
		float bonusCooldownReduction,
		int bonusBaseDamage)
	{
		return BuildPlayerUnitStatsAtLevelForDeck(
			definition,
			GetUnitLevel(definition.Id),
			deckUnits,
			bonusHealthScale,
			bonusDamageScale,
			bonusCooldownReduction,
			bonusBaseDamage);
	}

	private UnitStats BuildPlayerUnitStatsAtLevelForDeck(
		UnitDefinition definition,
		int level,
		IEnumerable<UnitDefinition> deckUnits,
		float bonusHealthScale,
		float bonusDamageScale,
		float bonusCooldownReduction,
		int bonusBaseDamage)
	{
		return BuildPlayerUnitStatsForLevel(
			definition,
			level,
			deckUnits,
			bonusHealthScale,
			bonusDamageScale,
			bonusCooldownReduction,
			bonusBaseDamage);
	}

	public float GetPlayerBaseHealthScaleAtLevel(int level)
	{
		return 1f + (Mathf.Clamp(level, 0, MaxPersistentBaseUpgradeLevel) * 0.12f);
	}

	public float GetPlayerCourageMaxBonusAtLevel(int level)
	{
		return Mathf.Clamp(level, 0, MaxPersistentBaseUpgradeLevel) * 6f;
	}

	public float GetPlayerCourageGainScaleAtLevel(int level)
	{
		return 1f + (Mathf.Clamp(level, 0, MaxPersistentBaseUpgradeLevel) * 0.06f);
	}

	public float GetPlayerDeployCooldownScaleAtLevel(int level)
	{
		return Mathf.Max(0.55f, 1f - (Mathf.Clamp(level, 0, MaxPersistentBaseUpgradeLevel) * 0.06f));
	}

	public float GetPlayerSignalJamDurationScaleAtLevel(int level)
	{
		return Mathf.Max(0.5f, 1f - (Mathf.Clamp(level, 0, MaxPersistentBaseUpgradeLevel) * 0.1f));
	}

	public float GetPlayerSignalJamCooldownPenaltyScaleAtLevel(int level)
	{
		return Mathf.Max(0.35f, 1f - (Mathf.Clamp(level, 0, MaxPersistentBaseUpgradeLevel) * 0.13f));
	}

	public float GetPlayerSignalJamSuppressionMitigationAtLevel(int level)
	{
		return Mathf.Clamp(Mathf.Clamp(level, 0, MaxPersistentBaseUpgradeLevel) * 0.12f, 0f, 0.6f);
	}

	private UnitStats BuildPlayerUnitStatsForLevel(
		UnitDefinition definition,
		int level,
		IEnumerable<UnitDefinition> deckUnits,
		float bonusHealthScale,
		float bonusDamageScale,
		float bonusCooldownReduction,
		int bonusBaseDamage)
	{
		var bonusLevel = Math.Max(0, level - DefaultUnitLevel);
		var healthScale = (1f + (bonusLevel * 0.12f)) * Math.Max(0.1f, bonusHealthScale);
		var damageScale = (1f + (bonusLevel * 0.1f)) * Math.Max(0.1f, bonusDamageScale);
		var cooldownReduction = (bonusLevel * 0.03f) + Math.Max(0f, bonusCooldownReduction);
		var baseDamageBonus = (bonusLevel * 2) + Math.Max(0, bonusBaseDamage);
		var synergyBonus = ResolveDeckSynergyBonus(definition, deckUnits);
		var doctrineBonus = ResolveUnitDoctrineBonus(definition, level);

		healthScale *= synergyBonus.HealthScale;
		damageScale *= synergyBonus.DamageScale;
		cooldownReduction += synergyBonus.CooldownReduction;
		baseDamageBonus += synergyBonus.BaseDamageBonus;
		healthScale *= doctrineBonus.HealthScale;
		damageScale *= doctrineBonus.DamageScale;
		cooldownReduction += doctrineBonus.CooldownReduction;
		baseDamageBonus += doctrineBonus.BaseDamageBonus;

		return new UnitStats(
			definition,
			healthScale,
			damageScale,
			cooldownReduction,
			baseDamageBonus);
	}

	private static SquadSynergyBonus ResolveDeckSynergyBonus(UnitDefinition definition, IEnumerable<UnitDefinition> deckUnits)
	{
		var resolvedDeck = deckUnits?
			.Where(unit => unit != null)
			.ToArray() ?? Array.Empty<UnitDefinition>();
		var inDeck = resolvedDeck.Any(unit => unit.Id.Equals(definition.Id, StringComparison.OrdinalIgnoreCase));
		return inDeck
			? SquadSynergyCatalog.Aggregate(SquadSynergyCatalog.ResolveActive(resolvedDeck))
			: SquadSynergyBonus.None;
	}

	private UnitDoctrineBonus ResolveUnitDoctrineBonus(UnitDefinition definition, int level)
	{
		if (definition == null ||
			!definition.IsPlayerSide ||
			level < UnitDoctrineUnlockLevelValue)
		{
			return UnitDoctrineBonus.None;
		}

		return GetUnitDoctrineDefinition(definition.Id)?.Bonus ?? UnitDoctrineBonus.None;
	}

	public bool IsUnitInActiveDeck(string unitId)
	{
		return _activeDeckUnitIds.Contains(unitId, StringComparer.OrdinalIgnoreCase);
	}

	public bool IsSpellInActiveDeck(string spellId)
	{
		return _activeDeckSpellIds.Contains(spellId, StringComparer.OrdinalIgnoreCase);
	}

	public bool CanStartBattle(out string message)
	{
		if (_activeDeckUnitIds.Count < MaxDeckSize)
		{
			message = $"Fill all {MaxDeckSize} squad cards in Caravan Armory before deploying.";
			return false;
		}

		message = "Squad ready for deployment.";
		return true;
	}

	public bool CanStartCampaignBattle(int stage, out string message)
	{
		if (!CanStartBattle(out message))
		{
			return false;
		}

		if (stage < 1 || stage > HighestUnlockedStage)
		{
			message = "Explore this route segment before deploying there.";
			return false;
		}

		var foodCost = GetStageEntryFoodCost(stage);
		if (Food < foodCost)
		{
			message = $"Need {foodCost} food to begin stage {stage}.";
			return false;
		}

		message = $"Caravan ready. Stage entry costs {foodCost} food.";
		var readiness = BuildCampaignReadinessInlineSummary(stage);
		if (!string.IsNullOrWhiteSpace(readiness))
		{
			message += $" {readiness}.";
		}
		return true;
	}

	public bool TrySpendStageEntryFood(int stage, out string message)
	{
		if (!CanStartCampaignBattle(stage, out message))
		{
			return false;
		}

		var foodCost = GetStageEntryFoodCost(stage);
		Food -= foodCost;
		LastResultMessage = $"Caravan dispatched to stage {stage}. -{foodCost} food.";
		Persist();
		message = LastResultMessage;
		return true;
	}

	public bool CanExploreNextStage(out StageDefinition nextStage, out string message)
	{
		if (HighestUnlockedStage >= MaxStage)
		{
			nextStage = GameData.GetStage(MaxStage);
			message = "All route segments are already explored.";
			return false;
		}

		nextStage = GameData.GetStage(HighestUnlockedStage + 1);
		var foodCost = GetStageExploreFoodCost(nextStage.StageNumber);
		if (Food < foodCost)
		{
			message = $"Need {foodCost} food to explore stage {nextStage.StageNumber}.";
			return false;
		}

		message = $"Explore stage {nextStage.StageNumber} for {foodCost} food.";
		return true;
	}

	public bool TryExploreNextStage(out string message)
	{
		if (!CanExploreNextStage(out var nextStage, out message))
		{
			return false;
		}

		var previousHighestUnlockedStage = HighestUnlockedStage;
		var foodCost = GetStageExploreFoodCost(nextStage.StageNumber);
		Food -= foodCost;
		HighestUnlockedStage = nextStage.StageNumber;
		SelectedStage = nextStage.StageNumber;
		var newlyAvailableUnits = GetNewlyAvailablePlayerUnits(previousHighestUnlockedStage);
		var newlyAvailableSpells = GetNewlyAvailablePlayerSpells(previousHighestUnlockedStage);
		var availabilityParts = new List<string>();
		if (newlyAvailableUnits.Count > 0)
		{
			availabilityParts.Add($"New shop unit available: {string.Join(", ", newlyAvailableUnits.Select(unit => unit.DisplayName))}.");
		}

		if (newlyAvailableSpells.Count > 0)
		{
			availabilityParts.Add($"New spell archive unlocked: {string.Join(", ", newlyAvailableSpells.Select(spell => spell.DisplayName))}.");
		}

		var availabilitySuffix = availabilityParts.Count > 0
			? $" {string.Join(" ", availabilityParts)}"
			: "";
		LastResultMessage =
			$"Explored {nextStage.MapName} - Stage {nextStage.StageNumber}: {nextStage.StageName}. -{foodCost} food.{availabilitySuffix}";
		Persist();
		message = LastResultMessage;
		return true;
	}

	public int GetStageEntryFoodCost(int stage)
	{
		var definition = GameData.GetStage(Mathf.Clamp(stage, 1, MaxStage));
		return Math.Max(1, definition.EntryFoodCost);
	}

	public int GetStageExploreFoodCost(int stage)
	{
		var definition = GameData.GetStage(Mathf.Clamp(stage, 1, MaxStage));
		return Math.Max(1, definition.ExploreFoodCost);
	}

	public bool ToggleDeckUnit(string unitId, out string message)
	{
		if (string.IsNullOrWhiteSpace(unitId))
		{
			message = "Invalid unit.";
			return false;
		}

		var normalizedId = unitId.Trim();
		if (_activeDeckUnitIds.Contains(normalizedId, StringComparer.OrdinalIgnoreCase))
		{
			if (_activeDeckUnitIds.Count <= 1)
			{
				message = "Your deck needs at least one unit.";
				return false;
			}

			_activeDeckUnitIds.RemoveAll(id => id.Equals(normalizedId, StringComparison.OrdinalIgnoreCase));
			Persist();
			LanChallengeService.Instance?.RefreshLocalDeckProfile();
			message = "Unit moved to reserve.";
			return true;
		}

		if (_activeDeckUnitIds.Count >= MaxDeckSize)
		{
			message = $"Deck is full ({MaxDeckSize} cards). Remove one first.";
			return false;
		}

		try
		{
			var definition = GameData.GetUnit(normalizedId);
			if (!definition.IsPlayerSide)
			{
				message = "Only player units can be added to the deck.";
				return false;
			}

			if (!IsUnitOwned(definition.Id))
			{
				if (IsUnitAvailableForPurchase(definition.Id))
				{
					message = $"{definition.DisplayName} is in the shop for {GetUnitPurchaseCost(definition.Id)} gold.";
				}
				else
				{
					message = $"{definition.DisplayName} becomes available after exploring stage {definition.UnlockStage}.";
				}

				return false;
			}

			_activeDeckUnitIds.Add(definition.Id);
			Persist();
			LanChallengeService.Instance?.RefreshLocalDeckProfile();
			message = $"{definition.DisplayName} added to deck.";
			return true;
		}
		catch (Exception)
		{
			message = "Unit data was not found.";
			return false;
		}
	}

	public bool ToggleDeckSpell(string spellId, out string message)
	{
		if (string.IsNullOrWhiteSpace(spellId))
		{
			message = "Invalid spell.";
			return false;
		}

		var normalizedId = spellId.Trim();
		if (_activeDeckSpellIds.Contains(normalizedId, StringComparer.OrdinalIgnoreCase))
		{
			_activeDeckSpellIds.RemoveAll(id => id.Equals(normalizedId, StringComparison.OrdinalIgnoreCase));
			Persist();
			message = "Spell moved out of the active deck.";
			return true;
		}

		if (_activeDeckSpellIds.Count >= MaxSpellDeckSize)
		{
			message = $"Spell deck is full ({MaxSpellDeckSize} cards). Remove one first.";
			return false;
		}

		try
		{
			var definition = GameData.GetSpell(normalizedId);
			if (!IsSpellOwned(definition.Id))
			{
				if (IsSpellAvailableForPurchase(definition.Id))
				{
					message = $"{definition.DisplayName} can be scribed for {GetSpellPurchaseCost(definition.Id)} gold.";
				}
				else
				{
					message = $"{definition.DisplayName} becomes available after exploring stage {definition.UnlockStage}.";
				}

				return false;
			}

			_activeDeckSpellIds.Add(definition.Id);
			Persist();
			message = $"{definition.DisplayName} added to the active spell deck.";
			return true;
		}
		catch (Exception)
		{
			message = "Spell data was not found.";
			return false;
		}
	}

	private float GetPlayerBaseHealthScale()
	{
		return GetPlayerBaseHealthScaleAtLevel(GetBaseUpgradeLevel(BaseUpgradeCatalog.HullPlatingId));
	}

	private float GetPlayerCourageMaxBonus()
	{
		return GetPlayerCourageMaxBonusAtLevel(GetBaseUpgradeLevel(BaseUpgradeCatalog.PantryId));
	}

	private float GetPlayerCourageGainScale()
	{
		return GetPlayerCourageGainScaleAtLevel(GetBaseUpgradeLevel(BaseUpgradeCatalog.PantryId));
	}

	private float GetPlayerDeployCooldownScale()
	{
		return GetPlayerDeployCooldownScaleAtLevel(GetBaseUpgradeLevel(BaseUpgradeCatalog.DispatchConsoleId));
	}

	private float GetPlayerSignalJamDurationScale()
	{
		return GetPlayerSignalJamDurationScaleAtLevel(GetBaseUpgradeLevel(BaseUpgradeCatalog.SignalRelayId));
	}

	private float GetPlayerSignalJamCooldownPenaltyScale()
	{
		return GetPlayerSignalJamCooldownPenaltyScaleAtLevel(GetBaseUpgradeLevel(BaseUpgradeCatalog.SignalRelayId));
	}

	private float GetPlayerSignalJamSuppressionMitigation()
	{
		return GetPlayerSignalJamSuppressionMitigationAtLevel(GetBaseUpgradeLevel(BaseUpgradeCatalog.SignalRelayId));
	}

	private void LoadOrInitialize()
	{
		if (SaveSystem.Instance != null && SaveSystem.Instance.TryLoad(out var saved))
		{
			ApplySavedData(saved);
		}
		else
		{
			ApplyDefaults();
		}

		ClampState();
		GrantPendingDistrictRewardsOnLoad();
		Persist();
	}

	private void ApplyDefaults()
	{
		Gold = DefaultGold;
		Food = DefaultFood;
		HighestUnlockedStage = DefaultUnlockedStage;
		SelectedStage = DefaultUnlockedStage;
		SelectedAsyncChallengeCode = DefaultAsyncChallengeCode;
		SelectedEndlessRouteId = DefaultEndlessRouteId;
		SelectedEndlessBoonId = DefaultEndlessBoonId;
		LastResultMessage = DefaultReport;
		PlayerCallsign = DefaultPlayerCallsign;
		PlayerProfileId = GeneratePlayerProfileId();
		PlayerAuthToken = DefaultPlayerAuthToken;
		LastPlayerProfileSyncAtUnixSeconds = 0L;
		LastChallengeSyncAtUnixSeconds = 0L;
		TotalChallengeSubmissionsSynced = 0;
		ChallengeSyncProviderId = DefaultChallengeSyncProviderId;
		ChallengeSyncEndpoint = DefaultChallengeSyncEndpoint;
		ChallengeSyncAutoFlush = DefaultChallengeSyncAutoFlush;
		ShowDevUi = DefaultShowDevUi;
		ShowFpsCounter = DefaultShowFpsCounter;
		AudioMuted = DefaultAudioMuted;
		EffectsVolumePercent = DefaultEffectsVolumePercent;
		AmbienceVolumePercent = DefaultAmbienceVolumePercent;
		CurrentBattleMode = BattleRunMode.Campaign;
		_activeDeckUnitIds.Clear();
		_activeDeckUnitIds.AddRange(DefaultDeckUnitIds);
		_activeDeckSpellIds.Clear();
		_activeDeckSpellIds.AddRange(DefaultDeckSpellIds);
		_selectedAsyncChallengeLockedDeckUnitIds.Clear();
		_ownedPlayerUnitIds.Clear();
		_ownedPlayerSpellIds.Clear();
		foreach (var unitId in DefaultDeckUnitIds)
		{
			_ownedPlayerUnitIds.Add(unitId);
		}
		foreach (var spellId in DefaultDeckSpellIds)
		{
			_ownedPlayerSpellIds.Add(spellId);
		}

		_stageStars.Clear();
		_unitUpgradeLevels.Clear();
		_baseUpgradeLevels.Clear();
		_unitDoctrineSelections.Clear();
		_armedCampaignDirectiveStage = 0;
		_challengeBestScores.Clear();
		_challengeHistory.Clear();
		_pendingChallengeSubmissions.Clear();
		_pinnedChallengeCodes.Clear();
		_claimedDistrictRewardIds.Clear();
		_claimedCampaignDirectiveIds.Clear();
		BestEndlessWave = 0;
		BestEndlessTimeSeconds = 0f;
		EndlessRuns = 0;
		ChallengeRuns = 0;
	}

	private void ApplySavedData(GameSaveData saved)
	{
		Gold = saved.Version >= 8 ? saved.Gold : saved.Scrap;
		Food = saved.Version >= 8 ? saved.Food : saved.Fuel;
		HighestUnlockedStage = saved.HighestUnlockedStage;
		SelectedStage = saved.SelectedStage;
		SelectedAsyncChallengeCode = saved.Version >= 9
			? AsyncChallengeCatalog.NormalizeCode(saved.SelectedAsyncChallengeCode)
			: DefaultAsyncChallengeCode;
		_selectedAsyncChallengeLockedDeckUnitIds.Clear();
		if (saved.Version >= 13 && saved.SelectedAsyncChallengeLockedDeckUnitIds != null)
		{
			foreach (var unitId in saved.SelectedAsyncChallengeLockedDeckUnitIds)
			{
				if (!string.IsNullOrWhiteSpace(unitId))
				{
					_selectedAsyncChallengeLockedDeckUnitIds.Add(unitId.Trim());
				}
			}
		}
		SelectedEndlessRouteId = saved.Version >= 6
			? NormalizeRouteId(saved.SelectedEndlessRouteId)
			: DefaultEndlessRouteId;
		SelectedEndlessBoonId = saved.Version >= 7
			? NormalizeEndlessBoonId(saved.SelectedEndlessBoonId)
			: DefaultEndlessBoonId;
		PlayerCallsign = saved.Version >= 16
			? NormalizePlayerCallsign(saved.PlayerCallsign)
			: DefaultPlayerCallsign;
		PlayerProfileId = saved.Version >= 17
			? NormalizePlayerProfileId(saved.PlayerProfileId)
			: GeneratePlayerProfileId();
		PlayerAuthToken = saved.Version >= 20
			? NormalizePlayerAuthToken(saved.PlayerAuthToken)
			: DefaultPlayerAuthToken;
		LastPlayerProfileSyncAtUnixSeconds = saved.Version >= 20
			? Math.Max(0L, saved.LastPlayerProfileSyncAtUnixSeconds)
			: 0L;
		LastChallengeSyncAtUnixSeconds = saved.Version >= 18
			? Math.Max(0L, saved.LastChallengeSyncAtUnixSeconds)
			: 0L;
		TotalChallengeSubmissionsSynced = saved.Version >= 18
			? Math.Max(0, saved.TotalChallengeSubmissionsSynced)
			: 0;
		ChallengeSyncProviderId = saved.Version >= 19
			? ChallengeSyncProviderCatalog.NormalizeId(saved.ChallengeSyncProviderId)
			: DefaultChallengeSyncProviderId;
		ChallengeSyncEndpoint = saved.Version >= 19
			? NormalizeChallengeSyncEndpoint(saved.ChallengeSyncEndpoint)
			: DefaultChallengeSyncEndpoint;
		ChallengeSyncAutoFlush = saved.Version >= 19
			? saved.ChallengeSyncAutoFlush
			: DefaultChallengeSyncAutoFlush;
		LastResultMessage = string.IsNullOrWhiteSpace(saved.LastResultMessage)
			? DefaultReport
			: saved.LastResultMessage;
		CurrentBattleMode = BattleRunMode.Campaign;
		if (saved.Version >= 2)
		{
			ShowDevUi = saved.ShowDevUi;
			ShowFpsCounter = saved.ShowFpsCounter;
		}
		else
		{
			ShowDevUi = DefaultShowDevUi;
			ShowFpsCounter = DefaultShowFpsCounter;
		}

		if (saved.Version >= 11)
		{
			AudioMuted = saved.AudioMuted;
			EffectsVolumePercent = saved.EffectsVolumePercent;
			AmbienceVolumePercent = saved.AmbienceVolumePercent;
		}
		else
		{
			AudioMuted = DefaultAudioMuted;
			EffectsVolumePercent = DefaultEffectsVolumePercent;
			AmbienceVolumePercent = DefaultAmbienceVolumePercent;
		}

		_activeDeckUnitIds.Clear();
		if (saved.Version >= 3 && saved.ActiveDeckUnitIds != null)
		{
			foreach (var unitId in saved.ActiveDeckUnitIds)
			{
				if (string.IsNullOrWhiteSpace(unitId))
				{
					continue;
				}

				_activeDeckUnitIds.Add(unitId);
			}
		}

		_activeDeckSpellIds.Clear();
		if (saved.Version >= 21 && saved.ActiveDeckSpellIds != null)
		{
			foreach (var spellId in saved.ActiveDeckSpellIds)
			{
				if (string.IsNullOrWhiteSpace(spellId))
				{
					continue;
				}

				_activeDeckSpellIds.Add(spellId);
			}
		}

		_ownedPlayerUnitIds.Clear();
		if (saved.Version >= 8 && saved.OwnedPlayerUnitIds != null && saved.OwnedPlayerUnitIds.Length > 0)
		{
			foreach (var unitId in saved.OwnedPlayerUnitIds)
			{
				if (!string.IsNullOrWhiteSpace(unitId))
				{
					_ownedPlayerUnitIds.Add(unitId);
				}
			}
		}
		else
		{
			foreach (var unit in GameData.GetPlayerUnits())
			{
				if (unit.UnlockStage <= HighestUnlockedStage)
				{
					_ownedPlayerUnitIds.Add(unit.Id);
				}
			}
		}

		_ownedPlayerSpellIds.Clear();
		if (saved.Version >= 21 && saved.OwnedPlayerSpellIds != null && saved.OwnedPlayerSpellIds.Length > 0)
		{
			foreach (var spellId in saved.OwnedPlayerSpellIds)
			{
				if (!string.IsNullOrWhiteSpace(spellId))
				{
					_ownedPlayerSpellIds.Add(spellId);
				}
			}
		}
		else
		{
			foreach (var spell in GameData.GetPlayerSpells())
			{
				if (DefaultDeckSpellIds.Contains(spell.Id, StringComparer.OrdinalIgnoreCase) ||
					spell.GoldCost <= 0 && spell.UnlockStage <= HighestUnlockedStage)
				{
					_ownedPlayerSpellIds.Add(spell.Id);
				}
			}
		}

		_stageStars.Clear();
		if (saved.Version >= 4 && saved.StageStars != null)
		{
			foreach (var stars in saved.StageStars)
			{
				_stageStars.Add(stars);
			}
		}

		_unitUpgradeLevels.Clear();
		if (saved.Version >= 5 && saved.UnitLevels != null)
		{
			foreach (var pair in saved.UnitLevels)
			{
				if (string.IsNullOrWhiteSpace(pair.Key))
				{
					continue;
				}

				_unitUpgradeLevels[pair.Key] = pair.Value;
			}
		}

		_baseUpgradeLevels.Clear();
		if (saved.Version >= 8 && saved.BaseUpgradeLevels != null)
		{
			foreach (var pair in saved.BaseUpgradeLevels)
			{
				if (!string.IsNullOrWhiteSpace(pair.Key))
				{
					_baseUpgradeLevels[pair.Key] = pair.Value;
				}
			}
		}

		_unitDoctrineSelections.Clear();
		if (saved.Version >= 23 && saved.UnitDoctrineIds != null)
		{
			foreach (var pair in saved.UnitDoctrineIds)
			{
				if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
				{
					continue;
				}

				_unitDoctrineSelections[pair.Key.Trim()] = pair.Value.Trim();
			}
		}

		_armedCampaignDirectiveStage = saved.Version >= 24
			? Mathf.Clamp(saved.ArmedCampaignDirectiveStage, 0, MaxStage)
			: 0;

		_claimedCampaignDirectiveIds.Clear();
		if (saved.Version >= 24 && saved.ClaimedCampaignDirectiveIds != null)
		{
			foreach (var directiveId in saved.ClaimedCampaignDirectiveIds)
			{
				if (!string.IsNullOrWhiteSpace(directiveId))
				{
					_claimedCampaignDirectiveIds.Add(directiveId.Trim());
				}
			}
		}

		if (saved.Version >= 6)
		{
			BestEndlessWave = Math.Max(0, saved.BestEndlessWave);
			BestEndlessTimeSeconds = Math.Max(0f, saved.BestEndlessTimeSeconds);
			EndlessRuns = Math.Max(0, saved.EndlessRuns);
		}
		else
		{
			BestEndlessWave = 0;
			BestEndlessTimeSeconds = 0f;
			EndlessRuns = 0;
		}

		_challengeBestScores.Clear();
		if (saved.Version >= 9 && saved.ChallengeBestScores != null)
		{
			foreach (var pair in saved.ChallengeBestScores)
			{
				if (!string.IsNullOrWhiteSpace(pair.Key))
				{
					_challengeBestScores[AsyncChallengeCatalog.NormalizeCode(pair.Key)] = Math.Max(0, pair.Value);
				}
			}
		}

		ChallengeRuns = saved.Version >= 9
			? Math.Max(0, saved.ChallengeRuns)
			: 0;

		_challengeHistory.Clear();
		if (saved.Version >= 10 && saved.ChallengeHistory != null)
		{
			foreach (var entry in saved.ChallengeHistory)
			{
				if (entry == null)
				{
					continue;
				}

				_challengeHistory.Add(CloneChallengeHistoryRecord(entry));
			}
		}

		_pendingChallengeSubmissions.Clear();
		if (saved.Version >= 17 && saved.PendingChallengeSubmissions != null)
		{
			foreach (var entry in saved.PendingChallengeSubmissions)
			{
				if (entry == null)
				{
					continue;
				}

				_pendingChallengeSubmissions.Add(CloneChallengeSubmissionEnvelope(entry));
			}
		}

		_pinnedChallengeCodes.Clear();
		if (saved.Version >= 12 && saved.PinnedChallengeCodes != null)
		{
			foreach (var code in saved.PinnedChallengeCodes)
			{
				if (!string.IsNullOrWhiteSpace(code))
				{
					_pinnedChallengeCodes.Add(code);
				}
			}
		}

		_claimedDistrictRewardIds.Clear();
		if (saved.Version >= 22 && saved.ClaimedDistrictRewardIds != null)
		{
			foreach (var districtId in saved.ClaimedDistrictRewardIds)
			{
				if (!string.IsNullOrWhiteSpace(districtId))
				{
					_claimedDistrictRewardIds.Add(districtId.Trim());
				}
			}
		}
	}

	private void ClampState()
	{
		Gold = Math.Max(0, Gold);
		Food = Math.Max(0, Food);

		if (MaxStage <= 0)
		{
			HighestUnlockedStage = 1;
			SelectedStage = 1;
			return;
		}

		if (HighestUnlockedStage < 1)
		{
			HighestUnlockedStage = 1;
		}
		else if (HighestUnlockedStage > MaxStage)
		{
			HighestUnlockedStage = MaxStage;
		}

		if (SelectedStage < 1)
		{
			SelectedStage = 1;
		}
		else if (SelectedStage > HighestUnlockedStage)
		{
			SelectedStage = HighestUnlockedStage;
		}

		NormalizeOwnedUnits();
		NormalizeOwnedSpells();
		NormalizeDeck();
		NormalizeSpellDeck();
		NormalizeStageStars();
		NormalizeUnitLevels();
		NormalizeBaseUpgrades();
		NormalizeUnitDoctrines();
		NormalizeChallengeScores();
		NormalizeChallengeHistory();
		NormalizePendingChallengeSubmissions();
		NormalizePinnedChallenges();
		NormalizeClaimedDistrictRewards();
		NormalizeCampaignDirectives();
		NormalizeSelectedAsyncChallengeLockedDeck();
		SelectedEndlessRouteId = NormalizeRouteId(SelectedEndlessRouteId);
		SelectedEndlessBoonId = NormalizeEndlessBoonId(SelectedEndlessBoonId);
		PlayerCallsign = NormalizePlayerCallsign(PlayerCallsign);
		PlayerProfileId = NormalizePlayerProfileId(PlayerProfileId);
		PlayerAuthToken = NormalizePlayerAuthToken(PlayerAuthToken);
		LastPlayerProfileSyncAtUnixSeconds = Math.Max(0L, LastPlayerProfileSyncAtUnixSeconds);
		ChallengeSyncProviderId = ChallengeSyncProviderCatalog.NormalizeId(ChallengeSyncProviderId);
		ChallengeSyncEndpoint = NormalizeChallengeSyncEndpoint(ChallengeSyncEndpoint);
		SelectedAsyncChallengeCode = GetSelectedAsyncChallenge().Code;
		BestEndlessWave = Math.Max(0, BestEndlessWave);
		BestEndlessTimeSeconds = Math.Max(0f, BestEndlessTimeSeconds);
		EndlessRuns = Math.Max(0, EndlessRuns);
		ChallengeRuns = Math.Max(0, ChallengeRuns);
		LastChallengeSyncAtUnixSeconds = Math.Max(0L, LastChallengeSyncAtUnixSeconds);
		TotalChallengeSubmissionsSynced = Math.Max(0, TotalChallengeSubmissionsSynced);
		EffectsVolumePercent = Mathf.Clamp(EffectsVolumePercent, 0, 100);
		AmbienceVolumePercent = Mathf.Clamp(AmbienceVolumePercent, 0, 100);
	}

	private void UnlockNextStageInternal(int clearedStage)
	{
		var nextStage = clearedStage + 1;
		if (nextStage > HighestUnlockedStage)
		{
			HighestUnlockedStage = nextStage;
		}

		if (HighestUnlockedStage > MaxStage)
		{
			HighestUnlockedStage = MaxStage;
		}
	}

	private GameSaveData BuildSaveData()
	{
		return new GameSaveData
		{
			Gold = Gold,
			Food = Food,
			HighestUnlockedStage = HighestUnlockedStage,
			SelectedStage = SelectedStage,
			SelectedAsyncChallengeCode = SelectedAsyncChallengeCode,
			SelectedAsyncChallengeLockedDeckUnitIds = _selectedAsyncChallengeLockedDeckUnitIds.ToArray(),
			SelectedEndlessRouteId = SelectedEndlessRouteId,
			SelectedEndlessBoonId = SelectedEndlessBoonId,
			LastResultMessage = LastResultMessage,
			PlayerCallsign = PlayerCallsign,
			PlayerProfileId = PlayerProfileId,
			PlayerAuthToken = PlayerAuthToken,
			LastPlayerProfileSyncAtUnixSeconds = LastPlayerProfileSyncAtUnixSeconds,
			ChallengeSyncProviderId = ChallengeSyncProviderId,
			ChallengeSyncEndpoint = ChallengeSyncEndpoint,
			ChallengeSyncAutoFlush = ChallengeSyncAutoFlush,
			ShowDevUi = ShowDevUi,
			ShowFpsCounter = ShowFpsCounter,
			AudioMuted = AudioMuted,
			EffectsVolumePercent = EffectsVolumePercent,
			AmbienceVolumePercent = AmbienceVolumePercent,
			ActiveDeckUnitIds = _activeDeckUnitIds.ToArray(),
			ActiveDeckSpellIds = _activeDeckSpellIds.ToArray(),
			OwnedPlayerUnitIds = _ownedPlayerUnitIds.ToArray(),
			OwnedPlayerSpellIds = _ownedPlayerSpellIds.ToArray(),
			StageStars = _stageStars.ToArray(),
			UnitLevels = new Dictionary<string, int>(_unitUpgradeLevels),
			BaseUpgradeLevels = new Dictionary<string, int>(_baseUpgradeLevels),
			UnitDoctrineIds = new Dictionary<string, string>(_unitDoctrineSelections),
			BestEndlessWave = BestEndlessWave,
			BestEndlessTimeSeconds = BestEndlessTimeSeconds,
			EndlessRuns = EndlessRuns,
			ChallengeBestScores = new Dictionary<string, int>(_challengeBestScores),
			ChallengeRuns = ChallengeRuns,
			ChallengeHistory = _challengeHistory
				.Select(CloneChallengeHistoryRecord)
				.ToList(),
			PendingChallengeSubmissions = _pendingChallengeSubmissions
				.Select(CloneChallengeSubmissionEnvelope)
				.ToList(),
			LastChallengeSyncAtUnixSeconds = LastChallengeSyncAtUnixSeconds,
			TotalChallengeSubmissionsSynced = TotalChallengeSubmissionsSynced,
			PinnedChallengeCodes = _pinnedChallengeCodes.ToArray(),
			ClaimedDistrictRewardIds = _claimedDistrictRewardIds.ToArray(),
			ArmedCampaignDirectiveStage = _armedCampaignDirectiveStage,
			ClaimedCampaignDirectiveIds = _claimedCampaignDirectiveIds.ToArray()
		};
	}

	public bool HasClaimedDistrictReward(string districtId)
	{
		return _claimedDistrictRewardIds.Contains(NormalizeRouteId(districtId));
	}

	public bool IsDistrictCleared(string districtId)
	{
		var stages = GameData.GetStagesForMap(districtId);
		return stages.Count > 0 && stages.All(stage => GetStageStars(stage.StageNumber) > 0);
	}

	public string BuildDistrictRewardStatusText(string districtId)
	{
		if (!CampaignPlanCatalog.TryGet(districtId, out var district))
		{
			return "District reward: none";
		}

		var totalStages = GameData.GetStagesForMap(district.Id).Count;
		var clearedStages = GetClearedDistrictStageCount(district.Id);
		var rewardText = $"+{district.RewardGold} gold, +{district.RewardFood} food";
		return HasClaimedDistrictReward(district.Id)
			? $"District reward claimed: {rewardText}"
			: $"District reward on full clear: {rewardText}  |  {clearedStages}/{Math.Max(1, totalStages)} cleared";
	}

	private int GetClearedDistrictStageCount(string districtId)
	{
		return GameData.GetStagesForMap(districtId)
			.Count(stage => GetStageStars(stage.StageNumber) > 0);
	}

	private string TryClaimDistrictRewardForStage(int stage)
	{
		var stageData = GameData.GetStage(stage);
		if (!CampaignPlanCatalog.TryGet(stageData.MapId, out var district) ||
			_claimedDistrictRewardIds.Contains(district.Id) ||
			!IsDistrictCleared(district.Id))
		{
			return "";
		}

		_claimedDistrictRewardIds.Add(district.Id);
		Gold += Math.Max(0, district.RewardGold);
		Food += Math.Max(0, district.RewardFood);
		return $"{district.Title} secured. District reward: +{district.RewardGold} gold, +{district.RewardFood} food.";
	}

	private string TryClaimCampaignDirectiveReward(int stage)
	{
		if (!IsCampaignDirectiveArmed(stage))
		{
			return "";
		}

		var directive = GetCampaignDirective(stage);
		if (directive == null)
		{
			return "";
		}

		if (_claimedCampaignDirectiveIds.Contains(directive.Id))
		{
			return $"Heroic directive replayed: {directive.Title}. Bounty already claimed.";
		}

		_claimedCampaignDirectiveIds.Add(directive.Id);
		Gold += directive.BonusGold;
		Food += directive.BonusFood;
		return directive.BonusFood > 0
			? $"Heroic directive secured: {directive.Title}. +{directive.BonusGold} gold, +{directive.BonusFood} food."
			: $"Heroic directive secured: {directive.Title}. +{directive.BonusGold} gold.";
	}

	private static string BuildCombinedCampaignBonusSummary(params string[] summaries)
	{
		return string.Join(
			" ",
			summaries
				.Where(summary => !string.IsNullOrWhiteSpace(summary))
				.Select(summary => summary.Trim()));
	}

	private static StageDefinition CloneStageWithDirective(StageDefinition stage, CampaignDirectiveDefinition directive)
	{
		return new StageDefinition
		{
			StageNumber = stage.StageNumber,
			StageName = stage.StageName,
			MapId = stage.MapId,
			MapName = stage.MapName,
			TerrainId = stage.TerrainId,
			Description = stage.Description,
			RewardGold = stage.RewardGold,
			RewardFood = stage.RewardFood,
			EntryFoodCost = stage.EntryFoodCost,
			ExploreFoodCost = stage.ExploreFoodCost,
			MapX = stage.MapX,
			MapY = stage.MapY,
			PlayerBaseHealth = stage.PlayerBaseHealth,
			EnemyBaseHealth = stage.EnemyBaseHealth,
			EnemySpawnMin = stage.EnemySpawnMin,
			EnemySpawnMax = stage.EnemySpawnMax,
			EnemyHealthScale = stage.EnemyHealthScale,
			EnemyDamageScale = stage.EnemyDamageScale,
			WalkerWeight = stage.WalkerWeight,
			RunnerWeight = stage.RunnerWeight,
			BruteWeight = stage.BruteWeight,
			SpitterWeight = stage.SpitterWeight,
			CrusherWeight = stage.CrusherWeight,
			BossWeight = stage.BossWeight,
			BossSpawnStartTime = stage.BossSpawnStartTime,
			BonusWaveChance = stage.BonusWaveChance,
			TwoStarBusHullRatio = stage.TwoStarBusHullRatio,
			ThreeStarTimeLimitSeconds = stage.ThreeStarTimeLimitSeconds,
			Hazards = stage.Hazards,
			Modifiers = CampaignDirectiveCatalog.CombineModifiers(stage, directive)
				.Select(modifier => modifier.Clone())
				.ToArray(),
			Objectives = stage.Objectives,
			MissionEvents = stage.MissionEvents,
			Waves = stage.Waves
		};
	}

	private void GrantPendingDistrictRewardsOnLoad()
	{
		var grantedDistricts = new List<CampaignDistrictPlan>();
		foreach (var district in CampaignPlanCatalog.GetAll())
		{
			if (_claimedDistrictRewardIds.Contains(district.Id) || !IsDistrictCleared(district.Id))
			{
				continue;
			}

			_claimedDistrictRewardIds.Add(district.Id);
			Gold += Math.Max(0, district.RewardGold);
			Food += Math.Max(0, district.RewardFood);
			grantedDistricts.Add(district);
		}

		if (grantedDistricts.Count == 0)
		{
			return;
		}

		var totalGold = grantedDistricts.Sum(district => district.RewardGold);
		var totalFood = grantedDistricts.Sum(district => district.RewardFood);
		LastResultMessage = grantedDistricts.Count == 1
			? $"{grantedDistricts[0].Title} district reward granted on load. +{totalGold} gold, +{totalFood} food."
			: $"Campaign district rewards reconciled on load. +{totalGold} gold, +{totalFood} food across {grantedDistricts.Count} cleared districts.";
	}

	private void NormalizeOwnedUnits()
	{
		var validPlayerIds = new HashSet<string>(GameData.PlayerRosterIds, StringComparer.OrdinalIgnoreCase);
		_ownedPlayerUnitIds.RemoveWhere(unitId => !validPlayerIds.Contains(unitId));
		foreach (var defaultId in DefaultDeckUnitIds)
		{
			_ownedPlayerUnitIds.Add(defaultId);
		}
	}

	private void NormalizeOwnedSpells()
	{
		var validSpellIds = new HashSet<string>(GameData.PlayerSpellIds, StringComparer.OrdinalIgnoreCase);
		_ownedPlayerSpellIds.RemoveWhere(spellId => !validSpellIds.Contains(spellId));
		foreach (var defaultId in DefaultDeckSpellIds)
		{
			_ownedPlayerSpellIds.Add(defaultId);
		}
	}

	private void NormalizeDeck()
	{
		var validPlayerIds = new HashSet<string>(_ownedPlayerUnitIds, StringComparer.OrdinalIgnoreCase);
		_activeDeckUnitIds.RemoveAll(unitId => !validPlayerIds.Contains(unitId));

		for (var i = _activeDeckUnitIds.Count - 1; i >= 0; i--)
		{
			var currentId = _activeDeckUnitIds[i];
			for (var j = 0; j < i; j++)
			{
				if (!_activeDeckUnitIds[j].Equals(currentId, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				_activeDeckUnitIds.RemoveAt(i);
				break;
			}
		}

		if (_activeDeckUnitIds.Count > MaxDeckSize)
		{
			_activeDeckUnitIds.RemoveRange(MaxDeckSize, _activeDeckUnitIds.Count - MaxDeckSize);
		}

		foreach (var defaultId in DefaultDeckUnitIds)
		{
			if (_activeDeckUnitIds.Count >= MaxDeckSize)
			{
				break;
			}

			if (!IsUnitOwned(defaultId))
			{
				continue;
			}

			if (_activeDeckUnitIds.Contains(defaultId, StringComparer.OrdinalIgnoreCase))
			{
				continue;
			}

			_activeDeckUnitIds.Add(defaultId);
		}
	}

	private void NormalizeSpellDeck()
	{
		var validSpellIds = new HashSet<string>(_ownedPlayerSpellIds, StringComparer.OrdinalIgnoreCase);
		_activeDeckSpellIds.RemoveAll(spellId => !validSpellIds.Contains(spellId));

		for (var i = _activeDeckSpellIds.Count - 1; i >= 0; i--)
		{
			var currentId = _activeDeckSpellIds[i];
			for (var j = 0; j < i; j++)
			{
				if (!_activeDeckSpellIds[j].Equals(currentId, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				_activeDeckSpellIds.RemoveAt(i);
				break;
			}
		}

		if (_activeDeckSpellIds.Count > MaxSpellDeckSize)
		{
			_activeDeckSpellIds.RemoveRange(MaxSpellDeckSize, _activeDeckSpellIds.Count - MaxSpellDeckSize);
		}
	}

	private void NormalizeStageStars()
	{
		for (var i = 0; i < _stageStars.Count; i++)
		{
			_stageStars[i] = Mathf.Clamp(_stageStars[i], 0, 3);
		}

		if (_stageStars.Count > MaxStage)
		{
			_stageStars.RemoveRange(MaxStage, _stageStars.Count - MaxStage);
		}

		while (_stageStars.Count < MaxStage)
		{
			_stageStars.Add(0);
		}
	}

	private void NormalizeClaimedDistrictRewards()
	{
		var validDistrictIds = new HashSet<string>(
			CampaignPlanCatalog.GetAll().Select(district => district.Id),
			StringComparer.OrdinalIgnoreCase);
		_claimedDistrictRewardIds.RemoveWhere(districtId => !validDistrictIds.Contains(districtId));
	}

	private void NormalizeCampaignDirectives()
	{
		var validDirectiveIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		for (var stage = 1; stage <= MaxStage; stage++)
		{
			var directive = GetCampaignDirective(stage);
			if (directive != null)
			{
				validDirectiveIds.Add(directive.Id);
			}
		}

		_claimedCampaignDirectiveIds.RemoveWhere(directiveId => !validDirectiveIds.Contains(directiveId));
		if (_armedCampaignDirectiveStage < 0 || _armedCampaignDirectiveStage > MaxStage)
		{
			_armedCampaignDirectiveStage = 0;
			return;
		}

		if (_armedCampaignDirectiveStage > 0 && !IsCampaignDirectiveUnlocked(_armedCampaignDirectiveStage))
		{
			_armedCampaignDirectiveStage = 0;
		}
	}

	private int RecordStageStars(int stage, int starsEarned)
	{
		NormalizeStageStars();
		if (stage < 1 || stage > _stageStars.Count)
		{
			return 0;
		}

		var bestStars = Mathf.Max(_stageStars[stage - 1], Mathf.Clamp(starsEarned, 0, 3));
		_stageStars[stage - 1] = bestStars;
		return bestStars;
	}

	private void NormalizeUnitLevels()
	{
		var validPlayerIds = new HashSet<string>(GameData.PlayerRosterIds, StringComparer.OrdinalIgnoreCase);
		var invalidKeys = new List<string>();

		foreach (var pair in _unitUpgradeLevels)
		{
			if (!validPlayerIds.Contains(pair.Key))
			{
				invalidKeys.Add(pair.Key);
				continue;
			}

			_unitUpgradeLevels[pair.Key] = Mathf.Clamp(pair.Value, DefaultUnitLevel, MaxPlayerUnitLevel);
		}

		foreach (var invalidKey in invalidKeys)
		{
			_unitUpgradeLevels.Remove(invalidKey);
		}

		foreach (var unitId in GameData.PlayerRosterIds)
		{
			if (_unitUpgradeLevels.ContainsKey(unitId))
			{
				continue;
			}

			_unitUpgradeLevels[unitId] = DefaultUnitLevel;
		}
	}

	private void NormalizeUnitDoctrines()
	{
		var validPlayerIds = new HashSet<string>(GameData.PlayerRosterIds, StringComparer.OrdinalIgnoreCase);
		var invalidKeys = new List<string>();

		foreach (var pair in _unitDoctrineSelections)
		{
			if (!validPlayerIds.Contains(pair.Key))
			{
				invalidKeys.Add(pair.Key);
				continue;
			}

			if (!IsUnitOwned(pair.Key) || GetUnitLevel(pair.Key) < UnitDoctrineUnlockLevelValue)
			{
				invalidKeys.Add(pair.Key);
				continue;
			}

			var definition = GameData.GetUnit(pair.Key);
			var doctrine = UnitDoctrineCatalog.GetOrNull(pair.Value);
			if (doctrine == null ||
				!definition.IsPlayerSide ||
				!doctrine.SquadTag.Equals(
					SquadSynergyCatalog.NormalizeTag(definition.SquadTag),
					StringComparison.OrdinalIgnoreCase))
			{
				invalidKeys.Add(pair.Key);
				continue;
			}

			_unitDoctrineSelections[pair.Key] = doctrine.Id;
		}

		foreach (var invalidKey in invalidKeys)
		{
			_unitDoctrineSelections.Remove(invalidKey);
		}
	}

	private void NormalizeBaseUpgrades()
	{
		var validUpgradeIds = new HashSet<string>(
			BaseUpgradeCatalog.GetAll().Select(upgrade => upgrade.Id),
			StringComparer.OrdinalIgnoreCase);
		var invalidKeys = new List<string>();

		foreach (var pair in _baseUpgradeLevels)
		{
			if (!validUpgradeIds.Contains(pair.Key))
			{
				invalidKeys.Add(pair.Key);
				continue;
			}

			_baseUpgradeLevels[pair.Key] = Mathf.Clamp(pair.Value, 0, MaxPersistentBaseUpgradeLevel);
		}

		foreach (var invalidKey in invalidKeys)
		{
			_baseUpgradeLevels.Remove(invalidKey);
		}

		foreach (var upgrade in BaseUpgradeCatalog.GetAll())
		{
			if (_baseUpgradeLevels.ContainsKey(upgrade.Id))
			{
				continue;
			}

			_baseUpgradeLevels[upgrade.Id] = 0;
		}
	}

	private void NormalizeChallengeScores()
	{
		var invalidKeys = _challengeBestScores.Keys
			.Where(key => string.IsNullOrWhiteSpace(key))
			.ToArray();

		foreach (var invalidKey in invalidKeys)
		{
			_challengeBestScores.Remove(invalidKey);
		}

		foreach (var key in _challengeBestScores.Keys.ToArray())
		{
			var normalized = AsyncChallengeCatalog.NormalizeCode(key);
			var score = Math.Max(0, _challengeBestScores[key]);
			if (!key.Equals(normalized, StringComparison.OrdinalIgnoreCase))
			{
				_challengeBestScores.Remove(key);
			}

			_challengeBestScores[normalized] = score;
		}
	}

	private void NormalizeChallengeHistory()
	{
		_challengeHistory.RemoveAll(entry => entry == null);
		var validUnitIds = new HashSet<string>(GameData.PlayerRosterIds, StringComparer.OrdinalIgnoreCase);
		for (var i = 0; i < _challengeHistory.Count; i++)
		{
			var entry = _challengeHistory[i];
			entry.Code = AsyncChallengeCatalog.NormalizeCode(entry.Code);
			entry.Stage = Mathf.Clamp(entry.Stage, 1, MaxStage);
			entry.MutatorId = AsyncChallengeCatalog.NormalizeMutatorId(entry.MutatorId);
			entry.Score = Math.Max(0, entry.Score);
			entry.ElapsedSeconds = Math.Max(0f, entry.ElapsedSeconds);
			entry.EnemyDefeats = Math.Max(0, entry.EnemyDefeats);
			entry.StarsEarned = Mathf.Clamp(entry.StarsEarned, 0, 3);
			entry.CompletionBonus = Math.Max(0, entry.CompletionBonus);
			entry.StarBonus = Math.Max(0, entry.StarBonus);
			entry.KillBonus = Math.Max(0, entry.KillBonus);
			entry.HullBonus = Math.Max(0, entry.HullBonus);
			entry.TimeBonus = Math.Max(0, entry.TimeBonus);
			entry.DeployPenalty = Math.Max(0, entry.DeployPenalty);
			entry.RawScore = Math.Max(0, entry.RawScore);
			entry.ScoreMultiplier = entry.ScoreMultiplier > 0f ? entry.ScoreMultiplier : 1f;
			if (entry.RawScore == 0 && entry.Score > 0)
			{
				entry.RawScore = entry.Score;
			}
			entry.PlayerDeployments = Math.Max(0, entry.PlayerDeployments);
			entry.BusHullRatio = Mathf.Clamp(entry.BusHullRatio, 0f, 1f);
			entry.PlayedAtUnixSeconds = Math.Max(0L, entry.PlayedAtUnixSeconds);
			entry.DeckUnitIds = NormalizeChallengeDeckUnitIds(entry.DeckUnitIds, validUnitIds);
			entry.Deployments = NormalizeChallengeDeployments(entry.Deployments, validUnitIds);
		}

		_challengeHistory.Sort((left, right) =>
		{
			var playedComparison = right.PlayedAtUnixSeconds.CompareTo(left.PlayedAtUnixSeconds);
			return playedComparison != 0 ? playedComparison : right.Score.CompareTo(left.Score);
		});

		const int maxEntries = 18;
		if (_challengeHistory.Count > maxEntries)
		{
			_challengeHistory.RemoveRange(maxEntries, _challengeHistory.Count - maxEntries);
		}
	}

	private void NormalizePendingChallengeSubmissions()
	{
		_pendingChallengeSubmissions.RemoveAll(entry => entry == null);
		var validUnitIds = new HashSet<string>(GameData.PlayerRosterIds, StringComparer.OrdinalIgnoreCase);
		var seenSubmissionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		for (var i = _pendingChallengeSubmissions.Count - 1; i >= 0; i--)
		{
			var entry = _pendingChallengeSubmissions[i];
			entry.SubmissionId = NormalizeSubmissionId(entry.SubmissionId);
			if (!seenSubmissionIds.Add(entry.SubmissionId))
			{
				_pendingChallengeSubmissions.RemoveAt(i);
				continue;
			}

			entry.PlayerProfileId = NormalizePlayerProfileId(entry.PlayerProfileId);
			entry.PlayerCallsign = NormalizePlayerCallsign(entry.PlayerCallsign);
			entry.Code = AsyncChallengeCatalog.NormalizeCode(entry.Code);
			entry.Stage = Mathf.Clamp(entry.Stage, 1, MaxStage);
			entry.MutatorId = AsyncChallengeCatalog.NormalizeMutatorId(entry.MutatorId);
			entry.Score = Math.Max(0, entry.Score);
			entry.RawScore = Math.Max(0, entry.RawScore);
			if (entry.RawScore == 0 && entry.Score > 0)
			{
				entry.RawScore = entry.Score;
			}

			entry.ScoreMultiplier = entry.ScoreMultiplier > 0f ? entry.ScoreMultiplier : 1f;
			entry.ElapsedSeconds = Math.Max(0f, entry.ElapsedSeconds);
			entry.EnemyDefeats = Math.Max(0, entry.EnemyDefeats);
			entry.StarsEarned = Mathf.Clamp(entry.StarsEarned, 0, 3);
			entry.DeckUnitIds = NormalizeChallengeDeckUnitIds(entry.DeckUnitIds, validUnitIds);
			entry.PlayerDeployments = Math.Max(0, entry.PlayerDeployments);
			entry.HullPercent = Mathf.Clamp(entry.HullPercent, 0, 100);
			entry.QueuedAtUnixSeconds = Math.Max(0L, entry.QueuedAtUnixSeconds);
			entry.UploadAttempts = Math.Max(0, entry.UploadAttempts);
			entry.LastUploadAttemptUnixSeconds = Math.Max(0L, entry.LastUploadAttemptUnixSeconds);
			entry.Deployments = NormalizeChallengeDeployments(entry.Deployments, validUnitIds);
		}

		_pendingChallengeSubmissions.Sort((left, right) =>
		{
			var queuedComparison = right.QueuedAtUnixSeconds.CompareTo(left.QueuedAtUnixSeconds);
			return queuedComparison != 0 ? queuedComparison : right.Score.CompareTo(left.Score);
		});

		if (_pendingChallengeSubmissions.Count > MaxPendingChallengeSubmissions)
		{
			_pendingChallengeSubmissions.RemoveRange(MaxPendingChallengeSubmissions, _pendingChallengeSubmissions.Count - MaxPendingChallengeSubmissions);
		}
	}

	private void NormalizePinnedChallenges()
	{
		var normalized = new List<string>();
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (var code in _pinnedChallengeCodes)
		{
			var normalizedCode = NormalizePinnedChallengeCode(code);
			if (string.IsNullOrWhiteSpace(normalizedCode) || !seen.Add(normalizedCode))
			{
				continue;
			}

			normalized.Add(normalizedCode);
			if (normalized.Count >= MaxPinnedChallenges)
			{
				break;
			}
		}

		_pinnedChallengeCodes.Clear();
		_pinnedChallengeCodes.AddRange(normalized);
	}

	private void NormalizeSelectedAsyncChallengeLockedDeck()
	{
		var validIds = new HashSet<string>(GameData.PlayerRosterIds, StringComparer.OrdinalIgnoreCase);
		var normalized = new List<string>();

		foreach (var unitId in _selectedAsyncChallengeLockedDeckUnitIds)
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
			if (normalized.Count >= MaxDeckSize)
			{
				break;
			}
		}

		_selectedAsyncChallengeLockedDeckUnitIds.Clear();
		if (normalized.Count == MaxDeckSize)
		{
			_selectedAsyncChallengeLockedDeckUnitIds.AddRange(normalized);
		}
	}

	private static ChallengeRunRecord CloneChallengeHistoryRecord(ChallengeRunRecord record)
	{
		return new ChallengeRunRecord
		{
			Code = AsyncChallengeCatalog.NormalizeCode(record.Code),
			Stage = Math.Max(1, record.Stage),
			MutatorId = AsyncChallengeCatalog.NormalizeMutatorId(record.MutatorId),
			Score = Math.Max(0, record.Score),
			Won = record.Won,
			Retreated = record.Retreated,
			ElapsedSeconds = Math.Max(0f, record.ElapsedSeconds),
			EnemyDefeats = Math.Max(0, record.EnemyDefeats),
			StarsEarned = Mathf.Clamp(record.StarsEarned, 0, 3),
			CompletionBonus = Math.Max(0, record.CompletionBonus),
			StarBonus = Math.Max(0, record.StarBonus),
			KillBonus = Math.Max(0, record.KillBonus),
			HullBonus = Math.Max(0, record.HullBonus),
			TimeBonus = Math.Max(0, record.TimeBonus),
			DeployPenalty = Math.Max(0, record.DeployPenalty),
			RawScore = Math.Max(0, record.RawScore),
			ScoreMultiplier = record.ScoreMultiplier > 0f ? record.ScoreMultiplier : 1f,
			UsedLockedDeck = record.UsedLockedDeck,
			DeckUnitIds = record.DeckUnitIds?.ToArray() ?? [],
			PlayerDeployments = Math.Max(0, record.PlayerDeployments),
			BusHullRatio = Mathf.Clamp(record.BusHullRatio, 0f, 1f),
			Deployments = record.Deployments?
				.Select(CloneChallengeDeploymentRecord)
				.ToList() ?? [],
			PlayedAtUnixSeconds = Math.Max(0L, record.PlayedAtUnixSeconds)
		};
	}

	private static ChallengeSubmissionEnvelope CloneChallengeSubmissionEnvelope(ChallengeSubmissionEnvelope entry)
	{
		return new ChallengeSubmissionEnvelope
		{
			SubmissionId = entry?.SubmissionId ?? "",
			PlayerProfileId = entry?.PlayerProfileId ?? "",
			PlayerCallsign = entry?.PlayerCallsign ?? "",
			Code = AsyncChallengeCatalog.NormalizeCode(entry?.Code ?? ""),
			Stage = Math.Max(1, entry?.Stage ?? 1),
			MutatorId = AsyncChallengeCatalog.NormalizeMutatorId(entry?.MutatorId ?? AsyncChallengeCatalog.PressureSpikeId),
			Score = Math.Max(0, entry?.Score ?? 0),
			RawScore = Math.Max(0, entry?.RawScore ?? 0),
			ScoreMultiplier = entry != null && entry.ScoreMultiplier > 0f ? entry.ScoreMultiplier : 1f,
			Won = entry?.Won ?? false,
			Retreated = entry?.Retreated ?? false,
			ElapsedSeconds = Math.Max(0f, entry?.ElapsedSeconds ?? 0f),
			EnemyDefeats = Math.Max(0, entry?.EnemyDefeats ?? 0),
			StarsEarned = Mathf.Clamp(entry?.StarsEarned ?? 0, 0, 3),
			UsedLockedDeck = entry?.UsedLockedDeck ?? false,
			DeckUnitIds = entry?.DeckUnitIds?.ToArray() ?? [],
			PlayerDeployments = Math.Max(0, entry?.PlayerDeployments ?? 0),
			HullPercent = Mathf.Clamp(entry?.HullPercent ?? 0, 0, 100),
			QueuedAtUnixSeconds = Math.Max(0L, entry?.QueuedAtUnixSeconds ?? 0L),
			UploadAttempts = Math.Max(0, entry?.UploadAttempts ?? 0),
			LastUploadAttemptUnixSeconds = Math.Max(0L, entry?.LastUploadAttemptUnixSeconds ?? 0L),
			Deployments = entry?.Deployments?
				.Select(CloneChallengeDeploymentRecord)
				.ToList() ?? []
		};
	}

	private static string NormalizePlayerCallsign(string callsign)
	{
		var raw = string.IsNullOrWhiteSpace(callsign)
			? DefaultPlayerCallsign
			: callsign.Trim();
		var builder = new StringBuilder();
		foreach (var character in raw)
		{
			if (char.IsLetterOrDigit(character) || character == ' ' || character == '-' || character == '_')
			{
				builder.Append(character);
			}
		}

		var normalized = builder.ToString().Trim();
		if (string.IsNullOrWhiteSpace(normalized))
		{
			normalized = DefaultPlayerCallsign;
		}

		if (normalized.Length > 18)
		{
			normalized = normalized[..18].TrimEnd();
		}

		return string.IsNullOrWhiteSpace(normalized)
			? DefaultPlayerCallsign
			: normalized;
	}

	private static string NormalizePlayerProfileId(string profileId)
	{
		var normalized = NormalizeSubmissionId(profileId);
		return string.IsNullOrWhiteSpace(normalized)
			? GeneratePlayerProfileId()
			: normalized;
	}

	private static string NormalizePlayerAuthToken(string authToken)
	{
		if (string.IsNullOrWhiteSpace(authToken))
		{
			return "";
		}

		var normalized = authToken.Trim();
		return normalized.Length > 240
			? normalized[..240]
			: normalized;
	}

	private static string GeneratePlayerProfileId()
	{
		var token = Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
		return $"CVY-{token}";
	}

	private static string NormalizeChallengeSyncEndpoint(string endpoint)
	{
		var normalized = string.IsNullOrWhiteSpace(endpoint)
			? ""
			: endpoint.Trim();
		if (string.IsNullOrWhiteSpace(normalized))
		{
			return "";
		}

		return normalized.Length > 240
			? normalized[..240]
			: normalized;
	}

	private static string NormalizeSubmissionId(string submissionId)
	{
		if (string.IsNullOrWhiteSpace(submissionId))
		{
			return Guid.NewGuid().ToString("N").ToUpperInvariant();
		}

		var builder = new StringBuilder();
		foreach (var character in submissionId.Trim())
		{
			if (char.IsLetterOrDigit(character) || character == '-')
			{
				builder.Append(char.ToUpperInvariant(character));
			}
		}

		return builder.Length == 0
			? Guid.NewGuid().ToString("N").ToUpperInvariant()
			: builder.ToString();
	}

	private static ChallengeDeploymentRecord CloneChallengeDeploymentRecord(ChallengeDeploymentRecord record)
	{
		return new ChallengeDeploymentRecord
		{
			UnitId = record?.UnitId ?? "",
			TimeSeconds = Math.Max(0f, record?.TimeSeconds ?? 0f),
			LanePercent = Mathf.Clamp(record?.LanePercent ?? 0, 0, 100)
		};
	}

	private static bool IsBetterChallengeGhostCandidate(ChallengeRunRecord candidate, ChallengeRunRecord current)
	{
		if (candidate == null)
		{
			return false;
		}

		if (current == null)
		{
			return true;
		}

		var candidateOutcome = candidate.Won ? 2 : candidate.Retreated ? 0 : 1;
		var currentOutcome = current.Won ? 2 : current.Retreated ? 0 : 1;
		if (candidateOutcome != currentOutcome)
		{
			return candidateOutcome > currentOutcome;
		}

		if (candidate.Score != current.Score)
		{
			return candidate.Score > current.Score;
		}

		if (candidate.StarsEarned != current.StarsEarned)
		{
			return candidate.StarsEarned > current.StarsEarned;
		}

		if (!Mathf.IsEqualApprox(candidate.BusHullRatio, current.BusHullRatio))
		{
			return candidate.BusHullRatio > current.BusHullRatio;
		}

		return candidate.PlayedAtUnixSeconds > current.PlayedAtUnixSeconds;
	}

	private static AsyncChallengeScoreBreakdown BuildChallengeRunScoreBreakdown(ChallengeRunRecord record)
	{
		var completionBonus = Math.Max(0, record?.CompletionBonus ?? 0);
		var starBonus = Math.Max(0, record?.StarBonus ?? 0);
		var killBonus = Math.Max(0, record?.KillBonus ?? 0);
		var hullBonus = Math.Max(0, record?.HullBonus ?? 0);
		var timeBonus = Math.Max(0, record?.TimeBonus ?? 0);
		var deployPenalty = Math.Max(0, record?.DeployPenalty ?? 0);
		var multiplier = record != null && record.ScoreMultiplier > 0f ? record.ScoreMultiplier : 1f;
		var rawScore = Math.Max(0, record?.RawScore ?? 0);
		if (rawScore == 0)
		{
			rawScore = Math.Max(0, completionBonus + starBonus + killBonus + hullBonus + timeBonus - deployPenalty);
		}

		if (rawScore == 0)
		{
			rawScore = Math.Max(0, record?.Score ?? 0);
		}

		var finalScore = Math.Max(0, record?.Score ?? 0);
		if (finalScore == 0 && rawScore > 0)
		{
			finalScore = Mathf.Max(0, Mathf.RoundToInt(rawScore * multiplier));
		}

		return new AsyncChallengeScoreBreakdown(
			completionBonus,
			starBonus,
			killBonus,
			hullBonus,
			timeBonus,
			deployPenalty,
			rawScore,
			multiplier,
			finalScore);
	}

	private static string[] NormalizeChallengeDeckUnitIds(IEnumerable<string> deckUnitIds, HashSet<string> validUnitIds)
	{
		if (deckUnitIds == null)
		{
			return [];
		}

		var normalized = new List<string>();
		foreach (var unitId in deckUnitIds)
		{
			if (string.IsNullOrWhiteSpace(unitId) || !validUnitIds.Contains(unitId))
			{
				continue;
			}

			if (normalized.Contains(unitId, StringComparer.OrdinalIgnoreCase))
			{
				continue;
			}

			normalized.Add(unitId);
			if (normalized.Count >= MaxDeckSize)
			{
				break;
			}
		}

		return normalized.ToArray();
	}

	private static List<ChallengeDeploymentRecord> NormalizeChallengeDeployments(
		IEnumerable<ChallengeDeploymentRecord> deployments,
		HashSet<string> validUnitIds)
	{
		if (deployments == null)
		{
			return [];
		}

		var normalized = new List<ChallengeDeploymentRecord>();
		foreach (var deployment in deployments)
		{
			if (deployment == null ||
				string.IsNullOrWhiteSpace(deployment.UnitId) ||
				!validUnitIds.Contains(deployment.UnitId))
			{
				continue;
			}

			normalized.Add(new ChallengeDeploymentRecord
			{
				UnitId = deployment.UnitId,
				TimeSeconds = Math.Max(0f, deployment.TimeSeconds),
				LanePercent = Mathf.Clamp(deployment.LanePercent, 0, 100)
			});
		}

		normalized.Sort((left, right) => left.TimeSeconds.CompareTo(right.TimeSeconds));
		return normalized;
	}

	private void QueueChallengeSubmission(
		string normalizedCode,
		int stage,
		string mutatorId,
		int score,
		bool won,
		bool retreated,
		float elapsedSeconds,
		int enemyDefeats,
		int starsEarned,
		IReadOnlyList<string> deckUnitIds,
		IReadOnlyList<ChallengeDeploymentRecord> deployments,
		int playerDeployments,
		int hullPercent,
		bool usedLockedDeck,
		AsyncChallengeScoreBreakdown scoreBreakdown)
	{
		_pendingChallengeSubmissions.Insert(0, new ChallengeSubmissionEnvelope
		{
			SubmissionId = Guid.NewGuid().ToString("N").ToUpperInvariant(),
			PlayerProfileId = PlayerProfileId,
			PlayerCallsign = PlayerCallsign,
			Code = normalizedCode,
			Stage = Mathf.Clamp(stage, 1, MaxStage),
			MutatorId = AsyncChallengeCatalog.NormalizeMutatorId(mutatorId),
			Score = Math.Max(0, score),
			RawScore = Math.Max(0, scoreBreakdown?.RawScore ?? score),
			ScoreMultiplier = Mathf.Max(0.1f, scoreBreakdown?.Multiplier ?? 1f),
			Won = won,
			Retreated = retreated,
			ElapsedSeconds = Math.Max(0f, elapsedSeconds),
			EnemyDefeats = Math.Max(0, enemyDefeats),
			StarsEarned = Mathf.Clamp(starsEarned, 0, 3),
			UsedLockedDeck = usedLockedDeck,
			DeckUnitIds = (deckUnitIds ?? Array.Empty<string>()).ToArray(),
			PlayerDeployments = Math.Max(0, playerDeployments),
			HullPercent = Mathf.Clamp(hullPercent, 0, 100),
			QueuedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
			UploadAttempts = 0,
			LastUploadAttemptUnixSeconds = 0,
			Deployments = deployments == null
				? []
				: deployments.Select(CloneChallengeDeploymentRecord).ToList()
		});
		NormalizePendingChallengeSubmissions();
		ChallengeSyncService.Instance?.RefreshStatusFromState();
	}

	private string BuildPlayerProfileDisplayId()
	{
		return $"{PlayerCallsign}  |  {PlayerProfileId}";
	}

	private static string FormatUnixTimestamp(long unixSeconds)
	{
		if (unixSeconds <= 0)
		{
			return "never";
		}

		return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime().ToString("MM-dd HH:mm");
	}

	private static string FormatChallengeSubmissionEnvelope(ChallengeSubmissionEnvelope entry)
	{
		var stamp = entry.QueuedAtUnixSeconds > 0
			? FormatUnixTimestamp(entry.QueuedAtUnixSeconds)
			: "--";
		var medal = "No Medal";
		if (AsyncChallengeCatalog.TryParse(entry.Code, out var challenge, out _))
		{
			medal = AsyncChallengeCatalog.ResolveMedalLabel(challenge, entry.Score);
		}

		var status = entry.UploadAttempts > 0
			? $"attempted x{entry.UploadAttempts}"
			: "queued";
		return
			$"{stamp} | {entry.Score} pts | {medal} | {entry.Code} | " +
			$"{(entry.UsedLockedDeck ? "locked deck" : "player deck")} | {status}";
	}

	private string NormalizePinnedChallengeCode(string code)
	{
		if (!AsyncChallengeCatalog.TryParse(code, out var challenge, out _))
		{
			return string.Empty;
		}

		return AsyncChallengeCatalog.Create(
			Mathf.Clamp(challenge.Stage, 1, MaxStage),
			challenge.MutatorId,
			challenge.Seed).Code;
	}

	private IReadOnlyList<UnitDefinition> GetNewlyAvailablePlayerUnits(int previousHighestUnlockedStage)
	{
		return GameData.GetPlayerUnits()
			.Where(unit => unit.UnlockStage > previousHighestUnlockedStage && unit.UnlockStage <= HighestUnlockedStage)
			.ToArray();
	}

	private IReadOnlyList<SpellDefinition> GetNewlyAvailablePlayerSpells(int previousHighestUnlockedStage)
	{
		return GameData.GetPlayerSpells()
			.Where(spell => spell.UnlockStage > previousHighestUnlockedStage && spell.UnlockStage <= HighestUnlockedStage)
			.ToArray();
	}

	private void Persist()
	{
		SaveSystem.Instance?.Save(BuildSaveData());
	}

	private static string NormalizeRouteId(string routeId)
	{
		return RouteCatalog.Normalize(routeId);
	}

	private static string NormalizeEndlessBoonId(string boonId)
	{
		return EndlessBoonCatalog.Normalize(boonId);
	}
}
