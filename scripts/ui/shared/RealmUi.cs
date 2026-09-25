using System;
using System.Collections.Generic;
using Godot;

/// <summary>Small, shared presentation primitives. Game rules remain in GameState.</summary>
public static class RealmUi
{
    public static readonly Color Gold = new("d7b77b");
    public static readonly Color Muted = new("9eada9");
    private static readonly Dictionary<string, Texture2D> Icons = new();
    public static readonly Font TitleFont = new SystemFont { FontNames = new[] { "Georgia", "Noto Serif", "serif" } };

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
        var label = Label(text, size < 38 ? size + 4 : size);
        label.AddThemeFontOverride("font", TitleFont);
        return label;
    }

    public static Button Button(string icon, string label, Action action, bool primary = false)
    {
        var button = new RealmButton { Text = label, Icon = Icon(icon), ExpandIcon = true, TooltipText = label,
            CustomMinimumSize = new Vector2(string.IsNullOrEmpty(label) ? 48 : TitleFont.GetStringSize(label, HorizontalAlignment.Left, -1, 20).X + 72, 48),
            Alignment = HorizontalAlignment.Center, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        button.AddThemeConstantOverride("icon_max_width", 22);
        button.AddThemeConstantOverride("h_separation", 10);
        if (primary)
        {
            button.AddThemeStyleboxOverride("normal", MedievalUi.Engraved("button_primary", 18, 10));
            button.AddThemeStyleboxOverride("hover", MedievalUi.Engraved("button_hover", 18, 10));
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
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled" })
            button.AddThemeStyleboxOverride(state, MedievalUi.Engraved(state == "normal" ? "button" : "button_" + state, 8, 10));
        return button;
    }

    public static StyleBoxFlat Surface(Color fill, Color border)
    {
        return new StyleBoxFlat { BgColor = fill, BorderColor = border,
            BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            ContentMarginLeft = 16, ContentMarginRight = 16, ContentMarginTop = 12, ContentMarginBottom = 12 };
    }

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
        row.AddChild(UiBadgeFactory.CreateRewardMetric("food", "", GameState.Instance.Food.ToString("N0"), new Vector2(24, 24)));
        row.AddChild(IconButton("gear", "Settings", () => SceneRouter.Instance.GoToSettings()));
        return row;
    }

    public static HBoxContainer Tabs(Control host, Action<int> select, params string[] labels)
    {
        var row = new HBoxContainer();
        host.AddChild(row);
        row.SetMeta("realm_tabs", true);
        var group = new ButtonGroup();
        for (int i = 0; i < labels.Length; i++)
        {
            int index = i;
            var button = new RealmButton { Text = labels[i], ToggleMode = true, ButtonGroup = group, ButtonPressed = i == 0,
                CustomMinimumSize = new Vector2(0, 48), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            foreach (var state in new[] { "normal", "hover", "pressed", "disabled" })
                button.AddThemeStyleboxOverride(state, MedievalUi.Engraved(state == "normal" ? "button" : "button_" + state, 12, 10));
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
