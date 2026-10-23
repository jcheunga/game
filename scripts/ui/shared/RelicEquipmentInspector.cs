using System;
using Godot;

/// <summary>Preview both relic slots using battle stats before a deliberate equipment change.</summary>
public partial class RelicEquipmentInspector : CanvasLayer
{
    private EquipmentDefinition _relic;
    private Action _changed;
    private RealmModal _modal;
    private VBoxContainer _root,_body;
    private HBoxContainer _actions;
    private UnitDefinition _unit;
    private int _slot=1;
    private bool _removing;
    public RelicEquipmentPreview Preview { get; private set; }

    public static void Show(Control host,EquipmentDefinition relic,Action changed,UnitDefinition bearer=null,int slot=1)
    {
        var inspector=new RelicEquipmentInspector {Name="RelicEquipmentInspector",Layer=64,
            _relic=relic,_changed=changed,_unit=bearer,_slot=slot,_removing=bearer!=null};
        host.AddChild(inspector);
    }

    public override void _Ready()
    {
        _modal=RealmModal.OpenInspector(this,_relic.DisplayName,"relics",900,640);
        _root=new VBoxContainer();_root.AddThemeConstantOverride("separation",12);
        _modal.Content.AddChild(_root);_root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var scroll=new ScrollContainer {HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled,
            SizeFlagsVertical=Control.SizeFlags.ExpandFill,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};
        _root.AddChild(scroll);
        _body=new VBoxContainer {SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};
        _body.AddThemeConstantOverride("separation",12);scroll.AddChild(_body);
        _actions=new HBoxContainer();_actions.AddThemeConstantOverride("separation",12);_root.AddChild(_actions);
        if(_unit==null)ChooseAlly();else Compare();
    }

    private void Reset()
    {
        RealmUi.Clear(_body);RealmUi.Clear(_actions);
        ((ScrollContainer)_body.GetParent()).ScrollVertical=0;
    }

    private Button Action(string icon,string title,Action action,bool primary=false)
    {
        var button=RealmUi.Button(icon,title,action,primary);
        button.CustomMinimumSize=new Vector2(button.CustomMinimumSize.X,56);
        _actions.AddChild(button);return button;
    }

    private void RelicDetails()
    {
        _body.AddChild(RealmUi.Label(_relic.Description,20));
        var stats=RelicStatText.Stats(_relic);
        if(stats.Count>0)_body.AddChild(ArmoryDetailUi.Stats(stats,3));
        var enchantment=GameState.Instance.GetRelicEnchantment(_relic.Id);
        if(enchantment!=null)_body.AddChild(RealmUi.Label($"{enchantment.Title}: {enchantment.Description}",18,true));
        _body.AddChild(RealmUi.Label("pp means percentage points. Special effects have their own triggers and limits.",18,true));
    }

    private void ChooseAlly()
    {
        Reset();Preview=null;_removing=false;
        _modal.SetHeading(_relic.DisplayName);
        RelicDetails();
        _body.AddChild(RealmUi.SectionTitle("Choose an ally to compare"));
        var grid=new GridContainer {Columns=3,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};
        grid.AddThemeConstantOverride("h_separation",10);grid.AddThemeConstantOverride("v_separation",10);_body.AddChild(grid);
        foreach(var unit in GameState.Instance.GetOwnedPlayerUnits())
        {
            var button=RealmUi.Button("shield",unit.DisplayName,()=>{_unit=unit;_slot=1;Compare();});
            button.Icon=UnitFigure.For(unit);button.ExpandIcon=true;
            button.AccessibilityName=$"Compare {_relic.DisplayName} on {unit.DisplayName}";
            button.TooltipText=button.AccessibilityName;
            button.CustomMinimumSize=new Vector2(0,56);button.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;
            grid.AddChild(button);
        }
        Action("close","Close",QueueFree);
        RealmModal.Polish(_root);
    }

    private void Compare()
    {
        Reset();
        var state=GameState.Instance;
        Preview=state.BuildRelicEquipmentPreview(_unit.Id,_removing?null:_relic,_slot);
        _modal.SetHeading($"{_unit.DisplayName} · {_relic.DisplayName}");
        var slots=new HBoxContainer();slots.AddThemeConstantOverride("separation",12);_body.AddChild(slots);
        foreach(var slot in new[]{1,2})
        {
            var item=slot==1?state.GetUnitEquipment(_unit.Id):state.GetUnitEquipment2(_unit.Id);
            var unlocked=slot==1 || state.IsUnitPromoted(_unit.Id);
            var button=RealmUi.Button("shield",$"Slot {slot}: {(unlocked?item?.DisplayName??"Empty":"Locked")}",()=>{_slot=slot;Compare();});
            button.SetMeta("realm_toggle",true);button.ToggleMode=true;button.ButtonPressed=_slot==slot;
            button.AccessibilityName=$"Preview relic slot {slot}";
            button.Disabled=!unlocked || (_removing && item?.Id!=_relic.Id);
            button.CustomMinimumSize=new Vector2(0,56);
            slots.AddChild(button);
        }
        if(!state.IsUnitPromoted(_unit.Id))_body.AddChild(RealmUi.Label("Promote this ally to unlock its second relic slot.",18,true));
        var replacing=Preview.Replaced?.DisplayName??"Empty";
        var replacement=_removing?$"After removal: slot {_slot} is empty.":$"Replaces: {replacing}";
        _body.AddChild(RealmUi.Label($"Lv {state.GetUnitLevel(_unit.Id)} · "+replacement,20));
        if(Preview.Limits.Count>0)
        {
            foreach(var limit in Preview.Limits)_body.AddChild(RealmUi.Label(RelicStatText.LimitNotice(limit),20));
        }
        BuildComparison();
        if(!Preview.After.DamagesStructures && !_removing && (_relic.StructureDamageScale!=1 || _relic.BaseDamageBonus!=0))
            _body.AddChild(RealmUi.Label("This ally cannot damage the gate. Structure bonuses give it no gate attack.",18,true));
        _body.AddChild(RealmUi.SectionTitle(_removing?"Removed bonuses":"Relic bonuses"));
        RelicDetails();
        if(_removing)_body.AddChild(RealmUi.Label("The relic and its enchantment return to your collection; their bonuses leave this ally.",18,true));
        if(!Preview.CanApply)_body.AddChild(RealmUi.Label(Preview.Reason,20,true));
        Action("back","Choose ally",ChooseAlly);
        var confirm=Action("shield",_removing?$"Remove from slot {_slot}":$"Equip to slot {_slot}",Apply,true);
        confirm.AccessibilityName=confirm.Text;confirm.Disabled=!Preview.CanApply;
        RealmModal.Polish(_root);
    }

    private void BuildComparison() => _body.AddChild(ArmoryDetailUi.CompareStats(Preview.Current,Preview.After));

    private void Apply()
    {
        var state=GameState.Instance;
        var success=_removing?state.TryUnequipRelic(_unit.Id,_slot,out _)
            :_slot==1?state.TryEquipItem(_unit.Id,_relic.Id):state.TryEquipItem2(_unit.Id,_relic.Id);
        if(!success){Compare();return;}
        _changed?.Invoke();QueueFree();
    }
}
