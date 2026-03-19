using System;
using Godot;

public enum Team
{
    Player,
    Enemy
}

public sealed class UnitStats
{
    public UnitStats(
        UnitDefinition definition,
        float healthScale = 1f,
        float damageScale = 1f,
        float cooldownReduction = 0f,
        int baseDamageBonus = 0,
        float speedScale = 1f)
    {
        DefinitionId = definition.Id;
        Name = definition.DisplayName;
        MaxHealth = definition.MaxHealth * healthScale;
        Speed = definition.Speed * speedScale;
        AttackDamage = definition.AttackDamage * damageScale;
        AttackRange = definition.AttackRange;
        AttackCooldown = Mathf.Max(0.45f, definition.AttackCooldown - cooldownReduction);
        AttackSplashRadius = definition.AttackSplashRadius;
        UsesProjectile = definition.UsesProjectile;
        ProjectileSpeed = definition.ProjectileSpeed;
        AggroRangeX = Mathf.Max(AttackRange, definition.AggroRangeX);
        AggroRangeY = definition.AggroRangeY;
        BaseDamage = definition.BaseDamage + baseDamageBonus;
        BusRepairAmount = definition.BusRepairAmount * Mathf.Lerp(1f, damageScale, 0.8f);
        DamageTakenScale = definition.DamageTakenScale;
        DamageReflectScale = definition.DamageReflectScale;
        AuraRadius = definition.AuraRadius;
        AuraAttackDamageScale = definition.AuraAttackDamageScale;
        AuraSpeedScale = definition.AuraSpeedScale;
        SpecialAbilityId = definition.SpecialAbilityId ?? "";
        SpecialCooldown = definition.SpecialCooldown;
        SpecialCourageGainScale = definition.SpecialCourageGainScale;
        SpecialDeployCooldownPenalty = definition.SpecialDeployCooldownPenalty;
        SpecialSpawnUnitId = definition.SpecialSpawnUnitId ?? "";
        SpecialSpawnCount = definition.SpecialSpawnCount;
        SpecialBuffRadius = definition.SpecialBuffRadius;
        SpecialBuffDuration = definition.SpecialBuffDuration;
        SpecialBuffAttackDamageScale = definition.SpecialBuffAttackDamageScale;
        SpecialBuffSpeedScale = definition.SpecialBuffSpeedScale;
        DeathBurstDamage = definition.DeathBurstDamage;
        DeathBurstRadius = definition.DeathBurstRadius;
        SpawnOnDeathUnitId = definition.SpawnOnDeathUnitId;
        SpawnOnDeathCount = definition.SpawnOnDeathCount;
        VisualClass = ResolveVisualClass(definition);
        VisualScale = ResolveVisualScale(definition);
        Cost = definition.Cost;
        Color = definition.GetTint();
        DeployQuote = definition.DeployQuote ?? "";
        KillQuote = definition.KillQuote ?? "";
        AbilityQuote = definition.AbilityQuote ?? "";
    }

    public string DefinitionId { get; }
    public string Name { get; }
    public float MaxHealth { get; }
    public float Speed { get; }
    public float AttackDamage { get; }
    public float AttackRange { get; }
    public float AttackCooldown { get; }
    public float AttackSplashRadius { get; }
    public bool UsesProjectile { get; }
    public float ProjectileSpeed { get; }
    public float AggroRangeX { get; }
    public float AggroRangeY { get; }
    public int BaseDamage { get; }
    public float BusRepairAmount { get; }
    public float DamageTakenScale { get; }
    public float DamageReflectScale { get; }
    public float AuraRadius { get; }
    public float AuraAttackDamageScale { get; }
    public float AuraSpeedScale { get; }
    public string SpecialAbilityId { get; }
    public float SpecialCooldown { get; }
    public float SpecialCourageGainScale { get; }
    public float SpecialDeployCooldownPenalty { get; }
    public string SpecialSpawnUnitId { get; }
    public int SpecialSpawnCount { get; }
    public float SpecialBuffRadius { get; }
    public float SpecialBuffDuration { get; }
    public float SpecialBuffAttackDamageScale { get; }
    public float SpecialBuffSpeedScale { get; }
    public float DeathBurstDamage { get; }
    public float DeathBurstRadius { get; }
    public string SpawnOnDeathUnitId { get; }
    public int SpawnOnDeathCount { get; }
    public string VisualClass { get; }
    public float VisualScale { get; }
    public int Cost { get; }
    public Color Color { get; set; }
    public string DeployQuote { get; }
    public string KillQuote { get; }
    public string AbilityQuote { get; }

    private static string ResolveVisualClass(UnitDefinition definition)
    {
        if (!string.IsNullOrWhiteSpace(definition.VisualClass))
        {
            return definition.VisualClass.Trim().ToLowerInvariant();
        }

        if (definition.DeathBurstDamage > 0f)
        {
            return "bloater";
        }

        if (definition.AuraRadius > 0f || HasIdToken(definition, "howler"))
        {
            return "howler";
        }

        if (string.Equals(definition.SpecialAbilityId, "jam_signal", StringComparison.OrdinalIgnoreCase))
        {
            return "jammer";
        }

        if (definition.SpawnOnDeathCount > 0)
        {
            return "splitter";
        }

        if (definition.DamageTakenScale < 0.9f && definition.MaxHealth >= 250f)
        {
            return "boss";
        }

        if (HasIdToken(definition, "defender"))
        {
            return "shield";
        }

        if (HasIdToken(definition, "marksman"))
        {
            return "sniper";
        }

        if (definition.BusRepairAmount > 0f || HasIdToken(definition, "mechanic"))
        {
            return "support";
        }

        if (HasIdToken(definition, "ranger") || HasIdToken(definition, "shooter"))
        {
            return "gunner";
        }

        if (HasIdToken(definition, "raider"))
        {
            return "skirmisher";
        }

        if (definition.UsesProjectile)
        {
            return definition.IsPlayerSide ? "gunner" : "spitter";
        }

        if (definition.DamageTakenScale < 0.9f)
        {
            return definition.MaxHealth >= 180f ? "boss" : "crusher";
        }

        if (definition.MaxHealth >= 105f)
        {
            return "brute";
        }

        if (definition.Speed >= 100f)
        {
            return "runner";
        }

        return definition.IsPlayerSide ? "fighter" : "walker";
    }

