using System;
using Godot;

/// <summary>Calls <see cref="Tick"/> every rendered frame, also after the battle ends, for presentation that keeps
/// playing behind the result board (a stronghold collapsing, the caravan's flag still flying).</summary>
public partial class BattleTicker : Node
{
    public Action<float> Tick { get; set; }
    public override void _Ready() => ProcessMode = ProcessModeEnum.Always;
    public override void _Process(double delta) => Tick?.Invoke((float)delta);
    public override void _ExitTree() => Tick = null;
}
