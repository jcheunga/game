using System;

public sealed class StageWaveDefinition
{
    public string Label { get; set; } = "";
    public float TriggerTime { get; set; }
    public float SpawnInterval { get; set; } = 0.45f;
    public StageWaveEntryDefinition[] Entries { get; set; } = Array.Empty<StageWaveEntryDefinition>();
}

public sealed class StageWaveEntryDefinition
{
    public string UnitId { get; set; } = "";
    public int Count { get; set; } = 1;
}