    private static bool HasIdToken(UnitDefinition definition, string token)
    {
        return !string.IsNullOrWhiteSpace(definition?.Id) &&
            definition.Id.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static float ResolveVisualScale(UnitDefinition definition)
    {
        if (definition.VisualScale > 0f)
        {
            return Mathf.Clamp(definition.VisualScale, 0.75f, 1.8f);
        }

        return ResolveVisualClass(definition) switch
        {
            "boss" => 1.55f,
            "siegetower" => 1.6f,
            "crusher" => 1.25f,
            "brute" => 1.18f,
            "bloater" => 1.22f,
            "berserker" => 1.08f,
            "banner" => 1.06f,
            "mirror" => 1.06f,
            "howler" => 1.04f,
            "necromancer" => 1.0f,
            "jammer" => 0.98f,
            "shield" => 1.08f,
            "sniper" => 0.94f,
            "runner" => 0.92f,
            "saboteur" => 0.92f,
            "hound" => 0.82f,
            _ => 1f
        };
    }
}

public partial class Unit : Node2D
{
    public Team Team { get; private set; }
    public string DefinitionId { get; private set; } = "";
    public string UnitName { get; private set; } = "";
    public float MaxHealth { get; private set; } = 1f;
    public float Health { get; private set; } = 1f;
    public float Speed { get; private set; }
    public float AttackDamage { get; private set; }
    public float AttackRange { get; private set; }
    public float AttackCooldown { get; private set; }
    public float AttackSplashRadius { get; private set; }
    public bool UsesProjectile { get; private set; }
    public float ProjectileSpeed { get; private set; }
    public float AggroRangeX { get; private set; }
    public float AggroRangeY { get; private set; }
    public int BaseDamage { get; private set; }
    public float BusRepairAmount { get; private set; }
    public float DamageTakenScale { get; private set; } = 1f;
    public float DamageReflectScale { get; private set; }
    public float AuraRadius { get; private set; }
    public float AuraAttackDamageScale { get; private set; } = 1f;
    public float AuraSpeedScale { get; private set; } = 1f;
    public string SpecialAbilityId { get; private set; } = "";
    public float SpecialCooldown { get; private set; }
    public float SpecialCourageGainScale { get; private set; } = 1f;
    public float SpecialDeployCooldownPenalty { get; private set; }
    public string SpecialSpawnUnitId { get; private set; } = "";
    public int SpecialSpawnCount { get; private set; }
    public float SpecialBuffRadius { get; private set; }
    public float SpecialBuffDuration { get; private set; }
    public float SpecialBuffAttackDamageScale { get; private set; } = 1f;
    public float SpecialBuffSpeedScale { get; private set; } = 1f;
    public float DeathBurstDamage { get; private set; }
    public float DeathBurstRadius { get; private set; }
    public string SpawnOnDeathUnitId { get; private set; } = "";
    public int SpawnOnDeathCount { get; private set; }
    public string ActiveAbilityId { get; private set; } = "";
    public float ActiveAbilityCooldown { get; private set; }
    public bool HasActiveAbility => !string.IsNullOrWhiteSpace(ActiveAbilityId) && ActiveAbilityCooldown > 0.05f;
    public bool IsUntargetable { get; private set; }
    public string DeployQuote { get; private set; } = "";
    public string KillQuote { get; private set; } = "";
    public string AbilityQuote { get; private set; } = "";
    public string LastDamagedBy { get; set; } = "";
    public string VisualClass { get; private set; } = "fighter";
    public float VisualScale { get; private set; } = 1f;
    public float Radius { get; private set; } = 16f;
    public Color Tint => _bodyColor;
    public float CurrentAttackDamage => AttackDamage * _currentAttackDamageScale * ResolveBerserkerRageScale();
    public bool ProvidesAura => AuraRadius > 0f && (AuraAttackDamageScale > 1.01f || AuraSpeedScale > 1.01f);
    public bool HasSpecialAbility => !string.IsNullOrWhiteSpace(SpecialAbilityId) && SpecialCooldown > 0.05f;
    public float HealthRatio => MaxHealth <= 0.01f ? 0f : Mathf.Clamp(Health / MaxHealth, 0f, 1f);

    public bool IsDead => Health <= 0f;

    private float _attackTimer;
    private float _specialTimer;
    private float _activeAbilityTimer;
    private float _untargetableTimer;
    private float _hitFlashTimer;
    private float _attackFlashTimer;
    private float _auraFlashTimer;
    private float _idleTimer;
    private UnitSpriteSheet _spriteSheet;
    private UnitAnimState _spriteAnimState = UnitAnimState.Idle;
    private float _spriteAnimTimer;
    private int _spriteAnimFrame;
    private bool _spriteLoadAttempted;
    private Vector2 _prevPosition;
    private float _currentAttackDamageScale = 1f;
    private float _currentSpeedScale = 1f;
    private float _currentDamageTakenScale = 1f;
    private float _temporaryCombatBuffTimer;
    private float _temporaryAttackDamageScale = 1f;
    private float _temporarySpeedScale = 1f;
    private float _temporaryDamageTakenScale = 1f;
    private bool _hasAuraBuff;
    private Color _bodyColor = Colors.White;

    public void Setup(Team team, UnitStats stats, Vector2 startPosition)
    {
        Team = team;
        DefinitionId = stats.DefinitionId;
        UnitName = stats.Name;
        MaxHealth = stats.MaxHealth;
        Health = stats.MaxHealth;
        Speed = stats.Speed;
        AttackDamage = stats.AttackDamage;
        AttackRange = stats.AttackRange;
        AttackCooldown = stats.AttackCooldown;
        AttackSplashRadius = stats.AttackSplashRadius;
        UsesProjectile = stats.UsesProjectile;
        ProjectileSpeed = stats.ProjectileSpeed;
        AggroRangeX = stats.AggroRangeX;
        AggroRangeY = stats.AggroRangeY;
        BaseDamage = stats.BaseDamage;
        BusRepairAmount = stats.BusRepairAmount;
        DamageTakenScale = stats.DamageTakenScale;
        DamageReflectScale = stats.DamageReflectScale;
        AuraRadius = stats.AuraRadius;
        AuraAttackDamageScale = stats.AuraAttackDamageScale;
        AuraSpeedScale = stats.AuraSpeedScale;
        SpecialAbilityId = stats.SpecialAbilityId;
        SpecialCooldown = stats.SpecialCooldown;
        SpecialCourageGainScale = stats.SpecialCourageGainScale;
        SpecialDeployCooldownPenalty = stats.SpecialDeployCooldownPenalty;
        SpecialSpawnUnitId = stats.SpecialSpawnUnitId;
        SpecialSpawnCount = stats.SpecialSpawnCount;
        SpecialBuffRadius = stats.SpecialBuffRadius;
        SpecialBuffDuration = stats.SpecialBuffDuration;
        SpecialBuffAttackDamageScale = stats.SpecialBuffAttackDamageScale;
        SpecialBuffSpeedScale = stats.SpecialBuffSpeedScale;
        DeathBurstDamage = stats.DeathBurstDamage;
        DeathBurstRadius = stats.DeathBurstRadius;
        SpawnOnDeathUnitId = stats.SpawnOnDeathUnitId;
        SpawnOnDeathCount = stats.SpawnOnDeathCount;
        VisualClass = stats.VisualClass;
        VisualScale = stats.VisualScale;
        Radius = ResolveRadius(stats);
        _bodyColor = stats.Color;
        DeployQuote = stats.DeployQuote;
        KillQuote = stats.KillQuote;
        AbilityQuote = stats.AbilityQuote;
        _specialTimer = HasSpecialAbility
            ? Mathf.Max(3f, SpecialCooldown * 0.55f)
            : 0f;
        Position = startPosition;
    }

