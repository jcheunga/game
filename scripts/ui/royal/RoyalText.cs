using Godot;

/// <summary>Concept type styles, named after where they appear.</summary>
public static class RoyalText
{
    public static readonly Color Cream = new("f2e6cc");
    public static readonly Color Muted = new("b9ad96");

    /// <summary>Large engraved gold title ("WARBAND", "VICTORY").</summary>
    public static RoyalLabel Title(string text, int size)
    {
        return new RoyalLabel { Text = text, Font = RoyalFonts.Display(700), FontSize = size, CapRatio = .70f, Gold = true,
            OutlineSize = Mathf.Max(2, size / 14), OutlineInk = new Color("22140a"), ShadowOffset = new Vector2(0, Mathf.Max(2, size / 16)), ShadowInk = new Color(0, 0, 0, .6f) };
    }

    /// <summary>Engraved capitals for captions, tab and button labels ("SQUAD 5 / 6", "HEALTH").</summary>
    public static RoyalLabel Caps(string text, int size, Color? ink = null, int weight = 600)
    {
        return new RoyalLabel { Text = text, Font = RoyalFonts.Display(weight), FontSize = size, CapRatio = .70f, Ink = ink ?? Cream,
            ShadowOffset = new Vector2(0, 1), ShadowInk = new Color(0, 0, 0, .5f) };
    }

    /// <summary>Book serif for names, numbers and short descriptions ("Archer", "48").</summary>
    public static RoyalLabel Serif(string text, int size, Color? ink = null, int weight = 600)
    {
        return new RoyalLabel { Text = text, Font = RoyalFonts.Body(weight), FontSize = size, CapRatio = .58f, Ink = ink ?? Cream,
            ShadowOffset = new Vector2(0, 1), ShadowInk = new Color(0, 0, 0, .55f) };
    }

    /// <summary>Wrapping description text in the book serif.</summary>
    public static Label Paragraph(string text, int size, Color? ink = null, int weight = 500, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, HorizontalAlignment = align, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontOverride("font", RoyalFonts.Body(weight));
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", ink ?? Cream);
        label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, .45f));
        label.AddThemeConstantOverride("shadow_offset_x", 0);
        label.AddThemeConstantOverride("shadow_offset_y", 1);
        label.AddThemeConstantOverride("line_spacing", -Mathf.RoundToInt(size * .18f));
        return label;
    }

    /// <summary>Shrinks a wrapping paragraph until it fits the given number of lines in its width.</summary>
    public static Label FitLines(Label label, int lines, int minimumSize = 12)
    {
        var font = label.GetThemeFont("font");
        var size = label.GetThemeFontSize("font_size");
        while (size > minimumSize)
        {
            var height = font.GetMultilineStringSize(label.Text, HorizontalAlignment.Left, label.Size.X, size).Y;
            if (height <= font.GetHeight(size) * lines + 1) break;
            size--;
        }
        label.AddThemeFontSizeOverride("font_size", size);
        label.MaxLinesVisible = lines;
        var boxHeight = label.Size.Y;
        label.CustomMinimumSize = new Vector2(label.Size.X, 0);
        label.Size = new Vector2(label.Size.X, boxHeight);
        return label;
    }

    /// <summary>Places a control at a rect in its parent's (canvas) coordinates.</summary>
    public static T Place<T>(Control parent, T child, Rect2 rect) where T : Control
    {
        if (child.GetParent() == null) parent.AddChild(child);
        child.Position = rect.Position;
        // Wrapping labels size their minimum height from their width, so pin the width first.
        if (child is Label { AutowrapMode: not TextServer.AutowrapMode.Off }) child.CustomMinimumSize = new Vector2(rect.Size.X, 0);
        child.Size = rect.Size;
        // A wrapped label's cached minimum height still reflects its old width; settle it next frame.
        if (child is Label) Callable.From(() => { if (GodotObject.IsInstanceValid(child)) child.Size = rect.Size; }).CallDeferred();
        return child;
    }

    public static T Place<T>(Control parent, T child, float x, float y, float width, float height) where T : Control => Place(parent, child, new Rect2(x, y, width, height));
}
