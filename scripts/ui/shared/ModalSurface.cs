using Godot;

public enum ModalMaterial { Wood, Inset, Steel, Gold, Ruby, Tab, Portrait, Arcane, Forge, Paper }

/// <summary>The concept chrome around a surface: each material is a nine-sliced kit piece (art/royal/chrome.py).</summary>
public partial class ModalSurface : StyleBox
{
    private readonly ModalMaterial _material;
    private readonly bool _active, _pressed;
    private readonly SliceStyle _chrome;
    public ModalSurface() { }

    /// <summary>Width of the drawn rim and inner bevel line; content belongs inside it.</summary>
    public float FrameInset => _material == ModalMaterial.Portrait ? 4 : 8;
    public const int MinimumSideInset = 12, MinimumEndInset = 10;
    public ModalSurface(ModalMaterial material, int padding = 12, bool active = false, bool pressed = false)
    {
        _material = material; _active = active; _pressed = pressed;
        // Content always clears the rim and bevel with some air; portrait art may meet its thinner rim.
        var portrait = material == ModalMaterial.Portrait;
        ContentMarginLeft = ContentMarginRight = portrait ? padding : Mathf.Max(padding, MinimumSideInset);
        ContentMarginTop = ContentMarginBottom = portrait ? padding : Mathf.Max(padding, MinimumEndInset);
        _chrome = material switch
        {
            ModalMaterial.Wood => KitSlice("modal-frame", 22),
            ModalMaterial.Steel => KitSlice("button-navy", 14),
            ModalMaterial.Gold => KitSlice("button-gold", 14),
            ModalMaterial.Ruby => KitSlice("close-button", 14),
            ModalMaterial.Tab => KitSlice(active ? "tab-selected" : "tab", 12),
            ModalMaterial.Paper => KitSlice("paper", 40),
            _ => KitSlice("tile", 12)
        };
    }
    private static readonly System.Collections.Generic.Dictionary<string, SliceStyle> Kit = new();

    private static SliceStyle KitSlice(string name, float margin)
    {
        var key = name + margin;
        if (!Kit.TryGetValue(key, out var slice)) Kit[key] = slice = RoyalKit.Slice(name, margin);
        return slice;
    }

    public override void _Draw(Rid canvasItem, Rect2 rect)
    {
        if (_chrome == null) return;
        _chrome.Draw(canvasItem, rect);
        if (_active && _material is not (ModalMaterial.Tab or ModalMaterial.Wood or ModalMaterial.Paper))
        {
            // Selected cards and hovered buttons take the concept's warm gold edge.
            var glow = new StyleBoxFlat { BgColor = new Color(1f, .82f, .45f, .06f), BorderColor = new Color("f0c56f"), AntiAliasing = true };
            glow.SetBorderWidthAll(2); glow.SetCornerRadiusAll(5);
            glow.Draw(canvasItem, rect.Grow(-1));
        }
        if (_pressed) RenderingServer.CanvasItemAddRect(canvasItem, rect.Grow(-3), new Color(0, 0, 0, .18f));
    }
}
