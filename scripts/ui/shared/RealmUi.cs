using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>Small, shared presentation primitives. Game rules remain in GameState.</summary>
public static class RealmUi
{
    public const int ButtonFontSize = 18;
    public static readonly Color Gold = new("edc47d");
    public static readonly Color Muted = new("c6bba6");
    private static readonly Dictionary<string, Texture2D> Icons = new();
    // The concept typography: Crimson Pro for reading, Cinzel capitals for headings.
    public static readonly Font TitleFont = RoyalFonts.Body(500);
    public static readonly Font HeadingFont = RoyalFonts.Display(700);
    private static readonly System.Text.RegularExpressions.Regex CapitalRun = new(@"\p{Lu}{2,}");
    private const string DisplaySizeMeta = "display_size";

    /// <summary>The approved concepts use bold Roman serif headings. Keep the shared entry point for all screens.</summary>
    public static Font DisplayFont(int size, bool roman = false)
    {
        return HeadingFont;
    }

    /// <summary>Sets a readable serif heading with the same size across the shared interface.</summary>
    public static Label Display(Label label, int size)
    {
        size = Mathf.Max(size, 20);
        label.AddThemeFontOverride("font", DisplayFont(size, CapitalRun.IsMatch(label.Text)));
        label.AddThemeFontSizeOverride("font_size", size);
        label.SetMeta(DisplaySizeMeta, size);
        return label;
    }

    /// <summary>Replaces a display heading's text and re-picks blackletter or roman letterforms for it.</summary>
    public static void SetDisplayText(Label label, string text)
    {
        label.Text = text;
        if (label.HasMeta(DisplaySizeMeta))
            label.AddThemeFontOverride("font", DisplayFont(label.GetMeta(DisplaySizeMeta).AsInt32(), CapitalRun.IsMatch(text)));
    }

    public static Texture2D Icon(string id)
    {
        if (!Icons.TryGetValue(id, out var icon))
            Icons[id] = icon = ResourceLoader.Load<Texture2D>($"res://assets/ui/icons/navigation/{id}.svg");
        return icon;
    }

