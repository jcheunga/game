#nullable enable
using Godot;

public partial class CodexMenu : Control
{
    private string _activeCategory = "All";
    private string? _selectedEntryId;
    private static readonly string[] Categories = { "All", "Enemies", "Bosses", "Units", "Spells", "Relics" };

    public override void _Ready()
    {
        BuildBookUi();
        RefreshBook();
    }
}
