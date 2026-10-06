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
        // Aim at the struck point of the base, not its foundation line.
        projectile.TargetOffset = (team == Team.Player ? EnemyBaseCorePosition : PlayerBaseCorePosition) - target.Position + new Vector2(0, -12);
        projectile.ShouldPause = () => _battlePaused || _endlessCheckpointActive || _battleEnded;
        projectile.SetStyle(ProjectileStyles.ForUnit(attacker.DefinitionId, attacker.MotionProfile));
        projectile.LaunchGroundY = attacker.GlobalPosition.Y;
        projectile.Setup(target, damage, attacker.ProjectileSpeed > 0 ? attacker.ProjectileSpeed : 210,
            tint.Lightened(.25f), value => {
                ApplyUnitBaseHit(team, value, tint, attacker, lifetime);
                return value;
            }, () => _battleEnded || (team == Team.Player ? _enemyBaseHealth : _playerBaseHealth) <= 0);
    }
}
