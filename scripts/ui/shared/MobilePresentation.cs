using System.Linq;
using Godot;

// Presentation only: no changes to world coordinates, unit stats, or attack ranges.
public static class MobilePresentation
{
    internal static bool? TestOverride;
    public const float HudScale = 1.55f;
    public const float BattleZoom = 2.8f;
    public static bool Enabled => TestOverride ?? (OS.HasFeature("android") || OS.HasFeature("ios")
        || (OS.HasFeature("web") && DisplayServer.IsTouchscreenAvailable())
        || OS.GetCmdlineUserArgs().Contains("--mobile-preview"));

    public static void TouchButton(Button button)
    {
        button.CustomMinimumSize = new Vector2(Mathf.Max(56,button.CustomMinimumSize.X),Mathf.Max(56,button.CustomMinimumSize.Y));
        button.AddThemeFontSizeOverride("font_size",RealmUi.ButtonFontSize);
        button.AddThemeConstantOverride("icon_max_width",28);
        MedievalUi.StyleButton(button,8,8);
    }

    public static void ShowReport(Control host,string title,string text)
    {
        var layer=new CanvasLayer {Name="MobileReport",Layer=64}; host.AddChild(layer);
        var veil=new ColorRect {Color=new Color("080f14ed")}; layer.AddChild(veil);
        veil.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var canvas=new ResponsiveUiCanvas(); layer.AddChild(canvas); MedievalUi.Apply(canvas);
        var panel=new PanelContainer(); canvas.Content.AddChild(panel);
        var stack=new VBoxContainer(); panel.AddChild(stack);
        var row=new HBoxContainer(); stack.AddChild(row); row.AddChild(RealmUi.Heading(title,28));
        var close=RealmUi.Button("close","Close",layer.QueueFree); TouchButton(close); row.AddChild(close);
        RealmUi.Scroll(stack).AddChild(RealmUi.Label(text)); close.GrabFocus();
    }
}
