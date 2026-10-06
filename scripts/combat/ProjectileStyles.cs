using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;

public enum ProjectileShape { Sprite, Orb, Lightning }
// Path: turned along the flight path (shafts). Spin: tumbles end over end (thrown pots and cogs).
// Upright: stays upright, mirrored to face the way it flies and tilted a little with the arc (skulls, globs).
public enum ProjectileOrient { Path, Spin, Upright }
public enum ProjectileTrail { None, Streak, Embers, Smoke, Glow, Sparkle, Drips, Frost, Sparks }
public enum ProjectileImpact { Pierce, Heavy, Shatter, Splash, Burst, Lightning, Soul, Frost, Fire, Metal }
public enum ProjectileLaunch { None, Flash, Smoke, Dust }

/// <summary>How one kind of shot looks and moves. Sizes are battle-world pixels (a soldier stands about
/// 30 tall). Sprites come from art/remaster/build_projectiles.py; orbs and lightning are drawn from the
/// particle textures with additive glow.</summary>
public sealed record ProjectileStyle(
    string Id,
    ProjectileShape Shape = ProjectileShape.Sprite,
    string Sprite = "",
    float Length = 16f,
    ProjectileOrient Orient = ProjectileOrient.Path,
    float Arc = 0f,
    float ArcMax = 0f,
    float Spin = 0f,
    float Wobble = 0f,
    string Glow = "",
    float GlowRadius = 0f,
    string Core = "",
    ProjectileTrail Trail = ProjectileTrail.None,
    string TrailColor = "",
    ProjectileImpact Impact = ProjectileImpact.Burst,
    ProjectileLaunch Launch = ProjectileLaunch.None,
    bool Sticks = false,
    string Tint = "",
    float MaxSpeed = 0f)
{
    public Color GlowColor => Glow.Length > 0 ? new Color(Glow) : Colors.Transparent;
    public Color CoreColor => Core.Length > 0 ? new Color(Core) : Colors.White;
    public Color TrailTint => TrailColor.Length > 0 ? new Color(TrailColor) : GlowColor;
    public Color SpriteTint => Tint.Length > 0 ? new Color(Tint) : Colors.White;
    public bool Lobbed => Arc >= 0.1f;
}

public sealed record ProjectileSpriteMeta(Vector2 Tip, Vector2 Centre);

public static class ProjectileStyles
{
    private const string SpritePath = "res://assets/projectiles/";