    public void ResetForPool()
    {
        Health = 0f;
        _attackTimer = 0f;
        _specialTimer = 0f;
        _activeAbilityTimer = 0f;
        _untargetableTimer = 0f;
        _hitFlashTimer = 0f;
        _attackFlashTimer = 0f;
        _auraFlashTimer = 0f;
        _idleTimer = 0f;
        _currentAttackDamageScale = 1f;
        _currentSpeedScale = 1f;
        _currentDamageTakenScale = 1f;
        _temporaryCombatBuffTimer = 0f;
        _temporaryAttackDamageScale = 1f;
        _temporarySpeedScale = 1f;
        _temporaryDamageTakenScale = 1f;
        _spriteLoadAttempted = false;
        _spriteSheet = null;
        _spriteAnimState = UnitAnimState.Idle;
        _spriteAnimFrame = 0;
        _spriteAnimTimer = 0f;
        LastDamagedBy = null;
        Visible = false;
    }

    public void ResetCombatModifiers()
    {
        if (_temporaryCombatBuffTimer > 0.01f)
        {
            _currentAttackDamageScale = Mathf.Max(1f, _temporaryAttackDamageScale);
            _currentSpeedScale = Mathf.Clamp(_temporarySpeedScale, 0.35f, 3f);
            _currentDamageTakenScale = Mathf.Clamp(_temporaryDamageTakenScale, 0.25f, 2.5f);
            _hasAuraBuff =
                _temporaryAttackDamageScale > 1.001f ||
                Mathf.Abs(_temporarySpeedScale - 1f) > 0.01f ||
                _temporaryDamageTakenScale < 0.999f;
        }
        else
        {
            _currentAttackDamageScale = 1f;
            _currentSpeedScale = 1f;
            _currentDamageTakenScale = 1f;
            _hasAuraBuff = false;
            _temporaryAttackDamageScale = 1f;
            _temporarySpeedScale = 1f;
            _temporaryDamageTakenScale = 1f;
        }
    }

    public void ApplyCombatAura(float attackDamageScale, float speedScale)
    {
        var appliedAttackScale = Mathf.Max(1f, attackDamageScale);
        var appliedSpeedScale = Mathf.Max(1f, speedScale);
        if (appliedAttackScale <= 1.001f && appliedSpeedScale <= 1.001f)
        {
            return;
        }

        _currentAttackDamageScale = Mathf.Max(_currentAttackDamageScale, appliedAttackScale);
        _currentSpeedScale = Mathf.Max(_currentSpeedScale, appliedSpeedScale);
        _hasAuraBuff = true;
        _auraFlashTimer = Mathf.Max(_auraFlashTimer, 0.18f);
    }

    public void ApplyWeatherModifiers(float speedScale, float damageScale)
    {
        _currentSpeedScale *= speedScale;
        _currentAttackDamageScale *= damageScale;
    }

    public void ApplyTemporaryCombatBuff(float attackDamageScale, float speedScale, float duration)
    {
        if (duration <= 0.05f)
        {
            return;
        }

        _temporaryAttackDamageScale = Mathf.Max(_temporaryAttackDamageScale, Mathf.Max(1f, attackDamageScale));
        _temporarySpeedScale = Mathf.Max(_temporarySpeedScale, Mathf.Max(1f, speedScale));
        _temporaryCombatBuffTimer = Mathf.Max(_temporaryCombatBuffTimer, duration);
        _auraFlashTimer = Mathf.Max(_auraFlashTimer, 0.24f);
    }

    public void ApplyTemporarySpeedModifier(float speedScale, float duration)
    {
        if (duration <= 0.05f)
        {
            return;
        }

        var appliedScale = Mathf.Clamp(speedScale, 0.35f, 2.5f);
        _temporarySpeedScale = appliedScale < 1f
            ? Mathf.Min(_temporarySpeedScale, appliedScale)
            : Mathf.Max(_temporarySpeedScale, appliedScale);
        _temporaryCombatBuffTimer = Mathf.Max(_temporaryCombatBuffTimer, duration);
        _auraFlashTimer = Mathf.Max(_auraFlashTimer, 0.24f);
    }

    public void ApplyTemporaryDefenseModifier(float damageTakenScale, float duration)
    {
        if (duration <= 0.05f)
        {
            return;
        }

        _temporaryDamageTakenScale = Mathf.Min(_temporaryDamageTakenScale, Mathf.Clamp(damageTakenScale, 0.25f, 2.5f));
        _temporaryCombatBuffTimer = Mathf.Max(_temporaryCombatBuffTimer, duration);
        _auraFlashTimer = Mathf.Max(_auraFlashTimer, 0.24f);
    }

    public void TickAttackTimer(float delta)
    {
        if (_attackTimer > 0f)
        {
            _attackTimer -= delta;
        }
    }

    public void TickSpecialTimer(float delta)
    {
        if (_specialTimer > 0f)
        {
            _specialTimer -= delta;
        }
    }

    public bool TryTriggerSpecialAbility()
    {
        if (!HasSpecialAbility || _specialTimer > 0f)
        {
            return false;
        }

        _specialTimer = SpecialCooldown;
        _attackFlashTimer = Mathf.Max(_attackFlashTimer, 0.18f);
        return true;
    }

    public void SetActiveAbility(string abilityId, float cooldown)
    {
        ActiveAbilityId = abilityId ?? "";
        ActiveAbilityCooldown = cooldown;
        _activeAbilityTimer = HasActiveAbility
            ? Mathf.Max(3f, cooldown * 0.5f)
            : 0f;
    }

    public void TickActiveAbilityTimer(float delta)
    {
        if (_activeAbilityTimer > 0f)
        {
            _activeAbilityTimer -= delta;
        }

        if (_untargetableTimer > 0f)
        {
            _untargetableTimer -= delta;
            if (_untargetableTimer <= 0f)
            {
                IsUntargetable = false;
            }
        }
    }

    public bool TryTriggerActiveAbility()
    {
        if (!HasActiveAbility || _activeAbilityTimer > 0f)
        {
            return false;
        }

        _activeAbilityTimer = ActiveAbilityCooldown;
        _attackFlashTimer = Mathf.Max(_attackFlashTimer, 0.22f);
        return true;
    }

    public void SetUntargetable(float duration)
    {
        IsUntargetable = true;
        _untargetableTimer = duration;
        _auraFlashTimer = Mathf.Max(_auraFlashTimer, 0.24f);
    }

    public bool CanAttack(Unit target)
    {
        if (target.IsDead || target.IsUntargetable)
        {
            return false;
        }

        return CanAttackPosition(target.Position);
    }

    public bool CanAttackPosition(Vector2 position, float targetRadius = 0f)
    {
        return Position.DistanceTo(position) <= AttackRange + targetRadius;
    }

    public bool TryBeginAttackPosition(Vector2 position, float targetRadius = 0f)
    {
        if (_attackTimer > 0f || !CanAttackPosition(position, targetRadius))
        {
            return false;
        }

        _attackTimer = AttackCooldown;
        _attackFlashTimer = 0.14f;
        return true;
    }

    public bool TryBeginAttack(Unit target)
    {
        if (target.IsDead || !TryBeginAttackPosition(target.Position))
        {
            return false;
        }

        return true;
    }

    public bool TryAttack(Unit target)
    {
        if (!TryBeginAttack(target))
        {
            return false;
        }

        target.TakeDamage(AttackDamage);
        return true;
    }

    public bool IsInAggroRange(Unit target)
    {
        var delta = target.Position - Position;
        return Mathf.Abs(delta.X) <= AggroRangeX && Mathf.Abs(delta.Y) <= AggroRangeY;
    }

