using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

public partial class UiReviewSmoke
{
    private async Task ReviewDeploymentCards()
    {
        _output=ProjectSettings.GlobalizePath("res://artifacts/deployment-card-review");
        System.IO.Directory.CreateDirectory(_output);
        const BindingFlags hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        try
        {
            // Exercise every action state independently of the one-unit starter save.
            var fixture = GameState.Instance.BuildSaveData();
            fixture.OwnedPlayerUnitIds = GameData.PlayerRosterIds.ToArray(); fixture.OwnedPlayerSpellIds = GameData.PlayerSpellIds.ToArray();
            fixture.ActiveDeckUnitIds = GameData.PlayerRosterIds.Take(3).ToArray(); fixture.ActiveDeckSpellIds = GameData.PlayerSpellIds.Take(2).ToArray();
            GameState.Instance.RestoreCloudSave(fixture);
            foreach(var mobile in new[]{false,true})
            {
                var prefix=mobile?"phone":"desktop";
                MobilePresentation.TestOverride=mobile;
                GetWindow().Size=mobile?new Vector2I(844,390):new Vector2I(1280,720);
                GameState.Instance.PrepareCampaignBattle();
                await Open("Battle");
                var battle=(BattleController)GetTree().CurrentScene;
                battle.SetPhysicsProcess(false);
                T Read<T>(string name)=>(T)typeof(BattleController).GetField(name,hidden)!.GetValue(battle)!;
                void Write(string name,object value)=>typeof(BattleController).GetField(name,hidden)!.SetValue(battle,value);
                object Call(string name,params object[] args)=>typeof(BattleController)
                    .GetMethod(name,hidden,null,args.Select(a=>a.GetType()).ToArray(),null)!.Invoke(battle,args);
                if(Read<bool>("_battlePaused")) Call("TogglePause");
                var deck=Read<BattleDeckState>("_deck");
                var spells=Read<BattleSpellState>("_spellDeck");
                var cards=Walk(battle).OfType<BattleActionCard>().ToArray();
                var buttons=cards.Select(c=>(Button)c.GetParent()).ToArray();
                Write("_courage",100f); deck.ReduceCooldowns(1000); spells.ReduceCooldowns(1000); Call("UpdateHud");
                await Wait(.1);
                Check(cards.Length==deck.Roster.Count+spells.Roster.Count,$"{prefix}: every deck action has an icon card");
                for(var i=0;i<cards.Length;i++)
                {
                    var art=cards[i]; var button=buttons[i];
                    var cost=i<deck.Roster.Count?deck.Roster[i].Cost:GameState.Instance.BuildSpellStats(spells.Roster[i-deck.Roster.Count]).CourageCost;
                    Check(art.CostLabel.Text==cost.ToString(),$"{prefix}: badge uses the actual resolved courage cost");
                    Check(art.Portrait.Texture!=null && art.Portrait.Size.Y>=art.Size.Y*.6f && new Rect2(Vector2.Zero,art.Size).Encloses(art.Portrait.GetRect()),$"{prefix}: portrait sits whole inside the card");
                    Check(art.CostPlate.Position.X>art.Size.X*.5f && art.CostPlate.Position.Y<=6
                        && new Rect2(Vector2.Zero,art.Size).Encloses(new Rect2(art.CostPlate.Position,art.CostPlate.Size)),
                        $"{prefix}: textured cost badge fits the top-right corner");
                    Check(button.Text=="" && art.StatusLabel.Text=="",$"{prefix}: ready cards show no name/action/state clutter");
                    Check(!string.IsNullOrWhiteSpace(button.AccessibilityName) && string.IsNullOrWhiteSpace(button.TooltipText),
                        $"{prefix}: unit/spell identification remains available");
                    Check(Walk(art).OfType<Control>().All(c=>c.MouseFilter==Control.MouseFilterEnum.Ignore),
                        $"{prefix}: portrait, badge and status pass taps through to the button");
                }
                await Capture(prefix+"-ready");

                // Exercise actual GUI hit testing on the new badge, not just a signal.
                var target=cards[1];
                var click=target.CostPlate.GetGlobalTransformWithCanvas()*(target.CostPlate.Size*.5f);
                var deployments=Read<int>("_playerDeployments");
                Send(new InputEventMouseMotion {Position=click,GlobalPosition=click});
                Send(new InputEventMouseButton {ButtonIndex=MouseButton.Left,Pressed=true,Position=click,GlobalPosition=click});
                Send(new InputEventMouseButton {ButtonIndex=MouseButton.Left,Pressed=false,Position=click,GlobalPosition=click});
                await Wait(.1);
                Check(Read<int>("_playerDeployments")==deployments+1 && Read<List<Unit>>("_units").Last().DefinitionId==deck.Roster[1].Id
                    && !target.Selected,$"{prefix}: tapping the corner badge deploys its unit from the wagon");
                buttons[deck.Roster.Count].EmitSignal(BaseButton.SignalName.Pressed);
                Check(cards.Count(c=>c.Selected)==1 && cards[deck.Roster.Count].Selected,$"{prefix}: magic cards are still selected to aim");
                Send(new InputEventKey {Keycode=Key.Key1,Pressed=true});
                Send(new InputEventKey {Keycode=Key.Key1,Pressed=false});
                await Wait(.1);
                Check(Read<int>("_playerDeployments")==deployments+2 && Read<List<Unit>>("_units").Last().DefinitionId==deck.Roster[0].Id,
                    $"{prefix}: number keys deploy their unit");
                Check(cards.Count(c=>c.Selected)==1 && cards[deck.Roster.Count].Selected,$"{prefix}: deploying a unit keeps the aimed magic selected");
                deck.ReduceCooldowns(1000);

                Write("_courage",0f); Call("UpdateHud"); await Wait(.1);
                Check(buttons.All(b=>b.Disabled) && cards.All(c=>c.Unaffordable && c.Portrait.Modulate.R<.9f),
                    $"{prefix}: insufficient courage is explicit and prevents activation");
                Check(cards.All(c=>c.CostLabel.IsVisibleInTree()),$"{prefix}: unavailable cards still show their price");
                await Capture(prefix+"-low-courage");

                Write("_courage",100f);
                var unitCooldown=(float)Call("ResolvePlayerDeployCooldown",deck.Roster[0]);
                deck.MarkDeployed(deck.Roster[0],unitCooldown*.5f);
                var spellStats=GameState.Instance.BuildSpellStats(spells.Roster[0]);
                var spellCooldown=(float)Call("ResolvePlayerSpellCooldown",spellStats);
                spells.MarkCast(spells.Roster[0],spellCooldown*.5f);
                Call("UpdateHud"); await Wait(.1);
                foreach(var art in new[]{cards[0],cards[deck.Roster.Count]})
                {
                    Check(((Button)art.GetParent()).Disabled && Mathf.IsEqualApprox(art.CooldownRatio,.5f)
                        && art.StatusLabel.Text.EndsWith("s"),$"{prefix}: unit/spell cooldown keeps its timer and proportional shade");
                    Check(art.CostLabel.IsVisibleInTree() && art.CostLabel.SelfModulate==Colors.White
                        && art.CostPlate.GetIndex()>art.GetChildren().OfType<ColorRect>().Single().GetIndex(),
                        $"{prefix}: cooldown never obscures the cost badge");
                }
                await Capture(prefix+"-cooldown");
                deck.ReduceCooldowns(1000); spells.ReduceCooldowns(1000); Call("UpdateHud");
                Check(buttons.All(b=>!b.Disabled) && cards.All(c=>c.StatusLabel.Text=="" && c.CooldownRatio==0),
                    $"{prefix}: recovery returns cards to the clean icon-only state");
                Write("_endlessCheckpointActive",true); Call("UpdateHud");
                Check(buttons.All(b=>b.Disabled),$"{prefix}: checkpoint blocking is preserved");
                Write("_endlessCheckpointActive",false); Write("_battleEnded",true); Call("UpdateHud");
                Check(buttons.All(b=>b.Disabled),$"{prefix}: battle-end blocking is preserved");
            }
        }
        finally { MobilePresentation.TestOverride=null; }
        await LiveUiReview.StopAudio(this);
        GD.Print($"DEPLOYMENT_CARD_RESULT: {_failures} failures");
        QuitAfterAudio(_failures==0?0:1);
    }
}
