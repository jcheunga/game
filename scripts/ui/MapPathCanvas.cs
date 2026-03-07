using System;
using System.Collections.Generic;
using Godot;

public partial class MapPathCanvas : Control
{
    public Dictionary<int, Vector2> StagePoints { get; } = new();
    public Dictionary<int, string> StageMapIds { get; } = new();
    public int HighestUnlockedStage { get; set; } = 1;
    public int SelectedStage { get; set; } = 1;
    public string ActiveMapId { get; set; } = "city";

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), GetMapBackgroundColor(), true);

        for (var stage = 1; stage < GameData.MaxStage; stage++)
        {
            if (!StagePoints.TryGetValue(stage, out var from) || !StagePoints.TryGetValue(stage + 1, out var to))
            {
                continue;
            }

            if (!StageMapIds.TryGetValue(stage, out var fromMap) || !StageMapIds.TryGetValue(stage + 1, out var toMap))
            {
                continue;
            }

            if (!IsStageVisible(fromMap) || !IsStageVisible(toMap))
            {
                continue;
            }

            if (!fromMap.Equals(toMap, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var routeColor = stage < HighestUnlockedStage ? new Color("6fffe9") : new Color("3a506b");
            DrawLine(from, to, routeColor, 6f, true);
        }

        foreach (var pair in StagePoints)
        {
            var stage = pair.Key;
            var point = pair.Value;

            if (StageMapIds.TryGetValue(stage, out var mapId) && !IsStageVisible(mapId))
            {
                continue;
            }

            var unlocked = stage <= HighestUnlockedStage;
            var color = unlocked ? new Color("5bc0be") : new Color("1c2541");

            if (stage == SelectedStage)
            {
                color = new Color("ffd166");
            }

            DrawCircle(point, 32f, color);
            DrawCircle(point, 38f, new Color(0f, 0f, 0f, 0.45f));
        }
    }

    private bool IsStageVisible(string mapId)
    {
        return mapId.Equals(ActiveMapId, StringComparison.OrdinalIgnoreCase);
    }

    private Color GetMapBackgroundColor()
    {
        return ActiveMapId.ToLowerInvariant() switch
        {
            "harbor" => new Color("10253f"),
            _ => new Color("0d1321")
        };
    }
}
