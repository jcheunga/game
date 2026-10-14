using System;
using System.Linq;
using Godot;

public partial class ForgeEnchantmentInspector : CanvasLayer
{
    private Action<string> _changed;
    private string _relicId="",_candidateId="";
    private RealmModal _modal;
    private VBoxContainer _root,_body;
    private Label _wallet,_cost;
    private HBoxContainer _actions;
    public RelicEnchantmentPreview Preview { get; private set; }

    public static void Show(Control host,Action<string> changed,string relicId="")
    {
        host.AddChild(new ForgeEnchantmentInspector {Name="ForgeEnchantmentInspector",Layer=64,_changed=changed,_relicId=relicId});
    }

    public override void _Ready()
    {
        _modal=RealmModal.OpenInspector(this,"Enchantments","forge",940,640);
        _root=new VBoxContainer();_root.AddThemeConstantOverride("separation",10);
        _modal.Content.AddChild(_root);_root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _wallet=RealmUi.Label("",20,true);_wallet.Name="EnchantmentWallet";_root.AddChild(_wallet);
        var scroll=new ScrollContainer {HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled,
            SizeFlagsVertical=Control.SizeFlags.ExpandFill,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};_root.AddChild(scroll);
        _body=new VBoxContainer {SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};_body.AddThemeConstantOverride("separation",10);scroll.AddChild(_body);
        _cost=RealmUi.Label("",20);_cost.Name="EnchantmentCost";_root.AddChild(_cost);
        _actions=new HBoxContainer();_actions.AddThemeConstantOverride("separation",12);_root.AddChild(_actions);
        if(!string.IsNullOrWhiteSpace(_relicId) && GameState.Instance.GetOwnedEquipment().Contains(_relicId))ChooseEnchantment();else ChooseRelic();
    }

    private void Reset()
    {
        Preview=null;RealmUi.Clear(_body);RealmUi.Clear(_actions);
        ((ScrollContainer)_body.GetParent()).ScrollVertical=0;
        var state=GameState.Instance;_wallet.Text=$"Gold: {state.Gold:N0} · Essence: {state.Essence:N0}";
        _cost.Text="";_cost.Visible=false;
    }

    private Button Action(string icon,string title,Action run,bool primary=false)
    {
        var button=RealmUi.Button(icon,title,run,primary);button.AccessibilityName=title;
        if(MobilePresentation.Enabled)button.CustomMinimumSize=new Vector2(button.CustomMinimumSize.X,56);
        _actions.AddChild(button);return button;
    }

    private void ChooseRelic()
    {
        Reset();_modal.SetHeading("Enchantments");
        _body.AddChild(RealmUi.Label("Choose an owned relic. Its enchantment stays with it when you change its bearer.",20));
        _body.AddChild(RealmUi.Label("Essence comes from Tower rewards, Arena wins, bounties and reward tracks.",18,true));
        var owned=GameData.GetAllEquipment().Where(r=>GameState.Instance.GetOwnedEquipment().Contains(r.Id)).ToArray();
        if(owned.Length==0)_body.AddChild(RealmUi.EmptyState("crystal","No relics yet","Craft or earn a relic, then return here to enchant it."));
        else
        {
            var grid=new GridContainer {Columns=3,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};
            grid.AddThemeConstantOverride("h_separation",10);grid.AddThemeConstantOverride("v_separation",10);_body.AddChild(grid);
            foreach(var relic in owned)
            {
                var button=RealmUi.Button("shield",relic.DisplayName,()=>{_relicId=relic.Id;ChooseEnchantment();});
                button.Icon=ShopMenu.RelicArt(relic);button.AccessibilityName=$"Enchant relic {relic.DisplayName}";
                button.CustomMinimumSize=new Vector2(0,56);button.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;grid.AddChild(button);
            }
        }
        Action("back","Back to Forge",QueueFree);RealmModal.Polish(_root);
    }

