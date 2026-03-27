using System.Collections.Generic;

public sealed class ChallengeDeploymentRecord
{
    public string UnitId { get; set; } = "";
    public float TimeSeconds { get; set; }
    public int LanePercent { get; set; }
}

public sealed class ChallengeRunRecord
{
    public string Code { get; set; } = "";
    public int Stage { get; set; } = 1;
    public string MutatorId { get; set; } = AsyncChallengeCatalog.PressureSpikeId;
    public int Score { get; set; }
    public bool Won { get; set; }
    public bool Retreated { get; set; }
    public float ElapsedSeconds { get; set; }
    public int EnemyDefeats { get; set; }
    public int StarsEarned { get; set; }
    public int CompletionBonus { get; set; }
    public int StarBonus { get; set; }
    public int KillBonus { get; set; }
    public int HullBonus { get; set; }
    public int TimeBonus { get; set; }
    public int DeployPenalty { get; set; }
    public int RawScore { get; set; }
    public float ScoreMultiplier { get; set; } = 1f;
    public bool UsedLockedDeck { get; set; }
    public string[] DeckUnitIds { get; set; } = [];
    public int PlayerDeployments { get; set; }
    public float BusHullRatio { get; set; }
    public List<ChallengeDeploymentRecord> Deployments { get; set; } = [];
    public long PlayedAtUnixSeconds { get; set; }
}

public sealed class EndlessRunRecord
{
    public int Wave { get; set; }
    public float TimeSeconds { get; set; }
    public string RouteId { get; set; } = "";
    public string BoonId { get; set; } = "";
    public int GoldEarned { get; set; }
    public int FoodEarned { get; set; }
    public string Date { get; set; } = "";
    public string DifficultyId { get; set; } = "normal";
}

public sealed class ExpeditionSlotSaveData
{
    public string ExpeditionId { get; set; } = "";
    public string[] AssignedUnitIds { get; set; } = System.Array.Empty<string>();
    public long StartedAtUnixSeconds { get; set; }
}

public sealed class GameSaveData
{
    public int Version { get; set; } = 39;
    public int Gold { get; set; } = 120;
    public int Food { get; set; } = 12;
    public int Scrap { get => Gold; set => Gold = value; }
    public int Fuel { get => Food; set => Food = value; }
    public int HighestUnlockedStage { get; set; } = 1;
    public int SelectedStage { get; set; } = 1;
    public string SelectedEndlessRouteId { get; set; } = "city";
    public string SelectedEndlessBoonId { get; set; } = EndlessBoonCatalog.SurplusCourageId;
    public string LastResultMessage { get; set; } = "Pick a district and clear the route.";
    public string PlayerCallsign { get; set; } = "Lantern";
    public string PlayerProfileId { get; set; } = "";
    public string PlayerAuthToken { get; set; } = "";
    public long LastPlayerProfileSyncAtUnixSeconds { get; set; }
    public string ChallengeSyncProviderId { get; set; } = ChallengeSyncProviderCatalog.LocalJournalId;
    public string ChallengeSyncEndpoint { get; set; } = "";
    public bool ChallengeSyncAutoFlush { get; set; }
    public bool ShowDevUi { get; set; } = true;
    public bool ShowFpsCounter { get; set; } = true;
    public bool AudioMuted { get; set; }
    public int EffectsVolumePercent { get; set; } = 85;
    public int AmbienceVolumePercent { get; set; } = 65;
    public string[] ActiveDeckUnitIds { get; set; } = [];
    public string[] ActiveDeckSpellIds { get; set; } = [];
    public string[] OwnedPlayerUnitIds { get; set; } = [];
    public string[] OwnedPlayerSpellIds { get; set; } = [];
    public int[] StageStars { get; set; } = [];
    public Dictionary<string, int> UnitLevels { get; set; } = new();
    public Dictionary<string, int> SpellLevels { get; set; } = new();
    public Dictionary<string, int> BaseUpgradeLevels { get; set; } = new();
    public int BestEndlessWave { get; set; }
    public float BestEndlessTimeSeconds { get; set; }
    public int EndlessRuns { get; set; }
    public string SelectedAsyncChallengeCode { get; set; } = "CH-01-PRS-1001";
    public string[] SelectedAsyncChallengeLockedDeckUnitIds { get; set; } = [];
    public Dictionary<string, int> ChallengeBestScores { get; set; } = new();
    public int ChallengeRuns { get; set; }
    public List<ChallengeRunRecord> ChallengeHistory { get; set; } = [];
    public List<ChallengeSubmissionEnvelope> PendingChallengeSubmissions { get; set; } = [];
    public long LastChallengeSyncAtUnixSeconds { get; set; }
    public int TotalChallengeSubmissionsSynced { get; set; }
    public string[] PinnedChallengeCodes { get; set; } = [];
    public string[] ClaimedDistrictRewardIds { get; set; } = [];
    public Dictionary<string, string> UnitDoctrineIds { get; set; } = new();
    public List<EndlessRunRecord> EndlessRunHistory { get; set; } = [];
    public int ArmedCampaignDirectiveStage { get; set; }
    public string[] ClaimedCampaignDirectiveIds { get; set; } = [];
    public string[] OwnedEquipmentIds { get; set; } = [];
    public Dictionary<string, string> UnitEquipmentSlots { get; set; } = new();
    public string LastDailyDate { get; set; } = "";
    public string DifficultyId { get; set; } = "normal";
    public bool ShowHints { get; set; } = true;
    public string[] SeenHintIds { get; set; } = [];
    public int DailyStreak { get; set; }
    public string[] UnlockedAchievementIds { get; set; } = [];
    public Dictionary<string, int> UnitPrestigeSelections { get; set; } = new();
    public int MusicVolumePercent { get; set; } = 50;
    public string Language { get; set; } = "en";
    public bool AnalyticsConsent { get; set; }
    public bool HasShownConsentPrompt { get; set; }
    public int FontSizeOffset { get; set; }
    public bool HighContrast { get; set; }
    public int PrestigeLevel { get; set; }
    public int PrestigeTotalGoldEarned { get; set; }
    public int PrestigeTotalStagesCleared { get; set; }
    public int BestBossRushWave { get; set; }
    public float BestBossRushTimeSeconds { get; set; }
    public int BossRushRuns { get; set; }
    public string[] PurchasedProductIds { get; set; } = [];
    public int TotalPurchaseCount { get; set; }
    public string PurchaseValidationEndpoint { get; set; } = "";

