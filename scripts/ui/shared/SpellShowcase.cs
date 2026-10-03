using Godot;

public partial class SpellShowcase : CanvasLayer
{
    private SpellDefinition _spell;

    public static void Show(Control host, SpellDefinition spell)
    {
        host.AddChild(new SpellShowcase { Name = "SpellShowcase", Layer = 64, _spell = spell });
    }

    public override void _Ready()
    {
        var veil = new ColorRect { Color = new Color("080f14f5") };
        AddChild(veil); veil.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var canvas = new ResponsiveUiCanvas(); AddChild(canvas); MedievalUi.Apply(canvas);
        var stack = new VBoxContainer(); stack.AddThemeConstantOverride("separation", 14); canvas.Content.AddChild(stack);
        var header = new HBoxContainer(); stack.AddChild(header);
        header.AddChild(RealmUi.Heading(_spell.DisplayName, 30));
        var close = RealmUi.Button("close", "Close", QueueFree); MobilePresentation.TouchButton(close); header.AddChild(close);
        var body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill }; body.AddThemeConstantOverride("separation", 24); stack.AddChild(body);
        var stage = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        stage.AddThemeStyleboxOverride("panel", new ModalSurface(ModalMaterial.Arcane, 12)); body.AddChild(stage);
        var art = new Control { ClipContents = true }; stage.AddChild(art);
        var backdrop = new ModalShowcaseBackdrop { Magic = true }; art.AddChild(backdrop); backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var image = new TextureRect {
            Texture = UiArtLoader.TryLoadSpellIcon(_spell), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore
        };
        art.AddChild(image); image.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        image.OffsetLeft = image.OffsetTop = 48; image.OffsetRight = image.OffsetBottom = -48;
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(360, 0), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; body.AddChild(scroll);
        var details = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; details.AddThemeConstantOverride("separation", 14); scroll.AddChild(details);
        var resolved = GameState.Instance.BuildSpellStats(_spell);
        details.AddChild(RealmUi.Label($"Lv {resolved.Level:00} · {ArmoryDetailUi.SpellRole(_spell.EffectType)}", 20, true));
        details.AddChild(RealmUi.Label(ArmoryDetailUi.SpellPurpose(_spell.EffectType), 20));
        details.AddChild(ArmoryDetailUi.Stats(ArmoryDetailUi.SpellStats(resolved), 2));
        var extra = ArmoryDetailUi.Disclosure(details, "Full description", false);
        extra.AddChild(RealmUi.Label(_spell.Description, 20));
        RealmModal.Polish(details); close.GrabFocus();
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (input is InputEventKey { Pressed: true, Keycode: Key.Escape }) { QueueFree(); GetViewport().SetInputAsHandled(); }
    }
}
