using System.Collections.Generic;
using Godot;

public partial class BattleController
{
    private const float BossPhaseWarningSeconds = 1.6f;
    private readonly Dictionary<Unit, float> _pendingBossPhases = new();
    // These commanders replace casualties instead of accumulating an unbounded escort wall.
    private bool CanAddBossReinforcement(Unit source, string unitId)
    {
        if (source.DefinitionId == GameData.EnemyBossForgeId && !IsCampaignMode) return true;
        if ((source.DefinitionId != GameData.EnemyBossReliquaryId && source.DefinitionId != GameData.EnemyBossTidemasterId &&
             source.DefinitionId != GameData.EnemyBossForgeId) ||
            unitId != source.SpecialSpawnUnitId) return true;
        var active = 0;
        foreach (var unit in _units)
            if (!unit.IsDead && unit.Team == Team.Enemy && unit.DefinitionId == unitId) active++;
        return active < 2;
    }
    private Rect2 CursedGroundArea => new(BattlefieldLeft + 200f, BaseCenterY - 62f,
        BattlefieldRight - BattlefieldLeft - 340f, 124f);

    private void DrawTunnelInvasionWarning()
    {
        if (!_pendingTunnelInvasion.HasValue) return;
        var position = _pendingTunnelInvasion.Value;
        var color = new Color("ffb454");
        DrawCircle(position, 42f, new Color(color, 0.16f));
        DrawArc(position, 42f, 0, Mathf.Tau, 36, color, 3f, true);
        DrawPreviewLabel(position + new Vector2(-70, -65), $"TUNNEL BREACH · {Mathf.Max(0f, _tunnelInvasionTimer):0.0}s", color);
    }

    private void DrawCursedGround()
    {
        if (!StageModifiers.HasCursedGround(_stageData)) return;
        var area = CursedGroundArea;
        var color = new Color("c49be8");
        DrawRect(area, new Color(color, 0.13f));
        DrawRect(area, new Color(color, 0.55f), false, 2f);
        DrawPreviewLabel(area.Position + new Vector2(8, 8), "CURSED GROUND · deploy in another lane", color);
    }

    private bool PrepareBossPhase(Unit boss)
    {
        if (!_pendingBossPhases.TryGetValue(boss, out var triggerAt))
        {
            _pendingBossPhases[boss] = _elapsed + BossPhaseWarningSeconds;
            var title = StageEncounterIntel.GetBossPhaseTitle(boss.DefinitionId);
            SpawnFloatText(boss.Position + new Vector2(0, -62), "PHASE INCOMING", new Color("ffd166"), BossPhaseWarningSeconds);
            SetStatus($"{boss.UnitName} is preparing {title}. Reinforce, heal, or finish the commander!");
            return false;
        }
        if (_elapsed < triggerAt) return false;
        _pendingBossPhases.Remove(boss);
        return true;
    }

    private void DrawBossPhaseWarnings()
    {
        foreach (var entry in _pendingBossPhases)
        {
            if (entry.Key.IsDead) continue;
            var progress = Mathf.Clamp(1f - (entry.Value - _elapsed) / BossPhaseWarningSeconds, 0f, 1f);
            var color = new Color("ffb454");
            DrawCircle(entry.Key.Position, 120, new Color(color, 0.07f));
            DrawArc(entry.Key.Position, 120, -Mathf.Pi / 2, -Mathf.Pi / 2 + Mathf.Tau * progress, 48, color, 3, true);
        }
    }
}
