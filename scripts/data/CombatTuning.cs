using System;

public sealed class CombatTuning
{
    public float PlayerBaseX { get; set; } = 96f;
    public float EnemyBaseX { get; set; } = 1184f;
    public float PlayerSpawnX { get; set; } = 140f;
    public float EnemySpawnX { get; set; } = 1140f;

    public float BattlefieldLeft { get; set; } = 84f;
    public float BattlefieldRight { get; set; } = 1196f;
    public float BattlefieldTop { get; set; } = 96f;
    public float BattlefieldBottom { get; set; } = 584f;
    public float SpawnVerticalPadding { get; set; } = 12f;

    public float BaseCoreRadius { get; set; } = 44f;
    public float BaseApproachDistance { get; set; } = 170f;

    public float CourageStart { get; set; } = 45f;
    public float CourageMax { get; set; } = 100f;
    public float CourageGainPerSecond { get; set; } = 4.5f;

    public float InitialEnemySpawnDelay { get; set; } = 2.8f;
    public float EnemySpawnPressureTimeScale { get; set; } = 180f;
    public float EnemySpawnPressureMin { get; set; } = 1f;
    public float EnemySpawnPressureMax { get; set; } = 1.25f;
    public float EnemySpawnIntervalFloor { get; set; } = 1.4f;

    public int[] MaxActiveEnemiesByStage { get; set; } = { 5, 7, 8, 9, 10, 11, 12, 13 };
    public int VictoryFoodReward { get; set; } = 2;
    public int VictoryFuelReward { get => VictoryFoodReward; set => VictoryFoodReward = value; }

    public int GetMaxActiveEnemies(int stage)
    {
        if (MaxActiveEnemiesByStage == null || MaxActiveEnemiesByStage.Length == 0)
        {
            return 10;
        }

        var index = Math.Clamp(stage - 1, 0, MaxActiveEnemiesByStage.Length - 1);
        return Math.Max(1, MaxActiveEnemiesByStage[index]);
    }

    public void Normalize()
    {
        if (BattlefieldRight <= BattlefieldLeft)
        {
            BattlefieldRight = BattlefieldLeft + 100f;
        }

        if (BattlefieldBottom <= BattlefieldTop)
        {
            BattlefieldBottom = BattlefieldTop + 100f;
        }

        if (SpawnVerticalPadding < 0f)
        {
            SpawnVerticalPadding = 0f;
        }

        if (EnemySpawnPressureTimeScale <= 0f)
        {
            EnemySpawnPressureTimeScale = 1f;
        }

        if (EnemySpawnPressureMax < EnemySpawnPressureMin)
        {
            EnemySpawnPressureMax = EnemySpawnPressureMin;
        }

        if (EnemySpawnIntervalFloor <= 0f)
        {
            EnemySpawnIntervalFloor = 0.1f;
        }
    }
}
