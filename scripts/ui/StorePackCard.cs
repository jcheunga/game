using System;
using Godot;

/// <summary>
/// One pack in the royal storehouse: its painted picture in a pool of light, the pack's name, what it holds and a
/// gold buy button carrying the store's price. Gold and ration packs stand four to a row; bundles are wide, with the
/// picture beside their contents. The card lights up gold while its purchase waits for the second tap.
/// It lays its pieces out by hand (it sits on the 1.55x modal canvas, so sizes are modal units) and is never passed
/// through <see cref="RealmModal.Polish"/>, which would restyle its painted button.
/// </summary>
public partial class StorePackCard : Control
{
    public ShopProduct Product { get; }
    public RoyalButton Buy { get; private set; }
    public bool Confirming { get; private set; }

    private static readonly Color ButtonInk = new("382316"), TitleInk = new("f3deaa"), BodyInk = new("e9dfcb"), MutedInk = new("b9ad96");
    private readonly bool _wide, _owned, _bestValue;
    private readonly string _price;
    private Panel _frame;
    private TextureRect _glow, _shadow, _art;
    private RoyalLabel _title, _amount, _bonus;
    private TextureRect _amountIcon;
    private Control _ribbon;
    private RoyalLabel _ribbonLabel;
    private Control _contents;

    public StorePackCard() { }

    public StorePackCard(ShopProduct product, bool owned, bool bestValue, string price, bool wide, Action<StorePackCard> buy)
    {
        Product = product; _owned = owned; _bestValue = bestValue; _price = price; _wide = wide;
        Name = "Pack_" + product.Id;
        MouseFilter = MouseFilterEnum.Pass;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _frame = new Panel { MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_frame);
        _glow = Picture(Glow(product.Category));
        _shadow = Picture(Shadow);
        _glow.StretchMode = _shadow.StretchMode = TextureRect.StretchModeEnum.Scale;
        _art = Picture(RoyalArt.Item("store_" + product.Id));
        _art.TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        if (owned) _art.SelfModulate = new Color(.62f, .62f, .64f);
        _title = Caps(product.DisplayName, wide ? 15 : 13, TitleInk, 700);
        _title.Align = wide ? HorizontalAlignment.Left : HorizontalAlignment.Center;
        AddChild(_title);
        if (wide) BuildContents();
        else BuildAmount();
        if (_bestValue || (product.OneTimePurchase && !owned)) BuildRibbon(_bestValue ? "Best value" : "Once per account");
        Buy = new RoyalButton { Name = "Buy", FocusMode = FocusModeEnum.All, MouseDefaultCursorShape = CursorShape.PointingHand };
        Buy.SetStates(Kit("button-gold", 8), 6);
        Buy.SetMeta("store_product_id", product.Id);
        Buy.Disabled = owned;
        Buy.Pressed += () => buy?.Invoke(this);
        AddChild(Buy);
        SetConfirming(false);
        Resized += Layout;
    }

