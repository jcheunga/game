using System;

public sealed class UnitActiveAbilityDefinition
{
    public UnitActiveAbilityDefinition(
        string id,
        string unitId,
        string title,
        string description,
        float cooldownSeconds,
        int unlockLevel)
    {
        Id = id;
        UnitId = unitId;
        Title = title;
        Description = description;
        CooldownSeconds = cooldownSeconds;
        UnlockLevel = unlockLevel;
    }

    public string Id { get; }
    public string UnitId { get; }
    public string Title { get; }
    public string Description { get; }
    public float CooldownSeconds { get; }
    public int UnlockLevel { get; }
}

public static class UnitActiveAbilityCatalog
{
    private static readonly UnitActiveAbilityDefinition[] Definitions =
    {
        new(
            "swordsman_cleave",
            "player_brawler",
            "Cleave",
            "Deal 150% damage in a small AoE around the unit.",
            8f,
            4),
        new(
            "archer_volley",
            "player_shooter",
            "Arrow Volley",
            "Fire 3 projectiles at nearby enemies.",
            10f,
            4),
        new(
            "shield_knight_wall",
            "player_defender",
            "Shield Wall",
            "Reduce all damage taken by 60% for 4s.",
            12f,
            4),
        new(
            "spearman_thrust",
            "player_spear",
            "Piercing Thrust",
            "Deal 200% damage to the current target, ignoring defense.",
            9f,
            4),
        new(
            "crossbow_snipe",
            "player_ranger",
            "Snipe",
            "Deal 300% damage to the farthest enemy in range.",
            14f,
            4),
        new(
            "cavalry_charge",
            "player_raider",
            "Charge",
            "Dash forward dealing damage to all enemies in a line.",
            10f,
            4),
        new(
            "engineer_turret",
            "player_mechanic",
            "Deploy Turret",
            "Spawn a temporary stationary turret unit.",
            18f,
            4),
        new(
            "mage_beam",
            "player_marksman",
            "Arcane Beam",
            "Channel 400% damage split across 2 targets.",
            16f,
            4),
        new(
            "halberdier_sweep",
            "player_breacher",
            "Sweeping Strike",
            "Deal 120% damage in a wide arc.",
            9f,
            4),
        new(
            "alchemist_bomb",
            "player_grenadier",
            "Volatile Flask",
            "Throw a bomb dealing 200% splash damage.",
            12f,
            4),
        new(
            "monk_blessing",
            "player_coordinator",
            "Blessing",
            "Heal all allies in aura range by 15 HP.",
            14f,
            4),
        new(
            "hound_howl",
            "player_hound",
            "Pack Howl",
            "Boost own attack speed by 50% for 5s.",
            8f,
            4),
        new(
            "banner_inspire",
            "player_banner",
            "Inspire",
            "Double aura effect for 6s.",
            16f,
            4),
        new(
            "necro_mass_raise",
            "player_necromancer",
            "Mass Raise",
            "Spawn 3 skeletons at once.",
            20f,
            4),
        new(
            "rogue_vanish",
            "player_rogue",
            "Vanish",
            "Become untargetable for 3s, next attack deals 250% damage.",
            12f,
            4),
        new(
            "berserker_frenzy",
            "player_berserker",
            "Blood Frenzy",
            "Gain 40% attack speed for 6s but take 20% more damage.",
            10f,
            4),
        new(
            "lantern_guard_bulwark",
            "player_lantern_guard",
            "Bulwark",
            "Reduce all damage taken by 60% for 4s.",
            12f,
            4),
        new(
            "ballista_anchor_shot",
            "player_ballista",
            "Anchor Shot",
            "Deal 300% damage to the farthest enemy in range.",
            15f,
            4),
        new(
            "stormcaller_overcharge",
            "player_stormcaller",
            "Overcharge",
            "Channel 400% damage split across 2 targets.",
            16f,
            4)
    };

    public static UnitActiveAbilityDefinition GetForUnit(string unitId)
    {
        if (string.IsNullOrWhiteSpace(unitId))
        {
            return null;
        }

        for (var i = 0; i < Definitions.Length; i++)
        {
            if (Definitions[i].UnitId.Equals(unitId.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return Definitions[i];
            }
        }

        return null;
    }

    public static UnitActiveAbilityDefinition GetOrNull(string abilityId)
    {
        if (string.IsNullOrWhiteSpace(abilityId))
        {
            return null;
        }

        for (var i = 0; i < Definitions.Length; i++)
        {
            if (Definitions[i].Id.Equals(abilityId.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return Definitions[i];
            }
        }

        return null;
    }
}
