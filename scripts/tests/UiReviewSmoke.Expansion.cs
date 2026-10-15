using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

public partial class UiReviewSmoke
{
    private async Task ReviewExpansion()
    {
        foreach(var phone in new[]{false,true})
        {
            GetWindow().Size=phone?new Vector2I(844,390):new Vector2I(1280,720);
            _output=ProjectSettings.GlobalizePath("res://artifacts/expansion/"+(phone?"phone":"desktop"));System.IO.Directory.CreateDirectory(_output);
            var state=GameState.Instance;state.ResetProgress();state.SetShowHints(false);state.SetAnalyticsConsent(false);
            var fixture=state.BuildSaveData();fixture.Gold=10000;fixture.Essence=100;fixture.HighestUnlockedStage=100;
            fixture.OwnedPlayerUnitIds=GameData.PlayerRosterIds;fixture.UnitLevels=GameData.PlayerRosterIds.ToDictionary(id=>id,_=>20);
            fixture.ActiveDeckUnitIds=new[]{"player_peasant","player_brawler","player_spear"};fixture.StageStars=new int[100];fixture.StageStars[99]=1;
            typeof(GameState).GetMethod("ApplySavedData",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(state,new object[]{fixture});
            await Open("MainMenu");var menu=(MapMenu)GetTree().CurrentScene;var map=Walk(menu).OfType<MapPathCanvas>().Single().MapOffset;var food=state.Food;
            async Task Tap(string name)
            {
                var button=Walk(menu).OfType<Button>().Last(b=>b.IsVisibleInTree() && !b.Disabled && (b.AccessibilityName==name || b.Text==name || b.TooltipText==name));
                if(button.ToggleMode)button.ButtonPressed=true;
                await TapModal(button);await Wait(.15);
            }
            SceneRouter.Instance.GoToProgression();await Wait(.3);
            Check(menu.HomeModalDestination==SceneRouter.ProgressionScene,"Progression opens over the existing map");
            AuditText("Expansion / awakening");await Capture("01-awakening");
            var gold=state.Gold;var essence=state.Essence;await Tap("Train token");
            Check(state.GetUnitTokens("player_peasant")==1 && state.Gold==gold-100 && state.Essence==essence-2,"Native token training pays the advertised resources");
            await Tap("Awaken");Check(state.GetUnitStarLevel("player_peasant")==1,"Native awakening applies the new star level");
            await Tap("Collection");AuditText("Expansion / collection");await Capture("02-collection");
            gold=state.Gold;await Tap("Claim units_25");Check(state.Gold==gold+200 && state.HasClaimedCollectionMilestone("units_25"),"Native collection claim grants its reward once");
            await Tap("Wagon looks");AuditText("Expansion / wagon looks");await Capture("03-wagon-looks");
            await Tap("Use wagon skin_royal");Check(state.SelectedWagonSkinId=="skin_royal","Native cosmetic selection equips the unlocked look");
            Check(state.Food==food && Walk(menu).OfType<MapPathCanvas>().Single().MapOffset==map,"Progression actions preserve the map and travel supplies");
            menu.CloseHomeModal();SceneRouter.Instance.GoToBossRush();await Wait(.3);
            AuditText("Expansion / boss rush");await Capture("04-boss-rush");
            Check(Walk(menu).OfType<Label>().Count(l=>l.Text.Contains(" gold") && l.Text.Contains(" · Lv "))==14,"Boss rush menu lists every commander with level and reward");
            await Tap("Start boss rush");await Wait(.5);
            var battle=(BattleController)GetTree().CurrentScene;battle.SetPhysicsProcess(false);
            Check(state.CurrentBattleMode==BattleRunMode.BossRush,"Native boss-rush launch enters its dedicated battle mode");
            var hidden=BindingFlags.Instance|BindingFlags.NonPublic;
            void Call(string name,params object[] args)=>typeof(BattleController).GetMethod(name,hidden).Invoke(battle,args);
            typeof(BattleController).GetField("_elapsed",hidden).SetValue(battle,3f);Call("TickBossRush",.1f);
            var caster=(Unit)typeof(BattleController).GetMethod("SpawnUnit",hidden).Invoke(battle,new object[]{Team.Enemy,new UnitStats(GameData.GetUnit("enemy_cinder_spitter")),new Vector2(700,340)});
            caster.GiveShield(5,3);caster.ApplyArmorBreak(5,3);caster.ApplyControl(1);caster.Silence(1);
            var rogue=(Unit)typeof(BattleController).GetMethod("SpawnUnit",hidden).Invoke(battle,new object[]{Team.Player,new UnitStats(GameData.GetUnit("player_rogue")),new Vector2(620,340)});
            Call("BeginContentCast",rogue,2f,(Action)(()=>{}),true);
            var camera=(Camera2D)typeof(BattleController).GetField("_battleCamera",hidden).GetValue(battle);
            Call("SetBattleCameraFollow",false);
            camera.Position=new Vector2(650,camera.Position.Y);Call("ClampBattleCamera",camera);camera.ForceUpdateScroll();
            await Capture("05-battle-feedback");
            Check(Walk(battle).OfType<BattleContentWarning>().Any() && caster.ContentStatusLabels.Count>=3,"Live battle renders cast warnings and multiple status badges");
            Call("TogglePause");await Wait(.2);
            var guide=Walk(battle).OfType<Button>().Single(b=>b.Text=="Battle guide");await TapModal(guide);await Wait(.2);
            Check(Walk(battle).OfType<Label>().Any(l=>l.Text.Contains("CRIT: a critical hit")),"Battle guide explains status labels and critical feedback");
            await Capture("06-battle-guide");
            Walk(battle).OfType<Button>().Last(b=>b.AccessibilityName=="Close details").EmitSignal(BaseButton.SignalName.Pressed);await Wait(.1);
            Walk(battle).OfType<Button>().Single(b=>b.Text=="Quit battle").EmitSignal(BaseButton.SignalName.Pressed);await Wait(.5);
        }
        GD.Print($"EXPANSION_UI_RESULT: {_failures} failures");QuitAfterAudio(_failures==0?0:1);
    }
}
