using System.Collections.Generic;
using System.Linq;
using Godot;

// Raise Dead: necromancers (the Gloamwood Witch above all, the Lich and the Reliquary Tyrant) raise the fallen
// where they lie. Recent deaths of both armies are remembered for a while; the caster sends a soul-wisp to each
// body near it, and the body gets back up (its own death played backwards) as an undead of the Rotbound Host,
// green-lit, weaker than it was in life. Caravan soldiers can be raised against their own side. With no bodies
// near, the dead claw up out of the ground instead (Risen). Bosses, engines and the already-raised stay down.
public partial class BattleController
{
    private sealed class Fallen
    {
        public string DefinitionId;
        public Team Team;
        public Vector2 Position;
        public float Facing;
        public float Time;
        public UnitDeathVisual Corpse;
    }

    private const float FallenMemorySeconds = 14f;
    private const float RaiseReach = 260f;
    private readonly List<Fallen> _fallen = new();

    private static bool CanBeRaised(Unit dead) => !dead.IsTacticalObject && !dead.NonCombatant && !dead.IsRaised
        && IsRaisableKind(dead.Definition);

    /// <summary>Flesh and bone get back up; bosses, engines and things that burst do not.</summary>
    private static bool IsRaisableKind(UnitDefinition def) => def != null && !def.IsBoss && def.VisualClass is not ("siegetower" or "boss")
        && def.Id is not ("player_ballista" or "enemy_boneballista" or "enemy_plague_engine" or "enemy_splitter");

    private void RecordFallen(Unit dead, UnitDeathVisual corpse)
    {
        _fallen.RemoveAll(f => _elapsed - f.Time > FallenMemorySeconds);
        if (!CanBeRaised(dead)) return;
        _fallen.Add(new Fallen { DefinitionId = dead.DefinitionId, Team = dead.Team, Position = dead.Position,
            Facing = dead.Team == Team.Player ? 1 : -1, Time = _elapsed, Corpse = corpse });
        if (_fallen.Count > 40) _fallen.RemoveAt(0);
    }

    /// <summary>Raises up to `count` of the dead near the caster (from their bodies, else from the ground).</summary>
    private int RaiseDeadAround(Unit caster, int count, bool respectCaps = true)
    {
        _fallen.RemoveAll(f => _elapsed - f.Time > FallenMemorySeconds);
        var bodies = _fallen.Where(f => f.Position.DistanceTo(caster.Position) <= RaiseReach)
            .OrderBy(f => f.Position.DistanceTo(caster.Position)).Take(count).ToList();
        var raised = 0;
        var delay = 0f;
        foreach (var body in bodies)
        {
            if (respectCaps && !RoomToRaise(caster)) break;
            _fallen.Remove(body);
            StartRaise(caster, body, delay);
            delay += .12f;
            raised++;
        }
        // nobody near enough: the dead come up out of the ground in front of the caster
        var fromGround = caster.SpecialSpawnUnitId is { Length: > 0 } id ? id : "enemy_walker";
        while (raised < count && (!respectCaps || CanAddBossReinforcement(caster, fromGround) && RoomToRaise(caster)))
        {
            var toward = caster.Team == Team.Enemy ? -1f : 1f;
            var at = new Vector2(
                Mathf.Clamp(caster.Position.X + toward * _rng.RandfRange(30f, 75f), BattlefieldLeft + 20f, BattlefieldRight - 20f),
                Mathf.Clamp(caster.Position.Y + _rng.RandfRange(-46f, 46f), BattlefieldTop + SpawnVerticalPadding, BattlefieldBottom - SpawnVerticalPadding));
            StartRaise(caster, new Fallen { DefinitionId = fromGround, Team = Team.Enemy, Position = at, Facing = -1, Time = _elapsed }, delay);
            delay += .12f;
            raised++;
        }
        return raised;
    }

    private int _raisesPending;

    private bool RoomToRaise(Unit caster)
    {
        if (CountTeamUnits(Team.Enemy) + _raisesPending >= _spawnDirector.GetMaxActiveEnemies()) return false;
        if (!CanAddCampaignPeriodicReinforcement(caster)) return false;
        RecordCampaignPeriodicReinforcement(caster);
        return true;
    }

    private void StartRaise(Unit caster, Fallen body, float delay)
    {
        _raisesPending++;
        var from = caster.WeaponContactPosition;
        var wisp = new RaiseWisp { Start = from, Target = body.Position + new Vector2(0, -10), Delay = delay,
            ShouldPause = () => _battlePaused || _endlessCheckpointActive };
        wisp.Arrived = () => BeginRise(body);
        AddChild(wisp);
    }

    private void BeginRise(Fallen body)
    {
        var def = GameData.GetUnit(body.DefinitionId);
        if (def == null || _battleEnded) { _raisesPending--; return; }
        // the raised serve the Host: enemies keep their stage scaling, fallen Caravan soldiers come back weaker
        UnitStats stats;
        if (def.IsPlayerSide || !_spawnDirector.TryBuildEnemyStats(body.DefinitionId, out stats))
            stats = new UnitStats(def, healthScale: .6f, damageScale: .8f);
        var startFrame = -1;
        var facing = body.Facing;
        if (body.Corpse != null && IsInstanceValid(body.Corpse))
        {
            startFrame = body.Corpse.CurrentFrame;
            facing = body.Corpse.Facing;
            body.Position = body.Corpse.Position;      // where the knockback left it
            body.Corpse.QueueFree();
        }
        var rise = new RiseVisual();
        AddChild(rise);
        rise.Position = body.Position;
        rise.Setup(stats, startFrame, _activeRouteId ?? "city", fromGround: startFrame < 0, facing: facing);
        rise.ShouldPause = () => _battlePaused || _endlessCheckpointActive;
        rise.Risen = () =>
        {
            _raisesPending--;
            if (_battleEnded) return;
            var unit = SpawnUnit(Team.Enemy, stats, body.Position);
            unit.IsSummoned = true;
            unit.CanDropCourage = false;
            unit.MarkRaised();
            unit.SkipDeploy();
            AudioDirector.Instance?.PlayEnemySpawn(unit);
        };
        BattleParticles.SpawnResurrectRise(this, body.Position, startFrame < 0);
    }
}

