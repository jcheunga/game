using System;
using Godot;

public partial class GameState
{
    public bool DeveloperModeEnabled { get; private set; }
    public static bool DeveloperModeAvailable => OS.IsDebugBuild();
    public event Action DeveloperStateChanged;

    public void SetDeveloperMode(bool enabled)
    {
        DeveloperModeEnabled = enabled && DeveloperModeAvailable;
        Persist();
        DeveloperStateChanged?.Invoke();
    }

    /// <summary>Local testing grants bypass billing, purchase counts and progression rewards.</summary>
    public bool TryAddDeveloperResources(int gold, int food)
    {
        if (!DeveloperModeAvailable || !DeveloperModeEnabled || gold < 0 || food < 0
            || (gold == 0 && food == 0) || gold > int.MaxValue - Gold || food > int.MaxValue - Food)
            return false;

        Gold += gold;
        Food += food;
        Persist();
        DeveloperStateChanged?.Invoke();
        return true;
    }
}
