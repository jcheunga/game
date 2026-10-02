using Godot;

/// <summary>Illustrated stage lighting for the authentic animated unit and spell previews.</summary>
public partial class ModalShowcaseBackdrop : Control
{
    public bool Magic;
    public ModalShowcaseBackdrop() { MouseFilter = MouseFilterEnum.Ignore; }
    public override void _Draw()
    {
        var rect = new Rect2(Vector2.Zero, Size);
        var texture = ModalArt.Illustration(Magic ? 1 : 0);
        if (texture != null) DrawTextureRect(texture, rect, false, new Color(.48f, .45f, .55f));
        DrawRect(rect, new Color(Magic ? "1a103f66" : "16274588"));
        var tint = new Color(Magic ? "b990ff" : "9dccff");
        for (var i = 10; i > 0; i--) DrawCircle(Size * new Vector2(.5f,.52f), Mathf.Min(Size.X,Size.Y) * (.25f + i*.019f), new Color(tint, .017f));
        DrawSetTransform(Size * new Vector2(.5f,.88f), 0, new Vector2(Size.X * .34f, Size.Y * .05f));
        DrawCircle(Vector2.Zero, 1, new Color("e4c98560")); DrawArc(Vector2.Zero, 1, 0, Mathf.Tau, 64, new Color("ffdb9290"), .025f, true); DrawSetTransform(Vector2.Zero);
    }
}
