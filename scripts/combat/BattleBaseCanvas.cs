using Godot;
using System;

/// <summary>Ground-positioned bases participate in the same depth sorting as troops.</summary>
public partial class BattleBaseCanvas : Node2D
{
    public Action<CanvasItem> Paint { get; set; }
    public override void _Process(double delta) => QueueRedraw();
    public override void _Draw()
    {
        DrawSetTransform(-Position);
        Paint?.Invoke(this);
    }
    public override void _ExitTree() => Paint = null;
}
