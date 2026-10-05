using Godot;

public enum BaseWeaponKind { Arrows, Ballista, Firepot, Frost, Hex }

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

    public static BaseWeaponDefinition Stronghold(string routeId) => routeId switch
    {
        RouteCatalog.HarborId => new("Harpoon ballista", BaseWeaponKind.Ballista, 14, 180, 4.5f, 290, new Color("9bdaf1")),
        RouteCatalog.FoundryId => new("Furnace firepots", BaseWeaponKind.Firepot, 8, 150, 5, 145, new Color("ff9955"), 30),
        RouteCatalog.CitadelId => new("Citadel ballista", BaseWeaponKind.Ballista, 16, 190, 4.5f, 300, new Color("e4b96b")),
        RouteCatalog.ThornwallId => new("Frost sentries", BaseWeaponKind.Frost, 8, 165, 3.5f, 215, new Color("a4e7ef")),
        RouteCatalog.SteppeId => new("Raider archers", BaseWeaponKind.Arrows, 7, 165, 2.8f, 260, new Color("f4a261")),
        RouteCatalog.QuarantineId or RouteCatalog.MireId or RouteCatalog.BasilicaId or RouteCatalog.GloamwoodId =>
            new("Hex sentries", BaseWeaponKind.Hex, 9, 160, 4, 175, new Color("ca9ee6"), 21),
        _ => new("Castle archers", BaseWeaponKind.Arrows, 7, 155, 3, 245, new Color("f2be94"))
    };

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
