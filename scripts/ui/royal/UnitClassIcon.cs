/// <summary>The class medallion drawn on a unit's card: ranged, shield or melee. One mapping for every screen that
/// shows units (the armory, battle prep, Endless, Multiplayer Challenges).</summary>
public static class UnitClassIcon
{
    public static string For(UnitDefinition unit) => unit.Id switch
    {
        "player_shooter" or "player_ranger" or "player_marksman" or "player_ballista" => "class-ranged",
        "player_defender" or "player_lantern_guard" or "player_banner" => "class-shield",
        _ when unit.UsesProjectile => "class-ranged",
        _ => "class-melee"
    };
}
