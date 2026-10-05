using Godot;

public partial class BattleController
{
    private void ShowReleaseTrace(Unit attacker, Unit target)
        => ShowReleaseTraceTo(attacker,target.BodyContactPosition);

    private void ShowReleaseTraceTo(Unit attacker, Vector2 point)
    {
        var origin=attacker.WeaponContactPosition;
        var effect=new WeaponContactEffect();
        effect.SetupBeam(point-origin,attacker.Tint.Lightened(.5f),IsReducedMotionEnabled());
        effect.ShouldPause=()=>_battlePaused || _endlessCheckpointActive;
        AddChild(effect); effect.GlobalPosition=origin; effect.ZIndex=20;
    }

    private void QueueUnitStrike(Unit attacker, Unit target)
    {
        var targetLife = target.CombatLifetime;
        var damage = attacker.CurrentAttackDamage;
        attacker.ScheduleAttackImpact(() =>
        {
            if (!attacker.CanResolveContact(target,targetLife,attacker.UsesProjectile)) return;
            if (attacker.UsesProjectile)
            {
                SpawnProjectile(attacker,target);
                return;
            }
            if (attacker.AttackSplashRadius > .05f)
            {
                ApplySplashDamage(attacker.Team,target.Position,damage,attacker.AttackSplashRadius,attacker.Tint,attacker.UnitName);
                ShowWeaponContact(attacker,target,damage,false);
            }
            else
            {
                var applied = target.TakeDamage(damage,attacker.UnitName);
                TrackDamageDealt(attacker,applied);
                SpawnDamageFeedback(target.BodyContactPosition,applied,attacker.Tint);
                ApplyImpactReaction(attacker,target,applied,false);
                ApplyDamageReflect(target,attacker,applied);
                ApplyMirrorPressureReflect(attacker,target,applied);
            }
        });
    }

    private void ShowWeaponContact(Unit attacker, Unit target, float damage, bool ranged)
    {
        if (damage <= .05f || !IsInstanceValid(attacker) || !IsInstanceValid(target)) return;
        var direction = Mathf.Sign(target.Position.X-attacker.Position.X);
        if (direction == 0) direction = attacker.Team == Team.Player ? 1 : -1;
        target.ReactToContact(direction,damage,ResolveImpactResistance(target)*(ranged?.65f:1f));
        var point = target.BodyContactPosition - new Vector2(direction*target.Radius*.28f,0);
        if (!ranged)
        {
            var stroke = new WeaponContactEffect();
            stroke.Setup(attacker.MotionProfile,direction,attacker.Tint,IsReducedMotionEnabled());
            stroke.ShouldPause = () => _battlePaused || _endlessCheckpointActive;
            AddChild(stroke);
            stroke.GlobalPosition = point;
            stroke.ZIndex = 20;
            if (!IsReducedMotionEnabled()) BattleParticles.SpawnImpactSparks(this,point,attacker.Tint,damage*.7f);
        }
    }
}
