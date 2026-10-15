using Godot;

// Light around a unit while it channels an ability: a soft glow gathering at its hands and chest, motes drawn in
// toward it, and a flare as the cast goes off. It follows the caster and ends early if the caster dies.
public partial class BattleCastGlow : Node2D
{
    private Unit _unit;
    private uint _life;
    private Color _tint;
    private float _age, _total;
    private CpuParticles2D _motes;
    private bool _flared;

    public static BattleCastGlow Spawn(Node parent, Unit unit, Color tint, float seconds)
    {
        if (GameState.Instance?.ReducedMotion ?? false) return null;
        var g = new BattleCastGlow { _unit = unit, _life = unit.CombatLifetime, _tint = tint, _total = Mathf.Max(.2f, seconds) };
        parent.AddChild(g);
        return g;
    }

    public override void _Ready()
    {
        ZAsRelative = false; ZIndex = 30;
        Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
        _motes = new CpuParticles2D
        {
            Amount = 14, Lifetime = .6f, Emitting = true, LocalCoords = true,
            EmissionShape = CpuParticles2D.EmissionShapeEnum.Sphere, EmissionSphereRadius = 22f,
            Gravity = Vector2.Zero, RadialAccelMin = -90f, RadialAccelMax = -60f, InitialVelocityMin = 0, InitialVelocityMax = 6,
            ScaleAmountMin = .05f, ScaleAmountMax = .12f,
            Texture = ParticleTextureLoader.TryLoad("particle_soft") ?? ParticleTextureLoader.SoftTexture,
            Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
            ColorRamp = new Gradient { Offsets = new[] { 0f, .3f, 1f },
                Colors = new[] { new Color(_tint, 0f), new Color(_tint.Lightened(.3f), .9f), new Color(_tint, 0f) } }
        };
        AddChild(_motes);
    }

    private Vector2 Chest => _unit.BodyContactPosition;

    public override void _Process(double delta)
    {
        _age += (float)delta;
        var alive = IsInstanceValid(_unit) && !_unit.IsDead && _unit.CombatLifetime == _life;
        if (alive) GlobalPosition = Chest;
        if (!_flared && (_age >= _total || !alive))
        {
            _flared = true;
            _motes.Emitting = false;
            if (alive) BattleParticles.SpawnImpactSparks(GetParent(), Chest, _tint, 16f);
        }
        if (_age >= _total + .35f) { QueueFree(); return; }
        QueueRedraw();
    }

    public override void _Draw()
    {
        var charge = Mathf.Clamp(_age / _total, 0, 1);
        var flare = _age > _total ? 1 - (_age - _total) / .35f : 0f;
        var r = 10f + 8f * charge + 14f * flare;
        var a = .18f + .25f * charge + .5f * flare;
        var pulse = .85f + .15f * Mathf.Sin(_age * 14f);
        DrawCircle(Vector2.Zero, r * 1.6f * pulse, new Color(_tint, a * .25f));
        DrawCircle(Vector2.Zero, r * pulse, new Color(_tint, a * .5f));
        DrawCircle(Vector2.Zero, r * .45f * pulse, new Color(_tint.Lightened(.5f), a * .7f));
    }
}
