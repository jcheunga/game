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
    public float ArmorBonus { get; set; }
    public float PhysicalResistanceBonus { get; set; }
    public float MagicalResistanceBonus { get; set; }
    public float CriticalChanceBonus { get; set; }
    public float CriticalDamageBonus { get; set; }
    public float AttackIntervalReductionPct { get; set; }
    public float RangeScale { get; set; } = 1f;
    public float StructureDamageScale { get; set; } = 1f;
    public string EffectId { get; set; } = "";
    public string Family { get; set; } = "";
    public int UnlockStage { get; set; } = 1;
}
