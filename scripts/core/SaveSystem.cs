using System;
using System.Text.Json;
using Godot;

public partial class SaveSystem : Node
{
    private const string DefaultSaveFilePath = "user://savegame.json";
    private const string SaveFileArgPrefix = "--save-file=";
    private const string SaveSuffixArgPrefix = "--save-suffix=";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static SaveSystem Instance { get; private set; }
    public string ActiveSaveFilePath => ResolveSaveFilePath();
    public bool LoadedFromBackup { get; private set; }
    private bool _recoveryNoticePending;
    public bool LastSaveSucceeded { get; private set; } = true;
    public string LastSaveError { get; private set; } = "";
    public string PlayerStatus => !LastSaveSucceeded
        ? "Progress could not be saved. Free some storage and try again."
        : LoadedFromBackup ? "Progress recovered from the local backup." : "Progress is saved on this device.";

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _ExitTree()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public bool TryLoad(out GameSaveData saveData)
    {
        saveData = new GameSaveData();
        LoadedFromBackup = false;
        var saveFilePath = ResolveSaveFilePath();

        if (TryLoadFromPath(saveFilePath, out saveData))
        {
            return true;
        }

        // Main save failed or missing — try backup
        var backupPath = saveFilePath + ".bak";
        if (FileAccess.FileExists(backupPath))
        {
            GD.PushWarning($"Main save failed, attempting backup: {backupPath}");
            if (TryLoadFromPath(backupPath, out saveData))
            {
                LoadedFromBackup = true;
                _recoveryNoticePending = true;
                return true;
            }
        }

        saveData = new GameSaveData();
        return false;
    }

    private static bool TryLoadFromPath(string path, out GameSaveData saveData)
    {
        saveData = new GameSaveData();

        if (!FileAccess.FileExists(path))
        {
            return false;
        }

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            GD.PushError($"Failed to open save file for reading: {path}");
            return false;
        }

        var json = file.GetAsText();
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<GameSaveData>(json, JsonOptions);
            if (parsed == null)
            {
                return false;
            }

            saveData = parsed;
            return true;
        }
        catch (Exception ex)
        {
            GD.PushError($"Failed to parse save file {path}. {ex.Message}");
            return false;
        }
    }

    public void Save(GameSaveData saveData)
    {
        LastSaveSucceeded = false;
        LastSaveError = "";
        try
        {
            var json = JsonSerializer.Serialize(saveData, JsonOptions);
            var saveFilePath = ResolveSaveFilePath();
            var tempPath = saveFilePath + ".tmp";
            var backupPath = saveFilePath + ".bak";

            // Write to temp file first
            using (var file = FileAccess.Open(tempPath, FileAccess.ModeFlags.Write))
            {
                if (file == null)
                {
                    FailSave($"Failed to open temp save file for writing: {tempPath}");
                    return;
                }

                file.StoreString(json);
                file.Flush();
                if (file.GetError() != Error.Ok)
                {
                    FailSave($"Save write failed: {file.GetError()}");
                    return;
                }
            }

            // Parse the complete staged save before touching the current save or backup.
            if (!TryLoadFromPath(tempPath, out _))
            {
                FailSave("Temp save file verification failed, keeping the current save.");
                return;
            }

            // Copy first: a failed backup or replacement must leave the current save intact.
            if (FileAccess.FileExists(saveFilePath) && TryLoadFromPath(saveFilePath, out _))
            {
                var backupError = DirAccess.CopyAbsolute(saveFilePath, backupPath);
                if (backupError != Error.Ok)
                {
                    FailSave($"Save backup failed ({backupError}); keeping the current save.");
                    return;
                }
            }
            var replaceError = DirAccess.RenameAbsolute(tempPath, saveFilePath);
            if (replaceError != Error.Ok)
                FailSave($"Save replacement failed ({replaceError}); previous save and backup retained.");
            else LastSaveSucceeded = true;

        }
        catch (Exception ex)
        {
            FailSave($"Failed to write save file. {ex.Message}");
        }
    }

    private void FailSave(string reason)
    {
        LastSaveError = reason;
        GD.PushError(reason);
    }

    public bool ConsumeRecoveryNotice()
    {
        var pending = _recoveryNoticePending;
        _recoveryNoticePending = false;
        return pending;
    }

    private static string ResolveSaveFilePath()
    {
        foreach (var arg in GetCommandLineArguments())
        {
            if (arg.StartsWith(SaveFileArgPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var explicitPath = arg[SaveFileArgPrefix.Length..].Trim();
                if (!string.IsNullOrWhiteSpace(explicitPath))
                {
                    return explicitPath;
                }
            }

            if (arg.StartsWith(SaveSuffixArgPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var suffix = SanitizeFileToken(arg[SaveSuffixArgPrefix.Length..]);
                if (!string.IsNullOrWhiteSpace(suffix))
                {
                    return $"user://savegame.{suffix}.json";
                }
            }
        }

        return DefaultSaveFilePath;
    }

    private static string[] GetCommandLineArguments()
    {
        var userArgs = OS.GetCmdlineUserArgs();
        return userArgs.Length > 0 ? userArgs : OS.GetCmdlineArgs();
    }

    private static string SanitizeFileToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        Span<char> buffer = stackalloc char[value.Length];
        var length = 0;
        foreach (var ch in value.Trim())
        {
            if (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_')
            {
                buffer[length++] = char.ToLowerInvariant(ch);
            }
        }

        return length == 0 ? "" : new string(buffer[..length]);
    }
}
