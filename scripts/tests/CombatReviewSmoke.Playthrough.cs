using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;

// --playthrough-sim: one player's whole campaign, stage 1 to 100, from a new save.
//
// Every battle is the real BattleController stepped at fixed 60 Hz, played by a scripted player, and pays through the
// game's own victory path (gold, food, XP, milestones, relics, achievements). Between battles the player spends gold
// the way a focused player would, opens the atlas's supply tiles, and lives within the food supply: battles cost
// rations, rations recharge on a clock, and a day holds a limited amount of play. A stage the player cannot win sends
// them back to replay the last stage they beat (the grind) until training or a raised level cap lets them try again.
// The spender profile buys gold packs instead of grinding for gold, and food packs instead of waiting.
public partial class CombatReviewSmoke
{
    private sealed class PlaythroughSim
    {
        public string Profile = "free";
        public double Clock, DayPlay, PlaySeconds, IdleSeconds, FoodAnchor;
        public int Day, Battles, Wins, Losses, Replays, FirstClears, FoodBreaks, RefillsToday, RefillsBought;
        public int GoldFromBattles, GoldSpentUnits, GoldSpentRecruits, GoldSpentSpells, GoldSpentWagon, GoldSpentFood, GoldBought, FoodBought;
        public float Dollars;
        public readonly List<string> Purchases = new();
        public readonly List<object> BattleRows = new();
        public readonly List<object> StageRows = new();
        public double DailySeconds = 3600, BattleOverhead = 40, Budget = 1000;
        public int MaxGrindPerStage = 300;
        // 0 carries the usual squad; above that, the alternative lineup a stuck player is trying (SimLineup).
        public int Lineup, LineupSwaps;
    }

    private static readonly string[] SimWagonPriority =
    {
        BaseUpgradeCatalog.HullPlatingId, BaseUpgradeCatalog.PantryId, BaseUpgradeCatalog.DispatchConsoleId,
        BaseUpgradeCatalog.ArcherCrewId, BaseUpgradeCatalog.ReinforcedArmorId, BaseUpgradeCatalog.BallistaId,
        BaseUpgradeCatalog.ArrowVolleyId, BaseUpgradeCatalog.EmergencyRepairId
    };

    // Area damage spells a player swaps in as they unlock; the latest owned one is carried.
    private static readonly string[] SimSpellPlan =
        { "spell_fireball", "spell_lightning_strike", "spell_meteor", "spell_chain_lightning" };

    private static readonly HashSet<string> SimAllySpells = new()
    {
        "heal", "barrier_ward", "purifying_light", "rallying_advance", "mirror_ward", "aegis_bolt", "gale_step",
        "verdant_renewal", "war_cry", "consecrated_ground", "tactic_healing_shrine"
    };

    private static void SimSet(object owner, string property, object value) =>
        owner.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(owner, value);

