using System;
using System.Text.Json;
using Godot;

public partial class GameState
{
    public string AccountProvider { get; private set; } = "";
    public string AccountLabel { get; private set; } = "";
    public void SetSignedInAccount(string provider, string label)
    { AccountProvider = provider; AccountLabel = label; Persist(); }

    public void StartAccountSave()
    {
        var previous = BuildSaveData();
        var folder = "user://account-backups";
        DirAccess.MakeDirRecursiveAbsolute(folder);
        using (var backup = Godot.FileAccess.Open($"{folder}/{PlayerProfileId}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.json", Godot.FileAccess.ModeFlags.Write))
        {
            if (backup == null) throw new InvalidOperationException("Could not back up current progress. Sign-in was stopped.");
            backup.StoreString(JsonSerializer.Serialize(previous));
        }
        ApplyDefaults();
        AudioMuted = previous.AudioMuted; EffectsVolumePercent = previous.EffectsVolumePercent;
        AmbienceVolumePercent = previous.AmbienceVolumePercent; MusicVolumePercent = previous.MusicVolumePercent;
        Language = previous.Language; ShowHints = previous.ShowHints; ReducedMotion = previous.ReducedMotion;
        AnalyticsConsent = previous.AnalyticsConsent; HasShownConsentPrompt = previous.HasShownConsentPrompt;
        CrashReportingConsent = previous.CrashReportingConsent;
        ChallengeSyncProviderId = previous.ChallengeSyncProviderId; ChallengeSyncEndpoint = previous.ChallengeSyncEndpoint;
        _purchaseValidationEndpoint = previous.PurchaseValidationEndpoint;
        Persist();
    }
}
