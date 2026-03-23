using System;
using System.Collections.Generic;

public sealed class UnitPromotionDefinition
{
    public string BaseUnitId { get; }
    public string PromotedTitle { get; }
    public float HealthScale { get; }
    public float DamageScale { get; }
    public float SpeedScale { get; }
    public int GoldCost { get; }
    public int SigilCost { get; }
    public string GlowColorHex { get; }

    public UnitPromotionDefinition(string baseUnitId, string promotedTitle,
        float healthScale, float damageScale, float speedScale,
        int goldCost, int sigilCost, string glowColorHex)
    {
        BaseUnitId = baseUnitId;
        PromotedTitle = promotedTitle;
        HealthScale = healthScale;
        DamageScale = damageScale;
        SpeedScale = speedScale;
        GoldCost = goldCost;
        SigilCost = sigilCost;
        GlowColorHex = glowColorHex;
    }
}

public static class UnitPromotionCatalog
{
    public const int RequiredLevel = 5;

    private static readonly UnitPromotionDefinition[] Definitions =
    {
        new("player_brawler", "Knight", 1.15f, 1.10f, 1.0f, 1500, 3, "ffd700"),
        new("player_spear", "Lancer", 1.10f, 1.12f, 1.05f, 1500, 3, "c0c0ff"),
        new("player_defender", "Bastion", 1.20f, 1.05f, 1.0f, 1500, 3, "6090d0"),
        new("player_shooter", "Sharpshooter", 1.08f, 1.15f, 1.0f, 1800, 4, "40d040"),
        new("player_ranger", "Deadeye", 1.08f, 1.18f, 1.0f, 1800, 4, "d04040"),
        new("player_raider", "Charger", 1.12f, 1.10f, 1.08f, 1800, 4, "d0a030"),
        new("player_breacher", "Warden", 1.12f, 1.14f, 1.0f, 2000, 4, "b060b0"),
        new("player_coordinator", "Chaplain", 1.18f, 1.08f, 1.0f, 1500, 3, "f0f080"),
        new("player_marksman", "Archmage", 1.10f, 1.18f, 1.0f, 2200, 5, "8040ff"),
        new("player_mechanic", "Artificer", 1.14f, 1.12f, 1.0f, 2000, 4, "c08040"),
        new("player_grenadier", "Pyromancer", 1.08f, 1.16f, 1.0f, 2000, 4, "ff6020"),
        new("player_hound", "Dire Wolf", 1.15f, 1.12f, 1.10f, 1200, 2, "808080"),
        new("player_banner", "Marshal", 1.14f, 1.08f, 1.0f, 1800, 4, "ffa000"),
        new("player_necromancer", "Lich Lord", 1.10f, 1.20f, 1.0f, 2200, 5, "60ff80"),
        new("player_rogue", "Shadow Blade", 1.08f, 1.20f, 1.08f, 2000, 4, "303030"),
        new("player_berserker", "Warlord", 1.15f, 1.18f, 1.05f, 2200, 5, "cc0000"),
    };

    private static readonly Dictionary<string, UnitPromotionDefinition> ById;

    static UnitPromotionCatalog()
    {
        ById = new Dictionary<string, UnitPromotionDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var def in Definitions)
        {
            ById[def.BaseUnitId] = def;
        }
    }

    public static IReadOnlyList<UnitPromotionDefinition> GetAll() => Definitions;

    public static UnitPromotionDefinition TryGet(string baseUnitId)
    {
        return ById.TryGetValue(baseUnitId, out var def) ? def : null;
    }
}
