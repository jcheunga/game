using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class UiReviewSmoke
{
    private async Task ReviewMaterials()
    {
        _output=ProjectSettings.GlobalizePath("res://artifacts/ui-material-review");
        System.IO.Directory.CreateDirectory(_output);
        var root=new Control {Name="MaterialReview"}; GetTree().Root.AddChild(root); GetTree().CurrentScene=root;
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        MedievalUi.Apply(root);
        var backdrop=new ColorRect {Color=new Color("0e1a21"),MouseFilter=Control.MouseFilterEnum.Ignore};
        root.AddChild(backdrop); backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var heading=RealmUi.Heading("CROWNROAD / MATERIAL LIBRARY",30); heading.Position=new Vector2(32,24); heading.Size=new Vector2(1216,44); root.AddChild(heading);
        var sub=RealmUi.Label("Dark leather · aged brass · recessed steel · readable touch controls",18,true);
        sub.Position=new Vector2(32,70); sub.Size=new Vector2(1216,28); root.AddChild(sub);

        var surfaces=new[]{"engraved_panel","button","button_hover","button_pressed","button_hover_pressed",
            "button_disabled","button_primary","button_primary_hover","button_primary_pressed","focus","cost_badge","inset","input_focus","input_disabled",
            "surface_body","surface_rim","meter_track","meter_fill","scroll_thumb","scroll_thumb_hover",
            "scroll_thumb_horizontal","scroll_thumb_horizontal_hover","separator","dropdown",
            "checked","unchecked","radio_checked","radio_unchecked","checked_disabled","unchecked_disabled","radio_checked_disabled","radio_unchecked_disabled"};
        foreach(var name in surfaces)
        {
            var texture=MedievalUi.FrameTexture(name);
            Check(texture!=null && texture.GetWidth()>0 && texture.GetHeight()>0,name+" native UI material imports");
        }
        using(var pixels=MedievalUi.FrameTexture("engraved_panel").GetImage())
        {
            if(pixels.IsCompressed()) pixels.Decompress();
            var colours=new HashSet<Color>();
            for(var y=64;y<96;y++) for(var x=64;x<96;x++) colours.Add(pixels.GetPixel(x,y));
            Check(colours.Count>8,"Panel grain exists in the engine's decoded texture, not just in SVG source");
            GD.Print($"MATERIAL_GRAIN: {colours.Count} interior colours");
        }
        var panelStyle=MedievalUi.Engraved("engraved_panel",18,14);
        Check(panelStyle.AxisStretchHorizontal==StyleBoxTexture.AxisStretchMode.Tile && panelStyle.AxisStretchVertical==StyleBoxTexture.AxisStretchMode.Tile,
            "Large panels tile grain at fixed resolution instead of stretching it");
        Check(panelStyle.GetContentMargin(Side.Left)==18 && panelStyle.GetContentMargin(Side.Top)==14,
            "Panel content padding is unchanged");
        var badge=RealmUi.Surface(new Color("142228"),RealmUi.Gold);
        Check(badge is UiSurfaceStyle && badge.GetContentMargin(Side.Left)==16 && badge.GetContentMargin(Side.Top)==12,
            "Tinted badge surfaces preserve their content footprint");

        var stack=RealmUi.Panel(root,new Rect2(28,116,1224,566),out _);
        var states=new HBoxContainer(); stack.AddChild(states);
        var captions=new[]{"Normal","Hover","Pressed","Selected hover","Disabled","Primary"};
        var assets=new[]{"button","button_hover","button_pressed","button_hover_pressed","button_disabled","button_primary"};
        for(var i=0;i<captions.Length;i++)
        {
            var button=new Button {Text=captions[i],CustomMinimumSize=new Vector2(0,58),SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};
            button.AddThemeStyleboxOverride("normal",MedievalUi.Engraved(assets[i],12,10));
            if(i==4) button.Disabled=true;
            states.AddChild(button);
        }
        var focus=RealmUi.Button("eye","Keyboard focus",()=>{}); stack.AddChild(focus); focus.GrabFocus();
        var primary=RealmUi.Button("flag","Deploy / touch",()=>{},true); MobilePresentation.TouchButton(primary);
        stack.AddChild(primary);
        Check(primary.GetThemeStylebox("normal") is StyleBoxTexture normal && normal.Texture.ResourcePath.EndsWith("button_primary.svg"),
            "Touch sizing preserves the primary-action brass finish");
        Check(primary.GetThemeStylebox("hover_pressed") is StyleBoxTexture,"Selected hover state stays textured");
        Check(focus.GetThemeStylebox("focus") is StyleBoxTexture focusStyle && focusStyle.Texture.ResourcePath.EndsWith("focus.svg"),
            "Keyboard focus has a distinct bright outline");
        Check(root.Theme.GetStylebox("grabber","HScrollBar") is StyleBoxTexture horizontalThumb
            && horizontalThumb.Texture.ResourcePath.EndsWith("scroll_thumb_horizontal.svg"),
            "Horizontal card scrollers use a continuous rail, not repeating vertical grips");
        var inputs=new HBoxContainer(); stack.AddChild(inputs);
        inputs.AddChild(new LineEdit {Text="Caravan callsign",SizeFlagsHorizontal=Control.SizeFlags.ExpandFill});
        inputs.AddChild(new LineEdit {Text="Read only",Editable=false,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill});
        var select=new OptionButton {CustomMinimumSize=new Vector2(280,48)}; select.AddItem("King's Road"); select.AddItem("Saltwake Docks"); inputs.AddChild(select);
        var checks=new HBoxContainer(); stack.AddChild(checks);
        checks.AddChild(new CheckBox {Text="Field hints",ButtonPressed=true});
        checks.AddChild(new CheckBox {Text="Reduced motion"});
        checks.AddChild(new CheckBox {Text="Unavailable",Disabled=true});
        stack.AddChild(new HSeparator());
        var meters=new HBoxContainer(); stack.AddChild(meters);
        var courage=new BattleHudBar {CustomMinimumSize=new Vector2(350,30)}; courage.Setup(RealmUi.Gold,Colors.White,"Courage"); courage.SetValue(.68f,"68/100"); meters.AddChild(courage);
        var waves=new BattleHudBar {CustomMinimumSize=new Vector2(350,30)}; waves.Setup(new Color("86b4a0"),Colors.White,"Waves"); waves.SetValue(.5f,"3/6"); meters.AddChild(waves);
        var progress=new ProgressBar {Value=63,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,CustomMinimumSize=new Vector2(0,30)}; meters.AddChild(progress);
        var lower=new HBoxContainer {SizeFlagsVertical=Control.SizeFlags.ExpandFill}; stack.AddChild(lower);
        var inset=new PanelContainer {SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};
        inset.AddThemeStyleboxOverride("panel",badge); lower.AddChild(inset);
        inset.AddChild(RealmUi.Label("Worn surfaces stay quiet behind text.\n\nSelected cards keep their individual accent colours.",20));
        var scroll=new ScrollContainer {CustomMinimumSize=new Vector2(420,100),HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled}; lower.AddChild(scroll);
        var list=new VBoxContainer {SizeFlagsHorizontal=Control.SizeFlags.ExpandFill}; scroll.AddChild(list);
        for(var i=0;i<12;i++) list.AddChild(RealmUi.Label($"Caravan supplies / entry {i+1:00}",20));
        await Wait(.4);
        foreach(var (type,item) in new[]{("PanelContainer","panel"),("LineEdit","normal"),("LineEdit","focus"),
            ("OptionButton","normal"),("PopupMenu","panel"),("TooltipPanel","panel"),("VScrollBar","grabber"),
            ("HScrollBar","grabber"),("ProgressBar","background"),("ProgressBar","fill"),("HSeparator","separator")})
            Check(root.Theme.GetStylebox(item,type) is StyleBoxTexture,$"{type}/{item} uses a shared textured material");
        await Capture("material-library");
        GameState.Instance.SetHighContrast(true); await Wait(.1); await Capture("material-library-high-contrast");
        GameState.Instance.SetHighContrast(false);
        GD.Print($"UI_MATERIAL_RESULT: {_failures} failures"); GetTree().Quit(_failures==0?0:1);
    }
}
