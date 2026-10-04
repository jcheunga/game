using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>Layered enamel and brass, drawn at the final control size.</summary>
public partial class HomeMapFrame : StyleBox
{
    private readonly bool _highlighted;
    private readonly int _radius;
    private StyleBoxFlat _rim, _inset;

    public HomeMapFrame() { }

    /// <summary>Width of the drawn rim and inner line; content belongs inside it.</summary>
    public const float FrameInset = 7;
    public HomeMapFrame(bool highlighted, int padding, int radius)
    {
        _highlighted = highlighted;
        _radius = radius;
        ContentMarginLeft = ContentMarginRight = padding;
        ContentMarginTop = ContentMarginBottom = padding;
        _rim = new StyleBoxFlat
        {
            BgColor = new Color("13232a"), BorderColor = new Color(highlighted ? "e5c581" : "a18a62"),
            ShadowColor = new Color("09171c99"), ShadowSize = 6, ShadowOffset = new Vector2(0, 4)
        };
        _rim.SetCornerRadiusAll(radius);
        _rim.SetBorderWidthAll(2);
        _inset = new StyleBoxFlat { BgColor = Colors.Transparent, BorderColor = new Color("d6c39940") };
        _inset.SetCornerRadiusAll(System.Math.Max(2, radius - 5));
        _inset.SetBorderWidthAll(1);
    }

    public override void _Draw(Rid canvasItem, Rect2 rect)
    {
        if (_rim == null) return;
        _rim.Draw(canvasItem, rect);
        var face = rect.Grow(-3);
        var points = Rounded(face, System.Math.Max(2, _radius - 3));
        var top = new Color(_highlighted ? "657465" : "405a60");
        var bottom = new Color(_highlighted ? "293e38" : "1a3037");
        var shades = points.Select(point => top.Lerp(bottom, Mathf.Clamp((point.Y - face.Position.Y) / face.Size.Y, 0, 1))).ToArray();
        RenderingServer.CanvasItemAddPolygon(canvasItem, points, shades);
        _inset.Draw(canvasItem, rect.Grow(-6));
        RenderingServer.CanvasItemAddLine(canvasItem, rect.Position + new Vector2(_radius, 2),
            new Vector2(rect.End.X - _radius, rect.Position.Y + 2), new Color("fff0b990"), 1, true);
        if (rect.Size.X > 100 && _radius < 20)
        {
            foreach (var point in new[] { rect.Position + new Vector2(9, 9), new Vector2(rect.End.X - 9, rect.Position.Y + 9),
                rect.End - new Vector2(9, 9), new Vector2(rect.Position.X + 9, rect.End.Y - 9) })
            {
                RenderingServer.CanvasItemAddCircle(canvasItem, point, 2.2f, new Color("142327"));
                RenderingServer.CanvasItemAddCircle(canvasItem, point - new Vector2(0, .6f), 1.3f, new Color("d4ba80"));
            }
        }
    }

    private static Vector2[] Rounded(Rect2 rect, float radius)
    {
        radius = Mathf.Min(radius, Mathf.Min(rect.Size.X, rect.Size.Y) / 2);
        var centers = new[] { rect.Position + new Vector2(radius, radius), new Vector2(rect.End.X - radius, rect.Position.Y + radius),
            rect.End - new Vector2(radius, radius), new Vector2(rect.Position.X + radius, rect.End.Y - radius) };
        var points = new List<Vector2>();
        for (var corner = 0; corner < 4; corner++)
            for (var step = 0; step <= 7; step++)
            {
                var angle = Mathf.Pi + corner * Mathf.Pi / 2 + step * Mathf.Pi / 14;
                points.Add(centers[corner] + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        return points.ToArray();
    }
}
