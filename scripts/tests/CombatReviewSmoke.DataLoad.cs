using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

// A failed data load must leave the player's save exactly as it was on disk.
public partial class CombatReviewSmoke
{
    private void CheckDataLoadFailureKeepsSave()
    {
        const BindingFlags hiddenStatic = BindingFlags.Static | BindingFlags.NonPublic;
        var state = GameState.Instance;
        var savePath = SaveSystem.Instance.ActiveSaveFilePath;
        state.SetShowHints(false);
        var selectedStage = state.SelectedStage;
        var savedText = FileAccess.GetFileAsString(savePath);
        var stars = Read<List<int>>(state, "_stageStars");
        var starCount = stars.Count;
        var loadError = typeof(GameData).GetField("_loadError", hiddenStatic)!;
        var stages = typeof(GameData).GetField("_stages", hiddenStatic)!;
        var liveStages = stages.GetValue(null);

        // Mirror MarkLoadFailed without its PushError, which review runs treat as a failure.
        loadError.SetValue(null, "Simulated load failure.");
        stages.SetValue(null, Array.Empty<StageDefinition>());
        try
        {
            Check(GameData.LoadFailed && GameData.MaxStage == 0, "Simulated data failure leaves no campaign to clamp against");
            // Stage setters clamp against MaxStage and throw here; the error screen never reaches them.
            state.SetShowHints(true);
            state.ReloadFromDisk();
            Invoke(state, "NormalizeStageStars");
            Check(stars.Count == starCount && starCount > 0, $"Saved stars survive a failed data load ({stars.Count}/{starCount})");
            Check(FileAccess.GetFileAsString(savePath) == savedText, "A failed data load never rewrites the save");
        }
        finally
        {
            loadError.SetValue(null, "");
            stages.SetValue(null, liveStages);
        }

        state.ReloadFromDisk();
        Check(!GameData.LoadFailed && !state.ShowHints && state.SelectedStage == selectedStage,
            "The untouched save restores once data loads again");
    }
}
