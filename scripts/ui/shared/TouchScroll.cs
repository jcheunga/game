using Godot;

/// <summary>
/// Lets a finger drag a scroll area from anywhere on its contents. Godot passes a wheel turn up through
/// panels and buttons, but a touch drag stops at the first control that catches the mouse, so on a phone a
/// page of framed panels or a list of buttons never reached its ScrollContainer and could not be dragged to
/// its end. Controls inside a vertical scroll area pass pointer events up instead. A button still takes a tap:
/// Godot cancels its press once the list starts moving, and project.godot's scroll deadzone keeps a tap's
/// small wobble from starting one.
/// </summary>
public static class TouchScroll
{
    /// <summary>Meta for a control inside a scroll area that must keep its drags to itself.</summary>
    public const string KeepDrags = "touch_keep_drags";

    public static void Install(SceneTree tree) => tree.NodeAdded += node =>
    {
        // Deferred so it runs after the control's own _Ready, which may set its filter.
        if (node is Control control) Callable.From(() => PassDrags(control)).CallDeferred();
    };

    private static void PassDrags(Control control)
    {
        if (!GodotObject.IsInstanceValid(control) || !control.IsInsideTree() || control.MouseFilter != Control.MouseFilterEnum.Stop) return;
        // Sliders, scroll bars and text fields drag themselves.
        if (control is Godot.Range or LineEdit or TextEdit or ItemList or Tree || control.HasMeta(KeepDrags)) return;
        if (InVerticalScroll(control)) control.MouseFilter = Control.MouseFilterEnum.Pass;
    }

    // Horizontal-only strips (the battle card dock) keep their cards' own drags.
    private static bool InVerticalScroll(Control control)
    {
        for (var parent = control.GetParent(); parent != null && parent is not CanvasLayer and not Window; parent = parent.GetParent())
            if (parent is ScrollContainer { VerticalScrollMode: not ScrollContainer.ScrollMode.Disabled }) return true;
        return false;
    }
}
