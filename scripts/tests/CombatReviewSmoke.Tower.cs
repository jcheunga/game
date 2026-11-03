using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

// The challenge tower: each floor's modifiers reach its battle, and how much they change a floor.
public partial class CombatReviewSmoke
{
    private async Task<BattleController> OpenTowerBattle(int floor, ulong seed)
    {
        var state = GameState.Instance;
        state.PrepareTowerBattle(floor);
        var battle = GD.Load<PackedScene>("res://scenes/Battle.tscn").Instantiate<BattleController>();
        AddChild(battle);
        battle.SetPhysicsProcess(false);
        Read<RandomNumberGenerator>(battle, "_rng").Seed = seed;
        Invoke(battle, "InitializeContent", seed);
        Invoke(battle, "InitializeCourageDrops", seed);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        return battle;
    }

    /// <summary>--tower: the floor card's modifiers are the ones its battle plays with, and the campaign keeps its own.</summary>
    private async Task CheckTower()
    {
        GameState.Instance.ResetProgress();
        var floor = ChallengeTowerCatalog.GetFloor(85);
        var battle = await OpenTowerBattle(85, 4185);
        var stage = Read<StageDefinition>(battle, "_stageData");
        Check(floor.Modifiers.Length == 3 && floor.Modifiers.All(m => stage.Modifiers.Any(s => s.Type == m.Type && s.Value == m.Value)),
            "A tower battle plays with every modifier its floor card lists");
        Check(!ReferenceEquals(stage, GameData.GetStage(85)) && GameData.GetStage(85).Modifiers.Length == 0,
            "The campaign stage under a tower floor keeps its own modifiers");
        Check(floor.Modifiers.All(m => m.Value > 0f), "Every tower modifier has a strength (elite vanguard and rapid assault do nothing without one)");
        var director = Read<BattleSpawnDirector>(battle, "_spawnDirector");
        Check(Mathf.IsEqualApprox(director.AdditionalEnemyHealthScale, floor.EnemyHealthScale * ChallengeTowerCatalog.ModifierValue("elite_vanguard")),
            $"Elite vanguard strengthens the floor's enemies on top of its own scale (health ×{director.AdditionalEnemyHealthScale:0.00})");
        var strip = ((IEnumerable<Rect2>)Invoke(battle, "CursedGroundAreas")).Single();
        var unit = (Unit)Invoke(battle, "SpawnUnit", Team.Player, new UnitStats(GameData.GetUnit("player_defender")), strip.GetCenter());
        var hp = unit.Health;
        Invoke(battle, "ApplyCursedGroundAttrition", 1f);
        Check(unit.Health < hp, "Cursed ground hurts a troop standing in the tower's marked strip");
        if (OS.GetCmdlineUserArgs().Contains("--capture"))
        {
            // A few seconds of floor 85 as the player sees it: the strip, elite foes and the deploy guard.
            var deck = Read<BattleDeckState>(battle, "_deck");
            Write(battle, "_courage", 200f);
            foreach (var card in deck.Roster.Take(3)) Invoke(battle, "DeployPlayerUnit", card);
            for (var tick = 0; tick < 480; tick++) { battle._PhysicsProcess(1.0 / 60); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
            var path = ProjectSettings.GlobalizePath("res://artifacts/tower/floor-85.png");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"TOWER_CAPTURE: {path}");
        }
        await CloseBattle(battle);

        battle = await OpenTowerBattle(10, 4110);
        Check(Read<StageDefinition>(battle, "_stageData").Modifiers.Length == 0 && ChallengeTowerCatalog.GetFloor(10).Modifiers.Length == 0,
            "The first twenty floors have no modifiers");
        await CloseBattle(battle);
        var rapid = new StageDefinition { Modifiers = ChallengeTowerCatalog.GetFloor(50).Modifiers };
        Check(StageModifiers.ResolveEnemySpawnIntervalScale(rapid) < 1f, "Rapid assault shortens the time between enemy spawns");

        var all = ChallengeTowerCatalog.GetAll();
        Check(all.Count == ChallengeTowerCatalog.MaxFloor && all.All(f => f.EnemyHealthScale >= .6f && f.EnemyDamageScale >= .75f && f.EnemyHealthScale <= 5f),
            "Every floor has a fitted enemy scale between the search's limits");
        // The fitted floors stay beatable: the reference player wins sample floors on the fit's own seeds. A change to
        // campaign stages, modifiers or troops that fails this needs the fit rerun (scripts/analysis/tower_fit.py).
        foreach (var number in new[] { 21, 61, 100 })
        {
            var beaten = true;
            for (var seed = 0; seed < 2 && beaten; seed++)
            {
                SeedTowerSquad(ChallengeTowerCatalog.GetFloor(number).BaseStageNumber, ReferenceUnitLevel(number));
                beaten = (await PlayTowerFloor(number, (ulong)(7000 + number * 10 + seed))).Won;
            }
            Check(beaten, $"The tower's reference player (troops at level {ReferenceUnitLevel(number)}) beats floor {number} as fitted");
        }
        GameState.Instance.ResetProgress();
    }

