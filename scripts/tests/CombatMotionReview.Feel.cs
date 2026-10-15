using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class CombatMotionReview
{
    // Films real fights through the desktop battle camera, as a player sees them: matchups placed along the
    // walking band, simulated by the battle itself (movement, attacks, projectiles, hits, deaths). Run with
    // --fixed-fps 30 so every saved frame is 1/30 s of game time. Frames: artifacts/combat-feel/<take>/NNN.jpg
    //   godot --path . --fixed-fps 30 scenes/tests/CombatMotionReview.tscn -- --save-suffix=motion-review-feel --feel-only
    // Every shooter fires at once across the band; the shots are stepped to mid-flight and held there for the
    // capture, so each projectile can be judged at battle scale. artifacts/combat-feel/projectiles.png
    private async Task CaptureProjectiles()
    {
        GetWindow().Mode=Window.ModeEnum.Windowed; GetWindow().Size=new Vector2I(1280,720);
        GetWindow().ContentScaleSize=new Vector2I(1280,720); GetWindow().ContentScaleMode=Window.ContentScaleModeEnum.CanvasItems;
        var shooters=new[]{"player_shooter","player_ranger","player_longbowman","player_grenadier","player_quartermaster","player_ballista",
            "enemy_drowned_harpooner","enemy_dust_reaver","enemy_avalanche_bearer","enemy_plague_engine","enemy_cinder_spitter","enemy_powder_bandit"};
        for (var half=0;half<2;half++)
        {
            var battle=await Battle();
            var camera=Read<Camera2D>(battle,"_battleCamera");
            var top=Read<object>(battle,"_combat");
            float Prop(string n)=>(float)top.GetType().GetProperty(n)!.GetValue(top)!;
            var bandTop=Prop("BattlefieldTop")+Prop("SpawnVerticalPadding");
            var bandBottom=Prop("BattlefieldBottom")-Prop("SpawnVerticalPadding");
            var cx=camera.Position.X;
            var ids=shooters.Skip(half*6).Take(6).ToArray();
            var pairs=new List<(Unit a,Unit b)>();
            for (var i=0;i<ids.Length;i++)
            {
                // spread across the field as well as down the band, so shots do not stack on each other
                var y=Mathf.Lerp(bandTop,bandBottom,(i+.5f)/ids.Length);
                var lane=(i%3-1)*170f;
                var def=GameData.GetUnit(ids[i]);
                var player=def.IsPlayerSide;
                var a=(Unit)Call(battle,"SpawnUnit",player?Team.Player:Team.Enemy,new UnitStats(def,healthScale:50),new Vector2(cx+(player?-150:150),y));
                var gap=Mathf.Max(60f,a.AttackRange*.95f);
                a.Position=new Vector2(cx+lane+(player?-gap*.5f:gap*.5f),y);
                var b=(Unit)Call(battle,"SpawnUnit",player?Team.Enemy:Team.Player,new UnitStats(GameData.GetUnit(player?"enemy_brute":"player_paladin"),healthScale:50),
                    new Vector2(cx+lane+(player?gap*.5f:-gap*.5f),y));
                a.SetProcess(false); b.SetProcess(false); a._Process(.5); b._Process(.5);
                pairs.Add((a,b));
            }
            foreach (var (a,b) in pairs)
            {
                var ok=a.TryBeginAttack(b);
                if (ok) Call(battle,"QueueUnitStrike",a,b);
                a.TickAttackTimer(a.AttackContactSeconds+.001f);
                GD.Print($"FEEL_SHOT {a.DefinitionId} began={ok} range={a.AttackRange} projectile={a.UsesProjectile}");
            }
            for (var k=0;k<2;k++)
                foreach (var p in battle.GetChildren().OfType<Projectile>().ToArray()) { p.SetPhysicsProcess(false); p._PhysicsProcess(1f/30); p._Process(1f/30); }
            foreach (var (a,b) in pairs) { a._Process(.01); b._Process(.01); }
            camera.Zoom*=1.15f; camera.Position=new Vector2(cx,(bandTop+bandBottom)*.5f-20);
            for (var k=0;k<3;k++) await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            var dir=ProjectSettings.GlobalizePath("res://artifacts/combat-feel"); System.IO.Directory.CreateDirectory(dir);
            GetViewport().GetTexture().GetImage().SavePng($"{dir}/projectiles_{half}.png");
            GD.Print($"FEEL_PROJECTILES {half} {battle.GetChildren().OfType<Projectile>().Count()} in flight");
            battle.QueueFree(); await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            UnitPool.Clear(); ProjectilePool.Clear();
        }
    }

    private async Task CaptureBattleFeel()
    {
        if (OS.GetCmdlineUserArgs().Contains("--projectiles")) { await CaptureProjectiles(); return; }
        GetWindow().Mode=Window.ModeEnum.Windowed;
        GetWindow().Size=new Vector2I(1280,720);
        GetWindow().ContentScaleSize=new Vector2I(1280,720);
        GetWindow().ContentScaleMode=Window.ContentScaleModeEnum.CanvasItems;
        var takes=new (string name,(string player,string enemy)[] pairs,float enemyHealth)[]
        {
            ("melee", new[]{("player_brawler","enemy_walker"),("player_spear","enemy_brute"),("player_paladin","enemy_runner"),
                ("player_breacher","enemy_rustguard")}, 2.5f),
            ("ranged", new[]{("player_shooter","enemy_runner"),("player_ranger","enemy_walker"),("player_marksman","enemy_shieldwall"),
                ("player_grenadier","enemy_brute")}, 2.5f),
            ("beasts", new[]{("player_hound","enemy_grave_rat"),("player_raider","enemy_bone_lancer"),("player_ballista","enemy_bloater"),
                ("player_berserker","enemy_boss")}, 1.2f),
            // fragile on both sides, so the take is mostly falls, landings, pools and dissolves
            ("deaths", new[]{("player_paladin","enemy_walker"),("player_greatswordsman","enemy_brute"),("player_spear","enemy_runner"),
                ("player_shooter","enemy_drowned_harpooner"),("player_berserker","enemy_bone_lancer")}, .35f),
            // the abilities: beams, chain lightning, volleys, heals, a charge, roots and sleep, a summoning
            ("abilities", new[]{("player_marksman","enemy_briar_witch"),("player_stormcaller","enemy_dream_weaver"),
                ("player_field_medic","enemy_bone_cantor"),("player_raider","enemy_ash_hound"),("player_shooter","enemy_bellringer")}, 2.5f),
            // passives riding on blows: armour breaks, staggers, hooks, a great cut carrying through, parries, barnacle crust
            ("passives", new[]{("player_maceman","enemy_brute"),("player_billman","enemy_runner"),("player_greatswordsman","enemy_walker"),
                ("player_duelist","enemy_brute")}, 3f),
            // statuses on the Caravan line (applied by the take): burn, stun, sleep, root, shield, silence, frenzy
            ("status", new[]{("player_spear","enemy_walker"),("player_brawler","enemy_walker"),("player_paladin","enemy_walker"),
                ("player_militia","enemy_walker"),("player_man_at_arms","enemy_walker"),("player_marksman","enemy_walker"),
                ("player_berserker","enemy_walker")}, 6f),
            // the Gloamwood Witch behind a line of the Host: the fallen of both sides get back up for her
            ("witch", new[]{("player_paladin","enemy_walker"),("player_brawler","enemy_walker"),("player_spear","enemy_runner"),
                ("player_militia","enemy_walker")}, .6f),
        };
        var only=OS.GetCmdlineUserArgs().FirstOrDefault(a=>a.StartsWith("--take="))?.Substring(7);
        foreach (var take in takes.Where(t=>only==null || t.name==only))
        {
            var battle=await Battle();
            battle.SetPhysicsProcess(true);
            var camera=Read<Camera2D>(battle,"_battleCamera");
            var top=Read<object>(battle,"_combat");
            float Prop(string n)=>(float)top.GetType().GetProperty(n)!.GetValue(top)!;
            var bandTop=Prop("BattlefieldTop")+Prop("SpawnVerticalPadding");
            var bandBottom=Prop("BattlefieldBottom")-Prop("SpawnVerticalPadding");
            var viewW=battle.GetViewportRect().Size.X/camera.Zoom.X;
            var cx=camera.Position.X;
            var units=new List<Unit>();
            for (var i=0;i<take.pairs.Length;i++)
            {
                var y=Mathf.Lerp(bandTop,bandBottom,(i+.5f)/take.pairs.Length);
                var (pid,eid)=take.pairs[i];
                var p=(Unit)Call(battle,"SpawnUnit",Team.Player,new UnitStats(GameData.GetUnit(pid),healthScale:take.name=="deaths"?.6f:4),new Vector2(cx-viewW*.22f,y));
                var e=(Unit)Call(battle,"SpawnUnit",Team.Enemy,new UnitStats(GameData.GetUnit(eid),healthScale:take.enemyHealth),new Vector2(cx+viewW*.22f,y));
                units.Add(p); units.Add(e);
            }
            if (take.name=="passives")
            {
                // a second foe behind the Greatswordsman's, so the great cut has someone to carry into
                var row=Mathf.Lerp(bandTop,bandBottom,2.5f/take.pairs.Length)+9f;
                units.Add((Unit)Call(battle,"SpawnUnit",Team.Enemy,new UnitStats(GameData.GetUnit("enemy_walker"),healthScale:3f),new Vector2(cx+viewW*.22f+16f,row)));
            }
            Unit witch=null;
            if (take.name=="witch")
            {
                witch=(Unit)Call(battle,"SpawnUnit",Team.Enemy,new UnitStats(GameData.GetUnit("enemy_boss_verge"),healthScale:3f),
                    new Vector2(cx+viewW*.3f,(bandTop+bandBottom)*.5f));
                units.Add(witch);
            }
            var output=ProjectSettings.GlobalizePath($"res://artifacts/combat-feel/{take.name}");
            if (System.IO.Directory.Exists(output)) System.IO.Directory.Delete(output,true);
            System.IO.Directory.CreateDirectory(output);
            // --zoom=N: frame the fighting at N times the battle zoom (detail review), following the fighters
            var zoomArg=OS.GetCmdlineUserArgs().FirstOrDefault(a=>a.StartsWith("--zoom="));
            var zoom=zoomArg!=null?float.Parse(zoomArg.Substring(7),System.Globalization.CultureInfo.InvariantCulture):1f;
            var baseZoom=camera.Zoom;
            var frames=take.name=="witch" ? 600 : take.name is "abilities" or "status" or "passives" ? 420 : 210;
            for (var frame=0;frame<frames;frame++)
            {
                // five seconds in, wound the witch past her phase threshold: Hex Bloom and a mass raising
                if (witch!=null && frame==150 && IsInstanceValid(witch) && !witch.IsDead) witch.TakeDamage(witch.Health-witch.MaxHealth*.5f);
                if (take.name=="status" && frame%150==20)
                {
                    var line=units.Where(u=>IsInstanceValid(u) && !u.IsDead && u.Team==Team.Player).ToList();
                    void On(int i,System.Action<Unit> a){ if(i<line.Count) a(line[i]); }
                    On(0,u=>u.ApplyBurn(1f,6f,"test"));
                    On(1,u=>u.ApplyControl(2f));
                    On(2,u=>u.Sleep(2f));
                    On(3,u=>u.Root(2f));
                    On(4,u=>u.GiveShield(u.MaxHealth*.3f,6f));
                    On(5,u=>u.Silence(4f));
                    On(6,u=>u.ApplyTemporaryCombatBuff(1.3f,1f,6f));
                }
                camera.Zoom=baseZoom*zoom;
                if (Read<bool>(battle,"_battlePaused")) Call(battle,"TogglePause");
                var alive=units.Where(u=>IsInstanceValid(u) && !u.IsDead).ToList();
                var focus=alive.Count>0?new Vector2(alive.Average(u=>u.Position.X),alive.Average(u=>u.Position.Y)-12):new Vector2(cx,camera.Position.Y);
                camera.Position=zoom>1.01f?new Vector2(focus.X,(bandTop+bandBottom)*.5f-10):new Vector2(cx,camera.Position.Y);
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SaveJpg($"{output}/{frame:000}.jpg",.88f);
            }
            GD.Print($"FEEL_TAKE {take.name} {output}");
            battle.QueueFree(); await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            UnitPool.Clear(); ProjectilePool.Clear();
        }
    }
}
