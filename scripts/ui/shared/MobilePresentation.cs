using Godot;

// The game has one presentation, sized for a phone: one 1280x720 canvas (project.godot stretch "canvas_items",
// aspect "keep") scaled to fit every screen, so a phone, a tablet and a monitor all show exactly the same picture,
// letterboxed where their shape differs. Presentation only: no changes to world coordinates, unit stats, or
// attack ranges.
public static class MobilePresentation
{
    /// <summary>Menus and modals draw on a smaller logical canvas scaled up by this, so their type and buttons
    /// read and take a thumb on a phone.</summary>
    public const float HudScale = 1.55f;
    /// <summary>The battle HUD keeps the battle concept's own sizes: its cards, meters and buttons were drawn
    /// large enough for a phone, and a larger HUD would crowd the road.</summary>
    public const float BattleHudScale = 1f;

    public static void TouchButton(Button button)
    {
        button.CustomMinimumSize = new Vector2(Mathf.Max(56,button.CustomMinimumSize.X),Mathf.Max(56,button.CustomMinimumSize.Y));
        button.AddThemeConstantOverride("icon_max_width",28);
        // The kit's materials and engraved captions, as on every other screen (gold when realm_primary).
        ModalUi.StyleButton(button);
    }

}