    /// <summary>
    /// --tower-balance [--floors=10,20,21] [--seeds=2]: plays each sample floor's campaign stage, then the floor without
    /// and with its modifiers, and prints TOWER_SAMPLE lines (mode campaign, plain or modifiers). The squad is a player who first-cleared the campaign to that floor's stage: deck units at
    /// that player level's cap, wagon upgrades at its rank cap, no relics. Score: a win is 1 + the hull left, a loss the
    /// share of the enemy gatehouse destroyed. Fixed 60 Hz steps, so a run repeats exactly.
    /// </summary>
    private async Task ExportTowerBalance()
    {
        string Arg(string name) => OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith($"--{name}="))?.Split('=')[1];
        var floors = Arg("floors")?.Split(',').Select(int.Parse).ToArray() ?? new[] { 10, 20, 21, 30, 40, 41, 50, 60, 61, 70, 80, 81, 90, 100 };
        var seeds = int.Parse(Arg("seeds") ?? "2");
        var modes = Arg("modes")?.Split(',');
        var squadStage = Arg("squad") is { } squad ? int.Parse(squad) : 0;
        var unitLevel = Arg("unit-level") is { } level ? int.Parse(level) : 0;
        // --strength=s plays every floor at health 1 + 4s and damage 1 + 2.5s instead of the catalog's.
        var strength = Arg("strength") is { } given ? float.Parse(given, System.Globalization.CultureInfo.InvariantCulture) : -1f;
        const BindingFlags field = BindingFlags.Instance | BindingFlags.NonPublic;
        var backing = typeof(TowerFloorDefinition).GetField("<Modifiers>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        foreach (var number in floors)
        {
            var floor = ChallengeTowerCatalog.GetFloor(number);
            var modifiers = floor.Modifiers;
            var (health, damage) = (floor.EnemyHealthScale, floor.EnemyDamageScale);
            if (strength >= 0f)
            {
                typeof(TowerFloorDefinition).GetField("<EnemyHealthScale>k__BackingField", field)!.SetValue(floor, 1f + 4f * strength);
                typeof(TowerFloorDefinition).GetField("<EnemyDamageScale>k__BackingField", field)!.SetValue(floor, 1f + 2.5f * strength);
            }
            foreach (var mode in modifiers.Length == 0 ? new[] { "campaign", "plain" } : new[] { "campaign", "plain", "modifiers" })
            {
                if (modes != null && !modes.Contains(mode)) continue;
                backing.SetValue(floor, mode == "modifiers" ? modifiers : Array.Empty<StageModifierDefinition>());
                for (var seed = 0; seed < seeds; seed++)
                {
                    // A victory pays XP and gold, so every run starts from the same squad.
                    SeedTowerSquad(squadStage > 0 ? squadStage : floor.BaseStageNumber, unitLevel);
                    var (won, seconds, hull, gate) = await PlayTowerFloor(number, (ulong)(5000 + number * 10 + seed), mode == "campaign");
                    var score = won ? 1f + hull : gate;
                    GD.Print($"TOWER_SAMPLE: {{\"floor\":{number},\"mode\":\"{mode}\",\"seed\":{seed},\"won\":{(won ? "true" : "false")},\"seconds\":{seconds:0.0},\"hull\":{hull:0.000},\"gate\":{gate:0.000},\"score\":{score:0.000}}}");
                }
            }
            backing.SetValue(floor, modifiers);
            typeof(TowerFloorDefinition).GetField("<EnemyHealthScale>k__BackingField", field)!.SetValue(floor, health);
            typeof(TowerFloorDefinition).GetField("<EnemyDamageScale>k__BackingField", field)!.SetValue(floor, damage);
        }
    }

