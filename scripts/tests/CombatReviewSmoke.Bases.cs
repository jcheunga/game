using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class CombatReviewSmoke
{
    // Each stage's painted stronghold at the enemy end of its field, with troops fighting at its gate for scale.
    // --stages=1,10 picks stages (default: every stage with a painted stronghold); --screenshots writes
    // artifacts/enemy-bases/stage-NNN.png.
    private async Task CheckEnemyBases()
    {
        var combat = GameData.Combat;
        var selected = OS.GetCmdlineUserArgs().FirstOrDefault(x => x.StartsWith("--stages="))?.Split('=')[1];
        var stages = selected != null ? selected.Split(',').Select(int.Parse)
            : Enumerable.Range(1, GameData.MaxStage).Where(s => EnemyBaseArt.Load(s) != null);
        foreach (var stage in stages)
        {
            var battle = await OpenBattle(stage);
            var art = (EnemyBaseArt)Invoke(battle, "get_PaintedBase");
            Check(art != null && art.Zone == GameData.GetStage(stage).MapId, $"Stage {stage} draws its own painted stronghold");
            if (art == null) { await CloseBattle(battle); continue; }
            var castle = battle.GetNode<Node2D>("Castle");
            Check(castle.Position.Y < combat.BattlefieldTop, $"Stage {stage}: the stronghold sorts behind every troop on the road");
            Check(Read<List<(BattleStructureArt Art, Vector2 Ground)>>(battle, "_outworks").Count == 0,
                $"Stage {stage}: the painting replaces the Blender outworks");
            Check(art.Rect.Position.X < combat.EnemySpawnX && art.Rect.End.X > combat.EnemyBaseX,
                $"Stage {stage}: the stronghold spans the point where troops strike it");
            Check(art.Foot < (combat.BattlefieldTop + combat.BattlefieldBottom) * .5f,
                $"Stage {stage}: the stronghold stands on the far half of the road ({art.Foot:0})");
            if (OS.GetCmdlineUserArgs().Contains("--screenshots") && DisplayServer.GetName() != "headless")
            {
                foreach (var (id, x, y) in new[] { ("player_brawler", -68f, 334f), ("player_defender", -52f, 350f),
                    ("player_spear", -94f, 344f), ("enemy_walker", -30f, 336f), ("enemy_brute", -18f, 352f) })
                {
                    var unit = (Unit)Invoke(battle, "SpawnUnit", id.StartsWith("player") ? Team.Player : Team.Enemy,
                        new UnitStats(GameData.GetUnit(id), healthScale: 20), new Vector2(combat.EnemyBaseX + x, y));
                    unit._Process(.5); unit._Process(.01); unit.SetProcess(false);
                }
                Write(battle, "_enemyBaseHealth", Read<float>(battle, "_enemyBaseMaxHealth") * .6f);
                Invoke(battle, "SetBattleCameraX", combat.BattlefieldRight + combat.BattlefieldLeft);
                for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                var folder = ProjectSettings.GlobalizePath("res://artifacts/enemy-bases");
                System.IO.Directory.CreateDirectory(folder);
                GetViewport().GetTexture().GetImage().SavePng($"{folder}/stage-{stage:000}.png");
            }
            await CloseBattle(battle);
        }
    }
}
