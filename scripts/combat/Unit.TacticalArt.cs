using Godot;

/// <summary>
/// Placed tactics and the stone barricade stand on the road as their painted pictures (art/royal/items.py)
/// rather than as troop sprites: they pop into place, cast a contact shadow and, if they give light, glow.
/// </summary>
public partial class Unit
{
    private Texture2D _tacticalArt;
    private string _tacticalArtFor;
    private TacticalGround _tacticalGround;

    private Texture2D TacticalArt
    {
        get
        {
            if (!IsTacticalObject && DefinitionId != "barricade_wall") return null;
            // Keyed by definition: pooled units come back as other tactics.
            if (_tacticalArtFor == DefinitionId) return _tacticalArt;
            _tacticalArtFor = DefinitionId;
            var path = $"res://assets/ui/royal/items/{(DefinitionId == "barricade_wall" ? "spell_stone_barricade" : DefinitionId)}.png";
            return _tacticalArt = ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
        }
    }

    /// <summary>A tactic's rune circle, drawn by a child on the ground layer so it lies under every troop.</summary>
    private sealed partial class TacticalGround : Node2D
    {
        public Unit Host;
        public override void _Process(double delta) => QueueRedraw();
        public override void _Draw() => Host?.DrawTacticalGround(this);
    }

    private float TacticalWidth => DefinitionId switch
    {
        "barricade_wall" => 74f, "tactic_palisade_kit" => 70f, "tactic_healing_shrine" => 72f, "tactic_grounding_rod" => 76f,
        "tactic_purifying_censer" => 68f, "tactic_bolt_turret" => 60f, _ => 50f,
    };

    /// <summary>The rune circle a working tactic stands on, turning slowly.</summary>
    private string TacticalSigil => DefinitionId switch
    {
        "tactic_healing_shrine" => "nature", "tactic_purifying_censer" => "holy", "tactic_grounding_rod" => "storm", _ => "",
    };

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
        var beat = .32f + .12f * Mathf.Sin(ContentAge * 3.4f);
        if (TacticalSigil.Length > 0 && _tacticalGround == null)
        {
            _tacticalGround = new TacticalGround { Host = this, ZIndex = SpellVfx.GroundLayer, ZAsRelative = false };
            CallDeferred(Node.MethodName.AddChild, _tacticalGround);
        }
        if (glow.A > 0)
            DrawTextureRect(SpellVfx.Glow, new Rect2(-width * .75f, -width * 1.15f, width * 1.5f, width * 1.4f), false, new Color(glow, beat));
        DrawTextureRect(art, new Rect2(-width * .5f, -width * .96f, width, width), false, Modulate);
        if (DefinitionId is "tactic_healing_shrine" or "tactic_purifying_censer") DrawRisingMotes(width, glow);
        if (DefinitionId == "tactic_grounding_rod") DrawRodCrackle(width, glow);
        return true;
    }

    private void DrawTacticalGround(CanvasItem canvas)
    {
        if (IsDead || TacticalArt == null || TacticalSigil.Length == 0) return;
        // Ground plane, turning; drawn twice so the light lines hold up on a sunlit road.
        var pop = Mathf.Clamp(ContentAge / .35f, 0, 1);
        var reach = TacticalWidth * .8f * (1 - Mathf.Pow(1 - pop, 3));
        var glow = TacticalGlow;
        var beat = .32f + .12f * Mathf.Sin(ContentAge * 3.4f);
        canvas.DrawSetTransformMatrix(Transform2D.Identity.Scaled(new Vector2(1, BattleGroundPlane.DepthScale)) * new Transform2D(ContentAge * .5f, Vector2.Zero));
        canvas.DrawTextureRect(SpellVfx.Glow, new Rect2(-Vector2.One * reach * 1.3f, Vector2.One * reach * 2.6f), false, new Color(glow, beat));
        for (var pass = 0; pass < 2; pass++)
            canvas.DrawTextureRect(SpellVfx.Tex("sigil_" + TacticalSigil), new Rect2(-Vector2.One * reach, Vector2.One * reach * 2), false,
                new Color(glow.Lightened(.35f), .5f + beat * .4f));
        canvas.DrawSetTransform(Vector2.Zero);
    }

    /// <summary>Soft lights drifting up from a shrine or censer, looping.</summary>
    private void DrawRisingMotes(float width, Color glow)
    {
        for (var i = 0; i < 6; i++)
        {
            var phase = Mathf.PosMod(ContentAge * .45f + i / 6f, 1f);
            var at = new Vector2(Mathf.Sin(i * 2.3f + ContentAge * 1.3f) * width * .32f, -width * .45f - phase * width * 1.1f);
            var size = width * (.16f - phase * .07f);
            DrawTextureRect(SpellVfx.Glow, new Rect2(at - Vector2.One * size / 2, Vector2.One * size), false,
                new Color(glow.Lightened(.45f), Mathf.Sin(phase * Mathf.Pi) * .9f));
        }
    }

    /// <summary>Small arcs crackling off the grounding rod's tip, re-forking a dozen times a second.</summary>
    private void DrawRodCrackle(float width, Color glow)
    {
        var tip = new Vector2(width * .04f, -width * .86f);
        var still = GameState.Instance?.ReducedMotion ?? false;
        var flicker = still ? .8f : .65f + .35f * Mathf.Sin(ContentAge * 41f);
        DrawTextureRect(SpellVfx.Glow, new Rect2(tip - Vector2.One * width * .3f, Vector2.One * width * .6f), false, new Color(glow.Lightened(.4f), .55f * flicker));
        var rng = new RandomNumberGenerator { Seed = (ulong)(ContentAge * 12f) + GetInstanceId() };
        for (var arc = 0; arc < 3; arc++)
        {
            var points = new Vector2[5];
            var direction = Vector2.Up.Rotated(rng.RandfRange(-2.2f, 2.2f));
            var length = width * rng.RandfRange(.18f, .34f);
            for (var i = 0; i < points.Length; i++)
                points[i] = tip + direction * length * i / (points.Length - 1) + direction.Orthogonal() * rng.RandfRange(-4f, 4f) * (i > 0 ? 1 : 0);
            DrawPolyline(points, new Color(glow, .5f * flicker), 3f, true);
            DrawPolyline(points, new Color(1, 1, 1, .9f * flicker), 1.2f, true);
        }
    }
}
