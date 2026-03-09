using System;
using Godot;

public sealed class StageDefinition
{
    public int StageNumber { get; set; } = 1;
    public string StageName { get; set; } = "";
    public string MapId { get; set; } = "city";
    public string MapName { get; set; } = "City Route";
    public string TerrainId { get; set; } = "urban";
    public string Description { get; set; } = "";
    public int RewardGold { get; set; } = 40;
    public int RewardFood { get; set; } = 2;
    public int EntryFoodCost { get; set; } = 1;
    public int ExploreFoodCost { get; set; } = 2;
    public int RewardScrap { get => RewardGold; set => RewardGold = value; }
    public float MapX { get; set; } = 120f;
    public float MapY { get; set; } = 220f;
    public float PlayerBaseHealth { get; set; } = 300f;
    public float EnemyBaseHealth { get; set; } = 280f;
    public float EnemySpawnMin { get; set; } = 2f;
    public float EnemySpawnMax { get; set; } = 3f;
    public float EnemyHealthScale { get; set; } = 1f;
    public float EnemyDamageScale { get; set; } = 1f;
    public float WalkerWeight { get; set; } = 1f;
    public float RunnerWeight { get; set; } = 0f;
    public float BruteWeight { get; set; } = 0f;
    public float SpitterWeight { get; set; } = 0f;
    public float CrusherWeight { get; set; } = 0f;
    public float BossWeight { get; set; } = 0f;
    public float BossSpawnStartTime { get; set; } = 0f;
    public float BonusWaveChance { get; set; }
    public float TwoStarBusHullRatio { get; set; } = 0.5f;
    public float ThreeStarTimeLimitSeconds { get; set; } = 90f;
    public StageHazardDefinition[] Hazards { get; set; } = Array.Empty<StageHazardDefinition>();
    public StageModifierDefinition[] Modifiers { get; set; } = Array.Empty<StageModifierDefinition>();
    public StageObjectiveDefinition[] Objectives { get; set; } = Array.Empty<StageObjectiveDefinition>();
    public StageWaveDefinition[] Waves { get; set; } = Array.Empty<StageWaveDefinition>();

    public Vector2 MapPoint => new(MapX, MapY);
    public bool HasScriptedWaves => Waves.Length > 0;
}
