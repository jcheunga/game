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
                    GD.PushError($"Failed to open temp save file for writing: {tempPath}");
                    return;
                }

                file.StoreString(json);
            }

            // Verify the temp file is valid before replacing
            using (var verify = FileAccess.Open(tempPath, FileAccess.ModeFlags.Read))
            {
                if (verify == null || string.IsNullOrWhiteSpace(verify.GetAsText()))
                {
                    GD.PushError("Temp save file verification failed, aborting save.");
                    return;
                }
            }

            // Rotate backup: current save -> .bak
            if (FileAccess.FileExists(saveFilePath))
            {
                if (FileAccess.FileExists(backupPath))
                {
                    DirAccess.RemoveAbsolute(backupPath);
                }
                DirAccess.RenameAbsolute(saveFilePath, backupPath);
            }

            // Atomic rename: .tmp -> save
            DirAccess.RenameAbsolute(tempPath, saveFilePath);
        }
        catch (Exception ex)
        {
            GD.PushError($"Failed to write save file. {ex.Message}");
        }
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
