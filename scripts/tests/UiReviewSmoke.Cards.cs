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
        // Exercise every action state independently of the one-unit starter save.
        var fixture = GameState.Instance.BuildSaveData();
        fixture.OwnedPlayerUnitIds = GameData.PlayerRosterIds.ToArray(); fixture.OwnedPlayerSpellIds = GameData.PlayerSpellIds.ToArray();
        fixture.ActiveDeckUnitIds = GameData.PlayerRosterIds.Take(3).ToArray(); fixture.ActiveDeckSpellIds = GameData.PlayerSpellIds.Take(2).ToArray();
        // Mixed levels, including the widest tag (20), so the corner tag is checked at one and two digits.
        fixture.UnitLevels = new Dictionary<string,int>{[fixture.ActiveDeckUnitIds[0]]=UnitBalance.MaxPlayerLevel,[fixture.ActiveDeckUnitIds[1]]=7};
        fixture.SpellLevels = new Dictionary<string,int>{[fixture.ActiveDeckSpellIds[0]]=GameState.Instance.MaxSpellLevel};
        GameState.Instance.RestoreCloudSave(fixture);
        foreach(var mobile in new[]{false,true})
        {
            var prefix=mobile?"phone":"desktop";
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
            // Deck cards only; the caravan spell's card is reviewed on its own below.
            var cards=Walk(battle).OfType<BattleActionCard>().Where(c=>c.GetParent()?.GetParent() is not CaravanSpellButton).ToArray();
            var buttons=cards.Select(c=>(Button)c.GetParent()).ToArray();
            Write("_courage",100f); Write("_mana",Read<float>("_maxMana")); deck.ReduceCooldowns(1000); spells.ReduceCooldowns(1000); Call("UpdateHud");
            await Wait(.1);
            Check(cards.Length==deck.Roster.Count+spells.Roster.Count,$"{prefix}: every deck action has an icon card");
            for(var i=0;i<cards.Length;i++)
            {
                var art=cards[i]; var button=buttons[i];
                var cost=i<deck.Roster.Count?deck.Roster[i].Cost:GameState.Instance.BuildSpellStats(spells.Roster[i-deck.Roster.Count]).ManaCost;
                Check(art.CostLabel.Text==cost.ToString(),$"{prefix}: badge uses the actual resolved courage or mana cost");
                Check(art.CostPlate.Name==(i<deck.Roster.Count?"CourageCost":"ManaCost"),$"{prefix}: troops wear the courage badge, magic the mana badge");
                Check(art.Portrait.Texture!=null && art.Portrait.Size.Y>=art.Size.Y*.6f && new Rect2(Vector2.Zero,art.Size).Encloses(art.Portrait.GetRect()),$"{prefix}: portrait sits whole inside the card");
                Check(art.CostPlate.Position.X>art.Size.X*.5f && art.CostPlate.Position.Y<=6
                    && new Rect2(Vector2.Zero,art.Size).Encloses(new Rect2(art.CostPlate.Position,art.CostPlate.Size)),
                    $"{prefix}: textured cost badge fits the top-right corner");
                var level=i<deck.Roster.Count?GameState.Instance.GetUnitLevel(deck.Roster[i].Id):GameState.Instance.BuildSpellStats(spells.Roster[i-deck.Roster.Count]).Level;
                var tag=new Rect2(art.LevelPlate.Position,art.LevelPlate.Size);
                Check(art.LevelPlate.Visible && art.LevelLabel.Text==level.ToString(),$"{prefix}: level tag shows the unit or spell's actual level");
                Check(tag.Position.X<=8 && tag.End.Y>=art.Size.Y-8 && tag.Size.X<art.Size.X*.5f && new Rect2(Vector2.Zero,art.Size).Encloses(tag)
                    && !tag.Intersects(new Rect2(art.CostPlate.Position,art.CostPlate.Size)),$"{prefix}: level tag fits the bottom-left corner, clear of the cost badge");
                Check(art.LevelLabel.FittedSize()==art.LevelLabel.FontSize && art.LevelLabel.FontSize>=ReadingText.PhoneBody,
                    $"{prefix}: level tag number fits its plate at the phone text floor");
                Check(button.Text=="" && art.FindChildren("*","Label",true,false).All(label=>label==art.CostLabel)
                    && Walk(art).OfType<RoyalLabel>().All(label=>label==art.LevelLabel),$"{prefix}: ready cards show no name/action/state clutter");
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
            Check(buttons.All(b=>b.GetThemeStylebox("focus") is StyleBoxEmpty && b.GetThemeStylebox("hover")==b.GetThemeStylebox("normal")),
                $"{prefix}: a tapped card is left without a hover or focus ring");
            buttons[deck.Roster.Count].EmitSignal(BaseButton.SignalName.Pressed);
            Check(cards.Count(c=>c.Selected)==1 && cards[deck.Roster.Count].Selected,$"{prefix}: magic cards are still selected to aim");
            Send(new InputEventKey {Keycode=Key.Key1,Pressed=true});
            Send(new InputEventKey {Keycode=Key.Key1,Pressed=false});
            await Wait(.1);
            Check(Read<int>("_playerDeployments")==deployments+2 && Read<List<Unit>>("_units").Last().DefinitionId==deck.Roster[0].Id,
                $"{prefix}: number keys deploy their unit");
            Check(cards.Count(c=>c.Selected)==1 && cards[deck.Roster.Count].Selected,$"{prefix}: deploying a unit keeps the aimed magic selected");
            deck.ReduceCooldowns(1000);

            Write("_courage",0f); Write("_mana",0f); Call("UpdateHud"); await Wait(.1);
            Check(buttons.All(b=>b.Disabled) && cards.All(c=>c.Unaffordable && c.Portrait.Modulate.R<.9f),
                $"{prefix}: insufficient courage and mana is explicit and prevents activation");
            Check(cards.All(c=>c.CostLabel.IsVisibleInTree()),$"{prefix}: unavailable cards still show their price");
            await Capture(prefix+"-low-courage");

            // Courage and mana are separate purses: running out of one never blocks the other.
            Write("_mana",Read<float>("_maxMana")); Call("UpdateHud"); await Wait(.1);
            Check(buttons.Take(deck.Roster.Count).All(b=>b.Disabled) && buttons.Skip(deck.Roster.Count).All(b=>!b.Disabled),
                $"{prefix}: with no courage, magic is still paid for with mana");
            Write("_courage",100f); Write("_mana",0f); Call("UpdateHud"); await Wait(.1);
            Check(buttons.Take(deck.Roster.Count).All(b=>!b.Disabled) && buttons.Skip(deck.Roster.Count).All(b=>b.Disabled),
                $"{prefix}: with no mana, troops are still paid for with courage");

            Write("_courage",100f); Write("_mana",Read<float>("_maxMana"));
            var unitCooldown=(float)Call("ResolvePlayerDeployCooldown",deck.Roster[0]);
            deck.MarkDeployed(deck.Roster[0],unitCooldown*.5f);
            var spellStats=GameState.Instance.BuildSpellStats(spells.Roster[0]);
            var spellCooldown=(float)Call("ResolvePlayerSpellCooldown",spellStats);
            spells.MarkCast(spells.Roster[0],spellCooldown*.5f);
            Call("UpdateHud"); await Wait(.1);
            foreach(var art in new[]{cards[0],cards[deck.Roster.Count]})
            {
                Check(((Button)art.GetParent()).Disabled && Mathf.IsEqualApprox(art.CooldownRatio,.5f)
                    && art.FindChildren("*","Label",true,false).All(label=>label==art.CostLabel),$"{prefix}: unit/spell cooldown shows a proportional shade without a countdown");
                Check(art.CostLabel.IsVisibleInTree() && art.CostLabel.SelfModulate==Colors.White
                    && art.CostPlate.GetIndex()>art.CooldownShade.GetIndex() && art.LevelPlate.GetIndex()>art.CooldownShade.GetIndex(),
                    $"{prefix}: cooldown never obscures the cost badge or level tag");
                var split=(float)((ShaderMaterial)art.Portrait.Material).GetShaderParameter("split_y");
                Check(art.CooldownShade.Visible && split>0 && split<art.Portrait.Size.Y && art.Portrait.Modulate==Colors.White,
                    $"{prefix}: the art greys below the cooldown edge and keeps full colour above it");
            }
            await Capture(prefix+"-cooldown");
            deck.ReduceCooldowns(1000); spells.ReduceCooldowns(1000); Call("UpdateHud");
            Check(buttons.All(b=>!b.Disabled) && cards.All(c=>c.CooldownRatio==0 && !c.CooldownShade.Visible
                    && (float)((ShaderMaterial)c.Portrait.Material).GetShaderParameter("split_y")>c.Portrait.Size.Y),
                $"{prefix}: recovery returns cards to the clean icon-only state");
            // The caravan spell opens beside the dock on a full mana bar, and a tap on it casts.
            var caravan=Read<CaravanSpellButton>("_caravanSpellButton");
            var dock=Read<PanelContainer>("_cardDock");
            Write("_mana",Read<float>("_maxMana")); Call("UpdateHud"); await Wait(.7);
            var slot=caravan.GetGlobalRect(); var tray=dock.GetGlobalRect(); var view=GetViewport().GetVisibleRect();
            Check(caravan.IsOpen && caravan.Visible && !caravan.Card.Disabled,$"{prefix}: a full mana bar opens the caravan spell, ready to cast");
            Check(slot.Position.X>=tray.End.X && view.Encloses(slot) && Mathf.Abs(slot.End.Y-tray.End.Y)<2 && Mathf.Abs(slot.Size.Y-tray.Size.Y)<2,
                $"{prefix}: the caravan slot stands right of the dock on its base line, as tall as it and inside the screen");
            Check((caravan.Card.Size-((Button)cards[0].GetParent()).Size).Length()<1,
                $"{prefix}: the caravan card is the same size as a unit card");
            Check(!string.IsNullOrWhiteSpace(caravan.Card.AccessibilityName) && caravan.Art.Portrait.Texture!=null && !caravan.Art.CostPlate.Visible
                    && !caravan.Art.LevelPlate.Visible,
                $"{prefix}: the caravan card shows its painted lantern and can be identified");
            await Capture(prefix+"-caravan-ready");
            var tap=slot.GetCenter();
            Send(new InputEventMouseMotion {Position=tap,GlobalPosition=tap});
            Send(new InputEventMouseButton {ButtonIndex=MouseButton.Left,Pressed=true,Position=tap,GlobalPosition=tap});
            Send(new InputEventMouseButton {ButtonIndex=MouseButton.Left,Pressed=false,Position=tap,GlobalPosition=tap});
            await Wait(.1);
            Check(Read<int>("_caravanCasts")==1 && Read<float>("_mana")<.01f,$"{prefix}: tapping the caravan slot casts it and spends the bar");
            Call("UpdateHud"); await Wait(.5);
            Check(!caravan.IsOpen,$"{prefix}: the caravan slot closes once the bar is spent");

            Write("_endlessCheckpointActive",true); Call("UpdateHud");
            Check(buttons.All(b=>b.Disabled),$"{prefix}: checkpoint blocking is preserved");
            Write("_endlessCheckpointActive",false); Write("_battleEnded",true); Call("UpdateHud");
            Check(buttons.All(b=>b.Disabled),$"{prefix}: battle-end blocking is preserved");
        }
        await LiveUiReview.StopAudio(this);
        GD.Print($"DEPLOYMENT_CARD_RESULT: {_failures} failures");
        QuitAfterAudio(_failures==0?0:1);
    }
}
