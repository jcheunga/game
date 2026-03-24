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
	private const int MaxSpellDeckSize = 3;
	private const int DefaultUnitLevel = 1;
	private const int UnitDoctrineUnlockLevelValue = 3;
	private const int UnitDoctrineRetrainGoldCost = 75;
	private const int MaxPlayerUnitLevel = 5;
	private const int DefaultSpellLevel = 1;
	private const int MaxPlayerSpellLevel = 3;
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
	private const string DefaultDifficultyId = DifficultyCatalog.NormalId;
	private const bool DefaultShowDevUi = true;
	private const bool DefaultShowFpsCounter = true;
	private const bool DefaultAudioMuted = false;
	private const int DefaultEffectsVolumePercent = 85;
	private const int DefaultAmbienceVolumePercent = 65;
	private const int DefaultMusicVolumePercent = 50;
	private const string DefaultLanguage = "en";
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
	public string LastDailyDate { get; private set; } = "";
	public int TotalChallengeSubmissionsSynced { get; private set; }
	public string ChallengeSyncProviderId { get; private set; } = DefaultChallengeSyncProviderId;
	public string ChallengeSyncEndpoint { get; private set; } = DefaultChallengeSyncEndpoint;
	public bool ChallengeSyncAutoFlush { get; private set; } = DefaultChallengeSyncAutoFlush;
	public bool ShowDevUi { get; private set; } = DefaultShowDevUi;
	public bool ShowFpsCounter { get; private set; } = DefaultShowFpsCounter;
	public bool AudioMuted { get; private set; } = DefaultAudioMuted;
	public bool ShowHints { get; private set; } = true;
	public int EffectsVolumePercent { get; private set; } = DefaultEffectsVolumePercent;
	public int AmbienceVolumePercent { get; private set; } = DefaultAmbienceVolumePercent;
	public int MusicVolumePercent { get; private set; } = DefaultMusicVolumePercent;
	public string Language { get; private set; } = DefaultLanguage;
	public bool AnalyticsConsent { get; private set; }
	public bool HasShownConsentPrompt { get; private set; }
	public int FontSizeOffset { get; private set; }
	public bool HighContrast { get; private set; }
	public int PrestigeLevel { get; private set; }
	public int PrestigeTotalGoldEarned { get; private set; }
	public int PrestigeTotalStagesCleared { get; private set; }
	public bool CanPrestige => HighestUnlockedStage >= MaxStage && PrestigeLevel < MaxPrestigeLevel;
	public int BestBossRushWave { get; private set; }
	public float BestBossRushTimeSeconds { get; private set; }
	public int BossRushRuns { get; private set; }
	private const int MaxPrestigeLevel = 5;
	public string DifficultyId { get; private set; } = DefaultDifficultyId;
	public int TotalPurchaseCount => _totalPurchaseCount;
	public string PurchaseValidationEndpoint => _purchaseValidationEndpoint;
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
	public int DailyStreak => _dailyStreak;
	public int PromotedUnitCount => _promotedUnitIds.Count;
	public int ActiveExpeditionCount => _activeExpeditions.Count;

	public int MaxStage => GameData.MaxStage;
	public int DeckSizeLimit => MaxDeckSize;
	public int SpellDeckSizeLimit => MaxSpellDeckSize;
	public int MaxUnitLevel => MaxPlayerUnitLevel;
	public int MaxSpellLevel => MaxPlayerSpellLevel;
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
	private readonly Dictionary<string, int> _spellUpgradeLevels = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, int> _baseUpgradeLevels = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, string> _unitDoctrineSelections = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, int> _challengeBestScores = new(StringComparer.OrdinalIgnoreCase);
	private readonly List<EndlessRunRecord> _endlessRunHistory = new();
	private readonly List<ChallengeRunRecord> _challengeHistory = new();
	private readonly List<ChallengeSubmissionEnvelope> _pendingChallengeSubmissions = new();
	private readonly List<string> _pinnedChallengeCodes = new();
	private readonly HashSet<string> _claimedDistrictRewardIds = new(StringComparer.OrdinalIgnoreCase);
	private readonly HashSet<string> _claimedCampaignDirectiveIds = new(StringComparer.OrdinalIgnoreCase);
	private readonly HashSet<string> _ownedEquipmentIds = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, string> _unitEquipmentSlots = new(StringComparer.OrdinalIgnoreCase);
	private readonly HashSet<string> _seenHintIds = new(StringComparer.OrdinalIgnoreCase);
	private readonly HashSet<string> _unlockedAchievementIds = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, int> _unitPrestigeSelections = new(StringComparer.OrdinalIgnoreCase);
	private readonly HashSet<string> _purchasedProductIds = new(StringComparer.OrdinalIgnoreCase);
	private int _totalPurchaseCount;
	private string _purchaseValidationEndpoint = "";
	private int _armedCampaignDirectiveStage;
	private int _dailyStreak;
	private readonly RandomNumberGenerator _rng = new();
	private string _lastAchievementNotification = "";
	private double _lastAchievementSyncTime;
	private bool _achievementSyncPending;

	// Relic Forge
	public int RelicShards { get; private set; }

	// Unit Promotion
	public int Sigils { get; private set; }
	private readonly HashSet<string> _promotedUnitIds = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, string> _unitEquipmentSlot2 = new(StringComparer.OrdinalIgnoreCase);

	// Expeditions
	private readonly List<ExpeditionSlotState> _activeExpeditions = new();
	public int TotalExpeditionsCompleted { get; private set; }

	// Seasonal Events
	private readonly Dictionary<string, int> _eventStagesCleared = new(StringComparer.OrdinalIgnoreCase);
	private readonly HashSet<string> _claimedEventRewardIds = new(StringComparer.OrdinalIgnoreCase);
	public string SelectedEventId { get; private set; } = "";
	public int SelectedEventStageIndex { get; private set; }

	// Codex
	private readonly HashSet<string> _discoveredCodexIds = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, int> _codexKillCounts = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, long> _codexFirstSeenAt = new(StringComparer.OrdinalIgnoreCase);

	// Skill Trees
	public int Tomes { get; private set; }
	private readonly Dictionary<string, HashSet<string>> _unlockedSkillNodes = new(StringComparer.OrdinalIgnoreCase);

	// PvP Arena
	public int ArenaRating { get; private set; } = 1000;
	public int ArenaWins { get; private set; }
	public int ArenaLosses { get; private set; }
	public ArenaOpponentSnapshot SelectedArenaOpponent { get; private set; }

	// Guild
	public string GuildId { get; private set; } = "";
	public int GuildContributionPoints { get; private set; }
	public GuildSnapshot CachedGuildInfo { get; set; }

	// Hard Mode
	public bool IsHardModeActive { get; private set; }
	public int HardModeHighestCleared { get; private set; }
	private readonly List<int> _hardModeStars = new();
	public bool IsHardModeUnlocked => HighestUnlockedStage >= MaxStage || PrestigeLevel >= 1;

	// Enchantments
	public int Essence { get; private set; }
	private readonly Dictionary<string, string> _relicEnchantments = new(StringComparer.OrdinalIgnoreCase);

	// Raid
	public string LastRaidWeek { get; private set; } = "";
	public int RaidDamageContributed { get; private set; }
	private readonly HashSet<string> _claimedRaidRewardIds = new(StringComparer.OrdinalIgnoreCase);
	private int _raidContributionCount;

	// Bounty Board
	private readonly HashSet<string> _completedBountyIds = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, int> _bountyProgress = new(StringComparer.OrdinalIgnoreCase);
	private string _lastBountyDate = "";

	// Challenge Tower
	public int TowerHighestFloor { get; private set; }
	private readonly List<int> _towerFloorStars = new();
	public bool IsTowerMode => CurrentBattleMode == BattleRunMode.Tower;
	public int SelectedTowerFloor { get; private set; }

	// Friends
	private readonly HashSet<string> _friendIds = new(StringComparer.OrdinalIgnoreCase);
	private string _lastGiftSentDate = "";
	private int _giftsSentToday;
	public int GiftsSentToday => _giftsSentToday;

	// Mastery
	private readonly Dictionary<string, int> _unitMasteryXP = new(StringComparer.OrdinalIgnoreCase);

	// Profile accessors
	public int OwnedUnitCount => _ownedPlayerUnitIds.Count;
	public int OwnedSpellCount => _ownedPlayerSpellIds.Count;
	public int OwnedRelicCount => _ownedEquipmentIds.Count;
	public int AchievementUnlockedCount => _unlockedAchievementIds.Count;
	public int TotalStarsEarned { get { var s = 0; for (var i = 1; i <= MaxStage; i++) s += GetStageStars(i); return s; } }
	public int TotalHardModeStarsEarned { get { var s = 0; for (var i = 1; i <= MaxStage; i++) s += GetHardModeStars(i); return s; } }

	// Achievement Rewards
	private readonly HashSet<string> _claimedAchievementRewardIds = new(StringComparer.OrdinalIgnoreCase);

	// Login Calendar
	public int LoginCalendarDay { get; private set; }
	private string _lastLoginCalendarDate = "";
	private string _loginCalendarMonth = "";

	// War Wagon Cosmetics
	public string SelectedWagonSkinId { get; private set; } = WagonSkinCatalog.DefaultSkinId;

	// Unit Awakening
	private readonly Dictionary<string, int> _unitStarLevels = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, int> _unitTokens = new(StringComparer.OrdinalIgnoreCase);

	// Season Pass
	public int SeasonPassXP { get; private set; }
	public int SeasonPassTier { get; private set; }
	public string SeasonId { get; private set; } = SeasonPassCatalog.CurrentSeasonId;
	public bool HasPremiumPass { get; private set; }
	private readonly HashSet<int> _claimedSeasonFreeTiers = new();
	private readonly HashSet<int> _claimedSeasonPremiumTiers = new();

	// Collection Milestones
	private readonly HashSet<string> _claimedCollectionMilestoneIds = new(StringComparer.OrdinalIgnoreCase);

	// Battle Mutators
	private readonly HashSet<string> _activeMutatorIds = new(StringComparer.OrdinalIgnoreCase);
	public int MutatorBattlesCompleted { get; private set; }

	// Accessibility
	public string ColorblindMode { get; private set; } = "none";
	public bool ReducedMotion { get; private set; }
	public bool AutoBattleEnabled { get; private set; }
	public bool LargeTextMode { get; private set; }

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

	public void ReloadFromDisk()
	{
		LoadOrInitialize();
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
		var diff = GetDifficulty();
		rewardGold = Godot.Mathf.RoundToInt(Math.Max(0, rewardGold) * diff.GoldRewardScale * GetPrestigeGoldBonus());
		rewardFood = Godot.Mathf.RoundToInt(Math.Max(0, rewardFood) * diff.FoodRewardScale * GetPrestigeFoodBonus());
		Gold += rewardGold;
		Food += rewardFood;
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
		CheckAchievements();
		TryAutoCloudBackup();
		AnalyticsService.TrackStageEnd(stage, true, bestStars, 0f, 1f);
		return extraRewardSummary;
	}

	public void ApplyDefeat(int stage)
	{
		LastResultMessage = $"Stage {stage} failed. The war wagon line was overrun.";
		Persist();
		AnalyticsService.TrackStageEnd(stage, false, 0, 0f, 0f);
	}

	public void ApplyRetreat(int stage)
	{
		LastResultMessage = $"Retreated from stage {stage}. No rewards earned.";
		Persist();
		AnalyticsService.TrackStageEnd(stage, false, 0, 0f, 0f);
	}

	public void ApplyEndlessResult(string routeId, int waveReached, float elapsedSeconds, int enemyDefeats, int rewardGold, int rewardFood, bool retreated)
	{
		var diff = GetDifficulty();
		rewardGold = Godot.Mathf.RoundToInt(Math.Max(0, rewardGold) * diff.GoldRewardScale);
		rewardFood = Godot.Mathf.RoundToInt(Math.Max(0, rewardFood) * diff.FoodRewardScale);
		SelectedEndlessRouteId = NormalizeRouteId(routeId);
		Gold += rewardGold;
		Food += rewardFood;
		BestEndlessWave = Math.Max(BestEndlessWave, Math.Max(0, waveReached));
		BestEndlessTimeSeconds = Math.Max(BestEndlessTimeSeconds, Math.Max(0f, elapsedSeconds));
		EndlessRuns++;

		_endlessRunHistory.Insert(0, new EndlessRunRecord
		{
			Wave = Math.Max(0, waveReached),
			TimeSeconds = Math.Max(0f, elapsedSeconds),
			RouteId = SelectedEndlessRouteId,
			BoonId = SelectedEndlessBoonId,
			GoldEarned = rewardGold,
			FoodEarned = rewardFood,
			Date = DateTime.UtcNow.ToString("yyyy-MM-dd"),
			DifficultyId = DifficultyId ?? DefaultDifficultyId
		});
		NormalizeEndlessRunHistory();

		var routeLabel = GameData.GetLatestStageForMap(SelectedEndlessRouteId).MapName;
		var outcome = retreated ? "withdrew" : "was overrun";
		LastResultMessage =
			$"Endless run {outcome} on {routeLabel}. Wave {Math.Max(0, waveReached)}, {elapsedSeconds:0.0}s, {enemyDefeats} defeats. " +
			$"+{Math.Max(0, rewardGold)} gold, +{Math.Max(0, rewardFood)} food.";
		Persist();
		CheckAchievements();
	}

	public IReadOnlyList<EndlessRunRecord> GetEndlessRunHistory()
	{
		return _endlessRunHistory.ToArray();
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

		var daily = GetDailyChallenge();
		var dailyChallenge = AsyncChallengeCatalog.Create(
			Mathf.Clamp(daily.StageIndex, 1, MaxStage),
			AsyncChallengeCatalog.PressureSpikeId,
			daily.Seed);
		if (normalizedCode.Equals(dailyChallenge.Code, StringComparison.OrdinalIgnoreCase))
		{
			LastDailyDate = daily.Date;
		}

		Persist();
		if (ChallengeSyncAutoFlush)
		{
			ChallengeSyncService.Instance?.TryAutoFlushPending();
		}
	}

	public void SetDifficulty(string id)
	{
		DifficultyId = DifficultyCatalog.GetById(id).Id;
		Persist();
	}

	public DifficultyDefinition GetDifficulty()
	{
		return DifficultyCatalog.GetById(DifficultyId);
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

	public void SetMusicVolumePercent(int percent)
	{
		MusicVolumePercent = Mathf.Clamp(percent, 0, 100);
		Persist();
		MusicPlayer.Instance?.SetVolumeScale(MusicVolumePercent / 100f);
	}

	public void SetLanguage(string language)
	{
		Language = string.IsNullOrWhiteSpace(language) ? DefaultLanguage : language.Trim().ToLowerInvariant();
		Locale.SetLanguage(Language);
		Persist();
	}

	public void SetAnalyticsConsent(bool consent)
	{
		AnalyticsConsent = consent;
		HasShownConsentPrompt = true;
		Persist();
	}

	public void SetFontSizeOffset(int offset)
	{
		FontSizeOffset = Mathf.Clamp(offset, -4, 8);
		Persist();
		ApplyFontSizeOffset();
	}

	public void SetHighContrast(bool enabled)
	{
		HighContrast = enabled;
		Persist();
	}

	public bool PrepareBossRush(out string message)
	{
		if (HighestUnlockedStage < 10)
		{
			message = "Unlock at least 10 stages to access Boss Rush.";
			return false;
		}

		CurrentBattleMode = BattleRunMode.BossRush;
		message = "Boss Rush armed. Deploy to face all 10 district bosses in sequence.";
		Persist();
		return true;
	}

	public void ApplyBossRushResult(int wavesCleared, float elapsedSeconds, int totalGold, bool completed)
	{
		BestBossRushWave = Math.Max(BestBossRushWave, wavesCleared);
		BestBossRushTimeSeconds = BestBossRushTimeSeconds > 0.01f
			? (completed ? Math.Min(BestBossRushTimeSeconds, elapsedSeconds) : BestBossRushTimeSeconds)
			: elapsedSeconds;
		BossRushRuns++;
		Gold += totalGold;

		var resultLabel = completed ? "Boss Rush COMPLETE" : $"Boss Rush failed at wave {wavesCleared}/{BossRushCatalog.TotalWaves}";
		LastResultMessage = $"{resultLabel}. +{totalGold} gold. Best: {BestBossRushWave}/{BossRushCatalog.TotalWaves} waves.";

		Persist();
		CheckAchievements();
		AnalyticsService.Track("boss_rush", $"waves={wavesCleared},time={elapsedSeconds:F1},gold={totalGold},complete={completed}");

		if (completed)
		{
			TryUnlockAchievement("boss_rush_complete");
		}
	}

	public float GetPrestigeGoldBonus() => 1f + (PrestigeLevel * 0.10f);
	public float GetPrestigeFoodBonus() => 1f + (PrestigeLevel * 0.08f);
	public float GetPrestigeUnitHealthBonus() => 1f + (PrestigeLevel * 0.05f);
	public int GetPrestigeStartingGold() => DefaultGold + (PrestigeLevel * 200);
	public int GetPrestigeStartingFood() => DefaultFood + (PrestigeLevel * 8);

	public string GetPrestigeLabel()
	{
		return PrestigeLevel switch
		{
			0 => "",
			1 => "Veteran",
			2 => "Elite",
			3 => "Champion",
			4 => "Legendary",
			5 => "Mythic",
			_ => $"Prestige {PrestigeLevel}"
		};
	}

	public bool TryPrestige(out string message)
	{
		if (!CanPrestige)
		{
			message = PrestigeLevel >= MaxPrestigeLevel
				? "Maximum prestige level reached."
				: $"Clear all {MaxStage} stages to unlock prestige.";
			return false;
		}

		// Record lifetime stats before reset
		PrestigeTotalGoldEarned += Gold;
		PrestigeTotalStagesCleared += HighestUnlockedStage;
		PrestigeLevel++;

		// Reset progression but keep permanent unlocks
		var prestigeGold = GetPrestigeStartingGold();
		var prestigeFood = GetPrestigeStartingFood();
		var keepUnits = new List<string>(_ownedPlayerUnitIds);
		var keepSpells = new List<string>(_ownedPlayerSpellIds);
		var keepEquipment = new List<string>(_ownedEquipmentIds);
		var keepAchievements = new List<string>(_unlockedAchievementIds);
		var keepPrestige = new Dictionary<string, int>(_unitPrestigeSelections);
		var keepDoctrines = new Dictionary<string, string>(_unitDoctrineSelections);
		var keepPromotions = new HashSet<string>(_promotedUnitIds);
		var keepEquipSlot2 = new Dictionary<string, string>(_unitEquipmentSlot2);
		var keepShards = RelicShards;
		var keepSigils = Sigils;
		var keepTomes = Tomes;
		var keepSkillNodes = _unlockedSkillNodes.ToDictionary(p => p.Key, p => new HashSet<string>(p.Value, StringComparer.OrdinalIgnoreCase));
		var keepArenaRating = ArenaRating;
		var keepArenaWins = ArenaWins;
		var keepArenaLosses = ArenaLosses;
		var keepGuildId = GuildId;
		var keepGuildContribution = GuildContributionPoints;
		var keepCodexIds = new HashSet<string>(_discoveredCodexIds, StringComparer.OrdinalIgnoreCase);
		var keepCodexKills = new Dictionary<string, int>(_codexKillCounts, StringComparer.OrdinalIgnoreCase);
		var keepCodexFirstSeen = new Dictionary<string, long>(_codexFirstSeenAt, StringComparer.OrdinalIgnoreCase);
		var keepHardModeStars = new List<int>(_hardModeStars);
		var keepHardModeHighest = HardModeHighestCleared;
		var keepEssence = Essence;
		var keepEnchantments = new Dictionary<string, string>(_relicEnchantments, StringComparer.OrdinalIgnoreCase);
		var keepTowerHighest = TowerHighestFloor;
		var keepTowerStars = new List<int>(_towerFloorStars);
		var keepFriends = new HashSet<string>(_friendIds, StringComparer.OrdinalIgnoreCase);
		var keepMasteryXP = new Dictionary<string, int>(_unitMasteryXP, StringComparer.OrdinalIgnoreCase);
		var keepClaimedAchRewards = new HashSet<string>(_claimedAchievementRewardIds, StringComparer.OrdinalIgnoreCase);
		var keepLoginDay = LoginCalendarDay;
		var keepLoginDate = _lastLoginCalendarDate;
		var keepLoginMonth = _loginCalendarMonth;
		var keepWagonSkin = SelectedWagonSkinId;
		var keepStarLevels = new Dictionary<string, int>(_unitStarLevels, StringComparer.OrdinalIgnoreCase);
		var keepTokens = new Dictionary<string, int>(_unitTokens, StringComparer.OrdinalIgnoreCase);
		var keepSeasonXP = SeasonPassXP;
		var keepSeasonTier = SeasonPassTier;
		var keepPremium = HasPremiumPass;
		var keepSeasonFree = new HashSet<int>(_claimedSeasonFreeTiers);
		var keepSeasonPrem = new HashSet<int>(_claimedSeasonPremiumTiers);
		var keepCollMilestones = new HashSet<string>(_claimedCollectionMilestoneIds, StringComparer.OrdinalIgnoreCase);
		var keepColorblind = ColorblindMode;
		var keepReducedMotion = ReducedMotion;
		var keepAutoBattle = AutoBattleEnabled;
		var keepLargeText = LargeTextMode;
		var keepMutatorBattles = MutatorBattlesCompleted;

		// Reset campaign state
		HighestUnlockedStage = DefaultUnlockedStage;
		SelectedStage = DefaultUnlockedStage;
		Gold = prestigeGold;
		Food = prestigeFood;
		_stageStars.Clear();
		_unitUpgradeLevels.Clear();
		_spellUpgradeLevels.Clear();
		_baseUpgradeLevels.Clear();
		_claimedDistrictRewardIds.Clear();
		_claimedCampaignDirectiveIds.Clear();
		_armedCampaignDirectiveStage = 0;

		// Keep permanent unlocks
		_ownedPlayerUnitIds.Clear();
		foreach (var id in keepUnits) _ownedPlayerUnitIds.Add(id);
		_ownedPlayerSpellIds.Clear();
		foreach (var id in keepSpells) _ownedPlayerSpellIds.Add(id);
		_ownedEquipmentIds.Clear();
		foreach (var id in keepEquipment) _ownedEquipmentIds.Add(id);
		_unlockedAchievementIds.Clear();
		foreach (var id in keepAchievements) _unlockedAchievementIds.Add(id);
		_unitPrestigeSelections.Clear();
		foreach (var (k, v) in keepPrestige) _unitPrestigeSelections[k] = v;
		_unitDoctrineSelections.Clear();
		foreach (var (k, v) in keepDoctrines) _unitDoctrineSelections[k] = v;
		_promotedUnitIds.Clear();
		foreach (var id in keepPromotions) _promotedUnitIds.Add(id);
		_unitEquipmentSlot2.Clear();
		foreach (var (k, v) in keepEquipSlot2) _unitEquipmentSlot2[k] = v;
		RelicShards = keepShards;
		Sigils = keepSigils;
		Tomes = keepTomes;
		_unlockedSkillNodes.Clear();
		foreach (var (k, v) in keepSkillNodes) _unlockedSkillNodes[k] = v;
		ArenaRating = keepArenaRating;
		ArenaWins = keepArenaWins;
		ArenaLosses = keepArenaLosses;
		GuildId = keepGuildId;
		GuildContributionPoints = keepGuildContribution;
		_discoveredCodexIds.Clear();
		foreach (var id in keepCodexIds) _discoveredCodexIds.Add(id);
		_codexKillCounts.Clear();
		foreach (var (k, v) in keepCodexKills) _codexKillCounts[k] = v;
		_codexFirstSeenAt.Clear();
		foreach (var (k, v) in keepCodexFirstSeen) _codexFirstSeenAt[k] = v;
		_hardModeStars.Clear();
		_hardModeStars.AddRange(keepHardModeStars);
		HardModeHighestCleared = keepHardModeHighest;
		Essence = keepEssence;
		_relicEnchantments.Clear();
		foreach (var (k, v) in keepEnchantments) _relicEnchantments[k] = v;
		TowerHighestFloor = keepTowerHighest;
		_towerFloorStars.Clear();
		_towerFloorStars.AddRange(keepTowerStars);
		_friendIds.Clear();
		foreach (var id in keepFriends) _friendIds.Add(id);
		_unitMasteryXP.Clear();
		foreach (var (k, v) in keepMasteryXP) _unitMasteryXP[k] = v;
		_claimedAchievementRewardIds.Clear();
		foreach (var id in keepClaimedAchRewards) _claimedAchievementRewardIds.Add(id);
		LoginCalendarDay = keepLoginDay;
		_lastLoginCalendarDate = keepLoginDate;
		_loginCalendarMonth = keepLoginMonth;
		SelectedWagonSkinId = keepWagonSkin;
		_unitStarLevels.Clear();
		foreach (var (k, v) in keepStarLevels) _unitStarLevels[k] = v;
		_unitTokens.Clear();
		foreach (var (k, v) in keepTokens) _unitTokens[k] = v;
		SeasonPassXP = keepSeasonXP;
		SeasonPassTier = keepSeasonTier;
		HasPremiumPass = keepPremium;
		_claimedSeasonFreeTiers.Clear();
		foreach (var t in keepSeasonFree) _claimedSeasonFreeTiers.Add(t);
		_claimedSeasonPremiumTiers.Clear();
		foreach (var t in keepSeasonPrem) _claimedSeasonPremiumTiers.Add(t);
		_claimedCollectionMilestoneIds.Clear();
		foreach (var id in keepCollMilestones) _claimedCollectionMilestoneIds.Add(id);
		ColorblindMode = keepColorblind;
		ReducedMotion = keepReducedMotion;
		AutoBattleEnabled = keepAutoBattle;
		LargeTextMode = keepLargeText;
		MutatorBattlesCompleted = keepMutatorBattles;

		// Keep active deck if units are still owned
		var validDeck = new List<string>();
		foreach (var id in _activeDeckUnitIds)
		{
			if (_ownedPlayerUnitIds.Contains(id)) validDeck.Add(id);
		}
		_activeDeckUnitIds.Clear();
		_activeDeckUnitIds.AddRange(validDeck.Count >= 3 ? validDeck.GetRange(0, 3) : validDeck);
		if (_activeDeckUnitIds.Count == 0)
		{
			_activeDeckUnitIds.AddRange(DefaultDeckUnitIds);
		}

		LastResultMessage = $"Prestige {PrestigeLevel} achieved! The caravan rides again with {GetPrestigeLabel()} rank.\n" +
			$"+{PrestigeLevel * 10}% gold, +{PrestigeLevel * 8}% food, +{PrestigeLevel * 5}% unit health.\n" +
			$"Starting gold: {prestigeGold}, starting food: {prestigeFood}.";

		Persist();
		CheckAchievements();
		AnalyticsService.Track("prestige", $"level={PrestigeLevel}");

		message = LastResultMessage;
		return true;
	}

	private void ApplyFontSizeOffset()
	{
		var defaultSize = 16 + FontSizeOffset;
		if (defaultSize < 12) defaultSize = 12;
		if (defaultSize > 28) defaultSize = 28;
		ThemeDB.FallbackFontSize = defaultSize;
	}

	public void SetShowHints(bool enabled)
	{
		ShowHints = enabled;
		Persist();
	}

	public bool HasSeenHint(string hintId)
	{
		return _seenHintIds.Contains(hintId);
	}

	public void MarkHintSeen(string hintId)
	{
		if (string.IsNullOrWhiteSpace(hintId))
		{
			return;
		}

		if (_seenHintIds.Add(hintId.Trim()))
		{
			Persist();
		}
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

	public IReadOnlyCollection<string> GetOwnedPlayerUnitIds()
	{
		return _ownedPlayerUnitIds;
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
			DiscoverCodexEntry(definition.Id);
			LastResultMessage = $"{definition.DisplayName} purchased for {cost} gold.";
			Persist();
			CheckAchievements();
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
			DiscoverCodexEntry(definition.Id);
			LastResultMessage = cost > 0
				? $"{definition.DisplayName} scribed for {cost} gold."
				: $"{definition.DisplayName} prepared for the caravan.";
			Persist();
			CheckAchievements();
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
			CheckAchievements();
			message = LastResultMessage;
			return true;
		}
		catch (Exception)
		{
			message = "Unit data was not found.";
			return false;
		}
	}

	public int GetSpellLevel(string spellId)
	{
		return _spellUpgradeLevels.TryGetValue(spellId, out var level)
			? level
			: DefaultSpellLevel;
	}

	public int GetSpellUpgradeCost(string spellId)
	{
		var level = GetSpellLevel(spellId);
		var definition = GameData.GetSpell(spellId);
		return definition.GoldCost + 60 + ((level - 1) * 45);
	}

	public bool TryUpgradeSpell(string spellId, out string message)
	{
		try
		{
			var definition = GameData.GetSpell(spellId);
			if (!IsSpellOwned(definition.Id))
			{
				message = $"Buy {definition.DisplayName} in the shop before upgrading it.";
				return false;
			}

			var currentLevel = GetSpellLevel(definition.Id);
			if (currentLevel >= MaxPlayerSpellLevel)
			{
				message = $"{definition.DisplayName} is already max level.";
				return false;
			}

			var cost = GetSpellUpgradeCost(definition.Id);
			if (Gold < cost)
			{
				message = $"Need {cost} gold to upgrade {definition.DisplayName}.";
				return false;
			}

			Gold -= cost;
			_spellUpgradeLevels[definition.Id] = currentLevel + 1;
			LastResultMessage = $"{definition.DisplayName} upgraded to level {currentLevel + 1}. -{cost} gold.";
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

	public ResolvedSpellStats BuildSpellStats(SpellDefinition definition)
	{
		return new ResolvedSpellStats(definition, GetSpellLevel(definition.Id));
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
			BaseUpgradeCatalog.RelicVaultId => 110 + (level * 85),
			BaseUpgradeCatalog.ProjectileWardId => 100 + (level * 75),
			BaseUpgradeCatalog.GateBreakerId => 95 + (level * 80),
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

		var speedScale = 1f;
		var equip = GetUnitEquipment(definition.Id);
		if (equip != null)
		{
			healthScale *= equip.HealthScale;
			damageScale *= equip.DamageScale;
			cooldownReduction += equip.CooldownReduction;
			baseDamageBonus += equip.BaseDamageBonus;
			speedScale *= equip.SpeedScale;
		}

		// Promotion bonuses + second equipment slot
		if (_promotedUnitIds.Contains(definition.Id))
		{
			var promo = UnitPromotionCatalog.TryGet(definition.Id);
			if (promo != null)
			{
				healthScale *= promo.HealthScale;
				damageScale *= promo.DamageScale;
				speedScale *= promo.SpeedScale;
			}

			var equip2 = GetUnitEquipment2(definition.Id);
			if (equip2 != null)
			{
				healthScale *= equip2.HealthScale;
				damageScale *= equip2.DamageScale;
				cooldownReduction += equip2.CooldownReduction;
				baseDamageBonus += equip2.BaseDamageBonus;
				speedScale *= equip2.SpeedScale;
			}
		}

		// Skill tree bonuses
		var skillBonus = ResolveSkillTreeBonus(definition.Id);
		healthScale *= skillBonus.HealthScale;
		damageScale *= skillBonus.DamageScale;
		speedScale *= skillBonus.SpeedScale;
		cooldownReduction += skillBonus.CooldownReduction;

		// Guild perk bonuses
		var guildBonus = ResolveGuildBonus();
		healthScale *= guildBonus.HealthScale;

		// Enchantment bonuses
		var equip1Id = GetEquippedRelicId(definition.Id);
		var equip2Id = GetEquippedRelicId2(definition.Id);
		var ench1 = GetRelicEnchantment(equip1Id);
		var ench2 = GetRelicEnchantment(equip2Id);
		if (ench1 != null) { healthScale *= ench1.HealthScale; damageScale *= ench1.DamageScale; speedScale *= ench1.SpeedScale; }
		if (ench2 != null) { healthScale *= ench2.HealthScale; damageScale *= ench2.DamageScale; speedScale *= ench2.SpeedScale; }

		// Mastery bonuses
		var masteryBonus = ResolveMasteryBonus(definition.Id);
		healthScale *= masteryBonus.HealthScale;
		damageScale *= masteryBonus.DamageScale;

		// Awakening star bonuses (final layer)
		var awakeningBonus = ResolveAwakeningBonus(definition.Id);
		healthScale *= awakeningBonus.HealthScale;
		damageScale *= awakeningBonus.DamageScale;

		var stats = new UnitStats(
			definition,
			healthScale,
			damageScale,
			cooldownReduction,
			baseDamageBonus,
			speedScale);

		var prestigeIndex = GetUnitPrestigeIndex(definition.Id);
		var prestigeColor = PrestigeColorCatalog.ResolvePrestigeColor(definition.Id, prestigeIndex);
		if (prestigeColor.HasValue)
		{
			stats.Color = prestigeColor.Value;
		}

		return stats;
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

	public EquipmentDefinition GetUnitEquipment(string unitId)
	{
		if (string.IsNullOrWhiteSpace(unitId))
		{
			return null;
		}

		if (_unitEquipmentSlots.TryGetValue(unitId, out var equipId) &&
			!string.IsNullOrWhiteSpace(equipId))
		{
			try
			{
				return GameData.GetEquipment(equipId);
			}
			catch
			{
				return null;
			}
		}

		return null;
	}

	public bool TryEquipItem(string unitId, string equipmentId)
	{
		if (string.IsNullOrWhiteSpace(unitId) || string.IsNullOrWhiteSpace(equipmentId))
		{
			return false;
		}

		if (!_ownedEquipmentIds.Contains(equipmentId))
		{
			return false;
		}

		foreach (var pair in _unitEquipmentSlots)
		{
			if (pair.Value.Equals(equipmentId, StringComparison.OrdinalIgnoreCase) &&
				!pair.Key.Equals(unitId, StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}
		}

		_unitEquipmentSlots[unitId] = equipmentId;
		Persist();
		return true;
	}

	public void UnequipItem(string unitId)
	{
		if (string.IsNullOrWhiteSpace(unitId))
		{
			return;
		}

		if (_unitEquipmentSlots.Remove(unitId))
		{
			Persist();
		}
	}

	public HashSet<string> GetOwnedEquipment()
	{
		return new HashSet<string>(_ownedEquipmentIds, StringComparer.OrdinalIgnoreCase);
	}

	public bool TryGrantEquipment(string equipmentId)
	{
		if (string.IsNullOrWhiteSpace(equipmentId))
		{
			return false;
		}

		if (_ownedEquipmentIds.Add(equipmentId))
		{
			DiscoverCodexEntry(equipmentId);
			Persist();
			CheckAchievements();
			return true;
		}

		return false;
	}

	// ── Relic Forge ──────────────────────────────────────────

	public bool TryDismantleRelic(string relicId, out int shardsGained)
	{
		shardsGained = 0;
		if (string.IsNullOrWhiteSpace(relicId) || !_ownedEquipmentIds.Contains(relicId))
		{
			return false;
		}

		var equip = GameData.GetEquipment(relicId);
		if (equip == null)
		{
			return false;
		}

		// Auto-unequip from any unit (both slots)
		foreach (var pair in new Dictionary<string, string>(_unitEquipmentSlots))
		{
			if (pair.Value.Equals(relicId, System.StringComparison.OrdinalIgnoreCase))
			{
				_unitEquipmentSlots.Remove(pair.Key);
			}
		}
		foreach (var pair in new Dictionary<string, string>(_unitEquipmentSlot2))
		{
			if (pair.Value.Equals(relicId, System.StringComparison.OrdinalIgnoreCase))
			{
				_unitEquipmentSlot2.Remove(pair.Key);
			}
		}

		_ownedEquipmentIds.Remove(relicId);
		_relicEnchantments.Remove(relicId);
		shardsGained = RelicForgeCatalog.GetDismantleShards(equip.Rarity);
		RelicShards += shardsGained;
		Persist();
		return true;
	}

	public bool TryFuseRelics(string[] relicIds, out string resultRelicId)
	{
		resultRelicId = null;
		if (relicIds == null || relicIds.Length != RelicForgeCatalog.RelicsRequiredForFusion)
		{
			return false;
		}

		// Validate all owned and same rarity
		string sourceRarity = null;
		foreach (var id in relicIds)
		{
			if (!_ownedEquipmentIds.Contains(id))
			{
				return false;
			}

			var equip = GameData.GetEquipment(id);
			if (equip == null)
			{
				return false;
			}

			if (sourceRarity == null)
			{
				sourceRarity = equip.Rarity;
			}
			else if (!string.Equals(sourceRarity, equip.Rarity, System.StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}
		}

		var targetRarity = RelicForgeCatalog.GetFusionTargetRarity(sourceRarity);
		if (targetRarity == null)
		{
			return false;
		}

		// Remove source relics (auto-unequips)
		foreach (var id in relicIds)
		{
			TryDismantleRelic(id, out _);
			// Dismantling grants shards — subtract them back since fusion is not dismantling
		}
		// Compensate: fusion should not grant shards
		RelicShards -= relicIds.Length * RelicForgeCatalog.GetDismantleShards(sourceRarity);
		if (RelicShards < 0) RelicShards = 0;

		// Pick a random relic of the target rarity
		var candidates = RelicForgeCatalog.GetRelicsByRarity(targetRarity);
		if (candidates.Count == 0)
		{
			return false;
		}

		var pick = candidates[_rng.RandiRange(0, candidates.Count - 1)];
		resultRelicId = pick.Id;
		TryGrantEquipment(pick.Id);
		TryUnlockAchievement("first_forge");
		Persist();
		CheckAchievements();
		return true;
	}

	public bool TryForgeRelic(string relicId, out string message)
	{
		message = "";
		var recipe = RelicForgeCatalog.GetCraftRecipe(relicId);
		if (recipe == null)
		{
			message = "Unknown relic.";
			return false;
		}

		if (_ownedEquipmentIds.Contains(relicId))
		{
			message = "Already owned.";
			return false;
		}

		if (RelicShards < recipe.ShardCost)
		{
			message = $"Need {recipe.ShardCost} shards (have {RelicShards}).";
			return false;
		}

		if (Gold < recipe.GoldCost)
		{
			message = $"Need {recipe.GoldCost} gold (have {Gold}).";
			return false;
		}

		RelicShards -= recipe.ShardCost;
		Gold -= recipe.GoldCost;
		TryGrantEquipment(relicId);
		TryUnlockAchievement("first_forge");
		Persist();
		CheckAchievements();
		message = $"Forged {GameData.GetEquipment(relicId)?.DisplayName ?? relicId}!";
		return true;
	}

	// ── Unit Promotion ───────────────────────────────────────

	public void GrantSigils(int amount)
	{
		if (amount > 0)
		{
			Sigils += amount;
			Persist();
		}
	}

	public bool IsUnitPromoted(string unitId)
	{
		return _promotedUnitIds.Contains(unitId);
	}

	public bool CanPromoteUnit(string unitId)
	{
		if (string.IsNullOrWhiteSpace(unitId) || _promotedUnitIds.Contains(unitId))
		{
			return false;
		}

		var promo = UnitPromotionCatalog.TryGet(unitId);
		if (promo == null)
		{
			return false;
		}

		var level = GetUnitLevel(unitId);
		return level >= UnitPromotionCatalog.RequiredLevel &&
			   _ownedPlayerUnitIds.Contains(unitId) &&
			   Gold >= promo.GoldCost &&
			   Sigils >= promo.SigilCost;
	}

	public bool TryPromoteUnit(string unitId, out string message)
	{
		message = "";
		var promo = UnitPromotionCatalog.TryGet(unitId);
		if (promo == null)
		{
			message = "No promotion path.";
			return false;
		}

		if (_promotedUnitIds.Contains(unitId))
		{
			message = "Already promoted.";
			return false;
		}

		if (!_ownedPlayerUnitIds.Contains(unitId))
		{
			message = "Unit not owned.";
			return false;
		}

		if (GetUnitLevel(unitId) < UnitPromotionCatalog.RequiredLevel)
		{
			message = $"Requires level {UnitPromotionCatalog.RequiredLevel}.";
			return false;
		}

		if (Gold < promo.GoldCost)
		{
			message = $"Need {promo.GoldCost} gold (have {Gold}).";
			return false;
		}

		if (Sigils < promo.SigilCost)
		{
			message = $"Need {promo.SigilCost} sigils (have {Sigils}).";
			return false;
		}

		Gold -= promo.GoldCost;
		Sigils -= promo.SigilCost;
		_promotedUnitIds.Add(unitId);
		Persist();
		CheckAchievements();
		message = $"{promo.PromotedTitle} promoted!";
		return true;
	}

	public EquipmentDefinition GetUnitEquipment2(string unitId)
	{
		if (string.IsNullOrWhiteSpace(unitId) || !_promotedUnitIds.Contains(unitId))
		{
			return null;
		}

		if (_unitEquipmentSlot2.TryGetValue(unitId, out var equipId) &&
			!string.IsNullOrWhiteSpace(equipId))
		{
			try
			{
				return GameData.GetEquipment(equipId);
			}
			catch
			{
				return null;
			}
		}

		return null;
	}

	public bool TryEquipItem2(string unitId, string equipmentId)
	{
		if (string.IsNullOrWhiteSpace(unitId) || string.IsNullOrWhiteSpace(equipmentId))
		{
			return false;
		}

		if (!_promotedUnitIds.Contains(unitId))
		{
			return false;
		}

		if (!_ownedEquipmentIds.Contains(equipmentId))
		{
			return false;
		}

		// Check not equipped by any unit in either slot
		foreach (var pair in _unitEquipmentSlots)
		{
			if (pair.Value.Equals(equipmentId, System.StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}
		}
		foreach (var pair in _unitEquipmentSlot2)
		{
			if (pair.Value.Equals(equipmentId, System.StringComparison.OrdinalIgnoreCase) &&
				!pair.Key.Equals(unitId, System.StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}
		}

		_unitEquipmentSlot2[unitId] = equipmentId;
		Persist();
		return true;
	}

	public void UnequipItem2(string unitId)
	{
		if (!string.IsNullOrWhiteSpace(unitId) && _unitEquipmentSlot2.Remove(unitId))
		{
			Persist();
		}
	}

	public string GetPromotedTitle(string unitId)
	{
		if (!_promotedUnitIds.Contains(unitId))
		{
			return null;
		}

		return UnitPromotionCatalog.TryGet(unitId)?.PromotedTitle;
	}

	// ── Expeditions ──────────────────────────────────────────

	public bool IsUnitOnExpedition(string unitId)
	{
		foreach (var slot in _activeExpeditions)
		{
			if (slot.AssignedUnitIds != null)
			{
				foreach (var id in slot.AssignedUnitIds)
				{
					if (string.Equals(id, unitId, System.StringComparison.OrdinalIgnoreCase))
					{
						return true;
					}
				}
			}
		}

		return false;
	}

	public IReadOnlyList<ExpeditionSlotState> GetActiveExpeditions() => _activeExpeditions;

	public bool TryStartExpedition(string expeditionId, string[] unitIds, out string message)
	{
		message = "";
		var def = ExpeditionCatalog.Get(expeditionId);
		if (def == null)
		{
			message = "Unknown expedition.";
			return false;
		}

		if (_activeExpeditions.Count >= ExpeditionCatalog.MaxSlots)
		{
			message = "All expedition slots are in use.";
			return false;
		}

		if (unitIds == null || unitIds.Length < def.MinUnits || unitIds.Length > def.MaxUnits)
		{
			message = $"Requires {def.MinUnits}-{def.MaxUnits} units.";
			return false;
		}

		foreach (var unitId in unitIds)
		{
			if (!_ownedPlayerUnitIds.Contains(unitId))
			{
				message = $"Unit {unitId} not owned.";
				return false;
			}

			if (IsUnitInActiveDeck(unitId))
			{
				message = "Cannot send deck units on expedition.";
				return false;
			}

			if (IsUnitOnExpedition(unitId))
			{
				message = "Unit already on expedition.";
				return false;
			}
		}

		var now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		_activeExpeditions.Add(new ExpeditionSlotState
		{
			ExpeditionId = expeditionId,
			AssignedUnitIds = (string[])unitIds.Clone(),
			StartedAtUnixSeconds = now
		});
		Persist();
		message = $"{def.Title} dispatched.";
		return true;
	}

	public bool IsExpeditionComplete(int slotIndex)
	{
		if (slotIndex < 0 || slotIndex >= _activeExpeditions.Count)
		{
			return false;
		}

		var slot = _activeExpeditions[slotIndex];
		var def = ExpeditionCatalog.Get(slot.ExpeditionId);
		if (def == null)
		{
			return false;
		}

		var now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		var elapsed = now - slot.StartedAtUnixSeconds;
		return elapsed >= def.DurationMinutes * 60;
	}

	public System.TimeSpan GetExpeditionTimeRemaining(int slotIndex)
	{
		if (slotIndex < 0 || slotIndex >= _activeExpeditions.Count)
		{
			return System.TimeSpan.Zero;
		}

		var slot = _activeExpeditions[slotIndex];
		var def = ExpeditionCatalog.Get(slot.ExpeditionId);
		if (def == null)
		{
			return System.TimeSpan.Zero;
		}

		var now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		var endTime = slot.StartedAtUnixSeconds + (def.DurationMinutes * 60);
		var remaining = endTime - now;
		return remaining > 0 ? System.TimeSpan.FromSeconds(remaining) : System.TimeSpan.Zero;
	}

	public bool TryCollectExpedition(int slotIndex, out string resultMessage)
	{
		resultMessage = "";
		if (!IsExpeditionComplete(slotIndex))
		{
			resultMessage = "Expedition still in progress.";
			return false;
		}

		var slot = _activeExpeditions[slotIndex];
		var def = ExpeditionCatalog.Get(slot.ExpeditionId);
		if (def == null)
		{
			resultMessage = "Unknown expedition.";
			return false;
		}

		// Seed RNG from start time for deterministic rewards
		var rewardRng = new RandomNumberGenerator();
		rewardRng.Seed = (ulong)slot.StartedAtUnixSeconds;

		var unitBonus = slot.AssignedUnitIds?.Length ?? 1;
		var goldReward = def.BaseGoldReward + (rewardRng.RandiRange(0, def.BaseGoldReward / 4));
		goldReward = (int)(goldReward * (1f + (unitBonus - 1) * 0.15f));
		var foodReward = def.BaseFoodReward + rewardRng.RandiRange(0, 2);

		Gold += goldReward;
		Food += foodReward;

		var relicMessage = "";
		var roll = rewardRng.Randf();
		if (roll < def.RelicDropChance)
		{
			var candidates = RelicForgeCatalog.GetRelicsByRarity(def.RelicDropRarity);
			if (candidates.Count > 0)
			{
				var pick = candidates[rewardRng.RandiRange(0, candidates.Count - 1)];
				if (TryGrantEquipment(pick.Id))
				{
					relicMessage = $" +{pick.DisplayName} ({pick.Rarity})!";
				}
			}
		}

		// Tome reward from expeditions
		var tomeReward = def.DurationMinutes >= 120 ? 2 : 1;
		Tomes += tomeReward;
		AddSeasonXP(SeasonPassCatalog.XPPerExpedition);

		_activeExpeditions.RemoveAt(slotIndex);
		TotalExpeditionsCompleted++;
		Persist();
		CheckAchievements();
		resultMessage = $"+{goldReward} gold, +{foodReward} food, +{tomeReward} tome(s){relicMessage}";
		return true;
	}

	// ── Seasonal Events ──────────────────────────────────────

	public SeasonalEventDefinition GetActiveEvent()
	{
		return SeasonalEventCatalog.GetActiveEvent(System.DateTime.UtcNow);
	}

	public int GetEventProgress(string eventId)
	{
		return _eventStagesCleared.TryGetValue(eventId, out var count) ? count : 0;
	}

	public void PrepareEventBattle(string eventId, int stageIndex)
	{
		SelectedEventId = eventId;
		SelectedEventStageIndex = stageIndex;
		CurrentBattleMode = BattleRunMode.SeasonalEvent;
		Persist();
	}

	public void RecordEventStageCleared(string eventId)
	{
		if (string.IsNullOrWhiteSpace(eventId))
		{
			return;
		}

		if (_eventStagesCleared.ContainsKey(eventId))
		{
			_eventStagesCleared[eventId]++;
		}
		else
		{
			_eventStagesCleared[eventId] = 1;
		}

		Persist();
		CheckAchievements();
	}

	public bool TryClaimEventReward(string eventId, int milestoneIndex, out string message)
	{
		message = "";
		var evt = SeasonalEventCatalog.GetById(eventId);
		if (evt == null || milestoneIndex < 0 || milestoneIndex >= evt.Milestones.Length)
		{
			message = "Invalid event or milestone.";
			return false;
		}

		var rewardKey = $"{eventId}:{milestoneIndex}";
		if (_claimedEventRewardIds.Contains(rewardKey))
		{
			message = "Already claimed.";
			return false;
		}

		var milestone = evt.Milestones[milestoneIndex];
		var progress = GetEventProgress(eventId);
		if (progress < milestone.StagesRequired)
		{
			message = $"Need {milestone.StagesRequired} clears (have {progress}).";
			return false;
		}

		ApplyEventReward(milestone.Reward);
		_claimedEventRewardIds.Add(rewardKey);
		Persist();
		message = $"Claimed: {milestone.Label}";
		return true;
	}

	public void ApplyEventStageReward(SeasonalEventReward reward)
	{
		ApplyEventReward(reward);
		Persist();
	}

	private void ApplyEventReward(SeasonalEventReward reward)
	{
		if (reward == null)
		{
			return;
		}

		switch (reward.Type?.ToLowerInvariant())
		{
			case "gold":
				Gold += reward.Amount;
				break;
			case "food":
				Food += reward.Amount;
				break;
			case "sigils":
				Sigils += reward.Amount;
				break;
			case "shards":
				RelicShards += reward.Amount;
				break;
			case "relic":
				if (!string.IsNullOrWhiteSpace(reward.ItemId))
				{
					TryGrantEquipment(reward.ItemId);
				}
				break;
		}
	}

	public bool HasClaimedEventReward(string eventId, int milestoneIndex)
	{
		return _claimedEventRewardIds.Contains($"{eventId}:{milestoneIndex}");
	}

	// ── Codex ────────────────────────────────────────────────

	public void DiscoverCodexEntry(string id)
	{
		if (string.IsNullOrWhiteSpace(id) || _discoveredCodexIds.Contains(id))
		{
			return;
		}

		if (CodexCatalog.GetById(id) == null)
		{
			return;
		}

		_discoveredCodexIds.Add(id);
		if (!_codexFirstSeenAt.ContainsKey(id))
		{
			_codexFirstSeenAt[id] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		}

		Persist();
		CheckAchievements();
	}

	public void RecordCodexKill(string enemyId)
	{
		if (string.IsNullOrWhiteSpace(enemyId))
		{
			return;
		}

		DiscoverCodexEntry(enemyId);
		if (_codexKillCounts.ContainsKey(enemyId))
		{
			_codexKillCounts[enemyId]++;
		}
		else
		{
			_codexKillCounts[enemyId] = 1;
		}
	}

	public bool IsCodexEntryDiscovered(string id) => _discoveredCodexIds.Contains(id);
	public int GetCodexKillCount(string id) => _codexKillCounts.TryGetValue(id, out var c) ? c : 0;
	public long GetCodexFirstSeenAt(string id) => _codexFirstSeenAt.TryGetValue(id, out var t) ? t : 0;
	public int DiscoveredCodexCount => _discoveredCodexIds.Count;

	// ── Skill Trees ──────────────────────────────────────────

	public void GrantTomes(int amount)
	{
		if (amount > 0)
		{
			Tomes += amount;
			Persist();
		}
	}

	public IReadOnlyCollection<string> GetUnlockedSkillNodes(string unitId)
	{
		return _unlockedSkillNodes.TryGetValue(unitId, out var set) ? set : Array.Empty<string>();
	}

	public bool IsSkillNodeUnlocked(string unitId, string nodeId)
	{
		return _unlockedSkillNodes.TryGetValue(unitId, out var set) && set.Contains(nodeId);
	}

	public SkillTreeBonus ResolveSkillTreeBonus(string unitId)
	{
		if (!_unlockedSkillNodes.TryGetValue(unitId, out var set) || set.Count == 0)
		{
			return SkillTreeBonus.None;
		}

		return UnitSkillTreeCatalog.Resolve(unitId, set);
	}

	public bool TryUnlockSkillNode(string unitId, string nodeId, out string message)
	{
		message = "";
		var node = UnitSkillTreeCatalog.GetNode(unitId, nodeId);
		if (node == null)
		{
			message = "Unknown talent node.";
			return false;
		}

		if (IsSkillNodeUnlocked(unitId, nodeId))
		{
			message = "Already unlocked.";
			return false;
		}

		if (!string.IsNullOrWhiteSpace(node.PrerequisiteNodeId) &&
			!IsSkillNodeUnlocked(unitId, node.PrerequisiteNodeId))
		{
			message = "Prerequisite not met.";
			return false;
		}

		if (Gold < node.GoldCost)
		{
			message = $"Need {node.GoldCost} gold (have {Gold}).";
			return false;
		}

		if (Tomes < node.TomeCost)
		{
			message = $"Need {node.TomeCost} tomes (have {Tomes}).";
			return false;
		}

		Gold -= node.GoldCost;
		Tomes -= node.TomeCost;

		if (!_unlockedSkillNodes.ContainsKey(unitId))
		{
			_unlockedSkillNodes[unitId] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		}

		_unlockedSkillNodes[unitId].Add(nodeId);
		TryUnlockAchievement("first_talent");

		// Check if full tree is complete
		var tree = UnitSkillTreeCatalog.GetTree(unitId);
		if (tree != null && _unlockedSkillNodes[unitId].Count >= tree.Nodes.Length)
		{
			TryUnlockAchievement("talent_master");
		}

		Persist();
		CheckAchievements();
		message = $"Unlocked {node.Title}!";
		return true;
	}

	// ── PvP Arena ────────────────────────────────────────────

	public ArenaTier GetArenaTier() => ArenaCatalog.GetTier(ArenaRating);

	public void PrepareArenaBattle(ArenaOpponentSnapshot opponent)
	{
		SelectedArenaOpponent = opponent;
		CurrentBattleMode = BattleRunMode.Arena;
		Persist();
	}

	public void ApplyArenaResult(bool won, int opponentRating)
	{
		var newRating = ArenaCatalog.CalculateElo(ArenaRating, opponentRating, won);
		ArenaRating = newRating;
		if (won)
		{
			ArenaWins++;
			Essence += 2;
			AddSeasonXP(SeasonPassCatalog.XPPerArenaWin);
			TryUnlockAchievement("arena_first_win");
			if (ArenaWins >= 10) TryUnlockAchievement("arena_10_wins");
		}
		else
		{
			ArenaLosses++;
		}

		if (ArenaRating >= 1300) TryUnlockAchievement("arena_gold_tier");
		Persist();
		CheckAchievements();
	}

	// ── Guild ────────────────────────────────────────────────

	public void SetGuildId(string guildId)
	{
		GuildId = guildId ?? "";
		if (!string.IsNullOrWhiteSpace(GuildId))
		{
			TryUnlockAchievement("guild_join");
		}

		Persist();
	}

	public void AddGuildContribution(int points)
	{
		if (points > 0)
		{
			GuildContributionPoints += points;
			Persist();
			CheckAchievements();
		}
	}

	public bool TryGuildContribute(int goldCost, int contributionPoints, out string message)
	{
		if (Gold < goldCost)
		{
			message = $"Need {goldCost} gold to contribute. You have {Gold}.";
			return false;
		}

		Gold -= goldCost;
		AddGuildContribution(contributionPoints);
		message = $"Contributed {contributionPoints} points for {goldCost} gold. Gold remaining: {Gold}";
		return true;
	}

	public GuildBonus ResolveGuildBonus()
	{
		return GuildCatalog.ResolveBonus(CachedGuildInfo);
	}

	// ── Hard Mode ────────────────────────────────────────────

	public void ToggleHardMode()
	{
		IsHardModeActive = !IsHardModeActive;
	}

	public int GetHardModeStars(int stage)
	{
		var index = stage - 1;
		return index >= 0 && index < _hardModeStars.Count ? _hardModeStars[index] : 0;
	}

	public void RecordHardModeStars(int stage, int stars)
	{
		var index = stage - 1;
		while (_hardModeStars.Count <= index) _hardModeStars.Add(0);
		_hardModeStars[index] = Math.Max(_hardModeStars[index], stars);
		if (stars > 0 && stage > HardModeHighestCleared) HardModeHighestCleared = stage;
		Persist();
	}

	public int HardModeClearedCount
	{
		get
		{
			var count = 0;
			for (var i = 0; i < _hardModeStars.Count; i++)
			{
				if (_hardModeStars[i] > 0) count++;
			}
			return count;
		}
	}

	public void ApplyHardModeVictory(int stage, int baseRewardGold, int baseRewardFood, int starsEarned)
	{
		var hardOverride = HardModeCatalog.GetForStage(stage);
		var goldReward = (int)(baseRewardGold * 1.5f) + (hardOverride?.BonusGold ?? 0);
		var foodReward = (int)(baseRewardFood * 1.5f) + (hardOverride?.BonusFood ?? 0);

		Gold += goldReward;
		Food += foodReward;
		Essence += 1;
		RecordHardModeStars(stage, starsEarned);

		// Milestone relic drop
		if (hardOverride != null && !string.IsNullOrWhiteSpace(hardOverride.MilestoneRelicId))
		{
			TryGrantEquipment(hardOverride.MilestoneRelicId);
		}

		Persist();
		CheckAchievements();
	}

	// ── Enchantments ─────────────────────────────────────────

	public void AddEssence(int amount)
	{
		if (amount > 0) { Essence += amount; Persist(); }
	}

	public EnchantmentDefinition GetRelicEnchantment(string relicId)
	{
		if (string.IsNullOrWhiteSpace(relicId)) return null;
		return _relicEnchantments.TryGetValue(relicId, out var enchId) ? EnchantmentCatalog.GetById(enchId) : null;
	}

	public string GetRelicEnchantmentId(string relicId)
	{
		return _relicEnchantments.TryGetValue(relicId, out var enchId) ? enchId : null;
	}

	public bool TryApplyEnchantment(string relicId, string enchantmentId, out string message)
	{
		message = "";
		if (!_ownedEquipmentIds.Contains(relicId))
		{
			message = "Relic not owned.";
			return false;
		}

		var ench = EnchantmentCatalog.GetById(enchantmentId);
		if (ench == null)
		{
			message = "Unknown enchantment.";
			return false;
		}

		if (Gold < ench.GoldCost)
		{
			message = $"Need {ench.GoldCost} gold (have {Gold}).";
			return false;
		}

		if (Essence < ench.EssenceCost)
		{
			message = $"Need {ench.EssenceCost} essence (have {Essence}).";
			return false;
		}

		Gold -= ench.GoldCost;
		Essence -= ench.EssenceCost;
		_relicEnchantments[relicId] = enchantmentId;
		TryUnlockAchievement("first_enchantment");
		Persist();
		message = $"Applied {ench.Title} to relic!";
		return true;
	}

	public void RemoveEnchantment(string relicId)
	{
		if (_relicEnchantments.Remove(relicId)) Persist();
	}

	private string GetEquippedRelicId(string unitId)
	{
		return _unitEquipmentSlots.TryGetValue(unitId, out var id) ? id : null;
	}

	private string GetEquippedRelicId2(string unitId)
	{
		return _unitEquipmentSlot2.TryGetValue(unitId, out var id) ? id : null;
	}

	// ── Weekly Raid ──────────────────────────────────────────

	public void ContributeRaidDamage(int damage)
	{
		var currentWeek = RaidBossCatalog.GetCurrentWeekId();
		if (LastRaidWeek != currentWeek)
		{
			LastRaidWeek = currentWeek;
			RaidDamageContributed = 0;
			_claimedRaidRewardIds.Clear();
			_raidContributionCount = 0;
		}

		RaidDamageContributed += damage;
		_raidContributionCount++;
		if (_raidContributionCount >= 3)
		{
			TryUnlockAchievement("raid_contributor");
		}

		Persist();
	}

	public bool HasClaimedRaidReward(string weekId, int milestoneIndex)
	{
		return _claimedRaidRewardIds.Contains($"{weekId}:{milestoneIndex}");
	}

	public bool TryClaimRaidReward(string weekId, int milestoneIndex, out string message)
	{
		message = "";
		var boss = RaidBossCatalog.GetForWeek(weekId);
		if (boss == null || milestoneIndex < 0 || milestoneIndex >= boss.Milestones.Length)
		{
			message = "Invalid raid or milestone.";
			return false;
		}

		var key = $"{weekId}:{milestoneIndex}";
		if (_claimedRaidRewardIds.Contains(key))
		{
			message = "Already claimed.";
			return false;
		}

		var milestone = boss.Milestones[milestoneIndex];
		if (RaidDamageContributed < milestone.DamageThreshold)
		{
			message = $"Community damage not reached ({RaidDamageContributed}/{milestone.DamageThreshold}).";
			return false;
		}

		switch (milestone.RewardType?.ToLowerInvariant())
		{
			case "gold": Gold += milestone.RewardAmount; break;
			case "essence": Essence += milestone.RewardAmount; break;
			case "relic": TryGrantEquipment(milestone.RewardItemId); break;
		}

		_claimedRaidRewardIds.Add(key);
		Persist();
		message = $"Claimed: {milestone.Label}";
		return true;
	}

	// ── Bounty Board ─────────────────────────────────────────

	private void ResetBountiesIfNewDay()
	{
		var today = BountyBoardCatalog.GetDateKey();
		if (_lastBountyDate != today)
		{
			_lastBountyDate = today;
			_bountyProgress.Clear();
		}
	}

	public int GetBountyProgress(string bountyId)
	{
		ResetBountiesIfNewDay();
		return _bountyProgress.TryGetValue(bountyId, out var p) ? p : 0;
	}

	public bool IsBountyCompleted(string bountyId)
	{
		var today = BountyBoardCatalog.GetDateKey();
		return _completedBountyIds.Contains($"{today}:{bountyId}");
	}

	public void AddBountyProgress(string trackingType, int amount)
	{
		ResetBountiesIfNewDay();
		var bounties = BountyBoardCatalog.GetDailyBounties(DateTime.UtcNow);
		foreach (var b in bounties)
		{
			if (string.Equals(b.TrackingType, trackingType, StringComparison.OrdinalIgnoreCase))
			{
				if (_bountyProgress.ContainsKey(b.Id))
					_bountyProgress[b.Id] += amount;
				else
					_bountyProgress[b.Id] = amount;
			}
		}
	}

	public bool TryClaimBounty(string bountyId, out string message)
	{
		message = "";
		var bounty = BountyBoardCatalog.GetById(bountyId);
		if (bounty == null) { message = "Unknown bounty."; return false; }

		var today = BountyBoardCatalog.GetDateKey();
		var key = $"{today}:{bountyId}";
		if (_completedBountyIds.Contains(key)) { message = "Already claimed."; return false; }

		var progress = GetBountyProgress(bountyId);
		if (progress < bounty.TargetCount) { message = $"Progress: {progress}/{bounty.TargetCount}"; return false; }

		switch (bounty.RewardType?.ToLowerInvariant())
		{
			case "gold": Gold += bounty.RewardAmount; break;
			case "food": Food += bounty.RewardAmount; break;
			case "tomes": Tomes += bounty.RewardAmount; break;
			case "essence": Essence += bounty.RewardAmount; break;
			case "sigils": Sigils += bounty.RewardAmount; break;
		}

		_completedBountyIds.Add(key);
		AddSeasonXP(SeasonPassCatalog.XPPerBounty);
		Persist();
		CheckAchievements();
		message = $"Claimed +{bounty.RewardAmount} {bounty.RewardType}!";
		return true;
	}

	// ── Challenge Tower ──────────────────────────────────────

	public int GetTowerFloorStars(int floor)
	{
		var index = floor - 1;
		return index >= 0 && index < _towerFloorStars.Count ? _towerFloorStars[index] : 0;
	}

	public void PrepareTowerBattle(int floor)
	{
		SelectedTowerFloor = floor;
		CurrentBattleMode = BattleRunMode.Tower;
		Persist();
	}

	public void ApplyTowerVictory(int floor, int starsEarned)
	{
		var floorDef = ChallengeTowerCatalog.GetFloor(floor);
		if (floorDef == null) return;

		Gold += floorDef.RewardGold;
		Food += floorDef.RewardFood;
		if (floorDef.RewardTomes > 0) Tomes += floorDef.RewardTomes;
		if (floorDef.RewardEssence > 0) Essence += floorDef.RewardEssence;
		if (!string.IsNullOrWhiteSpace(floorDef.MilestoneRelicId))
			TryGrantEquipment(floorDef.MilestoneRelicId);

		// Record stars
		var index = floor - 1;
		while (_towerFloorStars.Count <= index) _towerFloorStars.Add(0);
		_towerFloorStars[index] = Math.Max(_towerFloorStars[index], starsEarned);
		if (floor > TowerHighestFloor) TowerHighestFloor = floor;
		AddSeasonXP(SeasonPassCatalog.XPPerTowerFloor);

		Persist();
		CheckAchievements();
	}

	// ── Friends ──────────────────────────────────────────────

	public IReadOnlyCollection<string> GetFriendIds() => _friendIds;

	public void AddFriend(string profileId)
	{
		if (!string.IsNullOrWhiteSpace(profileId) && _friendIds.Add(profileId.Trim()))
		{
			Persist();
		}
	}

	public void RemoveFriend(string profileId)
	{
		if (_friendIds.Remove(profileId))
		{
			Persist();
		}
	}

	public bool TrySendGift(string friendProfileId, out string message)
	{
		message = "";
		var today = BountyBoardCatalog.GetDateKey();
		if (_lastGiftSentDate != today)
		{
			_lastGiftSentDate = today;
			_giftsSentToday = 0;
		}

		if (_giftsSentToday >= 3) { message = "Maximum 3 gifts per day."; return false; }
		if (!_friendIds.Contains(friendProfileId)) { message = "Not on friend list."; return false; }

		_giftsSentToday++;
		Gold += 50;
		Food += 2;
		TryUnlockAchievement("gift_sent");
		Persist();
		message = "Gift sent! +50 gold, +2 food.";
		return true;
	}

	// ── Mastery ──────────────────────────────────────────────

	public int GetUnitMasteryXP(string unitId)
	{
		return _unitMasteryXP.TryGetValue(unitId, out var xp) ? xp : 0;
	}

	public MasteryRankDefinition GetUnitMasteryRank(string unitId)
	{
		return MasteryCatalog.GetRank(GetUnitMasteryXP(unitId));
	}

	public void AddUnitMasteryXP(string unitId, int amount)
	{
		if (string.IsNullOrWhiteSpace(unitId) || amount <= 0) return;

		if (_unitMasteryXP.ContainsKey(unitId))
			_unitMasteryXP[unitId] += amount;
		else
			_unitMasteryXP[unitId] = amount;

		var rank = MasteryCatalog.GetRank(_unitMasteryXP[unitId]);
		if (rank.Rank >= 2) TryUnlockAchievement("first_mastery");
		if (rank.Rank >= 5) TryUnlockAchievement("grand_master");
	}

	public MasteryBonus ResolveMasteryBonus(string unitId)
	{
		return MasteryCatalog.ResolveBonus(GetUnitMasteryXP(unitId));
	}

	// ── Achievement Rewards ──────────────────────────────────

	public bool HasClaimedAchievementReward(string achievementId)
	{
		return _claimedAchievementRewardIds.Contains(achievementId);
	}

	public int GetUnclaimedAchievementRewardCount()
	{
		var count = 0;
		foreach (var a in AchievementCatalog.GetAll())
		{
			if (_unlockedAchievementIds.Contains(a.Id) &&
				!_claimedAchievementRewardIds.Contains(a.Id) &&
				AchievementRewardCatalog.GetForAchievement(a.Id) != null)
			{
				count++;
			}
		}
		return count;
	}

	public bool TryClaimAchievementReward(string achievementId, out string message)
	{
		message = "";
		if (!_unlockedAchievementIds.Contains(achievementId))
		{
			message = "Achievement not unlocked.";
			return false;
		}

		if (_claimedAchievementRewardIds.Contains(achievementId))
		{
			message = "Already claimed.";
			return false;
		}

		var reward = AchievementRewardCatalog.GetForAchievement(achievementId);
		if (reward == null)
		{
			message = "No reward for this achievement.";
			return false;
		}

		switch (reward.RewardType?.ToLowerInvariant())
		{
			case "gold": Gold += reward.RewardAmount; break;
			case "food": Food += reward.RewardAmount; break;
			case "tomes": Tomes += reward.RewardAmount; break;
			case "essence": Essence += reward.RewardAmount; break;
			case "sigils": Sigils += reward.RewardAmount; break;
			case "relic": TryGrantEquipment(reward.RewardItemId); break;
		}

		_claimedAchievementRewardIds.Add(achievementId);
		Persist();
		message = $"Claimed: {reward.RewardLabel}";
		return true;
	}

	// ── Login Calendar ───────────────────────────────────────

	public bool CanClaimLoginReward()
	{
		var today = BountyBoardCatalog.GetDateKey();
		var currentMonth = LoginCalendarCatalog.GetCurrentMonth();

		if (_loginCalendarMonth != currentMonth)
		{
			return true; // New month = reset + day 1 available
		}

		return _lastLoginCalendarDate != today && LoginCalendarDay < LoginCalendarCatalog.TotalDays;
	}

	public bool TryClaimLoginReward(out string message)
	{
		message = "";
		var today = BountyBoardCatalog.GetDateKey();
		var currentMonth = LoginCalendarCatalog.GetCurrentMonth();

		// Reset at month boundary
		if (_loginCalendarMonth != currentMonth)
		{
			_loginCalendarMonth = currentMonth;
			LoginCalendarDay = 0;
			_lastLoginCalendarDate = "";
		}

		if (_lastLoginCalendarDate == today)
		{
			message = "Already claimed today.";
			return false;
		}

		if (LoginCalendarDay >= LoginCalendarCatalog.TotalDays)
		{
			message = "Calendar complete for this month.";
			return false;
		}

		LoginCalendarDay++;
		_lastLoginCalendarDate = today;

		var reward = LoginCalendarCatalog.GetDay(LoginCalendarDay);
		if (reward != null)
		{
			switch (reward.RewardType?.ToLowerInvariant())
			{
				case "gold": Gold += reward.RewardAmount; break;
				case "food": Food += reward.RewardAmount; break;
				case "tomes": Tomes += reward.RewardAmount; break;
				case "essence": Essence += reward.RewardAmount; break;
				case "sigils": Sigils += reward.RewardAmount; break;
			}

			Persist();
			message = $"Day {LoginCalendarDay}: {reward.Label}";
			return true;
		}

		Persist();
		message = $"Day {LoginCalendarDay} claimed.";
		return true;
	}

	// ── War Wagon Cosmetics ──────────────────────────────────

	public void SetWagonSkin(string skinId)
	{
		if (WagonSkinCatalog.IsSkinUnlocked(skinId, this))
		{
			SelectedWagonSkinId = skinId;
			if (!skinId.Equals(WagonSkinCatalog.DefaultSkinId, StringComparison.OrdinalIgnoreCase))
			{
				TryUnlockAchievement("first_skin");
			}
			Persist();
		}
	}

	public IReadOnlyList<WagonSkinDefinition> GetUnlockedWagonSkins()
	{
		return WagonSkinCatalog.GetUnlocked(this);
	}

	public Godot.Color GetWagonSkinColor()
	{
		var skin = WagonSkinCatalog.GetById(SelectedWagonSkinId);
		return new Godot.Color(skin.ColorHex);
	}

	// ── Unit Awakening ───────────────────────────────────────

	public int GetUnitStarLevel(string unitId)
	{
		return _unitStarLevels.TryGetValue(unitId, out var s) ? s : 0;
	}

	public int GetUnitTokens(string unitId)
	{
		return _unitTokens.TryGetValue(unitId, out var t) ? t : 0;
	}

	public void GrantUnitTokens(string unitId, int amount)
	{
		if (string.IsNullOrWhiteSpace(unitId) || amount <= 0) return;
		if (_unitTokens.ContainsKey(unitId)) _unitTokens[unitId] += amount;
		else _unitTokens[unitId] = amount;
	}

	public bool TryAwakenUnit(string unitId, out string message)
	{
		message = "";
		var currentStars = GetUnitStarLevel(unitId);
		var nextLevel = AwakeningCatalog.GetNextLevel(currentStars);
		if (nextLevel == null)
		{
			message = "Maximum star level reached.";
			return false;
		}

		var tokens = GetUnitTokens(unitId);
		if (tokens < nextLevel.TokenCost)
		{
			message = $"Need {nextLevel.TokenCost} tokens (have {tokens}).";
			return false;
		}

		if (Gold < nextLevel.GoldCost)
		{
			message = $"Need {nextLevel.GoldCost} gold (have {Gold}).";
			return false;
		}

		_unitTokens[unitId] -= nextLevel.TokenCost;
		Gold -= nextLevel.GoldCost;
		_unitStarLevels[unitId] = currentStars + 1;
		TryUnlockAchievement("first_awakening");
		Persist();
		message = $"Awakened to {currentStars + 1} stars!";
		return true;
	}

	public AwakeningBonus ResolveAwakeningBonus(string unitId)
	{
		return AwakeningCatalog.ResolveBonus(GetUnitStarLevel(unitId));
	}

	// ── Season Pass ──────────────────────────────────────────

	public void AddSeasonXP(int amount)
	{
		if (amount <= 0) return;
		SeasonPassXP += amount;
		SeasonPassTier = SeasonPassCatalog.GetTierForXP(SeasonPassXP);
		Persist();
	}

	public bool HasClaimedSeasonFreeTier(int tier)
	{
		return _claimedSeasonFreeTiers.Contains(tier);
	}

	public bool HasClaimedSeasonPremiumTier(int tier)
	{
		return _claimedSeasonPremiumTiers.Contains(tier);
	}

	public bool TryClaimSeasonReward(int tier, bool isPremium, out string message)
	{
		message = "";
		if (tier < 1 || tier > SeasonPassCatalog.MaxTier)
		{
			message = "Invalid tier.";
			return false;
		}

		if (tier > SeasonPassTier)
		{
			message = "Tier not reached yet.";
			return false;
		}

		if (isPremium && !HasPremiumPass)
		{
			message = "Requires premium pass.";
			return false;
		}

		var claimedSet = isPremium ? _claimedSeasonPremiumTiers : _claimedSeasonFreeTiers;
		if (claimedSet.Contains(tier))
		{
			message = "Already claimed.";
			return false;
		}

		var tierDef = SeasonPassCatalog.GetTier(tier);
		if (tierDef == null)
		{
			message = "Unknown tier.";
			return false;
		}

		var rewardType = isPremium ? tierDef.PremiumRewardType : tierDef.FreeRewardType;
		var rewardAmount = isPremium ? tierDef.PremiumRewardAmount : tierDef.FreeRewardAmount;
		var rewardLabel = isPremium ? tierDef.PremiumRewardLabel : tierDef.FreeRewardLabel;

		switch (rewardType?.ToLowerInvariant())
		{
			case "gold": Gold += rewardAmount; break;
			case "food": Food += rewardAmount; break;
			case "tomes": Tomes += rewardAmount; break;
			case "essence": Essence += rewardAmount; break;
			case "sigils": Sigils += rewardAmount; break;
		}

		claimedSet.Add(tier);
		Persist();
		message = $"Tier {tier}: {rewardLabel}";
		return true;
	}

	public void SetPremiumPass(bool value)
	{
		HasPremiumPass = value;
		Persist();
	}

	// ── Collection Milestones ────────────────────────────────

	public bool HasClaimedCollectionMilestone(string milestoneId)
	{
		return _claimedCollectionMilestoneIds.Contains(milestoneId);
	}

	public bool TryClaimCollectionMilestone(string milestoneId, out string message)
	{
		message = "";
		var milestone = CollectionMilestoneCatalog.GetById(milestoneId);
		if (milestone == null)
		{
			message = "Unknown milestone.";
			return false;
		}

		if (_claimedCollectionMilestoneIds.Contains(milestoneId))
		{
			message = "Already claimed.";
			return false;
		}

		var progress = CollectionMilestoneCatalog.GetCollectionPercent(milestone.Category, this);
		if (progress < milestone.ThresholdPercent)
		{
			message = $"Need {milestone.ThresholdPercent}% (at {progress}%).";
			return false;
		}

		switch (milestone.RewardType?.ToLowerInvariant())
		{
			case "gold": Gold += milestone.RewardAmount; break;
			case "food": Food += milestone.RewardAmount; break;
			case "tomes": Tomes += milestone.RewardAmount; break;
			case "essence": Essence += milestone.RewardAmount; break;
			case "sigils": Sigils += milestone.RewardAmount; break;
		}

		_claimedCollectionMilestoneIds.Add(milestoneId);
		Persist();
		CheckAchievements();
		message = $"Claimed: {milestone.RewardLabel}";
		return true;
	}

	public int GetCollectionProgress(string category)
	{
		return CollectionMilestoneCatalog.GetCollectionPercent(category, this);
	}

	// ── Battle Mutators ──────────────────────────────────────

	public void ToggleMutator(string mutatorId)
	{
		if (_activeMutatorIds.Contains(mutatorId))
			_activeMutatorIds.Remove(mutatorId);
		else
			_activeMutatorIds.Add(mutatorId);
		Persist();
	}

	public IReadOnlyCollection<string> GetActiveMutatorIds() => _activeMutatorIds;

	public bool IsMutatorActive(string mutatorId) => _activeMutatorIds.Contains(mutatorId);

	public float GetMutatorRewardMultiplier()
	{
		var mult = 1f;
		foreach (var id in _activeMutatorIds)
		{
			var def = BattleMutatorCatalog.GetById(id);
			if (def != null) mult *= def.GoldRewardMultiplier;
		}
		return mult;
	}

	public void RecordMutatorBattleComplete()
	{
		if (_activeMutatorIds.Count > 0)
		{
			MutatorBattlesCompleted++;
			if (MutatorBattlesCompleted >= 5) TryUnlockAchievement("mutator_5");
			Persist();
		}
	}

	public void ClearMutators()
	{
		_activeMutatorIds.Clear();
		Persist();
	}

	// ── Accessibility ────────────────────────────────────────

	public void SetColorblindMode(string mode)
	{
		ColorblindMode = mode ?? "none";
		Persist();
	}

	public void SetReducedMotion(bool enabled)
	{
		ReducedMotion = enabled;
		Persist();
	}

	public void SetAutoBattle(bool enabled)
	{
		AutoBattleEnabled = enabled;
		Persist();
	}

	public void SetLargeTextMode(bool enabled)
	{
		LargeTextMode = enabled;
		Persist();
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

		if (IsUnitOnExpedition(normalizedId))
		{
			message = "Unit is away on expedition.";
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
		Locale.SetLanguage(Language);
		MusicPlayer.Instance?.SetVolumeScale(MusicVolumePercent / 100f);
		ApplyFontSizeOffset();
		GrantPendingDistrictRewardsOnLoad();
		Persist();
		AnalyticsService.TrackSessionStart();
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
		LastDailyDate = "";
		TotalChallengeSubmissionsSynced = 0;
		ChallengeSyncProviderId = DefaultChallengeSyncProviderId;
		ChallengeSyncEndpoint = DefaultChallengeSyncEndpoint;
		ChallengeSyncAutoFlush = DefaultChallengeSyncAutoFlush;
		DifficultyId = DefaultDifficultyId;
		ShowDevUi = DefaultShowDevUi;
		ShowFpsCounter = DefaultShowFpsCounter;
		AudioMuted = DefaultAudioMuted;
		EffectsVolumePercent = DefaultEffectsVolumePercent;
		AmbienceVolumePercent = DefaultAmbienceVolumePercent;
		MusicVolumePercent = DefaultMusicVolumePercent;
		Language = DefaultLanguage;
		AnalyticsConsent = false;
		HasShownConsentPrompt = false;
		FontSizeOffset = 0;
		HighContrast = false;
		PrestigeLevel = 0;
		PrestigeTotalGoldEarned = 0;
		PrestigeTotalStagesCleared = 0;
		BestBossRushWave = 0;
		BestBossRushTimeSeconds = 0f;
		BossRushRuns = 0;
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
		_spellUpgradeLevels.Clear();
		_baseUpgradeLevels.Clear();
		_unitDoctrineSelections.Clear();
		_armedCampaignDirectiveStage = 0;
		_challengeBestScores.Clear();
		_endlessRunHistory.Clear();
		_challengeHistory.Clear();
		_pendingChallengeSubmissions.Clear();
		_pinnedChallengeCodes.Clear();
		_claimedDistrictRewardIds.Clear();
		_claimedCampaignDirectiveIds.Clear();
		_ownedEquipmentIds.Clear();
		_unitEquipmentSlots.Clear();
		_unlockedAchievementIds.Clear();
		_unitPrestigeSelections.Clear();
		_purchasedProductIds.Clear();
		_totalPurchaseCount = 0;
		_purchaseValidationEndpoint = "";
		BestEndlessWave = 0;
		BestEndlessTimeSeconds = 0f;
		EndlessRuns = 0;
		ChallengeRuns = 0;
		_dailyStreak = 0;

		// v32
		RelicShards = 0;
		Sigils = 0;
		_promotedUnitIds.Clear();
		_unitEquipmentSlot2.Clear();
		_activeExpeditions.Clear();
		TotalExpeditionsCompleted = 0;
		_eventStagesCleared.Clear();
		_claimedEventRewardIds.Clear();
		SelectedEventId = "";
		SelectedEventStageIndex = 0;

		// v38
		_activeMutatorIds.Clear();
		MutatorBattlesCompleted = 0;
		ColorblindMode = "none";
		ReducedMotion = false;
		AutoBattleEnabled = false;
		LargeTextMode = false;

		// v37
		_unitStarLevels.Clear();
		_unitTokens.Clear();
		SeasonPassXP = 0;
		SeasonPassTier = 0;
		SeasonId = SeasonPassCatalog.CurrentSeasonId;
		HasPremiumPass = false;
		_claimedSeasonFreeTiers.Clear();
		_claimedSeasonPremiumTiers.Clear();
		_claimedCollectionMilestoneIds.Clear();

		// v36
		_claimedAchievementRewardIds.Clear();
		LoginCalendarDay = 0;
		_lastLoginCalendarDate = "";
		_loginCalendarMonth = "";
		SelectedWagonSkinId = WagonSkinCatalog.DefaultSkinId;

		// v35
		_completedBountyIds.Clear();
		_bountyProgress.Clear();
		_lastBountyDate = "";
		TowerHighestFloor = 0;
		SelectedTowerFloor = 0;
		_towerFloorStars.Clear();
		_friendIds.Clear();
		_lastGiftSentDate = "";
		_giftsSentToday = 0;
		_unitMasteryXP.Clear();

		// v34
		IsHardModeActive = false;
		HardModeHighestCleared = 0;
		_hardModeStars.Clear();
		Essence = 0;
		_relicEnchantments.Clear();
		LastRaidWeek = "";
		RaidDamageContributed = 0;
		_claimedRaidRewardIds.Clear();
		_raidContributionCount = 0;

		// v33
		_discoveredCodexIds.Clear();
		_codexKillCounts.Clear();
		_codexFirstSeenAt.Clear();
		Tomes = 0;
		_unlockedSkillNodes.Clear();
		ArenaRating = 1000;
		ArenaWins = 0;
		ArenaLosses = 0;
		SelectedArenaOpponent = null;
		GuildId = "";
		GuildContributionPoints = 0;
		CachedGuildInfo = null;
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
			MusicVolumePercent = saved.Version >= 31
				? Mathf.Clamp(saved.MusicVolumePercent, 0, 100)
				: DefaultMusicVolumePercent;
			Language = saved.Version >= 31 && !string.IsNullOrWhiteSpace(saved.Language)
				? saved.Language.Trim().ToLowerInvariant()
				: DefaultLanguage;
			AnalyticsConsent = saved.Version >= 31 && saved.AnalyticsConsent;
			HasShownConsentPrompt = saved.Version >= 31 && saved.HasShownConsentPrompt;
			FontSizeOffset = saved.Version >= 31 ? Mathf.Clamp(saved.FontSizeOffset, -4, 8) : 0;
			HighContrast = saved.Version >= 31 && saved.HighContrast;
			PrestigeLevel = saved.Version >= 31 ? Mathf.Clamp(saved.PrestigeLevel, 0, MaxPrestigeLevel) : 0;
			PrestigeTotalGoldEarned = saved.Version >= 31 ? Math.Max(0, saved.PrestigeTotalGoldEarned) : 0;
			PrestigeTotalStagesCleared = saved.Version >= 31 ? Math.Max(0, saved.PrestigeTotalStagesCleared) : 0;
			BestBossRushWave = saved.Version >= 31 ? Math.Max(0, saved.BestBossRushWave) : 0;
			BestBossRushTimeSeconds = saved.Version >= 31 ? Math.Max(0f, saved.BestBossRushTimeSeconds) : 0f;
			BossRushRuns = saved.Version >= 31 ? Math.Max(0, saved.BossRushRuns) : 0;
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

		_spellUpgradeLevels.Clear();
		if (saved.Version >= 25 && saved.SpellLevels != null)
		{
			foreach (var pair in saved.SpellLevels)
			{
				if (!string.IsNullOrWhiteSpace(pair.Key))
				{
					_spellUpgradeLevels[pair.Key] = pair.Value;
				}
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

		_ownedEquipmentIds.Clear();
		if (saved.Version >= 26 && saved.OwnedEquipmentIds != null)
		{
			foreach (var equipId in saved.OwnedEquipmentIds)
			{
				if (!string.IsNullOrWhiteSpace(equipId))
				{
					_ownedEquipmentIds.Add(equipId.Trim());
				}
			}
		}

		_unitEquipmentSlots.Clear();
		if (saved.Version >= 26 && saved.UnitEquipmentSlots != null)
		{
			foreach (var pair in saved.UnitEquipmentSlots)
			{
				if (!string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
				{
					_unitEquipmentSlots[pair.Key.Trim()] = pair.Value.Trim();
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

		_endlessRunHistory.Clear();
		if (saved.Version >= 29 && saved.EndlessRunHistory != null)
		{
			foreach (var entry in saved.EndlessRunHistory)
			{
				if (entry == null)
				{
					continue;
				}

				_endlessRunHistory.Add(new EndlessRunRecord
				{
					Wave = entry.Wave,
					TimeSeconds = entry.TimeSeconds,
					RouteId = entry.RouteId ?? "",
					BoonId = entry.BoonId ?? "",
					GoldEarned = entry.GoldEarned,
					FoodEarned = entry.FoodEarned,
					Date = entry.Date ?? "",
					DifficultyId = entry.DifficultyId ?? "normal"
				});
			}
		}
		NormalizeEndlessRunHistory();

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

		LastDailyDate = saved.Version >= 27 && !string.IsNullOrWhiteSpace(saved.LastDailyDate)
			? saved.LastDailyDate.Trim()
			: "";

		DifficultyId = saved.Version >= 28 && !string.IsNullOrWhiteSpace(saved.DifficultyId)
			? DifficultyCatalog.GetById(saved.DifficultyId).Id
			: DefaultDifficultyId;

		ShowHints = saved.Version < 28 || saved.ShowHints;

		_seenHintIds.Clear();
		if (saved.Version >= 28 && saved.SeenHintIds != null)
		{
			foreach (var hintId in saved.SeenHintIds)
			{
				if (!string.IsNullOrWhiteSpace(hintId))
				{
					_seenHintIds.Add(hintId.Trim());
				}
			}
		}

		_dailyStreak = saved.DailyStreak > 0 ? saved.DailyStreak : 0;

		_unlockedAchievementIds.Clear();
		if (saved.UnlockedAchievementIds != null)
		{
			foreach (var achievementId in saved.UnlockedAchievementIds)
			{
				if (!string.IsNullOrWhiteSpace(achievementId))
				{
					_unlockedAchievementIds.Add(achievementId.Trim());
				}
			}
		}

		_unitPrestigeSelections.Clear();
		if (saved.Version >= 30 && saved.UnitPrestigeSelections != null)
		{
			foreach (var (unitId, index) in saved.UnitPrestigeSelections)
			{
				if (!string.IsNullOrWhiteSpace(unitId) && index >= 1 && index <= PrestigeColorCatalog.MaxPrestigeIndex)
				{
					_unitPrestigeSelections[unitId.Trim()] = index;
				}
			}
		}

		_purchasedProductIds.Clear();
		_totalPurchaseCount = 0;
		_purchaseValidationEndpoint = "";
		if (saved.Version >= 31)
		{
			if (saved.PurchasedProductIds != null)
			{
				foreach (var id in saved.PurchasedProductIds)
				{
					if (!string.IsNullOrWhiteSpace(id))
					{
						_purchasedProductIds.Add(id.Trim());
					}
				}
			}

			_totalPurchaseCount = Math.Max(0, saved.TotalPurchaseCount);
			_purchaseValidationEndpoint = saved.PurchaseValidationEndpoint?.Trim() ?? "";
		}

		// v32: Relic Forge, Promotion, Expeditions, Events
		if (saved.Version >= 32)
		{
			RelicShards = Math.Max(0, saved.RelicShards);
			Sigils = Math.Max(0, saved.Sigils);

			_promotedUnitIds.Clear();
			if (saved.PromotedUnitIds != null)
			{
				foreach (var id in saved.PromotedUnitIds)
				{
					if (!string.IsNullOrWhiteSpace(id))
					{
						_promotedUnitIds.Add(id.Trim());
					}
				}
			}

			_unitEquipmentSlot2.Clear();
			if (saved.UnitEquipmentSlot2 != null)
			{
				foreach (var pair in saved.UnitEquipmentSlot2)
				{
					if (!string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
					{
						_unitEquipmentSlot2[pair.Key.Trim()] = pair.Value.Trim();
					}
				}
			}

			_activeExpeditions.Clear();
			if (saved.ActiveExpeditions != null)
			{
				foreach (var slot in saved.ActiveExpeditions)
				{
					if (slot != null && !string.IsNullOrWhiteSpace(slot.ExpeditionId))
					{
						_activeExpeditions.Add(new ExpeditionSlotState
						{
							ExpeditionId = slot.ExpeditionId.Trim(),
							AssignedUnitIds = slot.AssignedUnitIds ?? Array.Empty<string>(),
							StartedAtUnixSeconds = slot.StartedAtUnixSeconds
						});
					}
				}
			}
			TotalExpeditionsCompleted = Math.Max(0, saved.TotalExpeditionsCompleted);

			_eventStagesCleared.Clear();
			if (saved.EventStagesCleared != null)
			{
				foreach (var pair in saved.EventStagesCleared)
				{
					if (!string.IsNullOrWhiteSpace(pair.Key))
					{
						_eventStagesCleared[pair.Key.Trim()] = Math.Max(0, pair.Value);
					}
				}
			}

			_claimedEventRewardIds.Clear();
			if (saved.ClaimedEventRewardIds != null)
			{
				foreach (var id in saved.ClaimedEventRewardIds)
				{
					if (!string.IsNullOrWhiteSpace(id))
					{
						_claimedEventRewardIds.Add(id.Trim());
					}
				}
			}
		}

		// v33: Codex, Skill Trees, Arena, Guild
		if (saved.Version >= 33)
		{
			_discoveredCodexIds.Clear();
			if (saved.DiscoveredCodexIds != null)
			{
				foreach (var id in saved.DiscoveredCodexIds)
				{
					if (!string.IsNullOrWhiteSpace(id)) _discoveredCodexIds.Add(id.Trim());
				}
			}

			_codexKillCounts.Clear();
			if (saved.CodexKillCounts != null)
			{
				foreach (var pair in saved.CodexKillCounts)
				{
					if (!string.IsNullOrWhiteSpace(pair.Key)) _codexKillCounts[pair.Key.Trim()] = Math.Max(0, pair.Value);
				}
			}

			_codexFirstSeenAt.Clear();
			if (saved.CodexFirstSeenAt != null)
			{
				foreach (var pair in saved.CodexFirstSeenAt)
				{
					if (!string.IsNullOrWhiteSpace(pair.Key)) _codexFirstSeenAt[pair.Key.Trim()] = pair.Value;
				}
			}

			Tomes = Math.Max(0, saved.Tomes);

			_unlockedSkillNodes.Clear();
			if (saved.UnlockedSkillNodeIds != null)
			{
				foreach (var pair in saved.UnlockedSkillNodeIds)
				{
					if (!string.IsNullOrWhiteSpace(pair.Key) && pair.Value != null)
					{
						var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
						foreach (var nodeId in pair.Value)
						{
							if (!string.IsNullOrWhiteSpace(nodeId)) set.Add(nodeId.Trim());
						}
						if (set.Count > 0) _unlockedSkillNodes[pair.Key.Trim()] = set;
					}
				}
			}

			ArenaRating = Math.Max(0, saved.ArenaRating);
			ArenaWins = Math.Max(0, saved.ArenaWins);
			ArenaLosses = Math.Max(0, saved.ArenaLosses);

			GuildId = saved.GuildId?.Trim() ?? "";
			GuildContributionPoints = Math.Max(0, saved.GuildContributionPoints);
		}

		// v34: Hard Mode, Enchantments, Raid
		if (saved.Version >= 34)
		{
			_hardModeStars.Clear();
			if (saved.HardModeStars != null)
			{
				foreach (var s in saved.HardModeStars) _hardModeStars.Add(Math.Max(0, s));
			}
			HardModeHighestCleared = Math.Max(0, saved.HardModeHighestCleared);

			Essence = Math.Max(0, saved.Essence);
			_relicEnchantments.Clear();
			if (saved.RelicEnchantments != null)
			{
				foreach (var pair in saved.RelicEnchantments)
				{
					if (!string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
						_relicEnchantments[pair.Key.Trim()] = pair.Value.Trim();
				}
			}

			LastRaidWeek = saved.LastRaidWeek?.Trim() ?? "";
			RaidDamageContributed = Math.Max(0, saved.RaidDamageContributed);
			_claimedRaidRewardIds.Clear();
			if (saved.ClaimedRaidRewardIds != null)
			{
				foreach (var id in saved.ClaimedRaidRewardIds)
				{
					if (!string.IsNullOrWhiteSpace(id)) _claimedRaidRewardIds.Add(id.Trim());
				}
			}
		}

		// v35: Bounty, Tower, Friends, Mastery
		if (saved.Version >= 35)
		{
			_completedBountyIds.Clear();
			if (saved.CompletedBountyIds != null)
				foreach (var id in saved.CompletedBountyIds)
					if (!string.IsNullOrWhiteSpace(id)) _completedBountyIds.Add(id.Trim());

			_bountyProgress.Clear();
			if (saved.BountyProgress != null)
				foreach (var pair in saved.BountyProgress)
					if (!string.IsNullOrWhiteSpace(pair.Key)) _bountyProgress[pair.Key.Trim()] = Math.Max(0, pair.Value);

			_lastBountyDate = saved.LastBountyDate?.Trim() ?? "";

			TowerHighestFloor = Math.Max(0, saved.TowerHighestFloor);
			_towerFloorStars.Clear();
			if (saved.TowerFloorStars != null)
				foreach (var s in saved.TowerFloorStars) _towerFloorStars.Add(Math.Max(0, s));

			_friendIds.Clear();
			if (saved.FriendIds != null)
				foreach (var id in saved.FriendIds)
					if (!string.IsNullOrWhiteSpace(id)) _friendIds.Add(id.Trim());

			_lastGiftSentDate = saved.LastGiftSentDate?.Trim() ?? "";
			_giftsSentToday = Math.Max(0, saved.GiftsSentToday);

			_unitMasteryXP.Clear();
			if (saved.UnitMasteryXP != null)
				foreach (var pair in saved.UnitMasteryXP)
					if (!string.IsNullOrWhiteSpace(pair.Key)) _unitMasteryXP[pair.Key.Trim()] = Math.Max(0, pair.Value);
		}

		// v36: Achievement Rewards, Login Calendar, Wagon Skins
		if (saved.Version >= 36)
		{
			_claimedAchievementRewardIds.Clear();
			if (saved.ClaimedAchievementRewardIds != null)
				foreach (var id in saved.ClaimedAchievementRewardIds)
					if (!string.IsNullOrWhiteSpace(id)) _claimedAchievementRewardIds.Add(id.Trim());

			LoginCalendarDay = Math.Max(0, saved.LoginCalendarDay);
			_lastLoginCalendarDate = saved.LastLoginCalendarDate?.Trim() ?? "";
			_loginCalendarMonth = saved.LoginCalendarMonth?.Trim() ?? "";

			SelectedWagonSkinId = !string.IsNullOrWhiteSpace(saved.SelectedWagonSkinId)
				? saved.SelectedWagonSkinId.Trim()
				: WagonSkinCatalog.DefaultSkinId;
		}

		// v37: Awakening, Season Pass, Collection Milestones
		if (saved.Version >= 37)
		{
			_unitStarLevels.Clear();
			if (saved.UnitStarLevels != null)
				foreach (var pair in saved.UnitStarLevels)
					if (!string.IsNullOrWhiteSpace(pair.Key)) _unitStarLevels[pair.Key.Trim()] = Math.Clamp(pair.Value, 0, AwakeningCatalog.MaxStars);

			_unitTokens.Clear();
			if (saved.UnitTokens != null)
				foreach (var pair in saved.UnitTokens)
					if (!string.IsNullOrWhiteSpace(pair.Key)) _unitTokens[pair.Key.Trim()] = Math.Max(0, pair.Value);

			SeasonPassXP = Math.Max(0, saved.SeasonPassXP);
			SeasonPassTier = Math.Max(0, saved.SeasonPassTier);
			SeasonId = saved.SeasonId?.Trim() ?? SeasonPassCatalog.CurrentSeasonId;
			HasPremiumPass = saved.HasPremiumPass;

			_claimedSeasonFreeTiers.Clear();
			if (saved.ClaimedSeasonFreeTiers != null)
				foreach (var t in saved.ClaimedSeasonFreeTiers) _claimedSeasonFreeTiers.Add(t);

			_claimedSeasonPremiumTiers.Clear();
			if (saved.ClaimedSeasonPremiumTiers != null)
				foreach (var t in saved.ClaimedSeasonPremiumTiers) _claimedSeasonPremiumTiers.Add(t);

			_claimedCollectionMilestoneIds.Clear();
			if (saved.ClaimedCollectionMilestoneIds != null)
				foreach (var id in saved.ClaimedCollectionMilestoneIds)
					if (!string.IsNullOrWhiteSpace(id)) _claimedCollectionMilestoneIds.Add(id.Trim());
		}

		// v38: Battle Mutators, Accessibility
		if (saved.Version >= 38)
		{
			_activeMutatorIds.Clear();
			if (saved.ActiveMutatorIds != null)
				foreach (var id in saved.ActiveMutatorIds)
					if (!string.IsNullOrWhiteSpace(id)) _activeMutatorIds.Add(id.Trim());
			MutatorBattlesCompleted = Math.Max(0, saved.MutatorBattlesCompleted);

			ColorblindMode = saved.ColorblindMode?.Trim() ?? "none";
			ReducedMotion = saved.ReducedMotion;
			AutoBattleEnabled = saved.AutoBattleEnabled;
			LargeTextMode = saved.LargeTextMode;
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
		NormalizeEndlessRunHistory();
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
			MusicVolumePercent = MusicVolumePercent,
			Language = Language ?? DefaultLanguage,
			AnalyticsConsent = AnalyticsConsent,
			HasShownConsentPrompt = HasShownConsentPrompt,
			FontSizeOffset = FontSizeOffset,
			HighContrast = HighContrast,
			PrestigeLevel = PrestigeLevel,
			PrestigeTotalGoldEarned = PrestigeTotalGoldEarned,
			PrestigeTotalStagesCleared = PrestigeTotalStagesCleared,
			BestBossRushWave = BestBossRushWave,
			BestBossRushTimeSeconds = BestBossRushTimeSeconds,
			BossRushRuns = BossRushRuns,
			ActiveDeckUnitIds = _activeDeckUnitIds.ToArray(),
			ActiveDeckSpellIds = _activeDeckSpellIds.ToArray(),
			OwnedPlayerUnitIds = _ownedPlayerUnitIds.ToArray(),
			OwnedPlayerSpellIds = _ownedPlayerSpellIds.ToArray(),
			StageStars = _stageStars.ToArray(),
			UnitLevels = new Dictionary<string, int>(_unitUpgradeLevels),
			SpellLevels = new Dictionary<string, int>(_spellUpgradeLevels),
			BaseUpgradeLevels = new Dictionary<string, int>(_baseUpgradeLevels),
			UnitDoctrineIds = new Dictionary<string, string>(_unitDoctrineSelections),
			BestEndlessWave = BestEndlessWave,
			BestEndlessTimeSeconds = BestEndlessTimeSeconds,
			EndlessRuns = EndlessRuns,
			EndlessRunHistory = _endlessRunHistory
				.Select(e => new EndlessRunRecord
				{
					Wave = e.Wave,
					TimeSeconds = e.TimeSeconds,
					RouteId = e.RouteId,
					BoonId = e.BoonId,
					GoldEarned = e.GoldEarned,
					FoodEarned = e.FoodEarned,
					Date = e.Date,
					DifficultyId = e.DifficultyId
				})
				.ToList(),
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
			ClaimedCampaignDirectiveIds = _claimedCampaignDirectiveIds.ToArray(),
			OwnedEquipmentIds = _ownedEquipmentIds.ToArray(),
			UnitEquipmentSlots = new Dictionary<string, string>(_unitEquipmentSlots),
			LastDailyDate = LastDailyDate ?? "",
			DifficultyId = DifficultyId ?? DefaultDifficultyId,
			ShowHints = ShowHints,
			SeenHintIds = _seenHintIds.ToArray(),
			DailyStreak = _dailyStreak,
			UnlockedAchievementIds = _unlockedAchievementIds.ToArray(),
			UnitPrestigeSelections = new Dictionary<string, int>(_unitPrestigeSelections),
			PurchasedProductIds = _purchasedProductIds.ToArray(),
			TotalPurchaseCount = _totalPurchaseCount,
			PurchaseValidationEndpoint = _purchaseValidationEndpoint ?? "",

			// v32
			RelicShards = RelicShards,
			Sigils = Sigils,
			PromotedUnitIds = _promotedUnitIds.ToArray(),
			UnitEquipmentSlot2 = new Dictionary<string, string>(_unitEquipmentSlot2),
			ActiveExpeditions = _activeExpeditions
				.Select(s => new ExpeditionSlotSaveData
				{
					ExpeditionId = s.ExpeditionId,
					AssignedUnitIds = s.AssignedUnitIds,
					StartedAtUnixSeconds = s.StartedAtUnixSeconds
				})
				.ToList(),
			TotalExpeditionsCompleted = TotalExpeditionsCompleted,
			EventStagesCleared = new Dictionary<string, int>(_eventStagesCleared),
			ClaimedEventRewardIds = _claimedEventRewardIds.ToArray(),

			// v33
			DiscoveredCodexIds = _discoveredCodexIds.ToArray(),
			CodexKillCounts = new Dictionary<string, int>(_codexKillCounts),
			CodexFirstSeenAt = new Dictionary<string, long>(_codexFirstSeenAt),
			Tomes = Tomes,
			UnlockedSkillNodeIds = _unlockedSkillNodes.ToDictionary(
				p => p.Key,
				p => p.Value.ToArray()),
			ArenaRating = ArenaRating,
			ArenaWins = ArenaWins,
			ArenaLosses = ArenaLosses,
			GuildId = GuildId ?? "",
			GuildContributionPoints = GuildContributionPoints,

			// v34
			HardModeStars = _hardModeStars.ToArray(),
			HardModeHighestCleared = HardModeHighestCleared,
			Essence = Essence,
			RelicEnchantments = new Dictionary<string, string>(_relicEnchantments),
			LastRaidWeek = LastRaidWeek ?? "",
			RaidDamageContributed = RaidDamageContributed,
			ClaimedRaidRewardIds = _claimedRaidRewardIds.ToArray(),

			// v35
			CompletedBountyIds = _completedBountyIds.ToArray(),
			BountyProgress = new Dictionary<string, int>(_bountyProgress),
			LastBountyDate = _lastBountyDate ?? "",
			TowerHighestFloor = TowerHighestFloor,
			TowerFloorStars = _towerFloorStars.ToArray(),
			FriendIds = _friendIds.ToArray(),
			LastGiftSentDate = _lastGiftSentDate ?? "",
			GiftsSentToday = _giftsSentToday,
			UnitMasteryXP = new Dictionary<string, int>(_unitMasteryXP),

			// v36
			ClaimedAchievementRewardIds = _claimedAchievementRewardIds.ToArray(),
			LoginCalendarDay = LoginCalendarDay,
			LastLoginCalendarDate = _lastLoginCalendarDate ?? "",
			LoginCalendarMonth = _loginCalendarMonth ?? "",
			SelectedWagonSkinId = SelectedWagonSkinId ?? WagonSkinCatalog.DefaultSkinId,

			// v37
			UnitStarLevels = new Dictionary<string, int>(_unitStarLevels),
			UnitTokens = new Dictionary<string, int>(_unitTokens),
			SeasonPassXP = SeasonPassXP,
			SeasonPassTier = SeasonPassTier,
			SeasonId = SeasonId ?? SeasonPassCatalog.CurrentSeasonId,
			HasPremiumPass = HasPremiumPass,
			ClaimedSeasonFreeTiers = _claimedSeasonFreeTiers.ToArray(),
			ClaimedSeasonPremiumTiers = _claimedSeasonPremiumTiers.ToArray(),
			ClaimedCollectionMilestoneIds = _claimedCollectionMilestoneIds.ToArray(),

			// v38
			ActiveMutatorIds = _activeMutatorIds.ToArray(),
			MutatorBattlesCompleted = MutatorBattlesCompleted,
			ColorblindMode = ColorblindMode ?? "none",
			ReducedMotion = ReducedMotion,
			AutoBattleEnabled = AutoBattleEnabled,
			LargeTextMode = LargeTextMode
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
		var relicHint = "";
		if (!string.IsNullOrEmpty(district.RewardRelicId))
		{
			var relic = GameData.GetEquipment(district.RewardRelicId);
			if (relic != null)
			{
				relicHint = $", +{relic.DisplayName} ({relic.Rarity} relic)";
			}
		}
		var rewardText = $"+{district.RewardGold} gold, +{district.RewardFood} food{relicHint}";
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

		var relicSummary = "";
		if (!string.IsNullOrEmpty(district.RewardRelicId))
		{
			var relic = GameData.GetEquipment(district.RewardRelicId);
			if (relic != null && TryGrantEquipment(relic.Id))
			{
				relicSummary = $" Relic earned: {relic.DisplayName} ({relic.Rarity})!";
			}
		}

		var baseSummary = $"{district.Title} secured. District reward: +{district.RewardGold} gold, +{district.RewardFood} food.{relicSummary}";

		var campaignBonusSummary = TryGrantCampaignCompletionBonus();
		return string.IsNullOrWhiteSpace(campaignBonusSummary) ? baseSummary : $"{baseSummary} {campaignBonusSummary}";
	}

	private static readonly string[] CampaignCompletionBonusRelicIds =
	{
		"relic_phantom_mantle",
		"relic_dragon_heart"
	};

	private string TryGrantCampaignCompletionBonus()
	{
		var allDistricts = CampaignPlanCatalog.GetAll();
		foreach (var district in allDistricts)
		{
			if (!_claimedDistrictRewardIds.Contains(district.Id))
			{
				return "";
			}
		}

		var granted = new List<string>();
		foreach (var relicId in CampaignCompletionBonusRelicIds)
		{
			var relic = GameData.GetEquipment(relicId);
			if (relic != null && TryGrantEquipment(relic.Id))
			{
				granted.Add($"{relic.DisplayName} ({relic.Rarity})");
			}
		}

		return granted.Count > 0
			? $"Campaign complete! Bonus relics earned: {string.Join(", ", granted)}!"
			: "";
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
		var baseSummary = directive.BonusFood > 0
			? $"Heroic directive secured: {directive.Title}. +{directive.BonusGold} gold, +{directive.BonusFood} food."
			: $"Heroic directive secured: {directive.Title}. +{directive.BonusGold} gold.";

		var relicSummary = TryRollHeroicRelicDrop();
		return string.IsNullOrWhiteSpace(relicSummary) ? baseSummary : $"{baseSummary} {relicSummary}";
	}

	private string TryRollHeroicRelicDrop()
	{
		var roll = _rng.Randf();
		string targetRarity;
		if (roll < 0.05f)
			targetRarity = "epic";
		else if (roll < 0.25f)
			targetRarity = "rare";
		else if (roll < 0.65f)
			targetRarity = "common";
		else
			return "";

		var candidates = GameData.GetAllEquipment()
			.Where(e => string.Equals(e.Rarity, targetRarity, StringComparison.OrdinalIgnoreCase))
			.ToList();
		if (candidates.Count == 0)
			return "";

		var relic = candidates[_rng.RandiRange(0, candidates.Count - 1)];
		var isNew = TryGrantEquipment(relic.Id);
		return isNew
			? $"Relic drop: {relic.DisplayName} ({relic.Rarity})!"
			: $"Relic drop: {relic.DisplayName} (already owned).";
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
			if (!string.IsNullOrEmpty(district.RewardRelicId))
			{
				TryGrantEquipment(district.RewardRelicId);
			}
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

		TryGrantCampaignCompletionBonus();
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

	private void NormalizeEndlessRunHistory()
	{
		_endlessRunHistory.RemoveAll(entry => entry == null);
		foreach (var entry in _endlessRunHistory)
		{
			entry.Wave = Math.Max(0, entry.Wave);
			entry.TimeSeconds = Math.Max(0f, entry.TimeSeconds);
			entry.RouteId = NormalizeRouteId(entry.RouteId ?? DefaultEndlessRouteId);
			entry.BoonId = NormalizeEndlessBoonId(entry.BoonId ?? DefaultEndlessBoonId);
			entry.GoldEarned = Math.Max(0, entry.GoldEarned);
			entry.FoodEarned = Math.Max(0, entry.FoodEarned);
			entry.Date = entry.Date ?? "";
			entry.DifficultyId = DifficultyCatalog.GetById(entry.DifficultyId ?? DefaultDifficultyId).Id;
		}

		const int maxEntries = 20;
		if (_endlessRunHistory.Count > maxEntries)
		{
			_endlessRunHistory.RemoveRange(maxEntries, _endlessRunHistory.Count - maxEntries);
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

	public static DailyChallenge GetDailyChallenge()
	{
		var now = DateTime.UtcNow;
		var seed = now.DayOfYear + now.Year * 1000;
		var rng = new Random(seed);
		var stageIndex = rng.Next(10, 41);
		var lockedSquad = now.DayOfYear % 2 == 0;
		var boardLabels = new[] { "Route Trial", "Pressure Test", "Final Push", "Endurance Run" };
		var boardLabel = boardLabels[seed % boardLabels.Length];
		var date = now.ToString("yyyy-MM-dd");

		string[] lockedDeckUnitIds;
		if (lockedSquad)
		{
			var roster = GameData.PlayerRosterIds;
			var deckRng = new Random(seed);
			var picked = new List<string>(3);
			var available = new List<string>(roster);
			for (var i = 0; i < 3 && available.Count > 0; i++)
			{
				var index = deckRng.Next(available.Count);
				picked.Add(available[index]);
				available.RemoveAt(index);
			}
			lockedDeckUnitIds = picked.ToArray();
		}
		else
		{
			lockedDeckUnitIds = Array.Empty<string>();
		}

		return new DailyChallenge(seed, boardLabel, stageIndex, lockedSquad, date, lockedDeckUnitIds);
	}

	public bool HasCompletedDailyChallenge()
	{
		var daily = GetDailyChallenge();
		return !string.IsNullOrEmpty(LastDailyDate) &&
		       LastDailyDate.Equals(daily.Date, StringComparison.Ordinal);
	}

	public void MarkDailyChallengeCompleted(int score = 0)
	{
		LastDailyDate = GetDailyChallenge().Date;
		_dailyStreak++;
		Tomes += 1;
		AddSeasonXP(SeasonPassCatalog.XPPerDailyChallenge);
		Persist();
		CheckAchievements();
		SubmitDailyChallengeToServer(LastDailyDate, score);
	}

	// ── Cash shop / purchase system ─────────────────────────────────────

	public bool HasPurchasedProduct(string productId)
	{
		return !string.IsNullOrWhiteSpace(productId) && _purchasedProductIds.Contains(productId);
	}

	public void SetPurchaseValidationEndpoint(string endpoint)
	{
		_purchaseValidationEndpoint = endpoint?.Trim() ?? "";
		Persist();
	}

	public bool TryApplyPurchaseReward(PurchaseValidationResult result)
	{
		if (result == null || result.Status != "ok")
		{
			return false;
		}

		if (result.GoldCredited > 0)
		{
			Gold += result.GoldCredited;
		}

		if (result.FoodCredited > 0)
		{
			Food += result.FoodCredited;
		}

		if (!string.IsNullOrWhiteSpace(result.ProductId))
		{
			_purchasedProductIds.Add(result.ProductId);
		}

		_totalPurchaseCount++;

		if (result.GrantedUnitUnlock)
		{
			GrantRandomUnitUnlock();
		}

		Persist();
		CheckAchievements();
		return true;
	}

	private void GrantRandomUnitUnlock()
	{
		var candidates = new List<string>();
		foreach (var unit in GameData.GetPlayerUnits())
		{
			if (!_ownedPlayerUnitIds.Contains(unit.Id) && unit.UnlockStage <= HighestUnlockedStage)
			{
				candidates.Add(unit.Id);
			}
		}

		if (candidates.Count == 0) return;

		var index = _rng.RandiRange(0, candidates.Count - 1);
		_ownedPlayerUnitIds.Add(candidates[index]);
	}

	public PurchaseValidationResult ValidatePurchaseWithServer(string productId, string platform, string receiptToken, string transactionId)
	{
		if (string.IsNullOrWhiteSpace(_purchaseValidationEndpoint))
		{
			return new PurchaseValidationResult
			{
				Status = "error",
				Message = "Purchase validation endpoint not configured."
			};
		}

		try
		{
			var provider = new HttpApiPurchaseValidationProvider(_purchaseValidationEndpoint);
			var request = new PurchaseValidationRequest
			{
				PlayerProfileId = PlayerProfileId,
				ProductId = productId,
				Platform = platform,
				ReceiptToken = receiptToken,
				TransactionId = transactionId,
				RequestedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
			};

			return provider.ValidatePurchase(request);
		}
		catch (Exception e)
		{
			return new PurchaseValidationResult
			{
				Status = "error",
				Message = $"Validation failed: {e.Message}"
			};
		}
	}

	// ── Auto cloud backup ───────────────────────────────────────────────

	private void TryAutoCloudBackup()
	{
		if (string.IsNullOrWhiteSpace(_purchaseValidationEndpoint))
		{
			return;
		}

		try
		{
			CloudSaveService.Upload(out _);
		}
		catch
		{
			// Silent — auto backup should never block gameplay
		}
	}

	// ── Achievement system ──────────────────────────────────────────────

	public bool IsAchievementUnlocked(string id)
	{
		return !string.IsNullOrWhiteSpace(id) && _unlockedAchievementIds.Contains(id);
	}

	public bool TryUnlockAchievement(string id)
	{
		if (string.IsNullOrWhiteSpace(id))
		{
			return false;
		}

		if (!_unlockedAchievementIds.Add(id))
		{
			return false;
		}

		var definition = AchievementCatalog.GetById(id);
		_lastAchievementNotification = definition != null
			? $"Achievement unlocked: {definition.Title}!"
			: $"Achievement unlocked: {id}!";
		AudioDirector.Instance?.PlayAchievementUnlock();
		Persist();
		ScheduleAchievementSync();
		return true;
	}

	public int GetUnlockedAchievementCount()
	{
		return _unlockedAchievementIds.Count;
	}

	public string ConsumeAchievementNotification()
	{
		var notification = _lastAchievementNotification;
		_lastAchievementNotification = "";
		return notification ?? "";
	}

	private void ScheduleAchievementSync()
	{
		_achievementSyncPending = true;
		var now = Godot.Time.GetTicksMsec() / 1000.0;
		if (now - _lastAchievementSyncTime >= 30.0)
		{
			FlushAchievementSync();
		}
	}

	public void FlushAchievementSync()
	{
		if (!_achievementSyncPending)
		{
			return;
		}

		_achievementSyncPending = false;
		_lastAchievementSyncTime = Godot.Time.GetTicksMsec() / 1000.0;
		SyncAchievementsToServer();
	}

	public void SyncAchievementsToServer()
	{
		if (_unlockedAchievementIds.Count == 0)
		{
			return;
		}

		var providerId = ChallengeSyncProviderCatalog.NormalizeId(ChallengeSyncProviderId);
		if (providerId != ChallengeSyncProviderCatalog.HttpApiId)
		{
			return;
		}

		var endpointUrl = BuildAchievementSyncEndpoint(ChallengeSyncEndpoint);
		if (string.IsNullOrWhiteSpace(endpointUrl))
		{
			return;
		}

		try
		{
			var provider = new HttpApiAchievementSyncProvider(endpointUrl);
			provider.SyncAchievements(PlayerProfileId, _unlockedAchievementIds.ToArray());
		}
		catch (Exception ex)
		{
			GD.PrintErr($"Achievement sync failed: {ex.Message}");
		}
	}

	private void SubmitDailyChallengeToServer(string date, int score)
	{
		var providerId = ChallengeSyncProviderCatalog.NormalizeId(ChallengeSyncProviderId);
		if (providerId != ChallengeSyncProviderCatalog.HttpApiId)
		{
			return;
		}

		var baseUrl = BuildDailyBaseUrl(ChallengeSyncEndpoint);
		if (string.IsNullOrWhiteSpace(baseUrl))
		{
			return;
		}

		try
		{
			var provider = new HttpApiDailyLeaderboardProvider(baseUrl);
			provider.SubmitCompletion(PlayerProfileId, date, score);
		}
		catch (Exception ex)
		{
			GD.PrintErr($"Daily challenge submit failed: {ex.Message}");
		}
	}

	private static string BuildAchievementSyncEndpoint(string syncEndpoint)
	{
		var normalized = string.IsNullOrWhiteSpace(syncEndpoint) ? "" : syncEndpoint.Trim();
		if (string.IsNullOrWhiteSpace(normalized))
		{
			return "";
		}

		if (normalized.EndsWith("/challenge-sync", StringComparison.OrdinalIgnoreCase))
		{
			return normalized[..^"/challenge-sync".Length] + "/achievements/sync";
		}

		return normalized.TrimEnd('/') + "/achievements/sync";
	}

	private static string BuildDailyBaseUrl(string syncEndpoint)
	{
		var normalized = string.IsNullOrWhiteSpace(syncEndpoint) ? "" : syncEndpoint.Trim();
		if (string.IsNullOrWhiteSpace(normalized))
		{
			return "";
		}

		if (normalized.EndsWith("/challenge-sync", StringComparison.OrdinalIgnoreCase))
		{
			return normalized[..^"/challenge-sync".Length];
		}

		return normalized.TrimEnd('/');
	}

	// ── Prestige color selections ──────────────────────────────────────

	public int GetUnitPrestigeIndex(string unitId)
	{
		if (string.IsNullOrWhiteSpace(unitId))
		{
			return 0;
		}

		return _unitPrestigeSelections.TryGetValue(unitId, out var index) ? index : 0;
	}

	public void SetUnitPrestigeIndex(string unitId, int index)
	{
		if (string.IsNullOrWhiteSpace(unitId))
		{
			return;
		}

		if (index <= 0)
		{
			_unitPrestigeSelections.Remove(unitId);
		}
		else
		{
			_unitPrestigeSelections[unitId] = Math.Clamp(index, 1, PrestigeColorCatalog.MaxPrestigeIndex);
		}
	}

	public void CheckAchievements()
	{
		// Campaign
		if (HighestUnlockedStage > 1)
		{
			TryUnlockAchievement("first_blood");
		}

		foreach (var district in CampaignPlanCatalog.GetAll())
		{
			if (IsDistrictCleared(district.Id))
			{
				TryUnlockAchievement("district_clear");
				break;
			}
		}

		var allDistrictsCleared = true;
		foreach (var district in CampaignPlanCatalog.GetAll())
		{
			if (!IsDistrictCleared(district.Id))
			{
				allDistrictsCleared = false;
				break;
			}
		}
		if (allDistrictsCleared && CampaignPlanCatalog.GetAll().Count > 0)
		{
			TryUnlockAchievement("campaign_complete");
		}

		var threeStarCount = 0;
		for (var i = 1; i <= MaxStage; i++)
		{
			if (GetStageStars(i) >= 3)
			{
				threeStarCount++;
			}
		}
		if (threeStarCount >= 25)
		{
			TryUnlockAchievement("all_stars");
		}

		if (_claimedCampaignDirectiveIds.Count >= 10)
		{
			TryUnlockAchievement("heroic_clear");
		}

		// Combat - boss_slayer and boss_hunter checked via stage clears
		var bossesDefeated = 0;
		foreach (var district in CampaignPlanCatalog.GetAll())
		{
			var stages = GameData.GetStagesForMap(district.Id);
			if (stages.Count > 0)
			{
				var bossStageNumber = stages[stages.Count - 1].StageNumber;
				if (GetStageStars(bossStageNumber) > 0)
				{
					bossesDefeated++;
				}
			}
		}
		if (bossesDefeated > 0)
		{
			TryUnlockAchievement("boss_slayer");
		}
		if (bossesDefeated >= 10)
		{
			TryUnlockAchievement("boss_hunter");
		}

		// Endless
		if (BestEndlessWave >= 30)
		{
			TryUnlockAchievement("endless_30");
		}
		if (BestEndlessWave >= 60)
		{
			TryUnlockAchievement("endless_60");
		}
		if (BestEndlessWave >= 90)
		{
			TryUnlockAchievement("endless_90");
		}

		// Collection
		if (_ownedEquipmentIds.Count >= 6)
		{
			TryUnlockAchievement("relic_collector");
		}
		if (_ownedEquipmentIds.Count >= 12)
		{
			TryUnlockAchievement("full_armory");
		}
		if (_ownedPlayerUnitIds.Count >= GameData.PlayerRosterIds.Length)
		{
			TryUnlockAchievement("full_roster");
		}

		// Mastery
		foreach (var pair in _unitUpgradeLevels)
		{
			if (pair.Value >= MaxPlayerUnitLevel)
			{
				TryUnlockAchievement("max_unit");
				break;
			}
		}
		if (_ownedPlayerSpellIds.Count >= GameData.PlayerSpellIds.Length)
		{
			TryUnlockAchievement("all_spells");
		}
		if (_dailyStreak >= 7)
		{
			TryUnlockAchievement("daily_streak");
		}

		// New systems
		if (_promotedUnitIds.Count > 0)
		{
			TryUnlockAchievement("first_promotion");
		}
		if (TotalExpeditionsCompleted >= 10)
		{
			TryUnlockAchievement("expedition_10");
		}

		// Check event completion
		foreach (var evt in SeasonalEventCatalog.GetAll())
		{
			if (GetEventProgress(evt.Id) >= evt.Stages.Length)
			{
				TryUnlockAchievement("event_complete");
				break;
			}
		}

		// Codex
		if (_discoveredCodexIds.Count >= 10)
		{
			TryUnlockAchievement("codex_10");
		}
		if (_discoveredCodexIds.Count >= CodexCatalog.TotalEntries)
		{
			TryUnlockAchievement("codex_complete");
		}

		// Guild contribution
		if (GuildContributionPoints >= 100)
		{
			TryUnlockAchievement("guild_contributor");
		}

		// Tower
		if (TowerHighestFloor >= 25) TryUnlockAchievement("tower_25");
		if (TowerHighestFloor >= 50) TryUnlockAchievement("tower_50");
		if (TowerHighestFloor >= 100) TryUnlockAchievement("tower_100");

		// Collection milestones — check if all 100% milestones claimed
		var all100Claimed = true;
		foreach (var m in CollectionMilestoneCatalog.GetAll())
		{
			if (m.ThresholdPercent == 100 && !_claimedCollectionMilestoneIds.Contains(m.Id))
			{
				all100Claimed = false;
				break;
			}
		}
		if (all100Claimed && CollectionMilestoneCatalog.GetAll().Count > 0)
		{
			TryUnlockAchievement("collector_complete");
		}

		// Hard mode
		if (HardModeClearedCount >= 10)
		{
			TryUnlockAchievement("hard_mode_10");
		}
		if (HardModeClearedCount >= MaxStage)
		{
			TryUnlockAchievement("hard_mode_complete");
		}
	}

	public void CheckCombatAchievements(float busHealthRatio, float elapsedSeconds, int distinctComboPairsTriggered, int endlessBossCheckpointsCleared)
	{
		if (busHealthRatio >= 1f)
		{
			TryUnlockAchievement("no_damage");
		}
		if (elapsedSeconds > 0f && elapsedSeconds < 60f)
		{
			TryUnlockAchievement("speed_clear");
		}
		if (distinctComboPairsTriggered >= ComboPairCatalog.GetAll().Count)
		{
			TryUnlockAchievement("combo_master");
		}
		if (endlessBossCheckpointsCleared >= 5)
		{
			TryUnlockAchievement("endless_boss");
		}
	}
}

public readonly struct DailyChallenge
{
	public int Seed { get; }
	public string BoardLabel { get; }
	public int StageIndex { get; }
	public bool LockedSquad { get; }
	public string Date { get; }
	public string[] LockedDeckUnitIds { get; }

	public DailyChallenge(int seed, string boardLabel, int stageIndex, bool lockedSquad, string date, string[] lockedDeckUnitIds)
	{
		Seed = seed;
		BoardLabel = boardLabel;
		StageIndex = stageIndex;
		LockedSquad = lockedSquad;
		Date = date;
		LockedDeckUnitIds = lockedDeckUnitIds ?? Array.Empty<string>();
	}
}
