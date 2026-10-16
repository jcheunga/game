using Godot;

public partial class BossRushMenu : Control
{
    public override void _Ready()
    {
        SetMeta("self_modal_layout",true);
        MedievalUi.Apply(this);
        var root=new VBoxContainer();AddChild(root);root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);root.AddThemeConstantOverride("separation",12);
        var state=GameState.Instance;
        // The briefing and the commander list scroll together, so a phone shows the whole list; the start button stays below.
        var page=RealmUi.Scroll(root);page.AddThemeConstantOverride("separation",12);
        page.AddChild(RealmUi.Label($"Best: {state.BestBossRushWave}/14 commanders · Runs {state.BossRushRuns}",20));
        page.AddChild(RealmUi.Label("For a full clear, develop a level-20 melee core, recovery support, earned relics and the wagon's defensive upgrades.",20,true));
        page.AddChild(RealmUi.Label("Defeat commanders and their escorts. Enemy levels rise from 2 to 20 on the same shallow curve. Between rounds troops regroup, courage resets to 40, starting mana is restored and the wagon repairs fully. No food cost; rewards are banked at the end.",20));
        var row=new HBoxContainer();row.AddThemeConstantOverride("separation",12);page.AddChild(row);
        row.AddChild(RealmUi.Button("people","Edit warband",()=>SceneRouter.Instance.GoToShop(0)));
        row.AddChild(RealmUi.Button("flame","Edit utilities",()=>SceneRouter.Instance.GoToShop(1)));
        var body=new VBoxContainer();page.AddChild(body);
        for(var i=0;i<BossRushCatalog.Waves.Length;i++)
        {
            var wave=BossRushCatalog.Waves[i];body.AddChild(RealmUi.Label($"{i+1}. {wave.BossName} · Lv {GameData.GetStage(wave.SourceStage).EnemyLevel} · {wave.RewardGold} gold",20));
        }
        var play=RealmUi.Button("sword","Start boss rush",()=>{if(state.PrepareBossRush(out _))SceneRouter.Instance.GoToBattle();},true);
        play.Disabled=state.HighestUnlockedStage<=10 || state.GetActiveDeckUnits().Count==0;root.AddChild(play);RealmModal.Polish(root);
    }
}
