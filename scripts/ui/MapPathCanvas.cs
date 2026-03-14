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
        var route = RouteCatalog.Get(ActiveMapId);
        DrawBackground(route);
        DrawRouteDecor(route);
        DrawRouteLines(route);
        DrawStageNodes(route);
    }

    private void DrawBackground(RouteDefinition route)
    {
        DrawRect(new Rect2(Vector2.Zero, Size), route.BackgroundBottom, true);
        DrawRect(new Rect2(0f, 0f, Size.X, Size.Y * 0.44f), route.BackgroundTop, true);

        for (var i = 0; i < 10; i++)
        {
            var t = i / 9f;
            var y = Mathf.Lerp(58f, Size.Y - 42f, t);
            DrawLine(
                new Vector2(32f, y),
                new Vector2(Size.X - 32f, y),
                new Color(route.RouteGlow, 0.045f + (i * 0.003f)),
                1.2f,
                true);
        }
    }

    private void DrawRouteDecor(RouteDefinition route)
    {
        var mapId = NormalizeMapId(ActiveMapId);
        if (mapId == "harbor")
        {
            DrawRect(new Rect2(0f, Size.Y - 138f, Size.X, 98f), new Color("1b4965", 0.38f), true);
            for (var i = 0; i < 5; i++)
            {
                var x = Mathf.Lerp(110f, Size.X - 110f, i / 4f);
                DrawRect(new Rect2(x - 42f, 78f + (i % 2) * 28f, 84f, 20f), new Color(0f, 0f, 0f, 0.18f), true);
                DrawRect(new Rect2(x - 18f, Size.Y - 174f + ((i + 1) % 2) * 14f, 36f, 54f), new Color(1f, 1f, 1f, 0.06f), true);
            }
        }
        else if (mapId == "foundry")
        {
            DrawRect(new Rect2(0f, Size.Y - 108f, Size.X, 52f), new Color("311d18", 0.46f), true);
            for (var i = 0; i < 6; i++)
            {
                var x = Mathf.Lerp(72f, Size.X - 72f, i / 5f);
                DrawLine(
                    new Vector2(x - 36f, Size.Y - 132f),
                    new Vector2(x + 48f, 112f + ((i % 2) * 24f)),
                    new Color(1f, 0.62f, 0.2f, 0.08f),
                    14f,
                    true);
                DrawRect(new Rect2(x - 28f, 84f + ((i + 1) % 2) * 36f, 56f, 16f), new Color(0f, 0f, 0f, 0.22f), true);
            }

            for (var i = 0; i < 5; i++)
            {
                var x = Mathf.Lerp(90f, Size.X - 90f, i / 4f);
                DrawLine(
                    new Vector2(x - 42f, Size.Y - 88f),
                    new Vector2(x + 42f, Size.Y - 88f),
                    new Color(1f, 0.7f, 0.32f, 0.28f),
                    3f,
                    true);
            }
        }
        else if (mapId == "quarantine")
        {
            DrawRect(new Rect2(0f, Size.Y - 124f, Size.X, 72f), new Color("183a37", 0.44f), true);

            for (var i = 0; i < 7; i++)
            {
                var x = Mathf.Lerp(78f, Size.X - 78f, i / 6f);
                DrawRect(new Rect2(x - 38f, 96f + ((i + 1) % 2) * 26f, 76f, 18f), new Color(1f, 0.96f, 0.62f, 0.12f), true);
                DrawLine(
                    new Vector2(x - 44f, Size.Y - 142f),
                    new Vector2(x + 34f, Size.Y - 72f),
                    new Color(0.85f, 1f, 0.78f, 0.08f),
                    10f,
                    true);
            }

            for (var i = 0; i < 10; i++)
            {
                var x = Mathf.Lerp(42f, Size.X - 42f, i / 9f);
                var y = Size.Y - 110f;
                DrawLine(
                    new Vector2(x - 18f, y - 10f),
                    new Vector2(x + 8f, y + 10f),
                    new Color(1f, 0.95f, 0.55f, 0.26f),
                    4f,
                    true);
            }
        }
        else if (mapId == "thornwall")
        {
            DrawRect(new Rect2(0f, Size.Y - 132f, Size.X, 84f), new Color("223140", 0.54f), true);

            for (var i = 0; i < 5; i++)
            {
                var left = Mathf.Lerp(-24f, Size.X - 180f, i / 4f);
                var peak = left + 90f + ((i % 2) * 22f);
                var right = left + 190f;
                DrawColoredPolygon(
                    new[]
                    {
                        new Vector2(left, Size.Y - 90f),
                        new Vector2(peak, 118f + ((i % 2) * 26f)),
                        new Vector2(right, Size.Y - 90f)
                    },
                    new Color("d8e2f0", 0.12f));
                DrawLine(
                    new Vector2(peak - 20f, 132f + ((i % 2) * 24f)),
                    new Vector2(peak + 10f, 188f + ((i % 2) * 18f)),
                    new Color(1f, 1f, 1f, 0.16f),
                    4f,
                    true);
            }

            for (var i = 0; i < 10; i++)
            {
                var x = Mathf.Lerp(54f, Size.X - 54f, i / 9f);
                var y = 72f + ((i % 3) * 18f);
                DrawLine(
                    new Vector2(x - 16f, y),
                    new Vector2(x + 8f, y + 22f),
                    new Color("f1faee", 0.18f),
                    3f,
                    true);
            }
        }
        else if (mapId == "basilica")
        {
            DrawRect(new Rect2(0f, Size.Y - 128f, Size.X, 80f), new Color("2a241d", 0.56f), true);

            for (var i = 0; i < 5; i++)
            {
                var x = Mathf.Lerp(92f, Size.X - 92f, i / 4f);
                DrawArc(
                    new Vector2(x, 132f + ((i % 2) * 18f)),
                    44f,
                    Mathf.Pi,
                    Mathf.Tau,
                    18,
                    new Color("fef3c7", 0.12f),
                    4f);
                DrawLine(
                    new Vector2(x - 44f, 132f + ((i % 2) * 18f)),
                    new Vector2(x - 44f, Size.Y - 122f),
                    new Color(1f, 0.97f, 0.86f, 0.08f),
                    5f,
                    true);
                DrawLine(
                    new Vector2(x + 44f, 132f + ((i % 2) * 18f)),
                    new Vector2(x + 44f, Size.Y - 122f),
                    new Color(1f, 0.97f, 0.86f, 0.08f),
                    5f,
                    true);
            }

            for (var i = 0; i < 12; i++)
            {
                var x = Mathf.Lerp(42f, Size.X - 42f, i / 11f);
                DrawCircle(new Vector2(x, Size.Y - 92f - ((i % 2) * 8f)), 3f, new Color("ffd166", 0.55f));
            }
        }
        else if (mapId == "mire")
        {
            DrawRect(new Rect2(0f, Size.Y - 136f, Size.X, 92f), new Color("20331f", 0.58f), true);

            for (var i = 0; i < 6; i++)
            {
                var x = Mathf.Lerp(76f, Size.X - 76f, i / 5f);
                DrawCircle(new Vector2(x, Size.Y - 98f + ((i % 2) * 10f)), 38f, new Color("90be6d", 0.14f));
                DrawRect(new Rect2(x - 8f, 104f + ((i % 2) * 22f), 16f, 86f), new Color(1f, 1f, 1f, 0.06f), true);
            }

            for (var i = 0; i < 10; i++)
            {
                var x = Mathf.Lerp(48f, Size.X - 48f, i / 9f);
                DrawLine(
                    new Vector2(x - 18f, Size.Y - 76f - ((i % 3) * 6f)),
                    new Vector2(x + 18f, Size.Y - 82f - ((i % 3) * 6f)),
                    new Color("ecf39e", 0.18f),
                    3f,
                    true);
            }
        }
        else
        {
            for (var i = 0; i < 7; i++)
            {
                var x = Mathf.Lerp(64f, Size.X - 64f, i / 6f);
                DrawLine(
                    new Vector2(x - 42f, Size.Y - 104f),
                    new Vector2(x + 18f, 96f),
                    new Color(1f, 1f, 1f, 0.05f),
                    18f,
                    true);
            }

            DrawRect(new Rect2(0f, Size.Y - 92f, Size.X, 42f), new Color(0f, 0f, 0f, 0.12f), true);
            for (var i = 0; i < 18; i++)
            {
                var x = Mathf.Lerp(32f, Size.X - 32f, i / 17f);
                DrawRect(new Rect2(x - 7f, Size.Y - 74f, 14f, 4f), new Color(route.BannerAccent, 0.35f), true);
            }
        }
    }

    private void DrawRouteLines(RouteDefinition route)
    {
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

            if (!IsStageVisible(fromMap) || !IsStageVisible(toMap) || !fromMap.Equals(toMap, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var unlocked = stage < HighestUnlockedStage;
            var lineColor = unlocked ? route.RouteColor : new Color(route.LockedNode, 0.55f);
            var glowColor = unlocked ? route.RouteGlow : new Color(0f, 0f, 0f, 0.22f);

            DrawLine(from, to, glowColor, 11f, true);
            DrawLine(from, to, lineColor, 5f, true);
        }
    }

    private void DrawStageNodes(RouteDefinition route)
    {
        foreach (var pair in StagePoints)
        {
            var stage = pair.Key;
            var point = pair.Value;

            if (StageMapIds.TryGetValue(stage, out var mapId) && !IsStageVisible(mapId))
            {
                continue;
            }

            var unlocked = stage <= HighestUnlockedStage;
            var completed = stage < HighestUnlockedStage;
            var isSelected = stage == SelectedStage;

            var nodeColor = unlocked ? route.UnlockedNode : route.LockedNode;
            if (isSelected)
            {
                nodeColor = route.SelectedNode;
            }

            if (completed)
            {
                DrawCircle(point, 44f, new Color(route.RouteGlow, 0.16f));
            }

            if (isSelected)
            {
                DrawArc(point, 48f, 0f, Mathf.Tau, 36, new Color(route.SelectedNode, 0.75f), 5f);
            }

            DrawCircle(point, 38f, new Color(0f, 0f, 0f, 0.34f));
            DrawCircle(point, 31f, nodeColor);
            DrawCircle(point, 22f, nodeColor.Lightened(0.12f));

            if (completed)
            {
                DrawRect(new Rect2(point + new Vector2(18f, -34f), new Vector2(10f, 10f)), route.Accent, true);
            }

            if (!unlocked)
            {
                DrawArc(point + new Vector2(0f, 2f), 10f, Mathf.Pi, Mathf.Tau, 14, new Color(1f, 1f, 1f, 0.75f), 3f);
                DrawRect(new Rect2(point + new Vector2(-8f, 2f), new Vector2(16f, 12f)), new Color(1f, 1f, 1f, 0.75f), true);
            }
        }
    }

    private bool IsStageVisible(string mapId)
    {
        return NormalizeMapId(mapId).Equals(NormalizeMapId(ActiveMapId), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeMapId(string mapId)
    {
        return string.IsNullOrWhiteSpace(mapId) ? "city" : mapId.Trim().ToLowerInvariant();
    }
}
