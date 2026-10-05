using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class CombatReviewSmoke
{
    private async Task CaptureStructures(string name)
    {
        if (!OS.GetCmdlineUserArgs().Contains("--screenshots") || DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var folder = ProjectSettings.GlobalizePath("res://artifacts/battle-structures");
        System.IO.Directory.CreateDirectory(folder);
        GetViewport().GetTexture().GetImage().SavePng($"{folder}/{name}.png");
    }

    // The caravan's troop door, the centred stronghold and its outworks.
    private async Task CheckBattleStructures()
    {
        MobilePresentation.TestOverride = false;
        try
        {
            var battle = await OpenBattle(1);
            var combat = GameData.Combat;
            var center = (combat.BattlefieldTop + combat.BattlefieldBottom) * .5f;
            var camera = Read<Camera2D>(battle, "_battleCamera");
            void Frame(float x) { Invoke(battle, "SetBattleCameraX", x); camera.ForceUpdateScroll(); battle.QueueRedraw(); }
            void Simulate(float seconds) { for (var t = 0f; t < seconds; t += 1f / 60) battle._PhysicsProcess(1.0 / 60); }

            var foot = (Vector2)Invoke(battle, "get_CaravanDeployPosition");
            var exit = (Vector2)Invoke(battle, "get_WagonDoorExit");
            Check(Mathf.IsEqualApprox(foot.Y, center) && exit.Y < foot.Y && Mathf.Abs(exit.X - foot.X) < 1,
                "The ramp foot lies on the centre line, straight below the doorway");
            Check((int)Invoke(battle, "get_WagonDoorFrame") < 0, "The troop door starts shut");
            Frame(0);
            await CaptureStructures("wagon-door-shut");

            Write(battle, "_courage", 100f);
            var deck = Read<BattleDeckState>(battle, "_deck");
            Invoke(battle, "DeployPlayerUnit", deck.Roster[0]);
            var unit = Read<List<Unit>>(battle, "_units").Last(u => u.Team == Team.Player);
            Check(unit.Position == exit && !unit.Visible, "A deployed unit waits unseen in the doorway while the door drops");
            Simulate(.25f);
            Check((int)Invoke(battle, "get_WagonDoorFrame") >= 0 && unit.Visible && unit.Position.Y > exit.Y && unit.Position.Y < foot.Y,
                "The door opens and the unit walks down the ramp");
            await CaptureStructures("wagon-door-exit");
            Simulate(.21f);
            Check(Mathf.Abs(unit.Position.Y - foot.Y) < .5f && unit.Position.X >= foot.X - .5f && unit.Position.X - foot.X < 8,
                "The unit leaves the ramp on the centre line");
            Simulate(1.2f);
            Check((int)Invoke(battle, "get_WagonDoorFrame") < 0 && unit.Position.X > foot.X, "The door shuts behind the troops as they march");
            await CaptureStructures("wagon-door-march");

            var castle = battle.GetNode<Node2D>("Castle");
            var art = BattleStructureArt.Load("gatehouse");
            var rect = art.At(castle.Position);
            Check(rect.Position.Y < combat.BattlefieldTop && rect.End.Y > combat.BattlefieldBottom - 10 &&
                Mathf.Abs(rect.Position.Y + art.Centre.Y * rect.Size.Y - center) < 1,
                "The stronghold straddles the band, its mass on the centre line");
            var outworks = Read<List<(BattleStructureArt Art, Vector2 Ground)>>(battle, "_outworks");
            Check(outworks.Count == 5, "Five outworks flank the stronghold");
            Check(outworks.All(o => o.Ground.X > combat.EnemySpawnX + 40 || o.Ground.Y < combat.BattlefieldTop + combat.SpawnVerticalPadding ||
                o.Ground.Y > combat.BattlefieldBottom - combat.SpawnVerticalPadding),
                "Outworks stand behind the gate or beyond the band's edges, clear of the approach");
            Check(!Read<List<Unit>>(battle, "_units").Any(u => u.Name.ToString().StartsWith("Outwork")),
                "Outworks are scenery: never units, never targets");
            Frame(combat.BattlefieldRight + combat.BattlefieldLeft);
            await CaptureStructures("stronghold-outworks");
            await CloseBattle(battle);
        }
        finally { MobilePresentation.TestOverride = null; }
    }
}