    public static readonly ProjectileStyle Arrow = new("arrow", Sprite: "arrow", Length: 20, Arc: .14f, ArcMax: 26,
        Trail: ProjectileTrail.Streak, TrailColor: "f1e6cf", Impact: ProjectileImpact.Pierce, Sticks: true);
    public static readonly ProjectileStyle Bolt = new("bolt", Sprite: "bolt", Length: 12, Arc: .05f, ArcMax: 10,
        Trail: ProjectileTrail.Streak, TrailColor: "e3d8c0", Impact: ProjectileImpact.Pierce, Sticks: true);
    public static readonly ProjectileStyle BallistaBolt = new("ballista_bolt", Sprite: "ballista_bolt", Length: 32, Arc: .06f,
        ArcMax: 16, Trail: ProjectileTrail.Streak, TrailColor: "efe3c8", Impact: ProjectileImpact.Heavy,
        Launch: ProjectileLaunch.Dust, MaxSpeed: 620);
    public static readonly ProjectileStyle BoneBolt = new("bone_bolt", Sprite: "bone_bolt", Length: 30, Arc: .06f, ArcMax: 16,
        Glow: "6ff0d2", GlowRadius: 5, Trail: ProjectileTrail.Glow, TrailColor: "6ff0d2", Impact: ProjectileImpact.Heavy,
        Launch: ProjectileLaunch.Dust);
    public static readonly ProjectileStyle Harpoon = new("harpoon", Sprite: "harpoon", Length: 32, Arc: .07f, ArcMax: 18,
        Trail: ProjectileTrail.Streak, TrailColor: "cfe6ee", Impact: ProjectileImpact.Heavy, Launch: ProjectileLaunch.Dust,
        MaxSpeed: 620);
    public static readonly ProjectileStyle Flask = new("flask", Sprite: "flask", Length: 7, Orient: ProjectileOrient.Spin,
        Arc: .32f, ArcMax: 58, Spin: 13, Glow: "ff9a3c", GlowRadius: 9, Trail: ProjectileTrail.Embers, TrailColor: "ff9a3c",
        Impact: ProjectileImpact.Shatter);
    public static readonly ProjectileStyle PlaguePot = new("plague_pot", Sprite: "plague_pot", Length: 10,
        Orient: ProjectileOrient.Spin, Arc: .34f, ArcMax: 70, Spin: 6, Glow: "9cf25c", GlowRadius: 9,
        Trail: ProjectileTrail.Smoke, TrailColor: "8fbf5a", Impact: ProjectileImpact.Splash, Launch: ProjectileLaunch.Smoke);
    public static readonly ProjectileStyle Firepot = new("firepot", Sprite: "firepot", Length: 9, Orient: ProjectileOrient.Spin,
        Arc: .3f, ArcMax: 60, Spin: 8, Glow: "ff8a3c", GlowRadius: 10, Trail: ProjectileTrail.Embers, TrailColor: "ff8a3c",
        Impact: ProjectileImpact.Fire, Launch: ProjectileLaunch.Smoke);
    public static readonly ProjectileStyle Cog = new("cog", Sprite: "cog", Length: 8, Orient: ProjectileOrient.Spin, Arc: .16f,
        ArcMax: 26, Spin: 18, Trail: ProjectileTrail.Sparks, TrailColor: "ffd27a", Impact: ProjectileImpact.Metal);
    public static readonly ProjectileStyle BlightGlob = new("blight_glob", Sprite: "blight_glob", Length: 13,
        Orient: ProjectileOrient.Upright, Arc: .12f, ArcMax: 22, Wobble: 1.2f, Glow: "9cf25c", GlowRadius: 7,
        Trail: ProjectileTrail.Drips, TrailColor: "8fcf4a", Impact: ProjectileImpact.Splash, Launch: ProjectileLaunch.Flash);
    public static readonly ProjectileStyle SoulSkull = new("soul_skull", Sprite: "skull", Length: 10,
        Orient: ProjectileOrient.Upright, Wobble: 2.5f, Glow: "7dffbf", GlowRadius: 13, Trail: ProjectileTrail.Glow,
        TrailColor: "7dffbf", Impact: ProjectileImpact.Soul, Launch: ProjectileLaunch.Flash, Tint: "d9ffe9");
    public static readonly ProjectileStyle NecroticSkull = new("necrotic_skull", Sprite: "skull", Length: 11,
        Orient: ProjectileOrient.Upright, Wobble: 2.5f, Glow: "b07cff", GlowRadius: 14, Trail: ProjectileTrail.Glow,
        TrailColor: "a070ff", Impact: ProjectileImpact.Soul, Launch: ProjectileLaunch.Flash, Tint: "e6dcff");
    public static readonly ProjectileStyle Arcane = new("arcane", ProjectileShape.Orb, Length: 5, Wobble: 1.5f, Glow: "7cc8ff",
        GlowRadius: 12, Core: "f2fbff", Trail: ProjectileTrail.Sparkle, TrailColor: "9fd8ff", Launch: ProjectileLaunch.Flash);
    public static readonly ProjectileStyle Holy = new("holy", ProjectileShape.Orb, Length: 5, Wobble: 1f, Glow: "ffd36b",
        GlowRadius: 12, Core: "fffbe8", Trail: ProjectileTrail.Sparkle, TrailColor: "ffe39a", Launch: ProjectileLaunch.Flash);
    public static readonly ProjectileStyle Hex = new("hex", ProjectileShape.Orb, Length: 5, Wobble: 2.5f, Glow: "b67cff",
        GlowRadius: 12, Core: "c8ff9a", Trail: ProjectileTrail.Glow, TrailColor: "8f5ad8", Launch: ProjectileLaunch.Flash);
    public static readonly ProjectileStyle Water = new("water", ProjectileShape.Orb, Length: 6, Wobble: 1.5f, Glow: "4fd2e6",
        GlowRadius: 11, Core: "e6fdff", Trail: ProjectileTrail.Drips, TrailColor: "7fe0ef", Impact: ProjectileImpact.Splash,
        Launch: ProjectileLaunch.Flash);
    public static readonly ProjectileStyle Relic = new("relic", ProjectileShape.Orb, Length: 7, Wobble: 1f, Glow: "ffb84a",
        GlowRadius: 14, Core: "fff4d0", Trail: ProjectileTrail.Embers, TrailColor: "ffc65a", Impact: ProjectileImpact.Fire,
        Launch: ProjectileLaunch.Flash);
    public static readonly ProjectileStyle Storm = new("storm", ProjectileShape.Lightning, Length: 5, Glow: "9fd8ff",
        GlowRadius: 11, Core: "ffffff", Trail: ProjectileTrail.Sparkle, TrailColor: "cfeaff", Impact: ProjectileImpact.Lightning,
        Launch: ProjectileLaunch.Flash);
    public static readonly ProjectileStyle Default = new("orb", ProjectileShape.Orb, Length: 4, Glow: "ffe6b0", GlowRadius: 8,
        Trail: ProjectileTrail.Glow, TrailColor: "ffe6b0");

