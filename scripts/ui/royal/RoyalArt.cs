using System.Collections.Generic;
using Godot;

/// <summary>
/// Painted interface art cut from the approved concept screens. Every plate is addressed in the
/// 1280x720 canvas the concepts were measured in, whatever the plate's own resolution, so a rect
/// read off a reference capture is the rect used here.
/// </summary>
public static class RoyalArt
{
    public static readonly Vector2 Canvas = new(1280, 720);
    private static readonly Dictionary<string, Texture2D> Textures = new();

    public static Texture2D Load(string path)
    {
        if (Textures.TryGetValue(path, out var cached)) return cached;
        var texture = ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
        if (texture == null) GD.PushWarning($"Royal art missing: {path}");
        return Textures[path] = texture;
    }

    public static Texture2D Plate(string name) => Load($"res://assets/ui/royal/plates/{name}.png");
    public static Texture2D Item(string name) => Load($"res://assets/ui/royal/items/{name}.png");

    /// <summary>Plate pixels per canvas unit.</summary>
    public static float Density(Texture2D plate) => plate == null ? 1 : plate.GetWidth() / Canvas.X;

    /// <summary>A region of a plate, given in canvas units.</summary>
    public static AtlasTexture Cut(string plate, Rect2 canvasRect)
    {
        var texture = Plate(plate);
        if (texture == null) return null;
        var density = Density(texture);
        return new AtlasTexture { Atlas = texture, Region = new Rect2(canvasRect.Position * density, canvasRect.Size * density), FilterClip = true };
    }

}

/// <summary>
/// Nine-slice drawing at the source art's density. Godot's StyleBoxTexture draws margins at texture
/// pixel size; plates are denser than the canvas, so this scales borders back to canvas units and keeps
/// them sharp on high-density displays.
/// </summary>
public partial class SliceStyle : StyleBox
{
    private readonly Texture2D _texture;
    private readonly Rect2 _source;
    private readonly float _density, _left, _top, _right, _bottom;
    public bool DrawCenter = true;
    public Color Modulate = Colors.White;

    public SliceStyle() { }

    public SliceStyle(Texture2D texture, Rect2 canvasRect, float density, float left, float top, float right, float bottom, float padX, float padY)
    {
        _texture = texture; _density = density; _left = left; _top = top; _right = right; _bottom = bottom;
        _source = new Rect2(canvasRect.Position * density, canvasRect.Size * density);
        ContentMarginLeft = ContentMarginRight = padX;
        ContentMarginTop = ContentMarginBottom = padY;
    }

    public SliceStyle Tinted(Color modulate)
    {
        var copy = (SliceStyle)Duplicate();
        copy.Modulate = modulate; copy.DrawCenter = DrawCenter;
        return copy;
    }

    public override void _Draw(Rid canvasItem, Rect2 rect)
    {
        if (_texture == null || rect.Size.X <= 0 || rect.Size.Y <= 0) return;
        float l = Mathf.Min(_left, rect.Size.X / 2), r = Mathf.Min(_right, rect.Size.X / 2);
        float t = Mathf.Min(_top, rect.Size.Y / 2), b = Mathf.Min(_bottom, rect.Size.Y / 2);
        float[] dx = { rect.Position.X, rect.Position.X + l, rect.End.X - r, rect.End.X };
        float[] dy = { rect.Position.Y, rect.Position.Y + t, rect.End.Y - b, rect.End.Y };
        float[] sx = { _source.Position.X, _source.Position.X + l * _density, _source.End.X - r * _density, _source.End.X };
        float[] sy = { _source.Position.Y, _source.Position.Y + t * _density, _source.End.Y - b * _density, _source.End.Y };
        var rid = _texture.GetRid();
        for (var row = 0; row < 3; row++)
            for (var column = 0; column < 3; column++)
            {
                if (!DrawCenter && row == 1 && column == 1) continue;
                var destination = new Rect2(dx[column], dy[row], dx[column + 1] - dx[column], dy[row + 1] - dy[row]);
                var source = new Rect2(sx[column], sy[row], sx[column + 1] - sx[column], sy[row + 1] - sy[row]);
                if (destination.Size.X <= 0 || destination.Size.Y <= 0 || source.Size.X <= 0 || source.Size.Y <= 0) continue;
                RenderingServer.CanvasItemAddTextureRectRegion(canvasItem, destination, rid, source, Modulate, false, true);
            }
    }
}
