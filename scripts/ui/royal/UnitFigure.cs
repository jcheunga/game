using Godot;

/// <summary>
/// A unit's resting pose stood on the bottom of its rect. Figures are the first idle frame of each
/// preview sheet, cropped offline by art/royal/figures.py, so cards draw one small texture each.
/// </summary>
public partial class UnitFigure : Control
{
    private Texture2D _texture;
    public float Fill = .96f;
    public Color Tint = Colors.White;

    public UnitFigure() { MouseFilter = MouseFilterEnum.Ignore; TextureFilter = TextureFilterEnum.LinearWithMipmaps; }

    public static Texture2D For(UnitDefinition unit)
    {
        if (unit == null) return null;
        var path = $"res://assets/ui/royal/figures/{unit.Id}.png";
        return ResourceLoader.Exists(path) ? RoyalArt.Load(path) : UiArtLoader.TryLoadUnitIcon(unit);
    }

    public void SetUnit(UnitDefinition unit) { _texture = For(unit); QueueRedraw(); }

    public override void _Draw()
    {
        if (_texture == null) return;
        var source = _texture.GetSize();
        var scale = Mathf.Min(Size.Y * Fill / source.Y, Size.X / source.X);
        var size = source * scale;
        DrawTextureRect(_texture, new Rect2(new Vector2((Size.X - size.X) / 2, Size.Y - size.Y), size), false, Tint);
    }
}
