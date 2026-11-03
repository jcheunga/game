using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

// Seasonal events: they return every year, each year's run keeps its own progress, and their battles play with
// the modifiers the event screen lists.
public partial class CombatReviewSmoke
{
    private async Task CheckEvents()
    {
        var siege = SeasonalEventCatalog.GetById("midwinter_siege");
        var harvest = SeasonalEventCatalog.GetById("harvest_moon");
        DateTime Day(int year, int month, int day) => new(year, month, day, 12, 0, 0, DateTimeKind.Utc);
        Check(siege.IsActive(Day(2026, 12, 15)) && siege.IsActive(Day(2027, 1, 5)) && !siege.IsActive(Day(2026, 12, 14)) && !siege.IsActive(Day(2027, 1, 6)),
            "Midwinter Siege runs 15 December to 5 January");
        Check(siege.EditionKey(Day(2027, 1, 3)) == "midwinter_siege@2026" && siege.EditionKey(Day(2027, 12, 20)) == "midwinter_siege@2027",
            "A run that crosses the new year keeps the year it opened in, and next winter is a new run");
        Check(harvest.IsActive(Day(2027, 9, 20)) && harvest.IsActive(Day(2027, 10, 10)) && !harvest.IsActive(Day(2027, 9, 19)) && !harvest.IsActive(Day(2027, 10, 11))
              && harvest.EditionKey(Day(2028, 10, 1)) == "harvest_moon@2028", "Harvest Moon runs 20 September to 10 October, every year");
        Check(Enumerable.Range(0, 366).Select(d => Day(2027, 1, 1).AddDays(d)).All(day => SeasonalEventCatalog.GetAll().Count(e => e.IsActive(day)) <= 1),
            "No two events run at once");
        var (next, opens) = SeasonalEventCatalog.GetNextEvent(Day(2026, 11, 4));
        var (after, opensAfter) = SeasonalEventCatalog.GetNextEvent(Day(2027, 1, 10));
        Check(next == siege && opens == new DateTime(2026, 12, 15) && after == harvest && opensAfter == new DateTime(2027, 9, 20),
            "Between events the hub names the day the next one opens");

        // Each year's run keeps its own clears and claims; last year's clears neither count nor lock this year's run.
        var state = GameState.Instance;
        state.ResetProgress();
        var edition = siege.EditionKey(DateTime.UtcNow);
        var cleared = Read<Dictionary<string, int>>(state, "_eventStagesCleared");
        cleared["midwinter_siege@1999"] = 5;
        cleared["midwinter_siege"] = 5;
        Check(state.GetEventProgress(siege.Id) == 0 && !state.HasClaimedEventReward(siege.Id, 0), "Another year's run, or a save from before yearly events, starts this run afresh");
        state.RecordEventStageCleared(siege.Id);
        state.RecordEventStageCleared(siege.Id);
        Check(state.GetEventProgress(siege.Id) == 2 && cleared[edition] == 2 && state.TryClaimEventReward(siege.Id, 0, out _) && state.HasClaimedEventReward(siege.Id, 0),
            $"Clears and claims this year go under {edition}");
        Check(state.IsAchievementUnlocked("event_complete"), "Clearing a whole run in any year earns Festival Champion");
        state.ResetProgress();

        // The battle plays with the event stage's modifiers, on a copy of its campaign stage.
        var stage = siege.Stages[2];
        var battle = await OpenEventBattle(siege.Id, 2, 9401);
        var data = Read<StageDefinition>(battle, "_stageData");
        Check(stage.Modifiers.Length > 0 && stage.Modifiers.All(m => m.Value > 0f && data.Modifiers.Any(d => d.Type == m.Type && d.Value == m.Value))
              && GameData.GetStage(stage.BaseStageNumber).Modifiers.Length == 0 && Read<float>(battle, "_eventEnemyHealthScale") == stage.EnemyHealthScale,
            "An event battle plays with its stage's modifiers and strength, and the campaign stage keeps its own");
        await CloseBattle(battle);

        // Every event stage stays beatable for a player who just cleared its campaign stage, on the fit's seeds.
        var unbeaten = new List<string>();
        foreach (var evt in SeasonalEventCatalog.GetAll())
            for (var index = 0; index < evt.Stages.Length; index++)
                for (var seed = 0; seed < 2; seed++)
                {
                    SeedTowerSquad(evt.Stages[index].BaseStageNumber);
                    if ((await PlayOpened(await OpenEventBattle(evt.Id, index, (ulong)(9000 + evt.Stages[index].BaseStageNumber * 10 + seed)))).Won) continue;
                    unbeaten.Add($"{evt.Title} {index + 1}");
                    break;
                }
        Check(unbeaten.Count == 0, $"A player who just cleared each campaign stage beats every event stage with its modifiers{(unbeaten.Count > 0 ? $" (not {string.Join(", ", unbeaten)})" : "")}");
        state.ResetProgress();
    }
}
