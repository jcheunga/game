using System;
using System.Linq;
using Godot;

/// <summary>A single, map-preserving presentation for every home destination.</summary>
public partial class RealmModal : Control
{
    public Control Content { get; private set; }
    public string Destination { get; private set; }
    public Action Closed, Back;
    private Button _back, _close;
    private Label _title, _subtitle;
    private int _titleSize = 28;
    private PanelContainer _frame;
    private PanelContainer _header;
    private TextureRect _emblem;
    private float _preferredWidth = 1120, _preferredHeight = 612;
    private bool _mobileCanvas;

    public override void _Ready()
    {
        Name = "HomeModal";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var veil = new ColorRect { Color = new Color("050f18ab"), MouseFilter = MouseFilterEnum.Stop };
        AddChild(veil); veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        veil.GuiInput += input => {
            if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) Closed?.Invoke();
        };
        _frame = new PanelContainer { MouseFilter = MouseFilterEnum.Stop };
        _frame.AddThemeStyleboxOverride("panel", new ModalSurface(ModalMaterial.Wood, 16));
        AddChild(_frame);
        var stack = new VBoxContainer(); stack.AddThemeConstantOverride("separation", 12); _frame.AddChild(stack);
        _header = new PanelContainer(); _header.AddThemeStyleboxOverride("panel", new ModalSurface(ModalMaterial.Steel, 10)); stack.AddChild(_header);
        var heading = new HBoxContainer(); heading.AddThemeConstantOverride("separation", 14); _header.AddChild(heading);
        _back = HomeMapUi.IconButton("back", "Back to previous panel", () => Back?.Invoke()); heading.AddChild(_back);
        ModalUi.StyleButton(_back);
        _emblem = new TextureRect { CustomMinimumSize = new Vector2(52, 52), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore }; heading.AddChild(_emblem);
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter }; titles.AddThemeConstantOverride("separation", 0); heading.AddChild(titles);
        _title = RealmUi.Heading("", 28); _title.AddThemeColorOverride("font_color", new Color("ffe3a1")); titles.AddChild(_title);
        _subtitle = RealmUi.Label("", 18, true); _subtitle.AddThemeFontSizeOverride("font_size", 18); _subtitle.AddThemeColorOverride("font_color", ModalUi.Muted); titles.AddChild(_subtitle);
        _close = HomeMapUi.IconButton("close", "Close panel", () => Closed?.Invoke()); heading.AddChild(_close);
        ModalUi.StyleButton(_close, material: ModalMaterial.Ruby);
        Content = new Control { Name = "Content", SizeFlagsVertical = SizeFlags.ExpandFill, ClipContents = true };
        stack.AddChild(Content);
        Resized += FitToOwnArea;
    }

    public void Present(string destination, string title, string subtitle, bool hasBack, float width = 1120, float height = 612, string identity = null)
    {
        _preferredWidth = width; _preferredHeight = height;
        _close.GrabFocus();
        Destination = destination; _title.Text = title; _subtitle.Text = subtitle ?? ""; _back.Visible = hasBack;
        ApplyIdentity(identity ?? title);
        _frame.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        _frame.OffsetLeft = -width / 2; _frame.OffsetRight = width / 2;
        _frame.OffsetTop = -322; _frame.OffsetBottom = 290;
        FitToOwnArea();
        if (!(GameState.Instance?.ReducedMotion ?? false)) RealmUi.FadeIn(_frame);
    }

    /// <summary>
    /// A smaller modal stacked above whatever is open, for inspecting one item.
    /// It lives on its own layer so it covers the home panel and the battle HUD alike.
    /// </summary>
    public static RealmModal OpenInspector(CanvasLayer layer, string title, string identity, float width = 880, float height = 540)
    {
        var modal = new RealmModal();
        MedievalUi.Apply(modal);
        layer.AddChild(modal);
        modal.Name = "InspectorModal";
        if (MobilePresentation.Enabled) modal.UseMobileCanvas();
        modal.Closed = modal.Back = layer.QueueFree;
        modal._close.TooltipText = modal._close.AccessibilityName = "Close details";
        modal.Present("inspector", title, "", false, width, height, identity);
        return modal;
    }

    public void SetHeading(string title) { _title.Text = title; RealmUi.Display(_title, _titleSize); }

    public static void UpdateHeading(Node child, string title = null, string subtitle = null)
    {
        for (var parent = child.GetParent(); parent != null; parent = parent.GetParent())
            if (parent is RealmModal modal) { if (title != null) { modal.SetHeading(title); modal.ApplyIdentity(title); } if (subtitle != null) { modal._subtitle.Text = subtitle; modal.FitToOwnArea(); } return; }
    }

    public void FitToArea(Vector2 area)
    {
        var width = Mathf.Min(_preferredWidth, area.X - 32);
        var height = Mathf.Min(_preferredHeight, area.Y - 32);
        _frame.OffsetLeft = -width / 2; _frame.OffsetRight = width / 2;
        _frame.OffsetTop = -height / 2; _frame.OffsetBottom = height / 2;
        var compact = area.Y < 500;
        _subtitle.Visible = !compact && _subtitle.Text.Length > 0;
        _titleSize = compact ? 22 : 28;
        RealmUi.Display(_title, _titleSize);
        _title.ClipText = true;
        _title.AutowrapMode = TextServer.AutowrapMode.Off;
        _title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _emblem.CustomMinimumSize = new Vector2(compact ? 36 : 52, compact ? 36 : 52);
    }

    private void FitToOwnArea() { if (_frame != null && Size.X > 0 && Size.Y > 0) FitToArea(Size); }

    public void UseMobileCanvas()
    {
        if (_mobileCanvas) return;
        _mobileCanvas = true;
        SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
        GetViewport().SizeChanged += ResizeMobileCanvas;
        ResizeMobileCanvas();
    }

    private void ResizeMobileCanvas()
    {
        Scale = Vector2.One * MobilePresentation.HudScale;
        Size = GetViewportRect().Size / MobilePresentation.HudScale;
        FitToOwnArea();
    }

    public override void _ExitTree()
    {
        if (_mobileCanvas) GetViewport().SizeChanged -= ResizeMobileCanvas;
    }

    private void ApplyIdentity(string title)
    {
        var text = title.ToLowerInvariant();
        var material = text.Contains("spell") || text.Contains("relic") || text.Contains("tower") || text.Contains("raid") ? ModalMaterial.Arcane : text.Contains("upgrade") || text.Contains("wagon") || text.Contains("forge") ? ModalMaterial.Forge : ModalMaterial.Wood;
        _frame.AddThemeStyleboxOverride("panel", new ModalSurface(material, 16));
        _header.AddThemeStyleboxOverride("panel", new ModalSurface(ModalMaterial.Tab, 10, ModalUi.Accent(title), true));
        _emblem.Texture = HomeMapArt.Icon(EmblemFor(text));
    }

    // Painted emblems keep every header in one illustrated family.
    private static string EmblemFor(string title)
    {
        bool Has(params string[] words) => words.Any(title.Contains);
        if (Has("setting")) return "gear";
        if (Has("spell", "endless")) return "flame";
        if (Has("upgrade", "wagon", "forge", "talent")) return "hammer";
        if (Has("codex")) return "book";
        if (Has("storehouse")) return "gold";
        if (Has("achievement", "gift", "season", "ranking", "profile", "event")) return "star";
        if (Has("expedition", "bount")) return "map";
        if (Has("caravan", "guild", "friend")) return "people";
        return "sword";
    }

    public override void _Input(InputEvent input)
    {
        if (input is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.Keycode == Key.Escape) { var viewport = GetViewport(); Closed?.Invoke(); viewport.SetInputAsHandled(); return; }
        if (key.Keycode != Key.Tab) return;
        var focusable = Focusable(this).ToArray(); if (focusable.Length == 0) return;
        var current = Array.IndexOf(focusable, GetViewport().GuiGetFocusOwner());
        var next = (current + (key.ShiftPressed ? -1 : 1) + focusable.Length) % focusable.Length;
        focusable[next].GrabFocus(); GetViewport().SetInputAsHandled();
    }

    private static System.Collections.Generic.IEnumerable<Control> Focusable(Node node)
    {
        foreach (var child in node.GetChildren()) {
            if (child is Control control && control.IsVisibleInTree() && control.FocusMode == FocusModeEnum.All && (control is not BaseButton button || !button.Disabled)) yield return control;
            foreach (var descendant in Focusable(child)) yield return descendant;
        }
    }

    public static bool Embedded(Control menu) => menu.HasMeta("home_modal");

    public static void Polish(Node node)
    {
        foreach (var child in node.GetChildren()) Polish(child);
        ModalUi.Dress(node);
    }

    // Older activity screens keep their rules and actions. Their body panels are
    // reflowed into scrollable rows, rather than scaling down their text or map.
    public static void AdaptActivity(Control menu)
    {
        var children = menu.GetChildren().OfType<Control>().ToArray();
        var body = children.Where(child => !child.HasMeta("medieval_backdrop") && child.Position.Y >= 105 && child.Position.Y < 600).ToArray();
        var footers = children.Where(child => !child.HasMeta("medieval_backdrop") && child.Position.Y >= 600 && child.Position.Y < 650).ToArray();
        foreach (var child in children) child.Hide();
        var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 12); menu.AddChild(root); root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var stack = RealmUi.Scroll(root);
        ((ScrollContainer)stack.GetParent()).SetMeta("modal_min_height", 0);
        stack.AddThemeConstantOverride("separation", 14); stack.SizeFlagsVertical = SizeFlags.ExpandFill;
        foreach (var band in body.GroupBy(child => Math.Round(child.Position.Y / 24)).OrderBy(group => group.Key))
        {
            BoxContainer row = MobilePresentation.Enabled ? new VBoxContainer() : new HBoxContainer();
            row.SizeFlagsVertical = band.Any(child => child is PanelContainer && child.Size.Y > 150) ? SizeFlags.ExpandFill : SizeFlags.Fill;
            row.AddThemeConstantOverride("separation", 14); stack.AddChild(row);
            foreach (var child in band.OrderBy(child => child.Position.X))
            {
                var weight = Math.Max(1, child.Size.X);
                menu.RemoveChild(child); row.AddChild(child); child.Visible = !child.HasMeta("modal_hidden");
                RelaxMinimums(child);
                child.SizeFlagsHorizontal = SizeFlags.ExpandFill; child.SizeFlagsStretchRatio = weight;
                if (child is Label label) label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            }
        }
        // Some newer screens have one full-height container instead of panels.
        if (body.Length == 0)
        {
            foreach (var child in children.Where(child => !child.HasMeta("medieval_backdrop") && child is Container))
            {
                menu.RemoveChild(child); stack.AddChild(child); child.Show(); RelaxMinimums(child);
            }
        }
        foreach (var footer in footers) {
            menu.RemoveChild(footer);
            if (MobilePresentation.Enabled)
            {
                var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
                    VerticalScrollMode = ScrollContainer.ScrollMode.Disabled };
                // Without this, relaxing minimums reserves the default scroll height and the footer crowds out the body.
                scroll.SetMeta("modal_min_height", 0);
                root.AddChild(scroll); scroll.AddChild(footer);
            }
            else root.AddChild(footer);
            footer.Show(); RelaxMinimums(footer);
            footer.SizeFlagsHorizontal = SizeFlags.ExpandFill; footer.SizeFlagsVertical = SizeFlags.Fill;
            HideModalNavigation(footer);
            MarkLaunchActions(footer);
            if (MobilePresentation.Enabled) CompactFooter(footer);
        }
        MarkLaunchActions(root); Polish(root);
        var upkeep = new ModalActivityLayout { Body = root }; menu.AddChild(upkeep);
    }

    // The modal already provides back, close and the home settings button.
    private static bool IsModalNavigation(string text) => text.StartsWith("Back ") || text is "Settings" or "Caravan Armory" or "Main Menu" or "Campaign Map" or "Armory";

    private static void HideModalNavigation(Node node)
    {
        if (node is Button button && IsModalNavigation(button.Text)) button.Hide();
        foreach (var child in node.GetChildren()) HideModalNavigation(child);
    }

    private static void CompactFooter(Node node)
    {
        if (node is Button { Visible: true } button)
        {
            button.AddThemeFontSizeOverride("font_size", RealmUi.ButtonFontSize);
            var textWidth = ModalUi.HeadingFont.GetStringSize(button.Text, fontSize: RealmUi.ButtonFontSize).X;
            var iconWidth = button.Icon == null ? 0 : button.GetThemeConstant("icon_max_width") + button.GetThemeConstant("h_separation");
            // Room for the text plus the surface's rim-clearing insets on both sides.
            button.CustomMinimumSize = new Vector2(Mathf.Max(48, textWidth + iconWidth + 2 * ModalSurface.MinimumSideInset + 4), 48);
        }
        foreach (var child in node.GetChildren()) CompactFooter(child);
    }

    private static void MarkLaunchActions(Node node)
    {
        if (node is Button button && (button.Text.StartsWith("Begin ") || button.Text.StartsWith("Deploy") || button.Text.StartsWith("Start "))) button.SetMeta("realm_primary", true);
        foreach (var child in node.GetChildren()) MarkLaunchActions(child);
    }

    public const string KeepMinimum = "modal_keep_min";

    internal static void RelaxMinimums(Control control)
    {
        if (control is BaseButton || control is TextureRect || control is HeraldicEmblem || (control is PanelContainer && control.MouseFilter == Control.MouseFilterEnum.Ignore)) return;
        // Rows that align across cards or columns keep their reserved size.
        if (!control.HasMeta(KeepMinimum))
        {
            var minHeight = control is ScrollContainer ? (int)control.GetMeta("modal_min_height", 280) : 0;
            control.CustomMinimumSize = new Vector2((int)control.GetMeta("modal_min_width", 0), minHeight);
        }
        if (control is Label label && (label.AutowrapMode != TextServer.AutowrapMode.Off || label.Text.Length > 70 || label.Text.Contains('\n'))) {
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart; label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        }
        foreach (var child in control.GetChildren().OfType<Control>()) RelaxMinimums(child);
    }
}
