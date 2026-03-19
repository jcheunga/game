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
        float scoreMultiplier,
        float signalJamIntervalSeconds = 0f,
        float signalJamDurationSeconds = 0f,
        float signalJamCourageGainScale = 1f,
        float signalJamCooldownPenalty = 0f)
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
        SignalJamIntervalSeconds = signalJamIntervalSeconds;
        SignalJamDurationSeconds = signalJamDurationSeconds;
        SignalJamCourageGainScale = signalJamCourageGainScale;
        SignalJamCooldownPenalty = signalJamCooldownPenalty;
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
    public float SignalJamIntervalSeconds { get; }
    public float SignalJamDurationSeconds { get; }
    public float SignalJamCourageGainScale { get; }
    public float SignalJamCooldownPenalty { get; }
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

public sealed class AsyncChallengeScoreBreakdown
{
    public AsyncChallengeScoreBreakdown(
        int completionBonus,
        int starBonus,
        int killBonus,
        int hullBonus,
        int timeBonus,
        int deployPenalty,
        int rawScore,
        float multiplier,
        int finalScore)
    {
        CompletionBonus = completionBonus;
        StarBonus = starBonus;
        KillBonus = killBonus;
        HullBonus = hullBonus;
        TimeBonus = timeBonus;
        DeployPenalty = deployPenalty;
        RawScore = rawScore;
        Multiplier = multiplier;
        FinalScore = finalScore;
    }

    public int CompletionBonus { get; }
    public int StarBonus { get; }
    public int KillBonus { get; }
    public int HullBonus { get; }
    public int TimeBonus { get; }
    public int DeployPenalty { get; }
    public int RawScore { get; }
    public float Multiplier { get; }
    public int FinalScore { get; }
}

public sealed class AsyncChallengeTargetScores
{
    public AsyncChallengeTargetScores(int bronze, int silver, int gold, int ace)
    {
        Bronze = bronze;
        Silver = silver;
        Gold = gold;
        Ace = ace;
    }

    public int Bronze { get; }
    public int Silver { get; }
    public int Gold { get; }
    public int Ace { get; }
}

