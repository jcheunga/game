using Godot;

/// <summary>Embossed gold stars, with dark metal sockets for unearned stars.</summary>
public partial class StageStarRating : Control
{
    private static Texture2D _earnedTexture, _emptyTexture;
    private int _stars;
    public int Stars
    {
        get => _stars;
        set
        {
            _stars = Mathf.Clamp(value, 0, 3);
            AccessibilityName = $"{_stars} of 3 stars";
            QueueRedraw();
        }
    }

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(132, 48);
        MouseFilter = MouseFilterEnum.Ignore;
        AccessibilityName = $"{Mathf.Clamp(Stars, 0, 3)} of 3 stars";
    }

    public override void _Draw() => DrawStars(this, new Rect2(Vector2.Zero, Size), Stars);

    public static void DrawStars(CanvasItem canvas, Rect2 bounds, int stars, float archRise = 3f)
    {
        // The concept's painted gold star; empty sockets are the same star in shadow.
        _earnedTexture ??= RoyalKit.Texture("result-star");
        _emptyTexture ??= _earnedTexture;
        var side = Mathf.Min(bounds.Size.Y - archRise, (bounds.Size.X - 4) / 3);
        var left = bounds.Position.X + (bounds.Size.X - (side * 3 + 4)) / 2;
        for (var i = 0; i < 3; i++)
        {
            var position = new Vector2(left + i * (side + 2), bounds.Position.Y + (i == 1 ? 0 : archRise));
            canvas.DrawTextureRect(i < stars ? _earnedTexture : _emptyTexture,
                new Rect2(position, new Vector2(side, side)), false, i < stars ? Colors.White : new Color(.2f, .19f, .18f, .85f));
        }
    }
}