    private static readonly Dictionary<string, ProjectileStyle> ByUnit = new()
    {
        ["player_shooter"] = Arrow,
        ["player_ranger"] = Bolt,
        ["player_mechanic"] = Cog,
        ["player_marksman"] = Arcane,
        ["player_grenadier"] = Flask,
        ["player_coordinator"] = Holy,
        ["player_necromancer"] = SoulSkull,
        ["player_ballista"] = BallistaBolt,
        ["player_stormcaller"] = Storm,
        ["enemy_spitter"] = BlightGlob,
        ["enemy_lich"] = NecroticSkull,
        ["enemy_boneballista"] = BoneBolt,
        ["enemy_plague_engine"] = PlaguePot,
        ["enemy_boss_docks"] = Water,
        ["enemy_boss_verge"] = Hex,
        ["enemy_boss_reliquary"] = Relic,
        ["enemy_boss_tidemaster"] = Harpoon,
    };

    public static ProjectileStyle ForUnit(string definitionId, string motionProfile)
    {
        if (definitionId != null && ByUnit.TryGetValue(definitionId, out var style)) return style;
        return motionProfile switch
        {
            "bow-draw" => Arrow,
            "crossbow" => Bolt,
            "ballista" => BallistaBolt,
            "bombard" => PlaguePot,
            "flask-toss" => Flask,
            "hammer-command" => Cog,
            "staff-cast" => Arcane,
            _ => Default
        };
    }

    /// <summary>A wagon weapon's shot.</summary>
    public static ProjectileStyle ForBaseWeapon(BaseWeaponKind kind) => kind switch
    {
        BaseWeaponKind.Arrows => Arrow,
        BaseWeaponKind.Ballista => BallistaBolt,
        BaseWeaponKind.Firepot => Firepot,
        _ => Default
    };

    private static readonly Dictionary<string, Texture2D> Textures = new();
    private static Dictionary<string, ProjectileSpriteMeta> _meta;

    public static Texture2D Texture(string sprite)
    {
        if (string.IsNullOrEmpty(sprite)) return null;
        if (Textures.TryGetValue(sprite, out var cached)) return cached;
        var path = $"{SpritePath}{sprite}.png";
        var texture = ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
        Textures[sprite] = texture;
        return texture;
    }

    /// <summary>Normalised tip and centre of a sprite (projectiles.json); a shaft's tip is its front.</summary>
    public static ProjectileSpriteMeta Meta(string sprite)
    {
        if (_meta == null)
        {
            _meta = new();
            try
            {
                using var file = FileAccess.Open($"{SpritePath}projectiles.json", FileAccess.ModeFlags.Read);
                if (file != null)
                {
                    using var doc = JsonDocument.Parse(file.GetAsText());
                    foreach (var entry in doc.RootElement.EnumerateObject())
                    {
                        var tip = entry.Value.GetProperty("tip");
                        var centre = entry.Value.GetProperty("centre");
                        _meta[entry.Name] = new ProjectileSpriteMeta(new Vector2(tip[0].GetSingle(), tip[1].GetSingle()),
                            new Vector2(centre[0].GetSingle(), centre[1].GetSingle()));
                    }
                }
            }
            catch (Exception)
            {
                // Missing or broken metadata (bad JSON, a missing key, a non-number): shafts fall back to a tip at
                // their front edge.
            }
        }
        return _meta.TryGetValue(sprite ?? "", out var meta) ? meta : new ProjectileSpriteMeta(new Vector2(.95f, .5f), new Vector2(.5f, .5f));
    }
}