/// <summary>A soul-wisp flying from a necromancer to a body: a green flame on an arc, leaving a trail.</summary>
public partial class RaiseWisp : Node2D
{
    public Vector2 Start, Target;
    public float Delay;
    public System.Action Arrived;
    public System.Func<bool> ShouldPause;
    private const float Flight = .5f;
    private float _age;
    private readonly List<Vector2> _trail = new();
    private static readonly Color Soul = new(.55f, 1f, .6f);

    public override void _Ready()
    {
        ZAsRelative = false; ZIndex = 35;
        Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
        Position = Start;
    }

    public override void _Process(double delta)
    {
        if (ShouldPause?.Invoke() ?? false) return;
        _age += (float)delta;
        var t = (_age - Delay) / Flight;
        if (t < 0) { Visible = false; return; }
        Visible = true;
        if (t >= 1)
        {
            Arrived?.Invoke();
            Arrived = null;
            if (t >= 1.4f) { QueueFree(); return; }
        }
        var u = Mathf.Clamp(t, 0, 1);
        var e = u * u * (3 - 2 * u);
        var lift = -60f * Mathf.Sin(Mathf.Pi * u);
        Position = Start.Lerp(Target, e) + new Vector2(0, lift);
        _trail.Add(Position);
        if (_trail.Count > 10) _trail.RemoveAt(0);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var fade = _age - Delay > Flight ? Mathf.Clamp(1 - (_age - Delay - Flight) / .4f, 0, 1) : 1f;
        for (var i = 0; i < _trail.Count; i++)
        {
            var k = (i + 1f) / _trail.Count;
            DrawCircle(_trail[i] - Position, 2f + 4f * k, new Color(Soul, .18f * k * fade));
        }
        DrawCircle(Vector2.Zero, 9f, new Color(Soul, .25f * fade));
        DrawCircle(Vector2.Zero, 4.5f, new Color(Soul.Lightened(.4f), .8f * fade));
    }
}

/// <summary>A body getting up: the unit's death clip played backwards from where the body lay (or from flat on the
/// ground for the dead that claw up out of it), lit sickly green, with dirt and grave-light at its feet. Calls
/// Risen when it stands, so the battle can put the real unit in its place.</summary>
public partial class RiseVisual : Node2D
{
    private UnitSpriteSheet _sheet;
    private SpriteAnimRange _clip;
    private Vector2 _size;
    private float _facing = -1f, _age, _duration = .9f;
    private int _from;
    private ShaderMaterial _look;
    public System.Action Risen;
    public System.Func<bool> ShouldPause;

    public void Setup(UnitStats stats, int startFrame, string zone, bool fromGround, float facing = -1f)
    {
        var probe = new Unit();
        probe.Setup(Team.Enemy, stats, Vector2.Zero);
        _sheet = UnitSpriteLoader.TryLoad(probe.VisualClass, probe.DefinitionId);
        var width = probe.Radius * 2f * probe.VisualScale * (_sheet?.DrawScale ?? 1f);
        probe.Free();
        _facing = facing;
        if (_sheet == null || !_sheet.Animations.TryGetValue(UnitAnimState.Death, out _clip)) return;
        _size = new Vector2(width, width * _sheet.FrameHeight / _sheet.FrameWidth);
        _from = startFrame >= 0 ? Mathf.Clamp(startFrame, 0, _clip.FrameCount - 1) : _clip.FrameCount - 1;
        _duration = fromGround ? 1.05f : .85f;
        _look = UnitLook.CreateMaterial(zone);
        _look.SetShaderParameter("raised", 1f);
        Material = _look;
    }

    public override void _Ready() { ZAsRelative = false; ZIndex = 4; }

    public override void _Process(double delta)
    {
        if (ShouldPause?.Invoke() ?? false) return;
        _age += (float)delta;
        if (_age >= _duration || _sheet == null || _clip == null)
        {
            Risen?.Invoke();
            Risen = null;
            QueueFree();
            return;
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_sheet == null || _clip == null) return;
        // slow to stir, then up in a rush, steadying at the end
        var t = Mathf.Clamp(_age / _duration, 0, 1);
        var e = t < .35f ? t / .35f * .2f : .2f + .8f * (1 - Mathf.Pow(1 - (t - .35f) / .65f, 2.2f));
        var frame = Mathf.Clamp(Mathf.RoundToInt(_from * (1 - e)), 0, _clip.FrameCount - 1);
        var piece = UnitSpriteLoader.Frame(_sheet, _clip.StartFrame + frame);
        var shake = t < .35f ? Mathf.Sin(_age * 70f) * 1.2f : 0f;
        DrawSetTransform(new Vector2(shake, 0), 0f, new Vector2(_facing < 0 ? -1 : 1, 1));
        if (_look != null) UnitLook.SetFrame(_look, piece.Texture, piece.Source, Mathf.Clamp(1 - t * 3, 0, 1) * .35f, _facing, 0);
        var rect = new Rect2(new Vector2(-_size.X * _sheet.AnchorX, -_size.Y * _sheet.AnchorY), _size);
        DrawTextureRectRegion(piece.Texture, UnitSpriteLoader.Place(_sheet, piece, rect), piece.Source);
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }
}
