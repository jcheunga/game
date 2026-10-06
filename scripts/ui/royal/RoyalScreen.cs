using System;
using Godot;

/// <summary>
/// A concept screen drawn on the fixed 1280x720 canvas: its painted plate, then live text, art and
/// controls placed at the rects measured on the concept. Screens open over the home map (inside the
/// home modal) or stand alone; either way Close and Back reach whoever presented them.
/// </summary>
public partial class RoyalScreen : Control
{
    /// <summary>Plate name in assets/ui/royal/plates; empty for screens that draw their own backdrop.</summary>
    public string PlateName = "";
    /// <summary>Darkens whatever is behind a plate with transparent surroundings (the home map).</summary>
    public Color Veil = new(.02f, .05f, .08f, .62f);
    public Action Closed;
    private TextureRect _plate;

    public RoyalScreen()
    {
        MouseFilter = MouseFilterEnum.Stop;
        SetMeta("royal_screen", true);
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        if (Veil.A > 0)
        {
            var veil = new ColorRect { Name = "Veil", Color = Veil, MouseFilter = MouseFilterEnum.Ignore };
            AddChild(veil); veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }
        _plate = new TextureRect { Name = "Plate", ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_plate);
        _plate.Position = Vector2.Zero; _plate.Size = RoyalArt.Canvas;
        SetPlate(PlateName);
        Build();
    }

    /// <summary>Swaps the painted plate, e.g. when an armory tab changes layout.</summary>
    public void SetPlate(string name)
    {
        PlateName = name;
        if (_plate != null) _plate.Texture = string.IsNullOrEmpty(name) ? null : RoyalArt.Plate(name);
    }

    protected virtual void Build() { }

    /// <summary>A layer of live content that can be rebuilt without touching the plate.</summary>
    protected Control Layer(string name)
    {
        var layer = new Control { Name = name, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(layer); layer.Position = Vector2.Zero; layer.Size = RoyalArt.Canvas;
        return layer;
    }

    public void Close()
    {
        if (Closed != null) { Closed(); return; }
        if (FindHostModal() is { } modal) { modal.Closed?.Invoke(); return; }
        SceneRouter.Instance.GoToMap();
    }

    private RealmModal FindHostModal()
    {
        for (var parent = GetParent(); parent != null; parent = parent.GetParent())
            if (parent is RealmModal modal) return modal;
        return null;
    }

    private Image _plateImage;
    private Texture2D _plateImageSource;

    /// <summary>
    /// A click that lands on no control and outside the painted frame (where the plate is transparent)
    /// dismisses the screen, as a modal backdrop does. Full-bleed plates have no outside.
    /// </summary>
    public override void _GuiInput(InputEvent input)
    {
        if (input is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click || _plate?.Texture is not { } texture) return;
        if (_plateImageSource != texture)
        {
            _plateImageSource = texture;
            _plateImage = texture.GetImage();
            if (_plateImage?.IsCompressed() == true) _plateImage.Decompress();
        }
        if (_plateImage == null) return;
        var uv = (click.Position - _plate.Position) / _plate.Size;
        if (uv.X < 0 || uv.Y < 0 || uv.X >= 1 || uv.Y >= 1) return;
        var pixel = _plateImage.GetPixel((int)(uv.X * _plateImage.GetWidth()), (int)(uv.Y * _plateImage.GetHeight()));
        if (pixel.A > .05f) return;
        AcceptEvent();
        Close();
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape } && IsVisibleInTree() && FindHostModal() == null)
        {
            GetViewport().SetInputAsHandled();
            Close();
        }
    }
}
