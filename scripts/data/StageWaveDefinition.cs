using System;

public sealed class StageWaveDefinition
{
    public string Label { get; set; } = "";
    public float TriggerTime { get; set; }
    public float SpawnInterval { get; set; } = 0.45f;
    public string Area { get; set; } = "";
    public float AdvanceTriggerXRatio { get; set; } = -1f;
    public float SpawnXRatio { get; set; } = 1f;
    public StageWaveEntryDefinition[] Entries { get; set; } = Array.Empty<StageWaveEntryDefinition>();
}

public sealed class StageWaveEntryDefinition
{
    public string UnitId { get; set; } = "";
    public int Count { get; set; } = 1;
}
