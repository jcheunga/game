using Godot;

/// <summary>Illustrated stage lighting for the authentic animated unit and spell previews.</summary>
public partial class ModalShowcaseBackdrop : Control
{
    public bool Magic;
    public ModalShowcaseBackdrop() { MouseFilter = MouseFilterEnum.Ignore; }
    public override void _Draw()
    {
        var rect = new Rect2(Vector2.Zero, Size);
        // The settings concept's lantern-lit workshop alcove.
        var texture = RoyalArt.Cut("settings", new Rect2(122, 238, 290, 410));
        if (texture != null) DrawTextureRect(texture, rect, false, new Color(.75f, .75f, .75f));
        DrawRect(rect, new Color("12242e33"));
        var tint = new Color(Magic ? "f3b14a" : "e7b76a");
        for (var i = 10; i > 0; i--) DrawCircle(Size * new Vector2(.5f,.52f), Mathf.Min(Size.X,Size.Y) * (.25f + i*.019f), new Color(tint, .017f));
        DrawSetTransform(Size * new Vector2(.5f,.88f), 0, new Vector2(Size.X * .34f, Size.Y * .05f));
        DrawCircle(Vector2.Zero, 1, new Color("e4c98560")); DrawArc(Vector2.Zero, 1, 0, Mathf.Tau, 64, new Color("ffdb9290"), .025f, true); DrawSetTransform(Vector2.Zero);
    }
}
