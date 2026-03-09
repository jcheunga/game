using System.Collections.Generic;

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
    public long PlayedAtUnixSeconds { get; set; }
}

public sealed class GameSaveData
{
    public int Version { get; set; } = 10;
    public int Gold { get; set; } = 120;
    public int Food { get; set; } = 12;
    public int Scrap { get => Gold; set => Gold = value; }
    public int Fuel { get => Food; set => Food = value; }
    public int HighestUnlockedStage { get; set; } = 1;
    public int SelectedStage { get; set; } = 1;
    public string SelectedEndlessRouteId { get; set; } = "city";
    public string SelectedEndlessBoonId { get; set; } = EndlessBoonCatalog.SurplusCourageId;
    public string LastResultMessage { get; set; } = "Pick a district and clear the route.";
    public bool ShowDevUi { get; set; } = true;
    public bool ShowFpsCounter { get; set; } = true;
    public string[] ActiveDeckUnitIds { get; set; } = [];
    public string[] OwnedPlayerUnitIds { get; set; } = [];
    public int[] StageStars { get; set; } = [];
    public Dictionary<string, int> UnitLevels { get; set; } = new();
    public Dictionary<string, int> BaseUpgradeLevels { get; set; } = new();
    public int BestEndlessWave { get; set; }
    public float BestEndlessTimeSeconds { get; set; }
    public int EndlessRuns { get; set; }
    public string SelectedAsyncChallengeCode { get; set; } = "CH-01-PRS-1001";
    public Dictionary<string, int> ChallengeBestScores { get; set; } = new();
    public int ChallengeRuns { get; set; }
    public List<ChallengeRunRecord> ChallengeHistory { get; set; } = [];
}
