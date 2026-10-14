using System;
using System.Linq;
using Godot;

public partial class ProgressionMenu : Control
{
    private VBoxContainer _root,_body;
    private int _tab;
    private string _unitId="";
    private string _notice="";
    public override void _Ready()
    {
        SetMeta("self_modal_layout",true);
        var embedded=RealmModal.Embedded(this);
        MedievalUi.Apply(this);_root=new VBoxContainer();AddChild(_root);_root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _root.AddThemeConstantOverride("separation",12);
        RealmUi.Tabs(_root,i=>{_tab=i;Refresh();},"Awakening","Collection","Wagon looks");
        _body=RealmUi.Scroll(_root);Refresh();
        if(!embedded)_root.AddChild(MapNavigation.Button(()=>SceneRouter.Instance.GoToMap()));
    }
    private void Refresh()
    {
        RealmUi.Clear(_body);
        var state=GameState.Instance;
        _body.AddChild(RealmUi.Label($"Gold {state.Gold:N0} · Essence {state.Essence} · Sigils {state.Sigils}",20,true));
        if(_notice.Length>0)_body.AddChild(RealmUi.Label(_notice,20));
        if(_tab==0)Awakening();else if(_tab==1)Collection();else Skins();
        RealmModal.Polish(_root);
    }
    private void Awakening()
    {
        var state=GameState.Instance;
        _body.AddChild(RealmUi.Label("Awakening shares the equipment stat limits. Tokens belong to one ally.",20));
        var choices=new OptionButton {CustomMinimumSize=new Vector2(0,56),SizeFlagsHorizontal=SizeFlags.ExpandFill};
        var units=state.GetOwnedPlayerUnits().ToArray();if(units.All(u=>u.Id!=_unitId))_unitId=units.First().Id;
        foreach(var unit in units)choices.AddItem(unit.DisplayName);
        choices.Select(Array.FindIndex(units,u=>u.Id==_unitId));choices.ItemSelected+=i=>{_unitId=units[(int)i].Id;Refresh();};_body.AddChild(choices);
        var preview=state.BuildAwakeningPreview(_unitId);
        _body.AddChild(RealmUi.SectionTitle($"{preview.Unit.DisplayName} · {preview.Stars}/5 stars"));
        _body.AddChild(RealmUi.Label($"Tokens: {state.GetUnitTokens(_unitId)} · Token training costs 100 gold + 2 Essence.",20));
        var train=RealmUi.Button("crystal","Train token",()=>{state.TryTrainAwakeningToken(_unitId,out _notice);Refresh();});
        train.Disabled=preview.Next==null || state.Gold<100 || state.Essence<2;_body.AddChild(train);
        if(preview.Next==null){_body.AddChild(RealmUi.Label("Maximum awakening reached.",20));return;}
        _body.AddChild(RealmUi.Label($"Next star costs {preview.Next.GoldCost} gold + {preview.Next.TokenCost} tokens.",20));
        foreach(var limit in preview.Limits)_body.AddChild(RealmUi.Label(RelicStatText.LimitNotice(limit),20));
        _body.AddChild(ArmoryDetailUi.CompareStats(preview.Current,preview.After,true));
        var awaken=RealmUi.Button("star","Awaken",()=>{state.TryAwakenUnit(_unitId,out _notice);Refresh();},true);
        awaken.Disabled=state.GetUnitTokens(_unitId)<preview.Next.TokenCost || state.Gold<preview.Next.GoldCost;_body.AddChild(awaken);
    }
    private void Collection()
    {
        var state=GameState.Instance;
        _body.AddChild(RealmUi.Label("Claim each milestone once. Core unit progress excludes retained legacy recruits; utility progress includes tactical gear.",20));
        foreach(var group in CollectionMilestoneCatalog.GetAll().GroupBy(m=>m.Category))
        {
            var progress=CollectionMilestoneCatalog.GetCollectionPercent(group.Key,state);
            _body.AddChild(RealmUi.SectionTitle($"{group.First().CategoryLabel} · {progress}%"));
            foreach(var milestone in group)
            {
                var claimed=state.HasClaimedCollectionMilestone(milestone.Id);
                var row=new HBoxContainer();row.AddThemeConstantOverride("separation",12);_body.AddChild(row);
                row.AddChild(RealmUi.Label($"{milestone.ThresholdPercent}% · {milestone.RewardLabel}",20));
                var button=RealmUi.Button("star",claimed?"Claimed":"Claim",()=>{state.TryClaimCollectionMilestone(milestone.Id,out _notice);Refresh();});
                button.AccessibilityName=$"Claim {milestone.Id}";button.Disabled=claimed || progress<milestone.ThresholdPercent;row.AddChild(button);
            }
        }
    }
    private void Skins()
    {
        var state=GameState.Instance;_body.AddChild(RealmUi.Label("Wagon looks change appearance only. They provide no combat bonuses.",20));
        foreach(var skin in WagonSkinCatalog.GetAll())
        {
            var unlocked=WagonSkinCatalog.IsSkinUnlocked(skin.Id,state);var selected=state.SelectedWagonSkinId==skin.Id;
            var panel=new PanelContainer();_body.AddChild(panel);var box=new VBoxContainer();panel.AddChild(box);
            box.AddChild(RealmUi.SectionTitle(skin.Title));
            var art=BattleStructureArt.Load(skin.Id==WagonSkinCatalog.DefaultSkinId?"war_wagon":"war_wagon_"+skin.Id)??BattleStructureArt.Load("war_wagon");
            if(art?.Texture!=null)box.AddChild(new TextureRect {Texture=art.Texture,CustomMinimumSize=new Vector2(0,140),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered});
            box.AddChild(RealmUi.Label(skin.Description+" · "+skin.UnlockCondition,20,true));
            var button=RealmUi.Button("shield",selected?"Selected":unlocked?"Use look":"Locked",()=>{state.SetWagonSkin(skin.Id);Refresh();});
            button.AccessibilityName=$"Use wagon {skin.Id}";button.Disabled=!unlocked || selected;box.AddChild(button);
        }
    }
}
