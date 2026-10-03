using Godot;

public partial class BattleController
{
    private void SpawnUnitBaseProjectile(Unit attacker)
    {
        var team = attacker.Team;
        var damage = attacker.BaseDamage;
        var tint = attacker.Tint;
        var lifetime = attacker.CombatLifetime;
        var target = GetNode<Node2D>(team == Team.Player ? "Castle" : "Caravan");
        var projectile = ProjectilePool.Acquire();
        AddChild(projectile);
        projectile.GlobalPosition = attacker.WeaponContactPosition;
        projectile.TargetOffset = new Vector2(0, -24);
        projectile.ShouldPause = () => _battlePaused || _endlessCheckpointActive || _battleEnded;
        if (attacker.MotionProfile == "ballista") projectile.SetWeaponVisual(BaseWeaponKind.Ballista);
        else if (attacker.MotionProfile is "bow-draw" or "crossbow") projectile.SetWeaponVisual(BaseWeaponKind.Arrows);
        projectile.Setup(target, damage, attacker.ProjectileSpeed > 0 ? attacker.ProjectileSpeed : 420,
            tint.Lightened(.25f), value => {
                ApplyUnitBaseHit(team, value, tint, attacker, lifetime);
                return value;
            }, () => _battleEnded || (team == Team.Player ? _enemyBaseHealth : _playerBaseHealth) <= 0);
    }
}
