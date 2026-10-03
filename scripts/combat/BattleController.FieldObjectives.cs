using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class BattleController
{
    private readonly Dictionary<Unit, int> _campaignPeriodicSummons = new();
    private bool CanAddCampaignPeriodicReinforcement(Unit source) => !HasCampaignField ||
        _campaignPeriodicSummons.GetValueOrDefault(source) < (source.VisualClass == "boss" ? (_stage <= 20 ? 3 : 5) : 3);
    private void RecordCampaignPeriodicReinforcement(Unit source)
    {
        if (HasCampaignField) _campaignPeriodicSummons[source] = _campaignPeriodicSummons.GetValueOrDefault(source) + 1;
    }

    private bool HasCampaignField => IsCampaignMode && _stageData?.Battlefield != null;
    private Vector2 FieldPoint(float x, float y) => new(
        Mathf.Lerp(BattlefieldLeft + 64, BattlefieldRight - 64, x),
        Mathf.Lerp(BattlefieldTop + 48, BattlefieldBottom - 48, y));
    private bool HasUnitNear(Team team, Vector2 point, float radius) => _units.Any(u => !u.IsDead && u.Team == team && u.Position.DistanceTo(point) <= radius);

    private void UpdateCampaignField(float delta)
    {
        if (!HasCampaignField) return;
        _spawnDirector.SetPlayerFrontline(_units.Where(u => !u.IsDead && u.Team == Team.Player)
            .Select(u => u.Position.X).DefaultIfEmpty(PlayerSpawnX).Max());
    }

    private Vector2 ResolvePlayerDeployPosition(float requestedY)
    {
        return new Vector2(PlayerSpawnX, ResolveDeployLaneY(requestedY, out _));
    }

    private bool CanUseCampaignEnemySpecial(Unit unit)
    {
        if (!HasCampaignField || unit.Team != Team.Enemy ||
            unit.SpecialAbilityId is not ("rally_call" or "raise_fallen" or "jam_signal")) return true;
        if (_enemyBaseHealth <= 0) return false;
        // Commanders begin their pressure when the fighting reaches them, not during the long walk.
        return unit.Position.DistanceTo(PlayerBaseCorePosition) < 550 || HasUnitNear(Team.Player, unit.Position, 550);
    }

    private IEnumerable<Rect2> CursedGroundAreas()
    {
        if (HasCampaignField)
        {
            foreach (var patch in _stageData.Battlefield.CursePatches)
                yield return new Rect2(FieldPoint(patch.XRatio, patch.YRatio) - new Vector2(patch.Width, patch.Height) * .5f,
                    new Vector2(patch.Width, patch.Height));
        }
        else yield return CursedGroundArea;
    }
}
