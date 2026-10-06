using Godot;

/// <summary>A short confirmation that fades out over the screen that raised it.</summary>
public partial class RoyalToast : PanelContainer
{
    public static void Show(Control host, string message, float y = 664)
    {
        foreach (var old in host.GetChildren())
            if (old is RoyalToast toast) toast.QueueFree();
        var panel = new RoyalToast { MouseFilter = MouseFilterEnum.Ignore, ZIndex = 50 };
        var style = new StyleBoxFlat { BgColor = new Color(.06f, .07f, .09f, .92f), BorderColor = new Color("c9a463"), ContentMarginLeft = 18, ContentMarginRight = 18, ContentMarginTop = 8, ContentMarginBottom = 9, AntiAliasing = true };
        style.SetBorderWidthAll(1); style.SetCornerRadiusAll(6);
        panel.AddThemeStyleboxOverride("panel", style);
        var text = RoyalText.Paragraph(message, 19, RoyalText.Cream, 500, HorizontalAlignment.Center);
        text.AutowrapMode = TextServer.AutowrapMode.Off;
        panel.AddChild(text);
        host.AddChild(panel);
        panel.ResetSize();
        panel.Position = new Vector2((RoyalArt.Canvas.X - panel.Size.X) / 2, y - panel.Size.Y);
        var tween = panel.CreateTween();
        tween.TweenInterval(2.2);
        tween.TweenProperty(panel, "modulate:a", 0f, .45);
        tween.TweenCallback(Callable.From(panel.QueueFree));
    }
}
