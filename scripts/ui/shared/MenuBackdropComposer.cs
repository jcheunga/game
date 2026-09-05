using Godot;

public sealed class MenuBackdropSet
{
    public ColorRect PrimaryRect { get; init; }
    public ColorRect SecondaryRect { get; init; }
    public ColorRect AccentBand { get; init; }
    public TextureRect BackgroundTexture { get; init; }
    public ColorRect TextureScrim { get; init; }

    public void SetTexture(Texture2D texture)
    {
        var hasTexture = texture != null;
        BackgroundTexture.Texture = texture;
        BackgroundTexture.Visible = hasTexture;
        TextureScrim.Visible = hasTexture;
    }
}

public static class MenuBackdropComposer
{
    public static MenuBackdropSet AddSolidBackdrop(Control root, string screenId, Color fallbackColor, string variantId = "")
    {
        MedievalUi.Apply(root);
        var primaryRect = new ColorRect
        {
            Color = fallbackColor
        };
        primaryRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(primaryRect);
        MedievalUi.MarkBackdrop(primaryRect);

        var textureRect = BuildTextureRect();
        root.AddChild(textureRect);
        MedievalUi.MarkBackdrop(textureRect);

        var textureScrim = BuildTextureScrim(0.24f);
        root.AddChild(textureScrim);
        MedievalUi.MarkBackdrop(textureScrim);

        var set = new MenuBackdropSet
        {
            PrimaryRect = primaryRect,
            BackgroundTexture = textureRect,
            TextureScrim = textureScrim
        };
        set.SetTexture(UiTextureLoader.TryLoadScreenBackground(screenId, variantId));
        return set;
    }

    public static MenuBackdropSet AddSplitBackdrop(Control root, string screenId, Color topColor, Color bottomColor, Color accentColor, float accentY, string variantId = "")
    {
        MedievalUi.Apply(root);
        var topRect = new ColorRect
        {
            Color = topColor,
            Position = Vector2.Zero,
            Size = new Vector2(1280f, 360f)
        };
        root.AddChild(topRect);
        MedievalUi.MarkBackdrop(topRect);

        var bottomRect = new ColorRect
        {
            Color = bottomColor,
            Position = new Vector2(0f, 360f),
            Size = new Vector2(1280f, 360f)
        };
        root.AddChild(bottomRect);
        MedievalUi.MarkBackdrop(bottomRect);

        var textureRect = BuildTextureRect();
        root.AddChild(textureRect);
        MedievalUi.MarkBackdrop(textureRect);

        var textureScrim = BuildTextureScrim(0.28f);
        root.AddChild(textureScrim);
        MedievalUi.MarkBackdrop(textureScrim);

        var accentBand = new ColorRect
        {
            Color = accentColor,
            Position = new Vector2(0f, accentY),
            Size = new Vector2(1280f, 6f)
        };
        root.AddChild(accentBand);
        MedievalUi.MarkBackdrop(accentBand);

        var set = new MenuBackdropSet
        {
            PrimaryRect = topRect,
            SecondaryRect = bottomRect,
            AccentBand = accentBand,
            BackgroundTexture = textureRect,
            TextureScrim = textureScrim
        };
        set.SetTexture(UiTextureLoader.TryLoadScreenBackground(screenId, variantId));
        return set;
    }

    private static TextureRect BuildTextureRect()
    {
        var textureRect = new TextureRect
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered
        };
        textureRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        textureRect.Visible = false;
        return textureRect;
    }

    private static ColorRect BuildTextureScrim(float alpha)
    {
        var scrim = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, alpha),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
        scrim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        return scrim;
    }
}
