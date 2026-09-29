using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class CombatMotionReview
{
    private void CheckHitReactionBlending()
    {
        var single = new HitReactionMotion();
        var crowd = new HitReactionMotion();
        single.Add(1, .5f);
        for (var i=0; i<6; i++) crowd.Add(1, .5f);
        single.Advance(.03f); crowd.Advance(.03f);
        Check(Mathf.IsEqualApprox(single.Amount,crowd.Amount), "Six simultaneous hits share one flinch, not six stacked recoils");
        var pose = crowd.Amount;
        crowd.Add(1, .5f);
        Check(crowd.Amount==pose, "A new overlapping hit never snaps the current pose");
        single.Advance(.09f); crowd.Advance(.09f);
        Check(Mathf.IsEqualApprox(single.Amount,crowd.Amount), "Overlapping hits do not restart or extend the reaction window");

        foreach (var reverse in new[]{false,true})
        {
            var opposed = new HitReactionMotion();
            opposed.Add(reverse?-1:1, .6f); opposed.Add(reverse?1:-1, .6f); opposed.Advance(.05f);
            Check(Mathf.Abs(opposed.Amount)<.00001f, "Opposing simultaneous hits balance without last-hit direction bias");
        }
        var light = new HitReactionMotion(); var mixed = new HitReactionMotion(); var heavy = new HitReactionMotion();
        light.Add(1,.3f); mixed.Add(1,.3f); mixed.Add(1,.8f); heavy.Add(1,.8f);
        light.Advance(.04f); mixed.Advance(.04f); heavy.Advance(.04f);
        Check(mixed.Amount>light.Amount && Mathf.IsEqualApprox(mixed.Amount,heavy.Amount), "A heavier hit leads the blended reaction without adding amplitudes");
        var coarse = new HitReactionMotion(); var fine = new HitReactionMotion();
        coarse.Add(1,.7f); fine.Add(1,.7f);
        for (var i=0;i<6;i++) coarse.Advance(1f/30);
        for (var i=0;i<24;i++) fine.Advance(1f/120);
        Check(Mathf.Abs(coarse.Amount-fine.Amount)<.0001f, "Recoil settles consistently at 30 and 120 presentation frames per second");
        var bounded = true;
        for (var i=0;i<240;i++)
        {
            crowd.Add(i%11<7?1:-1, 100);
            crowd.Advance(1f/120);
            bounded &= float.IsFinite(crowd.Amount) && Mathf.Abs(crowd.Amount)<=1;
        }
        Check(bounded, "Sustained crossfire cannot grow the flinch beyond its cap");
        crowd.Advance(2);
        Check(Mathf.Abs(crowd.Amount)<.0001f, "A slow frame safely settles recoil with no overshoot");

        var unit=Spawn("enemy_walker",Team.Enemy,new Vector2(500,350));
        unit.Position+=Vector2.Right; unit.TakeDamage(1);
        unit.ReactToContact(1,15); unit._Process(.03);
        Check(Read<UnitAnimState>(unit,"_spriteAnimState")==UnitAnimState.Walk,
            "Ordinary hit keeps locomotion playing rather than selecting the large authored hit pose");
        var amount=unit.HitReactionAmount;
        unit.ShouldPausePresentation=()=>true; unit._Process(1);
        Check(unit.HitReactionAmount==amount, "Pause holds the blended hit reaction");
        unit.ShouldPausePresentation=null;
        GameState.Instance.SetReducedMotion(true); unit._Process(.01); unit.ReactToContact(1,100);
        Check(unit.HitReactionAmount==0, "Reduced motion clears and suppresses recoil");
        GameState.Instance.SetReducedMotion(false);
        unit.ReactToContact(1,30); unit._Process(.03); unit.ResetForPool();
        Check(unit.HitReactionAmount==0, "Pooling clears all accumulated hit-reaction state");
        unit.Free();

        var number=new BattleFloatText {PresentationScale=()=>1.55f/3.4f};
        AddChild(number); number.SetProcess(false); number.Setup("-15",Colors.White);
        Check(Mathf.IsEqualApprox(number.Scale.X*3.4f,1.55f), "Close-up damage labels start at HUD scale rather than model zoom");
        number._Process(.02);
        Check(number.Scale.X*3.4f<1.7f, "Animated damage text stays restrained in the closest camera view");
        number.PresentationScale=()=>1.55f/2.2f; number._Process(.02);
        Check(number.Scale.X*2.2f<1.7f, "Existing damage labels adapt when the combat zoom changes");
        number.Free();
    }

    private Unit CrowdUnit(BattleController battle,string id,Team team,Vector2 position)
    {
        var unit=(Unit)Call(battle,"SpawnUnit",team,new UnitStats(GameData.GetUnit(id),healthScale:100),position);
        unit.SetProcess(false); unit._Process(.5); unit._Process(.01);
        return unit;
    }

    private async Task CheckCrowdContacts()
    {
        foreach(var mode in new[]{"same tick","reverse order","staggered","opposing sides"})
        {
            var battle=await Battle();
            var target=CrowdUnit(battle,"enemy_walker",Team.Enemy,new Vector2(600,350));
            var attackers=Enumerable.Range(0,6).Select(i=>
            {
                var a=CrowdUnit(battle,"player_brawler",Team.Player,Vector2.Zero);
                var direction=mode=="opposing sides" && i%2==1?1:-1;
                var y=(i-2.5f)*3;
                var x=Mathf.Sqrt(Mathf.Pow(a.AttackRange-.01f,2)-y*y)*direction;
                a.Position=target.Position+new Vector2(x,y);
                return a;
            }).ToArray();
            var positions=attackers.Select(a=>a.Position).ToArray(); var feet=target.Position;
            var before=target.Health;
            var allReady=true;
            foreach(var a in attackers) { allReady &= a.TryBeginAttack(target); Call(battle,"QueueUnitStrike",a,target); }
            Check(allReady,$"{mode}: six attackers can commit at the edge of reach");
            var counterHits=0;
            target.TryBeginAttack(attackers[0]); target.ScheduleAttackImpact(()=>counterHits++);
            foreach(var a in mode=="reverse order"?attackers.Reverse():attackers)
            {
                a.TickAttackTimer(a.AttackContactSeconds);
                if(mode=="staggered") target._Process(.025);
            }
            var expected=attackers.Sum(a=>a.CurrentAttackDamage)*target.DamageTakenScale;
            Check(Mathf.Abs(before-target.Health-expected)<.01f,$"{mode}: every valid contact applies its damage exactly once");
            Check(target.Position==feet && positions.SequenceEqual(attackers.Select(a=>a.Position)),
                $"{mode}: ordinary hits move neither the target nor its attackers out of reach");
            target._Process(.025);
            Check(Read<UnitAnimState>(target,"_spriteAnimState")==UnitAnimState.Attack,
                $"{mode}: the surrounded defender retains its own attack pose");
            target.TickAttackTimer(target.AttackContactSeconds);
            Check(counterHits==1,$"{mode}: the surrounded defender can still strike back");
            foreach(var a in attackers) a.TickAttackTimer(2);
            Check(Mathf.Abs(before-target.Health-expected)<.01f,$"{mode}: recovery cannot duplicate crowd damage");
            Check(Read<float>(battle,"_impactShakeStrength")==0,$"{mode}: ordinary sword hits do not shake the battlefield");
            Call(battle,"ApplyImpactReaction",attackers[0],target,80f,false);
            Check(Read<float>(battle,"_impactShakeStrength")<=.65f && target.Position==feet,
                $"{mode}: heavy hit accent is capped and leaves the feet planted");
            Call(battle,"PushUnitsFromPoint",Team.Enemy,feet-new Vector2(10,0),50f,12f,.8f,.2f,Colors.White,"");
            Check(target.Position.X>feet.X+11,$"{mode}: explicit ability and hazard knockback remains functional");
            battle.QueueFree(); await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            UnitPool.Clear(); ProjectilePool.Clear();
        }
    }

    private async Task CaptureCrowdContacts()
    {
        MobilePresentation.TestOverride=true;
        GetWindow().Mode=Window.ModeEnum.Windowed; GetWindow().Size=new Vector2I(844,390);
        GetWindow().ContentScaleSize=new Vector2I(1280,720);
        GetWindow().ContentScaleMode=Window.ContentScaleModeEnum.CanvasItems;
        var battle=await Battle();
        Call(battle,"CycleMobileZoom"); Call(battle,"ToggleMobileClearView");
        Write(battle,"_mobileFollow",false);
        var camera=Read<Camera2D>(battle,"_mobileCamera"); camera.Position=new Vector2(500,335); camera.ForceUpdateScroll();
        var target=CrowdUnit(battle,"enemy_brute",Team.Enemy,new Vector2(500,360));
        var sword=CrowdUnit(battle,"player_brawler",Team.Player,new Vector2(470,350));
        var spear=CrowdUnit(battle,"player_spear",Team.Player,new Vector2(467,391));
        var flank=CrowdUnit(battle,"player_brawler",Team.Player,new Vector2(530,351));
        var attackers=new[]{sword,spear,flank};
        var feet=target.Position;
        var output=ProjectSettings.GlobalizePath("res://artifacts/mobile-review/crowd-hit-frames");
        System.IO.Directory.CreateDirectory(output);
        for(var frame=0;frame<120;frame++)
        {
            foreach(var unit in attackers.Append(target)) unit.TickAttackTimer(1f/30);
            // Start each demonstration beat together; the real authored contact
            // timings still determine when each weapon actually lands.
            if(frame%45==0)
            {
                foreach(var a in attackers) if(a.TryBeginAttack(target)) Call(battle,"QueueUnitStrike",a,target);
                // Isolate the incoming flinch for the first two beats, then
                // demonstrate that the defender can counter through the third.
                if(frame>=90 && target.TryBeginAttack(sword)) Call(battle,"QueueUnitStrike",target,sword);
            }
            foreach(var unit in attackers.Append(target)) unit._Process(1f/30);
            foreach(var effect in battle.GetChildren().OfType<WeaponContactEffect>().ToArray()) {effect.SetProcess(false); effect._Process(1f/30);}
            battle.QueueRedraw();
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng($"{output}/{frame:000}.png");
        }
        Check(target.Position==feet,"Captured three-on-one exchange leaves the shared target planted");
        battle.QueueFree(); await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        UnitPool.Clear(); ProjectilePool.Clear(); MobilePresentation.TestOverride=null;
    }
}
