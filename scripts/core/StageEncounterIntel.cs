using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

public static class StageEncounterIntel
{
    public static string ResolveThreatRating(StageDefinition stage)
    {
        var threatScore = CalculateThreatScore(stage);
        return threatScore switch
        {
            < 1.75f => "Low",
            < 2.4f => "Moderate",
            < 3.15f => "High",
            < 4.0f => "Severe",
            _ => "Extreme"
        };
    }

    public static string BuildCompactSummary(StageDefinition stage)
    {
        return BuildCompactSummary(stage, false);
    }

    public static string BuildCampaignCompactSummary(StageDefinition stage)
    {
        return BuildCompactSummary(stage, true);
    }

    private static string BuildCompactSummary(StageDefinition stage, bool includeCampaignMissionFallback)
    {
        if (stage == null)
        {
            return "Encounter intel unavailable.";
        }

        if (!stage.HasScriptedWaves)
        {
            var dynamicMissionSummary = ResolveMissionInlineSummary(stage, includeCampaignMissionFallback);
            var missionLine = dynamicMissionSummary.Length > 0
                ? $"Mission events: {dynamicMissionSummary}"
                : "";
            return
                $"Threat: {ResolveThreatRating(stage)}\n" +
                "Pressure: dynamic undead activity\n" +
                missionLine +
                (missionLine.Length > 0 ? "\n" : "") +
                $"Modifiers: {StageModifiers.BuildInlineSummary(stage)}\n" +
                $"Hazards: {StageHazards.BuildInlineSummary(stage)}";
        }

        var counts = BuildUnitCounts(stage, out var totalEnemies);
        var firstWave = stage.Waves.OrderBy(wave => wave.TriggerTime).FirstOrDefault();
        var peakWave = stage.Waves
            .OrderByDescending(wave => wave.Entries.Sum(entry => entry == null ? 0 : Mathf.Max(1, entry.Count)))
            .ThenBy(wave => wave.TriggerTime)
            .FirstOrDefault();
        var bossWave = FindBossWave(stage);
        var bossUnitId = TryGetBossUnitId(bossWave);
        var supportPressure = BuildSupportPressureSummary(counts);

        var missionInlineSummary = ResolveMissionInlineSummary(stage, includeCampaignMissionFallback);
        var missionSummary = missionInlineSummary.Length > 0
            ? $"Mission events: {missionInlineSummary}"
            : "";

        return
            $"Threat: {ResolveThreatRating(stage)}  |  Contacts: {totalEnemies}  |  Enemy types: {counts.Count}\n" +
            $"First contact: {(firstWave == null ? "dynamic" : $"{firstWave.TriggerTime:0.#}s")}  |  " +
            $"Peak wave: {(peakWave == null ? "n/a" : $"{peakWave.TriggerTime:0.#}s")}  |  " +
            $"Boss: {(bossWave == null ? "none" : $"{bossWave.TriggerTime:0.#}s")}  |  " +
            $"Boss phase: {(string.IsNullOrWhiteSpace(bossUnitId) ? "n/a" : GetBossPhaseTitle(bossUnitId))}\n" +
            $"{supportPressure}\n" +
            $"{missionSummary}" +
            (missionSummary.Length > 0 ? "\n" : "") +
            $"Modifiers: {StageModifiers.BuildInlineSummary(stage)}  |  Hazards: {StageHazards.BuildInlineSummary(stage)}";
    }

    public static string BuildEncounterIntel(StageDefinition stage)
    {
        return BuildEncounterIntel(stage, false);
    }

    public static string BuildCampaignEncounterIntel(StageDefinition stage)
    {
        return BuildEncounterIntel(stage, true);
    }

    private static string BuildEncounterIntel(StageDefinition stage, bool includeCampaignMissionFallback)
    {
        if (stage == null)
        {
            return "Encounter intel unavailable.";
        }

        if (!stage.HasScriptedWaves)
        {
            return
                "Encounter intel:\n" +
                $"Threat rating: {ResolveThreatRating(stage)}\n" +
                "Roaming undead pressure with dynamic composition.";
        }

        var counts = BuildUnitCounts(stage, out var totalEnemies);
        var topThreats = counts
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => GameData.GetUnit(pair.Key).DisplayName)
            .Take(5)
            .Select(pair => $"{GameData.GetUnit(pair.Key).DisplayName} x{pair.Value}");

        var bossWave = FindBossWave(stage);
        var bossUnitId = TryGetBossUnitId(bossWave);
        var peakWave = stage.Waves
            .OrderByDescending(wave => wave.Entries.Sum(entry => entry == null ? 0 : Mathf.Max(1, entry.Count)))
            .ThenBy(wave => wave.TriggerTime)
            .First();

        var builder = new StringBuilder();
        builder.AppendLine("Encounter intel:");
        builder.AppendLine($"Threat rating: {ResolveThreatRating(stage)}");
        builder.AppendLine(
            $"Scheduled contacts: {totalEnemies}  |  Enemy types: {counts.Count}  |  Peak wave: {peakWave.TriggerTime:0}s");
        builder.AppendLine($"Threat mix: {string.Join(", ", topThreats)}");
        builder.AppendLine(BuildSupportPressureSummary(counts));
        var missionInlineSummary = ResolveMissionInlineSummary(stage, includeCampaignMissionFallback);
        if (missionInlineSummary.Length > 0)
        {
            builder.AppendLine($"Battlefield events: {missionInlineSummary}");
        }
        builder.AppendLine($"Hazards: {StageHazards.BuildInlineSummary(stage)}");

        if (bossWave != null)
        {
            var bossName = GameData.GetUnit(bossUnitId).DisplayName;
            builder.AppendLine(
                $"Boss warning: {bossWave.TriggerTime:0}s  |  {bossName}  |  " +
                $"Phase at ~55% HP: {BuildBossPhaseSummary(bossUnitId)}");
        }
        else
        {
            builder.AppendLine("Boss warning: none on this route.");
        }

        var spellSuggestions = BuildSpellCounterSuggestions(counts);
        if (spellSuggestions.Count > 0)
        {
            builder.Append("Recommended spells: ");
            builder.Append(string.Join(", ", spellSuggestions.Select(s => $"{s.SpellName} ({s.Reason})")));
        }

        return builder.ToString();
    }

    public static string BuildSupportPressureSummary(StageDefinition stage)
    {
        var counts = BuildUnitCounts(stage, out _);
        return BuildSupportPressureSummary(counts);
    }

    public static string GetBossPhaseTitleForStage(StageDefinition stage)
    {
        return GetBossPhaseTitle(GetBossUnitIdForStage(stage));
    }

    public static string BuildBossPhaseWarning(StageDefinition stage)
    {
        var bossUnitId = GetBossUnitIdForStage(stage);
        return string.IsNullOrWhiteSpace(bossUnitId)
            ? ""
            : $"Boss phase: {GetBossPhaseTitle(bossUnitId)} at ~55% HP. {BuildBossPhaseSummary(bossUnitId)}";
    }

    public static string GetBossPhaseTitle(string bossUnitId)
    {
        return NormalizeBossUnitId(bossUnitId) switch
        {
            GameData.EnemyBossDocksId => "Undertow",
            GameData.EnemyBossForgeId => "Forge Surge",
            GameData.EnemyBossWardId => "Blackout Bloom",
            GameData.EnemyBossPassId => "War Stampede",
            GameData.EnemyBossBasilicaId => "Crypt Vow",
            GameData.EnemyBossMireId => "Rot Swell",
            GameData.EnemyBossSteppeId => "Wolf Run",
            GameData.EnemyBossVergeId => "Hex Bloom",
            GameData.EnemyBossCitadelId => "Keep Ward",
            GameData.EnemyBossReliquaryId => "Catacomb Awakening",
            GameData.EnemyBossAshenRegentId => "Ashfall Edict",
            GameData.EnemyBossTidemasterId => "Floodgate Break",
            GameData.EnemyBossPlagueMonarchId => "Plague Eclipse",
            GameData.EnemyBossId => "Last Stand",
            _ => ""
        };
    }

    public static string BuildWavePressureSummary(StageWaveDefinition wave)
    {
        var pressureFlags = BuildWavePressureFlags(wave);
        return string.IsNullOrWhiteSpace(pressureFlags)
            ? "Pressure tags: standard front."
            : $"Pressure tags: {pressureFlags}";
    }

    public static string BuildWaveSummary(StageDefinition stage, int maxEntriesPerWave = 3)
    {
        if (stage == null)
        {
            return "Incoming pressure: unavailable.";
        }

        if (!stage.HasScriptedWaves)
        {
            return "Incoming pressure: dynamic random wave pacing.";
        }

        var builder = new StringBuilder();
        builder.AppendLine($"Scripted waves: {stage.Waves.Length}");

        for (var i = 0; i < stage.Waves.Length; i++)
        {
            var wave = stage.Waves[i];
            var label = string.IsNullOrWhiteSpace(wave.Label) ? $"Wave {i + 1}" : wave.Label;
            var pressureFlags = BuildWavePressureFlags(wave);
            builder.AppendLine(
                $"{i + 1}. {wave.TriggerTime:0}s  |  {label}  |  {BuildWaveEntrySummary(wave, maxEntriesPerWave)}" +
                (string.IsNullOrWhiteSpace(pressureFlags) ? "" : $"  |  {pressureFlags}"));
        }

        return builder.ToString().TrimEnd();
    }

    public static Dictionary<string, int> BuildUnitCounts(StageDefinition stage, out int totalEnemies)
    {
        var counts = new Dictionary<string, int>();
        totalEnemies = 0;

        if (stage?.Waves == null)
        {
            return counts;
        }

        foreach (var wave in stage.Waves)
        {
            foreach (var entry in wave.Entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.UnitId))
                {
                    continue;
                }

                var count = Mathf.Max(1, entry.Count);
                totalEnemies += count;
                counts[entry.UnitId] = counts.TryGetValue(entry.UnitId, out var existing)
                    ? existing + count
                    : count;
            }
        }

        return counts;
    }