    // v32: Relic Forge
    public int RelicShards { get; set; }

    // v32: Unit Promotion
    public int Sigils { get; set; }
    public string[] PromotedUnitIds { get; set; } = [];
    public Dictionary<string, string> UnitEquipmentSlot2 { get; set; } = new();

    // v32: Expeditions
    public List<ExpeditionSlotSaveData> ActiveExpeditions { get; set; } = [];
    public int TotalExpeditionsCompleted { get; set; }

    // v32: Seasonal Events
    public Dictionary<string, int> EventStagesCleared { get; set; } = new();
    public string[] ClaimedEventRewardIds { get; set; } = [];

    // v33: Codex
    public string[] DiscoveredCodexIds { get; set; } = [];
    public Dictionary<string, int> CodexKillCounts { get; set; } = new();
    public Dictionary<string, long> CodexFirstSeenAt { get; set; } = new();

    // v33: Skill Trees
    public int Tomes { get; set; }
    public Dictionary<string, string[]> UnlockedSkillNodeIds { get; set; } = new();

    // v33: PvP Arena
    public int ArenaRating { get; set; } = 1000;
    public int ArenaWins { get; set; }
    public int ArenaLosses { get; set; }

    // v33: Guild
    public string GuildId { get; set; } = "";
    public int GuildContributionPoints { get; set; }

    // v34: Hard Mode
    public int[] HardModeStars { get; set; } = [];
    public int HardModeHighestCleared { get; set; }

    // v34: Enchantments
    public int Essence { get; set; }
    public Dictionary<string, string> RelicEnchantments { get; set; } = new();

    // v34: Weekly Raid
    public string LastRaidWeek { get; set; } = "";
    public int RaidDamageContributed { get; set; }
    public string[] ClaimedRaidRewardIds { get; set; } = [];

    // v35: Bounty Board
    public string[] CompletedBountyIds { get; set; } = [];
    public Dictionary<string, int> BountyProgress { get; set; } = new();
    public string LastBountyDate { get; set; } = "";

    // v35: Challenge Tower
    public int TowerHighestFloor { get; set; }
    public int[] TowerFloorStars { get; set; } = [];

    // v35: Friends
    public string[] FriendIds { get; set; } = [];
    public string LastGiftSentDate { get; set; } = "";
    public int GiftsSentToday { get; set; }

    // v35: Mastery
    public Dictionary<string, int> UnitMasteryXP { get; set; } = new();

    // v36: Achievement Rewards
    public string[] ClaimedAchievementRewardIds { get; set; } = [];

    // v36: Login Calendar
    public int LoginCalendarDay { get; set; }
    public string LastLoginCalendarDate { get; set; } = "";
    public string LoginCalendarMonth { get; set; } = "";

    // v36: War Wagon Cosmetics
    public string SelectedWagonSkinId { get; set; } = "skin_default";

    // v37: Unit Awakening
    public Dictionary<string, int> UnitStarLevels { get; set; } = new();
    public Dictionary<string, int> UnitTokens { get; set; } = new();

    // v37: Season Pass
    public int SeasonPassXP { get; set; }
    public int SeasonPassTier { get; set; }
    public string SeasonId { get; set; } = "S1";
    public bool HasPremiumPass { get; set; }
    public int[] ClaimedSeasonFreeTiers { get; set; } = [];
    public int[] ClaimedSeasonPremiumTiers { get; set; } = [];

    // v37: Collection Milestones
    public string[] ClaimedCollectionMilestoneIds { get; set; } = [];

    // v38: Battle Mutators
    public string[] ActiveMutatorIds { get; set; } = [];
    public int MutatorBattlesCompleted { get; set; }

    // v38: Accessibility
    public string ColorblindMode { get; set; } = "none";
    public bool ReducedMotion { get; set; }
    public bool AutoBattleEnabled { get; set; }
    public bool LargeTextMode { get; set; }

    // v39: Campaign Momentum
    public int CampaignMomentumStacks { get; set; }
}
