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

public sealed class GameSaveData
{
    public int Version { get; set; } = 22;
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
}
