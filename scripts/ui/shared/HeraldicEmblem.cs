using Godot;

/// <summary>A scalable enamel shield and brass laurel, independent of text layout.</summary>
public partial class HeraldicEmblem : Control
{
    public string Symbol { get; set; } = "crown";
    public Color Enamel { get; set; } = new("582d2e");

    public HeraldicEmblem()
    {
        CustomMinimumSize = new Vector2(58, 58);
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Draw()
    {
        var scale = Mathf.Min(Size.X, Size.Y) / 64f;
        DrawSetTransform(new Vector2((Size.X - 64 * scale) / 2, 0), 0, Vector2.One * scale);
        var brass = new Color("d8b879");
        var shield = new Vector2[] { new(15, 8), new(32, 4), new(49, 8), new(47, 37), new(42, 48), new(32, 58), new(22, 48), new(17, 37) };
        DrawColoredPolygon(shield, Enamel);
        for (var i = 0; i < shield.Length; i++) DrawLine(shield[i], shield[(i + 1) % shield.Length], brass, 1.5f, true);
        DrawLine(new Vector2(20, 13), new Vector2(44, 13), new Color("e9cc9244"), 1, true);
        foreach (var side in new[] { -1, 1 })
        {
            for (var i = 0; i < 5; i++)
            {
                var y = 22 + i * 6;
                var x = 32 + side * (25 - i * 1.1f);
                DrawLine(new Vector2(x, y), new Vector2(x - side * 5, y + 5), brass.Darkened(.2f), 2, true);
            }
        }
        DrawTextureRect(RealmUi.Icon(Symbol), new Rect2(21, 21, 22, 22), false, new Color("ffdfa0"));
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }
}
