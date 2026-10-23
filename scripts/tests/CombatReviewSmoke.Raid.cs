using System.Linq;
using System.Threading.Tasks;
using Godot;

// The weekly raid: every battle wears this week's boss down, a new week starts it over, and the pools fit a week of play.
public partial class CombatReviewSmoke
{
    private async Task CheckWeeklyRaid()
    {
        var state = GameState.Instance;
        var checkpoint = (GameSaveData)Invoke(state, "BuildSaveData");
        state.ResetProgress();
        state.SetAnalyticsConsent(false);
        state.SetShowHints(false);
        var week = RaidBossCatalog.GetCurrentWeekId();
        Check(state.RaidDamageContributed == 0, "A new caravan has dealt no raid damage");

        var battle = await OpenBattle(10);
        var director = Read<BattleSpawnDirector>(battle, "_spawnDirector");
        director.TryBuildEnemyStats(GameData.EnemyWalkerId, out var stats);
        var enemy = (Unit)Invoke(battle, "SpawnUnit", Team.Enemy, stats, new Vector2(560, 300));
        var health = enemy.MaxHealth;
        enemy.TakeDamage(health * 10f, damageType: CombatDamageType.True);
        Invoke(battle, "CleanupDeadUnits");
        Write(battle, "_enemyBaseHealth", Read<float>(battle, "_enemyBaseMaxHealth") - 40f);
        var won = Mathf.RoundToInt(health + 40f);
        Invoke(battle, "EndBattle", true);
        Check(state.RaidDamageContributed == won && state.LastRaidWeek == week,
            $"A won battle adds the health of the enemies it defeated and its gate damage to the raid ({state.RaidDamageContributed}, expected {won})");
        await CloseBattle(battle);

        battle = await OpenBattle(10);
        Write(battle, "_enemyBaseHealth", Read<float>(battle, "_enemyBaseMaxHealth") - 25f);
        Invoke(battle, "EndBattle", false);
        Check(state.RaidDamageContributed == won + 25, "A lost battle still counts the damage it dealt");
        await CloseBattle(battle);

        var boss = RaidBossCatalog.GetCurrentBoss();
        var first = boss.Milestones[0];
        Check(!state.TryClaimRaidReward(week, 0, out _) && !state.HasClaimedRaidReward(week, 0), "A milestone waits for its damage");
        state.ContributeRaidDamage(first.DamageThreshold);
        var gold = state.Gold;
        Check(state.TryClaimRaidReward(week, 0, out _) && state.HasClaimedRaidReward(week, 0) && !state.TryClaimRaidReward(week, 0, out _)
              && (first.RewardType != "gold" || state.Gold == gold + first.RewardAmount), "A reached milestone pays once");
        var dealt = state.RaidDamageContributed;
        state.ReloadFromDisk();
        Check(state.RaidDamageContributed == dealt && state.HasClaimedRaidReward(week, 0), "Raid damage and claims survive a reload");
        Check(!state.IsAchievementUnlocked("raid_contributor"), "Many battles in one week count as one raid toward Raid Striker");

        var stale = state.BuildSaveData();
        stale.LastRaidWeek = "2020-W01";
        stale.RaidDamageContributed = 999_999;
        stale.RaidContributionCount = 2;
        Invoke(state, "ApplySavedData", stale);
        Check(state.RaidDamageContributed == 0 && !state.TryClaimRaidReward(week, 1, out _), "Last week's damage does not count against this week's boss");
        state.ContributeRaidDamage(10);
        Check(state.RaidDamageContributed == 10 && state.LastRaidWeek == week && state.IsAchievementUnlocked("raid_contributor"),
            "A new week starts the raid over, and a third raid week earns Raid Striker");

        // Tactical review battles deal about 1,000 raid damage mid-campaign (COMBAT_SAMPLE raidDamage: ~250 at stage 1,
        // ~2,000 at stage 100), so a pool is a week of play and the first milestone a day's handful of battles.
        const int midCampaignBattle = 1_000;
        foreach (var raid in RaidBossCatalog.GetAll())
        {
            var battles = raid.TotalHealthPool / midCampaignBattle;
            var steps = raid.Milestones.Select(m => m.DamageThreshold).ToArray();
            Check(battles is >= 20 and <= 50 && steps[0] <= 10 * midCampaignBattle && steps[^1] == raid.TotalHealthPool
                  && steps.Zip(steps.Skip(1)).All(pair => pair.First < pair.Second),
                $"{raid.BossName}: the kill takes about {battles} mid-campaign battles and the milestones climb to it");
        }
        Invoke(state, "ApplySavedData", checkpoint);
    }
}
