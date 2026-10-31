using System;
using System.Collections.Generic;
using Godot;

public enum CombatDamageType { Physical, Magical, True }

// All permanent sources join this budget; none gets its own multiplier ladder.
public sealed class UnitStatBonuses
{
    public float Health, Damage, Speed, Range, Structure, AttackInterval;
    public float Armor, PhysicalResistance, MagicalResistance, CriticalChance, CriticalDamage;
    public float AuraRange;

    public UnitStatBonuses Copy() => (UnitStatBonuses)MemberwiseClone();

    public IReadOnlyList<UnitBonusLimit> GetLimitNotices()
    {
        var applied = Copy();
        applied.ApplyCaps();
        var limits = new List<UnitBonusLimit>();
        void Add(string label, float requested, float effective, bool points = false)
        {
            if (Math.Abs(requested-effective) > .0001f)
                limits.Add(new UnitBonusLimit(label, points ? requested : requested*100f,
                    points ? effective : effective*100f, points ? "points" : "%"));
        }
        Add("Health",Health,applied.Health);
        Add("Damage",Damage,applied.Damage);
        Add("Speed",Speed,applied.Speed);
        Add("Reach",Range,applied.Range);
        Add("Structure damage",Structure,applied.Structure);
        Add("Attack interval reduction",AttackInterval,applied.AttackInterval);
        Add("Armor",Armor,applied.Armor,true);
        Add("Physical resistance",PhysicalResistance,applied.PhysicalResistance,true);
        Add("Magic resistance",MagicalResistance,applied.MagicalResistance,true);
        Add("Critical chance",CriticalChance,applied.CriticalChance,true);
        Add("Critical bonus",CriticalDamage,applied.CriticalDamage,true);
        Add("Aura reach",AuraRange,applied.AuraRange);
        return limits;
    }

    public void AddScales(float health = 1f, float damage = 1f, float speed = 1f)
    {
        Health += health - 1f;
        Damage += damage - 1f;
        Speed += speed - 1f;
    }

    public void AddEquipment(EquipmentDefinition item, UnitDefinition unit)
    {
        if (item == null) return;
        AddScales(item.HealthScale, item.DamageScale, item.SpeedScale);
        Range += item.RangeScale - 1f;
        Structure += item.StructureDamageScale - 1f;
        if (unit.BaseDamage > 0) Structure += item.BaseDamageBonus / (float)unit.BaseDamage;
        AttackInterval += item.AttackIntervalReductionPct / 100f;
        if (unit.AttackCooldown > 0) AttackInterval += item.CooldownReduction / unit.AttackCooldown;
        Armor += item.ArmorBonus;
        PhysicalResistance += item.PhysicalResistanceBonus;
        MagicalResistance += item.MagicalResistanceBonus;
        CriticalChance += item.CriticalChanceBonus;
        CriticalDamage += item.CriticalDamageBonus;
        if(item.Id=="relic_banner_ribbon") AuraRange += .05f;
    }

    public void ApplyCaps()
    {
        Health = Mathf.Clamp(Health, -.1f, .1f);
        Damage = Mathf.Clamp(Damage, -.1f, .1f);
        Speed = Mathf.Clamp(Speed, -.1f, .1f);
        Range = Mathf.Clamp(Range, -.1f, .1f);
        Structure = Mathf.Clamp(Structure, -.1f, .1f);
        AttackInterval = Mathf.Clamp(AttackInterval, 0f, .05f);
        Armor = Mathf.Clamp(Armor, 0f, 10f);
        PhysicalResistance = Mathf.Clamp(PhysicalResistance, 0f, .5f);
        MagicalResistance = Mathf.Clamp(MagicalResistance, 0f, 8f);
        CriticalChance = Mathf.Clamp(CriticalChance, 0f, 5f);
        CriticalDamage = Mathf.Clamp(CriticalDamage, 0f, 10f);
        AuraRange = Mathf.Clamp(AuraRange, 0f, .05f);
    }
}

public sealed record UnitBonusLimit(string Label,float Supplied,float Applied,string Unit)
{
    public float Excess => Supplied-Applied;
}

public static class UnitBalance
{
    public const int MaxPlayerLevel = 20;
    public static float HealthScale(int level) => 1f + Math.Max(0, level - 1) * .015f;
    public static float DamageScale(int level) => 1f + Math.Max(0, level - 1) * .01f;
    public static float PhysicalResistance(float armor, float points = 0f)
        => Mathf.Clamp(100f * Math.Max(0f, armor) / (100f + Math.Max(0f, armor)) + points, 0f, 75f);
    public static CombatDamageType ParseDamageType(string type) => type?.ToLowerInvariant() switch
    {
        "magic" or "magical" or "m" => CombatDamageType.Magical,
        "true" => CombatDamageType.True,
        _ => CombatDamageType.Physical
    };
}
