using System.Linq;
using Godot;

// Presentation only: no changes to world coordinates, unit stats, or attack ranges.
public static class MobilePresentation
{
    internal static bool? TestOverride;
    public const float HudScale = 1.55f;
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

}
