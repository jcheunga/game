using System.Linq;
using Godot;

public enum ModalMaterial { Wood, Inset, Steel, Gold, Ruby, Tab, Portrait, Arcane, Forge, Paper }

/// <summary>Scalable bevelled frames around painted surfaces. No baked text or fixed-size UI.</summary>
public partial class ModalSurface : StyleBox
{
    private ModalMaterial _material;
    private Color _accent = new("b09a69");
    private bool _active, _pressed;
    private StyleBoxFlat _shadow, _inset;
    public ModalSurface() { }
    public ModalSurface(ModalMaterial material, int padding = 12, Color? accent = null, bool active = false, bool pressed = false)
    {
        _material = material; _accent = accent ?? _accent; _active = active; _pressed = pressed;
        ContentMarginLeft = ContentMarginRight = padding;
        ContentMarginTop = ContentMarginBottom = padding;
        _shadow = new StyleBoxFlat { BgColor = new Color("15101a"), ShadowColor = new Color("09060c99"), ShadowSize = 4, ShadowOffset = new Vector2(0, 4) };
        _shadow.SetCornerRadiusAll(5);
        _inset = new StyleBoxFlat { BgColor = Colors.Transparent, BorderColor = new Color(material == ModalMaterial.Paper ? "c6a36a80" : "ecdbb650") };
        _inset.SetBorderWidthAll(1); _inset.SetCornerRadiusAll(2);
    }
    public override void _Draw(Rid canvasItem, Rect2 rect)
    {
        if (_shadow == null) return;
        _shadow.Draw(canvasItem, rect);
        bool paper = _material == ModalMaterial.Paper;
        bool warm = _material is ModalMaterial.Gold or ModalMaterial.Forge;
        var rim = new Color(paper ? "8d6333" : warm ? "9c8356" : "85857a");
        Polygon(canvasItem, rect, rim.Lightened(.06f), rim.Darkened(.48f));
        var face = rect.Grow(-4);
        var pigment = _accent.Lerp(new Color("6d6457"), .35f);
        var (top, bottom) = _material switch {
            ModalMaterial.Steel => (new Color("77776f"), new Color("41443f")),
            ModalMaterial.Gold => (new Color("cfb276"), new Color("977440")),
            ModalMaterial.Ruby => (new Color("905d58"), new Color("573b37")),
            ModalMaterial.Tab => (_active ? pigment.Lightened(.07f) : pigment.Lerp(new Color("504b42"), .55f), pigment.Darkened(_active ? .38f : .58f)),
            ModalMaterial.Portrait => (pigment, pigment.Darkened(.65f)),
            ModalMaterial.Paper => (new Color("f7e2ac"), new Color("dcc18a")),
            ModalMaterial.Arcane => (new Color("514078"), new Color("251d3c")),
            _ => (new Color("493b31"), new Color("251c1d"))
        };
        if (_active && _material == ModalMaterial.Steel) { top = top.Lightened(.04f); bottom = bottom.Lightened(.04f); }
        if (_pressed) { top = top.Darkened(.15f); bottom = bottom.Darkened(.12f); }
        Polygon(canvasItem, face, top, bottom);
        var textureIndex = _material switch { ModalMaterial.Wood => 0, ModalMaterial.Arcane => 1, ModalMaterial.Paper => 2, ModalMaterial.Forge => 3, _ => -1 };
        if (textureIndex >= 0 && ModalArt.Material(textureIndex) is AtlasTexture texture)
        {
            var shade = paper ? Colors.White : new Color(_material == ModalMaterial.Wood ? .64f : .78f, _material == ModalMaterial.Wood ? .64f : .78f, _material == ModalMaterial.Wood ? .64f : .78f);
            RenderingServer.CanvasItemAddTextureRectRegion(canvasItem, face.Grow(-2), texture.Atlas.GetRid(), texture.Region, shade, false, true);
        }
        _inset.Draw(canvasItem, rect.Grow(-7));
        RenderingServer.CanvasItemAddLine(canvasItem, rect.Position + new Vector2(8, 2), new Vector2(rect.End.X - 8, rect.Position.Y + 2), new Color("d8d2bc80"), 1, true);
        if (_active || _material == ModalMaterial.Gold)
            RenderingServer.CanvasItemAddLine(canvasItem, rect.Position + new Vector2(9, rect.Size.Y - 3), rect.End - new Vector2(9, 3), new Color("c7ac71"), 2, true);
        if (rect.Size.X > 80 && !paper)
            foreach (var point in new[] { rect.Position + new Vector2(7, 7), new Vector2(rect.End.X - 7, rect.Position.Y + 7), rect.End - new Vector2(7, 7), new Vector2(rect.Position.X + 7, rect.End.Y - 7) })
            {
                RenderingServer.CanvasItemAddCircle(canvasItem, point, 2.6f, new Color("22212d"));
                RenderingServer.CanvasItemAddCircle(canvasItem, point - new Vector2(.3f, .8f), 1.5f, new Color("bbb8a8"));
            }
    }
    private static void Polygon(Rid canvas, Rect2 rect, Color top, Color bottom)
    {
        if (rect.Size.X <= 0 || rect.Size.Y <= 0) return;
        float cut = Mathf.Min(6, Mathf.Min(rect.Size.X, rect.Size.Y) / 4);
        var points = new[] { rect.Position + new Vector2(cut, 0), new Vector2(rect.End.X - cut, rect.Position.Y), new Vector2(rect.End.X, rect.Position.Y + cut), rect.End - new Vector2(0, cut), rect.End - new Vector2(cut, 0), new Vector2(rect.Position.X + cut, rect.End.Y), new Vector2(rect.Position.X, rect.End.Y - cut), rect.Position + new Vector2(0, cut) };
        RenderingServer.CanvasItemAddPolygon(canvas, points, points.Select(p => top.Lerp(bottom, Mathf.Clamp((p.Y - rect.Position.Y) / Mathf.Max(1, rect.Size.Y), 0, 1))).ToArray());
    }
}
