using Godot;
using System;

public partial class BattleShadowCanvas : Node2D
{
    public Action<CanvasItem> Paint { get; set; }
    public override void _Process(double delta) => QueueRedraw();
    public override void _Draw() => Paint?.Invoke(this);
    public override void _ExitTree() => Paint = null;
}
