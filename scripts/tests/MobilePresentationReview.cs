using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

public partial class MobilePresentationReview : Node
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private int _failures;
    private static T Read<T>(object o,string name)=>(T)o.GetType().GetField(name,Hidden)!.GetValue(o)!;
    private static void Write(object o,string name,object value)=>o.GetType().GetField(name,Hidden)!.SetValue(o,value);
    private static object Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Hidden)!.Invoke(o,args);
    private void Check(bool ok,string label) { GD.Print($"MOBILE_CHECK: {(ok?"PASS":"FAIL")} {label}"); if(!ok) _failures++; }
    public override void _Ready()=>Callable.From(Run).CallDeferred();
    private async Task Settle(int frames=5) { for(var i=0;i<frames;i++) await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame); }
    private async Task Capture(string name)
    {
        if(DisplayServer.GetName()=="headless") return;
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        var path=ProjectSettings.GlobalizePath("res://artifacts/mobile-review");
        System.IO.Directory.CreateDirectory(path);
        GetViewport().GetTexture().GetImage().SavePng($"{path}/{name}.png");
    }
    private async Task<BattleController> Battle(bool mobile,int stage=1,bool endless=false)
    {
        MobilePresentation.TestOverride=mobile;
        GameState.Instance.SetSelectedStage(stage);
        if(endless) GameState.Instance.PrepareEndlessBattle(GameData.GetStage(stage).MapId);
        else GameState.Instance.PrepareCampaignBattle();
        var battle=GD.Load<PackedScene>("res://scenes/Battle.tscn").Instantiate<BattleController>();
        AddChild(battle); battle.SetPhysicsProcess(false);
        await Settle();
        foreach(var pair in new[] {("player_brawler",390f,350f),("player_spear",440f,405f),("player_shooter",290f,385f),
            ("enemy_walker",420f,350f),("enemy_brute",470f,405f),("enemy_walker",560f,410f)})
        {
            var team=pair.Item1.StartsWith("player")?Team.Player:Team.Enemy;
            var unit=(Unit)Call(battle,"SpawnUnit",team,new UnitStats(GameData.GetUnit(pair.Item1),healthScale:20),new Vector2(pair.Item2,pair.Item3));
            unit._Process(.5); unit._Process(.01); unit.SetProcess(false);
        }
        if(mobile)
        {
            Write(battle,"_mobileFollow",false);
            var camera=Read<Camera2D>(battle,"_mobileCamera"); camera.Position=new Vector2(395,380); camera.ForceUpdateScroll();
        }
        Call(battle,"UpdateHud"); Call(battle,"RefreshMobileHud");
        await Settle(); return battle;
    }
    private async void Run()
    {
        try
        {
            if(!OS.GetCmdlineUserArgs().Any(x=>x.StartsWith("--save-suffix=mobile-review-")))
                throw new InvalidOperationException("Requires isolated mobile-review save.");
            GameState.Instance.SetAnalyticsConsent(false); GameState.Instance.SetShowHints(false);
            CheckParticleSizes();
            var insets=SafeAreaService.LogicalInsets(new Rect2(150,0,3540,2100),new Transform2D(0,Vector2.One*3,0,Vector2.Zero),new Vector2(1280,720));
            Check(insets.DistanceTo(new Vector4(50,0,50,20))<.01,"Notch and home-indicator pixels convert to logical canvas margins");
            insets=SafeAreaService.LogicalInsets(new Rect2(30,0,784,390),new Transform2D(0,Vector2.One*(390f/720),0,new Vector2(75.333f,0)),new Vector2(1280,720));
            Check(insets.Length()<.01,"Letterbox padding already protects safe-area insets");
            GetWindow().Mode=Window.ModeEnum.Windowed;
            GetWindow().Size=new Vector2I(844,390);
            GetWindow().ContentScaleSize=new Vector2I(1280,720);
            GetWindow().ContentScaleMode=Window.ContentScaleModeEnum.CanvasItems;
            await Settle();
            var before=await Battle(false);
            Check(Read<Camera2D>(before,"_mobileCamera")==null,"Desktop retains original framing");
            await Capture("phone-before"); before.QueueFree(); await Settle();
            var battle=await Battle(true);
            var camera=Read<Camera2D>(battle,"_mobileCamera");
            var hud=Read<Control>(battle,"_mobileHud");
            Check(camera.Zoom==Vector2.One*MobilePresentation.BattleZoom,"Mobile characters enlarged without changing unit scale");
            Check(hud.Size.DistanceTo(battle.GetViewportRect().Size/1.55f)<1,"HUD fits logical phone viewport");
            var actionCards=hud.FindChildren("*","Control",true,false).OfType<BattleActionCard>().ToArray();
            Check(actionCards.Length>=5,"The battle bar contains real icon-first unit and spell cards");
            foreach(var art in actionCards)
            {
                var button=(Button)art.GetParent();
                var labels=art.FindChildren("*","Label",true,false).OfType<Label>().Where(l=>l.IsVisibleInTree()).ToArray();
                var cardRect=button.GetGlobalRect();
                Check(labels.All(l=>cardRect.Grow(.1f).Encloses(l.GetGlobalRect())),"Corner cost and temporary status remain inside card");
                Check(art.CostPlate.Position.X>=art.Size.X*.5f && art.CostPlate.Position.Y<=6,"Courage cost sits in the top-right corner");
                Check(art.Portrait.Texture!=null && art.Portrait.Size.Y>=art.Size.Y*.8f,"Large portrait dominates the card");
                Check(labels.All(l=>!l.Text.Contains("DEPLOY") && !l.Text.Contains("CAST") && !l.Text.Contains("Ready")),"Ready cards have no redundant action words");
                Check(button.CustomMinimumSize.X>=56 && button.CustomMinimumSize.Y>=56 && !string.IsNullOrWhiteSpace(button.AccessibilityName),
                    "Icon-only cards retain large touch targets and accessible names");
            }
            await Capture("phone-after");
            await CheckCloserViews(battle);
            if(OS.GetCmdlineUserArgs().Contains("--capture-motion")) await CaptureCombat(battle);
            var top=Read<PanelContainer>(battle,"_topHudPanel");
            Check(top.GetGlobalRect().End.X<=battle.GetViewportRect().Size.X+1,"Header fits phone width");
            foreach(var b in top.FindChildren("*","Button",true,false).OfType<Button>().Where(b=>b.IsVisibleInTree()))
            {
                Check(b.Size.Y>=56,$"Touch target {b.Text}/{b.TooltipText} is at least 56 logical pixels");
                Check(b.GetGlobalRect().End.X<=battle.GetViewportRect().Size.X+1,"Header button remains on screen");
            }
            var point=new Vector2(430,340);
            var screen=battle.GetCanvasTransform()*point;
            Check(((Vector2)Call(battle,"ScreenToBattle",screen)).DistanceTo(point)<.01,"Zoomed taps map back to world coordinates");
            Write(battle,"_courage",100f);
            Call(battle,"ArmPlayerUnit",GameData.GetUnit("player_brawler"));
            var count=Read<int>(battle,"_playerDeployments");
            battle._UnhandledInput(new InputEventScreenTouch { Index=0,Pressed=true,Position=screen });
            Check(Read<int>(battle,"_playerDeployments")==count,"Touch down does not deploy before drag is known");
            battle._Input(new InputEventScreenTouch {Index=0,Pressed=false,Position=screen});
            Check(Read<int>(battle,"_playerDeployments")==count+1,"Touch release deploys exactly once");
            battle._Input(new InputEventScreenTouch {Index=0,Pressed=false,Position=screen});
            Check(Read<int>(battle,"_playerDeployments")==count+1,"Duplicate release cannot deploy again");
            battle._UnhandledInput(new InputEventScreenTouch {Index=0,Pressed=true,Position=screen});
            battle._Input(new InputEventScreenTouch {Index=0,Pressed=false,Canceled=true,Position=screen});
            Check(Read<int>(battle,"_playerDeployments")==count+1 && !Read<bool>(battle,"_mobilePointerDown"),"OS-canceled touch never deploys or sticks");
            var previous=camera.Position;
            battle._UnhandledInput(new InputEventScreenTouch {Index=0,Pressed=true,Position=screen});
            battle._Input(new InputEventScreenDrag {Index=0,Position=screen+new Vector2(-100,0)});
            battle._Input(new InputEventScreenTouch {Index=0,Pressed=false,Position=screen+new Vector2(-100,0)});
            Check(camera.Position.X>previous.X+20,"Dragging pans the close view");
            Check(Read<int>(battle,"_playerDeployments")==count+1,"Dragging never deploys a unit");
            Call(battle,"ToggleMobileOverview");
            Check(Read<bool>(battle,"_mobileFollow") && !Read<bool>(battle,"_mobileOverview"),"Follow button restores automatic tracking");
            Call(battle,"ToggleMobileOverview");
            Check(battle.GetViewportRect().Size.X/camera.Zoom.X>=GameData.Combat.BattlefieldRight+GameData.Combat.BattlefieldLeft,
                "Overview fits the entire extended battlefield");
            Call(battle,"ToggleMobileOverview");
            Write(battle,"_mobileFollow",false); camera.Position=new Vector2(440,350); camera.ForceUpdateScroll();
            await Settle(90); Call(battle,"ClearArmedSelection"); await Settle();
            await Capture("phone-after-interaction");
            Call(battle,"TogglePause"); await Settle(); await Capture("phone-pause");
            Check(Read<CenterContainer>(battle,"_pauseOverlay").GetGlobalRect().End.Y<=battle.GetViewportRect().Size.Y+1,"Pause overlay fits phone height");
            var pausedCount=Read<int>(battle,"_playerDeployments");
            battle._UnhandledInput(new InputEventScreenTouch {Index=0,Pressed=true,Position=screen});
            battle._Input(new InputEventScreenTouch {Index=0,Pressed=false,Position=screen});
            Check(Read<int>(battle,"_playerDeployments")==pausedCount,"Paused touches cannot deploy");
            Call(battle,"TogglePause");
            GetWindow().Size=new Vector2I(667,375); await Settle(); await Capture("small-phone-after");
            Check(top.GetGlobalRect().End.X<=battle.GetViewportRect().Size.X+1,"Small-phone header fits after resize");
            Read<CenterContainer>(battle,"_endCenter").Show();
            var result=Read<PanelContainer>(battle,"_endPanel"); result.Show();
            Read<Label>(battle,"_endLabel").Text=string.Join("\n",Enumerable.Repeat("Victory · Full battle report remains scrollable.",25));
            await Settle(); await Capture("phone-results");
            Check(result.GetGlobalRect().Position.Y>=0 && result.GetGlobalRect().End.Y<=battle.GetViewportRect().Size.Y,"Results and actions fit small phone");
            Read<CenterContainer>(battle,"_endCenter").Hide();
            Read<CenterContainer>(battle,"_draftCenter").Show();
            var draft=Read<PanelContainer>(battle,"_draftPanel"); draft.Show();
            Read<Label>(battle,"_draftLabel").Text=string.Join("\n",Enumerable.Repeat("Checkpoint report: caravan, contact and tradeoff details.",15));
            foreach(var b in draft.FindChildren("*","Button",true,false).OfType<Button>())
                b.Text="Caravan reinforcement\nA long upgrade description must wrap and remain reachable.";
            await Settle(); await Capture("phone-checkpoint");
            Check(draft.GetGlobalRect().Position.Y>=0 && draft.GetGlobalRect().End.Y<=battle.GetViewportRect().Size.Y,"Long checkpoint report stays within phone height");
            var draftScroll=draft.FindChildren("*","ScrollContainer",true,false).OfType<ScrollContainer>().Single();
            draftScroll.ScrollVertical=10000; await Settle();
            Check(draftScroll.ScrollVertical>0,"Long checkpoint choices are scrollable, not cut off");
            battle.QueueFree(); await Settle();
            GameState.Instance.UnlockNextStage(59);
            foreach(var late in new[]{false,true})
            {
                var advanced=await Battle(true,60,late);
                var header=Read<PanelContainer>(advanced,"_topHudPanel");
                Check(header.GetGlobalRect().End.X<=advanced.GetViewportRect().Size.X+1,late?"Endless HUD fits":"Late campaign HUD fits");
                await Capture(late?"phone-endless":"phone-stage-60");
                advanced.QueueFree(); await Settle();
            }
            await CheckModelPreviews();
        }
        catch(Exception ex) {GD.PrintErr(ex.ToString()); _failures++;}
        finally { MobilePresentation.TestOverride=null; }
        GD.Print($"MOBILE_PRESENTATION_RESULT: {_failures} failures");
        GetTree().Quit(_failures==0?0:1);
    }

    private async Task CheckCloserViews(BattleController battle)
    {
        var camera=Read<Camera2D>(battle,"_mobileCamera");
        var start=camera.Position;
        var center=new Vector2(640,(Read<float>(battle,"_mobileFieldTop")+Read<float>(battle,"_mobileFieldBottom"))*.5f);
        var focus=(Vector2)Call(battle,"ScreenToBattle",center);
        Call(battle,"CycleMobileZoom");
        Check(Mathf.IsEqualApprox(camera.Zoom.X,3.4f),"Zoom control reaches the 3.4x close-up");
        Check(((Vector2)Call(battle,"ScreenToBattle",center)).DistanceTo(focus)<.01,"Zoom holds the visible focus point steady");
        var positions=battle.GetChildren().OfType<Unit>().Select(u=>u.Position).ToArray();
        var oldBottom=Read<float>(battle,"_mobileFieldBottom");
        Call(battle,"ToggleMobileClearView"); await Settle();
        Check(Read<bool>(battle,"_mobileClearView") && Read<float>(battle,"_mobileFieldBottom")>oldBottom+150,"Clear view opens more vertical space for models");
        var point=battle.GetCanvasTransform()*new Vector2(430,340);
        var deployments=Read<int>(battle,"_playerDeployments");
        battle._UnhandledInput(new InputEventScreenTouch {Index=0,Pressed=true,Position=point});
        battle._Input(new InputEventScreenTouch {Index=0,Pressed=false,Position=point});
        Check(Read<int>(battle,"_playerDeployments")==deployments,"View-only taps cannot spend courage or deploy hidden selections");
        Check(positions.SequenceEqual(battle.GetChildren().OfType<Unit>().Select(u=>u.Position)),"Zoom and clear view never rescale or move the simulation");
        await Capture("phone-clear-closeup");
        if(OS.GetCmdlineUserArgs().Contains("--capture-motion")) await CaptureCombat(battle,"closeup-frames");
        Call(battle,"ToggleMobileClearView");
        Check(!Read<bool>(battle,"_mobileClearView") && Mathf.IsEqualApprox(oldBottom,Read<float>(battle,"_mobileFieldBottom")),"Cards restore the regular deployment layout");
        Write(battle,"_mobileFollow",true);
        var close=camera.Position;
        Call(battle,"ToggleMobileOverview"); Call(battle,"ToggleMobileOverview");
        Check(Mathf.IsEqualApprox(camera.Zoom.X,3.4f) && camera.Position.DistanceTo(close)<.1,"Map returns to the chosen zoom and prior close view");
        Write(battle,"_mobileFollow",false);
        Call(battle,"CycleMobileZoom"); Check(Mathf.IsEqualApprox(camera.Zoom.X,2.2f),"Zoom cycles to the wider tactical close view");
        Call(battle,"CycleMobileZoom"); Check(Mathf.IsEqualApprox(camera.Zoom.X,2.8f),"Zoom cycles back to the larger default");
        camera.Position=start; camera.ForceUpdateScroll();
    }

    private async Task CheckModelPreviews()
    {
        foreach(var id in GameData.PlayerRosterIds.Concat(GameData.EnemyRosterIds).Distinct())
        {
            var source=UnitSpriteLoader.LoadOwnedPreview(id);
            Check(source!=null && source.FrameWidth==256 && source.FrameHeight==320 && source.Animations.Count==3,
                $"{id} has original-resolution idle, walk and attack previews");
            source?.Texture.Dispose();
        }
        GameState.Instance.SetSelectedStage(1); GameState.Instance.PrepareCampaignBattle();
        foreach(var mobile in new[]{true,false})
        {
            MobilePresentation.TestOverride=mobile;
            var loadout=GD.Load<PackedScene>("res://scenes/LoadoutMenu.tscn").Instantiate<LoadoutMenu>();
            AddChild(loadout); await Settle(10); await Capture(mobile?"phone-loadout":"desktop-loadout-at-phone-size");
            var previews=loadout.FindChildren("*","Control",true,false).OfType<UnitModelPreview>().ToArray();
            Check(previews.Length==GameState.Instance.GetActiveDeckUnits().Count,"Preparation displays an animated model for each squad member");
            if(mobile)
                foreach(var button in loadout.FindChildren("*","Button",true,false).OfType<Button>().Where(b=>b.IsVisibleInTree()))
                {
                    var rect=button.GetGlobalTransformWithCanvas()*new Rect2(Vector2.Zero,button.Size);
                    Check(rect.End.X<=1281 && rect.End.Y<=721 && button.Size.Y>=56,"Phone preparation controls fit and remain touch sized");
                }
            var preview=previews.First();
            preview._GuiInput(new InputEventMouseButton {ButtonIndex=MouseButton.Left,Pressed=true,Position=new Vector2(20,20)});
            preview._GuiInput(new InputEventMouseMotion {Position=new Vector2(60,20)});
            preview._GuiInput(new InputEventMouseButton {ButtonIndex=MouseButton.Left,Pressed=false,Position=new Vector2(60,20)});
            Check(preview.MouseFilter==Control.MouseFilterEnum.Pass && !loadout.GetChildren().OfType<ModelShowcase>().Any(),
                "Dragging a model passes through to the card scroller without opening inspection");
            preview.InspectRequested(); await Settle(6);
            var gallery=loadout.GetChildren().OfType<ModelShowcase>().Single();
            var model=gallery.FindChildren("*","Control",true,false).OfType<UnitModelPreview>().Single();
            Check(!loadout.GetChildren().OfType<Unit>().Any(),"Inspecting models spawns no combat units");
            Call(gallery,"Play",UnitAnimState.Attack); model.SetProcess(false); model._Process(.25);
            Check(model.Animation==UnitAnimState.Attack && model.GlobalFrame>=10 && model.GlobalFrame<=19,"Inspector plays the authored attack frames");
            await Capture(mobile?"phone-model-inspector":"desktop-model-inspector-at-phone-size");
            if(DisplayServer.GetName()!="headless") Check(model.ModelRect.Size.Y>150 && model.ModelRect.End.Y<=model.Size.Y,"Inspector gives the model substantial space without cropping it");
            var first=Read<int>(gallery,"_index"); Call(gallery,"Select",1);
            Check(Read<int>(gallery,"_index")!=first,"Inspector browses the active squad");
            GameState.Instance.SetReducedMotion(true); Call(gallery,"Play",UnitAnimState.Attack);
            var frame=model.GlobalFrame; model._Process(3);
            Check(model.GlobalFrame==frame,"Reduced motion shows a still authored pose");
            GameState.Instance.SetReducedMotion(false);
            gallery.QueueFree(); await Settle();
            loadout.QueueFree(); await Settle();
        }
        MobilePresentation.TestOverride=true;
        var shop=GD.Load<PackedScene>("res://scenes/ShopMenu.tscn").Instantiate<ShopMenu>();
        AddChild(shop); await Settle(20);
        var shopModel=shop.FindChildren("*","Control",true,false).OfType<UnitModelPreview>().Single();
        shopModel.InspectRequested(); await Settle();
        Check(shop.GetChildren().OfType<ModelShowcase>().Any(),"Armory opens the large model inspector");
        shop.QueueFree(); await Settle();
    }

    private async Task CaptureCombat(BattleController battle,string folder="combat-frames")
    {
        if(DisplayServer.GetName()=="headless") return;
        var camera=Read<Camera2D>(battle,"_mobileCamera");
        var start=camera.Position;
        camera.Position=new Vector2(395,Read<bool>(battle,"_mobileClearView")?345:380); camera.ForceUpdateScroll();
        var units=battle.GetChildren().OfType<Unit>().ToArray();
        var sword=units.Single(u=>u.DefinitionId=="player_brawler");
        var spear=units.Single(u=>u.DefinitionId=="player_spear");
        var archer=units.Single(u=>u.DefinitionId=="player_shooter");
        var walker=units.Where(u=>u.DefinitionId=="enemy_walker").OrderBy(u=>u.Position.X).First();
        var brute=units.Single(u=>u.DefinitionId=="enemy_brute");
        var pairs=new[]{(sword,walker),(spear,brute),(archer,walker),(walker,sword),(brute,spear)};
        var path=ProjectSettings.GlobalizePath($"res://artifacts/mobile-review/{folder}");
        System.IO.Directory.CreateDirectory(path);
        for(var frame=0;frame<90;frame++)
        {
            foreach(var unit in units) unit.TickAttackTimer(1f/30);
            foreach(var (a,b) in pairs)
            {
                if(a.TryBeginAttack(b)) Call(battle,"QueueUnitStrike",a,b);
                else if(!a.UsesProjectile && !a.IsAttackCommitted && !a.CanAttack(b)) a.MoveToward(b.Position,1f/30,200,1150,96,584);
            }
            foreach(var unit in units) unit._Process(1f/30);
            foreach(var projectile in battle.GetChildren().OfType<Projectile>().ToArray()) {projectile.SetPhysicsProcess(false); projectile._PhysicsProcess(1f/30);}
            foreach(var effect in battle.GetChildren().OfType<WeaponContactEffect>().ToArray()) {effect.SetProcess(false); effect._Process(1f/30);}
            battle.QueueRedraw();
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng($"{path}/{frame:000}.png");
        }
        camera.Position=start; camera.ForceUpdateScroll();
    }

    private void CheckParticleSizes()
    {
        var host=new Node2D { Position=new Vector2(-2000,-2000) }; AddChild(host);
        var p=Vector2.Zero; var color=Colors.Gold;
        var emitters=new[] {
            BattleParticles.SpawnImpactSparks(host,p,color,20), BattleParticles.SpawnDeployBurst(host,p,color),
            BattleParticles.SpawnDeathBurst(host,p,color,false), BattleParticles.SpawnDeathBurst(host,p,color,true),
            BattleParticles.SpawnProjectileTrail(host,color), BattleParticles.SpawnBaseHitDebris(host,p,color),
            BattleParticles.SpawnFireballParticles(host,p,color,100), BattleParticles.SpawnHealSparkles(host,p,color,100),
            BattleParticles.SpawnFrostParticles(host,p,color,100), BattleParticles.SpawnLightningParticles(host,p,color),
            BattleParticles.SpawnWardParticles(host,p,color,100), BattleParticles.SpawnStoneBarricadeParticles(host,p,color,100),
            BattleParticles.SpawnWarCryParticles(host,p,color,100), BattleParticles.SpawnEarthquakeParticles(host,p,color,100),
            BattleParticles.SpawnPolymorphParticles(host,p,color), BattleParticles.SpawnResurrectParticles(host,p,color),
            BattleParticles.SpawnBossSpawnBurst(host,p,color)
        };
        foreach(var emitter in emitters)
        {
            var pixels=emitter.Texture==null?1:Mathf.Max(emitter.Texture.GetWidth(),emitter.Texture.GetHeight());
            Check(pixels*emitter.ScaleAmountMax<=8.01f && pixels*emitter.ScaleAmountMin>=1.49f,
                "Particle texture is sized in world pixels, not giant texture multiples");
        }
        host.QueueFree();
    }
}
