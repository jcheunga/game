using Godot;

/// <summary>
/// Placed tactics and the stone barricade stand on the road as their painted pictures (art/royal/items.py)
/// rather than as troop sprites: they pop into place, cast a contact shadow and, if they give light, glow.
/// </summary>
public partial class Unit
{
    private Texture2D _tacticalArt;
    private bool _tacticalArtResolved;

    private Texture2D TacticalArt
    {
        get
        {
            if (!IsTacticalObject && DefinitionId != "barricade_wall") return null;
            if (_tacticalArtResolved) return _tacticalArt;
            _tacticalArtResolved = true;
            var path = $"res://assets/ui/royal/items/{(DefinitionId == "barricade_wall" ? "spell_stone_barricade" : DefinitionId)}.png";
            return _tacticalArt = ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
        }
    }

    private float TacticalWidth => DefinitionId switch { "barricade_wall" => 74f, "tactic_palisade_kit" => 70f, "tactic_bolt_turret" => 60f, _ => 50f };

    private Color TacticalGlow => DefinitionId switch
    {
        "tactic_healing_shrine" => new Color(.5f, .92f, .5f),
        "tactic_decoy_lantern" => new Color(1f, .7f, .32f),
        "tactic_purifying_censer" => new Color(.7f, .88f, 1f),
        "tactic_grounding_rod" => new Color(.62f, .7f, 1f),
        _ => new Color(0, 0, 0, 0),
    };

    /// <summary>Draws a tactic's picture; false when this unit is a troop.</summary>
    private bool DrawTacticalObject()
    {
        var art = TacticalArt;
        if (art == null) return false;
        // A quick overshoot as it is set down.
        var pop = Mathf.Clamp(ContentAge / .28f, 0, 1);
        var settle = 1 + 2.7f * Mathf.Pow(pop - 1, 3) + 1.7f * Mathf.Pow(pop - 1, 2);
        var width = TacticalWidth * Mathf.Max(.2f, settle);
        var glow = TacticalGlow;
        if (glow.A > 0)
        {
            var beat = .32f + .12f * Mathf.Sin(ContentAge * 3.4f);
            DrawTextureRect(SpellVfx.Glow, new Rect2(-width * .75f, -width * 1.15f, width * 1.5f, width * 1.4f), false, new Color(glow, beat));
        }
        DrawTextureRect(art, new Rect2(-width * .5f, -width * .96f, width, width), false, Modulate);
        return true;
    }
}
