using System.Collections.Generic;

public sealed class GameSaveData
{
    public int Version { get; set; } = 8;
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
}
