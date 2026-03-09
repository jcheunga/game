using System;
using Godot;

public sealed class UnitDefinition
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Side { get; set; } = "Enemy";
    public int UnlockStage { get; set; } = 1;
    public int GoldCost { get; set; }
    public int Cost { get; set; }
    public float MaxHealth { get; set; } = 50f;
    public float Speed { get; set; } = 60f;
    public float AttackDamage { get; set; } = 10f;
    public float AttackRange { get; set; } = 30f;
    public float AttackCooldown { get; set; } = 1f;
    public bool UsesProjectile { get; set; }
    public float ProjectileSpeed { get; set; } = 420f;
    public float AggroRangeX { get; set; } = 220f;
    public float AggroRangeY { get; set; } = 96f;
    public int BaseDamage { get; set; } = 12;
    public float DeployCooldown { get; set; } = 8f;
    public float DamageTakenScale { get; set; } = 1f;
    public float DeathBurstDamage { get; set; }
    public float DeathBurstRadius { get; set; }
    public string SpawnOnDeathUnitId { get; set; } = "";
    public int SpawnOnDeathCount { get; set; }
    public string VisualClass { get; set; } = "";
    public float VisualScale { get; set; }
    public string ColorHex { get; set; } = "ffffff";

    public bool IsPlayerSide => Side.Equals("Player", StringComparison.OrdinalIgnoreCase);

    public Color GetTint()
    {
        if (string.IsNullOrWhiteSpace(ColorHex))
        {
            return Colors.White;
        }

        return new Color(ColorHex);
    }
}
