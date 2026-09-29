using System;
using Godot;

// Contact scheduling lives on the simulation clock, not the rendering clock.
public partial class Unit
{
    public uint CombatLifetime { get; private set; }
    public bool IsAttackCommitted => _contactMotionActive;
    public Func<bool> ShouldPausePresentation { get; set; }
    private Action _contactAction;
    private bool _contactMotionActive;
    private float _contactClock;
    private float _contactDuration;
    private float _contactTime;
    private float _facing;
    private Unit _contactTarget;
    private uint _contactTargetLifetime;
    private Vector2 _aimPosition;
    private Vector2 _contactOffset;
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
            return GlobalPosition + new Vector2(tip.X * SpriteDrawSize.X * GetFacing(), tip.Y * SpriteDrawSize.Y) + ContactDrawOffset();
        }
    }
    public string MotionProfile { get { EnsureSpriteLoaded(); return _spriteSheet?.MotionProfile ?? "sword-cut"; } }
    public float AttackContactSeconds
    {
        get
        {
            EnsureSpriteLoaded();
            if (_spriteSheet != null && _spriteSheet.Animations.TryGetValue(UnitAnimState.Attack, out var clip))
                return Mathf.Max(.01f,Mathf.Clamp(clip.ContactFrame, 0, clip.FrameCount - 1) * AttackFrameSeconds(clip));
            return .18f;
        }
    }
    private float AttackFrameSeconds(SpriteAnimRange clip) => Mathf.Min(clip.FrameDuration, AttackCooldown * .9f / Mathf.Max(1,clip.FrameCount));

    public void FaceCombatTarget(Unit target)
    {
        _contactTarget = target;
        _contactTargetLifetime = target.CombatLifetime;
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
        _contactDuration = _spriteSheet != null && _spriteSheet.Animations.TryGetValue(UnitAnimState.Attack,out var clip)
            ? clip.FrameCount * AttackFrameSeconds(clip) : .45f;
        _spriteAttackRemaining = _contactDuration;
        _spriteAnimState = UnitAnimState.Attack;
        _spriteAnimFrame = 0;
        _spriteAnimTimer = 0;
        _contactOffset = Vector2.Zero;
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

    private Vector2 ContactDrawOffset()
    {
        if (!_contactMotionActive || UsesProjectile) return Vector2.Zero;
        if (_contactClock <= _contactTime)
        {
            var target = _contactTarget;
            var point = _aimPosition + new Vector2(0,-Radius*.9f);
            if (IsInstanceValid(target) && target.CombatLifetime == _contactTargetLifetime && !target.IsDead)
                point = target.BodyContactPosition - new Vector2(GetFacing()*target.Radius*.28f,0);
            var tip = _spriteSheet?.ContactOffset ?? new Vector2(.28f,-.25f);
            var nativeTip = GlobalPosition + new Vector2(tip.X*SpriteDrawSize.X*GetFacing(),tip.Y*SpriteDrawSize.Y);
            _contactOffset = (point-nativeTip).LimitLength(Mathf.Min(48,AttackRange+8));
        }
        var weight = _contactClock <= _contactTime
            ? Mathf.SmoothStep(0,1,Mathf.Clamp((_contactClock/_contactTime-.4f)/.6f,0,1))
            : 1-Mathf.SmoothStep(0,1,Mathf.Clamp((_contactClock-_contactTime)/Mathf.Max(.01f,_contactDuration-_contactTime),0,1));
        return _contactOffset * weight;
    }

    private void ResetCombatMotion()
    {
        CombatLifetime++;
        _contactAction = null;
        _contactMotionActive = false;
        _contactTarget = null;
        _contactClock = _contactDuration = _contactTime = 0;
        _hitReaction.Reset();
        _contactOffset = Vector2.Zero;
        _facing = 0;
        ShouldPausePresentation = null;
    }
}
