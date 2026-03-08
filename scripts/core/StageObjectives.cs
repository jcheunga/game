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
        var objectives = ResolveObjectives(stage);
        var outcomes = objectives
            .Select(objective => new StageObjectiveOutcome
            {
                Label = BuildObjectiveLabel(stage, objective),
                Completed = EvaluateObjective(stage, objective, result)
            })
            .ToArray();

        return new StageObjectiveEvaluation
        {
            Outcomes = outcomes,
            StarsEarned = Mathf.Clamp(outcomes.Count(outcome => outcome.Completed), 1, 3)
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
        int rewardScrap,
        int rewardFuel,
        int bestStars)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Reward: +{rewardScrap} scrap, +{rewardFuel} fuel.");
        builder.AppendLine($"Stars earned: {evaluation.StarsEarned}/3   |   Best: {bestStars}/3");

        for (var i = 0; i < evaluation.Outcomes.Length; i++)
        {
            var prefix = evaluation.Outcomes[i].Completed ? "[OK]" : "[--]";
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
        StageBattleResult result)
    {
        var type = NormalizeType(objective.Type);
        return type switch
        {
            "clear_route" => true,
            "bus_hull_ratio" => result.PlayerBaseHealth / ResolvePlayerBaseReference(stage, result) >= ResolveBusHullThreshold(stage, objective),
            "clear_within" => result.Elapsed <= ResolveTimeLimit(stage, objective),
            "deploy_limit" => result.PlayerDeployments <= Mathf.RoundToInt(Mathf.Max(1f, objective.Value)),
            "enemy_defeats" => result.EnemyDefeats >= Mathf.RoundToInt(Mathf.Max(1f, objective.Value)),
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
            "bus_hull_ratio" => $"Finish with bus hull >= {Mathf.RoundToInt(ResolveBusHullThreshold(stage, objective) * 100f)}%",
            "clear_within" => $"Clear within {ResolveTimeLimit(stage, objective):0}s",
            "deploy_limit" => $"Deploy no more than {Mathf.RoundToInt(Mathf.Max(1f, objective.Value))} units",
            "enemy_defeats" => $"Defeat at least {Mathf.RoundToInt(Mathf.Max(1f, objective.Value))} enemies",
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

    private static string NormalizeType(string type)
    {
        return string.IsNullOrWhiteSpace(type)
            ? "clear_route"
            : type.Trim().ToLowerInvariant();
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
}
