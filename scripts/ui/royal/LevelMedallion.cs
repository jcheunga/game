using Godot;

/// <summary>
/// The player's level as a steel medallion: the number in engraved gold, ringed by a track that fills
/// with XP toward the next level. A lit medallion (a level just gained, rewards waiting) glows, and a
/// badge counts rewards ready to claim.
/// </summary>
public partial class LevelMedallion : Control
{
    private int _level = 1;
    private float _progress;
    private bool _lit;
    private int _badge;

    public int Level { get => _level; set { _level = value; QueueRedraw(); } }
    /// <summary>Share of the way to the next level, 0..1.</summary>
    public float Progress { get => _progress; set { _progress = Mathf.Clamp(value, 0, 1); QueueRedraw(); } }
    public bool Lit { get => _lit; set { _lit = value; QueueRedraw(); } }
    /// <summary>Rewards waiting to be claimed; zero hides the badge.</summary>
    public int Badge { get => _badge; set { _badge = value; QueueRedraw(); } }

    public LevelMedallion() { MouseFilter = MouseFilterEnum.Ignore; }

    public override void _Ready() => Resized += QueueRedraw;

    public override void _Draw()
    {
        var centre = Size / 2;
        var radius = Mathf.Min(Size.X, Size.Y) / 2 - 1;
        if (radius <= 6) return;
        var ring = Mathf.Max(3f, radius * .13f);
        if (_lit)
            for (var i = 3; i >= 1; i--) DrawCircle(centre, radius + i * 2.5f, new Color(.98f, .8f, .4f, .09f));
        DrawCircle(centre, radius, new Color("0d1318"));
        // XP track: steel all round, gold for the share earned toward the next level.
        DrawArc(centre, radius - ring / 2 - 1, 0, Mathf.Tau, 64, new Color("34414b"), ring, true);
        if (_progress > 0)
            DrawArc(centre, radius - ring / 2 - 1, -Mathf.Pi / 2, -Mathf.Pi / 2 + Mathf.Tau * _progress, 64, new Color("e8c26b"), ring, true);
        var inner = radius - ring - 2;
        DrawCircle(centre, inner, _lit ? new Color("2a2516") : new Color("18232d"));
        DrawArc(centre, inner, 0, Mathf.Tau, 64, new Color("b8924f"), 1.5f, true);

        var font = RoyalFonts.Display(700);
        var text = _level.ToString();
        var size = Mathf.RoundToInt(inner * (text.Length > 1 ? 1.0f : 1.15f));
        var width = font.GetStringSize(text, HorizontalAlignment.Left, -1, size).X;
        var baseline = centre.Y + size * .70f / 2;
        var origin = new Vector2(Mathf.Round(centre.X - width / 2), Mathf.Round(baseline));
        DrawStringOutline(font, origin + new Vector2(0, 1), text, HorizontalAlignment.Left, -1, size, 3, new Color(0, 0, 0, .55f));
        DrawString(font, origin, text, HorizontalAlignment.Left, -1, size, new Color("f6d98f"));
        if (_badge <= 0) return;

        var count = _badge > 9 ? "9+" : _badge.ToString();
        var dot = Mathf.Max(9f, radius * .34f);
        var at = centre + new Vector2(radius * .74f, -radius * .74f);
        DrawCircle(at, dot + 1.5f, new Color("1a0f07"));
        DrawCircle(at, dot, new Color("b8372b"));
        DrawArc(at, dot, 0, Mathf.Tau, 32, new Color("f0c977"), 1.5f, true);
        var body = RoyalFonts.Body(700);
        var small = Mathf.RoundToInt(dot * (count.Length > 1 ? 1.05f : 1.3f));
        var countWidth = body.GetStringSize(count, HorizontalAlignment.Left, -1, small).X;
        DrawString(body, new Vector2(Mathf.Round(at.X - countWidth / 2), Mathf.Round(at.Y + small * .58f / 2)), count, HorizontalAlignment.Left, -1, small, new Color("fff4db"));
    }
}
