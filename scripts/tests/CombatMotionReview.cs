using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

public partial class CombatMotionReview : Node
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private int _failures;
    private static T Read<T>(object o,string name) => (T)o.GetType().GetField(name,Hidden)!.GetValue(o)!;
    private static void Write(object o,string name,object value) => o.GetType().GetField(name,Hidden)!.SetValue(o,value);
    private static object Call(object o,string name,params object[] args) => o.GetType().GetMethod(name,Hidden)!.Invoke(o,args);
    private void Check(bool ok,string label)
    {
        GD.Print($"MOTION_CHECK: {(ok ? "PASS" : "FAIL")} {label}");
        if (!ok) _failures++;
    }
    public override void _Ready() => Callable.From(Run).CallDeferred();
    private Unit Spawn(string id,Team team,Vector2 position)
    {
        var unit=new Unit();
        unit.Setup(team,new UnitStats(GameData.GetUnit(id),healthScale:20),position);
        AddChild(unit); unit.SetProcess(false); unit._Process(.5); unit._Process(.01);
        return unit;
    }
    private async void Run()
    {
        try
        {
            if (!OS.GetCmdlineUserArgs().Any(x=>x.StartsWith("--save-suffix=motion-review-")))
                throw new InvalidOperationException("Requires isolated motion-review save.");
            GameState.Instance.SetAnalyticsConsent(false);
            GameState.Instance.SetShowHints(false);
            foreach (var id in GameData.PlayerRosterIds.Concat(GameData.EnemyRosterIds).Distinct())
            {
                var attacker=Spawn(id,Team.Player,new Vector2(400,340));
                var target=Spawn("enemy_walker",Team.Enemy,new Vector2(400+Mathf.Max(0,attacker.AttackRange-1),340));
                var sheet=UnitSpriteLoader.TryLoad(attacker.VisualClass,id);
                var clip=sheet.Animations[UnitAnimState.Attack];
                Check(clip.FrameCount==10 && clip.ContactFrame==4,id+" authored ten-frame/contact contract");
                Check(attacker.TryBeginAttack(target),id+" begins attack");
                var life=target.CombatLifetime;
                var before=target.Health;
                var hits=0;
                attacker.ScheduleAttackImpact(()=>{ if(attacker.CanResolveContact(target,life,attacker.UsesProjectile)) { hits++; target.TakeDamage(1); } });
                attacker._Process(1);
                Check(target.Health==before && Read<int>(attacker,"_spriteAnimFrame")==0,id+" render time cannot advance damage or attack");
                attacker.TickAttackTimer(attacker.AttackContactSeconds*.5f);
                attacker._Process(.01);
                Check(hits==0,id+" wind-up cannot deal damage");
                attacker.TakeDamage(1); attacker._Process(.01);
                Check(Read<UnitAnimState>(attacker,"_spriteAnimState")==UnitAnimState.Attack,id+" incoming hit cannot desynchronize committed swing");
                attacker.TickAttackTimer(attacker.AttackContactSeconds*.5f);
                attacker._Process(.01);
                Check(hits==1 && Read<int>(attacker,"_spriteAnimFrame")==4,id+" damage coincides with contact pose");
                if (!attacker.UsesProjectile && attacker.AttackRange>0)
                {
                    Check((Vector2)Call(attacker,"ContactDrawOffset")==Vector2.Zero,id+" keeps its feet fixed through contact");
                }
                attacker.TickAttackTimer(2); attacker.TickAttackTimer(2);
                Check(hits==1 && !attacker.IsAttackCommitted,id+" fires once and exits recovery after a large step");
                attacker.Free(); target.Free();
            }
            CheckCancellationAndPause();
            GameState.Instance.SetReducedMotion(true);
            CheckCancellationAndPause();
            GameState.Instance.SetReducedMotion(false);
            CheckHitReactionBlending();
            await CheckCrowdContacts();
            await CheckBattleIntegration();
            await CheckStructureContacts();
            if (OS.GetCmdlineUserArgs().Contains("--capture")) await CaptureDuels();
            if (OS.GetCmdlineUserArgs().Contains("--capture-crowd")) await CaptureCrowdContacts();
        }
        catch(Exception ex) { GD.PrintErr(ex.ToString()); _failures++; }
        GD.Print($"COMBAT_MOTION_RESULT: {_failures} failures");
        await LiveUiReview.StopAudio(this); GetTree().Quit(_failures==0?0:1);
    }
    private void CheckCancellationAndPause()
    {
        var a=Spawn("player_brawler",Team.Player,new Vector2(400,340));
        var b=Spawn("enemy_walker",Team.Enemy,new Vector2(430,340));
        a.TryAttack(b); var hp=b.Health;
        a.TakeDamage(100000); a.TickAttackTimer(2);
        Check(b.Health==hp,"Dead attacker cannot deliver a queued hit");
        a.Free();
        a=Spawn("player_brawler",Team.Player,new Vector2(400,340));
        a.TryAttack(b); b.ResetForPool(); b.Setup(Team.Enemy,new UnitStats(GameData.GetUnit("enemy_walker")),new Vector2(430,340)); hp=b.Health;
        a.TickAttackTimer(2);
        Check(b.Health==hp,"Reused target cannot inherit an old queued hit");
        a.TryAttack(b); a.ResetForPool(); a.Setup(Team.Player,new UnitStats(GameData.GetUnit("player_brawler")),new Vector2(400,340)); a.TickAttackTimer(2);
        Check(b.Health==hp,"Reused attacker drops pending actions");
        a.TryAttack(b); b.Position+=new Vector2(300,0); a.TickAttackTimer(2);
        Check(b.Health==hp,"Escaped targets do not receive remote melee hits");
        b.Position=new Vector2(375,340); a.TryAttack(b); a.TickAttackTimer(a.AttackContactSeconds); a._Process(.001);
        Check(a.WeaponContactPosition.X<a.GlobalPosition.X,"Attacker turns toward an enemy behind it");
        var frame=Read<int>(a,"_spriteAnimFrame");
        a.ShouldPausePresentation=()=>true; a._Process(2);
        Check(Read<int>(a,"_spriteAnimFrame")==frame,"Paused presentation holds its pose");
        a.Free(); b.Free();
    }
    private async Task<BattleController> Battle()
    {
        GameState.Instance.SetSelectedStage(1); GameState.Instance.PrepareCampaignBattle();
        var battle=GD.Load<PackedScene>("res://scenes/Battle.tscn").Instantiate<BattleController>();
        AddChild(battle); battle.SetPhysicsProcess(false);
        await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        return battle;
    }
    private async Task CheckBattleIntegration()
    {
        var battle=await Battle();
        var a=(Unit)Call(battle,"SpawnUnit",Team.Player,new UnitStats(GameData.GetUnit("player_shooter")),new Vector2(400,350));
        var b=(Unit)Call(battle,"SpawnUnit",Team.Enemy,new UnitStats(GameData.GetUnit("enemy_walker"),healthScale:20),new Vector2(460,350));
        a.TryBeginAttack(b); Call(battle,"QueueUnitStrike",a,b);
        Check(!battle.GetChildren().OfType<Projectile>().Any(),"Arrow is not spawned during the draw");
        a.TickAttackTimer(a.AttackContactSeconds);
        var p=battle.GetChildren().OfType<Projectile>().Single(); p.SetPhysicsProcess(false);
        Check(p.GlobalPosition.DistanceTo(a.WeaponContactPosition)<.1,"Arrow leaves the authored release point");
        var hp=b.Health; p._PhysicsProcess(2);
        Check(b.Health<hp,"Released arrow hits target body");
        a.TickAttackTimer(4); a.TryBeginAttack(b); Call(battle,"QueueUnitStrike",a,b); a.TickAttackTimer(a.AttackContactSeconds);
        p=battle.GetChildren().OfType<Projectile>().Single(); p.SetPhysicsProcess(false);
        b.ResetForPool(); b.Setup(Team.Enemy,new UnitStats(GameData.GetUnit("enemy_walker")),new Vector2(460,350)); hp=b.Health;
        p._PhysicsProcess(2);
        Check(b.Health==hp,"Projectile cannot hit a recycled target");
        battle.QueueFree(); await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        UnitPool.Clear(); ProjectilePool.Clear();
    }
    private async Task CaptureDuels()
    {
        var battle=await Battle();
        GetWindow().Mode=Window.ModeEnum.Windowed;
        GetWindow().Size=new Vector2I(1280,720);
        GetWindow().ContentScaleSize=new Vector2I(1280,720);
        GetWindow().ContentScaleMode=Window.ContentScaleModeEnum.Viewport;
        var pairs=new List<(Unit a,Unit b)>();
        var entries=new[] { ("player_brawler",330f,300f,30f), ("player_breacher",650f,300f,28f), ("player_hound",950f,300f,23f),
            ("player_spear",310f,475f,44f), ("player_shooter",570f,475f,136f), ("player_ballista",870f,475f,204f) };
        foreach(var (id,x,y,gap) in entries)
        {
            var a=(Unit)Call(battle,"SpawnUnit",Team.Player,new UnitStats(GameData.GetUnit(id),healthScale:100),new Vector2(x,y));
            var b=(Unit)Call(battle,"SpawnUnit",Team.Enemy,new UnitStats(GameData.GetUnit("enemy_walker"),healthScale:100),new Vector2(x+gap,y));
            a.SetProcess(false); b.SetProcess(false); a._Process(.5); a._Process(.01); b._Process(.5); b._Process(.01);
            pairs.Add((a,b));
        }
        var output=ProjectSettings.GlobalizePath("res://artifacts/blender/combat-motion/game-frames");
        System.IO.Directory.CreateDirectory(output);
        for(var frame=0;frame<90;frame++)
        {
            foreach(var (a,b) in pairs)
            {
                a.TickAttackTimer(1f/30); b.TickAttackTimer(1f/30);
                if(a.TryBeginAttack(b)) Call(battle,"QueueUnitStrike",a,b);
                else if (!a.UsesProjectile && !a.IsAttackCommitted && !a.CanAttack(b))
                    a.MoveToward(b.Position,1f/30,200,1150,200,550);
                if(b.TryBeginAttack(a)) Call(battle,"QueueUnitStrike",b,a);
                a._Process(1f/30); b._Process(1f/30);
            }
            foreach(var p in battle.GetChildren().OfType<Projectile>().ToArray()) { p.SetPhysicsProcess(false); p._PhysicsProcess(1f/30); }
            foreach(var effect in battle.GetChildren().OfType<WeaponContactEffect>().ToArray()) { effect.SetProcess(false); effect._Process(1f/30); }
            battle.QueueRedraw();
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng($"{output}/{frame:000}.png");
        }
        battle.QueueFree(); await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        UnitPool.Clear(); ProjectilePool.Clear();
    }

    private async Task CheckStructureContacts()
    {
        var battle=await Battle();
        var core=(Vector2)typeof(BattleController).GetProperty("PlayerBaseCorePosition",Hidden)!.GetValue(battle)!;
        var mechanic=(Unit)Call(battle,"SpawnUnit",Team.Player,new UnitStats(GameData.GetUnit("player_mechanic")),core);
        var hull=Read<float>(battle,"_playerBaseMaxHealth")-30;
        Write(battle,"_playerBaseHealth",hull);
        Call(battle,"SimulatePlayerBusSupport",mechanic,1f/60);
        Check(Read<float>(battle,"_playerBaseHealth")==hull,"Caravan repair waits for the tool stroke");
        mechanic.TickAttackTimer(mechanic.AttackContactSeconds);
        Check(Read<float>(battle,"_playerBaseHealth")>hull,"Tool contact repairs the caravan");

        battle.QueueFree(); await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        UnitPool.Clear(); ProjectilePool.Clear();
    }
}
