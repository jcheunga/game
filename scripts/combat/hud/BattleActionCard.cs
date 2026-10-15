using System.Collections.Generic;
using Godot;

/// <summary>Icon-first presentation only; the owning button retains all input and game rules.</summary>
public partial class BattleActionCard : Control
{
    private static readonly Dictionary<ulong, Rect2I> IconBounds = new();
    private AtlasTexture _ownedCrop;
    private Texture2D _sourceIcon;
    private readonly TextureRect _portrait, _face;
    private static GradientTexture2D _faceTexture;
    private readonly Control _cooldownShade;
    private readonly ShaderMaterial _portraitMaterial;
    private static Shader _cooldownShader;
    private readonly PanelContainer _costPlate;
    private readonly Label _cost;
    private readonly StyleBoxTexture _costMaterial, _selection;
    private bool _selected;
    private float _cooldownRatio, _numeralBadge;

    internal TextureRect Portrait => _portrait;
    internal PanelContainer CostPlate => _costPlate;
    internal Control CooldownShade => _cooldownShade;
    internal Label CostLabel => _cost;
    internal bool Selected => _selected;
    internal float CooldownRatio => _cooldownRatio;
    internal bool Unaffordable { get; private set; }
    /// <summary>A card without a cost badge (the caravan spell) lets its art fill the face.</summary>
    public bool FullArt { get; set; }

