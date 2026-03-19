using System;
using Godot;

public sealed class EquipmentDefinition
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public string Rarity { get; set; } = "common";
    public float HealthScale { get; set; } = 1f;
    public float DamageScale { get; set; } = 1f;
    public float CooldownReduction { get; set; }
    public float SpeedScale { get; set; } = 1f;
    public int BaseDamageBonus { get; set; }
}
