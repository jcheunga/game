using System;
using System.Linq;
using Godot;

public partial class ModelShowcase : CanvasLayer
{
    private UnitDefinition[] _units;
    private int _index;
    private UnitModelPreview _preview;
    private Label _title, _page;
    private VBoxContainer _details;
    private Button[] _animationButtons;
    public static ModelShowcase Show(Control host, UnitDefinition[] units, string selectedId)
    {
        var gallery=new ModelShowcase {Name="ModelShowcase",Layer=64,_units=units,
            _index=Mathf.Max(0,Array.FindIndex(units,u=>u.Id==selectedId))};
        if(units.Length==0) return null;
        host.AddChild(gallery); return gallery;
    }

    public override void _Ready()
    {
        var veil=new ColorRect { Color=new Color("080f14f5") };
        AddChild(veil); veil.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var canvas=new ResponsiveUiCanvas(); AddChild(canvas); MedievalUi.Apply(canvas);
        var stack=new VBoxContainer(); stack.AddThemeConstantOverride("separation",8); canvas.Content.AddChild(stack);
        var header=new HBoxContainer(); stack.AddChild(header);
        _title=RealmUi.Heading("",30); header.AddChild(_title);
        var close=RealmUi.Button("close","Close",QueueFree); MobilePresentation.TouchButton(close); header.AddChild(close);
        var body=new HBoxContainer { SizeFlagsVertical=Control.SizeFlags.ExpandFill }; stack.AddChild(body);
        var stage=new PanelContainer { SizeFlagsHorizontal=Control.SizeFlags.ExpandFill }; body.AddChild(stage);
        stage.AddThemeStyleboxOverride("panel",MedievalUi.Engraved("engraved_panel",12,8));
        _preview=new UnitModelPreview { CustomMinimumSize=new Vector2(250,150),MouseFilter=Control.MouseFilterEnum.Ignore };
        stage.AddChild(_preview);
        var report=new ScrollContainer { CustomMinimumSize=new Vector2(320,0),
            HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled }; body.AddChild(report);
        _details=new VBoxContainer { SizeFlagsHorizontal=Control.SizeFlags.ExpandFill };
        _details.AddThemeConstantOverride("separation",12); report.AddChild(_details);
        var footer=new HBoxContainer(); footer.AddThemeConstantOverride("separation",8); stack.AddChild(footer);
        var previous=RealmUi.IconButton("back","Previous model",()=>Select(-1)); MobilePresentation.TouchButton(previous); footer.AddChild(previous);
        _page=RealmUi.Label("",20,true); _page.VerticalAlignment=VerticalAlignment.Center; footer.AddChild(_page);
        var states=new[]{UnitAnimState.Idle,UnitAnimState.Walk,UnitAnimState.Attack};
        _animationButtons=states.Select(state=>
        {
            var b=new Button {Text=state.ToString(),CustomMinimumSize=new Vector2(100,56),ToggleMode=true};
            MobilePresentation.TouchButton(b); b.Pressed+=()=>Play(state); footer.AddChild(b); return b;
        }).ToArray();
        var next=RealmUi.IconButton("arrow","Next model",()=>Select(1)); MobilePresentation.TouchButton(next); footer.AddChild(next);
        Select(0); close.GrabFocus();
    }

    private void Select(int direction)
    {
        _index=(_index+direction+_units.Length)%_units.Length;
        var unit=_units[_index]; _preview.SetUnit(unit); _title.Text=unit.DisplayName;
        _page.Text=$"{_index+1} / {_units.Length}";
        var stats=unit.IsPlayerSide?GameState.Instance.BuildPlayerUnitStats(unit):new UnitStats(unit);
        RealmUi.Clear(_details);
        _details.AddChild(RealmUi.Label(SquadSynergyCatalog.GetTagDisplayName(unit.SquadTag),18,true));
        _details.AddChild(ArmoryDetailUi.Stats(ArmoryDetailUi.UnitStats(unit),2));
        var extra=ArmoryDetailUi.Disclosure(_details,"More stats",false);
        extra.AddChild(ArmoryDetailUi.Stats(new[] {
            new ArmoryDetailUi.Stat("arrow","Move speed",$"{stats.Speed:0.#}"),
            new ArmoryDetailUi.Stat("clock","Attack interval",$"{stats.AttackCooldown:0.##}s")
        },2));
        var traits=UnitStatText.BuildInlineTraits(stats).Trim(' ','|');
        if(traits.Length>0) extra.AddChild(RealmUi.Label(traits,18));
        RealmModal.Polish(_details);
        Play(UnitAnimState.Idle);
    }

    private void Play(UnitAnimState state)
    {
        _preview.Play(state);
        foreach(var b in _animationButtons) b.ButtonPressed=b.Text==state.ToString();
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if(input is InputEventKey {Pressed:true,Keycode:Key.Escape}) {QueueFree(); GetViewport().SetInputAsHandled();}
    }
}