    public bool IsInAggroRange(Unit target, float rangeScale)
    {
        var delta = target.Position - Position;
        return Mathf.Abs(delta.X) <= AggroRangeX * rangeScale && Mathf.Abs(delta.Y) <= AggroRangeY * rangeScale;
    }

    public void MoveToward(Vector2 target, float delta, float minX, float maxX, float minY, float maxY)
    {
        var offset = target - Position;
        if (offset.LengthSquared() <= 0.0001f)
        {
            return;
        }

        var effectiveSpeed = Speed * Mathf.Max(0.4f, _currentSpeedScale);
        Position += offset.Normalized() * effectiveSpeed * delta;
        Position = new Vector2(
            Mathf.Clamp(Position.X, minX, maxX),
            Mathf.Clamp(Position.Y, minY, maxY));
    }

    public void Advance(float delta, float minX, float maxX, float minY, float maxY)
    {
        var direction = Team == Team.Player ? 1f : -1f;
        Position += new Vector2(direction * Speed * Mathf.Max(0.4f, _currentSpeedScale) * delta, 0f);
        Position = new Vector2(
            Mathf.Clamp(Position.X, minX, maxX),
            Mathf.Clamp(Position.Y, minY, maxY));
    }

    public float TakeDamage(float damage, string attackerName = null)
    {
        var previousHealth = Health;
        Health -= damage * Mathf.Max(0.05f, DamageTakenScale * _currentDamageTakenScale);
        if (Health < 0f)
        {
            Health = 0f;
        }

        if (!string.IsNullOrEmpty(attackerName))
        {
            LastDamagedBy = attackerName;
        }

        _hitFlashTimer = 0.2f;
        return Mathf.Max(0f, previousHealth - Health);
    }

    public float Heal(float amount)
    {
        if (amount <= 0f || Health >= MaxHealth)
        {
            return 0f;
        }

        var previousHealth = Health;
        Health = Mathf.Min(MaxHealth, Health + amount);
        _auraFlashTimer = Mathf.Max(_auraFlashTimer, 0.18f);
        return Mathf.Max(0f, Health - previousHealth);
    }

    public override void _Process(double delta)
    {
        var deltaF = (float)delta;
        _idleTimer += deltaF;
        _hitFlashTimer = Mathf.Max(0f, _hitFlashTimer - deltaF);
        _attackFlashTimer = Mathf.Max(0f, _attackFlashTimer - deltaF);
        _auraFlashTimer = Mathf.Max(0f, _auraFlashTimer - deltaF);
        _temporaryCombatBuffTimer = Mathf.Max(0f, _temporaryCombatBuffTimer - deltaF);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!_spriteLoadAttempted)
        {
            _spriteLoadAttempted = true;
            _spriteSheet = UnitSpriteLoader.TryLoad(VisualClass);
        }

        // Shadow
        DrawSetTransform(new Vector2(0f, Radius * 0.9f), 0f, new Vector2(1.4f, 0.45f));
        DrawCircle(Vector2.Zero, Radius * 0.82f, new Color(0f, 0f, 0f, 0.18f));
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);

        if (_spriteSheet != null)
        {
            DrawSpriteFrame();
        }
        else
        {
            DrawProceduralUnit();
        }

