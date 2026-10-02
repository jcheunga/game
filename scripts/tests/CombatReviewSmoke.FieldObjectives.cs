using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class CombatReviewSmoke
{
    private async Task CheckCampaignFieldObjectives()
    {
        var battle = await OpenBattle(8);
        var plan = Read<StageDefinition>(battle, "_stageData").Battlefield;
        var point = (Vector2)Invoke(battle, "FieldPoint", plan.OutpostXRatio, plan.OutpostYRatio);
        Unit Spawn(string id, Team team, Vector2 position) => (Unit)Invoke(battle, "SpawnUnit", team, new UnitStats(GameData.GetUnit(id)), position);
        var ally = Spawn("player_defender", Team.Player, point);
        var enemy = Spawn("enemy_walker", Team.Enemy, point + new Vector2(50, 0));
        Invoke(battle, "UpdateCampaignField", 4f);
        Check(!Read<bool>(battle, "_outpostCaptured"), "Contested post cannot be captured");
        enemy.Position = new Vector2(2400, 340);
        var allyStart = ally.Position;
        Check(!(bool)Invoke(battle, "TryHoldCampaignFieldPoint", ally, enemy), "Removed posts cannot hold or snap troops");
        Invoke(battle, "UpdateCampaignField", plan.CaptureSeconds + .01f);
        Check(!Read<bool>(battle, "_outpostCaptured") && Read<int>(battle, "_forwardDeploymentsRemaining") == 0,
            "Standing near a removed post never creates forward deployments");
        Check(ally.Position == allyStart, "Field updates leave troop positions unchanged");
        var deck = Read<BattleDeckState>(battle, "_deck");
        var card = deck.Roster[0];
        Invoke(battle, "ArmPlayerUnit", card);
        Write(battle, "_courage", 100f);
        var preview = (Vector2)Invoke(battle, "ResolvePlayerDeployPosition", point.Y - 200);
        Invoke(battle, "TryDeployAtY", point.Y - 200);
        var spawned = Read<List<Unit>>(battle, "_units").Last();
        Check(spawned.Position == preview && Mathf.IsEqualApprox(preview.X, GameData.Combat.PlayerSpawnX),
            "Deployment matches its preview and always begins at the wagon");
        Check(deck.GetCooldownRemaining(card.Id) > 0 && Read<float>(battle, "_courage") < 100,
            "Deployment consumes the normal courage and cooldown");
        ally.Position = (Vector2)Invoke(battle, "FieldPoint", plan.SupplyXRatio, plan.SupplyYRatio);
        var gate = Read<float>(battle, "_enemyBaseHealth");
        Invoke(battle, "UpdateCampaignField", 3f);
        Check(!Read<bool>(battle, "_supplyCollected") && Read<float>(battle, "_enemyBaseHealth") == gate,
            "Standing near a removed supply cache grants no capture reward");
        var summoner = Spawn("enemy_lich", Team.Enemy, ally.Position + new Vector2(50, 0));
        Check((bool)Invoke(battle, "CanUseCampaignEnemySpecial", summoner), "An engaged summoner can create a finite reinforcement wave");
        for (var i = 0; i < 3; i++)
        {
            summoner.TickSpecialTimer(100);
            Invoke(battle, "TriggerEnemyRaiseFallen", summoner);
            foreach (var raised in Read<List<Unit>>(battle, "_units").Where(u => u.Team == Team.Enemy && u != summoner && !u.IsDead))
                raised.TakeDamage(raised.MaxHealth * 100);
        }
        var unitCount = Read<List<Unit>>(battle, "_units").Count;
        summoner.TickSpecialTimer(100);
        Invoke(battle, "TriggerEnemyRaiseFallen", summoner);
        Check(Read<List<Unit>>(battle, "_units").Count == unitCount && !(bool)Invoke(battle, "CanAddCampaignPeriodicReinforcement", summoner),
            "Defeated summons exhaust the finite reserve instead of being replaced forever");
        summoner.Position = new Vector2(2450, 200);
        Check(!(bool)Invoke(battle, "CanUseCampaignEnemySpecial", summoner), "Remote summoners wait for the fighting to reach them");
        summoner.Position = ally.Position;
        Write(battle, "_enemyBaseHealth", 0f);
        Check(!(bool)Invoke(battle, "CanUseCampaignEnemySpecial", summoner), "Breaching the gate ends renewable summon pressure");
        await CloseBattle(battle);

        foreach (var stageNumber in new[] { 1, 13, 49 })
        {
            battle = await OpenBattle(stageNumber);
            Write(battle, "_courage", 0f);
            Write(battle, "_playerBaseHealth", 1f);
            if (stageNumber == 49)
            {
                var patches = ((IEnumerable<Rect2>)Invoke(battle, "CursedGroundAreas")).ToArray();
                Check(patches.Length == 2 && patches.Sum(p => p.Size.X) <= 600, "Cursed ground is limited to two bounded pockets");
                var unit = (Unit)Invoke(battle, "SpawnUnit", Team.Player, new UnitStats(GameData.GetUnit("player_defender")), patches[0].GetCenter());
                var hp = unit.Health;
                Invoke(battle, "ApplyCursedGroundAttrition", 1f);
                Check(unit.Health < hp, "Standing in a cursed pocket deals damage");
                unit.Position = (patches[0].End + patches[1].Position) * .5f;
                hp = unit.Health;
                Invoke(battle, "ApplyCursedGroundAttrition", 1f);
                Check(unit.Health == hp, "The gap between cursed pockets is safe");
            }
            await CloseBattle(battle);
        }
        CheckAdvanceEncounters();
        ExportStageLayoutReview();
    }

    private void CheckAdvanceEncounters()
    {
        var data = new StageDefinition { StageNumber = 1, EnemyHealthScale = 1, EnemyDamageScale = 1,
            Waves = new[] {
                new StageWaveDefinition { TriggerTime = 3, Area = "Approach", SpawnXRatio = .3f,
                    Entries = new[] { new StageWaveEntryDefinition { UnitId = "enemy_walker", Count = 1 } } },
                new StageWaveDefinition { TriggerTime = 100, Area = "Crossroads", SpawnXRatio = .6f, AdvanceTriggerXRatio = .4f,
                    Entries = new[] { new StageWaveEntryDefinition { UnitId = "enemy_walker", Count = 1 } } } } };
        var director = new BattleSpawnDirector(new RandomNumberGenerator());
        director.Initialize(1, data, GameData.Combat, GameData.GetEnemyUnits());
        director.EnableAdvanceEncounters(true);
        var positions = new List<Vector2>();
        director.Tick(1, 1, () => 0, (_, p) => positions.Add(p), _ => { });
        Check(director.EncounterWarningActive && positions.Count == 0, "Encounters warn before spawning");
        director.Tick(3, 4, () => 0, (_, p) => positions.Add(p), _ => { });
        Check(positions.Count == 1 && positions[0].X < GameData.Combat.EnemySpawnX, "Approach encounters use their local entry point");
        director.SetPlayerFrontline(1300);
        director.Tick(1, 5, () => 100, (_, p) => positions.Add(p), _ => { });
        Check(director.IsScriptedWaveHeld && positions.Count == 1, "Advancement respects the active enemy cap");
        director.Tick(1, 6, () => 0, (_, p) => positions.Add(p), _ => { });
        Check(director.EncounterWarningActive, "Reaching the next area arms its encounter before the fallback time");
        director.Tick(4, 10, () => 0, (_, p) => positions.Add(p), _ => { });
        Check(positions.Count == 2 && positions[1].X >= 1480 && director.NextScriptedWaveIndex == 2,
            "An advanced encounter spawns ahead of troops and keeps the full authored roster");
        director.Initialize(1, data, GameData.Combat, GameData.GetEnemyUnits());
        director.EnableAdvanceEncounters(true);
        for (var t = 0; t <= 110; t++) director.Tick(1, t, () => 0, (_, _) => { }, _ => { });
        Check(director.NextScriptedWaveIndex == 2, "Time fallback completes encounters without advancing troops");
    }
}
