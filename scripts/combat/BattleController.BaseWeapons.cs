using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class BattleController
{
    private sealed class BaseMount
    {
        public BaseWeaponDefinition Weapon;
        public float Recovery;
        public BaseMount(BaseWeaponDefinition weapon) { Weapon = weapon; }
    }

    private readonly List<BaseMount> _wagonMounts = new();
    private int _wagonVolleyLevel, _wagonRepairLevel, _wagonArmorLevel;
    private float _wagonVolleyRecovery;
    private bool _wagonRepairUsed;

    private void InitializeBaseWeapons()
    {
        var state = GameState.Instance;
        _wagonMounts.Clear();
        foreach (var id in new[] { BaseUpgradeCatalog.ArcherCrewId, BaseUpgradeCatalog.BallistaId, BaseUpgradeCatalog.FirepotId })
        {
            var weapon = BaseWeaponCatalog.Wagon(id, state.GetBaseUpgradeLevel(id));
            if (weapon != null) _wagonMounts.Add(new BaseMount(weapon));
        }
        _wagonVolleyLevel = state.GetBaseUpgradeLevel(BaseUpgradeCatalog.ArrowVolleyId);
        _wagonRepairLevel = state.GetBaseUpgradeLevel(BaseUpgradeCatalog.EmergencyRepairId);
        _wagonArmorLevel = state.GetBaseUpgradeLevel(BaseUpgradeCatalog.ReinforcedArmorId);
        _wagonVolleyRecovery = BaseWeaponCatalog.VolleyCooldown(_wagonVolleyLevel);
        _wagonRepairUsed = false;
    }

    private bool CanBaseTarget(Team team, Vector2 origin, BaseWeaponDefinition weapon, Unit target) =>
        IsInstanceValid(target) && !target.IsDead && !target.IsUntargetable && target.Team != team &&
        origin.DistanceTo(target.Position) <= weapon.Range + target.Radius;

    private static bool IsArmoredBaseTarget(Unit target) => target.DamageTakenScale < 0.95f ||
        target.VisualClass is "shield" or "brute" or "crusher" or "siegetower" or "boss" ||
        target.DefinitionId.Contains("boss", StringComparison.OrdinalIgnoreCase);

    private Unit FindBaseWeaponTarget(Team team, Vector2 origin, BaseWeaponDefinition weapon)
    {
        Unit best = null;
        var bestScore = float.MinValue;
        foreach (var target in _units)
        {
            if (!CanBaseTarget(team, origin, weapon, target)) continue;
            var score = -origin.DistanceTo(target.Position);
            if (weapon.Kind == BaseWeaponKind.Ballista && IsArmoredBaseTarget(target)) score += 500;
            if (weapon.Kind == BaseWeaponKind.Arrows && (target.Speed >= 75 || target.VisualClass == "saboteur")) score += 350;
            if (weapon.SplashRadius > 0)
                score += 180 * _units.Count(other => IsInstanceValid(other) && !other.IsDead && !other.IsUntargetable &&
                    other.Team != team && other.Position.DistanceTo(target.Position) <= weapon.SplashRadius);
            if (score > bestScore) { best = target; bestScore = score; }
        }
        return best;
    }

    private void TickBaseWeapons(float delta)
    {
        if (_battleEnded || _battlePaused || _endlessCheckpointActive || _playerBaseHealth <= 0) return;
        foreach (var mount in _wagonMounts)
        {
            mount.Recovery = Mathf.Max(0, mount.Recovery - delta);
            if (mount.Recovery > 0) continue;
            var target = FindBaseWeaponTarget(Team.Player, PlayerBaseCorePosition, mount.Weapon);
            if (target == null) continue;
            FireBaseWeapon(mount.Weapon, target);
            mount.Recovery = mount.Weapon.Cooldown;
        }
        TickWagonSkills(delta);
    }

    private void TickWagonSkills(float delta)
    {
        if (_wagonRepairLevel > 0 && !_wagonRepairUsed && _playerBaseHealth <= _playerBaseMaxHealth * 0.4f)
        {
            _wagonRepairUsed = true;
            var repair = _playerBaseMaxHealth * BaseWeaponCatalog.RepairRatio(_wagonRepairLevel);
            _playerBaseHealth = Mathf.Min(_playerBaseMaxHealth, _playerBaseHealth + repair);
            SpawnEffect(PlayerBaseCorePosition, new Color("98dbaa"), 20, 90, 0.5f, false);
            AudioDirector.Instance?.PlayBusRepair(repair);
            SetStatus("The wagon crew triggered emergency repairs. Repairs are spent for this battle.");
        }
        if (_wagonVolleyLevel <= 0) return;
        _wagonVolleyRecovery = Mathf.Max(0, _wagonVolleyRecovery - delta);
        if (_wagonVolleyRecovery > 0) return;
        var arrows = _wagonMounts[0].Weapon;
        var targets = _units.Where(target => CanBaseTarget(Team.Player, PlayerBaseCorePosition, arrows, target))
            .OrderBy(target => PlayerBaseCorePosition.DistanceSquaredTo(target.Position)).Take(3).ToArray();
        if (targets.Length == 0) return; // Keep a ready skill until enemies arrive.
        foreach (var target in targets) FireBaseWeapon(arrows, target);
        _wagonVolleyRecovery = BaseWeaponCatalog.VolleyCooldown(_wagonVolleyLevel);
    }

    private void FireBaseWeapon(BaseWeaponDefinition weapon, Unit target)
    {
        var origin = PlayerBaseCorePosition;
        if (weapon.Kind is BaseWeaponKind.Arrows or BaseWeaponKind.Ballista)
        {
            // Use the same frontal shield protection as troop projectiles.
            target = _units.Where(shield => CanBaseTarget(Team.Player, origin, weapon, shield) &&
                    shield.SpecialAbilityId == "projectile_shield" && shield.SpecialBuffRadius > 0 &&
                    shield.Position.DistanceTo(target.Position) <= shield.SpecialBuffRadius &&
                    shield.Position.X >= origin.X && shield.Position.X <= target.Position.X + 40)
                .OrderBy(shield => origin.DistanceSquaredTo(shield.Position)).FirstOrDefault() ?? target;
        }
        var victim = target;
        var projectile = ProjectilePool.Acquire();
        AddChild(projectile);
        projectile.SetWeaponVisual(weapon.Kind);
        projectile.LaunchGroundY = (BattlefieldTop + BattlefieldBottom) * .5f;
        projectile.Position = WagonMountPosition(Mathf.Max(0, _wagonMounts.FindIndex(mount => mount.Weapon.Kind == weapon.Kind)));
        AudioDirector.Instance?.PlayBaseWeapon(ProjectileStyles.ForBaseWeapon(weapon.Kind).Id, projectile.GlobalPosition);
        projectile.ProcessMode = ProcessModeEnum.Pausable;
        projectile.ShouldPause = () => _battlePaused || _endlessCheckpointActive;
        projectile.Setup(victim, weapon.Damage, weapon.Speed, weapon.Color,
            amount =>
            {
                if (_battleEnded) return 0;
                if (weapon.SplashRadius > 0)
                {
                    ApplySplashDamage(Team.Player, victim.Position, amount, weapon.SplashRadius, weapon.Color, weapon.Title);
                    SpawnEffect(victim.Position, weapon.Color, 8, weapon.SplashRadius, 0.3f, false);
                    return 0;
                }
                if (weapon.Kind == BaseWeaponKind.Ballista && IsArmoredBaseTarget(victim)) amount *= 1.5f;
                return victim.TakeDamage(amount, weapon.Title);
            },
            () => _battleEnded || !IsInstanceValid(victim) || victim.IsDead || victim.IsUntargetable,
            (position, dealt, color) =>
            {
                if (dealt <= 0) return;
                TrackDamageDealt(weapon.Title, dealt);
                SpawnDamageFeedback(ToLocal(position), dealt, color);
            });
        SpawnEffect(projectile.Position, weapon.Color, 3, 15, 0.16f, false);
    }

    private void DrawWagonArmaments(CanvasItem canvas)
    {
        if (_playerBaseHealth <= 0) return;
        for (var i = 0; i < _wagonMounts.Count; i++)
            DrawBaseMount(canvas, WagonMountPosition(i), _wagonMounts[i].Weapon, 1);
    }

    private void DrawBaseMount(CanvasItem canvas, Vector2 position, BaseWeaponDefinition weapon, float direction)
    {
        var texture = BattlefieldTextureLoader.TryLoadStructure("mount_" + weapon.Kind.ToString().ToLowerInvariant());
        if (texture != null)
        {
            // Mounts scale with the bases they sit on.
            canvas.DrawSetTransform(position - ((Node2D)canvas).Position, 0f, new Vector2(direction, 1) * _combat.StructureScale);
            canvas.DrawTextureRect(texture, new Rect2(-30f, -59f, 60f, 75f), false, FieldLighting.Tint);
            canvas.DrawSetTransform(-((Node2D)canvas).Position, 0f, Vector2.One);
            return;
        }
        var wood = new Color("77563c");
        var iron = new Color("9b9987");
        var shade = new Color("302b26");
        var accent = weapon.Color.Darkened(0.3f);
        Vector2 Point(float x, float y) => position + new Vector2(x * direction, y);
        // Timber supports seat each crew or weapon on the wagon roof.
        canvas.DrawLine(Point(-5, 8), Point(-8, 24), shade, 4, true);
        canvas.DrawLine(Point(5, 8), Point(8, 24), wood, 3, true);
        canvas.DrawLine(Point(-11, 9), Point(11, 9), wood, 4, true);
        if (weapon.Kind == BaseWeaponKind.Arrows)
        {
            canvas.DrawLine(Point(-4, 6), Point(-4, -5), accent, 6, true);
            canvas.DrawCircle(Point(-4, -10), 4, new Color("b8a082"));
            canvas.DrawArc(Point(-4, -10), 5, Mathf.Pi, Mathf.Tau, 8, iron, 2, true);
            canvas.DrawLine(Point(-3, -3), Point(8, -2), iron, 2, true);
            canvas.DrawPolyline(new[] { Point(8, -14), Point(14, -3), Point(8, 8) }, wood.Lightened(0.2f), 2, true);
            canvas.DrawLine(Point(8, -14), Point(8, 8), new Color("cabd98"), 1, true);
            canvas.DrawLine(Point(2, -3), Point(20, -3), iron, 1, true);
        }
        else if (weapon.Kind == BaseWeaponKind.Ballista)
        {
            canvas.DrawLine(Point(-12, 3), Point(23, -3), wood, 5, true);
            canvas.DrawPolyline(new[] { Point(9, -14), Point(17, -2), Point(9, 11) }, iron, 3, true);
            canvas.DrawPolyline(new[] { Point(9, -14), Point(-5, 1), Point(9, 11) }, new Color("b8ab89"), 1, true);
            canvas.DrawLine(Point(-6, 0), Point(27, -4), iron.Lightened(0.2f), 2, true);
        }
        else // Firepot launcher.
        {
            canvas.DrawLine(Point(-4, 6), Point(9, -9), shade, 12, true);
            canvas.DrawLine(Point(-4, 6), Point(9, -9), iron.Darkened(0.3f), 8, true);
            canvas.DrawCircle(Point(9, -9), 4, accent);
            canvas.DrawCircle(Point(9, -9), 2, weapon.Color);
        }
    }
}