    private void ChooseEnchantment()
    {
        if(!GameState.Instance.GetOwnedEquipment().Contains(_relicId)){ChooseRelic();return;}
        Reset();var relic=GameData.GetEquipment(_relicId);var current=GameState.Instance.GetRelicEnchantment(_relicId);
        _modal.SetHeading(relic.DisplayName);
        _body.AddChild(RealmUi.Label($"Current enchantment: {current?.Title??"None"}",20));
        _body.AddChild(RealmUi.Label("One enchantment per relic. Replacing pays the new cost; removing is free and refunds no materials.",18,true));
        var grid=new GridContainer {Columns=2,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};
        grid.AddThemeConstantOverride("h_separation",12);grid.AddThemeConstantOverride("v_separation",12);_body.AddChild(grid);
        foreach(var enchantment in EnchantmentCatalog.GetAll())
        {
            var panel=new PanelContainer {SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};grid.AddChild(panel);
            var content=new VBoxContainer();content.AddThemeConstantOverride("separation",6);panel.AddChild(content);
            var button=RealmUi.Button("crystal",enchantment.Title,()=>{_candidateId=enchantment.Id;Compare();});
            button.AccessibilityName=$"Preview enchantment {enchantment.Title}";button.CustomMinimumSize=new Vector2(0,56);content.AddChild(button);
            content.AddChild(RealmUi.Label(enchantment.Description,20));
            content.AddChild(RealmUi.Label($"{enchantment.GoldCost} gold · {enchantment.EssenceCost} Essence{(current?.Id==enchantment.Id?" · Applied":"")}",18,true));
        }
        Action("back","Choose relic",ChooseRelic);
        if(current!=null)Action("close","Remove enchantment",()=>{_candidateId="";Compare();});
        RealmModal.Polish(_root);
    }

    private void Compare()
    {
        Reset();var state=GameState.Instance;
        Preview=state.BuildRelicEnchantmentPreview(_relicId,_candidateId);
        var removing=string.IsNullOrWhiteSpace(_candidateId);
        _modal.SetHeading($"{Preview.Relic.DisplayName} · {(removing?"Remove enchantment":Preview.Candidate?.Title??"Enchant")}");
        _body.AddChild(RealmUi.Label($"{Preview.Current?.Title??"None"} → {Preview.Candidate?.Title??"None"}",20));
        foreach(var wearer in Preview.Wearers)
        {
            _body.AddChild(RealmUi.Label($"{wearer.Unit.DisplayName} · slot {wearer.Slot} · Lv {state.GetUnitLevel(wearer.Unit.Id)}",20,true));
            foreach(var limit in wearer.Limits)_body.AddChild(RealmUi.Label(RelicStatText.LimitNotice(limit),20));
            _body.AddChild(ArmoryDetailUi.CompareStats(wearer.Current,wearer.After,true));
        }
        if(Preview.Wearers.Count==0)_body.AddChild(RealmUi.Label("Stored in your collection. These bonuses apply when the relic is equipped.",20,true));
        if(Preview.Current!=null)
        {
            _body.AddChild(RealmUi.SectionTitle("Current effect"));
            _body.AddChild(RealmUi.Label($"{Preview.Current.Title}: {Preview.Current.Description}",20));
        }
        _body.AddChild(RealmUi.SectionTitle(removing?"After removal":"New effect"));
        _body.AddChild(RealmUi.Label(removing?"The relic stays owned and equipped. Only its enchantment is removed.":Preview.Candidate?.Description??"Choose a valid enchantment.",20));
        _body.AddChild(RealmUi.Label("Permanent bonuses share the equipment limits. Conditional effects keep their own triggers and cooldowns.",18,true));
        _cost.Text=removing?"Free removal · no refund":$"Cost: {Preview.GoldCost} gold + {Preview.EssenceCost} Essence";_cost.Visible=true;
        if(!Preview.CanApply)_body.AddChild(RealmUi.Label(Preview.Reason,20,true));
        Action("back","Choose enchantment",ChooseEnchantment);
        var confirm=Action("crystal",removing?"Confirm removal":"Apply enchantment",Apply,true);
        confirm.Disabled=!Preview.CanApply;RealmModal.Polish(_root);
    }

    private void Apply()
    {
        var state=GameState.Instance;
        var success=string.IsNullOrWhiteSpace(_candidateId)?state.TryRemoveEnchantment(_relicId,out _):state.TryApplyEnchantment(_relicId,_candidateId,out _);
        if(!success){Compare();return;}
        _changed?.Invoke(_relicId);QueueFree();
    }
}