    private static string SimArg(string name, string fallback) =>
        OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith($"--{name}="))?.Split('=', 2)[1] ?? fallback;

    /// <summary>
    /// --playthrough-sim [--profile=free|spender] [--to-stage=100] [--daily-minutes=60] [--budget=1000]
    /// [--max-grind=300] [--out=name] [--expect-replays=a-b] [--expect-battles=a-b] [--expect-dollars=a-b]
    /// [--expect-days=a-b] [--strength-file=path]: prints PLAYTHROUGH_BATTLE, PLAYTHROUGH_STAGE and PLAYTHROUGH_SUMMARY
    /// lines, writes artifacts/playthrough/&lt;out&gt;.json for scripts/analysis/playthrough_report.py, and checks that
    /// the campaign was finished and that each given total lies in its band. --strength-file plays a candidate
    /// CampaignPacing table instead of the built one (100 numbers, as campaign_fit.py prints them), so one build can
    /// compare tables side by side.
    /// </summary>
    private async Task SimulatePlaythrough()
    {
        var strengthFile = SimArg("strength-file", null);
        if (strengthFile != null)
        {
            var table = System.Text.RegularExpressions.Regex.Matches(System.IO.File.ReadAllText(strengthFile), @"-?\d+\.\d+")
                .Select(m => float.Parse(m.Value, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            Check(table.Length == GameData.MaxStage, $"The strength file has a strength for each of the {GameData.MaxStage} stages ({table.Length})");
            CampaignPacing.StrengthOverride = n => table[Math.Clamp(n, 1, table.Length) - 1];
        }
        var sim = new PlaythroughSim
        {
            Profile = SimArg("profile", "free"),
            DailySeconds = double.Parse(SimArg("daily-minutes", "60"), System.Globalization.CultureInfo.InvariantCulture) * 60,
            Budget = double.Parse(SimArg("budget", "1000"), System.Globalization.CultureInfo.InvariantCulture),
            MaxGrindPerStage = int.Parse(SimArg("max-grind", "300"))
        };
        var target = Math.Min(GameData.MaxStage, int.Parse(SimArg("to-stage", GameData.MaxStage.ToString())));
        var state = GameState.Instance;
        state.ResetProgress();
        state.SetAnalyticsConsent(false);
        state.SetShowHints(false);
        // The sim keeps its own clock; the real-time food recharge would otherwise top rations up while it runs.
        state.SetProcess(false);
        sim.FoodAnchor = 0;
        var cleared = 0;
        var wall = 0;
        for (var stage = 1; stage <= target; stage++)
        {
            var (attempts, losses, grind) = (0, 0, 0);
            var startPlay = sim.PlaySeconds;
            var startDay = sim.Day;
            var won = false;
            while (!won)
            {
                SimShop(sim, stage);
                attempts++;
                won = await SimBattle(sim, stage, losses == 0 ? "first" : "retry", (ulong)(stage * 1000 + attempts));
                if (won) break;
                losses++;
                // A loss pays no gold, but its XP can lift a cap. Two quick retries the first time; after that, a
                // grind before every further try.
                SimShop(sim, stage);
                if (losses < 3) continue;
                if (grind >= sim.MaxGrindPerStage) break;
                // With troops at the last level and nothing left to buy, grinding can't make this squad any
                // stronger, so a stuck player tries another lineup (recruiting and training what it needs) instead of
                // replaying the same squad until it gets lucky.
                if (state.UnitLevelCap >= state.MaxUnitLevel && !SimUpgradeOptions(state).Any()) SimNextLineup(sim, stage);
                grind += await SimGrind(sim, stage);
            }
            if (!won)
            {
                wall = stage;
                GD.Print($"PLAYTHROUGH_WALL: {{\"stage\":{stage},\"attempts\":{attempts},\"grind\":{grind},\"level\":{state.PlayerLevel},\"unitCap\":{state.UnitLevelCap}}}");
                break;
            }
            cleared = stage;
            sim.FirstClears++;
            var lineup = sim.Lineup;
            sim.Lineup = 0;
            SimOpenAtlas(sim, stage);
            var row = new
            {
                stage, attempts, losses, grind, lineup, day = sim.Day, dayStart = startDay,
                playMinutes = Math.Round((sim.PlaySeconds - startPlay) / 60, 1),
                totalHours = Math.Round(sim.PlaySeconds / 3600, 2),
                level = state.PlayerLevel, unitCap = state.UnitLevelCap, wagonCap = state.WagonRankCap,
                deck = state.ActiveDeckUnitIds.Select(id => $"{id.Replace("player_", "")}:{state.GetUnitLevel(id)}").ToArray(),
                spells = state.ActiveDeckSpellIds.Select(id => $"{id.Replace("spell_", "")}:{state.GetSpellLevel(id)}").ToArray(),
                wagon = SimWagonPriority.Sum(state.GetBaseUpgradeLevel),
                gold = state.Gold, dollars = Math.Round(sim.Dollars, 2)
            };
            sim.StageRows.Add(row);
            GD.Print($"PLAYTHROUGH_STAGE: {JsonSerializer.Serialize(row)}");
        }
        var summary = new
        {
            profile = sim.Profile, cleared, wall, target, battles = sim.Battles, wins = sim.Wins, losses = sim.Losses,
            replays = sim.Replays, lineupSwaps = sim.LineupSwaps, days = sim.Day + 1, playHours = Math.Round(sim.PlaySeconds / 3600, 2),
            idleHours = Math.Round(sim.IdleSeconds / 3600, 2), foodBreaks = sim.FoodBreaks, refills = sim.RefillsBought,
            goldFromBattles = sim.GoldFromBattles, goldBought = sim.GoldBought, foodBought = sim.FoodBought,
            spent = new { units = sim.GoldSpentUnits, recruits = sim.GoldSpentRecruits, spells = sim.GoldSpentSpells, wagon = sim.GoldSpentWagon, food = sim.GoldSpentFood },
            dollars = Math.Round(sim.Dollars, 2), purchases = sim.Purchases,
            level = state.PlayerLevel, gold = state.Gold, dailyMinutes = sim.DailySeconds / 60
        };
        GD.Print($"PLAYTHROUGH_SUMMARY: {JsonSerializer.Serialize(summary)}");
        // Pacing guards (scripts/tests/playthrough_check.sh passes the bands): the campaign can be finished, and the
        // grind and the money it takes stay inside what was designed.
        Check(wall == 0 && cleared == target, $"The {sim.Profile} player clears stages 1-{target}{(wall > 0 ? $" (stopped at {wall})" : "")}");
        static (double Low, double High) Band(string text) =>
            (double.Parse(text.Split('-')[0], System.Globalization.CultureInfo.InvariantCulture),
             double.Parse(text.Split('-')[1], System.Globalization.CultureInfo.InvariantCulture));
        foreach (var (flag, label, value) in new (string, string, double)[]
                 {
                     ("expect-replays", "replays", sim.Replays), ("expect-battles", "battles", sim.Battles),
                     ("expect-dollars", "dollars", sim.Dollars), ("expect-days", "days", sim.Day + 1)
                 })
            if (SimArg(flag, null) is { } band)
            {
                var (low, high) = Band(band);
                Check(value >= low && value <= high, $"The {sim.Profile} player's {label} ({value:0.##}) stay within {band}");
            }
        var path = ProjectSettings.GlobalizePath($"res://artifacts/playthrough/{SimArg("out", sim.Profile)}.json");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path, JsonSerializer.Serialize(new { summary, stages = sim.StageRows, battles = sim.BattleRows },
            new JsonSerializerOptions { WriteIndented = true }));
        GD.Print($"PLAYTHROUGH_FILE: {path}");
        state.SetProcess(true);
    }

    /// <summary>
    /// Replays the latest beaten stage that the squad still wins until the replays have bought a real step of
    /// training: gold spent worth the five cheapest upgrades the caps allow (about a level for the whole squad), or a
    /// raised level cap when nothing under the caps is left. At least three replays, at most fifteen. Returns the
    /// replays played. A spender buys gold first.
    /// </summary>
    private async Task<int> SimGrind(PlaythroughSim sim, int stage)
    {
        var state = GameState.Instance;
        var capBefore = (state.UnitLevelCap, state.WagonRankCap);
        if (sim.Profile == "spender" && SimBuyGold(sim)) { SimShop(sim, stage); return 0; }
        var step = SimUpgradeOptions(state).Select(o => o.Cost).OrderBy(c => c).Take(5).Sum();
        var spentAtStart = SimSpent(sim);
        var replays = 0;
        var farm = stage - 1;
        var farmLosses = 0;
        bool Done() => step > 0 ? SimSpent(sim) - spentAtStart >= step : (state.UnitLevelCap, state.WagonRankCap) != capBefore;
        while (farm >= 1 && replays < 15 && !(replays >= 3 && Done()))
        {
            var won = await SimBattle(sim, farm, "grind", (ulong)(stage * 1000 + 500 + sim.Battles));
            replays++;
            sim.Replays++;
            if (!won && ++farmLosses >= 2) { farm--; farmLosses = 0; }
            SimShop(sim, stage);
        }
        return replays;
    }

    private static int SimSpent(PlaythroughSim sim) =>
        sim.GoldSpentUnits + sim.GoldSpentRecruits + sim.GoldSpentSpells + sim.GoldSpentWagon;

    /// <summary>
    /// The spender's answer to a wall: the smallest gold pack that covers every upgrade the current caps allow (or
    /// the largest pack), within the budget. False when nothing under the caps is left to buy.
    /// </summary>
    private bool SimBuyGold(PlaythroughSim sim)
    {
        var state = GameState.Instance;
        var wanted = SimUpgradeOptions(state).Sum(o => o.Cost) - state.Gold;
        if (wanted <= 0) return false;
        var packs = SimShopProducts("gold");
        var pack = packs.Any(p => p.Amount >= wanted) ? packs.First(p => p.Amount >= wanted) : packs.Last();
        if (sim.Dollars + pack.Price > sim.Budget) return false;
        sim.Dollars += pack.Price;
        sim.GoldBought += pack.Amount;
        sim.Purchases.Add($"{pack.Id}@{state.HighestUnlockedStage}");
        SimSet(state, "Gold", state.Gold + pack.Amount);
        return true;
    }

    private static List<(string Id, int Amount, float Price)> SimShopProducts(string category)
    {
        using var doc = JsonDocument.Parse(System.IO.File.ReadAllText(ProjectSettings.GlobalizePath("res://data/shop_products.json")));
        return doc.RootElement.EnumerateArray()
            .Where(p => p.GetProperty("category").GetString() == category && p.GetProperty("currencyType").GetString() == category)
            .Select(p => (p.GetProperty("id").GetString()!, p.GetProperty("currencyAmount").GetInt32() + p.GetProperty("bonusAmount").GetInt32(),
                p.GetProperty("priceUsd").GetSingle()))
            .OrderBy(p => p.Item3).ToList();
    }

    /// <summary>Upgrades the current caps allow for the carried squad, its spell and the wagon's main tracks.</summary>
    private static List<(string Kind, string Id, int Cost)> SimUpgradeOptions(GameState state)
    {
        var options = new List<(string, string, int)>();
        foreach (var id in state.ActiveDeckUnitIds)
            if (state.GetUnitLevel(id) < Math.Min(state.UnitLevelCap, state.MaxUnitLevel)) options.Add(("unit", id, state.GetUnitUpgradeCost(id)));
        foreach (var id in state.ActiveDeckSpellIds)
            if (state.GetSpellLevel(id) < 3) options.Add(("spell", id, state.GetSpellUpgradeCost(id)));
        foreach (var id in SimWagonPriority)
            if (state.GetBaseUpgradeLevel(id) < state.WagonRankCap) options.Add(("wagon", id, state.GetBaseUpgradeCost(id)));
        return options;
    }

    /// <summary>
    /// A focused player's visit to the armory: claim level rewards, recruit the newest troops for each role, carry the
    /// newest area spell, equip relics, then buy upgrades cheapest first (the wagon's weighted cheaper) until the gold runs out. Returns how
    /// many things were bought.
    /// </summary>
    private int SimShop(PlaythroughSim sim, int stage)
    {
        var state = GameState.Instance;
        state.ClaimAllLevelRewards();
        var bought = 0;
        // Recruit: the newest unlocked troop of each role if the squad would carry it.
        foreach (var unit in GameData.GetPlayerUnits().Where(u => u.UnlockStage <= stage && !state.IsUnitOwned(u.Id) && state.IsUnitAvailableForPurchase(u.Id))
                     .OrderByDescending(u => u.UnlockStage))
        {
            if (sim.Lineup == 0 && !SimWantsUnit(state, unit)) continue;
            if (sim.Lineup > 0 && !SimLineup(state, stage, sim.Lineup).Contains(unit.Id)) continue;
            var cost = state.GetUnitPurchaseCost(unit.Id);
            if (state.Gold < cost || !state.TryPurchaseUnit(unit.Id, out _)) continue;
            sim.GoldSpentRecruits += cost; bought++;
        }
        foreach (var id in SimSpellPlan)
        {
            var spell = GameData.GetSpell(id);
            if (spell.UnlockStage > stage || state.IsSpellOwned(id) || !state.IsSpellAvailableForPurchase(id)) continue;
            var cost = state.GetSpellPurchaseCost(id);
            if (state.Gold < cost || !state.TryPurchaseSpell(id, out _)) continue;
            sim.GoldSpentSpells += cost; bought++;
        }
        SimPickDeck(state, sim.Lineup > 0 ? SimLineup(state, stage, sim.Lineup) : null);
        SimEquipRelics(state);
        while (true)
        {
            // Cheapest first, with the wagon's main tracks (hull, stores, march drum) counted at half their price
            // and the other tracks at four fifths, so the wagon keeps pace with the troops instead of waiting for
            // every troop to reach its cap (troop levels are cheaper, and cheapest-first alone starves the wagon).
            var option = SimUpgradeOptions(state).Where(o => o.Cost <= state.Gold).OrderBy(o => o.Cost * SimUpgradeWeight(o.Kind, o.Id)).FirstOrDefault();
            if (option.Id == null) break;
            var ok = option.Kind switch
            {
                "unit" => state.TryUpgradeUnit(option.Id, out _),
                "spell" => state.TryUpgradeSpell(option.Id, out _),
                _ => state.TryUpgradeBase(option.Id, out _)
            };
            if (!ok) break;
            bought++;
            if (option.Kind == "unit") sim.GoldSpentUnits += option.Cost;
            else if (option.Kind == "spell") sim.GoldSpentSpells += option.Cost;
            else sim.GoldSpentWagon += option.Cost;
        }
        return bought;
    }

    private static float SimUpgradeWeight(string kind, string id) => kind != "wagon" ? 1f
        : id is BaseUpgradeCatalog.HullPlatingId or BaseUpgradeCatalog.PantryId or BaseUpgradeCatalog.DispatchConsoleId ? .5f : .8f;

    private static string SimRole(UnitDefinition unit) =>
        unit.SquadTag == SquadSynergyCatalog.SupportTag ? "support" : unit.UsesProjectile ? "ranged" : "melee";

    // The squad: two melee, two ranged and one support, each the newest owned troop of its role that is not far
    // behind in training (two unlock stages per level), plus the newest area spell.
    private static int SimScore(GameState state, UnitDefinition unit) => SimScore(unit, state.GetUnitLevel(unit.Id));
    private static int SimScore(UnitDefinition unit, int level) => unit.UnlockStage + 2 * level;

    private static bool SimWantsUnit(GameState state, UnitDefinition unit)
    {
        var role = SimRole(unit);
        var slots = role == "support" ? 1 : 2;
        var carried = state.ActiveDeckUnitIds.Select(GameData.GetUnit).Where(u => SimRole(u) == role).ToList();
        // A recruit joins at level 1 and is trained up from there: buy it only when it would take a place in the squad
        // straight away, so no gold goes on troops that sit on the bench.
        return carried.Count < slots || carried.Any(u => SimScore(state, u) < SimScore(unit, 1));
    }

    /// <summary>
    /// The alternative lineups a stuck player works through, from the three newest unlocked melee troops, three
    /// newest ranged and two newest supports (owned or not): two, two and one of each, newest first. Lineup n (from 1)
    /// is the n-th of these, wrapping round.
    /// </summary>
    private static string[] SimLineup(GameState state, int stage, int lineup)
    {
        string[] Newest(string role, int count) => GameData.GetPlayerUnits()
            .Where(u => SimRole(u) == role && u.UnlockStage <= stage && (state.IsUnitOwned(u.Id) || state.IsUnitAvailableForPurchase(u.Id)))
            .OrderByDescending(u => u.UnlockStage).ThenBy(u => u.Id, StringComparer.Ordinal).Take(count).Select(u => u.Id).ToArray();
        static IEnumerable<string[]> Pairs(string[] ids) =>
            ids.Length < 2 ? new[] { ids } : ids.SelectMany((a, i) => ids.Skip(i + 1).Select(b => new[] { a, b }));
        var supports = Newest("support", 2);
        var lineups = (from melee in Pairs(Newest("melee", 3))
                       from ranged in Pairs(Newest("ranged", 3))
                       from support in supports.Length == 0 ? new[] { Array.Empty<string>() } : supports.Select(s => new[] { s })
                       select melee.Concat(ranged).Concat(support).ToArray())
            .OrderByDescending(l => l.Sum(id => GameData.GetUnit(id).UnlockStage)).ThenBy(l => string.Join(",", l), StringComparer.Ordinal).ToArray();
        return lineups.Length == 0 ? Array.Empty<string>() : lineups[(lineup - 1) % lineups.Length];
    }

    /// <summary>Moves a stuck player on to the next lineup that differs from the squad that just lost.</summary>
    private void SimNextLineup(PlaythroughSim sim, int stage)
    {
        var state = GameState.Instance;
        var lost = state.ActiveDeckUnitIds.ToHashSet();
        for (var tries = 0; tries < 18; tries++)
        {
            sim.Lineup++;
            SimShop(sim, stage);
            if (!state.ActiveDeckUnitIds.ToHashSet().SetEquals(lost)) break;
        }
        sim.LineupSwaps++;
        GD.Print($"PLAYTHROUGH_LINEUP: {{\"stage\":{stage},\"lineup\":{sim.Lineup},\"deck\":\"{string.Join(" ", state.ActiveDeckUnitIds.Select(id => id.Replace("player_", "")))}\"}}");
    }

    private static void SimPickDeck(GameState state, string[] lineup = null)
    {
        var owned = GameData.GetPlayerUnits().Where(u => state.IsUnitOwned(u.Id)).ToList();
        // A stuck player's chosen lineup goes in first (the troops of it they own); the usual picks fill the rest.
        var deck = lineup?.Where(state.IsUnitOwned).ToList() ?? new List<string>();
        foreach (var (role, count) in new[] { ("melee", 2), ("ranged", 2), ("support", 1) })
            deck.AddRange(owned.Where(u => SimRole(u) == role && !deck.Contains(u.Id)).OrderByDescending(u => SimScore(state, u))
                .Take(count - deck.Count(id => SimRole(GameData.GetUnit(id)) == role)).Select(u => u.Id));
        foreach (var unit in owned.OrderByDescending(u => SimScore(state, u)))
            if (deck.Count < 5 && !deck.Contains(unit.Id)) deck.Add(unit.Id);
        var spell = SimSpellPlan.LastOrDefault(state.IsSpellOwned) ?? state.ActiveDeckSpellIds.FirstOrDefault();
        var units = Read<List<string>>(state, "_activeDeckUnitIds");
        var spells = Read<List<string>>(state, "_activeDeckSpellIds");
        units.Clear(); units.AddRange(deck);
        spells.Clear();
        if (spell != null) spells.Add(spell);
    }

    private static void SimEquipRelics(GameState state)
    {
        static int Rank(string rarity) => rarity switch { "legendary" => 4, "epic" => 3, "rare" => 2, _ => 1 };
        var equipped = new HashSet<string>(GameData.GetPlayerUnits().Select(u => state.GetUnitEquipment(u.Id)?.Id).Where(id => id != null)!);
        // Ordered by rarity then id: the owned set is a HashSet, and .NET randomizes string hashing per process, so
        // without the id the relics went to different troops on every run and fixed-seed battles stopped repeating.
        var free = state.GetOwnedEquipment().Where(id => !equipped.Contains(id)).Select(GameData.GetEquipment)
            .OrderByDescending(e => Rank(e.Rarity)).ThenBy(e => e.Id, StringComparer.Ordinal).ToList();
        foreach (var id in state.ActiveDeckUnitIds)
        {
            if (state.GetUnitEquipment(id) != null) continue;
            var relic = free.FirstOrDefault(e => state.TryEquipItem(id, e.Id));
            if (relic != null) free.Remove(relic);
        }
    }

    /// <summary>Opens the zone's supply tiles the caravan can reach, keeping enough food for the next battle.</summary>
    private void SimOpenAtlas(PlaythroughSim sim, int stage)
    {
        var state = GameState.Instance;
        var map = GameData.GetStage(stage).MapId;
        for (var pass = 0; pass < 12; pass++)
        {
            var opened = false;
            foreach (var tile in AdventureTileCatalog.ForMap(map))
            {
                if (!tile.IsResource || state.IsAdventureTileOpened(tile)) continue;
                if (state.Food < GameState.AdventureTileFoodCost + state.GetStageEntryFoodCost(Math.Min(stage + 1, GameData.MaxStage))) return;
                if (!state.CanTravelToAdventureTile(tile, out _)) continue;
                var gold = state.Gold;
                if (state.TryOpenAdventureTile(tile, out _)) { opened = true; sim.GoldFromBattles += state.Gold - gold; }
            }
            if (!opened) return;
        }
    }

    private void SimAdvance(PlaythroughSim sim, double seconds, bool playing)
    {
        sim.Clock += seconds;
        if (playing) { sim.DayPlay += seconds; sim.PlaySeconds += seconds; }
        else sim.IdleSeconds += seconds;
        // GameState.RefreshFoodRecharge on the sim's clock: +2 every five minutes while under the cap.
        var state = GameState.Instance;
        if (state.Food >= GameState.FoodRechargeCap) { sim.FoodAnchor = sim.Clock; return; }
        var ticks = (long)((sim.Clock - sim.FoodAnchor) / GameState.FoodRechargeSeconds);
        if (ticks <= 0) return;
        var food = (int)Math.Min(GameState.FoodRechargeCap, state.Food + ticks * GameState.FoodRechargeAmount);
        SimSet(state, "Food", food);
        sim.FoodAnchor = food >= GameState.FoodRechargeCap ? sim.Clock : sim.FoodAnchor + ticks * GameState.FoodRechargeSeconds;
    }

    private void SimNextDay(PlaythroughSim sim)
    {
        var next = (sim.Day + 1) * 86400.0;
        SimAdvance(sim, next - sim.Clock, false);
        sim.Day++;
        sim.DayPlay = 0;
        sim.RefillsToday = 0;
    }

    /// <summary>
    /// Makes sure the next battle's rations are there: a day's play time ends the day; a short supply is refilled
    /// with gold when the gold is spare, bought by a spender, or waited out (the player comes back when the
    /// recharge has filled the store).
    /// </summary>
    private void SimEnsureFood(PlaythroughSim sim, int cost)
    {
        var state = GameState.Instance;
        if (sim.DayPlay >= sim.DailySeconds) SimNextDay(sim);
        while (state.Food < cost)
        {
            if (sim.RefillsToday < GameState.FoodRefillsPerDay && state.Gold >= GameState.FoodRefillGold * 2)
            {
                SimSet(state, "Gold", state.Gold - GameState.FoodRefillGold);
                SimSet(state, "Food", state.Food + GameState.FoodRefillAmount);
                sim.GoldSpentFood += GameState.FoodRefillGold;
                sim.RefillsToday++; sim.RefillsBought++;
                continue;
            }
            if (sim.Profile == "spender")
            {
                var pack = SimShopProducts("food").First();
                if (sim.Dollars + pack.Price <= sim.Budget)
                {
                    sim.Dollars += pack.Price; sim.FoodBought += pack.Amount;
                    sim.Purchases.Add($"{pack.Id}@{state.HighestUnlockedStage}");
                    SimSet(state, "Food", state.Food + pack.Amount);
                    continue;
                }
            }
            sim.FoodBreaks++;
            // Come back when the store is full, or tomorrow if the day's play is spent.
            var full = (GameState.FoodRechargeCap - state.Food + 1) / GameState.FoodRechargeAmount * (double)GameState.FoodRechargeSeconds;
            if (sim.DayPlay + full >= sim.DailySeconds || (sim.Clock % 86400) + full >= 86400) SimNextDay(sim);
            else SimAdvance(sim, full, false);
        }
    }

    /// <summary>One campaign battle: rations spent, the battle played by the scripted player, the clock advanced.</summary>
    private async Task<bool> SimBattle(PlaythroughSim sim, int stage, string kind, ulong seed)
    {
        var state = GameState.Instance;
        var cost = state.GetStageEntryFoodCost(stage);
        SimEnsureFood(sim, cost);
        SimSet(state, "Food", state.Food - cost);
        var (gold, xp) = (state.Gold, state.PlayerXP);
        // Silence: with frames this far apart, the audio server spends most of the run mixing battle sounds.
        await LiveUiReview.StopAudio(this);
        state.SetSelectedStage(stage);
        state.PrepareCampaignBattle();
        var battle = GD.Load<PackedScene>("res://scenes/Battle.tscn").Instantiate<BattleController>();
        AddChild(battle);
        battle.SetPhysicsProcess(false);
        Read<RandomNumberGenerator>(battle, "_rng").Seed = seed;
        Invoke(battle, "InitializeContent", seed);
        Invoke(battle, "InitializeCourageDrops", seed);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var (won, seconds, hull, gate) = await SimPlay(battle);
        var earned = state.Gold - gold;
        sim.GoldFromBattles += Math.Max(0, earned);
        sim.Battles++;
        if (won) sim.Wins++; else sim.Losses++;
        SimAdvance(sim, seconds + sim.BattleOverhead, true);
        var row = new
        {
            n = sim.Battles, stage, kind, won, seconds = Math.Round(seconds, 1), hull = Math.Round(hull, 3), gate = Math.Round(gate, 3),
            gold = state.Gold, earned, xp = state.PlayerXP - xp, level = state.PlayerLevel, unitCap = state.UnitLevelCap,
            squad = Math.Round(state.ActiveDeckUnitIds.Average(id => state.GetUnitLevel(id)), 2),
            day = sim.Day, hours = Math.Round(sim.PlaySeconds / 3600, 2), food = state.Food, dollars = Math.Round(sim.Dollars, 2)
        };
        sim.BattleRows.Add(row);
        GD.Print($"PLAYTHROUGH_BATTLE: {JsonSerializer.Serialize(row)}");
        return won;
    }

    /// <summary>
    /// The scripted player: taps courage packs about a second after they drop, keeps two troops in front, deploys
    /// whatever else is ready, aims area spells at the thickest group of enemies (support spells at the troops in the
    /// fight), and fires the caravan spell when three enemies or a commander are on the field.
    /// </summary>
    private async Task<(bool Won, float Seconds, float Hull, float Gate)> SimPlay(BattleController battle)
    {
        await SimDrive(battle, 480f);
        var won = Read<bool>(battle, "_battleEnded") && Read<float>(battle, "_enemyBaseHealth") <= 0f;
        var hull = Read<float>(battle, "_playerBaseHealth") / Mathf.Max(1f, Read<float>(battle, "_playerBaseMaxHealth"));
        var gate = 1f - Read<float>(battle, "_enemyBaseHealth") / Mathf.Max(1f, Read<float>(battle, "_enemyBaseMaxHealth"));
        var seconds = Read<float>(battle, "_elapsed");
        await CloseBattle(battle);
        return (won, seconds, Mathf.Clamp(hull, 0f, 1f), Mathf.Clamp(gate, 0f, 1f));
    }

    /// <summary>Plays the battle as the scripted player until it ends or maxSeconds pass; leaves it open.</summary>
    private async Task SimDrive(BattleController battle, float maxSeconds)
    {
        var state = GameState.Instance;
        var units = Read<List<Unit>>(battle, "_units");
        var deck = Read<BattleDeckState>(battle, "_deck");
        var spellDeck = Read<BattleSpellState>(battle, "_spellDeck");
        var pickups = Read<List<CouragePickup>>(battle, "_couragePickups");
        var seen = new Dictionary<CouragePickup, float>();
        var spells = state.ActiveDeckSpellIds.Select(GameData.GetSpell).ToArray();
        var caravanReady = typeof(BattleController).GetProperty("CaravanSpellReady", Hidden)!;
        for (var tick = 0; !Read<bool>(battle, "_battleEnded") && Read<float>(battle, "_elapsed") < maxSeconds; tick++)
        {
            if (tick % 15 == 0)
            {
                var elapsed = Read<float>(battle, "_elapsed");
                foreach (var pickup in pickups.ToArray())
                {
                    if (!pickup.Available) continue;
                    if (!seen.TryGetValue(pickup, out var at)) seen[pickup] = at = elapsed;
                    if (elapsed - at >= 1f) Invoke(battle, "CollectCouragePickup", pickup);
                }
                var friends = units.Where(u => u.Team == Team.Player && !u.IsDead && !u.IsTacticalObject).ToArray();
                var foes = units.Where(u => u.Team == Team.Enemy && !u.IsDead && !u.IsUntargetable).ToArray();
                var front = friends.Count(u => !u.UsesProjectile && u.Definition.SquadTag != SquadSynergyCatalog.SupportTag);
                var courage = Read<float>(battle, "_courage");
                var card = deck.Roster
                    .OrderBy(u => front < 2 && !u.UsesProjectile && u.SquadTag != SquadSynergyCatalog.SupportTag ? 0 : 1)
                    .ThenBy(u => friends.Count(f => f.DefinitionId == u.Id))
                    .FirstOrDefault(u => deck.CanDeploy(u, courage, false, out _));
                if (card != null) Invoke(battle, "DeployPlayerUnit", card);
                foreach (var spell in spells)
                {
                    var resolved = state.BuildSpellStats(spell);
                    if (Read<float>(battle, "_mana") < resolved.ManaCost || spellDeck.GetCooldownRemaining(spell.Id) > 0) continue;
                    var ally = SimAllySpells.Contains(resolved.EffectType);
                    var pool = ally ? friends : foes;
                    if (pool.Length == 0 || (ally && foes.Length == 0)) continue;
                    var radius = Mathf.Max(30f, resolved.Radius);
                    var best = pool.OrderByDescending(u => pool.Count(o => o.Position.DistanceTo(u.Position) <= radius))
                        .ThenBy(u => ally ? u.Health / u.MaxHealth : u.Position.X).First();
                    var crowd = pool.Count(o => o.Position.DistanceTo(best.Position) <= radius);
                    // Hold an area spell for a group, unless a commander or a foe at the wagon needs it now.
                    if (!ally && crowd < 2 && !best.Definition.IsBoss && best.Position.X > 260f) continue;
                    if (ally && resolved.EffectType == "heal" && best.Health >= best.MaxHealth * .8f) continue;
                    Invoke(battle, "TryCastSpellAt", spell, best.Position);
                }
                if ((bool)caravanReady.GetValue(battle)! && (foes.Length >= 3 || foes.Any(f => f.Definition.IsBoss)))
                    Invoke(battle, "TryCastCaravanSpell");
            }
            battle._PhysicsProcess(1.0 / 60);
            if (tick % 600 == 0) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }
}
