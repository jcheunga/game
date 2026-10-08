using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>Moves saves from the 60-stage campaign (six stages per zone, postgame stages 47-60) onto ten
/// contiguous ten-stage zones. The mapping is produced by scripts/analysis/expand_campaign.py.</summary>
public static class CampaignRenumbering
{
    public const int LegacyStageCount = 60;
    // Old stage N is now stage LegacyToCurrent[N - 1].
    private static readonly int[] LegacyToCurrent =
    {
        1, 2, 4, 10, 11, 12, 14, 15, 21, 22, 24, 30, 31, 32, 34, 35, 41, 42, 44, 46,
        50, 51, 52, 54, 57, 55, 61, 62, 64, 66, 70, 71, 72, 74, 76, 80, 81, 82, 84, 86,
        90, 91, 92, 94, 97, 95, 6, 17, 26, 37, 48, 60, 68, 78, 88, 100, 8, 20, 28, 40,
    };
    // Milestone rewards follow their reward, not their old stage: relics moved to zone bosses.
    private static readonly Dictionary<int, int> LegacyMilestones = new()
    {
        [4] = 10, [8] = 20, [12] = 30, [16] = 40, [18] = 23, [23] = 45, [26] = 50, [28] = 33, [31] = 60,
        [33] = 38, [38] = 53, [41] = 70, [43] = 58, [46] = 80, [47] = 68, [53] = 78, [56] = 90, [60] = 100,
    };
    private static readonly Regex SiteId = new(@"^(leader|supply|landmark)-(\d+)$", RegexOptions.Compiled);

    public static int Stage(int legacy) => legacy >= 1 && legacy <= LegacyStageCount ? LegacyToCurrent[legacy - 1] : legacy;

    /// <summary>True for saves written before the campaign grew to ten stages per zone. Their stage arrays
    /// hold at most the 60 legacy stages; newer saves size them to the current campaign.</summary>
    public static bool NeedsMigration(GameSaveData saved) => saved.Version < 46 && (saved.StageStars?.Length ?? 0) <= LegacyStageCount
        && (saved.HardModeStars?.Length ?? 0) <= LegacyStageCount;

    public static void Migrate(GameSaveData saved, int stageCount)
    {
        if (!NeedsMigration(saved)) return;
        saved.StageStars = Remap(saved.StageStars, stageCount);
        saved.HardModeStars = Remap(saved.HardModeStars, stageCount);
        var cleared = Enumerable.Range(1, saved.StageStars.Length).Where(stage => saved.StageStars[stage - 1] > 0).DefaultIfEmpty(0).Max();
        saved.HighestUnlockedStage = Math.Clamp(cleared + 1, 1, Math.Max(1, stageCount));
        saved.SelectedStage = Math.Clamp(Stage(saved.SelectedStage), 1, Math.Max(1, stageCount));
        if (saved.HardModeHighestCleared > 0) saved.HardModeHighestCleared = Stage(saved.HardModeHighestCleared);
        saved.ClaimedProgressionMilestones = (saved.ClaimedProgressionMilestones ?? Array.Empty<int>())
            .Select(stage => LegacyMilestones.GetValueOrDefault(stage, Stage(stage))).Distinct().ToArray();
        saved.ClaimedStageMasteryRewards = (saved.ClaimedStageMasteryRewards ?? Array.Empty<int>()).Select(Stage).Distinct().ToArray();
        saved.VisitedAdventureSites = (saved.VisitedAdventureSites ?? Array.Empty<string>()).Select(Site).ToArray();
        saved.AdventureOpenTiles = (saved.AdventureOpenTiles ?? Array.Empty<string>()).Select(Site).ToArray();
        saved.AdventureReachedTiles = (saved.AdventureReachedTiles ?? Array.Empty<string>()).Select(Site).ToArray();
        saved.AdventureCaravanTiles = Remap(saved.AdventureCaravanTiles);
        foreach (var run in saved.ChallengeHistory ?? new()) run.Stage = Stage(run.Stage);
        foreach (var pending in saved.PendingChallengeSubmissions ?? new()) pending.Stage = Stage(pending.Stage);
    }

    private static int[] Remap(int[] legacy, int stageCount)
    {
        var result = new int[Math.Max(1, stageCount)];
        for (var i = 0; i < (legacy?.Length ?? 0); i++)
        {
            var stage = Stage(i + 1);
            if (stage <= result.Length) result[stage - 1] = Math.Max(result[stage - 1], legacy[i]);
        }
        return result;
    }

    private static string Site(string id) => id != null && SiteId.Match(id) is { Success: true } m
        ? $"{m.Groups[1].Value}-{Stage(int.Parse(m.Groups[2].Value))}" : id;

    private static Dictionary<string, string> Remap(Dictionary<string, string> nodes) =>
        (nodes ?? new()).ToDictionary(pair => pair.Key, pair => Site(pair.Value));
}
