using System.Collections.Generic;
using Godot;

/// <summary>
/// First-time battle hints, one at a time in a dark, gold-edged banner above the card dock. A hint stays up long
/// enough to read (longer for longer text); a tap dismisses it early and the next queued hint follows.
/// </summary>
public partial class BattleHintBanner : PanelContainer
{
    private readonly Queue<(string Title, string Body)> _queue = new();
    private readonly RoyalLabel _title;
    private readonly Label _body;
    private float _remaining;
    private Vector2 _viewport = new(1280, 720);
    private float _dockTop = 540;

    public BattleHintBanner()
    {
        Name = "BattleHint";
        Visible = false;
        ZIndex = 40;
        MouseFilter = MouseFilterEnum.Stop;
        ProcessMode = ProcessModeEnum.Pausable;
        var style = new StyleBoxFlat
        {
            BgColor = new Color(.06f, .07f, .09f, .93f), BorderColor = new Color("c9a463"), AntiAliasing = true,
            ContentMarginLeft = 22, ContentMarginRight = 22, ContentMarginTop = 10, ContentMarginBottom = 12
        };
        style.SetBorderWidthAll(1); style.SetCornerRadiusAll(8);
        AddThemeStyleboxOverride("panel", style);
        var stack = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        stack.AddThemeConstantOverride("separation", 3);
        AddChild(stack);
        _title = RoyalText.Serif("", 21, new Color("e8c787"));
        _title.MouseFilter = MouseFilterEnum.Ignore;
        stack.AddChild(_title);
        _body = RoyalText.Paragraph("", 18, RoyalText.Cream);
        _body.MouseFilter = MouseFilterEnum.Ignore;
        _body.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        stack.AddChild(_body);
    }

    public string Title => _title.Text;
    public string Body => _body.Text;
    public int Queued => _queue.Count;

    public void Enqueue(string title, string body)
    {
        _queue.Enqueue((title, body));
        if (!Visible) ShowNext();
    }

    /// <summary>Lays the banner out centred above the card dock, as wide as its text needs (up to 640 px).</summary>
    public void Place(Vector2 viewport, float dockTop)
    {
        _viewport = viewport; _dockTop = dockTop;
        var width = Mathf.Min(640, viewport.X - 32);
        CustomMinimumSize = new Vector2(width, 0);
        Size = new Vector2(width, 0);
        ResetSize();
        Position = new Vector2((viewport.X - width) / 2, dockTop - 14 - Size.Y);
    }

    public override void _GuiInput(InputEvent input)
    {
        if (input is InputEventMouseButton { Pressed: true } or InputEventScreenTouch { Pressed: true })
        {
            AcceptEvent();
            ShowNext();
        }
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;
        _remaining -= (float)delta;
        if (_remaining <= 0) ShowNext();
    }

    private void ShowNext()
    {
        if (!_queue.TryDequeue(out var hint)) { Visible = false; return; }
        _title.Text = hint.Title;
        _body.Text = hint.Body;
        // About four seconds plus reading time.
        _remaining = 4f + hint.Body.Length / 22f;
        Visible = true;
        Place(_viewport, _dockTop);
    }
}
