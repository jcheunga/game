public partial class GameState
{
    public void SaveNow() => Persist();

    /// <summary>Restore player preferences without changing progress, identity, consent or connections.</summary>
    public void RestorePlayerPreferences()
    {
        SetAudioMuted(false);
        SetEffectsVolumePercent(85);
        SetAmbienceVolumePercent(65);
        SetMusicVolumePercent(50);
        SetLanguage("en");
        SetFontSizeOffset(0);
        SetHighContrast(false);
        SetReducedMotion(false);
        SetLargeTextMode(false);
        SetColorblindMode("none");
        SetShowDevUi(false);
        SetDeveloperMode(false);
        SetShowFpsCounter(false);
        SetDifficulty(DifficultyCatalog.NormalId);
        SetShowHints(true);
    }
}
