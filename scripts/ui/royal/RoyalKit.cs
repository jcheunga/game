using Godot;

/// <summary>Pieces cut from the concepts by art/royal/cuts.py (assets/ui/royal/kit), at concept density.</summary>
public static class RoyalKit
{
    /// <summary>Kit pixels per canvas unit (the concepts are 1672 wide; the canvas is 1280).</summary>
    public const float Density = 1672f / 1280f;

    /// <summary>A kit piece, falling back to the monochrome navigation icon of the same name.</summary>
    public static Texture2D Texture(string name)
    {
        var path = $"res://assets/ui/royal/kit/{name}.png";
        if (ResourceLoader.Exists(path)) return RoyalArt.Load(path);
        var icon = $"res://assets/ui/icons/navigation/{name}.svg";
        return ResourceLoader.Exists(icon) ? RoyalArt.Load(icon) : RoyalArt.Load(path);
    }

    /// <summary>An icon or ornament fitted (aspect kept) into a canvas rect.</summary>
    public static TextureRect Image(string name, Rect2 rect, Color? tint = null)
    {
        // ExpandMode must be set before Size, or the size is clamped to the texture's own.
        return new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Texture = Texture(name), Position = rect.Position, Size = rect.Size, MouseFilter = Control.MouseFilterEnum.Ignore,
            SelfModulate = tint ?? Colors.White, TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps };
    }

    /// <summary>A kit piece drawn as a nine-slice; margins are canvas units.</summary>
    public static SliceStyle Slice(string name, float margin, bool center = true) => Slice(name, margin, margin, margin, margin, center);

    public static SliceStyle Slice(string name, float left, float top, float right, float bottom, bool center = true)
    {
        var texture = Texture(name);
        var size = texture == null ? Vector2.One : texture.GetSize() / Density;
        var style = new SliceStyle(texture, new Rect2(Vector2.Zero, size), Density, left, top, right, bottom, left, top) { DrawCenter = center };
        return style;
    }

    /// <summary>A panel that draws a kit slice behind its children.</summary>
    public static Panel Frame(string name, Rect2 rect, float margin, bool center = true)
    {
        var panel = new Panel { Position = rect.Position, Size = rect.Size, MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", Slice(name, margin, center));
        return panel;
    }

    /// <summary>A row of level diamonds: filled for earned levels.</summary>
    public static Control Pips(Vector2 at, int filled, int total, float pitch = 16.5f, float size = 13f)
    {
        var row = new Control { Position = at, Size = new Vector2(pitch * total, size), MouseFilter = Control.MouseFilterEnum.Ignore };
        for (var i = 0; i < total; i++)
            row.AddChild(Image(i < filled ? "pip-on" : "pip-off", new Rect2(i * pitch, 0, size, size)));
        return row;
    }
}