    /// <summary>
    /// --tower-fit [--floors=...]: for each floor, the strongest enemy scale (health and damage rising together in the
    /// catalog's 4 : 2.5 shape, with the floor's modifiers on) that the floor's reference player beats on both of two
    /// seeds, up to the tower's original curve (s = (floor - 1) / 99, health ×5 and damage ×3.5 at floor 100). The reference player has the units of the floor's campaign stage, at
    /// that stage's unit cap rising evenly to level 20 by floor 100, since tower players train past the campaign.
    /// Prints TOWER_FIT lines; scripts/analysis/tower_fit.py builds the catalog's table from them.
    /// </summary>
    private async Task FitTowerFloors()
    {
        string Arg(string name) => OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith($"--{name}="))?.Split('=')[1];
        var floors = Arg("floors")?.Split(',').Select(int.Parse).ToArray() ?? Enumerable.Range(1, 20).Select(i => i * 5).Prepend(1).ToArray();
        const BindingFlags field = BindingFlags.Instance | BindingFlags.NonPublic;
        var health = typeof(TowerFloorDefinition).GetField("<EnemyHealthScale>k__BackingField", field)!;
        var damage = typeof(TowerFloorDefinition).GetField("<EnemyDamageScale>k__BackingField", field)!;
        foreach (var number in floors)
        {
            var floor = ChallengeTowerCatalog.GetFloor(number);
            var (originalHealth, originalDamage) = (floor.EnemyHealthScale, floor.EnemyDamageScale);
            var stage = floor.BaseStageNumber;
            var level = ReferenceUnitLevel(number);
            // Strength s puts health at 1 + 4s and damage at 1 + 2.5s; the original curve, the ceiling, had s = (floor - 1) / 99.
            var (low, high) = (0f, (number - 1) / 99f);
            async Task<bool> Beats(float strength)
            {
                health.SetValue(floor, 1f + 4f * strength);
                damage.SetValue(floor, 1f + 2.5f * strength);
                for (var seed = 0; seed < 2; seed++)
                {
                    SeedTowerSquad(stage, level);
                    if (!(await PlayTowerFloor(number, (ulong)(7000 + number * 10 + seed))).Won) return false;
                }
                return true;
            }
            // A tower battle lacks the campaign's battlefield help, so a floor whose modifiers are too much for the
            // reference player even unscaled searches down to enemies weaker than its campaign stage's (s = -0.1:
            // health ×0.6, damage ×0.75).
            const float lowest = -.1f;
            var beatsPlain = await Beats(0f);
            if (!beatsPlain) (low, high) = (lowest, 0f);
            var beatsLow = beatsPlain || await Beats(lowest);
            if (beatsPlain && await Beats(high)) low = high;
            else if (beatsLow)
                for (var step = 0; step < 6; step++)
                {
                    var mid = (low + high) / 2f;
                    if (await Beats(mid)) low = mid; else high = mid;
                }
            health.SetValue(floor, originalHealth);
            damage.SetValue(floor, originalDamage);
            GD.Print($"TOWER_FIT: {{\"floor\":{number},\"stage\":{stage},\"unitLevel\":{level},\"beatsUnscaled\":{(beatsPlain ? "true" : "false")},\"beatsLowest\":{(beatsLow ? "true" : "false")},\"strength\":{low:0.000},\"ceiling\":{(number - 1) / 99f:0.000},\"modifiers\":\"{string.Join(" ", floor.ForcedModifierIds)}\"}}");
        }
    }

    /// <summary>The tower's reference player's troop level on a floor: its campaign stage's cap, rising to 20 by floor 100.</summary>
    private static int ReferenceUnitLevel(int number)
    {
        var stage = ChallengeTowerCatalog.GetFloor(number).BaseStageNumber;
        var xp = Enumerable.Range(1, stage).Sum(s => PlayerLevelCatalog.CampaignXP(s, true, true));
        var cap = PlayerLevelCatalog.UnitLevelCap(PlayerLevelCatalog.LevelForXP(xp));
        return Mathf.RoundToInt(cap + (UnitBalance.MaxPlayerLevel - cap) * number / (float)ChallengeTowerCatalog.MaxFloor);
    }

    /// <summary>A player who first-cleared the campaign to this stage; unitLevel above zero overrides their troops' level.</summary>
    private void SeedTowerSquad(int stage, int unitLevel = 0)
    {
        var state = GameState.Instance;
        state.ResetProgress();
        var save = state.BuildSaveData();
        var xp = Enumerable.Range(1, stage).Sum(s => PlayerLevelCatalog.CampaignXP(s, true, true));
        var level = PlayerLevelCatalog.LevelForXP(xp);
        var cap = unitLevel > 0 ? unitLevel : PlayerLevelCatalog.UnitLevelCap(level);
        var units = GameData.GetPlayerUnits().Where(u => u.UnlockStage <= stage).ToArray();
        // Two of the latest melee troops, two of the latest ranged, a support if one is open, and fireball.
        IEnumerable<UnitDefinition> Latest(Func<UnitDefinition, bool> role, int count) => units.Where(role).OrderByDescending(u => u.UnlockStage).Take(count);
        var deck = Latest(u => !u.UsesProjectile && u.SquadTag != SquadSynergyCatalog.SupportTag, 2)
            .Concat(Latest(u => u.UsesProjectile && u.SquadTag != SquadSynergyCatalog.SupportTag, 2))
            .Concat(Latest(u => u.SquadTag == SquadSynergyCatalog.SupportTag, 1)).Select(u => u.Id).ToList();
        foreach (var extra in units.OrderByDescending(u => u.UnlockStage).Select(u => u.Id).Where(id => !deck.Contains(id)))
            if (deck.Count < 5) deck.Add(extra);
        var spell = GameData.GetPlayerSpells().Where(s => s.UnlockStage <= stage).OrderBy(s => s.Id == "spell_fireball" ? 0 : 1).First();
        save.HighestUnlockedStage = Math.Min(stage + 1, GameData.MaxStage);
        save.PlayerXP = xp;
        save.OwnedPlayerUnitIds = units.Select(u => u.Id).ToArray();
        save.UnitLevels = units.ToDictionary(u => u.Id, _ => cap);
        save.ActiveDeckUnitIds = deck.ToArray();
        save.OwnedPlayerSpellIds = new[] { spell.Id };
        save.ActiveDeckSpellIds = new[] { spell.Id };
        var rank = PlayerLevelCatalog.WagonRankCap(level);
        save.BaseUpgradeLevels = new[] { BaseUpgradeCatalog.HullPlatingId, BaseUpgradeCatalog.PantryId, BaseUpgradeCatalog.DispatchConsoleId }
            .ToDictionary(id => id, _ => rank);
        Invoke(state, "ApplySavedData", save);
        GD.Print($"TOWER_SQUAD: stage {stage}, player level {level}, unit level {cap}, wagon rank {rank}: {string.Join(", ", deck)} + {spell.Id}");
    }

    private async Task<(bool Won, float Seconds, float Hull, float Gate)> PlayTowerFloor(int floor, ulong seed, bool campaign = false)
    {
        var battle = campaign ? await OpenBattle(ChallengeTowerCatalog.GetFloor(floor).BaseStageNumber) : await OpenTowerBattle(floor, seed);
        var units = Read<List<Unit>>(battle, "_units");
        var deck = Read<BattleDeckState>(battle, "_deck");
        var spells = Read<BattleSpellState>(battle, "_spellDeck");
        var spell = GameData.GetSpell(GameState.Instance.ActiveDeckSpellIds.First());
        for (var tick = 0; !Read<bool>(battle, "_battleEnded") && Read<float>(battle, "_elapsed") < 300f; tick++)
        {
            if (tick % 30 == 0)
            {
                // Keep two troops in front, then deploy whatever is ready, and throw the spell at the nearest foe.
                var friends = units.Where(u => u.Team == Team.Player && !u.IsDead).ToArray();
                var front = friends.Count(u => !u.UsesProjectile && u.Definition.SquadTag != SquadSynergyCatalog.SupportTag);
                var courage = Read<float>(battle, "_courage");
                var card = deck.Roster.OrderBy(u => front < 2 && !u.UsesProjectile ? 0 : 1).ThenBy(u => friends.Count(f => f.DefinitionId == u.Id))
                    .FirstOrDefault(u => deck.CanDeploy(u, courage, false, out _));
                if (card != null) Invoke(battle, "DeployPlayerUnit", card);
                var foe = units.Where(u => u.Team == Team.Enemy && !u.IsDead).OrderBy(u => u.Position.X).FirstOrDefault();
                if (foe != null && Read<float>(battle, "_mana") >= spell.ManaCost && spells.GetCooldownRemaining(spell.Id) <= 0)
                    Invoke(battle, "TryCastSpellAt", spell, foe.Position);
            }
            battle._PhysicsProcess(1.0 / 60);
            if (tick % 600 == 0) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        var won = Read<bool>(battle, "_battleEnded") && Read<float>(battle, "_enemyBaseHealth") <= 0f;
        var hull = Read<float>(battle, "_playerBaseHealth") / Mathf.Max(1f, Read<float>(battle, "_playerBaseMaxHealth"));
        var gate = 1f - Read<float>(battle, "_enemyBaseHealth") / Mathf.Max(1f, Read<float>(battle, "_enemyBaseMaxHealth"));
        var seconds = Read<float>(battle, "_elapsed");
        await CloseBattle(battle);
        return (won, seconds, Mathf.Clamp(hull, 0f, 1f), Mathf.Clamp(gate, 0f, 1f));
    }
}
