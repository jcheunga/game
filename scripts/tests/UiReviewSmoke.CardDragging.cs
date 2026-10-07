using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

public partial class UiReviewSmoke
{
    private async Task ReviewCardDragging()
    {
        _output=ProjectSettings.GlobalizePath("res://artifacts/card-drag-review");
        System.IO.Directory.CreateDirectory(_output);
        const BindingFlags hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        try
        {
            var fixture = GameState.Instance.BuildSaveData();
            fixture.OwnedPlayerUnitIds = GameData.PlayerRosterIds.ToArray(); fixture.OwnedPlayerSpellIds = GameData.PlayerSpellIds.ToArray();
            fixture.ActiveDeckUnitIds = GameData.PlayerRosterIds.Take(3).ToArray(); fixture.ActiveDeckSpellIds = GameData.PlayerSpellIds.Take(2).ToArray();
            GameState.Instance.RestoreCloudSave(fixture);
            foreach(var touch in new[]{false,true})
            {
                var mode=touch?"phone-touch":"desktop-mouse";
                MobilePresentation.TestOverride=touch;
                GetWindow().Size=touch?new Vector2I(844,390):new Vector2I(1280,720);
                GameState.Instance.PrepareCampaignBattle(); await Open("Battle");
                var battle=(BattleController)GetTree().CurrentScene; battle.SetPhysicsProcess(false);
                T Read<T>(string name)=>(T)typeof(BattleController).GetField(name,hidden)!.GetValue(battle)!;
                void Write(string name,object value)=>typeof(BattleController).GetField(name,hidden)!.SetValue(battle,value);
                object Call(string name,params object[] args)=>typeof(BattleController)
                    .GetMethod(name,hidden,null,args.Select(a=>a.GetType()).ToArray(),null)!.Invoke(battle,args);
                if(Read<bool>("_battlePaused")) Call("TogglePause");
                var deck=Read<BattleDeckState>("_deck"); var spells=Read<BattleSpellState>("_spellDeck");
                var cards=Walk(battle).OfType<BattleActionCard>().ToArray();
                var camera=Read<Camera2D>(touch?"_mobileCamera":"_battleCamera");
                if(touch) { Write("_mobileFollow",false); camera.Position=new Vector2(340,380); camera.ForceUpdateScroll(); }
                void Ready() { Write("_courage",100f); deck.ReduceCooldowns(1000); spells.ReduceCooldowns(1000); Call("UpdateHud"); }
                Vector2 CardPoint(int i)=>cards[i].CostPlate.GetGlobalTransformWithCanvas()*(cards[i].CostPlate.Size*.5f);
                Vector2 FieldPoint()=>battle.GetGlobalTransformWithCanvas()*new Vector2(380,360);
                void Down(Vector2 p)
                {
                    if(touch) Send(new InputEventScreenTouch {Index=7,Pressed=true,Position=p});
                    else { Send(new InputEventMouseMotion {Position=p,GlobalPosition=p}); Send(new InputEventMouseButton {ButtonIndex=MouseButton.Left,Pressed=true,ButtonMask=MouseButtonMask.Left,Position=p,GlobalPosition=p}); }
                }
                void Move(Vector2 p)
                {
                    if(touch) Send(new InputEventScreenDrag {Index=7,Position=p});
                    else Send(new InputEventMouseMotion {Position=p,GlobalPosition=p,ButtonMask=MouseButtonMask.Left});
                }
                void Up(Vector2 p,bool cancel=false)
                {
                    if(touch) Send(new InputEventScreenTouch {Index=7,Pressed=false,Canceled=cancel,Position=p});
                    else Send(new InputEventMouseButton {ButtonIndex=MouseButton.Left,Pressed=false,Canceled=cancel,Position=p,GlobalPosition=p});
                }
                // Magic is aimed by dragging onto the field; a unit card is a button that deploys on release.
                void Start(int index=0) { Ready(); Down(CardPoint(index)); Move(FieldPoint()); }
                void Press(int index=0) { Ready(); Down(CardPoint(index)); }
                void CancelHeldCard() => Send(new InputEventKey {Keycode=Key.Escape,Pressed=true});
                Ready(); await Wait(.1);
                var point=FieldPoint(); var source=CardPoint(0);
                var count=Read<int>("_playerDeployments"); var casts=Read<int>("_spellsCast");
                Down(source);
                Check(Read<bool>("_cardPointerDown") && !Read<bool>("_cardDragging"),mode+": press captures the card without deploying");
                Move(source+new Vector2(2,-2));
                Check(!Read<bool>("_cardDragging") && Read<int>("_playerDeployments")==count,mode+": small finger jitter cannot deploy");
                var cameraPosition=camera.Position;
                Move(point); await Wait(.05);
                Check(!Read<bool>("_cardDragging") && !Read<Control>("_cardDragGhost").Visible,mode+": a unit card cannot be dragged to choose a position");
                Check(camera.Position==cameraPosition,mode+": holding a card does not pan the battlefield");
                Up(point); await Wait(.05);
                Check(Read<int>("_playerDeployments")==count && Mathf.IsEqualApprox(Read<float>("_courage"),100),mode+": lifting off a unit card cancels for free");
                var spawn=(Vector2)typeof(BattleController).GetMethod("get_WagonDoorExit",hidden)!.Invoke(battle,null)!;
                Ready(); Down(source); Move(source+new Vector2(2,-2)); Up(source+new Vector2(2,-2)); await Wait(.05);
                var unit=Read<List<Unit>>("_units").Last(u=>u.Team==Team.Player);
                Check(Read<int>("_playerDeployments")==count+1 && unit.DefinitionId==deck.Roster[0].Id,mode+": tapping a unit card deploys it exactly once");
                Check(unit.Position.DistanceTo(spawn)<.01,mode+": the unit steps out of the wagon's door");
                Check(Mathf.IsEqualApprox(Read<float>("_courage"),100-deck.Roster[0].Cost) && deck.GetCooldownRemaining(deck.Roster[0].Id)>0,
                    mode+": deployment charges the existing cost and starts cooldown");
                await Capture(mode+"-unit-deployed");
                Up(source);
                Check(Read<int>("_playerDeployments")==count+1,mode+": duplicate release cannot deploy again");

                Ready();
                var target=(Unit)Call("SpawnUnit",Team.Enemy,new UnitStats(GameData.GetUnit("enemy_brute"),healthScale:20),new Vector2(380,360));
                target.SetProcess(false); var health=target.Health;
                Down(CardPoint(deck.Roster.Count)); Move(point); await Wait(.05);
                Check(Read<bool>("_cardDragging") && Read<SpellDefinition>("_dragSpell")==spells.Roster[0],mode+": magic card has its own drag payload");
                await Capture(mode+"-magic-preview");
                Up(point); await Wait(.05);
                var cost=GameState.Instance.BuildSpellStats(spells.Roster[0]).CourageCost;
                Check(Read<int>("_spellsCast")==casts+1 && Mathf.IsEqualApprox(Read<float>("_courage"),100-cost),mode+": magic release casts once and charges its resolved cost");
                Check(target.Health<health && spells.GetCooldownRemaining(spells.Roster[0].Id)>0,mode+": magic hits the drop location and starts cooldown");
                Up(point);
                Check(Read<int>("_spellsCast")==casts+1,mode+": duplicate magic release cannot cast twice");

                for(var i=1;i<deck.Roster.Count;i++)
                {
                    var before=Read<int>("_playerDeployments");
                    Press(i); Up(CardPoint(i));
                    Check(Read<int>("_playerDeployments")==before+1
                        && Read<List<Unit>>("_units").Last(u=>u.Team==Team.Player).DefinitionId==deck.Roster[i].Id,
                        mode+": each unit card deploys its own unit, not the previously armed one");
                }
                var heal=spells.Roster.First(s=>s.EffectType=="heal");
                var healIndex=spells.Roster.ToList().IndexOf(heal);
                var ally=(Unit)Call("SpawnUnit",Team.Player,new UnitStats(deck.Roster[0]),new Vector2(380,360));
                ally.SetProcess(false); ally.TakeDamage(ally.MaxHealth*.5f); var injuredHealth=ally.Health;
                var castsBeforeHeal=Read<int>("_spellsCast");
                Start(deck.Roster.Count+healIndex); Up(point);
                Check(Read<int>("_spellsCast")==castsBeforeHeal+1 && ally.Health>injuredHealth,
                    mode+": healing magic reaches allies at the drop location");

                count=Read<int>("_playerDeployments"); casts=Read<int>("_spellsCast");
                Start(deck.Roster.Count); Move(CardPoint(0)); Up(CardPoint(0));
                Check(Read<int>("_playerDeployments")==count && Read<int>("_spellsCast")==casts && Read<float>("_courage")==100,
                    mode+": returning a magic card to the bar cancels for free");
                foreach(var spell in new[]{false,true})
                {
                    var index=spell?deck.Roster.Count:0;
                    Start(index); Move(new Vector2(30,35)); Up(new Vector2(30,35));
                    Check(Read<int>("_playerDeployments")==count && Read<int>("_spellsCast")==casts && Read<float>("_courage")==100,
                        mode+": releasing over the HUD never uses the card");
                    Press(index); Up(CardPoint(index),true);
                    Check(!Read<bool>("_cardPointerDown") && Read<float>("_courage")==100,mode+": OS-canceled unit/magic release is free");
                    Start(index); Move(new Vector2(-20,200)); Up(new Vector2(-20,200));
                    Check(Read<int>("_playerDeployments")==count && Read<int>("_spellsCast")==casts,mode+": outside-window release is rejected");
                    if(spell) { Start(index); Write("_courage",0f); Up(point); }
                    else { Press(index); Write("_courage",0f); Up(CardPoint(index)); }
                    Check(Read<int>("_playerDeployments")==count && Read<int>("_spellsCast")==casts && Read<float>("_courage")==0,
                        mode+": release rechecks courage instead of trusting the press");
                }
                Press(); deck.MarkDeployed(deck.Roster[0],5); Up(CardPoint(0));
                Check(Read<int>("_playerDeployments")==count,mode+": release rechecks a newly active cooldown");
                Start(deck.Roster.Count); spells.MarkCast(spells.Roster[0],5); Up(point);
                Check(Read<int>("_spellsCast")==casts,mode+": magic release rechecks a newly active cooldown");
                Ready(); Write("_courage",0f); Call("UpdateHud"); Down(CardPoint(0)); Up(CardPoint(0));
                Check(!Read<bool>("_cardPointerDown") && Read<int>("_playerDeployments")==count,mode+": unavailable cards cannot be pressed");

                Press(); Send(new InputEventKey {Keycode=Key.Escape,Pressed=true}); Up(CardPoint(0));
                Check(!Read<bool>("_battlePaused") && !Read<bool>("_cardPointerDown") && Read<int>("_playerDeployments")==count,
                    mode+": Escape cancels a held card without pausing or deploying");
                Press(); battle._Notification((int)Node.NotificationApplicationFocusOut); Up(CardPoint(0));
                Check(!Read<bool>("_cardPointerDown") && Read<int>("_playerDeployments")==count,mode+": focus loss cancels and consumes the late release");
                Press(); Call("TogglePause"); Up(CardPoint(0)); Call("TogglePause");
                Check(Read<int>("_playerDeployments")==count && !Read<bool>("_cardPointerDown"),mode+": pause cancels a held card");
                Start(deck.Roster.Count); Write("_endlessCheckpointActive",true); Call("UpdateCardDragPreview"); Up(point); Write("_endlessCheckpointActive",false);
                Check(Read<int>("_spellsCast")==casts && !Read<bool>("_cardPointerDown"),mode+": checkpoint interrupts without spending");
                Start(deck.Roster.Count); Write("_battleEnded",true); Call("UpdateCardDragPreview"); Up(point); Write("_battleEnded",false);
                Check(Read<int>("_spellsCast")==casts && !Read<bool>("_cardPointerDown"),mode+": battle end interrupts without spending");
                Press(); GetWindow().Size=touch?new Vector2I(667,375):new Vector2I(1200,720); await Wait(.15); Up(CardPoint(0));
                Check(!Read<bool>("_cardPointerDown") && Read<int>("_playerDeployments")==count,mode+": resizing cancels stale card presses");

                if(touch)
                {
                    Press(); point=FieldPoint();
                    Send(new InputEventScreenTouch {Index=8,Pressed=true,Position=point});
                    Send(new InputEventScreenDrag {Index=8,Position=point+new Vector2(70,0)});
                    Send(new InputEventScreenTouch {Index=8,Pressed=false,Position=point});
                    Check(Read<bool>("_cardPointerDown") && Read<int>("_playerDeployments")==count,mode+": a second finger cannot release or deploy the first card");
                    Up(CardPoint(0)); count++;
                    Check(Read<int>("_playerDeployments")==count,mode+": the first finger's release deploys once");
                    Send(new InputEventMouseButton {ButtonIndex=MouseButton.Left,Pressed=true,Position=point,GlobalPosition=point,Device=-1});
                    Send(new InputEventMouseButton {ButtonIndex=MouseButton.Left,Pressed=false,Position=point,GlobalPosition=point,Device=-1});
                    Check(Read<int>("_playerDeployments")==count,mode+": emulated mouse events cannot duplicate a touch drop");

                    // The card bar never scrolls: cards shrink to share the dock, so every card stays on screen.
                    Ready();
                    var view=battle.GetViewportRect();
                    Check(cards.All(art=>view.Grow(1).Encloses(((Control)art.GetParent()).GetGlobalRect())),mode+": every card fits on screen without scrolling");
                    source=CardPoint(0); Down(source); Move(source+new Vector2(-150,2));
                    Check(!Read<bool>("_cardScrolling"),mode+": horizontal swipes do not scroll the card bar");
                    Up(source+new Vector2(-150,2));
                    Check(Read<int>("_playerDeployments")==count,mode+": a swipe off a card cannot deploy");
                    await Wait(.1);
                }

                Ready();
                var bar=cards[0].GetParent().GetParent().GetParent() as ScrollContainer;
                bar?.EnsureControlVisible((Control)cards[deck.Roster.Count].GetParent()); await Wait(.1);
                point=FieldPoint(); Down(CardPoint(deck.Roster.Count)); Move(point);
                Check(Read<bool>("_cardDragging") && !Read<bool>("_cardScrolling"),
                    mode+": lifting a spell out of the card bar is a magic drag");
                if(Read<bool>("_cardPointerDown")) CancelHeldCard();
                if(bar!=null) bar.ScrollHorizontal=0;
                await Wait(.1);
                Ready(); Down(CardPoint(0)); Up(CardPoint(0)); await Wait(.05);
                unit=Read<List<Unit>>("_units").Last(u=>u.Team==Team.Player);
                Check(Read<int>("_playerDeployments")==count+1 && unit.Position.DistanceTo(spawn)<.01,
                    mode+": a tapped unit deploys at the wagon");
            }
        }
        finally { MobilePresentation.TestOverride=null; GetTree().Paused=false; }
        GD.Print($"CARD_DRAG_RESULT: {_failures} failures"); QuitAfterAudio(_failures==0?0:1);
    }
}
