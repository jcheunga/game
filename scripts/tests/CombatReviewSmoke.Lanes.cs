using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class CombatReviewSmoke
{
    // The shallow band, caravan deployment, support marching and which troops can hurt a base.
    private async Task CheckBattleLanes()
    {
        var combat = GameData.Combat;
        var center = (combat.BattlefieldTop + combat.BattlefieldBottom) * .5f;
        var top = combat.BattlefieldTop + combat.SpawnVerticalPadding;
        var bottom = combat.BattlefieldBottom - combat.SpawnVerticalPadding;
        var battle = await OpenBattle(1);
        Unit Spawn(string id, Team team, Vector2 position) => (Unit)Invoke(battle, "SpawnUnit", team, new UnitStats(GameData.GetUnit(id)), position);
        void Simulate(int ticks) { for (var i = 0; i < ticks; i++) Invoke(battle, "SimulateUnits", 1f / 60f); }
        void Clear() { foreach (var unit in Read<List<Unit>>(battle, "_units")) unit.TakeDamage(unit.MaxHealth * 1000); Invoke(battle, "CleanupDeadUnits"); }

        var fighters = GameData.GetPlayerUnits().Concat(GameData.GetEnemyUnits()).Where(unit => unit.AttackRange > 0).ToArray();
        Check(fighters.All(unit => unit.AggroRangeY >= combat.LaneHalfHeight && unit.AggroRangeY < combat.LaneHalfHeight * 2),
            "Every fighter on the centre line reaches both edges, but no fighter on one edge reaches the other");
        var marcher = Spawn("player_defender", Team.Player, new Vector2(200, center));
        var flanker = Spawn("enemy_walker", Team.Enemy, new Vector2(250, top));
        Check(marcher.IsInAggroRange(flanker, 1f), "A soldier marching from the wagon engages an enemy running along the top edge");
        marcher.Position = new Vector2(200, top);
        flanker.Position = new Vector2(250, bottom);
        Check(!marcher.IsInAggroRange(flanker, 1f), "A soldier pulled to the top edge cannot see an enemy along the bottom edge");
        Clear();

        Invoke(battle, "SpawnEnemyUnit", new UnitStats(GameData.GetUnit("enemy_runner")), new Vector2(1200, top - 80));
        Check(Read<List<Unit>>(battle, "_units").Last().Position == new Vector2(combat.EnemySpawnX, top),
            "Enemies leave the stronghold on any line up to the band's edge");
        Clear();

        // Support troops never walk back to an escort behind them; they keep marching to the far end.
        // The escort trails close enough to count as the formation leader.
        var escort = Spawn("player_brawler", Team.Player, new Vector2(90, center));
        var archer = Spawn("player_shooter", Team.Player, new Vector2(140, center));
        var enemyEscort = Spawn("enemy_walker", Team.Enemy, new Vector2(420, center));
        var caster = Spawn("enemy_spitter", Team.Enemy, new Vector2(370, center));
        float archerX = archer.Position.X, casterX = caster.Position.X;
        bool archerForward = true, casterForward = true;
        for (var i = 0; i < 180; i++)
        {
            Simulate(1);
            archerForward &= archer.Position.X >= archerX - .01f; archerX = archer.Position.X;
            casterForward &= caster.Position.X <= casterX + .01f; casterX = caster.Position.X;
        }
        Check(archerForward && archer.Position.X > 160, "A ranged soldier ahead of its escort keeps marching instead of falling back");
        Check(casterForward && caster.Position.X < 350, "A ranged enemy ahead of its escort keeps marching instead of falling back");
        Clear();

        // Only melee troops and authored siege weapons damage the wagon or the stronghold.
        var gate = new Vector2(combat.EnemyBaseX, center);
        var wagon = new Vector2(combat.PlayerBaseX, center);
        var gateArcher = Spawn("player_shooter", Team.Player, gate - new Vector2(90, 0));
        var ballista = Spawn("player_ballista", Team.Player, gate - new Vector2(120, 10));
        var swordsman = Spawn("player_brawler", Team.Player, gate - new Vector2(40, -10));
        var wagonCaster = Spawn("enemy_spitter", Team.Enemy, wagon + new Vector2(90, 0));
        var boneBallista = Spawn("enemy_boneballista", Team.Enemy, wagon + new Vector2(110, 10));
        var archerStart = gateArcher.Position;
        Simulate(2);
        Check(!gateArcher.IsAttackCommitted && gateArcher.Position == archerStart && !wagonCaster.IsAttackCommitted,
            "Archers and casters hold at firing range and cannot attack a base");
        Check(ballista.IsAttackingPosition(gate) && swordsman.IsAttackingPosition(gate) && boneBallista.IsAttackingPosition(wagon),
            "Melee troops and siege ballistas still attack the bases");
        Check(!new UnitStats(GameData.GetUnit("player_shooter")).DamagesStructures && new UnitStats(GameData.GetUnit("player_ballista")).DamagesStructures
            && new UnitStats(GameData.GetUnit("player_brawler")).DamagesStructures, "Only melee troops and authored siege units are flagged to hit bases");
        await CloseBattle(battle);
    }
}