    private TextureRect Picture(Texture2D texture)
    {
        var picture = new TextureRect { Texture = texture, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(picture);
        return picture;
    }

    /// <summary>Engraved capitals at a modal size; the 1.55x canvas keeps 11 units above the phone floor for caps.</summary>
    internal static RoyalLabel Caps(string text, int size, Color ink, int weight = 600)
    {
        var label = RoyalText.Caps(text, size, ink, weight);
        label.FontSize = size;
        label.MinimumFontSize = Math.Min(size, 11);
        label.AllCaps = true;
        return label;
    }

    /// <summary>Book serif at a modal size (12 units clears the 18 px phone floor for mixed case).</summary>
    internal static Label Serif(string text, int size, Color ink, int weight = 500)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontOverride("font", RoyalFonts.Body(weight));
        label.AddThemeFontSizeOverride("font_size", Math.Max(12, size + (GameState.Instance?.FontSizeOffset ?? 0)));
        label.AddThemeColorOverride("font_color", ink);
        label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, .45f));
        label.AddThemeConstantOverride("shadow_offset_x", 0);
        label.AddThemeConstantOverride("shadow_offset_y", 1);
        label.AddThemeConstantOverride("line_spacing", -2);
        return label;
    }

    private void BuildAmount()
    {
        _amountIcon = Picture(HomeMapArt.Icon(Product.CurrencyType == "food" ? "food" : "gold"));
        _amount = RoyalText.Title(Product.TotalCurrencyAmount.ToString("N0"), 19);
        _amount.MinimumFontSize = 12;
        AddChild(_amount);
        if (Product.BonusAmount <= 0) return;
        _bonus = Caps($"+{Product.BonusAmount:N0} bonus", 11, new Color("9fd08a"), 700);
        _bonus.Align = HorizontalAlignment.Center;
        AddChild(_bonus);
    }

    /// <summary>A bundle lists each thing it holds on its own line, with the picture of what it is.</summary>
    private void BuildContents()
    {
        _contents = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _contents.AddThemeConstantOverride("separation", 2);
        AddChild(_contents);
        void Line(Texture2D icon, string text, Color? tint = null)
        {
            var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(0, 22) };
            row.AddThemeConstantOverride("separation", 8);
            row.AddChild(new TextureRect { Texture = icon, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = new Vector2(22, 22), MouseFilter = MouseFilterEnum.Ignore, SelfModulate = tint ?? Colors.White,
                TextureFilter = TextureFilterEnum.LinearWithMipmaps });
            var label = Serif(text, 14, BodyInk, 600);
            label.AutowrapMode = TextServer.AutowrapMode.Off;
            label.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(label);
            _contents.AddChild(row);
        }
        if (Product.GoldAmount > 0) Line(HomeMapArt.Icon("gold"), $"{Product.GoldAmount:N0} gold");
        if (Product.FoodAmount > 0) Line(HomeMapArt.Icon("food"), $"{Product.FoodAmount:N0} food");
        if (Product.GrantsUnitUnlock) Line(RoyalKit.Texture("store-unit"), "A new unit", new Color("f3deaa"));
        // The column's width wraps the description, so it lives in the column below the contents.
        _contents.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4), MouseFilter = MouseFilterEnum.Ignore });
        _contents.AddChild(Serif(Product.Description, 12, MutedInk));
    }

    private void BuildRibbon(string text)
    {
        _ribbon = new Control { Name = "Ribbon", MouseFilter = MouseFilterEnum.Ignore };
        var cloth = new Panel { MouseFilter = MouseFilterEnum.Ignore };
        cloth.AddThemeStyleboxOverride("panel", Ribbon(RibbonHeight));
        cloth.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _ribbon.AddChild(cloth);
        var label = _ribbonLabel = Caps(text, 11, new Color("fff1d2"), 700);
        label.Align = HorizontalAlignment.Center;
        label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        // The swallowtails and the cloth's folds are not lettering room.
        label.OffsetLeft = RibbonTail; label.OffsetRight = -RibbonTail; label.OffsetTop = -1;
        _ribbon.AddChild(label);
        AddChild(_ribbon);
    }

    /// <summary>The first tap lights the card and turns its button into the confirmation.</summary>
    public void SetConfirming(bool confirming)
    {
        Confirming = confirming;
        _frame.AddThemeStyleboxOverride("panel", confirming ? Kit("ach-card-ready", 11) : Kit("ach-card", 9));
        ArrangeBuy();
    }

    private void ArrangeBuy()
    {
        var verb = Confirming ? "Confirm" : "Buy";
        if (Buy.Size.X > 0 && _owned)
        {
            Buy.Arrange(Caps("Purchased", 13, ButtonInk, 700), RoyalKit.Texture("icon-check"), new Vector2(16, 16), gap: 8);
            Buy.Glyph.SelfModulate = ButtonInk;
        }
        else if (Buy.Size.X > 0)
        {
            // Caption and price share one row; both step down together if a long price would crowd the rim.
            var caption = Caps(verb, 14, ButtonInk, 700);
            var price = Caps(_price, 14, ButtonInk, 700);
            while (caption.FontSize > 11 && caption.TextWidth(caption.FontSize) + price.TextWidth(price.FontSize) + 2 * 12 + 2 > Buy.Size.X - 20)
                caption.FontSize = price.FontSize = caption.FontSize - 1;
            Buy.Arrange(caption, price: price, rule: 12);
        }
        // The caption is drawn; the button's own text names it for accessibility and the reviews.
        Buy.Text = _owned ? "Purchased" : $"{verb} — {_price}";
        Buy.AccessibilityName = Buy.TooltipText = _owned ? $"{Product.DisplayName} purchased" : $"{verb} {Product.DisplayName} for {_price}";
    }

    private void Layout()
    {
        if (Size.X <= 0 || Size.Y <= 0) return;
        _frame.Position = Vector2.Zero; _frame.Size = Size;
        const float Inset = 12;
        float width = Size.X, height = Size.Y;
        Rect2 well;
        if (_wide)
        {
            var artWidth = Mathf.Round(Mathf.Min(height - 20, width * .44f));
            well = new Rect2(Inset, 10, artWidth, height - 20);
            var left = well.End.X + 12;
            var column = width - left - Inset;
            Place(_title, new Rect2(left, 10, column, 28));
            var buttonTop = height - 50;
            Place(_contents, new Rect2(left, 40, column, buttonTop - 46));
            Place(Buy, new Rect2(left, buttonTop, column, 40));
        }
        else
        {
            Place(_title, new Rect2(Inset, 8, width - 2 * Inset, 26));
            well = new Rect2(Inset, 34, width - 2 * Inset, height - 34 - 102);
            var amountTop = well.End.Y + 2;
            var number = _amount.TextWidth(_amount.FontSize);
            const float Icon = 24, Gap = 6;
            var left = Mathf.Round((width - Icon - Gap - number) / 2);
            Place(_amountIcon, new Rect2(left, amountTop + 1, Icon, Icon));
            Place(_amount, new Rect2(left + Icon + Gap, amountTop, number + 4, 26));
            if (_bonus != null) Place(_bonus, new Rect2(Inset, amountTop + 26, width - 2 * Inset, 18));
            Place(Buy, new Rect2(Inset, height - 52, width - 2 * Inset, 40));
        }
        // A pool of warm light behind the picture, and a soft contact shadow under it.
        Place(_glow, well.Grow(6));
        var art = well.Grow(-2);
        Place(_art, art);
        Place(_shadow, new Rect2(art.Position.X + art.Size.X * .14f, art.End.Y - art.Size.Y * .16f, art.Size.X * .72f, art.Size.Y * .2f));
        if (_ribbon != null)
        {
            var ribbonWidth = Mathf.Min(well.Size.X + 8, Mathf.Ceil(_ribbonLabel.TextWidth(_ribbonLabel.FontSize)) + 2 * RibbonTail + 12);
            Place(_ribbon, new Rect2(well.GetCenter().X - ribbonWidth / 2, well.Position.Y - (_wide ? -2 : 6), ribbonWidth, RibbonHeight));
        }
        ArrangeBuy();
    }

    private static void Place(Control control, Rect2 rect) { control.Position = rect.Position; control.Size = rect.Size; }

    /// <summary>A kit piece at its concept-screen size on the modal canvas; margins are modal units.</summary>
    internal static SliceStyle Kit(string name, float margin, bool center = true) => RoyalKit.Scaled(name, MobilePresentation.HudScale, margin, center);

    private const float RibbonHeight = 23, RibbonTail = 20;

    /// <summary>The painted ribbon at a given height: its swallowtails keep their shape and the band stretches.</summary>
    private static SliceStyle Ribbon(float height)
    {
        var texture = RoyalKit.Texture("store-ribbon");
        var density = texture.GetHeight() / height;
        var tail = 52 / density;
        return new SliceStyle(texture, new Rect2(Vector2.Zero, texture.GetSize() / density), density, tail, 0, tail, 0, 0, 0);
    }

    private static readonly System.Collections.Generic.Dictionary<string, GradientTexture2D> Glows = new();

    private static GradientTexture2D Glow(string category)
    {
        if (Glows.TryGetValue(category, out var glow)) return glow;
        var tint = category switch { "food" => new Color(.98f, .8f, .45f), "mixed" => new Color(.45f, .78f, .9f), _ => new Color(1f, .78f, .36f) };
        var gradient = new Gradient { Offsets = new[] { 0f, .55f, 1f }, Colors = new[] { new Color(tint, .62f), new Color(tint, .2f), new Color(tint, 0) } };
        return Glows[category] = new GradientTexture2D { Gradient = gradient, Fill = GradientTexture2D.FillEnum.Radial, Width = 256, Height = 256,
            FillFrom = new Vector2(.5f, .55f), FillTo = new Vector2(.5f, .02f) };
    }

    private static GradientTexture2D _shadowTexture;
    private static GradientTexture2D Shadow => _shadowTexture ??= new GradientTexture2D
    {
        Gradient = new Gradient { Offsets = new[] { 0f, 1f }, Colors = new[] { new Color(0, 0, 0, .55f), new Color(0, 0, 0, 0) } },
        Fill = GradientTexture2D.FillEnum.Radial, Width = 128, Height = 64, FillFrom = new Vector2(.5f, .5f), FillTo = new Vector2(.5f, 0f)
    };
}