    public BattleActionCard()
    {
        Name = "ActionCardArt";
        SetMeta("frame_bleed", true);
        MouseFilter = MouseFilterEnum.Ignore;
        ClipContents = false;
        TextureFilter = TextureFilterEnum.Linear;
        _selection = MedievalUi.Engraved("focus", 0, 0);
        _costMaterial = new StyleBoxTexture { Texture = RoyalKit.Texture("hud-cost") };
        // The concept's card face: dark steel-teal, lit softly behind the figure.
        _faceTexture ??= new GradientTexture2D {
            Width = 128, Height = 128, Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(.5f, .42f), FillTo = new Vector2(1.05f, 1.1f),
            Gradient = new Gradient { Offsets = new[] { 0f, 1f }, Colors = new[] { new Color("2a3d40"), new Color("0d1515") } }
        };
        _face = new TextureRect { Name = "Face", Texture = _faceTexture, MouseFilter = MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale };
        AddChild(_face);
        _cooldownShader ??= new Shader { Code = CooldownShaderCode };
        _portraitMaterial = new ShaderMaterial { Shader = _cooldownShader };
        _portrait = new TextureRect
        {
            Name = "Portrait", MouseFilter = MouseFilterEnum.Ignore, Material = _portraitMaterial,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
        };
        AddChild(_portrait);
        // The dark card face swallows a dark veil, so a cooldown reads from the greyed art and a bright gold edge.
        _cooldownShade = new Control { Name = "CooldownShade", MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        _cooldownShade.Draw += DrawCooldown;
        AddChild(_cooldownShade);
        _costPlate = new PanelContainer { Name = "CourageCost", MouseFilter = MouseFilterEnum.Ignore };
        _costPlate.SetMeta("frame_inset", 1f);
        _costPlate.AddThemeStyleboxOverride("panel", _costMaterial);
        AddChild(_costPlate);
        // The concept's round bronze badge with the courage cost in heavy white book serif (blue for mana).
        _cost = CardLabel(21);
        _cost.AddThemeFontOverride("font", RoyalFonts.Body(800));
        _cost.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, .7f));
        _cost.AddThemeConstantOverride("shadow_offset_x", 0);
        _cost.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _costPlate.AddChild(_cost);
        Resized += LayoutArt;
    }

    private static Label CardLabel(int size)
    {
        var label = new Label { MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeFontOverride("font", RoyalFonts.Body(600));
        label.AddThemeColorOverride("font_color", new Color("fbfaf9"));
        label.AddThemeColorOverride("font_outline_color", new Color("080e12"));
        label.AddThemeConstantOverride("outline_size", 2);
        return label;
    }

    public void SetIcon(Texture2D texture)
    {
        ReleaseCrop();
        if (texture == null) texture = RealmUi.Icon("shield");
        _sourceIcon = texture;
        var key = texture.GetInstanceId();
        if (!IconBounds.TryGetValue(key, out var bounds))
        {
            // Remove transparent padding without editing the authored portrait.
            using var pixels = texture.GetImage();
            if (pixels != null)
            {
                if (pixels.IsCompressed()) pixels.Decompress();
                bounds = pixels.GetUsedRect();
            }
            if (bounds.Size.X <= 0 || bounds.Size.Y <= 0) bounds = new Rect2I(Vector2I.Zero, (Vector2I)texture.GetSize());
            IconBounds[key] = bounds;
        }
        _portrait.Texture = _ownedCrop = new AtlasTexture { Atlas = texture, Region = bounds };
        LayoutArt();
    }

    private void ReleaseCrop()
    {
        _portrait.Texture = null;
        _ownedCrop?.Dispose();
        _ownedCrop = null;
    }

    public override void _ExitTree() => ReleaseCrop();

    public override void _EnterTree()
    {
        // Mobile layout reparents the row into a scroller. Re-entry must restore
        // its owned wrapper after _ExitTree released it, without decoding again.
        if (_ownedCrop == null && _sourceIcon != null) SetIcon(_sourceIcon);
    }

    /// <summary>Magic is paid in mana, so its cards wear the blue mana badge instead of the bronze courage one.</summary>
    public void UseManaCost()
    {
        _costPlate.Name = "ManaCost";
        _costMaterial.Texture = RoyalKit.Texture("hud-cost-mana");
    }

    /// <param name="funds">The courage (troops) or mana (magic) the player has to pay the cost with.</param>
    public void SetState(int cost, float funds, float cooldown, float totalCooldown, bool selected, bool blocked)
    {
        var cooling = cooldown > .05f;
        var affordable = funds >= cost;
        Unaffordable = !affordable;
        _selected = selected;
        _cooldownRatio = cooling && totalCooldown > .1f ? Mathf.Clamp(cooldown / totalCooldown, 0, 1) : 0;
        _cost.Text = cost.ToString();
        _costMaterial.ModulateColor = affordable ? Colors.White : new Color("e88f7a");
        // The recharged part of a cooling card keeps full colour so the split against the greyed part is plain.
        _portrait.Modulate = !affordable || blocked ? new Color("a0aaa9") : Colors.White;
        // Unaffordable cards read from the dimmed art and red-tinted cost; a cooldown from its falling shade alone.
        _cooldownShade.Visible = cooling;
        LayoutArt();
        QueueRedraw();
    }

    private void LayoutArt()
    {
        var inner = new Rect2(new Vector2(4, 4), (Size - new Vector2(8, 8)).Max(Vector2.One));
        _face.Position = inner.Position + Vector2.One; _face.Size = (inner.Size - Vector2.One * 2).Max(Vector2.One);
        // The art sits whole inside the card with a margin, a little below the cost badge.
        _portrait.Position = inner.Position + inner.Size * (FullArt ? new Vector2(.08f, .07f) : new Vector2(.14f, .17f));
        _portrait.Size = inner.Size * (FullArt ? new Vector2(.84f, .86f) : new Vector2(.72f, .76f));
        _cooldownShade.Position = new Vector2(inner.Position.X, inner.End.Y - inner.Size.Y * _cooldownRatio);
        _cooldownShade.Size = new Vector2(inner.Size.X, inner.Size.Y * _cooldownRatio);
        _cooldownShade.QueueRedraw();
        // The portrait greys out below the same line (in its own local pixels).
        _portraitMaterial.SetShaderParameter("split_y", _cooldownRatio > 0 ? _cooldownShade.Position.Y - _portrait.Position.Y : 1e5f);
        // Badge: 48 px at the concept's 119 px card (the concept's 44 read too small), top-right, scaled with the card.
        var badge = Mathf.Round(48 * Size.X / 119f);
        _costPlate.Position = new Vector2(Size.X - badge - 2, 1);
        _costPlate.Size = new Vector2(badge, badge);
        // Heavy numerals filling most of the badge's dark centre, with an outline that holds on the bronze or blue.
        // LayoutArt runs every HUD update; theme overrides relayout the label, so only touch them on a resize.
        if (badge == _numeralBadge) return;
        _numeralBadge = badge;
        _cost.AddThemeFontSizeOverride("font_size", Mathf.Max(16, Mathf.RoundToInt(badge * .58f)));
        _cost.AddThemeConstantOverride("outline_size", Mathf.Max(3, Mathf.RoundToInt(badge * .1f)));
        _cost.AddThemeConstantOverride("shadow_offset_y", Mathf.Max(1, Mathf.RoundToInt(badge * .04f)));
    }

    private void DrawCooldown()
    {
        var size = _cooldownShade.Size;
        if (size.Y < .5f) return;
        // A light veil keeps the face's teal from reading as ready; the greyed portrait carries most of the contrast.
        _cooldownShade.DrawRect(new Rect2(Vector2.Zero, size), new Color(.02f, .04f, .05f, .45f));
        // The leading edge: a soft gold glow either side of a crisp bright line, visible on any art.
        var glow = Mathf.Clamp(size.X * .06f, 3, 8);
        var gold = new Color("ffd47a");
        var clear = new Color(gold, 0);
        _cooldownShade.DrawPolygon(new[] { new Vector2(0, -glow), new Vector2(size.X, -glow), new Vector2(size.X, 0), new Vector2(0, 0) },
            new[] { clear, clear, new Color(gold, .55f), new Color(gold, .55f) });
        _cooldownShade.DrawPolygon(new[] { new Vector2(0, 0), new Vector2(size.X, 0), new Vector2(size.X, glow), new Vector2(0, glow) },
            new[] { new Color(gold, .4f), new Color(gold, .4f), clear, clear });
        _cooldownShade.DrawRect(new Rect2(0, -1, size.X, 2), new Color("fff1c9"));
    }

    private const string CooldownShaderCode = @"shader_type canvas_item;
// Local y (control pixels) below which the art is still recharging.
uniform float split_y = 100000.0;
varying float local_y;
void vertex() { local_y = VERTEX.y; }
void fragment() {
    vec4 c = texture(TEXTURE, UV) * COLOR;
    float cooling = smoothstep(split_y - 0.75, split_y + 0.75, local_y);
    float g = dot(c.rgb, vec3(0.3, 0.59, 0.11));
    COLOR = vec4(mix(c.rgb, vec3(g) * 0.55, cooling), c.a);
}";

    private static SliceStyle _selectedFrame;

    public override void _Draw()
    {
        if (!_selected) return;
        // The concept's glowing gold frame marks the armed card.
        _selectedFrame ??= RoyalKit.Slice("hud-card-selected", 14, false);
        DrawStyleBox(_selectedFrame, new Rect2(-3, -3, Size.X + 6, Size.Y + 6));
    }
}
