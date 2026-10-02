using System.Collections.Generic;
using Godot;

/// <summary>Reflow newly rebuilt activity rows without changing game actions.</summary>
public partial class ModalActivityLayout : Node
{
    public Control Body;
    private readonly HashSet<ulong> _dressed = new();
    private double _elapsed;
    public override void _Process(double delta)
    {
        _elapsed += delta; if (_elapsed < .2 || Body == null) return; _elapsed = 0;
        Visit(Body);
    }
    private void Visit(Node node)
    {
        if (node is BaseButton) { if (_dressed.Add(node.GetInstanceId())) ModalUi.Dress(node); return; } // Button's own icon/text group must not wrap.
        if (_dressed.Add(node.GetInstanceId()) && node is Control control)
        {
            RealmModal.RelaxMinimums(control);
            ModalUi.Dress(control);
        }
        foreach (var child in node.GetChildren()) Visit(child);
    }
}
