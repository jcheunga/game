using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Godot;

// Every achievement the panel lists can be earned in this build, and its count matches what the game has.
public partial class CombatReviewSmoke
{
    private void CheckAchievementReach()
    {
        // Each listed achievement must have code that unlocks it. Hard mode's two were listed with no way to play it,
        // and Bounty Hunter had nothing unlocking it.
        var source = new List<string>();
        void Collect(string dir)
        {
            using var folder = DirAccess.Open(dir);
            if (folder == null) return;
            foreach (var name in folder.GetFiles()) if (name.EndsWith(".cs")) source.Add(FileAccess.GetFileAsString($"{dir}/{name}"));
            foreach (var sub in folder.GetDirectories()) if (sub != "tests") Collect($"{dir}/{sub}");
        }
        Collect("res://scripts");
        var code = string.Join("\n", source);
        var unreached = AchievementCatalog.GetAll().Select(a => a.Id)
            .Where(id => !Regex.IsMatch(code, $"TryUnlockAchievement\\(\\s*\"{Regex.Escape(id)}\"")).ToArray();
        Check(source.Count > 0 && unreached.Length == 0, $"Every listed achievement has code that unlocks it{(unreached.Length > 0 ? $" (none for {string.Join(", ", unreached)})" : "")}");

        var troops = GameState.RecruitableTroops().ToArray();
        Check(troops.Length == 32 && troops.All(unit => !unit.LegacyRecruit && unit.UnlockStage <= GameData.MaxStage),
            $"Full Roster counts the {troops.Length} troops a player can recruit, all within the campaign");
        Check(GameData.PlayerSpellIds.All(id => GameData.GetSpell(id).UnlockStage <= GameData.MaxStage),
            "Every spell and tactical tool Arcane Scholar counts unlocks within the campaign");

        var state = GameState.Instance;
        state.ResetProgress();
        var claims = (HashSet<string>)typeof(GameState).GetField("_completedBountyIds", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(state)!;
        foreach (var day in Enumerable.Range(1, 6)) { claims.Add($"2026-11-{day:00}:bounty_a"); claims.Add($"2026-11-{day:00}:bounty_b"); }
        state.CheckAchievements();
        Check(state.BountyDaysCompleted == 6 && !state.IsAchievementUnlocked("bounty_streak_7"), "Six days of bounties, two claims on each, are six days");
        claims.Add("2026-11-07:bounty_a");
        state.CheckAchievements();
        Check(state.IsAchievementUnlocked("bounty_streak_7"), "Bounties claimed on a seventh day unlock Bounty Hunter");
        state.ResetProgress();
    }
}
