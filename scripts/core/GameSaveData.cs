using System.Collections.Generic;

public sealed class GameSaveData
{
    public int Version { get; set; } = 7;
    public int Scrap { get; set; } = 120;
    public int Fuel { get; set; } = 10;
    public int HighestUnlockedStage { get; set; } = 1;
    public int SelectedStage { get; set; } = 1;
    public string SelectedEndlessRouteId { get; set; } = "city";
    public string SelectedEndlessBoonId { get; set; } = EndlessBoonCatalog.SurplusCourageId;
    public string LastResultMessage { get; set; } = "Pick a district and clear the route.";
    public bool ShowDevUi { get; set; } = true;
    public bool ShowFpsCounter { get; set; } = true;
    public string[] ActiveDeckUnitIds { get; set; } = [];
    public int[] StageStars { get; set; } = [];
    public Dictionary<string, int> UnitLevels { get; set; } = new();
    public int BestEndlessWave { get; set; }
    public float BestEndlessTimeSeconds { get; set; }
    public int EndlessRuns { get; set; }
}