    private static string BuildWaveEntrySummary(StageWaveDefinition wave, int maxEntries)
    {
        var parts = new List<string>();
        var hiddenEntries = 0;

        for (var i = 0; i < wave.Entries.Length; i++)
        {
            var entry = wave.Entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.UnitId))
            {
                continue;
            }

            if (parts.Count >= maxEntries)
            {
                hiddenEntries++;
                continue;
            }

            var displayName = GameData.GetUnit(entry.UnitId).DisplayName;
            parts.Add($"{displayName} x{Mathf.Max(1, entry.Count)}");
        }

        if (hiddenEntries > 0)
        {
            parts.Add($"+{hiddenEntries} more");
        }

        return parts.Count > 0
            ? string.Join(", ", parts)
            : "No enemy composition data.";
    }

    private static string BuildSupportPressureSummary(Dictionary<string, int> counts)
    {
        if (counts == null || counts.Count == 0)
        {
            return "Support pressure: none.";
        }

        var pressure = new List<string>();

        if (counts.TryGetValue(GameData.EnemyHowlerId, out var howlerCount) && howlerCount > 0)
        {
            pressure.Add($"{GameData.GetUnit(GameData.EnemyHowlerId).DisplayName} x{howlerCount} (aura buffs)");
        }

        if (counts.TryGetValue(GameData.EnemyJammerId, out var jammerCount) && jammerCount > 0)
        {
            pressure.Add($"{GameData.GetUnit(GameData.EnemyJammerId).DisplayName} x{jammerCount} (courage hex)");
        }

        if (counts.TryGetValue(GameData.EnemySaboteurId, out var saboteurCount) && saboteurCount > 0)
        {
            pressure.Add($"{GameData.GetUnit(GameData.EnemySaboteurId).DisplayName} x{saboteurCount} (war wagon dives)");
        }

        if (counts.TryGetValue(GameData.EnemySpitterId, out var spitterCount) && spitterCount > 0)
        {
            pressure.Add($"{GameData.GetUnit(GameData.EnemySpitterId).DisplayName} x{spitterCount} (ranged blight)");
        }

        if (counts.TryGetValue(GameData.EnemyShieldWallId, out var shieldWallCount) && shieldWallCount > 0)
        {
            pressure.Add($"{GameData.GetUnit(GameData.EnemyShieldWallId).DisplayName} x{shieldWallCount} (projectile blockers)");
        }

        if (counts.TryGetValue(GameData.EnemyLichId, out var lichCount) && lichCount > 0)
        {
            pressure.Add($"{GameData.GetUnit(GameData.EnemyLichId).DisplayName} x{lichCount} (necromancer support)");
        }

        if (counts.TryGetValue(GameData.EnemySiegeTowerId, out var siegeTowerCount) && siegeTowerCount > 0)
        {
            pressure.Add($"{GameData.GetUnit(GameData.EnemySiegeTowerId).DisplayName} x{siegeTowerCount} (siege towers)");
        }

        if (counts.TryGetValue(GameData.EnemyMirrorId, out var mirrorCount) && mirrorCount > 0)
        {
            pressure.Add($"{GameData.GetUnit(GameData.EnemyMirrorId).DisplayName} x{mirrorCount} (damage reflectors)");
        }

        if (counts.TryGetValue(GameData.EnemyTunnelerId, out var tunnelerCount) && tunnelerCount > 0)
        {
            pressure.Add($"{GameData.GetUnit(GameData.EnemyTunnelerId).DisplayName} x{tunnelerCount} (burrowers)");
        }

        return pressure.Count == 0
            ? "Support pressure: none."
            : $"Support pressure: {string.Join(", ", pressure)}";
    }

    private static string BuildWavePressureFlags(StageWaveDefinition wave)
    {
        if (wave?.Entries == null || wave.Entries.Length == 0)
        {
            return "";
        }

        var flags = new List<string>();
        if (WaveContainsUnit(wave, GameData.EnemyHowlerId))
        {
            flags.Add("buff aura");
        }

        if (WaveContainsUnit(wave, GameData.EnemyJammerId))
        {
            flags.Add("signal jam");
        }

        if (WaveContainsUnit(wave, GameData.EnemySaboteurId))
        {
            flags.Add("war wagon dive");
        }

        if (WaveContainsUnit(wave, GameData.EnemySpitterId))
        {
            flags.Add("ranged chip");
        }

        if (WaveContainsUnit(wave, GameData.EnemyShieldWallId))
        {
            flags.Add("shield wall");
        }

        if (WaveContainsUnit(wave, GameData.EnemyLichId))
        {
            flags.Add("liches");
        }

        if (WaveContainsUnit(wave, GameData.EnemySiegeTowerId))
        {
            flags.Add("siege towers");
        }

        if (WaveContainsUnit(wave, GameData.EnemyMirrorId))
        {
            flags.Add("mirror knights");
        }

        if (WaveContainsUnit(wave, GameData.EnemyTunnelerId))
        {
            flags.Add("tunnelers");
        }

        if (WaveContainsBossUnit(wave))
        {
            flags.Add("boss phase");
        }

        return string.Join(", ", flags);
    }

    private static bool WaveContainsUnit(StageWaveDefinition wave, string unitId)
    {
        for (var i = 0; i < wave.Entries.Length; i++)
        {
            var entry = wave.Entries[i];
            if (entry != null && entry.UnitId == unitId)
            {
                return true;
            }
        }

        return false;
    }

    public static string BuildNotableEnemyCallouts(StageDefinition stage, int maxCallouts = 4)
    {
        if (stage?.Waves == null || stage.Waves.Length == 0)
        {
            return "";
        }

        var seen = new HashSet<string>();
        var callouts = new List<string>();

        foreach (var wave in stage.Waves)
        {
            if (wave?.Entries == null)
            {
                continue;
            }

            foreach (var entry in wave.Entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.UnitId) || !seen.Add(entry.UnitId))
                {
                    continue;
                }

                if (callouts.Count >= maxCallouts)
                {
                    break;
                }

                var callout = entry.UnitId switch
                {
                    GameData.EnemyShieldWallId => "Shield Wall: blocks projectiles",
                    GameData.EnemyLichId => "Lich: raises fallen enemies",
                    GameData.EnemySiegeTowerId => "Siege Tower: deploys troops at your base",
                    GameData.EnemyMirrorId => "Mirror Knight: reflects damage",
                    GameData.EnemyTunnelerId => "Tunneler: burrows behind your lines",
                    _ when IsBossUnitId(entry.UnitId) =>
                        $"Boss encounter: {GameData.GetUnit(entry.UnitId).DisplayName} ({GetBossPhaseTitle(entry.UnitId)})",
                    _ => null
                };

                if (callout != null)
                {
                    callouts.Add(callout);
                }
            }

            if (callouts.Count >= maxCallouts)
            {
                break;
            }
        }

        return callouts.Count == 0 ? "" : string.Join("\n", callouts);
    }

    public static List<(string SpellName, string Reason)> BuildSpellCounterSuggestions(StageDefinition stage)
    {
        if (stage == null || !stage.HasScriptedWaves)
        {
            return new List<(string, string)>();
        }

        var counts = BuildUnitCounts(stage, out _);
        return BuildSpellCounterSuggestions(counts);
    }

    private static List<(string SpellName, string Reason)> BuildSpellCounterSuggestions(Dictionary<string, int> counts)
    {
        if (counts == null || counts.Count == 0)
        {
            return new List<(string, string)>();
        }

        var candidates = new List<(string SpellName, string Reason, int Weight)>();

        var swarmCount =
            (counts.TryGetValue(GameData.EnemyWalkerId, out var walkers) ? walkers : 0) +
            (counts.TryGetValue(GameData.EnemyRunnerId, out var runners) ? runners : 0);
        if (swarmCount >= 4)
        {
            candidates.Add(("Fireball", "AoE vs swarm", swarmCount));
            candidates.Add(("Earthquake", "AoE vs swarm", swarmCount - 1));
        }

        var rangedCount =
            (counts.TryGetValue(GameData.EnemySpitterId, out var spitters) ? spitters : 0) +
            (counts.TryGetValue(GameData.EnemyLichId, out var liches) ? liches : 0);
        if (rangedCount > 0)
        {
            candidates.Add(("Lightning Strike", "targets priority ranged enemies", rangedCount + 2));
        }

        var armorCount =
            (counts.TryGetValue(GameData.EnemyBruteId, out var brutes) ? brutes : 0) +
            (counts.TryGetValue(GameData.EnemyCrusherId, out var crushers) ? crushers : 0) +
            (counts.TryGetValue(GameData.EnemyShieldWallId, out var shields) ? shields : 0);
        if (armorCount > 0)
        {
            candidates.Add(("Polymorph", "disable heavy armor", armorCount + 1));
        }

        var howlerCount = counts.TryGetValue(GameData.EnemyHowlerId, out var howlers) ? howlers : 0;
        if (howlerCount > 0)
        {
            candidates.Add(("Frost Burst", "slow the buff pack", howlerCount + 1));
        }

        var jammerCount = counts.TryGetValue(GameData.EnemyJammerId, out var jammers) ? jammers : 0;
        if (jammerCount > 0)
        {
            candidates.Add(("War Cry", "burst through signal jam", jammerCount + 1));
        }

        var bloaterCount = counts.TryGetValue(GameData.EnemyBloaterId, out var bloaters) ? bloaters : 0;
        if (bloaterCount > 0)
        {
            candidates.Add(("Barrier Ward", "reduce death burst damage", bloaterCount));
        }

        var mirrorCount = counts.TryGetValue(GameData.EnemyMirrorId, out var mirrors) ? mirrors : 0;
        if (mirrorCount > 0)
        {
            candidates.Add(("Stone Barricade", "block without reflect damage", mirrorCount + 2));
        }

        var tunnelerCount = counts.TryGetValue(GameData.EnemyTunnelerId, out var tunnelers) ? tunnelers : 0;
        if (tunnelerCount > 0)
        {
            candidates.Add(("Heal", "sustain against rear attacks", tunnelerCount + 1));
        }

        var siegeCount = counts.TryGetValue(GameData.EnemySiegeTowerId, out var sieges) ? sieges : 0;
        if (siegeCount > 0)
        {
            candidates.Add(("Lightning Strike", "kill siege towers before arrival", siegeCount + 3));
            candidates.Add(("Fireball", "kill siege towers before arrival", siegeCount + 2));
        }

        var hasBoss = counts.Keys.Any(IsBossUnitId);
        if (hasBoss)
        {
            candidates.Add(("Resurrect", "recover from boss burst damage", 5));
        }

        var seen = new HashSet<string>();
        var results = new List<(string SpellName, string Reason)>();
        foreach (var candidate in candidates.OrderByDescending(c => c.Weight))
        {
            if (results.Count >= 3)
                break;
            if (seen.Add(candidate.SpellName))
            {
                results.Add((candidate.SpellName, candidate.Reason));
            }
        }

        return results;
    }

    private static float CalculateThreatScore(StageDefinition stage)
    {
        if (stage == null)
        {
            return 0f;
        }

        var counts = BuildUnitCounts(stage, out var totalEnemies);
        var peakWaveCount = stage.HasScriptedWaves
            ? stage.Waves.Max(wave => wave.Entries.Sum(entry => entry == null ? 0 : Mathf.Max(1, entry.Count)))
            : 0;
        var hasBoss = stage.HasScriptedWaves && stage.Waves.Any(WaveContainsBossUnit);
        var howlerCount = counts.TryGetValue(GameData.EnemyHowlerId, out var howlers) ? howlers : 0;
        var jammerCount = counts.TryGetValue(GameData.EnemyJammerId, out var jammers) ? jammers : 0;
        var saboteurCount = counts.TryGetValue(GameData.EnemySaboteurId, out var saboteurs) ? saboteurs : 0;
        var spitterCount = counts.TryGetValue(GameData.EnemySpitterId, out var spitters) ? spitters : 0;
        var shieldWallCount = counts.TryGetValue(GameData.EnemyShieldWallId, out var shieldWalls) ? shieldWalls : 0;
        var lichCount = counts.TryGetValue(GameData.EnemyLichId, out var liches) ? liches : 0;
        var siegeTowerCount = counts.TryGetValue(GameData.EnemySiegeTowerId, out var siegeTowers) ? siegeTowers : 0;
        var mirrorCount = counts.TryGetValue(GameData.EnemyMirrorId, out var mirrors) ? mirrors : 0;
        var tunnelerCount = counts.TryGetValue(GameData.EnemyTunnelerId, out var tunnelers) ? tunnelers : 0;

        return
            ((stage.EnemyHealthScale + stage.EnemyDamageScale) * 0.72f) +
            (stage.Waves.Length * 0.16f) +
            (counts.Count * 0.11f) +
            (peakWaveCount * 0.08f) +
            (Mathf.Min(totalEnemies, 30) * 0.015f) +
            (howlerCount * 0.06f) +
            (jammerCount * 0.08f) +
            (saboteurCount * 0.05f) +
            (spitterCount * 0.03f) +
            (shieldWallCount * 0.07f) +
            (lichCount * 0.08f) +
            (siegeTowerCount * 0.06f) +
            (mirrorCount * 0.05f) +
            (tunnelerCount * 0.06f) +
            (StageModifiers.HasModifiers(stage) ? stage.Modifiers.Length * 0.08f : 0f) +
            (StageHazards.HasHazards(stage) ? stage.Hazards.Length * 0.1f : 0f) +
            (StageModifiers.ResolveEnemyCapBonus(stage) * 0.12f) +
            (hasBoss ? 0.55f : 0f);
    }

    private static StageWaveDefinition FindBossWave(StageDefinition stage)
    {
        return stage?.Waves?.FirstOrDefault(WaveContainsBossUnit);
    }

    private static string GetBossUnitIdForStage(StageDefinition stage)
    {
        return TryGetBossUnitId(FindBossWave(stage));
    }

    private static string TryGetBossUnitId(StageWaveDefinition wave)
    {
        if (wave?.Entries == null)
        {
            return "";
        }

        foreach (var entry in wave.Entries)
        {
            if (entry != null && IsBossUnitId(entry.UnitId))
            {
                return entry.UnitId;
            }
        }

        return "";
    }

    private static bool WaveContainsBossUnit(StageWaveDefinition wave)
    {
        return !string.IsNullOrWhiteSpace(TryGetBossUnitId(wave));
    }

    private static bool IsBossUnitId(string unitId)
    {
        return !string.IsNullOrWhiteSpace(unitId) &&
            unitId.StartsWith(GameData.EnemyBossId, System.StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeBossUnitId(string bossUnitId)
    {
        return string.IsNullOrWhiteSpace(bossUnitId)
            ? ""
            : bossUnitId.Trim().ToLowerInvariant();
    }

    private static string BuildBossPhaseSummary(string bossUnitId)
    {
        return NormalizeBossUnitId(bossUnitId) switch
        {
            GameData.EnemyBossDocksId => "Wave surge damages, slows, and shoves the frontline.",
            GameData.EnemyBossForgeId => "Brute escorts deploy and the nearby push hardens.",
            GameData.EnemyBossWardId => "Signal jam spikes, cooldowns stall, and the line buckles.",
            GameData.EnemyBossPassId => "Fast escorts flood in and the nearby pack surges forward.",
            GameData.EnemyBossBasilicaId => "The keep recovers while ritual escorts answer the vow.",
            GameData.EnemyBossMireId => "A blight burst damages and drags down nearby defenders.",
            GameData.EnemyBossSteppeId => "Runners pour in and the nearby pack accelerates.",
            GameData.EnemyBossVergeId => "The strongest defender is hexed while witchlights thicken the screen.",
            GameData.EnemyBossCitadelId => "The keep recovers and elite escorts lock down the lane.",
            GameData.EnemyBossReliquaryId => "Bone artillery joins the fight and the keep regains ground.",
            GameData.EnemyBossAshenRegentId => "Ashen shockwaves hammer the frontline and a giant steps in.",
            GameData.EnemyBossTidemasterId => "A tidal break slams the frontline and buys the keep breathing room.",
            GameData.EnemyBossPlagueMonarchId => "A major blackout hits courage flow while captains reinforce the push.",
            GameData.EnemyBossId => "The boss rallies nearby undead for one last surge.",
            _ => "Expect a major battlefield swing near half health."
        };
    }

    private static string ResolveMissionInlineSummary(StageDefinition stage, bool includeCampaignMissionFallback)
    {
        if (includeCampaignMissionFallback)
        {
            var summary = StageMissionEvents.BuildCampaignInlineSummary(stage);
            return summary == "none" ? "" : summary;
        }

        if (!StageMissionEvents.HasMissionEvents(stage))
        {
            return "";
        }

        var authoredSummary = StageMissionEvents.BuildInlineSummary(stage);
        return authoredSummary == "none" ? "" : authoredSummary;
    }

}