        DrawHealthBar();
    }

    private void DrawSpriteFrame()
    {
        UpdateSpriteAnimState();

        if (!_spriteSheet.Animations.TryGetValue(_spriteAnimState, out var anim))
        {
            if (!_spriteSheet.Animations.TryGetValue(UnitAnimState.Idle, out anim))
                return;
        }

        _spriteAnimTimer += (float)GetProcessDeltaTime();
        if (_spriteAnimTimer >= anim.FrameDuration)
        {
            _spriteAnimTimer -= anim.FrameDuration;
            _spriteAnimFrame++;
            if (_spriteAnimFrame >= anim.FrameCount)
            {
                _spriteAnimFrame = anim.Loop ? 0 : anim.FrameCount - 1;
            }
        }

        var globalFrame = anim.StartFrame + _spriteAnimFrame;
        var srcRect = UnitSpriteLoader.GetFrameRect(_spriteSheet, globalFrame);

        var facing = GetFacing();
        var drawScale = (Radius * 2f) / _spriteSheet.FrameWidth * VisualScale;
        var bobOffset = Mathf.Sin(_idleTimer * 5f + (Speed * 0.02f)) * Radius * 0.06f;

        var drawSize = new Vector2(_spriteSheet.FrameWidth * drawScale, _spriteSheet.FrameHeight * drawScale);
        var drawPos = new Vector2(-drawSize.X * 0.5f, -drawSize.Y + bobOffset);

        // Flip for facing direction
        if (facing < 0)
        {
            drawPos.X += drawSize.X;
            drawSize.X = -drawSize.X;
        }

        var modulate = Colors.White;
        if (_hitFlashTimer > 0f)
        {
            modulate = modulate.Lerp(Colors.White, Mathf.Clamp(_hitFlashTimer / 0.2f, 0f, 0.6f));
            modulate.R = Mathf.Min(1f, modulate.R + 0.4f);
        }

        DrawTextureRectRegion(_spriteSheet.Texture, new Rect2(drawPos, drawSize), srcRect, modulate);
    }

    private void UpdateSpriteAnimState()
    {
        var prevState = _spriteAnimState;
        var moved = Position.DistanceTo(_prevPosition) > 0.5f;
        _prevPosition = Position;

        if (_hitFlashTimer > 0.05f)
            _spriteAnimState = UnitAnimState.Hit;
        else if (_attackFlashTimer > 0.05f)
            _spriteAnimState = UnitAnimState.Attack;
        else if (moved)
            _spriteAnimState = UnitAnimState.Walk;
        else
            _spriteAnimState = UnitAnimState.Idle;

        if (_spriteAnimState != prevState)
        {
            _spriteAnimFrame = 0;
            _spriteAnimTimer = 0f;
        }
    }

    private void DrawProceduralUnit()
    {
        var bodyColor = ResolveBodyColor();
        var accentColor = bodyColor.Lightened(0.2f);
        var detailColor = bodyColor.Darkened(0.3f);
        var flashStrength = Mathf.Clamp(_attackFlashTimer / 0.14f, 0f, 1f);
        var bobOffset = Mathf.Sin(_idleTimer * 5f + (Speed * 0.02f)) * Radius * 0.06f;

        DrawSetTransform(new Vector2(0f, bobOffset), 0f, Vector2.One);

        if (GameState.Instance != null && GameState.Instance.HighContrast)
        {
            var outlineColor = Team == Team.Player ? new Color("80ed99") : new Color("ef476f");
            DrawCircle(new Vector2(0f, -Radius * 0.3f), Radius * 0.72f, outlineColor);
        }

        DrawUnitSilhouette(bodyColor, accentColor, detailColor, flashStrength);
        DrawAuraIndicators(accentColor);
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }

    private void DrawUnitSilhouette(Color bodyColor, Color accentColor, Color detailColor, float flashStrength)
    {
        switch (VisualClass)
        {
            case "shield":
                DrawShieldUnit(bodyColor, accentColor, detailColor, flashStrength);
                break;
            case "gunner":
            case "marksman":
            case "sniper":
            case "support":
                DrawGunnerUnit(bodyColor, accentColor, detailColor, flashStrength, VisualClass == "sniper");
                break;
            case "spitter":
                DrawSpitterUnit(bodyColor, accentColor, detailColor, flashStrength);
                break;
            case "howler":
                DrawHowlerUnit(bodyColor, accentColor, detailColor, flashStrength);
                break;
            case "jammer":
                DrawJammerUnit(bodyColor, accentColor, detailColor, flashStrength);
                break;
            case "saboteur":
            case "skirmisher":
            case "runner":
                DrawSkirmisherUnit(bodyColor, accentColor, detailColor, flashStrength);
                break;
            case "bloater":
                DrawBloaterUnit(bodyColor, accentColor, detailColor, flashStrength);
                break;
            case "brute":
            case "crusher":
                DrawHeavyUnit(bodyColor, accentColor, detailColor, flashStrength, VisualClass == "crusher");
                break;
            case "boss":
                DrawBossUnit(bodyColor, accentColor, detailColor, flashStrength);
                break;
            case "hound":
                DrawHoundUnit(bodyColor, accentColor, detailColor, flashStrength);
                break;
            case "banner":
                DrawBannerUnit(bodyColor, accentColor, detailColor, flashStrength);
                break;
            case "necromancer":
                DrawNecromancerUnit(bodyColor, accentColor, detailColor, flashStrength);
                break;
            case "berserker":
                DrawBerserkerUnit(bodyColor, accentColor, detailColor, flashStrength);
                break;
            case "siegetower":
                DrawSiegeTowerUnit(bodyColor, accentColor, detailColor, flashStrength);
                break;
            case "mirror":
                DrawMirrorUnit(bodyColor, accentColor, detailColor, flashStrength);
                break;
            case "splitter":
                DrawWalkerUnit(bodyColor, accentColor, detailColor, flashStrength, true);
                break;
            case "walker":
            case "fighter":
            default:
                DrawWalkerUnit(bodyColor, accentColor, detailColor, flashStrength, false);
                break;
        }
    }

    private void DrawWalkerUnit(Color bodyColor, Color accentColor, Color detailColor, float flashStrength, bool splitMark)
    {
        var facing = GetFacing();
        DrawCircle(new Vector2(0f, -Radius * 0.16f), Radius * 0.58f, bodyColor);
        DrawCircle(new Vector2(facing * Radius * 0.18f, -Radius * 0.9f), Radius * 0.34f, accentColor);
        DrawRect(
            new Rect2(-Radius * 0.32f, -Radius * 0.1f, Radius * 0.64f, Radius * 0.78f),
            detailColor,
            true);
        DrawLine(
            new Vector2(-Radius * 0.22f, Radius * 0.44f),
            new Vector2(-Radius * 0.38f, Radius * 0.96f),
            detailColor.Darkened(0.1f),
            3f,
            true);
        DrawLine(
            new Vector2(Radius * 0.22f, Radius * 0.44f),
            new Vector2(Radius * 0.38f, Radius * 0.96f),
            detailColor.Darkened(0.1f),
            3f,
            true);

        if (splitMark)
        {
            DrawCircle(new Vector2(0f, -Radius * 0.1f), Radius * 0.18f, accentColor.Lightened(0.15f));
        }

        if (flashStrength > 0.05f)
        {
            DrawLine(
                new Vector2(facing * Radius * 0.3f, -Radius * 0.38f),
                new Vector2(facing * Radius * 0.82f, -Radius * 0.18f),
                accentColor.Lightened(0.55f),
                3f + flashStrength * 3f,
                true);
        }
    }

    private void DrawShieldUnit(Color bodyColor, Color accentColor, Color detailColor, float flashStrength)
    {
        var facing = GetFacing();
        DrawRect(new Rect2(-Radius * 0.38f, -Radius * 0.2f, Radius * 0.76f, Radius * 0.9f), bodyColor, true);
        DrawCircle(new Vector2(0f, -Radius * 0.86f), Radius * 0.3f, accentColor);
        DrawFacingRect(facing * Radius * 0.12f, -Radius * 0.44f, facing * Radius * 0.72f, Radius * 1.1f, detailColor);
        DrawFacingRect(facing * Radius * 0.22f, -Radius * 0.22f, facing * Radius * 0.54f, Radius * 0.18f, accentColor.Darkened(0.15f));

        if (flashStrength > 0.05f)
        {
            DrawArc(
                new Vector2(facing * Radius * 0.44f, Radius * 0.08f),
                Radius * 0.56f,
                -0.95f,
                0.95f,
                18,
                accentColor.Lightened(0.55f),
                3f);
        }
    }

    private void DrawGunnerUnit(Color bodyColor, Color accentColor, Color detailColor, float flashStrength, bool sniper)
    {
        var facing = GetFacing();
        DrawCircle(new Vector2(-Radius * 0.05f, -Radius * 0.28f), Radius * 0.5f, bodyColor);
        DrawCircle(new Vector2(-Radius * 0.08f, -Radius * 0.9f), Radius * 0.26f, accentColor);
        DrawRect(new Rect2(-Radius * 0.16f, -Radius * 0.08f, Radius * 0.44f, Radius * 0.78f), detailColor, true);

        var rifleLength = sniper ? Radius * 1.35f : Radius * 1.08f;
        var rifleWidth = sniper ? Radius * 0.16f : Radius * 0.2f;
        DrawFacingRect(facing * Radius * 0.1f, -Radius * 0.52f, facing * rifleLength, rifleWidth, accentColor.Darkened(0.2f));
        DrawRect(new Rect2(-Radius * 0.44f, -Radius * 0.36f, Radius * 0.22f, Radius * 0.46f), accentColor, true);

        if (flashStrength > 0.05f)
        {
            DrawCircle(
                new Vector2(facing * (Radius * 0.1f + rifleLength), -Radius * 0.42f),
                Radius * (sniper ? 0.16f : 0.22f) * (1f + flashStrength),
                accentColor.Lightened(0.6f));
        }
    }

    private void DrawSpitterUnit(Color bodyColor, Color accentColor, Color detailColor, float flashStrength)
    {
        var facing = GetFacing();
        DrawCircle(new Vector2(0f, -Radius * 0.2f), Radius * 0.56f, bodyColor);
        DrawCircle(new Vector2(-Radius * 0.08f, -Radius * 0.82f), Radius * 0.28f, accentColor);
        DrawCircle(new Vector2(facing * Radius * 0.36f, -Radius * 0.64f), Radius * 0.22f, detailColor);
        DrawRect(new Rect2(-Radius * 0.18f, -Radius * 0.08f, Radius * 0.36f, Radius * 0.78f), detailColor, true);

        if (flashStrength > 0.05f)
        {
            DrawCircle(
                new Vector2(facing * Radius * 0.84f, -Radius * 0.6f),
                Radius * 0.22f * (1f + flashStrength),
                accentColor.Lightened(0.55f));
        }
    }

    private void DrawHowlerUnit(Color bodyColor, Color accentColor, Color detailColor, float flashStrength)
    {
        var facing = GetFacing();
        DrawCircle(new Vector2(0f, -Radius * 0.18f), Radius * 0.58f, bodyColor);
        DrawCircle(new Vector2(0f, -Radius * 0.92f), Radius * 0.26f, accentColor);
        DrawRect(new Rect2(-Radius * 0.22f, -Radius * 0.08f, Radius * 0.44f, Radius * 0.82f), detailColor, true);
        DrawLine(
            new Vector2(-Radius * 0.22f, -Radius * 1.02f),
            new Vector2(-Radius * 0.46f, -Radius * 1.34f),
            accentColor,
            3f,
            true);
        DrawLine(
            new Vector2(Radius * 0.22f, -Radius * 1.02f),
            new Vector2(Radius * 0.46f, -Radius * 1.34f),
            accentColor,
            3f,
            true);
        DrawFacingRect(facing * Radius * 0.08f, -Radius * 0.38f, facing * Radius * 0.52f, Radius * 0.18f, accentColor.Darkened(0.15f));

        if (flashStrength > 0.05f)
        {
            DrawArc(
                new Vector2(facing * Radius * 0.42f, -Radius * 0.56f),
                Radius * 0.48f,
                -0.8f,
                0.8f,
                16,
                accentColor.Lightened(0.6f),
                3f);
        }
    }

    private void DrawJammerUnit(Color bodyColor, Color accentColor, Color detailColor, float flashStrength)
    {
        var facing = GetFacing();
        DrawCircle(new Vector2(0f, -Radius * 0.22f), Radius * 0.5f, bodyColor);
        DrawCircle(new Vector2(0f, -Radius * 0.9f), Radius * 0.24f, accentColor);
        DrawRect(new Rect2(-Radius * 0.2f, -Radius * 0.08f, Radius * 0.4f, Radius * 0.8f), detailColor, true);
        DrawRect(new Rect2(-Radius * 0.5f, -Radius * 0.16f, Radius * 0.22f, Radius * 0.54f), accentColor.Darkened(0.15f), true);
        DrawLine(
            new Vector2(Radius * 0.1f, -Radius * 0.62f),
            new Vector2(Radius * 0.48f, -Radius * 1.28f),
            accentColor,
            3f,
            true);
        DrawArc(
            new Vector2(Radius * 0.5f, -Radius * 1.34f),
            Radius * 0.22f,
            0.4f,
            2.7f,
            10,
            accentColor.Lightened(0.1f),
            2f);
        DrawFacingRect(facing * Radius * 0.08f, -Radius * 0.34f, facing * Radius * 0.54f, Radius * 0.16f, accentColor.Darkened(0.22f));

        if (flashStrength > 0.05f)
        {
            DrawArc(
                new Vector2(0f, -Radius * 0.32f),
                Radius * 0.84f,
                -0.5f,
                0.5f,
                14,
                accentColor.Lightened(0.6f),
                3f);
        }
    }

    private void DrawSkirmisherUnit(Color bodyColor, Color accentColor, Color detailColor, float flashStrength)
    {
        var facing = GetFacing();
        DrawLine(
            new Vector2(-facing * Radius * 0.24f, Radius * 0.2f),
            new Vector2(facing * Radius * 0.44f, -Radius * 0.56f),
            bodyColor,
            Radius * 0.5f,
            true);
        DrawCircle(new Vector2(facing * Radius * 0.34f, -Radius * 0.92f), Radius * 0.24f, accentColor);
        DrawLine(
            new Vector2(-Radius * 0.12f, Radius * 0.4f),
            new Vector2(-Radius * 0.42f, Radius * 0.96f),
            detailColor,
            3f,
            true);
        DrawLine(
            new Vector2(Radius * 0.1f, Radius * 0.28f),
            new Vector2(Radius * 0.32f, Radius * 0.96f),
            detailColor,
            3f,
            true);

        if (VisualClass == "saboteur")
        {
            DrawRect(new Rect2(-Radius * 0.08f, -Radius * 0.24f, Radius * 0.36f, Radius * 0.28f), accentColor, true);
        }

        if (flashStrength > 0.05f)
        {
            DrawLine(
                new Vector2(facing * Radius * 0.18f, -Radius * 0.44f),
                new Vector2(facing * Radius * 0.84f, -Radius * 0.6f),
                accentColor.Lightened(0.55f),
                4f,
                true);
        }
    }

    private void DrawBloaterUnit(Color bodyColor, Color accentColor, Color detailColor, float flashStrength)
    {
        DrawCircle(new Vector2(0f, Radius * 0.02f), Radius * 0.72f, bodyColor);
        DrawCircle(new Vector2(0f, -Radius * 0.88f), Radius * 0.24f, accentColor);
        DrawCircle(new Vector2(0f, Radius * 0.18f), Radius * 0.3f, detailColor);

        if (flashStrength > 0.05f)
        {
            DrawArc(
                new Vector2(0f, Radius * 0.04f),
                Radius * 0.94f,
                0f,
                Mathf.Tau,
                28,
                accentColor.Lightened(0.55f),
                3f);
        }
    }

    private void DrawHeavyUnit(Color bodyColor, Color accentColor, Color detailColor, float flashStrength, bool armored)
    {
        var facing = GetFacing();
        DrawRect(new Rect2(-Radius * 0.56f, -Radius * 0.34f, Radius * 1.12f, Radius * 0.96f), bodyColor, true);
        DrawRect(new Rect2(-Radius * 0.78f, -Radius * 0.14f, Radius * 0.28f, Radius * 0.36f), accentColor, true);
        DrawRect(new Rect2(Radius * 0.5f, -Radius * 0.14f, Radius * 0.28f, Radius * 0.36f), accentColor, true);
        DrawCircle(new Vector2(0f, -Radius * 0.94f), Radius * 0.28f, detailColor.Lightened(0.1f));

        if (armored)
        {
            DrawFacingRect(facing * Radius * 0.08f, -Radius * 0.22f, facing * Radius * 0.52f, Radius * 0.56f, detailColor);
        }

        if (flashStrength > 0.05f)
        {
            DrawCircle(
                new Vector2(facing * Radius * 0.78f, -Radius * 0.06f),
                Radius * 0.2f * (1f + flashStrength),
                accentColor.Lightened(0.55f));
        }
    }

    private void DrawBossUnit(Color bodyColor, Color accentColor, Color detailColor, float flashStrength)
    {
        DrawRect(new Rect2(-Radius * 0.6f, -Radius * 0.46f, Radius * 1.2f, Radius * 1.24f), bodyColor, true);
        DrawCircle(new Vector2(0f, -Radius * 1.02f), Radius * 0.34f, accentColor);
        DrawCircle(new Vector2(0f, -Radius * 0.06f), Radius * 0.28f, accentColor.Lightened(0.18f));
        DrawLine(new Vector2(-Radius * 0.44f, -Radius * 1.12f), new Vector2(-Radius * 0.72f, -Radius * 1.42f), detailColor, 4f, true);
        DrawLine(new Vector2(0f, -Radius * 1.18f), new Vector2(0f, -Radius * 1.52f), detailColor, 4f, true);
        DrawLine(new Vector2(Radius * 0.44f, -Radius * 1.12f), new Vector2(Radius * 0.72f, -Radius * 1.42f), detailColor, 4f, true);
        DrawRect(new Rect2(-Radius * 0.86f, -Radius * 0.12f, Radius * 0.26f, Radius * 0.42f), detailColor, true);
        DrawRect(new Rect2(Radius * 0.6f, -Radius * 0.12f, Radius * 0.26f, Radius * 0.42f), detailColor, true);

        if (flashStrength > 0.05f)
        {
            DrawArc(
                new Vector2(0f, -Radius * 0.06f),
                Radius * 0.7f,
                0f,
                Mathf.Tau,
                32,
                accentColor.Lightened(0.6f),
                4f);
        }
    }

    private void DrawHoundUnit(Color bodyColor, Color accentColor, Color detailColor, float flashStrength)
    {
        var facing = GetFacing();
        // Low crouching body
        DrawLine(
            new Vector2(-facing * Radius * 0.44f, -Radius * 0.08f),
            new Vector2(facing * Radius * 0.52f, -Radius * 0.26f),
            bodyColor,
            Radius * 0.46f,
            true);
        // Head
        DrawCircle(new Vector2(facing * Radius * 0.56f, -Radius * 0.52f), Radius * 0.28f, accentColor);
        // Ears
        DrawLine(
            new Vector2(facing * Radius * 0.48f, -Radius * 0.68f),
            new Vector2(facing * Radius * 0.32f, -Radius * 1.02f),
            accentColor,
            2.5f,
            true);
        // Legs
        DrawLine(new Vector2(-Radius * 0.28f, Radius * 0.08f), new Vector2(-Radius * 0.36f, Radius * 0.72f), detailColor, 2.5f, true);
        DrawLine(new Vector2(Radius * 0.14f, Radius * 0.08f), new Vector2(Radius * 0.24f, Radius * 0.72f), detailColor, 2.5f, true);
        // Tail
        DrawLine(new Vector2(-facing * Radius * 0.44f, -Radius * 0.18f), new Vector2(-facing * Radius * 0.78f, -Radius * 0.48f), detailColor, 2f, true);

        if (flashStrength > 0.05f)
        {
            DrawCircle(
                new Vector2(facing * Radius * 0.72f, -Radius * 0.42f),
                Radius * 0.18f * (1f + flashStrength),
                accentColor.Lightened(0.55f));
        }
    }

    private void DrawBannerUnit(Color bodyColor, Color accentColor, Color detailColor, float flashStrength)
    {
        var facing = GetFacing();
        // Body
        DrawRect(new Rect2(-Radius * 0.34f, -Radius * 0.2f, Radius * 0.68f, Radius * 0.88f), bodyColor, true);
        // Head
        DrawCircle(new Vector2(0f, -Radius * 0.82f), Radius * 0.3f, accentColor);
        // Banner pole
        DrawLine(
            new Vector2(-facing * Radius * 0.28f, -Radius * 0.14f),
            new Vector2(-facing * Radius * 0.28f, -Radius * 1.52f),
            detailColor.Darkened(0.15f),
            3f,
            true);
        // Banner flag
        DrawRect(
            new Rect2(
                -facing * Radius * 0.28f, -Radius * 1.52f,
                -facing * Radius * 0.52f, Radius * 0.42f),
            accentColor.Lightened(0.12f),
            true);
        // Legs
        DrawLine(new Vector2(-Radius * 0.18f, Radius * 0.44f), new Vector2(-Radius * 0.32f, Radius * 0.96f), detailColor, 3f, true);
        DrawLine(new Vector2(Radius * 0.18f, Radius * 0.44f), new Vector2(Radius * 0.32f, Radius * 0.96f), detailColor, 3f, true);

        if (flashStrength > 0.05f)
        {
            DrawArc(Vector2.Zero, Radius * 0.64f, 0f, Mathf.Tau, 18, accentColor.Lightened(0.55f), 3f);
        }
    }

    private void DrawNecromancerUnit(Color bodyColor, Color accentColor, Color detailColor, float flashStrength)
    {
        var facing = GetFacing();
        // Robed body (triangle-ish)
        DrawLine(
            new Vector2(0f, -Radius * 0.56f),
            new Vector2(-Radius * 0.42f, Radius * 0.62f),
            bodyColor,
            Radius * 0.52f,
            true);
        DrawLine(
            new Vector2(0f, -Radius * 0.56f),
            new Vector2(Radius * 0.42f, Radius * 0.62f),
            bodyColor,
            Radius * 0.52f,
            true);
        // Hooded head
        DrawCircle(new Vector2(0f, -Radius * 0.84f), Radius * 0.32f, accentColor.Darkened(0.2f));
        DrawCircle(new Vector2(0f, -Radius * 0.78f), Radius * 0.2f, detailColor.Lightened(0.1f));
        // Staff
        DrawLine(
            new Vector2(facing * Radius * 0.24f, -Radius * 0.2f),
            new Vector2(facing * Radius * 0.36f, -Radius * 1.34f),
            detailColor,
            3f,
            true);
        DrawCircle(new Vector2(facing * Radius * 0.36f, -Radius * 1.42f), Radius * 0.14f, accentColor.Lightened(0.2f));

        if (flashStrength > 0.05f)
        {
            DrawCircle(
                new Vector2(facing * Radius * 0.36f, -Radius * 1.42f),
                Radius * 0.24f * (1f + flashStrength),
                accentColor.Lightened(0.5f));
        }
    }

    private void DrawBerserkerUnit(Color bodyColor, Color accentColor, Color detailColor, float flashStrength)
    {
        var facing = GetFacing();
        var rageGlow = 1f - HealthRatio;
        var rageColor = bodyColor.Lerp(new Color("ff2222"), rageGlow * 0.6f);
        // Broad torso
        DrawRect(new Rect2(-Radius * 0.46f, -Radius * 0.28f, Radius * 0.92f, Radius * 0.84f), rageColor, true);
        // Head
        DrawCircle(new Vector2(0f, -Radius * 0.88f), Radius * 0.3f, accentColor);
        // Arms (wide stance)
        DrawLine(new Vector2(-Radius * 0.46f, -Radius * 0.08f), new Vector2(-Radius * 0.82f, Radius * 0.14f), detailColor, 4f, true);
        DrawLine(new Vector2(Radius * 0.46f, -Radius * 0.08f), new Vector2(Radius * 0.82f, Radius * 0.14f), detailColor, 4f, true);
        // Legs
        DrawLine(new Vector2(-Radius * 0.22f, Radius * 0.42f), new Vector2(-Radius * 0.34f, Radius * 0.96f), detailColor, 3f, true);
        DrawLine(new Vector2(Radius * 0.22f, Radius * 0.42f), new Vector2(Radius * 0.34f, Radius * 0.96f), detailColor, 3f, true);
        // Rage indicator when low health
        if (rageGlow > 0.15f)
        {
            DrawArc(Vector2.Zero, Radius * 1.1f, 0f, Mathf.Tau, 20, new Color(1f, 0.2f, 0.1f, rageGlow * 0.35f), 3f);
        }

        if (flashStrength > 0.05f)
        {
            DrawLine(
                new Vector2(facing * Radius * 0.5f, -Radius * 0.32f),
                new Vector2(facing * Radius * 1.08f, -Radius * 0.12f),
                accentColor.Lightened(0.55f),
                4f + flashStrength * 3f,
                true);
        }
    }

    private void DrawSiegeTowerUnit(Color bodyColor, Color accentColor, Color detailColor, float flashStrength)
    {
        // Tall rectangular tower
        DrawRect(new Rect2(-Radius * 0.52f, -Radius * 1.2f, Radius * 1.04f, Radius * 2.1f), bodyColor, true);
        // Horizontal planks
        DrawRect(new Rect2(-Radius * 0.48f, -Radius * 0.6f, Radius * 0.96f, Radius * 0.12f), detailColor, true);
        DrawRect(new Rect2(-Radius * 0.48f, Radius * 0.0f, Radius * 0.96f, Radius * 0.12f), detailColor, true);
        DrawRect(new Rect2(-Radius * 0.48f, Radius * 0.6f, Radius * 0.96f, Radius * 0.12f), detailColor, true);
        // Battlement top
        DrawRect(new Rect2(-Radius * 0.56f, -Radius * 1.28f, Radius * 0.3f, Radius * 0.22f), accentColor, true);
        DrawRect(new Rect2(Radius * 0.26f, -Radius * 1.28f, Radius * 0.3f, Radius * 0.22f), accentColor, true);
        // Wheels
        DrawCircle(new Vector2(-Radius * 0.34f, Radius * 0.88f), Radius * 0.16f, detailColor.Darkened(0.2f));
        DrawCircle(new Vector2(Radius * 0.34f, Radius * 0.88f), Radius * 0.16f, detailColor.Darkened(0.2f));

        if (flashStrength > 0.05f)
        {
            DrawRect(new Rect2(-Radius * 0.56f, -Radius * 1.32f, Radius * 1.12f, Radius * 0.08f), accentColor.Lightened(0.55f), true);
        }
    }

    private void DrawMirrorUnit(Color bodyColor, Color accentColor, Color detailColor, float flashStrength)
    {
        var facing = GetFacing();
        // Body
        DrawRect(new Rect2(-Radius * 0.36f, -Radius * 0.2f, Radius * 0.72f, Radius * 0.88f), bodyColor, true);
        // Head
        DrawCircle(new Vector2(0f, -Radius * 0.82f), Radius * 0.3f, accentColor);
        // Mirror shield (reflective, lighter)
        var shieldColor = accentColor.Lightened(0.35f);
        DrawFacingRect(facing * Radius * 0.14f, -Radius * 0.48f, facing * Radius * 0.58f, Radius * 0.96f, shieldColor);
        DrawFacingRect(facing * Radius * 0.24f, -Radius * 0.28f, facing * Radius * 0.36f, Radius * 0.16f, new Color(1f, 1f, 1f, 0.45f));
        // Legs
        DrawLine(new Vector2(-Radius * 0.18f, Radius * 0.44f), new Vector2(-Radius * 0.32f, Radius * 0.96f), detailColor, 3f, true);
        DrawLine(new Vector2(Radius * 0.18f, Radius * 0.44f), new Vector2(Radius * 0.32f, Radius * 0.96f), detailColor, 3f, true);

        if (flashStrength > 0.05f)
        {
            DrawArc(
                new Vector2(facing * Radius * 0.38f, Radius * 0.04f),
                Radius * 0.52f,
                -0.9f,
                0.9f,
                16,
                new Color(1f, 1f, 1f, 0.6f),
                3f);
        }
    }

    private void DrawHealthBar()
    {
        var highContrast = GameState.Instance != null && GameState.Instance.HighContrast;
        var hpBarWidth = Radius * (highContrast ? 2.5f : 2.15f);
        var hpBarHeight = highContrast ? 7f : 5f;
        var hpRatio = Mathf.Clamp(Health / MaxHealth, 0f, 1f);
        var barOrigin = new Vector2(-hpBarWidth * 0.5f, -Radius - 16f);

        DrawRect(new Rect2(barOrigin, new Vector2(hpBarWidth, hpBarHeight)), new Color(0f, 0f, 0f, highContrast ? 0.8f : 0.55f), true);
        var hpColor = Team == Team.Player ? new Color("80ed99") : new Color("ef476f");
        DrawRect(new Rect2(barOrigin, new Vector2(hpBarWidth * hpRatio, hpBarHeight)), hpColor, true);

        if (Team == Team.Player)
        {
            DrawRect(
                new Rect2(new Vector2(-Radius * 0.44f, Radius * 1.04f), new Vector2(Radius * 0.88f, 3f)),
                new Color(1f, 1f, 1f, 0.32f),
                true);
        }
    }

    private void DrawAuraIndicators(Color accentColor)
    {
        if (ProvidesAura)
        {
            var auraAlpha = 0.05f + (Mathf.Sin(_idleTimer * 2.4f) * 0.015f);
            var auraColor = new Color(accentColor.Lightened(0.12f), Mathf.Clamp(auraAlpha, 0.03f, 0.08f));
            DrawArc(Vector2.Zero, AuraRadius, 0f, Mathf.Tau, 36, auraColor, 2f);
        }

        if (!_hasAuraBuff && _auraFlashTimer <= 0.01f)
        {
            return;
        }

        var flashStrength = _hasAuraBuff
            ? 0.28f + (Mathf.Sin(_idleTimer * 8f) * 0.05f)
            : Mathf.Clamp(_auraFlashTimer / 0.18f, 0f, 1f) * 0.28f;
        DrawArc(
            Vector2.Zero,
            Radius * 1.18f,
            0f,
            Mathf.Tau,
            20,
            new Color(accentColor.Lightened(0.25f), Mathf.Clamp(flashStrength, 0.1f, 0.34f)),
            2f);
    }

    private float ResolveBerserkerRageScale()
    {
        if (!string.Equals(SpecialAbilityId, "berserk_rage", StringComparison.OrdinalIgnoreCase))
        {
            return 1f;
        }

        // At full health: 1.0x damage. At 0 health: up to 1.8x damage (capped to limit multiplicative stacking with Berserker Blood boon).
        var missingRatio = 1f - HealthRatio;
        return 1f + (missingRatio * 0.8f);
    }

    private float ResolveRadius(UnitStats stats)
    {
        var radius = 14f * stats.VisualScale;
        return stats.VisualClass switch
        {
            "boss" => radius + 6f,
            "siegetower" => radius + 6f,
            "bloater" => radius + 3f,
            "crusher" => radius + 3f,
            "brute" => radius + 2f,
            "banner" => radius + 1f,
            "berserker" => radius + 1f,
            "howler" => radius + 1f,
            "mirror" => radius + 1f,
            "sniper" => radius - 1f,
            "runner" => radius - 1f,
            "hound" => radius - 2f,
            _ => radius
        };
    }

    private Color ResolveBodyColor()
    {
        var baseColor = _bodyColor;

        if (GameState.Instance != null && GameState.Instance.HighContrast)
        {
            baseColor = Team == Team.Player
                ? baseColor.Lightened(0.25f)
                : baseColor.Darkened(0.15f);
        }

        if (_hitFlashTimer <= 0f)
        {
            return baseColor;
        }

        var flashStrength = Mathf.Clamp(_hitFlashTimer / 0.2f, 0f, 1f);
        return baseColor.Lerp(Colors.White, 0.2f + (flashStrength * 0.45f));
    }

    private float GetFacing()
    {
        return Team == Team.Player ? 1f : -1f;
    }

    private void DrawFacingRect(float x, float y, float width, float height, Color color)
    {
        if (width >= 0f)
        {
            DrawRect(new Rect2(x, y, width, height), color, true);
            return;
        }

        DrawRect(new Rect2(x + width, y, -width, height), color, true);
    }
}
