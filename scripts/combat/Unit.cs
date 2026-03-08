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
        UsesProjectile = definition.UsesProjectile;
        ProjectileSpeed = definition.ProjectileSpeed;
        AggroRangeX = Mathf.Max(AttackRange, definition.AggroRangeX);
        AggroRangeY = definition.AggroRangeY;
        BaseDamage = definition.BaseDamage + baseDamageBonus;
        DamageTakenScale = definition.DamageTakenScale;
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
    public bool UsesProjectile { get; }
    public float ProjectileSpeed { get; }
    public float AggroRangeX { get; }
    public float AggroRangeY { get; }
    public int BaseDamage { get; }
    public float DamageTakenScale { get; }
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

        if (definition.SpawnOnDeathCount > 0)
        {
            return "splitter";
        }

        if (definition.DamageTakenScale < 0.9f && definition.MaxHealth >= 250f)
        {
            return "boss";
        }

        if (definition.DisplayName.Contains("Defender"))
        {
            return "shield";
        }

        if (definition.DisplayName.Contains("Marksman"))
        {
            return "sniper";
        }

        if (definition.DisplayName.Contains("Ranger") || definition.DisplayName.Contains("Shooter"))
        {
            return "gunner";
        }

        if (definition.DisplayName.Contains("Raider"))
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
    public bool UsesProjectile { get; private set; }
    public float ProjectileSpeed { get; private set; }
    public float AggroRangeX { get; private set; }
    public float AggroRangeY { get; private set; }
    public int BaseDamage { get; private set; }
    public float DamageTakenScale { get; private set; } = 1f;
    public float DeathBurstDamage { get; private set; }
    public float DeathBurstRadius { get; private set; }
    public string SpawnOnDeathUnitId { get; private set; } = "";
    public int SpawnOnDeathCount { get; private set; }
    public string VisualClass { get; private set; } = "fighter";
    public float VisualScale { get; private set; } = 1f;
    public float Radius { get; private set; } = 16f;
    public Color Tint => _bodyColor;

    public bool IsDead => Health <= 0f;

    private float _attackTimer;
    private float _hitFlashTimer;
    private float _attackFlashTimer;
    private float _idleTimer;
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
        UsesProjectile = stats.UsesProjectile;
        ProjectileSpeed = stats.ProjectileSpeed;
        AggroRangeX = stats.AggroRangeX;
        AggroRangeY = stats.AggroRangeY;
        BaseDamage = stats.BaseDamage;
        DamageTakenScale = stats.DamageTakenScale;
        DeathBurstDamage = stats.DeathBurstDamage;
        DeathBurstRadius = stats.DeathBurstRadius;
        SpawnOnDeathUnitId = stats.SpawnOnDeathUnitId;
        SpawnOnDeathCount = stats.SpawnOnDeathCount;
        VisualClass = stats.VisualClass;
        VisualScale = stats.VisualScale;
        Radius = ResolveRadius(stats);
        _bodyColor = stats.Color;
        Position = startPosition;
    }

    public void TickAttackTimer(float delta)
    {
        if (_attackTimer > 0f)
        {
            _attackTimer -= delta;
        }
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

        Position += offset.Normalized() * Speed * delta;
        Position = new Vector2(
            Mathf.Clamp(Position.X, minX, maxX),
            Mathf.Clamp(Position.Y, minY, maxY));
    }

    public void Advance(float delta, float minX, float maxX, float minY, float maxY)
    {
        var direction = Team == Team.Player ? 1f : -1f;
        Position += new Vector2(direction * Speed * delta, 0f);
        Position = new Vector2(
            Mathf.Clamp(Position.X, minX, maxX),
            Mathf.Clamp(Position.Y, minY, maxY));
    }

    public float TakeDamage(float damage)
    {
        var previousHealth = Health;
        Health -= damage * Mathf.Max(0.05f, DamageTakenScale);
        if (Health < 0f)
        {
            Health = 0f;
        }

        _hitFlashTimer = 0.2f;
        return Mathf.Max(0f, previousHealth - Health);
    }

    public override void _Process(double delta)
    {
        var deltaF = (float)delta;
        _idleTimer += deltaF;
        _hitFlashTimer = Mathf.Max(0f, _hitFlashTimer - deltaF);
        _attackFlashTimer = Mathf.Max(0f, _attackFlashTimer - deltaF);
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
                DrawGunnerUnit(bodyColor, accentColor, detailColor, flashStrength, VisualClass == "sniper");
                break;
            case "spitter":
                DrawSpitterUnit(bodyColor, accentColor, detailColor, flashStrength);
                break;
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

    private float ResolveRadius(UnitStats stats)
    {
        var radius = 14f * stats.VisualScale;
        return stats.VisualClass switch
        {
            "boss" => radius + 6f,
            "bloater" => radius + 3f,
            "crusher" => radius + 3f,
            "brute" => radius + 2f,
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
