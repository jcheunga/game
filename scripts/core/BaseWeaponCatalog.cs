using Godot;

public enum BaseWeaponKind { Arrows, Ballista, Firepot }

/// <summary>A weapon mounted on the war wagon. The enemy stronghold has none: it never fires at troops.</summary>
public sealed record BaseWeaponDefinition(string Title, BaseWeaponKind Kind, float Damage,
    float Range, float Cooldown, float Speed, Color Color, float SplashRadius = 0f);

public static class BaseWeaponCatalog
{
    public static BaseWeaponDefinition Wagon(string upgradeId, int level)
    {
        level = Mathf.Clamp(level, 0, 5);
        return upgradeId switch
        {
            BaseUpgradeCatalog.ArcherCrewId => new("Wagon archers", BaseWeaponKind.Arrows,
                10 + 3 * level, 160 + 6 * level, 2.4f, 260, new Color("eed49a")),
            BaseUpgradeCatalog.BallistaId when level > 0 => new("Wagon ballista", BaseWeaponKind.Ballista,
                25 + 7 * level, 200 + 5 * level, 4.5f, 310, new Color("b4d9df")),
            BaseUpgradeCatalog.FirepotId when level > 0 => new("Wagon firepot", BaseWeaponKind.Firepot,
                10 + 4 * level, 155 + 5 * level, 5f, 155, new Color("ff9955"), 33),
            _ => null
        };
    }

    public static float ArmorScale(int level) => 1f - Mathf.Clamp(level, 0, 5) * 0.06f;
    public static float VolleyCooldown(int level) => 18f - Mathf.Clamp(level, 0, 5);
    public static float RepairRatio(int level) => level <= 0 ? 0 : 0.10f + Mathf.Clamp(level, 0, 5) * 0.03f;

    public static string UpgradeEffect(string id, int level)
    {
        var weapon = Wagon(id, level);
        if (weapon != null)
            return $"{weapon.Damage:0} damage · {weapon.Range:0} range · fires every {weapon.Cooldown:0.#}s" +
                (weapon.SplashRadius > 0 ? $" · {weapon.SplashRadius:0} blast radius" : "");
        return id switch
        {
            BaseUpgradeCatalog.BallistaId or BaseUpgradeCatalog.FirepotId => "Not installed",
            BaseUpgradeCatalog.ArrowVolleyId => level == 0 ? "Not learned" : $"Up to 3 arrows · {VolleyCooldown(level):0}s recovery",
            BaseUpgradeCatalog.EmergencyRepairId => level == 0 ? "Not learned" : $"Restore {RepairRatio(level) * 100:0}% hull · once per battle at 40% hull",
            BaseUpgradeCatalog.ReinforcedArmorId => $"{(1f - ArmorScale(level)) * 100:0}% less damage from attacks against the wagon",
            _ => null
        };
    }
}
