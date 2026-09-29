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
        Check((bool)Invoke(battle, "TryHoldCampaignFieldPoint", ally, enemy), "Troops hold a nearby post when combat is distant");
        enemy.Position = point + new Vector2(130, 0);
        Check(!(bool)Invoke(battle, "TryHoldCampaignFieldPoint", ally, enemy), "Nearby combat takes priority over capturing");
        enemy.Position = new Vector2(2400, 340);
        Invoke(battle, "UpdateCampaignField", plan.CaptureSeconds + .01f);
        Check(Read<bool>(battle, "_outpostCaptured") && Read<int>(battle, "_forwardDeploymentsRemaining") == plan.ForwardDeployments,
            "Holding the post grants the authored deployment budget");
        var deck = Read<BattleDeckState>(battle, "_deck");
        var card = deck.Roster[0];
        Invoke(battle, "ArmPlayerUnit", card);
        Write(battle, "_courage", 0f);
        Invoke(battle, "TryDeployAtY", point.Y);
        Check(Read<int>(battle, "_forwardDeploymentsRemaining") == plan.ForwardDeployments,
            "Unaffordable deployments cannot consume post charges");
        Write(battle, "_courage", 100f);
        var preview = (Vector2)Invoke(battle, "ResolvePlayerDeployPosition", point.Y - 200);
        Invoke(battle, "TryDeployAtY", point.Y - 200);
        var spawned = Read<List<Unit>>(battle, "_units").Last();
        Check(spawned.Position == preview && Mathf.IsEqualApprox(preview.X, point.X - 48) && preview.Y >= point.Y - 90,
            "Forward deployment exactly matches the preview and stays near its lane");
        Check(Read<int>(battle, "_forwardDeploymentsRemaining") == plan.ForwardDeployments - 1 && deck.GetCooldownRemaining(card.Id) > 0 && Read<float>(battle, "_courage") < 100,
            "Forward deployment consumes one charge and the normal courage and cooldown");
        Check(((Vector2)Invoke(battle, "ResolvePlayerDeployPosition", point.Y)).X == GameData.Combat.PlayerSpawnX,
            "Post recovery visibly falls back to the wagon");
        Write(battle, "_forwardCooldownRemaining", 0f);
        enemy.Position = point;
        Check(((Vector2)Invoke(battle, "ResolvePlayerDeployPosition", point.Y)).X == GameData.Combat.PlayerSpawnX,
            "Enemy occupation blocks forward deployments");
        enemy.Position = new Vector2(2400, 340);
        Write(battle, "_forwardDeploymentArmed", false);
        Check(((Vector2)Invoke(battle, "ResolvePlayerDeployPosition", point.Y)).X == GameData.Combat.PlayerSpawnX,
            "Players can save post charges by choosing the wagon");
        Write(battle, "_forwardDeploymentsRemaining", 0);
        Invoke(battle, "UpdateCampaignField", 30f);
        Check(Read<int>(battle, "_forwardDeploymentsRemaining") == 0, "Captured posts never refill their limited charges");
        ally.Position = (Vector2)Invoke(battle, "FieldPoint", plan.SupplyXRatio, plan.SupplyYRatio);
        var gate = Read<float>(battle, "_enemyBaseHealth");
        Invoke(battle, "UpdateCampaignField", 3f);
        Check(Read<bool>(battle, "_supplyCollected") && Read<float>(battle, "_enemyBaseHealth") < gate &&
            Read<float>(battle, "_fieldSummonSuppressionRemaining") == 18, "Siege supplies damage the gate and suppress summons");
        gate = Read<float>(battle, "_enemyBaseHealth");
        Invoke(battle, "CollectCampaignSupplies");
        Check(Read<float>(battle, "_enemyBaseHealth") == gate, "Supply rewards cannot be collected twice");
        var summoner = Spawn("enemy_lich", Team.Enemy, ally.Position + new Vector2(50, 0));
        Check(!(bool)Invoke(battle, "CanUseCampaignEnemySpecial", summoner), "Siege supply suppression interrupts nearby summoners");
        Write(battle, "_fieldSummonSuppressionRemaining", 0f);
        Check((bool)Invoke(battle, "CanUseCampaignEnemySpecial", summoner), "An engaged summoner resumes pressure after suppression expires");
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
            Invoke(battle, "CollectCampaignSupplies");
            if (stageNumber == 1) Check(Read<float>(battle, "_courage") == 25, "Courage supplies grant their stated reward");
            if (stageNumber == 13) Check(Read<float>(battle, "_playerBaseHealth") > 1 && Read<float>(battle, "_courage") == 8,
                "Repair supplies restore hull and courage");
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
