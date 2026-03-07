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
    public int Cost { get; }
    public Color Color { get; }
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
    public float Radius { get; private set; } = 16f;
    public Color Tint => _bodyColor;

    public bool IsDead => Health <= 0f;

    private float _attackTimer;
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

    public void TakeDamage(float damage)
    {
        Health -= damage;
        if (Health < 0f)
        {
            Health = 0f;
        }
    }

    public override void _Process(double delta)
    {
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawCircle(Vector2.Zero, Radius, _bodyColor);

        var hpBarWidth = Radius * 2f;
        var hpRatio = Mathf.Clamp(Health / MaxHealth, 0f, 1f);
        var barOrigin = new Vector2(-Radius, -Radius - 14f);

        DrawRect(new Rect2(barOrigin, new Vector2(hpBarWidth, 5f)), new Color(0f, 0f, 0f, 0.55f), true);
        DrawRect(new Rect2(barOrigin, new Vector2(hpBarWidth * hpRatio, 5f)), new Color("80ed99"), true);
    }
}
