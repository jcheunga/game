using System;
using System.Linq;
using Godot;

public sealed class AsyncChallengeMutatorDefinition
{
    public AsyncChallengeMutatorDefinition(
        string id,
        string code,
        string title,
        string summary,
        float enemyHealthScale,
        float enemyDamageScale,
        float enemyBaseHealthScale,
        float playerBaseHealthScale,
        float courageGainScale,
        float courageMaxBonus,
        float deployCooldownScale,
        float scoreMultiplier)
    {
        Id = id;
        Code = code;
        Title = title;
        Summary = summary;
        EnemyHealthScale = enemyHealthScale;
        EnemyDamageScale = enemyDamageScale;
        EnemyBaseHealthScale = enemyBaseHealthScale;
        PlayerBaseHealthScale = playerBaseHealthScale;
        CourageGainScale = courageGainScale;
        CourageMaxBonus = courageMaxBonus;
        DeployCooldownScale = deployCooldownScale;
        ScoreMultiplier = scoreMultiplier;
    }

    public string Id { get; }
    public string Code { get; }
    public string Title { get; }
    public string Summary { get; }
    public float EnemyHealthScale { get; }
    public float EnemyDamageScale { get; }
    public float EnemyBaseHealthScale { get; }
    public float PlayerBaseHealthScale { get; }
    public float CourageGainScale { get; }
    public float CourageMaxBonus { get; }
    public float DeployCooldownScale { get; }
    public float ScoreMultiplier { get; }
}

public sealed class AsyncChallengeDefinition
{
    public AsyncChallengeDefinition(int stage, int seed, AsyncChallengeMutatorDefinition mutator)
    {
        Stage = Math.Max(1, stage);
        Seed = Mathf.Clamp(seed, 1000, 9999);
        MutatorId = mutator.Id;
        Code = $"CH-{Stage:00}-{mutator.Code}-{Seed:0000}";
    }

    public string Code { get; }
    public int Stage { get; }
    public int Seed { get; }
    public string MutatorId { get; }
}

public static class AsyncChallengeCatalog
{
    public const string PressureSpikeId = "pressure_spike";
    public const string RationedRunId = "rationed_run";
    public const string SiegeNightId = "siege_night";

    private static readonly AsyncChallengeMutatorDefinition[] Mutators =
    {
        new(
            PressureSpikeId,
            "PRS",
            "Pressure Spike",
            "Enemy bodies are tougher and hit harder. Best for players who want the cleanest direct combat benchmark.",
            1.18f,
            1.12f,
            1.08f,
            1f,
            1f,
            0f,
            1f,
            1.25f),
        new(
            RationedRunId,
            "RAT",
            "Rationed Run",
            "Convoy courage income is thinner and squad recovery drags. Strong test of deck order and discipline.",
            1.08f,
            1.05f,
            1f,
            1f,
            0.82f,
            -8f,
            1.12f,
            1.32f),
        new(
            SiegeNightId,
            "SGE",
            "Siege Night",
            "The barricade is reinforced and the bus starts the run under heavier siege conditions.",
            1.1f,
            1.08f,
            1.18f,
            0.92f,
            0.94f,
            0f,
            1.05f,
            1.38f)
    };

    public static AsyncChallengeMutatorDefinition[] GetAll()
    {
        return Mutators;
    }

    public static AsyncChallengeMutatorDefinition GetMutator(string mutatorId)
    {
        foreach (var mutator in Mutators)
        {
            if (mutator.Id.Equals(mutatorId, StringComparison.OrdinalIgnoreCase))
            {
                return mutator;
            }
        }

        return Mutators[0];
    }

    public static string NormalizeMutatorId(string mutatorId)
    {
        return GetMutator(mutatorId).Id;
    }

    public static AsyncChallengeDefinition Create(int stage, string mutatorId, int seed)
    {
        return new AsyncChallengeDefinition(stage, seed, GetMutator(mutatorId));
    }

    public static AsyncChallengeDefinition Generate(int stage, string mutatorId)
    {
        var rng = new RandomNumberGenerator();
        rng.Randomize();
        return Create(stage, mutatorId, rng.RandiRange(1000, 9999));
    }

    public static bool TryParse(string code, out AsyncChallengeDefinition challenge, out string message)
    {
        var normalized = NormalizeCode(code);
        var parts = normalized.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 4 || !parts[0].Equals("CH", StringComparison.OrdinalIgnoreCase))
        {
            challenge = Create(1, PressureSpikeId, 1001);
            message = "Challenge code must look like CH-04-PRS-4821.";
            return false;
        }

        if (!int.TryParse(parts[1], out var stage) || stage < 1)
        {
            challenge = Create(1, PressureSpikeId, 1001);
            message = "Challenge stage was invalid.";
            return false;
        }

        var mutator = Mutators.FirstOrDefault(entry => entry.Code.Equals(parts[2], StringComparison.OrdinalIgnoreCase));
        if (mutator == null)
        {
            challenge = Create(stage, PressureSpikeId, 1001);
            message = "Challenge mutator code was invalid.";
            return false;
        }

        if (!int.TryParse(parts[3], out var seed))
        {
            challenge = Create(stage, mutator.Id, 1001);
            message = "Challenge seed was invalid.";
            return false;
        }

        challenge = Create(stage, mutator.Id, seed);
        message = "";
        return true;
    }

    public static string NormalizeCode(string code)
    {
        return string.IsNullOrWhiteSpace(code)
            ? ""
            : code.Trim().ToUpperInvariant();
    }

    public static int CalculateScore(AsyncChallengeDefinition challenge, StageBattleResult result, bool won, int starsEarned)
    {
        var mutator = GetMutator(challenge.MutatorId);
        var baseHullRatio = result.PlayerBaseMaxHealth <= 0f
            ? 0f
            : Mathf.Clamp(result.PlayerBaseHealth / result.PlayerBaseMaxHealth, 0f, 1f);
        var completionBonus = won ? 1000 : 250;
        var starBonus = starsEarned * 240;
        var killBonus = result.EnemyDefeats * 22;
        var hullBonus = Mathf.RoundToInt(baseHullRatio * 280f);
        var deployPenalty = result.PlayerDeployments * 18;
        var timeBonus = won
            ? Mathf.Max(0, Mathf.RoundToInt((120f - Mathf.Min(120f, result.Elapsed)) * 10f))
            : Mathf.RoundToInt(Mathf.Min(result.Elapsed, 90f) * 2.5f);

        var rawScore = completionBonus + starBonus + killBonus + hullBonus + timeBonus - deployPenalty;
        return Mathf.Max(0, Mathf.RoundToInt(rawScore * mutator.ScoreMultiplier));
    }

    public static string BuildSummary(AsyncChallengeDefinition challenge)
    {
        var mutator = GetMutator(challenge.MutatorId);
        return
            $"Code: {challenge.Code}\n" +
            $"Mutator: {mutator.Title}\n" +
            $"{mutator.Summary}\n" +
            $"Score multiplier: x{mutator.ScoreMultiplier:0.##}";
    }
}
