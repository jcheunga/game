using System;
using System.Text.Json;
using Godot;

public partial class GameState
{
    public string AccountProvider { get; private set; } = "";
    public string AccountLabel { get; private set; } = "";
    public bool SkipAutoSignIn { get; private set; }
    /// <summary>
    /// Set when the device found its account deleted elsewhere and reset itself; home explains it once.
    /// It is saved, so the explanation still comes if the game closes before home opens.
    /// </summary>
    public bool AccountDeletedNoticePending { get; private set; }
    public void AcknowledgeAccountDeletedNotice() { if (!AccountDeletedNoticePending) return; AccountDeletedNoticePending = false; Persist(); }
    public void SetSignedInAccount(string provider, string label)
    { AccountProvider = provider; AccountLabel = label; SkipAutoSignIn = false; Persist(); }
    /// <summary>After an explicit sign-out the player stays a guest until they choose an account again.</summary>
    public void PauseAutoSignIn() { SkipAutoSignIn = true; Persist(); }

    private const string AccountBackupFolder = "user://account-backups";

    public void StartAccountSave()
    {
        var previous = BuildSaveData();
        DirAccess.MakeDirRecursiveAbsolute(AccountBackupFolder);
        using (var backup = Godot.FileAccess.Open($"{AccountBackupFolder}/{PlayerProfileId}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.json", Godot.FileAccess.ModeFlags.Write))
        {
            if (backup == null) throw new InvalidOperationException("Could not back up current progress. Sign-in was stopped.");
            backup.StoreString(JsonSerializer.Serialize(previous));
        }
        StartNewGuest(previous);
    }

    /// <summary>
    /// After the server deleted this account, the device forgets it too: progress, the account's local
    /// backups and the save's backup copy go, while sound, language and privacy choices stay. The new
    /// guest does not sign in by itself, so the phone's game account is not linked again unasked.
    /// When the deletion happened elsewhere (another device, support), home tells the player once why.
    /// </summary>
    public void ForgetDeletedAccount(bool deletedElsewhere = false)
    {
        var previous = BuildSaveData();
        using (var folder = DirAccess.Open(AccountBackupFolder))
        {
            if (folder != null)
                foreach (var file in folder.GetFiles())
                    if (file.StartsWith(previous.PlayerProfileId + "-", StringComparison.Ordinal) && file.EndsWith(".json", StringComparison.Ordinal) &&
                        long.TryParse(file[(previous.PlayerProfileId.Length + 1)..^5], out _))
                        folder.Remove(file);
        }
        StartNewGuest(previous);
        SkipAutoSignIn = true;
        AccountDeletedNoticePending = deletedElsewhere;
        LastResultMessage = "Account deleted. You're playing as a new guest.";
        // Each save copies the one before it into the backup, so a second save leaves the deleted account in neither file.
        Persist();
    }

    private void StartNewGuest(GameSaveData previous)
    {
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
