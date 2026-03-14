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
        int baseDamageBonus = 0)
    {
        Name = definition.DisplayName;
        MaxHealth = definition.MaxHealth * healthScale;
        Speed = definition.Speed;
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
    }

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
    public Color Color { get; }

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
            "crusher" => 1.25f,
            "brute" => 1.18f,
            "bloater" => 1.22f,
            "saboteur" => 0.92f,
            "howler" => 1.04f,
            "jammer" => 0.98f,
            "shield" => 1.08f,
            "sniper" => 0.94f,
            "runner" => 0.92f,
            _ => 1f
        };
    }
}

public partial class Unit : Node2D
{
    public Team Team { get; private set; }
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
    public string VisualClass { get; private set; } = "fighter";
    public float VisualScale { get; private set; } = 1f;
    public float Radius { get; private set; } = 16f;
    public Color Tint => _bodyColor;
    public float CurrentAttackDamage => AttackDamage * _currentAttackDamageScale;
    public bool ProvidesAura => AuraRadius > 0f && (AuraAttackDamageScale > 1.01f || AuraSpeedScale > 1.01f);
    public bool HasSpecialAbility => !string.IsNullOrWhiteSpace(SpecialAbilityId) && SpecialCooldown > 0.05f;
    public float HealthRatio => MaxHealth <= 0.01f ? 0f : Mathf.Clamp(Health / MaxHealth, 0f, 1f);

    public bool IsDead => Health <= 0f;

    private float _attackTimer;
    private float _specialTimer;
    private float _hitFlashTimer;
    private float _attackFlashTimer;
    private float _auraFlashTimer;
    private float _idleTimer;
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
        _specialTimer = HasSpecialAbility
            ? Mathf.Max(3f, SpecialCooldown * 0.55f)
            : 0f;
        Position = startPosition;
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

    public bool CanAttack(Unit target)
    {
        if (target.IsDead)
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

    public float TakeDamage(float damage)
    {
        var previousHealth = Health;
        Health -= damage * Mathf.Max(0.05f, DamageTakenScale * _currentDamageTakenScale);
        if (Health < 0f)
        {
            Health = 0f;
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
        DrawSetTransform(new Vector2(0f, Radius * 0.9f), 0f, new Vector2(1.4f, 0.45f));
        DrawCircle(Vector2.Zero, Radius * 0.82f, new Color(0f, 0f, 0f, 0.18f));
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);

        var bodyColor = ResolveBodyColor();
        var accentColor = bodyColor.Lightened(0.2f);
        var detailColor = bodyColor.Darkened(0.3f);
        var flashStrength = Mathf.Clamp(_attackFlashTimer / 0.14f, 0f, 1f);
        var bobOffset = Mathf.Sin(_idleTimer * 5f + (Speed * 0.02f)) * Radius * 0.06f;

        DrawSetTransform(new Vector2(0f, bobOffset), 0f, Vector2.One);
        DrawUnitSilhouette(bodyColor, accentColor, detailColor, flashStrength);
        DrawAuraIndicators(accentColor);
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);

        DrawHealthBar();
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

    private void DrawHealthBar()
    {
        var hpBarWidth = Radius * 2.15f;
        var hpRatio = Mathf.Clamp(Health / MaxHealth, 0f, 1f);
        var barOrigin = new Vector2(-hpBarWidth * 0.5f, -Radius - 16f);

        DrawRect(new Rect2(barOrigin, new Vector2(hpBarWidth, 5f)), new Color(0f, 0f, 0f, 0.55f), true);
        DrawRect(new Rect2(barOrigin, new Vector2(hpBarWidth * hpRatio, 5f)), new Color("80ed99"), true);

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

    private float ResolveRadius(UnitStats stats)
    {
        var radius = 14f * stats.VisualScale;
        return stats.VisualClass switch
        {
            "boss" => radius + 6f,
            "bloater" => radius + 3f,
            "crusher" => radius + 3f,
            "brute" => radius + 2f,
            "howler" => radius + 1f,
            "sniper" => radius - 1f,
            "runner" => radius - 1f,
            _ => radius
        };
    }

    private Color ResolveBodyColor()
    {
        if (_hitFlashTimer <= 0f)
        {
            return _bodyColor;
        }

        var flashStrength = Mathf.Clamp(_hitFlashTimer / 0.2f, 0f, 1f);
        return _bodyColor.Lerp(Colors.White, 0.2f + (flashStrength * 0.45f));
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
