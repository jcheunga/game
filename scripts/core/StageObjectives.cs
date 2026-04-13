using System;
using System.Linq;
using System.Text;
using Godot;

public sealed class StageBattleResult
{
    public float PlayerBaseHealth { get; init; }
    public float PlayerBaseMaxHealth { get; init; }
    public float Elapsed { get; init; }
    public int PlayerDeployments { get; init; }
    public int EnemyDefeats { get; init; }
    public int PlayerHazardHits { get; init; }
    public float PlayerSignalJamSeconds { get; init; }
    public int CompletedMissionEvents { get; init; }
    public int FailedMissionEvents { get; init; }
    public int TotalMissionEvents { get; init; }
    public int CampaignBossPressureTriggers { get; init; }
    public int CampaignLateConditionTriggers { get; init; }
    public bool CampaignAdaptiveWaveChoiceReady { get; init; }
    public bool CampaignAdaptiveWaveChoiceUsed { get; init; }
    public bool CampaignAdaptiveWaveOverrideQueued { get; init; }
    public bool CampaignAdaptiveWaveRewardReady { get; init; }
    public bool CampaignAdaptiveWaveFollowUpActive { get; init; }
    public bool CampaignAdaptiveWaveFollowUpCompleted { get; init; }
    public bool CampaignAdaptiveWaveFollowUpFailed { get; init; }
    public string CampaignAdaptiveWaveFollowUpMode { get; init; } = "";
    public float CampaignAdaptiveWaveFollowUpTimer { get; init; }
    public float CampaignAdaptiveWaveFollowUpProgress { get; init; }
    public float CampaignAdaptiveWaveFollowUpTarget { get; init; }
    public string CampaignAdaptiveWaveChoiceLabel { get; init; } = "";
    public string CampaignAdaptiveWaveBranchLabel { get; init; } = "";
    public string CampaignAdaptiveWaveBranchWaveLabel { get; init; } = "";
    public int CampaignAdaptiveWaveBranchSpawnCount { get; init; }
    public string CampaignAdaptiveWaveFollowUpLabel { get; init; } = "";
}

public sealed class StageObjectiveOutcome
{
    public string Label { get; init; } = "";
    public bool Completed { get; init; }
}

public enum StageObjectiveLiveState
{
    Active,
    Completed,
    Failed
}

public sealed class StageObjectiveLiveStatus
{
    public string Label { get; init; } = "";
    public string Detail { get; init; } = "";
    public StageObjectiveLiveState State { get; init; }
}

public sealed class StageObjectiveEvaluation
{
    public StageObjectiveOutcome[] Outcomes { get; init; } = Array.Empty<StageObjectiveOutcome>();
    public int StarsEarned { get; init; }
}

public static class StageObjectives
{
    public static StageObjectiveEvaluation EvaluateVictory(StageDefinition stage, StageBattleResult result)
    {
        return EvaluateBattle(stage, result, true);
    }

    public static StageObjectiveEvaluation EvaluateBattle(StageDefinition stage, StageBattleResult result, bool playerWon)
    {
        var objectives = ResolveObjectives(stage);
        var outcomes = objectives
            .Select(objective => new StageObjectiveOutcome
            {
                Label = BuildObjectiveLabel(stage, objective),
                Completed = EvaluateObjective(stage, objective, result, playerWon)
            })
            .ToArray();

        return new StageObjectiveEvaluation
        {
            Outcomes = outcomes,
            StarsEarned = playerWon
                ? Mathf.Clamp(outcomes.Count(outcome => outcome.Completed), 1, 3)
                : 0
        };
    }

    public static string BuildSummaryText(StageDefinition stage, int bestStars)
    {
        var objectives = ResolveObjectives(stage);
        var builder = new StringBuilder();
        builder.AppendLine("Objectives:");

        for (var i = 0; i < objectives.Length; i++)
        {
            builder.AppendLine($"{i + 1}* {BuildObjectiveLabel(stage, objectives[i])}");
        }

        builder.Append($"Best: {(bestStars > 0 ? $"{bestStars}/3" : "none")}");
        return builder.ToString();
    }

