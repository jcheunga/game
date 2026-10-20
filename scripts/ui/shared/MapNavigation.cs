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

    /// <summary>
    /// Back to the map on a plate that paints the button: the plate's own surface (gold, steel or dark, as its
    /// concept has it) with the map icon and capitals centred on it. A gold plate takes dark ink.
    /// </summary>
    public static RoyalButton Fixed(Rect2 rect, Action action, string text = Caption, bool gold = false, int size = 18)
    {
        text = Normalize(text);
        var button = RoyalButton.Over(rect, text, action, 8);
        var label = RoyalText.Caps(text, size, gold ? new Color("1e0f04") : ModalUi.Cream, 700);
        var icon = Mathf.Round(Mathf.Clamp(rect.Size.Y * .5f, 26, 40));
        button.Arrange(label, RoyalKit.Texture("icon-map-large"), new Vector2(Mathf.Round(icon * 1.2f), icon), gap: Mathf.Round(icon * .35f));
        // Dark lettering pressed into gold keeps a faint light edge below it instead of a dark drop shadow.
        if (gold) { label.ShadowOffset = new Vector2(0, 1); label.ShadowInk = new Color(1f, .92f, .7f, .35f); }
        return button;
    }
}