    public static Label Label(string text, int size = 20, bool muted = false)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        label.AddThemeFontSizeOverride("font_size", size < 15 ? 18 : size < 20 ? 20 : size);
        if (muted) label.AddThemeColorOverride("font_color", Muted);
        return label;
    }

    public static Label Heading(string text, int size = 30)
    {
        return Display(Label(text), size < 38 ? size + 4 : size);
    }

    public static readonly Color SectionInk = new("e8c88a");

    /// <summary>A left-aligned panel heading in the display face.</summary>
    public static Label SectionTitle(string text, int size = 20)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        Display(label, size);
        label.AddThemeColorOverride("font_color", SectionInk);
        label.SetMeta("section_title", true);
        return label;
    }

    /// <summary>A muted name on the left with its value aligned right.</summary>
    public static HBoxContainer KeyValue(string name, string value, Color? valueInk = null)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 12);
        var key = Label(name, 18, true); key.AutowrapMode = TextServer.AutowrapMode.Off; row.AddChild(key);
        var amount = Label(value, 18); amount.AutowrapMode = TextServer.AutowrapMode.Off; amount.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
        amount.HorizontalAlignment = HorizontalAlignment.Right;
        if (valueInk is { } ink) amount.AddThemeColorOverride("font_color", ink);
        row.AddChild(amount);
        return row;
    }

    /// <summary>A calm, centred message for pages with nothing to show yet.</summary>
    public static VBoxContainer EmptyState(string icon, string title, string body = "")
    {
        var stack = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
        stack.AddThemeConstantOverride("separation", 8);
        stack.AddChild(new Control { CustomMinimumSize = new Vector2(0, 24) });
        stack.AddChild(new TextureRect { Texture = HomeMapArt.Icon(icon), CustomMinimumSize = new Vector2(56, 56), SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, Modulate = new Color(1, 1, 1, .85f) });
        var heading = SectionTitle(title, 22);
        heading.HorizontalAlignment = HorizontalAlignment.Center;
        heading.AddThemeColorOverride("font_color", ModalUi.Cream);
        stack.AddChild(heading);
        if (body.Length > 0)
        {
            var text = Label(body, 18, true);
            text.HorizontalAlignment = HorizontalAlignment.Center;
            text.AddThemeColorOverride("font_color", ModalUi.Muted);
            stack.AddChild(text);
        }
        return stack;
    }

    public static Button Button(string icon, string label, Action action, bool primary = false)
    {
        var button = new RealmButton { Text = label, Icon = Icon(icon), ExpandIcon = true, CenterIconAndText = true, TooltipText = label,
            CustomMinimumSize = new Vector2(string.IsNullOrEmpty(label) ? 48 : TitleFont.GetStringSize(label, HorizontalAlignment.Left, -1, ButtonFontSize).X + 72, 48),
            Alignment = HorizontalAlignment.Center, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        button.AddThemeConstantOverride("icon_max_width", 22);
        button.AddThemeConstantOverride("h_separation", 10);
        if (primary)
        {
            button.SetMeta("realm_primary", true);
            MedievalUi.StyleButton(button, 18, 10);
            button.AddThemeColorOverride("font_color", new Color("fff4db"));
        }
        button.Pressed += () => action?.Invoke();
        return button;
    }

    public static Button IconButton(string icon, string hint, Action action)
    {
        var button = Button(icon, "", action);
        button.TooltipText = hint;
        button.AccessibilityName = hint;
        MedievalUi.StyleButton(button, 8, 10);
        return button;
    }

    public static StyleBox Surface(Color fill, Color border) => new UiSurfaceStyle(fill, border);

    public static VBoxContainer Panel(Control host, Rect2 rect, out PanelContainer panel)
    {
        panel = new PanelContainer { Position = rect.Position, Size = rect.Size };
        host.AddChild(panel);
        var stack = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        panel.AddChild(stack);
        return stack;
    }

    public static HBoxContainer Header(Control root, string eyebrow, string title, Action back, string emblem = "flag")
    {
        var row = new HBoxContainer { Position = new Vector2(28, 20), Size = new Vector2(1224, 62) };
        row.AddThemeConstantOverride("separation", 18);
        root.AddChild(row);
        row.AddChild(IconButton("back", "Return", back));
        row.AddChild(new HeraldicEmblem { Symbol = emblem });
        var titles = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        titles.AddThemeConstantOverride("separation", 0);
        titles.AddChild(Label(eyebrow.ToUpperInvariant(), 12, true));
        titles.AddChild(Heading(title, 27));
        row.AddChild(titles);
        row.AddChild(UiBadgeFactory.CreateRewardMetric("gold", "", GameState.Instance.Gold.ToString("N0"), new Vector2(24, 24)));
        row.AddChild(new FoodBalance());
        row.AddChild(IconButton("gear", "Settings", () => SceneRouter.Instance.GoToSettings()));
        return row;
    }

    public static HBoxContainer Tabs(Control host, Action<int> select, params string[] labels)
    {
        var row = new HBoxContainer();
        if (MobilePresentation.Enabled)
        {
            var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Auto, VerticalScrollMode = ScrollContainer.ScrollMode.Disabled,
                CustomMinimumSize = new Vector2(0, 56), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            scroll.SetMeta("modal_min_height", 0); host.AddChild(scroll); scroll.AddChild(row);
        }
        else host.AddChild(row);
        row.SetMeta("realm_tabs", true);
        var group = new ButtonGroup();
        for (int i = 0; i < labels.Length; i++)
        {
            int index = i;
            var symbol = labels[i] switch {
                "Warband" or "Squad" or "Community" => "people", "Spells" => "flame", "War wagon" or "Caravan" => "wagon",
                "Relics" => "crown", "All" => "book", "Enemies" or "Bosses" => "skull",
                "Units" => "people", "Sound" => "music", "Gameplay" or "Combat" or "Adventure" => "sword",
                "Online" or "Account" or "Rooms" => "people", "Saved" => "flag", "Daily" => "star", "Featured" => "star",
                _ => "shield" };
            // Navigation has small monochrome emblems; illustrated resource icons remain in content cards.
            if (!ResourceLoader.Exists($"res://assets/ui/icons/navigation/{symbol}.svg")) symbol = "shield";
            var button = new RealmButton { Text = labels[i], Icon = Icon(symbol), ExpandIcon = true, CenterIconAndText = true,
                VerticalContent = labels.Contains("War wagon") || labels.Contains("Enemies"),
                ToggleMode = true, ButtonGroup = group, ButtonPressed = i == 0,
                CustomMinimumSize = new Vector2(0, 48), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            button.AddThemeConstantOverride("icon_max_width", 20); button.AddThemeConstantOverride("h_separation", 6);
            MedievalUi.StyleButton(button, 12, 10);
            button.Pressed += () => select(index);
            row.AddChild(button);
        }
        return row;
    }

    public static VBoxContainer Scroll(Control host)
    {
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        host.AddChild(scroll);
        var stack = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(stack);
        return stack;
    }

    public static void Details(Control host, string title, string text)
    {
        var dialog = new AcceptDialog { Title = title, DialogText = "", MinSize = new Vector2I(640, 360), Exclusive = true };
        dialog.Theme = host.Theme;
        host.AddChild(dialog);
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(620, 330), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        dialog.AddChild(scroll);
        scroll.AddChild(Label(text));
        dialog.Confirmed += () => dialog.QueueFree();
        dialog.Canceled += () => dialog.QueueFree();
        dialog.PopupCentered(new Vector2I(680, 430));
    }

    public static void Clear(Node node) => TrimChildren(node, 0);

    public static void TrimChildren(Node node, int keep)
    {
        while (node.GetChildCount() > keep)
        {
            var child = node.GetChild(node.GetChildCount() - 1);
            node.RemoveChild(child);
            child.QueueFree();
        }
    }

    public static void FadeIn(Control host)
    {
        host.Modulate = new Color(1, 1, 1, 0);
        host.CreateTween().TweenProperty(host, "modulate:a", 1f, 0.25);
    }
}
