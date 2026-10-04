using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class UiReviewSmoke
{
    // Icons and text must sit inside a frame's drawn rim with a little air, never on it.
    private const float FrameAir = 2;

    private void AuditFrameClearance(string screen)
    {
        var reported = 0;
        foreach (var frame in Walk(LiveUiReview.ActiveRoot(GetTree())).OfType<Control>()
            .Where(control => control.IsVisibleInTree() && control is PanelContainer or Button))
        {
            var band = FrameBand(frame);
            if (band <= 0) continue;
            var inner = new Rect2(Vector2.Zero, frame.Size).Grow(-(band + FrameAir));
            var toFrame = frame.GetGlobalTransform().AffineInverse();
            foreach (var (content, global) in FrameContent(frame))
            {
                var local = toFrame * global;
                if (inner.Grow(.5f).Encloses(local)) continue;
                var gap = Mathf.Min(Mathf.Min(local.Position.X, local.Position.Y), Mathf.Min(frame.Size.X - local.End.X, frame.Size.Y - local.End.Y));
                _failures++;
                if (reported++ < 40)
                    GD.Print($"FRAME_CLEARANCE {screen}: {Describe(content)} is {gap:0.#}px from the edge of {Describe(frame)} (rim {band}px)");
            }
        }
        if (reported > 40) GD.Print($"FRAME_CLEARANCE {screen}: {reported - 40} more");
    }

    private static string Describe(Control control) => control switch
    {
        Label label => $"label \"{Shorten(label.Text)}\"",
        Button button => $"{button.GetType().Name} \"{Shorten(button.Text.Length > 0 ? button.Text : button.TooltipText)}\"",
        TextureRect => $"icon in {control.GetParent()?.Name}",
        _ => $"{control.GetType().Name} {control.Name}"
    };

    private static string Shorten(string text) => text.Length > 28 ? text[..28] + "…" : text.Replace('\n', ' ');

    private static float FrameBand(Control control)
    {
        if (control.HasMeta("frame_bleed")) return 0;
        if (control.HasMeta("frame_inset")) return (float)control.GetMeta("frame_inset");
        StyleBox style;
        if (control is PanelContainer) style = control.GetThemeStylebox("panel");
        else if (control is Button { Flat: true }) return 0;
        else if (control is Button button)
            style = button.GetThemeStylebox(button.Disabled ? "disabled" : button.ToggleMode && button.ButtonPressed ? "pressed" : "normal");
        else return 0;
        return style switch
        {
            ModalSurface surface => surface.FrameInset,
            HomeMapFrame => HomeMapFrame.FrameInset,
            StyleBoxTexture => 4, // Engraved art: a 1.5px rim stroke inside a 2px shadow edge.
            StyleBoxFlat flat when flat.BorderColor.A > .1f => Mathf.Max(flat.BorderWidthLeft, flat.BorderWidthTop),
            _ => 0
        };
    }

    // Drawn extents, in global coordinates, of everything a frame visibly contains.
    private IEnumerable<(Control Content, Rect2 Global)> FrameContent(Control frame)
    {
        if (frame is Button button && !(button is RealmButton { CenterIconAndText: true }) && button.GetChildCount() == 0)
            foreach (var part in ButtonContent(button)) yield return part;
        foreach (var child in frame.GetChildren())
            foreach (var part in VisibleContent(child)) yield return part;
    }

    private IEnumerable<(Control, Rect2)> VisibleContent(Node node)
    {
        if (node is CanvasLayer || node is not Control { Visible: true } control || control.HasMeta("frame_bleed")) yield break;
        var xform = control.GetGlobalTransform();
        Rect2 Global(Rect2 local) => xform * local;
        switch (control)
        {
            case ScrollContainer or PanelContainer or Button when control is not Button { Flat: true }:
                // Nested frames and scroll views are measured as a whole; their own content is checked separately.
                if (control is ScrollContainer || FrameBand(control) > 0 || control is Button) { yield return (control, Global(new Rect2(Vector2.Zero, control.Size))); yield break; }
                break;
            case Label label when !string.IsNullOrWhiteSpace(label.Text):
                yield return (label, Global(TextExtent(label)));
                yield break;
            case TextureRect { Texture: not null } texture:
                yield return (texture, Global(DrawnExtent(texture)));
                yield break;
            case Control { ClipContents: true }:
                yield return (control, Global(new Rect2(Vector2.Zero, control.Size)));
                yield break;
        }
        foreach (var child in control.GetChildren())
            foreach (var part in VisibleContent(child)) yield return part;
    }

    private static Rect2 TextExtent(Label label)
    {
        var font = label.GetThemeFont("font");
        var size = label.GetThemeFontSize("font_size");
        var widest = label.Text.Split('\n').Max(line => font.GetStringSize(line, HorizontalAlignment.Left, -1, size).X);
        var width = label.AutowrapMode == TextServer.AutowrapMode.Off ? widest : Mathf.Min(widest, label.Size.X);
        var x = label.HorizontalAlignment switch
        {
            HorizontalAlignment.Center => (label.Size.X - width) / 2,
            HorizontalAlignment.Right => label.Size.X - width,
            _ => 0f
        };
        var lineHeight = font.GetHeight(size);
        var height = Mathf.Min(label.Size.Y, Mathf.Max(1, label.GetVisibleLineCount()) * lineHeight);
        var y = label.VerticalAlignment switch
        {
            VerticalAlignment.Center => (label.Size.Y - height) / 2,
            VerticalAlignment.Bottom => label.Size.Y - height,
            _ => 0f
        };
        // Line boxes include leading above capitals and room for descenders; measure the glyphs themselves.
        var leading = font.GetAscent(size) * .22f;
        var descenders = label.Text.IndexOfAny("gjpqy,;()".ToCharArray()) >= 0;
        var bottom = descenders ? height : height - lineHeight + font.GetAscent(size);
        return new Rect2(x, y + leading, width, Mathf.Max(1, bottom - leading));
    }

    private static Rect2 DrawnExtent(TextureRect texture)
    {
        var box = new Rect2(Vector2.Zero, texture.Size);
        if (texture.StretchMode is not (TextureRect.StretchModeEnum.KeepAspectCentered or TextureRect.StretchModeEnum.KeepAspect or TextureRect.StretchModeEnum.KeepCentered)) return box;
        var source = texture.Texture.GetSize();
        if (source.X <= 0 || source.Y <= 0) return box;
        var scale = texture.StretchMode == TextureRect.StretchModeEnum.KeepCentered ? 1 : Mathf.Min(box.Size.X / source.X, box.Size.Y / source.Y);
        var drawn = source * scale;
        return new Rect2((box.Size - drawn) / 2, drawn);
    }

    // Godot draws a plain button's icon at the content edge and its text beside it.
    private static IEnumerable<(Control, Rect2)> ButtonContent(Button button)
    {
        var style = button.GetThemeStylebox("normal");
        var left = style.GetContentMargin(Side.Left);
        var right = button.Size.X - style.GetContentMargin(Side.Right);
        var xform = button.GetGlobalTransform();
        var iconWidth = 0f;
        if (button.Icon != null)
        {
            var max = button.GetThemeConstant("icon_max_width");
            iconWidth = max > 0 ? Mathf.Min(max, button.Icon.GetWidth()) : button.Icon.GetWidth();
            var x = button.IconAlignment == HorizontalAlignment.Right ? right - iconWidth : left;
            if (button.IconAlignment != HorizontalAlignment.Center)
                yield return (button, xform * new Rect2(x, button.Size.Y / 2 - 1, iconWidth, 2));
        }
        if (button.Text.Length > 0 && button.Alignment == HorizontalAlignment.Left)
        {
            var x = left + (button.Icon != null && button.IconAlignment == HorizontalAlignment.Left ? iconWidth + button.GetThemeConstant("h_separation") : 0);
            yield return (button, xform * new Rect2(x, button.Size.Y / 2 - 1, 1, 2));
        }
    }
}
