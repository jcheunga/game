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
    private bool CanDeployForward => HasCampaignField && _outpostCaptured && _forwardDeploymentArmed &&
        _forwardDeploymentsRemaining > 0 && _forwardCooldownRemaining <= 0 && !OutpostBlocked;

    private void UpdateCampaignField(float delta)
    {
        if (!HasCampaignField) return;
        _spawnDirector.SetPlayerFrontline(_units.Where(u => !u.IsDead && u.Team == Team.Player)
            .Select(u => u.Position.X).DefaultIfEmpty(PlayerSpawnX).Max());
        _forwardCooldownRemaining = Mathf.Max(0, _forwardCooldownRemaining - delta);
        _fieldSummonSuppressionRemaining = Mathf.Max(0, _fieldSummonSuppressionRemaining - delta);
        if (!_outpostCaptured)
        {
            _outpostProgress = TickFieldCapture(OutpostPosition, OutpostRadius, _outpostProgress, delta);
            if (_outpostProgress >= _stageData.Battlefield.CaptureSeconds)
            {
                _outpostCaptured = _forwardDeploymentArmed = true;
                _forwardDeploymentsRemaining = _stageData.Battlefield.ForwardDeployments;
                SpawnFloatText(OutpostPosition, "FORWARD POST SECURED", new Color("86d5a5"), 1.6f);
                SetStatus($"{_stageData.Battlefield.OutpostTitle} secured! Your next {_forwardDeploymentsRemaining} deployments can start here. Use the Post button to save them.");
            }
        }
        if (!_supplyCollected)
        {
            _supplyProgress = TickFieldCapture(SupplyPosition, SupplyRadius, _supplyProgress, delta);
            if (_supplyProgress >= SupplyHoldSeconds) CollectCampaignSupplies();
        }
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
        if (!HasCampaignField || unit.Team != Team.Player ||
            (target != null && unit.Position.DistanceTo(target.Position) < Mathf.Max(240, unit.AttackRange + 60))) return false;
        if (!_outpostCaptured && unit.Position.DistanceTo(OutpostPosition) <= OutpostRadius) return true;
        if (!_supplyCollected && unit.Position.DistanceTo(SupplyPosition) <= SupplyRadius) return true;
        // Troops assigned to a mission lane stay long enough to finish its hold objective.
        return _stageMissions.Any(m => m.Started && !m.Completed && !m.Failed && !m.UsesAdaptiveWaveProgress &&
            unit.Position.DistanceTo(m.Anchor) <= m.Definition.Radius);
    }

    private string BuildCampaignFieldIntelText()
    {
        if (!HasCampaignField) return "";
        var plan = _stageData.Battlefield;
        var post = !_outpostCaptured ? $"Hold {plan.OutpostTitle} for {plan.CaptureSeconds:0.#}s to unlock forward deployments."
            : $"{plan.OutpostTitle}: {_forwardDeploymentsRemaining} deployments left; {(_forwardDeploymentArmed ? "post selected" : "wagon selected")}.";
        return $"{_spawnDirector.CurrentEncounterArea} · {post}\n" +
            (_supplyCollected ? $"{plan.SupplyTitle}: secured.\n" : $"Optional: hold {plan.SupplyTitle} for 2.5s. {plan.RewardSummary}.\n");
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
        if (!HasCampaignField) return;
        var plan = _stageData.Battlefield;
        DrawFieldPoint(OutpostPosition, OutpostRadius, _outpostProgress / plan.CaptureSeconds,
            _outpostCaptured ? new Color("86d5a5") : new Color("8ecae6"),
            _outpostCaptured ? $"{plan.OutpostTitle} · {_forwardDeploymentsRemaining} deploys" : $"{plan.OutpostTitle} · hold {plan.CaptureSeconds:0.#}s");
        if (!_supplyCollected) DrawFieldPoint(SupplyPosition, SupplyRadius, _supplyProgress / SupplyHoldSeconds,
            new Color("ffd166"), $"{plan.SupplyTitle} · {plan.SupplyReward}");
        if (_spawnDirector.EncounterWarningActive)
        {
            var x = _spawnDirector.NextEncounterSpawnX;
            var color = new Color("ffb454");
            DrawLine(new Vector2(x, BattlefieldTop + 20), new Vector2(x, BattlefieldBottom - 20), new Color(color, .5f), 3);
            _spawnDirector.TryGetNextScriptedWave(out var wave);
            DrawPreviewLabel(new Vector2(Mathf.Max(BattlefieldLeft, x - 220), BattlefieldTop + 62),
                $"{wave.Area}: {wave.Label} · {Mathf.Max(0, _spawnDirector.NextScriptedWaveTime - _elapsed):0.0}s", color);
        }
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
