using System;
using System.Collections.Generic;
using Godot;

public partial class Unit
{
    public float ShieldHealth { get; private set; }
    public float ArmorBreak { get; private set; }
    public float HealingReceivedScale { get; private set; } = 1f;
    public float AttackIntervalModifier { get; private set; } = 1f;
    public bool IsControlled => _controlTime > 0f;
    public bool HasReturned { get; private set; }
    public bool IsSummoned { get; set; }
    public bool IsTacticalObject { get; set; }
    public int CommittedAttacks { get; private set; }
    public float ContentAge { get; private set; }
    public float LifetimeRemaining { get; set; } = -1f;
    public int UtilityCharges { get; set; }
    public float ContentCooldown { get; set; }
    public float MarkedTime { get; set; }
    public float FocusMarkTime { get; set; }
    public string ContentState { get; set; } = "";
    public bool IsConcealed { get; private set; }
    public bool IsSilenced => _silenceTime > 0f;
    public bool IsBurning => _dotTime > 0f;
    public bool MovedRecently => _stillTime < .3f;
    public int CastGeneration { get; private set; }
    public int DirectHitsTaken { get; set; }
    public float GeneratedCourage { get; set; }
    public float ProjectileMirrorTime { get; set; }
    public int ProjectileMirrorCharges { get; set; }
    public float PhysicalGuard { get; private set; }
    public int CrewGroup { get; set; }
    public bool SecondaryBody { get; set; }
    public float LastAttackAge { get; private set; }
    public float SlowDurationScale { get; set; } = 1f;
    public float BurnDurationScale { get; set; } = 1f;
    public float BurnDamageScale { get; set; } = 1f;
    public float HealingPenaltyFloor { get; set; } = .5f;
    public float LowHealthHealingBonus { get; set; }
    public float FirstWindupScale { get; set; } = 1f;
    public int ProjectileDodgeCharges { get; set; }
    private float _concealTime, _silenceTime, _rootTime, _stillTime, _physicalGuardTime, _cleanseImmunity;
    private Vector2 _contentLastPosition;
    private bool _wakeOnDamage;
    public readonly Dictionary<string,float> EffectReadyAt = new(StringComparer.OrdinalIgnoreCase);
    private float _shieldTime, _armorBreakTime, _healingPenaltyTime, _controlTime, _controlImmunity;
    private float _attackIntervalTime, _dotTime, _dotClock, _dotDamage;
    private string _dotOwner = "";

    private void ResetContentState()
    {
        ShieldHealth = ArmorBreak = _shieldTime = _armorBreakTime = 0f;
        _healingPenaltyTime = _controlTime = _controlImmunity = 0f;
        _attackIntervalTime = _dotTime = _dotClock = _dotDamage = 0f;
        HealingReceivedScale = AttackIntervalModifier = 1f;
        HasReturned = IsSummoned = IsTacticalObject = false;
        CommittedAttacks = 0;
        ContentAge = ContentCooldown = MarkedTime = FocusMarkTime = 0f;
        LifetimeRemaining = -1f;
        UtilityCharges = 0;
        ContentState = _dotOwner = "";
        IsConcealed = IsUntargetable = false;
        _untargetableTimer = 0f;
        CastGeneration = DirectHitsTaken = CrewGroup = ProjectileMirrorCharges = 0;
        GeneratedCourage = ProjectileMirrorTime = PhysicalGuard = 0f;
        _concealTime = _silenceTime = _rootTime = _stillTime = _physicalGuardTime = _cleanseImmunity = 0f;
        SecondaryBody = false;
        LastAttackAge = 0f;
        SlowDurationScale = BurnDurationScale = BurnDamageScale = FirstWindupScale = 1f;
        HealingPenaltyFloor = .5f;LowHealthHealingBonus = 0f;ProjectileDodgeCharges = 0;
        _wakeOnDamage = false;
        _contentLastPosition = Position;
        EffectReadyAt.Clear();
    }

    public void TickContentState(float delta)
    {
        ContentAge += delta;
        _stillTime = Position.DistanceSquaredTo(_contentLastPosition) < .04f ? _stillTime + delta : 0f;
        _contentLastPosition = Position;
        if ((_concealTime -= delta) <= 0f) IsConcealed = false;
        _silenceTime = Mathf.Max(0f, _silenceTime - delta);
        _rootTime = Mathf.Max(0f, _rootTime - delta);
        _cleanseImmunity = Mathf.Max(0f, _cleanseImmunity - delta);
        ProjectileMirrorTime = Mathf.Max(0f, ProjectileMirrorTime - delta);
        if ((_physicalGuardTime -= delta) <= 0f) PhysicalGuard = 0f;
        ContentCooldown = Mathf.Max(0f, ContentCooldown - delta);
        MarkedTime = Mathf.Max(0f, MarkedTime - delta);
        FocusMarkTime = Mathf.Max(0f, FocusMarkTime - delta);
        _controlTime = Mathf.Max(0f, _controlTime - delta);
        _controlImmunity = Mathf.Max(0f, _controlImmunity - delta);
        if ((_shieldTime -= delta) <= 0f) ShieldHealth = 0f;
        if ((_armorBreakTime -= delta) <= 0f) ArmorBreak = 0f;
        if ((_healingPenaltyTime -= delta) <= 0f) HealingReceivedScale = 1f;
        if ((_attackIntervalTime -= delta) <= 0f) AttackIntervalModifier = 1f;
        if (LifetimeRemaining >= 0f && (LifetimeRemaining -= delta) <= 0f)
        {
            CanDropCourage = false;
            Health = 0f;
        }
        if (_dotTime > 0f && !IsDead)
        {
            _dotTime -= delta;
            _dotClock += delta;
            while (_dotClock >= 1f)
            {
                _dotClock -= 1f;
                TakeDamage(_dotDamage, _dotOwner, CombatDamageType.Magical);
            }
        }
    }

