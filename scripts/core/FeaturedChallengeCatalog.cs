using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public sealed class FeaturedChallengeDefinition
{
    public FeaturedChallengeDefinition(
        string id,
        string title,
        string summary,
        AsyncChallengeDefinition challenge,
        IReadOnlyList<string> lockedDeckUnitIds)
    {
        Id = id;
        Title = title;
        Summary = summary;
        Challenge = challenge;
        LockedDeckUnitIds = lockedDeckUnitIds;
    }

    public string Id { get; }
    public string Title { get; }
    public string Summary { get; }
    public AsyncChallengeDefinition Challenge { get; }
    public IReadOnlyList<string> LockedDeckUnitIds { get; }
}

public static class FeaturedChallengeCatalog
{
    private static readonly (string Id, string Title, string Summary)[] DailySlotTemplates =
    {
        (
            "daily_route_trial",
            "Route Trial",
            "A cleaner early-route benchmark for quick score races and deck checks."
        ),
        (
            "daily_pressure_test",
            "Pressure Test",
            "A midline challenge built to stress pacing, cooldown discipline, and lane control."
        ),
        (
            "daily_final_push",
            "Final Push",
            "A late-route board for high-pressure clears with tighter score separation."
        ),
        (
            "daily_ambush_gauntlet",
            "Ambush Gauntlet",
            "A mid-route board with tunneler and splitter mutators in mind. Rear-line awareness is key."
        ),
        (
            "daily_siege_front",
            "Siege Front",
            "A high-stage board where siege and mirror mutators test formation discipline under pressure."
        )
    };

    public static string GetDailyRotationStamp(DateTime? localDate = null)
    {
        var date = (localDate ?? DateTime.Now).Date;
        return date.ToString("yyyy-MM-dd");
    }

    public static IReadOnlyList<FeaturedChallengeDefinition> GetDailyRotation(
        int highestUnlockedStage,
        int maxStage,
        DateTime? localDate = null)
    {
        var unlockedCap = Mathf.Clamp(highestUnlockedStage, 1, Math.Max(1, maxStage));
        var date = (localDate ?? DateTime.Now).Date;
        var mutators = AsyncChallengeCatalog.GetAll();
        var slots = new List<FeaturedChallengeDefinition>(DailySlotTemplates.Length);
        var usedStages = new HashSet<int>();

        for (var i = 0; i < DailySlotTemplates.Length; i++)
        {
            var stage = ResolveStageForSlot(i, unlockedCap, date, usedStages);
            var mutator = mutators[(date.DayOfYear + (i * 2)) % mutators.Length];
            var seed = ResolveSeedForSlot(i, date);
            var challenge = AsyncChallengeCatalog.Create(stage, mutator.Id, seed);
            var lockedDeckUnitIds = ResolveLockedDeckUnitIds(stage, i, seed);
            var slot = DailySlotTemplates[i];
            slots.Add(new FeaturedChallengeDefinition(slot.Id, slot.Title, slot.Summary, challenge, lockedDeckUnitIds));
        }

        return slots;
    }

    private static int ResolveStageForSlot(int slotIndex, int unlockedCap, DateTime date, HashSet<int> usedStages)
    {
        if (unlockedCap <= 1)
        {
            usedStages.Add(1);
            return 1;
        }

        var baseStage = slotIndex switch
        {
            0 => 1 + ((date.DayOfYear + 1) % unlockedCap),
            1 => Mathf.Clamp((unlockedCap + 1) / 2 + ((date.DayOfYear * 2) % Math.Max(1, unlockedCap / 2 + 1)), 1, unlockedCap),
            _ => unlockedCap - ((date.DayOfYear + slotIndex) % Math.Min(3, unlockedCap))
        };

        var candidate = Mathf.Clamp(baseStage, 1, unlockedCap);
        for (var attempt = 0; attempt < unlockedCap; attempt++)
        {
            if (usedStages.Add(candidate))
            {
                return candidate;
            }

            candidate++;
            if (candidate > unlockedCap)
            {
                candidate = 1;
            }
        }

        usedStages.Add(candidate);
        return candidate;
    }

    private static int ResolveSeedForSlot(int slotIndex, DateTime date)
    {
        var rawSeed = 1000 + ((date.Year * 137) + (date.DayOfYear * 29) + (slotIndex * 911)) % 9000;
        return Mathf.Clamp(rawSeed, 1000, 9999);
    }

    private static IReadOnlyList<string> ResolveLockedDeckUnitIds(int stage, int slotIndex, int seed)
    {
        var availableUnits = GameData.GetPlayerUnits()
            .Where(unit => unit.UnlockStage <= Math.Max(1, stage))
            .ToArray();

        if (availableUnits.Length == 0)
        {
            return new[]
            {
                GameData.PlayerBrawlerId,
                GameData.PlayerShooterId,
                GameData.PlayerDefenderId
            };
        }

        var desiredTags = slotIndex switch
        {
            0 => new[]
            {
                SquadSynergyCatalog.FrontlineTag,
                SquadSynergyCatalog.SupportTag,
                SquadSynergyCatalog.ReconTag
            },
            1 => new[]
            {
                SquadSynergyCatalog.FrontlineTag,
                SquadSynergyCatalog.BreachTag,
                SquadSynergyCatalog.SupportTag
            },
            3 => new[]
            {
                SquadSynergyCatalog.ReconTag,
                SquadSynergyCatalog.FrontlineTag,
                SquadSynergyCatalog.BreachTag
            },
            4 => new[]
            {
                SquadSynergyCatalog.BreachTag,
                SquadSynergyCatalog.FrontlineTag,
                SquadSynergyCatalog.SupportTag
            },
            _ => new[]
            {
                SquadSynergyCatalog.BreachTag,
                SquadSynergyCatalog.SupportTag,
                SquadSynergyCatalog.ReconTag
            }
        };

        var result = new List<string>(3);
        for (var i = 0; i < desiredTags.Length; i++)
        {
            var matchingUnits = availableUnits
                .Where(unit =>
                    SquadSynergyCatalog.NormalizeTag(unit.SquadTag)
                        .Equals(desiredTags[i], StringComparison.OrdinalIgnoreCase) &&
                    !result.Contains(unit.Id, StringComparer.OrdinalIgnoreCase))
                .OrderBy(unit => unit.UnlockStage)
                .ThenBy(unit => unit.DisplayName)
                .ToArray();

            if (matchingUnits.Length == 0)
            {
                continue;
            }

            result.Add(matchingUnits[(seed + i + slotIndex) % matchingUnits.Length].Id);
        }

        var fallbackUnits = availableUnits
            .OrderBy(unit => unit.UnlockStage)
            .ThenBy(unit => unit.DisplayName)
            .ToArray();
        var fallbackIndex = 0;
        while (result.Count < 3 && fallbackUnits.Length > 0)
        {
            var candidate = fallbackUnits[(seed + fallbackIndex) % fallbackUnits.Length];
            fallbackIndex++;
            if (result.Contains(candidate.Id, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            result.Add(candidate.Id);
        }

        while (result.Count < 3)
        {
            result.Add(GameData.PlayerBrawlerId);
        }

        return result.ToArray();
    }
}
