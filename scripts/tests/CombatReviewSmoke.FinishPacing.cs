using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class CombatReviewSmoke
{
    private async Task CheckFinishPacing()
    {
        var battle = await OpenBattle(20);
        var director = Read<BattleSpawnDirector>(battle, "_spawnDirector");
        director.TryBuildEnemyStats(GameData.EnemyBossTidemasterId, out var stats);
        var boss = (Unit)Invoke(battle, "SpawnUnit", Team.Enemy, stats, new Vector2(900, 220));
        var units = Read<List<Unit>>(battle, "_units");
        int Escorts() => units.Count(unit => !unit.IsDead && unit.DefinitionId == boss.SpecialSpawnUnitId);
        // Reinforcement types are authored data; the cap includes existing bodies of that type.
        director.TryBuildEnemyStats(boss.SpecialSpawnUnitId, out var engineStats);
        Invoke(battle, "SpawnUnit", Team.Enemy, engineStats, new Vector2(880, 260));
        for (var call = 0; call < 5; call++)
        {
            boss.TickSpecialTimer(100);
            Invoke(battle, "TryTriggerEnemySpecialAbility", boss);
        }
        Check(Escorts() == 2, "Tidemaster rally replacements respect existing escorts and stop at two");
        Invoke(battle, "ApplyCampaignBossPhase", boss);
        Check(Escorts() == 2, "Tidemaster phase cannot add escorts above its rally limit");
        units.First(unit => !unit.IsDead && unit.DefinitionId == boss.SpecialSpawnUnitId).TakeDamage(10000);
        boss.TickSpecialTimer(100);
        Invoke(battle, "TryTriggerEnemySpecialAbility", boss);
        Check(Escorts() == 2, "Tidemaster can replace a defeated escort");
        Invoke(battle, "SpawnEnemyEscortsNear", boss, GameData.EnemySpitterId, 1, 32f, 58f);
        Check(units.Any(unit => !unit.IsDead && unit.DefinitionId == GameData.EnemySpitterId),
            "Escort cap does not suppress other enemy types");
        await CloseBattle(battle);

        battle = await OpenBattle(30);
        director = Read<BattleSpawnDirector>(battle, "_spawnDirector");
        director.TryBuildEnemyStats(GameData.EnemyBossForgeId, out var forgeStats);
        var forge = (Unit)Invoke(battle, "SpawnUnit", Team.Enemy, forgeStats, new Vector2(900, 220));
        units = Read<List<Unit>>(battle, "_units");
        for (var i = 0; i < 5; i++)
        {
            forge.TickSpecialTimer(100);
            Invoke(battle, "TryTriggerEnemySpecialAbility", forge);
        }
        Check(units.Count(u => !u.IsDead && u.DefinitionId == forge.SpecialSpawnUnitId) == 2,
            "Iron Warden's rally and phase pressure share a two-escort limit");
        var escort = units.First(u => !u.IsDead && u.DefinitionId == forge.SpecialSpawnUnitId);
        escort.TakeDamage(10000);
        forge.TickSpecialTimer(100);
        Invoke(battle, "TryTriggerEnemySpecialAbility", forge);
        Check(units.Count(u => !u.IsDead && u.DefinitionId == forge.SpecialSpawnUnitId) == 2,
            "Iron Warden can replace a defeated escort without accumulating more");
        await CloseBattle(battle);

    }
}
