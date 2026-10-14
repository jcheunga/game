using System;
using Godot;

/// <summary>One presentation for actions that return to the map or their parent activity.</summary>
public static class MapNavigation
{
    public const int FontSize = 20;
    public const string Caption = "Back to Map";
    public static string Normalize(string text) => text.Equals("Back to map", StringComparison.OrdinalIgnoreCase)
        || text.Equals("Campaign Map", StringComparison.OrdinalIgnoreCase)
        || text.Equals("Back to Campaign Map", StringComparison.OrdinalIgnoreCase) ? Caption : text;

    public static Button Button(Action action)
    {
        var button = RealmUi.Button("map", Caption, action);
        button.Icon = RoyalKit.Texture("icon-map-scroll");
        button.CustomMinimumSize = new Vector2(240, 52);
        button.AddThemeConstantOverride("icon_max_width", 28);
        button.SetMeta("map_navigation", true);
        ModalUi.StyleButton(button);
        return button;
    }

    public static RoyalButton Fixed(Rect2 rect, Action action, string text = Caption)
    {
        text = Normalize(text);
        var button = RoyalButton.Over(rect, text, action, 8);
        button.SetStates(new ModalSurface(ModalMaterial.Steel, 8));
        var label = RoyalText.Serif(text, FontSize, ModalUi.Cream, 500);
        label.ShadowOffset = Vector2.Zero;
        var width = label.TextWidth(label.FontSize);
        var left = (rect.Size.X - width - 38) / 2;
        button.SetGlyph(RoyalKit.Texture("icon-map-scroll"), new Rect2(left, (rect.Size.Y - 28) / 2, 28, 28));
        button.SetCaption(label, new Rect2(left + 38, 0, width, rect.Size.Y));
        return button;
    }
}
