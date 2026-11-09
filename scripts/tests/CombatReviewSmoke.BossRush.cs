using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class CombatReviewSmoke
{
    /// <summary>
    /// --bossrush-gameplay [--rush-start=n] [--seed-offset=n] [--squad=stage:level] [--deck=a,b,...] [--trace]: a whole
    /// boss rush played at fixed 60 Hz steps by the campaign's scripted player (SimDrive: collects courage packs, keeps two troops in front, aims spells, fires
    /// the caravan spell) with the endgame trained squad the tower and events check against (SeedTowerSquad at stage
    /// 100: every troop at level 20, wagon tracks at their rank cap, the spell at level 3, the milestone relics). Until
    /// November 2026 it used a loop that never collected courage or fired the caravan spell, with wagon rank 3 and
    /// untrained spells, and lost at commander 12 of 14. Each commander's fall prints BOSS_RUSH_ROUND; --rush-start skips
    /// to a later commander; --trace prints the field every five seconds and each round's opening state. --squad and
    /// --deck play as a weaker player instead (the squad on reaching that stage, the troops it fields) and only measure
    /// how far it gets; they fitted the per-commander reinforcements (BossRushCatalog.ReinforcementBudget).
    ///
    /// The final commander is meant to stay a real challenge (decided November 2026): a full run must clear the first
    /// thirteen and reach the Ashen Regent, but may lose to it (it fell in 3 of 11 full runs before the reinforcements,
    /// 1 of 3 after). A run started at a later commander must clear every remaining one; the Ashen Regent fresh fell in
    /// 14 of 14 runs.
    /// </summary>
    private async Task ReviewBossRushGameplay()
    {
        var args = OS.GetCmdlineUserArgs();
        // --squad=stage:level plays with the squad a player has on reaching that stage instead of the maxed one.
        var squad = args.FirstOrDefault(a => a.StartsWith("--squad="))?.Split('=')[1].Split(':').Select(int.Parse).ToArray();
        SeedTowerSquad(squad?[0] ?? GameData.MaxStage, squad?.ElementAtOrDefault(1) ?? 20);
        var state = GameState.Instance;
        // --deck=a,b,... swaps the squad's troops for these (ids without the player_ prefix), such as the deck the
        // playthrough sim actually fielded at that stage.
        var deck = args.FirstOrDefault(a => a.StartsWith("--deck="))?.Split('=')[1].Split(',').Select(id => "player_" + id).ToArray();
        if (deck != null)
        {
            var current = state.ActiveDeckUnitIds.ToArray();
            foreach (var (drop, add) in current.Except(deck).Zip(deck.Except(current)))
            {
                state.ToggleDeckUnit(drop, out _);
                Check(state.ToggleDeckUnit(add, out var why), $"The boss-rush squad takes {add} ({why})");
            }
            GD.Print($"BOSS_RUSH_DECK: {string.Join(", ", state.ActiveDeckUnitIds)}");
        }
        state.SetShowHints(false);
        state.SetAnalyticsConsent(false);
        state.PrepareBossRush(out _);
        var battle = GD.Load<PackedScene>("res://scenes/Battle.tscn").Instantiate<BattleController>();
        AddChild(battle);
        battle.SetPhysicsProcess(false);
        var seedArg = args.FirstOrDefault(a => a.StartsWith("--seed-offset="));
        var seed = 4100UL + (seedArg == null ? 0UL : ulong.Parse(seedArg.Split('=')[1]));
        Read<RandomNumberGenerator>(battle, "_rng").Seed = seed;
        Invoke(battle, "InitializeContent", seed);
        Invoke(battle, "InitializeCourageDrops", seed);
        await LiveUiReview.StopAudio(this);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var startArg = args.FirstOrDefault(a => a.StartsWith("--rush-start="));
        var startRound = startArg == null ? 0 : Math.Clamp(int.Parse(startArg.Split('=')[1]), 0, BossRushCatalog.TotalWaves - 1);
        if (startRound > 0) Write(battle, "_rushCleared", startRound);
        var trace = args.Contains("--trace");
        var units = Read<List<Unit>>(battle, "_units");
        var battleDeck = Read<BattleDeckState>(battle, "_deck");
        IEnumerable<Unit> Alive(Team team) => units.Where(u => u.Team == team && !u.IsDead);
        string Stats(Unit u) => $"{u.DefinitionId}:{u.MaxHealth:0}hp/{u.AttackDamage:0.0}dmg/{u.AttackCooldown:0.00}s/{u.Speed:0}";
        var described = -1;
        // Played in five-second spans so each commander's fall is logged as it happens.
        for (var round = startRound; !Read<bool>(battle, "_battleEnded") && Read<float>(battle, "_elapsed") < 2400f;)
        {
            await SimDrive(battle, Read<float>(battle, "_elapsed") + 5f);
            var current = Read<int>(battle, "_rushCleared");
            if (trace)
            {
                var boss = Alive(Team.Enemy).FirstOrDefault(u => u.Definition.IsBoss);
                GD.Print($"BOSS_RUSH_TRACE: t={Read<float>(battle, "_elapsed"):0} round={current + 1} hull={Read<float>(battle, "_playerBaseHealth"):0} " +
                    $"boss={(boss == null ? "-" : $"{boss.Health:0}/{boss.MaxHealth:0} x{boss.Position.X:0}")} foes={Alive(Team.Enemy).Count()} " +
                    $"troops={Alive(Team.Player).Count()} courage={Read<float>(battle, "_courage"):0} mana={Read<float>(battle, "_mana"):0}");
                if (boss != null && described != current)
                {
                    described = current;
                    GD.Print($"BOSS_RUSH_STATE: round={current + 1} courage/s={Read<float>(battle, "_courageGainPerSecond"):0.00} " +
                        $"cooldowns=[{string.Join(" ", battleDeck.Roster.Select(u => $"{u.Id}:{battleDeck.GetCooldownRemaining(u.Id):0.0}"))}] " +
                        $"troops=[{string.Join(" ", Alive(Team.Player).Select(Stats))}] foes=[{string.Join(" ", Alive(Team.Enemy).Select(Stats))}]");
                }
            }
            if (current == round) continue;
            GD.Print($"BOSS_RUSH_ROUND: {round + 1}/{BossRushCatalog.TotalWaves} {BossRushCatalog.Waves[round].BossName} cleared at {Read<float>(battle, "_elapsed"):0}s");
            round = current;
        }
        var cleared = Read<int>(battle, "_rushCleared");
        var ended = Read<bool>(battle, "_battleEnded");
        GD.Print($"BOSS_RUSH_SAMPLE: rounds={cleared},seconds={Read<float>(battle, "_elapsed"):0.0}," +
            $"hull={Read<float>(battle, "_playerBaseHealth"):0.0},ended={ended}");
        if (startRound == 0 || squad != null || deck != null)
            GD.Print(cleared == BossRushCatalog.TotalWaves ? $"BOSS_RUSH_FINAL: all {cleared} commanders fell"
                : $"BOSS_RUSH_FINAL: {BossRushCatalog.Waves[cleared].BossName} (commander {cleared + 1}) held");
        // A chosen squad or deck is a measurement (how far that player gets), not a pass/fail check.
        if (squad == null && deck == null)
            Check(ended && cleared >= BossRushCatalog.TotalWaves - (startRound == 0 ? 1 : 0), startRound == 0
                ? $"An endgame squad clears the first {BossRushCatalog.TotalWaves - 1} boss-rush commanders and reaches the final one in a full run"
                : "An endgame squad can clear the selected late boss-rush encounters");
        await CloseBattle(battle);
    }
}
