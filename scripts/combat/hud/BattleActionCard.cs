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
    private readonly ColorRect _cooldownShade;
    private readonly PanelContainer _costPlate;
    private readonly Label _cost;
    private readonly StyleBoxTexture _costMaterial, _selection;
    private bool _selected;
    private float _cooldownRatio;

    internal TextureRect Portrait => _portrait;
    internal PanelContainer CostPlate => _costPlate;
    internal Label CostLabel => _cost;
    internal bool Selected => _selected;
    internal float CooldownRatio => _cooldownRatio;
    internal bool Unaffordable { get; private set; }

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
        _portrait = new TextureRect
        {
            Name = "Portrait", MouseFilter = MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
        };
        AddChild(_portrait);
        _cooldownShade = new ColorRect
        {
            Color = new Color("07101799"), MouseFilter = MouseFilterEnum.Ignore, Visible = false
        };
        AddChild(_cooldownShade);
        _costPlate = new PanelContainer { Name = "CourageCost", MouseFilter = MouseFilterEnum.Ignore };
        _costPlate.SetMeta("frame_inset", 1f);
        _costPlate.AddThemeStyleboxOverride("panel", _costMaterial);
        AddChild(_costPlate);
        // The concept's round bronze badge with the courage cost in white book serif (blue for mana).
        _cost = CardLabel(21);
        _cost.AddThemeFontOverride("font", RoyalFonts.Body(600));
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
        _portrait.Modulate = cooling || !affordable || blocked ? new Color("a0aaa9") : Colors.White;
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
        _portrait.Position = inner.Position + inner.Size * new Vector2(.14f, .17f);
        _portrait.Size = inner.Size * new Vector2(.72f, .76f);
        _cooldownShade.Position = new Vector2(inner.Position.X, inner.End.Y - inner.Size.Y * _cooldownRatio);
        _cooldownShade.Size = new Vector2(inner.Size.X, inner.Size.Y * _cooldownRatio);
        // Badge: 44 px at the concept's 119 px card, top-right, scaled with the card.
        var badge = Mathf.Round(44 * Size.X / 119f);
        _costPlate.Position = new Vector2(Size.X - badge - 2, 1);
        _costPlate.Size = new Vector2(badge, badge);
    }

    private static SliceStyle _selectedFrame;

    public override void _Draw()
    {
        if (!_selected) return;
        // The concept's glowing gold frame marks the armed card.
        _selectedFrame ??= RoyalKit.Slice("hud-card-selected", 14, false);
        DrawStyleBox(_selectedFrame, new Rect2(-3, -3, Size.X + 6, Size.Y + 6));
    }
}