    public static string BuildResultSummary(
        StageDefinition stage,
        StageObjectiveEvaluation evaluation,
        int rewardGold,
        int rewardFood,
        int bestStars)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Reward: +{rewardGold} gold, +{rewardFood} food.");
        builder.AppendLine($"Stars earned: {evaluation.StarsEarned}/3   |   Best: {bestStars}/3");
        builder.Append(BuildOutcomeSummary(evaluation));
        return builder.ToString().TrimEnd();
    }

    public static string BuildOutcomeSummary(StageObjectiveEvaluation evaluation)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < evaluation.Outcomes.Length; i++)
        {
            var prefix = evaluation.Outcomes[i].Completed ? "[OK]" : "[X]";
            builder.AppendLine($"{prefix} {evaluation.Outcomes[i].Label}");
        }

        return builder.ToString().TrimEnd();
    }

    public static StageObjectiveLiveStatus[] EvaluateLive(StageDefinition stage, StageBattleResult result)
    {
        var objectives = ResolveObjectives(stage);
        return objectives
            .Select(objective => BuildLiveStatus(stage, objective, result))
            .ToArray();
    }

    public static string BuildLiveSummary(StageDefinition stage, StageBattleResult result)
    {
        var liveStatuses = EvaluateLive(stage, result);
        var builder = new StringBuilder();
        builder.AppendLine("Live objectives:");

        for (var i = 0; i < liveStatuses.Length; i++)
        {
            var status = liveStatuses[i];
            var prefix = status.State switch
            {
                StageObjectiveLiveState.Completed => "[OK]",
                StageObjectiveLiveState.Failed => "[X]",
                _ => "[..]"
            };

            builder.Append(prefix);
            builder.Append(' ');
            builder.Append(status.Label);

            if (!string.IsNullOrWhiteSpace(status.Detail))
            {
                builder.Append("  |  ");
                builder.Append(status.Detail);
            }

            if (i < liveStatuses.Length - 1)
            {
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }

    private static StageObjectiveDefinition[] ResolveObjectives(StageDefinition stage)
    {
        if (stage.Objectives != null && stage.Objectives.Length > 0)
        {
            return stage.Objectives.Take(3).ToArray();
        }

        if (GameState.Instance?.CurrentBattleMode == BattleRunMode.Campaign &&
            !string.IsNullOrWhiteSpace(StageEncounterIntel.GetBossPressureTitleForStage(stage)))
        {
            return new[]
            {
                new StageObjectiveDefinition { Type = "clear_route" },
                new StageObjectiveDefinition { Type = "bus_hull_ratio", Value = stage.TwoStarBusHullRatio },
                new StageObjectiveDefinition { Type = "boss_pressure_trigger_limit", Value = ResolveBossPressureTriggerLimit(stage, null) }
            };
        }

        if (HasAdaptiveWaveFollowUpObjective(stage))
        {
            return new[]
            {
                new StageObjectiveDefinition { Type = "clear_route" },
                new StageObjectiveDefinition { Type = "bus_hull_ratio", Value = stage.TwoStarBusHullRatio },
                new StageObjectiveDefinition { Type = "adaptive_wave_follow_up_success" }
            };
        }

        if (GameState.Instance?.CurrentBattleMode == BattleRunMode.Campaign &&
            GameState.Instance.HasCampaignLateCondition(stage.StageNumber))
        {
            return new[]
            {
                new StageObjectiveDefinition { Type = "clear_route" },
                new StageObjectiveDefinition { Type = "bus_hull_ratio", Value = stage.TwoStarBusHullRatio },
                new StageObjectiveDefinition { Type = "late_condition_trigger_limit", Value = ResolveLateConditionTriggerLimit(stage, null) }
            };
        }

        return new[]
        {
            new StageObjectiveDefinition { Type = "clear_route" },
            new StageObjectiveDefinition { Type = "bus_hull_ratio", Value = stage.TwoStarBusHullRatio },
            new StageObjectiveDefinition { Type = "clear_within", Value = stage.ThreeStarTimeLimitSeconds }
        };
    }

    private static bool EvaluateObjective(
        StageDefinition stage,
        StageObjectiveDefinition objective,
        StageBattleResult result,
        bool playerWon)
    {
        var type = NormalizeType(objective.Type);
        return type switch
        {
            "clear_route" => playerWon,
            "bus_hull_ratio" => result.PlayerBaseHealth / ResolvePlayerBaseReference(stage, result) >= ResolveBusHullThreshold(stage, objective),
            "clear_within" => result.Elapsed <= ResolveTimeLimit(stage, objective),
            "deploy_limit" => result.PlayerDeployments <= Mathf.RoundToInt(Mathf.Max(1f, objective.Value)),
            "enemy_defeats" => result.EnemyDefeats >= Mathf.RoundToInt(Mathf.Max(1f, objective.Value)),
            "hazard_hits_limit" => result.PlayerHazardHits <= Mathf.RoundToInt(Mathf.Max(0f, objective.Value)),
            "signal_jam_limit" => result.PlayerSignalJamSeconds <= Mathf.Max(0f, objective.Value),
            "mission_event_success" => result.CompletedMissionEvents >= ResolveMissionEventTarget(objective),
            "boss_pressure_trigger_limit" => result.CampaignBossPressureTriggers <= ResolveBossPressureTriggerLimit(stage, objective),
            "late_condition_trigger_limit" => result.CampaignLateConditionTriggers <= ResolveLateConditionTriggerLimit(stage, objective),
            "adaptive_wave_follow_up_success" => playerWon && result.CampaignAdaptiveWaveFollowUpCompleted,
            _ => false
        };
    }

    private static string BuildObjectiveLabel(StageDefinition stage, StageObjectiveDefinition objective)
    {
        if (!string.IsNullOrWhiteSpace(objective.Label))
        {
            return objective.Label;
        }

        var type = NormalizeType(objective.Type);
        return type switch
        {
            "clear_route" => "Clear the route",
            "bus_hull_ratio" => $"Finish with war wagon hull >= {Mathf.RoundToInt(ResolveBusHullThreshold(stage, objective) * 100f)}%",
            "clear_within" => $"Clear within {ResolveTimeLimit(stage, objective):0}s",
            "deploy_limit" => $"Deploy no more than {Mathf.RoundToInt(Mathf.Max(1f, objective.Value))} units",
            "enemy_defeats" => $"Defeat at least {Mathf.RoundToInt(Mathf.Max(1f, objective.Value))} enemies",
            "hazard_hits_limit" => $"Take no more than {Mathf.RoundToInt(Mathf.Max(0f, objective.Value))} hazard hits",
            "signal_jam_limit" => $"Spend no more than {Mathf.Max(0f, objective.Value):0.#}s under signal jam",
            "mission_event_success" => BuildMissionEventObjectiveLabel(stage, objective),
            "boss_pressure_trigger_limit" => BuildBossPressureLimitLabel(stage, objective),
            "late_condition_trigger_limit" => BuildLateConditionLimitLabel(stage, objective),
            "adaptive_wave_follow_up_success" => BuildAdaptiveWaveFollowUpLabel(stage),
            _ => objective.Type
        };
    }

    private static StageObjectiveLiveStatus BuildLiveStatus(
        StageDefinition stage,
        StageObjectiveDefinition objective,
        StageBattleResult result)
    {
        var type = NormalizeType(objective.Type);
        var label = BuildObjectiveLabel(stage, objective);
        return type switch
        {
            "clear_route" => new StageObjectiveLiveStatus
            {
                Label = label,
                Detail = "Secure victory to complete",
                State = StageObjectiveLiveState.Active
            },
            "bus_hull_ratio" => BuildBusHullLiveStatus(stage, objective, result, label),
            "clear_within" => BuildTimeLimitLiveStatus(stage, objective, result, label),
            "deploy_limit" => BuildDeployLimitLiveStatus(objective, result, label),
            "enemy_defeats" => BuildEnemyDefeatLiveStatus(objective, result, label),
            "hazard_hits_limit" => BuildHazardHitLiveStatus(objective, result, label),
            "signal_jam_limit" => BuildSignalJamLiveStatus(objective, result, label),
            "mission_event_success" => BuildMissionEventLiveStatus(objective, result, label),
            "boss_pressure_trigger_limit" => BuildBossPressureLimitLiveStatus(stage, objective, result, label),
            "late_condition_trigger_limit" => BuildLateConditionLimitLiveStatus(stage, objective, result, label),
            "adaptive_wave_follow_up_success" => BuildAdaptiveWaveFollowUpLiveStatus(result, label),
            _ => new StageObjectiveLiveStatus
            {
                Label = label,
                Detail = "Unknown objective type",
                State = StageObjectiveLiveState.Active
            }
        };
    }

    private static float ResolveBusHullThreshold(StageDefinition stage, StageObjectiveDefinition objective)
    {
        return Mathf.Clamp(objective.Value > 0f ? objective.Value : stage.TwoStarBusHullRatio, 0f, 1f);
    }

    private static float ResolveTimeLimit(StageDefinition stage, StageObjectiveDefinition objective)
    {
        return Mathf.Max(1f, objective.Value > 0f ? objective.Value : stage.ThreeStarTimeLimitSeconds);
    }

    private static float ResolvePlayerBaseReference(StageDefinition stage, StageBattleResult result)
    {
        return Mathf.Max(1f, result.PlayerBaseMaxHealth > 0f ? result.PlayerBaseMaxHealth : stage.PlayerBaseHealth);
    }

    private static int ResolveMissionEventTarget(StageObjectiveDefinition objective)
    {
        return Mathf.Max(1, Mathf.RoundToInt(objective.Value <= 0f ? 1f : objective.Value));
    }

    private static int ResolveLateConditionTriggerLimit(StageDefinition stage, StageObjectiveDefinition objective)
    {
        if (objective != null && objective.Value > 0f)
        {
            return Mathf.Max(1, Mathf.RoundToInt(objective.Value));
        }

        var interval = GameState.Instance?.GetCampaignLateConditionIntervalSeconds(stage.StageNumber) ?? 18f;
        return Mathf.Max(1, Mathf.FloorToInt(Mathf.Max(interval, stage.ThreeStarTimeLimitSeconds) / interval));
    }

    private static int ResolveBossPressureTriggerLimit(StageDefinition stage, StageObjectiveDefinition objective)
    {
        if (objective != null && objective.Value > 0f)
        {
            return Mathf.Max(1, Mathf.RoundToInt(objective.Value));
        }

        return stage.StageNumber >= 51
            ? 4
            : stage.StageNumber >= 36
                ? 3
                : 2;
    }

    private static string BuildMissionEventObjectiveLabel(StageDefinition stage, StageObjectiveDefinition objective)
    {
        var required = ResolveMissionEventTarget(objective);
        var primaryMission = StageMissionEvents.GetPrimaryEvent(stage);
        if (required == 1 && primaryMission != null)
        {
            return $"Secure {StageMissionEvents.ResolveTitle(primaryMission)}";
        }

        return required == 1
            ? "Secure the battlefield objective"
            : $"Secure {required} battlefield objectives";
    }

    private static bool HasAdaptiveWaveFollowUpObjective(StageDefinition stage)
    {
        return stage != null &&
            GameState.Instance?.CurrentBattleMode == BattleRunMode.Campaign &&
            GameState.Instance.HasCampaignAdaptiveWaveRead(stage.StageNumber) &&
            stage.HasScriptedWaves &&
            stage.Waves.Length >= 2 &&
            string.IsNullOrWhiteSpace(StageEncounterIntel.GetBossPressureTitleForStage(stage));
    }

    private static string NormalizeType(string type)
    {
        return string.IsNullOrWhiteSpace(type)
            ? "clear_route"
            : type.Trim().ToLowerInvariant();
    }

    private static string BuildLateConditionLimitLabel(StageDefinition stage, StageObjectiveDefinition objective)
    {
        var failCount = ResolveLateConditionTriggerLimit(stage, objective) + 1;
        var title = GameState.Instance?.GetCampaignLateConditionTitle(stage.MapId) ?? "late district condition";
        return $"Clear before {title} hits {failCount} times";
    }

    private static string BuildBossPressureLimitLabel(StageDefinition stage, StageObjectiveDefinition objective)
    {
        var failCount = ResolveBossPressureTriggerLimit(stage, objective) + 1;
        var title = StageEncounterIntel.GetBossPressureTitleForStage(stage);
        return $"Clear before {title} triggers {failCount} times";
    }

    private static string BuildAdaptiveWaveFollowUpLabel(StageDefinition stage)
    {
        return "Master the forced adaptive-wave branch";
    }

    private static string BuildAdaptiveWaveBranchClause(StageBattleResult result)
    {
        if (result.CampaignAdaptiveWaveBranchSpawnCount <= 0 ||
            string.IsNullOrWhiteSpace(result.CampaignAdaptiveWaveBranchLabel))
        {
            return "";
        }

        if (string.IsNullOrWhiteSpace(result.CampaignAdaptiveWaveBranchWaveLabel))
        {
            return $"  |  {result.CampaignAdaptiveWaveBranchLabel} active";
        }

        return $"  |  {result.CampaignAdaptiveWaveBranchLabel} in {result.CampaignAdaptiveWaveBranchWaveLabel}";
    }

    private static StageObjectiveLiveStatus BuildBusHullLiveStatus(
        StageDefinition stage,
        StageObjectiveDefinition objective,
        StageBattleResult result,
        string label)
    {
        var threshold = ResolveBusHullThreshold(stage, objective);
        var currentRatio = result.PlayerBaseHealth / ResolvePlayerBaseReference(stage, result);
        var currentPercent = Mathf.RoundToInt(currentRatio * 100f);
        var thresholdPercent = Mathf.RoundToInt(threshold * 100f);
        var state = currentRatio >= threshold
            ? StageObjectiveLiveState.Completed
            : StageObjectiveLiveState.Failed;

        return new StageObjectiveLiveStatus
        {
            Label = label,
            Detail = $"Current {currentPercent}% / Need {thresholdPercent}%",
            State = state
        };
    }

    private static StageObjectiveLiveStatus BuildTimeLimitLiveStatus(
        StageDefinition stage,
        StageObjectiveDefinition objective,
        StageBattleResult result,
        string label)
    {
        var timeLimit = ResolveTimeLimit(stage, objective);
        var state = result.Elapsed <= timeLimit
            ? StageObjectiveLiveState.Active
            : StageObjectiveLiveState.Failed;

        return new StageObjectiveLiveStatus
        {
            Label = label,
            Detail = $"{result.Elapsed:0.0}s / {timeLimit:0.0}s",
            State = state
        };
    }

    private static StageObjectiveLiveStatus BuildDeployLimitLiveStatus(
        StageObjectiveDefinition objective,
        StageBattleResult result,
        string label)
    {
        var limit = Mathf.RoundToInt(Mathf.Max(1f, objective.Value));
        var state = result.PlayerDeployments <= limit
            ? StageObjectiveLiveState.Active
            : StageObjectiveLiveState.Failed;

        return new StageObjectiveLiveStatus
        {
            Label = label,
            Detail = $"{result.PlayerDeployments}/{limit} deployed",
            State = state
        };
    }

    private static StageObjectiveLiveStatus BuildEnemyDefeatLiveStatus(
        StageObjectiveDefinition objective,
        StageBattleResult result,
        string label)
    {
        var target = Mathf.RoundToInt(Mathf.Max(1f, objective.Value));
        var state = result.EnemyDefeats >= target
            ? StageObjectiveLiveState.Completed
            : StageObjectiveLiveState.Active;

        return new StageObjectiveLiveStatus
        {
            Label = label,
            Detail = $"{result.EnemyDefeats}/{target} defeated",
            State = state
        };
    }

    private static StageObjectiveLiveStatus BuildHazardHitLiveStatus(
        StageObjectiveDefinition objective,
        StageBattleResult result,
        string label)
    {
        var limit = Mathf.RoundToInt(Mathf.Max(0f, objective.Value));
        var state = result.PlayerHazardHits <= limit
            ? StageObjectiveLiveState.Active
            : StageObjectiveLiveState.Failed;

        return new StageObjectiveLiveStatus
        {
            Label = label,
            Detail = $"{result.PlayerHazardHits}/{limit} hazard hits",
            State = state
        };
    }

    private static StageObjectiveLiveStatus BuildSignalJamLiveStatus(
        StageObjectiveDefinition objective,
        StageBattleResult result,
        string label)
    {
        var limit = Mathf.Max(0f, objective.Value);
        var state = result.PlayerSignalJamSeconds <= limit
            ? StageObjectiveLiveState.Active
            : StageObjectiveLiveState.Failed;

        return new StageObjectiveLiveStatus
        {
            Label = label,
            Detail = $"{result.PlayerSignalJamSeconds:0.0}s / {limit:0.0}s jammed",
            State = state
        };
    }

    private static StageObjectiveLiveStatus BuildMissionEventLiveStatus(
        StageObjectiveDefinition objective,
        StageBattleResult result,
        string label)
    {
        var required = ResolveMissionEventTarget(objective);
        var remaining = Mathf.Max(0, result.TotalMissionEvents - result.CompletedMissionEvents - result.FailedMissionEvents);
        var state = result.CompletedMissionEvents >= required
            ? StageObjectiveLiveState.Completed
            : result.CompletedMissionEvents + remaining < required
                ? StageObjectiveLiveState.Failed
                : StageObjectiveLiveState.Active;

        var detail = result.TotalMissionEvents <= 0
            ? "No battlefield objective authored"
            : $"{result.CompletedMissionEvents}/{required} secured  |  {remaining} pending  |  {result.FailedMissionEvents} lost";

        return new StageObjectiveLiveStatus
        {
            Label = label,
            Detail = detail,
            State = state
        };
    }

    private static StageObjectiveLiveStatus BuildLateConditionLimitLiveStatus(
        StageDefinition stage,
        StageObjectiveDefinition objective,
        StageBattleResult result,
        string label)
    {
        var limit = ResolveLateConditionTriggerLimit(stage, objective);
        var triggers = Mathf.Max(0, result.CampaignLateConditionTriggers);
        if (triggers > limit)
        {
            return new StageObjectiveLiveStatus
            {
                Label = label,
                Detail = $"{triggers}/{limit} safe pulses spent",
                State = StageObjectiveLiveState.Failed
            };
        }

        var detail = triggers == limit
            ? $"{triggers}/{limit} safe pulses spent  |  next pulse fails"
            : $"{triggers}/{limit} safe pulses spent";
        return new StageObjectiveLiveStatus
        {
            Label = label,
            Detail = detail,
            State = StageObjectiveLiveState.Active
        };
    }

    private static StageObjectiveLiveStatus BuildBossPressureLimitLiveStatus(
        StageDefinition stage,
        StageObjectiveDefinition objective,
        StageBattleResult result,
        string label)
    {
        var limit = ResolveBossPressureTriggerLimit(stage, objective);
        var triggers = Mathf.Max(0, result.CampaignBossPressureTriggers);
        if (triggers > limit)
        {
            return new StageObjectiveLiveStatus
            {
                Label = label,
                Detail = $"{triggers}/{limit} safe boss commands spent",
                State = StageObjectiveLiveState.Failed
            };
        }

        var detail = triggers == limit
            ? $"{triggers}/{limit} safe boss commands spent  |  next command fails"
            : $"{triggers}/{limit} safe boss commands spent";
        return new StageObjectiveLiveStatus
        {
            Label = label,
            Detail = detail,
            State = StageObjectiveLiveState.Active
        };
    }

    private static StageObjectiveLiveStatus BuildAdaptiveWaveFollowUpLiveStatus(
        StageBattleResult result,
        string label)
    {
        var choiceLabel = string.IsNullOrWhiteSpace(result.CampaignAdaptiveWaveChoiceLabel)
            ? "override"
            : result.CampaignAdaptiveWaveChoiceLabel;
        var followUpLabel = string.IsNullOrWhiteSpace(result.CampaignAdaptiveWaveFollowUpLabel)
            ? "follow-up"
            : result.CampaignAdaptiveWaveFollowUpLabel;

        if (result.CampaignAdaptiveWaveFollowUpCompleted)
        {
            return new StageObjectiveLiveStatus
            {
                Label = label,
                Detail = $"{followUpLabel} secured{BuildAdaptiveWaveBranchClause(result)}",
                State = StageObjectiveLiveState.Completed
            };
        }

        if (result.CampaignAdaptiveWaveFollowUpFailed)
        {
            return new StageObjectiveLiveStatus
            {
                Label = label,
                Detail = $"{followUpLabel} slipped{BuildAdaptiveWaveBranchClause(result)}",
                State = StageObjectiveLiveState.Failed
            };
        }

        if (result.CampaignAdaptiveWaveFollowUpActive)
        {
            var detail = result.CampaignAdaptiveWaveFollowUpMode switch
            {
                "hold" => $"{followUpLabel}: hold {result.CampaignAdaptiveWaveFollowUpTimer:0.0}s without hull damage",
                "defeats" => $"{followUpLabel}: {Mathf.RoundToInt(result.CampaignAdaptiveWaveFollowUpProgress)}/{Mathf.RoundToInt(result.CampaignAdaptiveWaveFollowUpTarget)} enemy defeats in {result.CampaignAdaptiveWaveFollowUpTimer:0.0}s",
                _ => $"{followUpLabel}: {Mathf.RoundToInt(result.CampaignAdaptiveWaveFollowUpProgress)}/{Mathf.RoundToInt(result.CampaignAdaptiveWaveFollowUpTarget)} keep damage in {result.CampaignAdaptiveWaveFollowUpTimer:0.0}s"
            };
            return new StageObjectiveLiveStatus
            {
                Label = label,
                Detail = $"{detail}{BuildAdaptiveWaveBranchClause(result)}",
                State = StageObjectiveLiveState.Active
            };
        }

        if (result.CampaignAdaptiveWaveOverrideQueued)
        {
            return new StageObjectiveLiveStatus
            {
                Label = label,
                Detail = $"{choiceLabel} queued for the next scripted wave{BuildAdaptiveWaveBranchClause(result)}",
                State = StageObjectiveLiveState.Active
            };
        }

        if (result.CampaignAdaptiveWaveChoiceUsed)
        {
            var detail = result.CampaignAdaptiveWaveRewardReady
                ? $"{choiceLabel} payout armed  |  follow-up pending"
                : $"{choiceLabel} committed  |  clear the forced read to arm the follow-up";
            return new StageObjectiveLiveStatus
            {
                Label = label,
                Detail = $"{detail}{BuildAdaptiveWaveBranchClause(result)}",
                State = StageObjectiveLiveState.Active
            };
        }

        if (result.CampaignAdaptiveWaveChoiceReady)
        {
            return new StageObjectiveLiveStatus
            {
                Label = label,
                Detail = "[V] Rescue or [B] Breakthrough ready",
                State = StageObjectiveLiveState.Active
            };
        }

        return new StageObjectiveLiveStatus
        {
            Label = label,
            Detail = "Spend the first adaptive read to unlock Rescue / Breakthrough",
            State = StageObjectiveLiveState.Active
        };
    }
}
