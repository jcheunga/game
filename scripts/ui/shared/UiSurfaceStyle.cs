using Godot;

// Tintable textured backing for inventory badges and selected cards. The body
// and rim are independent so custom team/reward colours remain meaningful.
public partial class UiSurfaceStyle : StyleBox
{
    private StyleBoxTexture _body, _rim;

    public UiSurfaceStyle() { }

    public UiSurfaceStyle(Color fill, Color border)
    {
        _body = MedievalUi.Engraved("surface_body", 0, 0);
        _body.ModulateColor = fill;
        _rim = MedievalUi.Engraved("surface_rim", 0, 0);
        _rim.ModulateColor = border;
        ContentMarginLeft = ContentMarginRight = 16;
        ContentMarginTop = ContentMarginBottom = 12;
    }

    public override void _Draw(Rid canvasItem, Rect2 rect)
    {
        _body?.Draw(canvasItem, rect);
        _rim?.Draw(canvasItem, rect);
    }
}
