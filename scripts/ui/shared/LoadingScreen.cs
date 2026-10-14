using Godot;

/// <summary>Dedicated loading illustrations can replace the existing zone picture without changing layout.</summary>
public partial class LoadingScreen : Control
{
    private TextureRect _art;
    private VBoxContainer _heading, _tipBox;
    private Label _region, _tip, _activity;
    public string ArtworkPath { get; private set; } = "";
    public string Tip => _tip?.Text ?? "";

    public override void _Ready()
    {
        Name = "LoadingScreen";
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(new ColorRect { Color = new Color("101a22"), MouseFilter = MouseFilterEnum.Ignore }.With(c => c.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect)));
        _art = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_art); _art.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var gradient = new Gradient { Colors = new[] { new Color(0, 0, 0, .7f), new Color(0, 0, 0, .08f), new Color(0, 0, 0, .2f), new Color(0, 0, 0, .88f) },
            Offsets = new[] { 0f, .3f, .55f, 1f } };
        var shade = new TextureRect { Texture = new GradientTexture2D { Gradient = gradient, Width = 2, Height = 256,
            FillFrom = new Vector2(.5f, 0), FillTo = new Vector2(.5f, 1) }, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(shade); shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _heading = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_heading);
        _heading.AddChild(RealmUi.Heading("CROWNROAD", 40));
        _region = RoyalText.Paragraph("", 23, new Color("e7cc95")); _heading.AddChild(_region);
        _tipBox = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _tipBox.AddThemeConstantOverride("separation", 10); AddChild(_tipBox);
        _activity = RoyalText.Paragraph("Preparing your warband", 18, new Color("e7cc95")); _tipBox.AddChild(_activity);
        _tipBox.AddChild(new HSeparator());
        _tip = RoyalText.Paragraph("", 24); _tipBox.AddChild(_tip);
        Resized += Place;
    }

    public void Prepare(StageDefinition stage, bool enteringBattle)
    {
        var zone = stage?.MapId ?? "city";
        var candidates = new[] { $"res://assets/ui/loading/{zone}.png", "res://assets/ui/loading/default.png",
            $"res://assets/ui/royal/missions/{zone}.png", "res://assets/ui/royal/missions/city.png" };
        _art.Texture = null; ArtworkPath = "";
        foreach (var path in candidates)
        {
            if (!ResourceLoader.Exists(path)) continue;
            _art.Texture = RoyalArt.Load(path); ArtworkPath = path; break;
        }
        _region.Text = stage == null ? "The Lantern Caravan" : RouteCatalog.Get(zone).Title;
        _activity.Text = enteringBattle ? "Preparing your warband" : "Returning to the caravan";
        _tip.Text = LoadingTipCatalog.GetRandom();
        Place(); Callable.From(Place).CallDeferred();
    }

    private void Place()
    {
        if (_tipBox == null) return;
        var safe = SafeAreaService.Instance;
        var left = Mathf.Max(32, safe?.MarginLeft ?? 0) + 16;
        var right = Mathf.Max(32, safe?.MarginRight ?? 0) + 16;
        var top = Mathf.Max(24, safe?.MarginTop ?? 0) + 12;
        var bottom = Mathf.Max(24, safe?.MarginBottom ?? 0) + 12;
        var width = Mathf.Max(100, Mathf.Min(900, Size.X - left - right));
        _activity.CustomMinimumSize = new Vector2(width, 0);
        _tip.CustomMinimumSize = new Vector2(width, 0);
        _tipBox.CustomMinimumSize = new Vector2(width, 0);
        _heading.Position = new Vector2(left, top); _heading.Size = new Vector2(width, 0);
        _tipBox.Size = new Vector2(width, 0); _tipBox.ResetSize();
        _tipBox.Position = new Vector2(left, Mathf.Max(top + 110, Size.Y - bottom - _tipBox.Size.Y));
    }
}
