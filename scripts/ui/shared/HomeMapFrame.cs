using Godot;

/// <summary>The concept's dark wood and brass frame (the modal-frame kit piece), lit when highlighted.</summary>
public partial class HomeMapFrame : StyleBox
{
    private readonly bool _highlighted;
    private static SliceStyle _frame;

    public HomeMapFrame() { }

    /// <summary>Width of the drawn rim and inner line; content belongs inside it.</summary>
    public const float FrameInset = 7;
    public HomeMapFrame(bool highlighted, int padding)
    {
        _highlighted = highlighted;
        ContentMarginLeft = ContentMarginRight = padding;
        ContentMarginTop = ContentMarginBottom = padding;
    }

    public override void _Draw(Rid canvasItem, Rect2 rect)
    {
        _frame ??= RoyalKit.Slice("modal-frame", 16);
        _frame.Draw(canvasItem, rect);
        if (!_highlighted) return;
        var glow = new StyleBoxFlat { BgColor = new Color(1f, .82f, .45f, .07f), BorderColor = new Color("f0c56f"), AntiAliasing = true };
        glow.SetBorderWidthAll(2); glow.SetCornerRadiusAll(6);
        glow.Draw(canvasItem, rect.Grow(-2));
    }
}
