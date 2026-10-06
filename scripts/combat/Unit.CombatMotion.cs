using System;
using Godot;

// Contact scheduling lives on the simulation clock, not the rendering clock.
public partial class Unit
{
    public uint CombatLifetime { get; private set; }
    public bool IsAttackCommitted => _contactMotionActive;
    public bool IsAttackingPosition(Vector2 position) => _contactTarget == null && _contactDuration > 0 && _aimPosition.DistanceSquaredTo(position) < .01f;
    public Func<bool> ShouldPausePresentation { get; set; }
    private Action _contactAction;
    private bool _contactMotionActive;
    private float _contactClock;
    private float _contactDuration;
    private float _contactTime;
    private float _facing;
    private Unit _contactTarget;
    private Vector2 _aimPosition;
    // The clip the current attack plays: Attack, or Melee for a ranged unit's close-quarters strike.
    private UnitAnimState _attackClip = UnitAnimState.Attack;
    /// <summary>True when the latest attack is a ranged unit's melee strike (whatever clip it plays). It stays set
    /// after the blow until the next attack begins.</summary>
    public bool IsMeleeStrike { get; private set; }
    private readonly HitReactionMotion _hitReaction = new();
    internal float HitReactionAmount => _hitReaction.Amount;

    public Vector2 BodyContactPosition
    {
        get
        {
            EnsureSpriteLoaded();
            return GlobalPosition + new Vector2(0, _spriteSheet != null
                ? SpriteDrawSize.Y * _spriteSheet.BodyOffset.Y : -Radius * .9f);
        }
    }
    private Vector2 SpriteDrawSize => new Vector2(Radius * 2 * VisualScale * (_spriteSheet?.DrawScale ?? 1),
        Radius * 2 * VisualScale * (_spriteSheet?.DrawScale ?? 1) * (_spriteSheet != null ? (float)_spriteSheet.FrameHeight / _spriteSheet.FrameWidth : 1));
    public Vector2 WeaponContactPosition
    {
        get
        {
            EnsureSpriteLoaded();
            var tip = _spriteSheet?.ContactOffset ?? new Vector2(.28f,-.25f);
            return GlobalPosition + new Vector2(tip.X * SpriteDrawSize.X * GetFacing(), tip.Y * SpriteDrawSize.Y);
        }
    }
    public string MotionProfile { get { EnsureSpriteLoaded(); return _spriteSheet?.MotionProfile ?? "sword-cut"; } }
    public float AttackContactSeconds
    {
        get
        {
            EnsureSpriteLoaded();
            if (_spriteSheet != null && _spriteSheet.Animations.TryGetValue(_attackClip, out var clip))
                return Mathf.Max(.01f,Mathf.Clamp(clip.ContactFrame, 0, clip.FrameCount - 1) * AttackFrameSeconds(clip));
            return .18f;
        }
    }

    /// <summary>Melee reach for a ranged unit: a soldier's close-quarters distance, grown by however far
    /// either body extends past a regular soldier's, plus <paramref name="slack"/>.</summary>
    public bool InMeleeReach(Unit target, float slack = 0f)
    {
        var reach = GameData.Combat.RangedMeleeReach + slack + Mathf.Max(0, Radius - 14f) + Mathf.Max(0, target.Radius - 14f);
        return Position.DistanceTo(target.Position) <= reach;
    }

    private void SelectAttackClip(bool melee)
    {
        EnsureSpriteLoaded();
        IsMeleeStrike = melee;
        _attackClip = melee && _spriteSheet != null && _spriteSheet.Animations.ContainsKey(UnitAnimState.Melee)
            ? UnitAnimState.Melee : UnitAnimState.Attack;
    }
    private float AttackFrameSeconds(SpriteAnimRange clip) => Mathf.Min(clip.FrameDuration, AttackCooldown * .9f / Mathf.Max(1,clip.FrameCount));

    public void FaceCombatTarget(Unit target)
    {
        _contactTarget = target;
        FaceCombatPosition(target.Position);
    }
    private void FaceCombatPosition(Vector2 position)
    {
        _aimPosition = position;
        if (Mathf.Abs(position.X - Position.X) > .5f) _facing = Mathf.Sign(position.X - Position.X);
    }

    public void ScheduleAttackImpact(Action action)
    {
        EnsureSpriteLoaded();
        _contactAction = action;
        _contactMotionActive = true;
        _contactClock = 0;
        _contactTime = AttackContactSeconds;
        _contactDuration = _spriteSheet != null && _spriteSheet.Animations.TryGetValue(_attackClip,out var clip)
            ? clip.FrameCount * AttackFrameSeconds(clip) : .45f;
        _spriteAttackRemaining = _contactDuration;
        _spriteAnimState = _attackClip;
        _spriteAnimFrame = 0;
        _spriteAnimTimer = 0;
    }

    private void TickCombatMotion(float delta)
    {
        if (!_contactMotionActive) return;
        if (IsDead) { _contactAction = null; _contactMotionActive = false; return; }
        _contactClock += Mathf.Max(0,delta);
        _spriteAttackRemaining = Mathf.Max(0,_contactDuration - _contactClock);
        if (_contactAction != null && _contactClock + .00001f >= _contactTime)
        {
            var action = _contactAction;
            _contactAction = null; // Re-entrancy and large simulation steps still fire exactly once.
            action();
        }
        if (_contactClock >= _contactDuration) _contactMotionActive = false;
    }

    public bool CanResolveContact(Unit target, uint lifetime, bool ranged)
    {
        return !IsDead && IsInstanceValid(target) && target.CombatLifetime == lifetime
            && !target.IsDead && !target.IsUntargetable
            && Position.DistanceTo(target.Position) <= AttackRange + (ranged ? 24f : 12f);
    }

    public void ReactToContact(float direction, float damage, float weight = 1f)
    {
        if (IsDead || damage <= .05f || (GameState.Instance?.ReducedMotion ?? false)) return;
        _hitReaction.Add(direction, Mathf.Clamp(.18f + damage*.012f, .18f, .85f) * weight);
        QueueRedraw();
    }

    private void ResetCombatMotion()
    {
        CombatLifetime++;
        _contactAction = null;
        _contactMotionActive = false;
        _contactTarget = null;
        _contactClock = _contactDuration = _contactTime = 0;
        _attackClip = UnitAnimState.Attack;
        IsMeleeStrike = false;
        _hitReaction.Reset();
        _facing = 0;
        ShouldPausePresentation = null;
    }
}
