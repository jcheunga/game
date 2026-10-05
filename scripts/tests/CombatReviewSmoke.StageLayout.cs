using System.Linq;
using System.Text.Json;
using Godot;

public partial class CombatReviewSmoke
{
    private void ExportStageLayoutReview()
    {
        var combat = GameData.Combat;
        GameState.Instance.PrepareCampaignBattle();
        foreach (var original in GameData.Stages.OrderBy(s => s.StageNumber))
        {
            var stage = GameData.GetStage(original.StageNumber);
            var missions = StageMissionEvents.GetCampaignMissionEvents(stage);
            var plan = stage.Battlefield;
            Check(plan != null && plan.OutpostXRatio is >= .3f and <= .5f && plan.SupplyXRatio > plan.OutpostXRatio &&
                plan.SupplyXRatio < .9f && plan.CaptureSeconds > 0 && plan.ForwardDeployments is >= 1 and <= 6,
                $"Stage {stage.StageNumber}: forward post and optional reward are reachable and bounded");
            Check(stage.Waves.First().Area == "Approach" && stage.Waves.Last().Area == "Gate" &&
                stage.Waves.All(w => w.SpawnXRatio is >= .2f and <= 1 && w.AdvanceTriggerXRatio < w.SpawnXRatio),
                $"Stage {stage.StageNumber}: encounters span the full battlefield with advance warning space");
            Check(!StageModifiers.HasCursedGround(stage) || plan.CursePatches.Length == 2 && plan.CursePatches.Sum(p => p.Width) <= 600,
                $"Stage {stage.StageNumber}: cursed ground preserves safe travel gaps");
            Check(missions.All(m => m.XRatio is >= 0 and <= 1 && m.YRatio is >= 0 and <= 1 &&
                m.Radius > 0 && m.TargetSeconds > 0), $"Stage {stage.StageNumber}: mission locations and hold requirements are valid");
            Check(stage.Hazards.All(h => h.XRatio is >= 0 and <= 1 && h.YRatio is >= 0 and <= 1 &&
                h.Radius > 0 && h.Interval > 0), $"Stage {stage.StageNumber}: hazards remain within the extended field");
            Check(stage.Waves.Length > 0 && stage.Waves.SelectMany(w => w.Entries).All(e =>
                e.Count > 0 && GameData.GetUnit(e.UnitId) != null), $"Stage {stage.StageNumber}: all authored waves resolve to enemy units");
            GD.Print("STAGE_LAYOUT: " + JsonSerializer.Serialize(new
            {
                stage = stage.StageNumber, name = stage.StageName, route = stage.MapId,
                objectives = StageObjectives.BuildSummaryText(stage, 0),
                waveCount = stage.Waves.Length,
                enemies = stage.Waves.SelectMany(w => w.Entries).Sum(e => e.Count),
                bossIds = stage.Waves.SelectMany(w => w.Entries).Select(e => e.UnitId).Distinct()
                    .Where(id => GameData.GetUnit(id).VisualClass == "boss"),
                modifiers = stage.Modifiers.Select(m => m.Type),
                missions = missions.Select(m => new
                {
                    type = m.NormalizedType, title = StageMissionEvents.ResolveTitle(m), m.StartTime,
                    m.TargetSeconds, m.Radius,
                    oldX = Mathf.Lerp(148, 1132, m.XRatio),
                    x = Mathf.Lerp(combat.BattlefieldLeft + 64, combat.BattlefieldRight - 64, m.XRatio),
                    y = Mathf.Lerp(combat.BattlefieldTop + 48, combat.BattlefieldBottom - 48, m.YRatio)
                }),
                hazards = stage.Hazards.Select(h => new
                {
                    h.Type, h.Label, h.Radius, h.Damage, h.StartTime, h.Interval, h.WarningDuration,
                    x = Mathf.Lerp(combat.BattlefieldLeft + 48, combat.BattlefieldRight - 48, h.XRatio),
                    y = Mathf.Lerp(combat.BattlefieldTop + 32, combat.BattlefieldBottom - 32, h.YRatio)
                })
            }));
        }
    }
}
