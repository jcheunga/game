using System;
using System.Collections.Generic;
using Godot;

public partial class MapPathCanvas : Control
{
    private readonly struct RouteTheme
    {
        public RouteTheme(
            Color backgroundTop,
            Color backgroundBottom,
            Color routeColor,
            Color routeGlow,
            Color unlockedNode,
            Color lockedNode,
            Color selectedNode,
            Color accent)
        {
            BackgroundTop = backgroundTop;
            BackgroundBottom = backgroundBottom;
            RouteColor = routeColor;
            RouteGlow = routeGlow;
            UnlockedNode = unlockedNode;
            LockedNode = lockedNode;
            SelectedNode = selectedNode;
            Accent = accent;
        }

        public Color BackgroundTop { get; }
        public Color BackgroundBottom { get; }
        public Color RouteColor { get; }
        public Color RouteGlow { get; }
        public Color UnlockedNode { get; }
        public Color LockedNode { get; }
        public Color SelectedNode { get; }
        public Color Accent { get; }
    }

    public Dictionary<int, Vector2> StagePoints { get; } = new();
    public Dictionary<int, string> StageMapIds { get; } = new();
    public int HighestUnlockedStage { get; set; } = 1;
    public int SelectedStage { get; set; } = 1;
    public string ActiveMapId { get; set; } = "city";

    public override void _Draw()
    {
        var theme = ResolveTheme();
        DrawBackground(theme);
        DrawRouteDecor(theme);
        DrawRouteLines(theme);
        DrawStageNodes(theme);
    }

    private void DrawBackground(RouteTheme theme)
    {
        DrawRect(new Rect2(Vector2.Zero, Size), theme.BackgroundBottom, true);
        DrawRect(new Rect2(0f, 0f, Size.X, Size.Y * 0.44f), theme.BackgroundTop, true);

        for (var i = 0; i < 10; i++)
        {
            var t = i / 9f;
            var y = Mathf.Lerp(58f, Size.Y - 42f, t);
            DrawLine(
                new Vector2(32f, y),
                new Vector2(Size.X - 32f, y),
                new Color(theme.RouteGlow, 0.045f + (i * 0.003f)),
                1.2f,
                true);
        }
    }

    private void DrawRouteDecor(RouteTheme theme)
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
                DrawRect(new Rect2(x - 7f, Size.Y - 74f, 14f, 4f), new Color(1f, 0.92f, 0.52f, 0.35f), true);
            }
        }
    }

    private void DrawRouteLines(RouteTheme theme)
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
            var lineColor = unlocked ? theme.RouteColor : new Color(theme.LockedNode, 0.55f);
            var glowColor = unlocked ? theme.RouteGlow : new Color(0f, 0f, 0f, 0.22f);

            DrawLine(from, to, glowColor, 11f, true);
            DrawLine(from, to, lineColor, 5f, true);
        }
    }

    private void DrawStageNodes(RouteTheme theme)
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

            var nodeColor = unlocked ? theme.UnlockedNode : theme.LockedNode;
            if (isSelected)
            {
                nodeColor = theme.SelectedNode;
            }

            if (completed)
            {
                DrawCircle(point, 44f, new Color(theme.RouteGlow, 0.16f));
            }

            if (isSelected)
            {
                DrawArc(point, 48f, 0f, Mathf.Tau, 36, new Color(theme.SelectedNode, 0.75f), 5f);
            }

            DrawCircle(point, 38f, new Color(0f, 0f, 0f, 0.34f));
            DrawCircle(point, 31f, nodeColor);
            DrawCircle(point, 22f, nodeColor.Lightened(0.12f));

            if (completed)
            {
                DrawRect(new Rect2(point + new Vector2(18f, -34f), new Vector2(10f, 10f)), theme.Accent, true);
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

    private RouteTheme ResolveTheme()
    {
        return NormalizeMapId(ActiveMapId) switch
        {
            "harbor" => new RouteTheme(
                new Color("173753"),
                new Color("0f2438"),
                new Color("5bc0eb"),
                new Color("9bdaf1"),
                new Color("3aaed8"),
                new Color("244a63"),
                new Color("ffd166"),
                new Color("80ed99")),
            _ => new RouteTheme(
                new Color("22304a"),
                new Color("111b2d"),
                new Color("6fffe9"),
                new Color("c9fff8"),
                new Color("5bc0be"),
                new Color("25314d"),
                new Color("ffd166"),
                new Color("ffb703"))
        };
    }

    private static string NormalizeMapId(string mapId)
    {
        return string.IsNullOrWhiteSpace(mapId) ? "city" : mapId.Trim().ToLowerInvariant();
    }
}
