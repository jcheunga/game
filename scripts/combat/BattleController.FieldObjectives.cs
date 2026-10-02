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
    private const float OutpostRadius = 108f, SupplyRadius = 86f, SupplyHoldSeconds = 2.5f;
    private float _outpostProgress, _supplyProgress, _forwardCooldownRemaining, _fieldSummonSuppressionRemaining;
    private bool _outpostCaptured, _supplyCollected, _forwardDeploymentArmed;
    private int _forwardDeploymentsRemaining, _forwardDeploymentsUsed;
    private Vector2 FieldPoint(float x, float y) => new(
        Mathf.Lerp(BattlefieldLeft + 64, BattlefieldRight - 64, x),
        Mathf.Lerp(BattlefieldTop + 48, BattlefieldBottom - 48, y));
    private Vector2 OutpostPosition => FieldPoint(_stageData.Battlefield.OutpostXRatio, _stageData.Battlefield.OutpostYRatio);
    private Vector2 SupplyPosition => FieldPoint(_stageData.Battlefield.SupplyXRatio, _stageData.Battlefield.SupplyYRatio);
    private bool HasUnitNear(Team team, Vector2 point, float radius) => _units.Any(u => !u.IsDead && u.Team == team && u.Position.DistanceTo(point) <= radius);
    private bool OutpostBlocked => HasCampaignField && HasUnitNear(Team.Enemy, OutpostPosition, OutpostRadius + 28);
    private bool CanDeployForward => false;

    private void UpdateCampaignField(float delta)
    {
        if (!HasCampaignField) return;
        _spawnDirector.SetPlayerFrontline(_units.Where(u => !u.IsDead && u.Team == Team.Player)
            .Select(u => u.Position.X).DefaultIfEmpty(PlayerSpawnX).Max());
    }

    private float TickFieldCapture(Vector2 point, float radius, float progress, float delta)
    {
        if (HasUnitNear(Team.Enemy, point, radius + 28)) return Mathf.Max(0, progress - delta * .5f);
        return HasUnitNear(Team.Player, point, radius) ? progress + delta : progress;
    }

    private void CollectCampaignSupplies()
    {
        if (_supplyCollected) return;
        _supplyCollected = true;
        switch (_stageData.Battlefield.SupplyReward)
        {
            case "repair": RepairBusByRatio(.12f); _courage = Mathf.Min(_maxCourage, _courage + 8); break;
            case "siege":
                DamageEnemyBaseByRatio(.12f, new Color("ffd166"), "SIEGE SUPPLIES");
                _fieldSummonSuppressionRemaining = 18f;
                break;
            default: _courage = Mathf.Min(_maxCourage, _courage + 25); _deck.ReduceCooldowns(3); break;
        }
        SpawnFloatText(SupplyPosition, "SUPPLIES SECURED", new Color("ffd166"), 1.6f);
        SetStatus($"{_stageData.Battlefield.SupplyTitle}: {_stageData.Battlefield.RewardSummary}.");
    }

    private Vector2 ResolvePlayerDeployPosition(float requestedY)
    {
        var y = ResolveDeployLaneY(requestedY, out _);
        return CanDeployForward ? new Vector2(OutpostPosition.X - 48,
            Mathf.Clamp(y, OutpostPosition.Y - 90, OutpostPosition.Y + 90)) : new Vector2(PlayerSpawnX, y);
    }

    private bool TryHoldCampaignFieldPoint(Unit unit, Unit target)
    {
        return false;
    }

    private string BuildCampaignFieldIntelText()
    {
        return "Destroy the enemy stronghold.";
    }

    private bool CanUseCampaignEnemySpecial(Unit unit)
    {
        if (!HasCampaignField || unit.Team != Team.Enemy ||
            unit.SpecialAbilityId is not ("rally_call" or "raise_fallen" or "jam_signal")) return true;
        if (_fieldSummonSuppressionRemaining > 0 || _enemyBaseHealth <= 0) return false;
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

    private void DrawCampaignFieldObjectives()
    {
        // Capturable posts and supply circles are removed.
    }

    private void DrawFieldPoint(Vector2 point, float radius, float progress, Color color, string title)
    {
        DrawCircle(point, radius, new Color(color, .06f));
        DrawArc(point, radius, 0, Mathf.Tau, 48, new Color(color, .45f), 2, true);
        DrawArc(point, radius - 5, -Mathf.Pi / 2, -Mathf.Pi / 2 + Mathf.Tau * Mathf.Clamp(progress, 0, 1), 48, color, 4, true);
        DrawLine(point + new Vector2(0, -22), point + new Vector2(0, 8), color, 3);
        DrawColoredPolygon(new[] { point + new Vector2(0, -22), point + new Vector2(23, -16), point + new Vector2(0, -9) }, color);
        DrawPreviewLabel(point + new Vector2(-radius, -radius - 26), title, color);
    }
}