    public void GiveShield(float amount, float duration)
    {
        if (IsDead || amount <= 0f || duration <= 0f) return;
        ShieldHealth = Mathf.Max(ShieldHealth, Mathf.Min(MaxHealth * .35f, amount));
        _shieldTime = Mathf.Max(_shieldTime, Mathf.Min(12f, duration));
        _auraFlashTimer = Mathf.Max(_auraFlashTimer, .3f);
    }

    public void RemoveShield() { ShieldHealth = 0f; _shieldTime = 0f; }

    public void ApplyArmorBreak(float amount, float duration)
    {
        ArmorBreak = Mathf.Max(ArmorBreak, Mathf.Min(Armor * .5f, amount));
        _armorBreakTime = Mathf.Max(_armorBreakTime, duration);
    }

    public void ApplyHealingPenalty(float scale, float duration)
    {
        if (_cleanseImmunity > 0f) return;
        HealingReceivedScale = Mathf.Min(HealingReceivedScale, Mathf.Clamp(scale, HealingPenaltyFloor, 1f));
        _healingPenaltyTime = Mathf.Max(_healingPenaltyTime, duration);
    }

    public void ApplyBurn(float damagePerSecond, float duration, string owner)
    {
        if (_cleanseImmunity > 0f) return;
        _dotDamage = Mathf.Max(_dotDamage, damagePerSecond * BurnDamageScale);
        _dotTime = Mathf.Max(_dotTime, Mathf.Min(6f, duration * BurnDurationScale));
        _dotOwner = owner ?? "";
    }

    public void Cleanse()
    {
        _dotTime = _dotDamage = _armorBreakTime = ArmorBreak = _healingPenaltyTime = 0f;
        HealingReceivedScale = 1f;
        _controlTime = 0f;
        _rootTime = 0f;
        _cleanseImmunity = 1.5f;
        _temporarySpeedScale = Mathf.Max(1f, _temporarySpeedScale);
    }

    public bool ApplyControl(float duration)
    {
        if (IsDead || _controlImmunity > 0f) return false;
        var boss = Definition?.IsBoss == true || VisualClass == "boss";
        _controlTime = Mathf.Min(boss ? .3f : 2f, duration);
        _controlImmunity = _controlTime + (boss ? 6f : 3f);
        CancelContentAttack();
        return true;
    }

    private void CancelContentAttack()
    {
        CastGeneration++;
        MarkedTime=0;
        _contactAction = null;
        _contactMotionActive = false;
        _contactTarget = null;
        _spriteAttackRemaining = 0f;
    }

    public void ApplyAttackRecovery(float scale, float duration)
    {
        AttackIntervalModifier = Mathf.Clamp(scale, .8f, 1.4f);
        _attackIntervalTime = Mathf.Max(_attackIntervalTime, duration);
    }

    public bool TryReturn(float healthRatio = .25f)
    {
        if (!IsDead || HasReturned || IsSummoned || IsTacticalObject) return false;
        HasReturned = true;
        CancelContentAttack();
        CanDropCourage = false;
        Health = Mathf.Max(1f, MaxHealth * Mathf.Clamp(healthRatio, .01f, .25f));
        Speed *= .85f;
        Cleanse();
        return true;
    }

    public void MarkReturned() => HasReturned = true;

    public void Conceal(float duration) { IsConcealed = true; _concealTime = duration; }
    public void Reveal() { IsConcealed = false; IsUntargetable = false; _concealTime = _untargetableTimer = 0f; }
    public void Silence(float duration) { _silenceTime = Math.Max(_silenceTime, Definition?.IsBoss == true ? .5f : duration); CancelContentAttack(); }
    public void Root(float duration)
    {
        if (_controlImmunity > 0f) return;
        _rootTime = Definition?.IsBoss == true ? .3f : Math.Min(2f, duration);
        _controlImmunity = _rootTime + 3f;
    }
    public bool MovementControlled => IsControlled || _rootTime > 0f;
    private void BreakRootFromDamage()
    {
        _rootTime = 0f;
        if (_wakeOnDamage) { _controlTime = 0f; _wakeOnDamage = false; }
    }
    public void Sleep(float duration) { if (ApplyControl(duration)) _wakeOnDamage = true; }
    public void GuardPhysical(float ratio, float duration) { PhysicalGuard = Mathf.Clamp(ratio, 0f, .2f); _physicalGuardTime = duration; }
    public void BecomeMelee(float reach = 30f) { UsesProjectile = false; AttackRange = reach; DamagesStructures = Definition.BaseDamage > 0; }
    public void Despawn() { CanDropCourage = false; IsSummoned = true; Health = 0f; }
    public void ShortenBlight() { _healingPenaltyTime *= .8f; _dotTime *= .8f; }

    public bool TryEffect(string id, float cooldown)
    {
        if (EffectReadyAt.TryGetValue(id, out var next) && ContentAge < next) return false;
        EffectReadyAt[id] = ContentAge + cooldown;
        return true;
    }
}
