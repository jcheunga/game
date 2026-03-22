using System;
using System.Collections.Generic;

public sealed class ComboPairDefinition
{
    public string Id;
    public string UnitIdA;
    public string UnitIdB;
    public float ProximityRadius;
    public float HealthScaleA, DamageScaleA, SpeedScaleA;
    public float HealthScaleB, DamageScaleB, SpeedScaleB;
    public string Title;
}

public static class ComboPairCatalog
{
    private static readonly ComboPairDefinition[] Definitions =
    {
        new()
        {
            Id = "phalanx",
            UnitIdA = "player_defender",
            UnitIdB = "player_spear",
            ProximityRadius = 80f,
            HealthScaleA = 1.15f, DamageScaleA = 1.10f, SpeedScaleA = 1f,
            HealthScaleB = 1.15f, DamageScaleB = 1.10f, SpeedScaleB = 1f,
            Title = "Phalanx"
        },
        new()
        {
            Id = "hunter_pack",
            UnitIdA = "player_hound",
            UnitIdB = "player_hound",
            ProximityRadius = 60f,
            HealthScaleA = 1f, DamageScaleA = 1.12f, SpeedScaleA = 1.20f,
            HealthScaleB = 1f, DamageScaleB = 1.12f, SpeedScaleB = 1.20f,
            Title = "Hunter Pack"
        },
        new()
        {
            Id = "arcane_guard",
            UnitIdA = "player_marksman",
            UnitIdB = "player_defender",
            ProximityRadius = 100f,
            HealthScaleA = 1f, DamageScaleA = 1.20f, SpeedScaleA = 1f,
            HealthScaleB = 1.15f, DamageScaleB = 1f, SpeedScaleB = 1f,
            Title = "Arcane Guard"
        },
        new()
        {
            Id = "skirmish_line",
            UnitIdA = "player_raider",
            UnitIdB = "player_rogue",
            ProximityRadius = 90f,
            HealthScaleA = 1f, DamageScaleA = 1.10f, SpeedScaleA = 1.15f,
            HealthScaleB = 1f, DamageScaleB = 1.10f, SpeedScaleB = 1.15f,
            Title = "Skirmish Line"
        },
        new()
        {
            Id = "siege_corps",
            UnitIdA = "player_breacher",
            UnitIdB = "player_grenadier",
            ProximityRadius = 80f,
            HealthScaleA = 1f, DamageScaleA = 1.12f, SpeedScaleA = 1f,
            HealthScaleB = 1f, DamageScaleB = 1.12f, SpeedScaleB = 1f,
            Title = "Siege Corps"
        },
        new()
        {
            Id = "holy_order",
            UnitIdA = "player_coordinator",
            UnitIdB = "player_banner",
            ProximityRadius = 100f,
            HealthScaleA = 1f, DamageScaleA = 1.08f, SpeedScaleA = 1.06f,
            HealthScaleB = 1f, DamageScaleB = 1.08f, SpeedScaleB = 1.06f,
            Title = "Holy Order"
        },
        new()
        {
            Id = "dark_pact",
            UnitIdA = "player_necromancer",
            UnitIdB = "player_berserker",
            ProximityRadius = 90f,
            HealthScaleA = 1.10f, DamageScaleA = 1.15f, SpeedScaleA = 1f,
            HealthScaleB = 1f, DamageScaleB = 1.18f, SpeedScaleB = 1.08f,
            Title = "Dark Pact"
        },
        new()
        {
            Id = "fire_support",
            UnitIdA = "player_shooter",
            UnitIdB = "player_mechanic",
            ProximityRadius = 110f,
            HealthScaleA = 1f, DamageScaleA = 1.14f, SpeedScaleA = 1f,
            HealthScaleB = 1.10f, DamageScaleB = 1f, SpeedScaleB = 1f,
            Title = "Fire Support"
        },
        new()
        {
            Id = "shadow_strike",
            UnitIdA = "player_rogue",
            UnitIdB = "player_necromancer",
            ProximityRadius = 80f,
            HealthScaleA = 1f, DamageScaleA = 1.16f, SpeedScaleA = 1.10f,
            HealthScaleB = 1f, DamageScaleB = 1.10f, SpeedScaleB = 1f,
            Title = "Shadow Strike"
        },
        new()
        {
            Id = "vanguard_charge",
            UnitIdA = "player_brawler",
            UnitIdB = "player_raider",
            ProximityRadius = 70f,
            HealthScaleA = 1.10f, DamageScaleA = 1.12f, SpeedScaleA = 1f,
            HealthScaleB = 1f, DamageScaleB = 1.10f, SpeedScaleB = 1.12f,
            Title = "Vanguard Charge"
        }
    };

    public static IReadOnlyList<ComboPairDefinition> GetAll() => Definitions;
}
