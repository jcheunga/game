using System;
using System.Text.Json;
using Godot;

public partial class SaveSystem : Node
{
    private const string SaveFilePath = "user://savegame.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static SaveSystem Instance { get; private set; }

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

        if (!FileAccess.FileExists(SaveFilePath))
        {
            return false;
        }

        using var file = FileAccess.Open(SaveFilePath, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            GD.PushError($"Failed to open save file for reading: {SaveFilePath}");
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
            GD.PushError($"Failed to parse save file. {ex.Message}");
            return false;
        }
    }

    public void Save(GameSaveData saveData)
    {
        try
        {
            var json = JsonSerializer.Serialize(saveData, JsonOptions);

            using var file = FileAccess.Open(SaveFilePath, FileAccess.ModeFlags.Write);
            if (file == null)
            {
                GD.PushError($"Failed to open save file for writing: {SaveFilePath}");
                return;
            }

            file.StoreString(json);
        }
        catch (Exception ex)
        {
            GD.PushError($"Failed to write save file. {ex.Message}");
        }
    }
}
