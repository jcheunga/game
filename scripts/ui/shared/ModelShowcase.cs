using System;
using System.Linq;
using Godot;

/// <summary>An ally's animated model and stats, in a modal above the current panel.</summary>
public partial class ModelShowcase : CanvasLayer
{
    private UnitDefinition[] _units;
    private int _index;
    private RealmModal _modal;
    private UnitModelPreview _preview;
    private Label _page;
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
        _modal=RealmModal.OpenInspector(this,"","warband");
        var stack=new VBoxContainer(); stack.AddThemeConstantOverride("separation",12);
        _modal.Content.AddChild(stack); stack.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var body=new HBoxContainer { SizeFlagsVertical=Control.SizeFlags.ExpandFill }; body.AddThemeConstantOverride("separation",20); stack.AddChild(body);
        var stage=new PanelContainer { SizeFlagsHorizontal=Control.SizeFlags.ExpandFill }; body.AddChild(stage);
        stage.AddThemeStyleboxOverride("panel",new ModalSurface(ModalMaterial.Steel,12));
        var art=new Control { ClipContents=true,CustomMinimumSize=new Vector2(250,150) }; stage.AddChild(art);
        var backdrop=new ModalShowcaseBackdrop(); art.AddChild(backdrop); backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _preview=new UnitModelPreview { CustomMinimumSize=new Vector2(250,150),MouseFilter=Control.MouseFilterEnum.Ignore };
        art.AddChild(_preview); _preview.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var report=new ScrollContainer { CustomMinimumSize=new Vector2(MobilePresentation.Enabled?300:320,0),
            HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled }; body.AddChild(report);
        _details=new VBoxContainer { SizeFlagsHorizontal=Control.SizeFlags.ExpandFill };
        _details.AddThemeConstantOverride("separation",12); report.AddChild(_details);

        // Animation choices on the left, squad paging on the right.
        var footer=new HBoxContainer(); footer.AddThemeConstantOverride("separation",8); stack.AddChild(footer);
        var states=new[]{UnitAnimState.Idle,UnitAnimState.Walk,UnitAnimState.Attack};
        _animationButtons=states.Select(state=>
        {
            var b=new RealmButton {Text=state.ToString(),CustomMinimumSize=new Vector2(96,48),ToggleMode=true,
                MouseDefaultCursorShape=Control.CursorShape.PointingHand};
            b.SetMeta("realm_toggle",true); ModalUi.StyleButton(b); Touch(b);
            b.Pressed+=()=>Play(state); footer.AddChild(b); return (Button)b;
        }).ToArray();
        footer.AddChild(new Control { SizeFlagsHorizontal=Control.SizeFlags.ExpandFill });
        var previous=RealmUi.IconButton("back","Previous ally",()=>Select(-1)); ModalUi.StyleButton(previous); Touch(previous); footer.AddChild(previous);
        _page=RealmUi.Label("",18,true); _page.VerticalAlignment=VerticalAlignment.Center; _page.HorizontalAlignment=HorizontalAlignment.Center;
        _page.AutowrapMode=TextServer.AutowrapMode.Off; _page.CustomMinimumSize=new Vector2(64,0); _page.SizeFlagsHorizontal=Control.SizeFlags.ShrinkCenter; footer.AddChild(_page);
        var next=RealmUi.IconButton("arrow","Next ally",()=>Select(1)); ModalUi.StyleButton(next); Touch(next); footer.AddChild(next);
        previous.Visible=next.Visible=_page.Visible=_units.Length>1;
        Select(0);
    }

    // Phones get touch-sized targets; the modal's own styling stays in place.
    private static void Touch(Button button)
    {
        if(MobilePresentation.Enabled) button.CustomMinimumSize=new Vector2(Mathf.Max(56,button.CustomMinimumSize.X),Mathf.Max(56,button.CustomMinimumSize.Y));
    }

    private void Select(int direction)
    {
        _index=(_index+direction+_units.Length)%_units.Length;
        var unit=_units[_index]; _preview.SetUnit(unit); _modal.SetHeading(unit.DisplayName);
        _page.Text=$"{_index+1} of {_units.Length}";
        var stats=unit.IsPlayerSide?GameState.Instance.BuildPlayerUnitStats(unit):new UnitStats(unit);
        RealmUi.Clear(_details);
        _details.AddChild(RealmUi.Label(SquadSynergyCatalog.GetTagDisplayName(unit.SquadTag),18,true));
        _details.AddChild(ArmoryDetailUi.Stats(ArmoryDetailUi.UnitStats(unit).Append(new ArmoryDetailUi.Stat("arrow","Move speed",$"{stats.Speed:0.#}"))
            .Append(new ArmoryDetailUi.Stat("clock","Attack interval",$"{stats.AttackCooldown:0.##}s")),2));
        var traits=UnitStatText.BuildInlineTraits(stats).Trim(' ','·');
        if(traits.Length>0) _details.AddChild(RealmUi.Label(traits,18));
        RealmModal.Polish(_details);
        Play(UnitAnimState.Idle);
    }

    private void Play(UnitAnimState state)
    {
        _preview.Play(state);
        foreach(var b in _animationButtons) b.ButtonPressed=b.Text==state.ToString();
    }
}
