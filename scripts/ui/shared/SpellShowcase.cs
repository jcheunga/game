using Godot;

/// <summary>A spell's art and stats, in a modal above the current panel.</summary>
public partial class SpellShowcase : CanvasLayer
{
    private SpellDefinition _spell;

    public static void Show(Control host, SpellDefinition spell)
    {
        host.AddChild(new SpellShowcase { Name = "SpellShowcase", Layer = 64, _spell = spell });
    }

    public override void _Ready()
    {
        var modal = RealmModal.OpenInspector(this, _spell.DisplayName, "spell");
        var body = new HBoxContainer(); body.AddThemeConstantOverride("separation", 20);
        modal.Content.AddChild(body); body.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var stage = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        stage.AddThemeStyleboxOverride("panel", new ModalSurface(ModalMaterial.Arcane, 12)); body.AddChild(stage);
        var art = new Control { ClipContents = true }; stage.AddChild(art);
        var backdrop = new ModalShowcaseBackdrop { Magic = true }; art.AddChild(backdrop); backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var image = new TextureRect {
            Texture = UiArtLoader.TryLoadSpellIcon(_spell), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore
        };
        art.AddChild(image); image.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        image.OffsetLeft = image.OffsetTop = 32; image.OffsetRight = image.OffsetBottom = -32;
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(MobilePresentation.Enabled ? 300 : 340, 0), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; body.AddChild(scroll);
        var details = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; details.AddThemeConstantOverride("separation", 12); scroll.AddChild(details);
        var resolved = GameState.Instance.BuildSpellStats(_spell);
        details.AddChild(RealmUi.Label($"Level {resolved.Level} · {ArmoryDetailUi.SpellRole(_spell.EffectType)}", 18, true));
        details.AddChild(RealmUi.Label(ArmoryDetailUi.SpellPurpose(_spell.EffectType), 18));
        details.AddChild(ArmoryDetailUi.Stats(ArmoryDetailUi.SpellStats(resolved), 2));
        details.AddChild(RealmUi.Label(_spell.Description, 18));
        RealmModal.Polish(details);
    }
}