public static class AsyncChallengeCatalog
{
    public const string PressureSpikeId = "pressure_spike";
    public const string RationedRunId = "rationed_run";
    public const string SiegeNightId = "siege_night";
    public const string BlackoutRelayId = "blackout_relay";
    public const string MirrorFieldId = "mirror_field";
    public const string UndyingHostId = "undying_host";
    public const string SiegeWaveId = "siege_wave";
    public const string SplitterSwarmId = "splitter_swarm";
    public const string TunnelerAmbushId = "tunneler_ambush";

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
            "Caravan courage income is thinner and squad recovery drags. Strong test of deck order and discipline.",
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
            "The gatehouse is reinforced and the war wagon starts the run under heavier siege pressure.",
            1.1f,
            1.08f,
            1.18f,
            0.92f,
            0.94f,
            0f,
            1.05f,
            1.38f)
        ,
        new(
            BlackoutRelayId,
            "BLK",
            "Blackout Relay",
            "Challenge control keeps pulsing signal blackouts across the route. Time drops around forced hex windows and caravan recovery discipline.",
            1.08f,
            1.06f,
            1f,
            1f,
            1f,
            0f,
            1f,
            1.44f,
            16f,
            3.8f,
            0.52f,
            1.35f),
        new(
            MirrorFieldId,
            "MIR",
            "Mirror Field",
            "Enemy formations reflect a portion of incoming damage. Rewards precision and spell-heavy strategies.",
            1.05f,
            1.05f,
            1f,
            1f,
            1f,
            0f,
            1f,
            1.52f),
        new(
            UndyingHostId,
            "UDH",
            "Undying Host",
            "Fallen enemies have a chance to rise again. Push fast or face an endless horde.",
            1.08f,
            1f,
            1f,
            1f,
            1f,
            0f,
            1f,
            1.48f),
        new(
            SiegeWaveId,
            "SGW",
            "Siege Wave",
            "Siege Towers appear alongside regular waves. Protect the war wagon at all costs.",
            1.06f,
            1.04f,
            1f,
            1f,
            1f,
            0f,
            1f,
            1.50f),
        new(
            SplitterSwarmId,
            "SPL",
            "Splitter Swarm",
            "Splitter enemies spawn additional minions on death. Area damage is essential.",
            1f,
            1.06f,
            1f,
            1f,
            1f,
            0f,
            1f,
            1.46f),
        new(
            TunnelerAmbushId,
            "TUN",
            "Tunneler Ambush",
            "Tunnelers burrow behind your lines more frequently. Guard your rear.",
            1.04f,
            1.05f,
            1f,
            1f,
            1f,
            0f,
            1f,
            1.49f)
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
        return CalculateScoreBreakdown(challenge, result, won, starsEarned).FinalScore;
    }

    public static AsyncChallengeScoreBreakdown CalculateScoreBreakdown(
        AsyncChallengeDefinition challenge,
        StageBattleResult result,
        bool won,
        int starsEarned)
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
        var finalScore = Mathf.Max(0, Mathf.RoundToInt(rawScore * mutator.ScoreMultiplier));
        return new AsyncChallengeScoreBreakdown(
            completionBonus,
            starBonus,
            killBonus,
            hullBonus,
            timeBonus,
            deployPenalty,
            rawScore,
            mutator.ScoreMultiplier,
            finalScore);
    }

    public static string BuildScoreSummary(AsyncChallengeScoreBreakdown breakdown)
    {
        return
            $"Outcome +{breakdown.CompletionBonus}  |  Stars +{breakdown.StarBonus}  |  Kills +{breakdown.KillBonus}\n" +
            $"Hull +{breakdown.HullBonus}  |  Time +{breakdown.TimeBonus}  |  Deploys -{breakdown.DeployPenalty}\n" +
            $"Raw {breakdown.RawScore}  x{breakdown.Multiplier:0.##}  =  {breakdown.FinalScore}";
    }

    public static string BuildScoringGuide(AsyncChallengeDefinition challenge)
    {
        var mutator = GetMutator(challenge.MutatorId);
        return
            "Score model:\n" +
            "- Outcome: clear +1000, fail +250\n" +
            "- Stars: +240 each   |   Kills: +22 each   |   Hull: up to +280\n" +
            "- Time: clear up to +1200 before 120s, fail survival up to +225\n" +
            $"- Deploys: -18 each   |   Mutator multiplier: x{mutator.ScoreMultiplier:0.##}";
    }

    public static AsyncChallengeTargetScores GetTargetScores(AsyncChallengeDefinition challenge)
    {
        var mutator = GetMutator(challenge.MutatorId);
        var baseTarget = 520 + (challenge.Stage * 125);
        var bronze = Mathf.RoundToInt(baseTarget * mutator.ScoreMultiplier);
        var silver = Mathf.RoundToInt((baseTarget + 220 + (challenge.Stage * 12)) * mutator.ScoreMultiplier);
        var gold = Mathf.RoundToInt((baseTarget + 500 + (challenge.Stage * 18)) * mutator.ScoreMultiplier);
        var ace = Mathf.RoundToInt((baseTarget + 860 + (challenge.Stage * 24)) * mutator.ScoreMultiplier);

        silver = Math.Max(silver, bronze + 120);
        gold = Math.Max(gold, silver + 140);
        ace = Math.Max(ace, gold + 180);
        return new AsyncChallengeTargetScores(bronze, silver, gold, ace);
    }

    public static string ResolveMedalLabel(AsyncChallengeDefinition challenge, int score)
    {
        var targets = GetTargetScores(challenge);
        if (score >= targets.Ace)
        {
            return "Crown Ace";
        }

        if (score >= targets.Gold)
        {
            return "Gold";
        }

        if (score >= targets.Silver)
        {
            return "Silver";
        }

        if (score >= targets.Bronze)
        {
            return "Bronze";
        }

        return "No Medal";
    }

    public static string BuildTargetSummary(AsyncChallengeDefinition challenge, int score = -1)
    {
        var targets = GetTargetScores(challenge);
        var summary =
            $"Targets: Bronze {targets.Bronze}  |  Silver {targets.Silver}  |  Gold {targets.Gold}  |  Ace {targets.Ace}";
        if (score < 0)
        {
            return summary;
        }

        return $"{summary}\nCurrent tier: {ResolveMedalLabel(challenge, score)}";
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
