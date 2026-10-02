using System;
using System.Linq;
using Godot;

/// <summary>Continuous ground-plane fog coverage derived from saved tile knowledge.</summary>
public static class AdventureFogMask
{
    public const int WorldUnitsPerPixel = 8;
    public const float Perspective = .56f;
    public static int Width => Mathf.CeilToInt(AdventureTerrain.WorldSize.X / WorldUnitsPerPixel);
    public static int Height => Mathf.CeilToInt(AdventureTerrain.WorldSize.Y / WorldUnitsPerPixel);

    public static byte[] Build(bool[] charted)
    {
        if (charted.Length != AdventureTerrain.CellCount) throw new ArgumentException("Fog knowledge must cover the zone.", nameof(charted));
        var coverage = new byte[Width * Height];
        if (charted.All(known => known))
        {
            Array.Fill(coverage, byte.MaxValue);
            return coverage;
        }

        // Recover the interiors of travelled/scouted patches. One camp or travel
        // step becomes an ellipse, rather than the squared silhouette of 13 tiles.
        for (var cell = 0; cell < charted.Length; cell++)
        {
            if (!charted[cell] || !AdventureTerrain.Area(cell, 2).All(nearby => charted[nearby])) continue;
            var radius = 226f + AdventureTerrain.Hash(8127, cell) % 19;
            Stamp(coverage, AdventureTerrain.Point(cell), radius);
        }

        // Retain individually known cells in old/partial saves and keep every
        // revealed landmark readable. These small lobes merge with the main bank.
        for (var cell = 0; cell < charted.Length; cell++)
        {
            if (!charted[cell]) continue;
            var point = AdventureTerrain.Point(cell);
            var x = Mathf.Clamp(Mathf.FloorToInt(point.X / WorldUnitsPerPixel), 0, Width - 1);
            var y = Mathf.Clamp(Mathf.FloorToInt(point.Y / WorldUnitsPerPixel), 0, Height - 1);
            if (coverage[y * Width + x] < 255) Stamp(coverage, point, 86);
        }
        return coverage;
    }

    private static void Stamp(byte[] coverage, Vector2 center, float radius)
    {
        var verticalRadius = radius * Perspective;
        var left = Math.Max(0, Mathf.FloorToInt((center.X - radius) / WorldUnitsPerPixel));
        var right = Math.Min(Width - 1, Mathf.CeilToInt((center.X + radius) / WorldUnitsPerPixel));
        var top = Math.Max(0, Mathf.FloorToInt((center.Y - verticalRadius) / WorldUnitsPerPixel));
        var bottom = Math.Min(Height - 1, Mathf.CeilToInt((center.Y + verticalRadius) / WorldUnitsPerPixel));
        for (var y = top; y <= bottom; y++)
        {
            var dy = ((y + .5f) * WorldUnitsPerPixel - center.Y) / verticalRadius;
            for (var x = left; x <= right; x++)
            {
                var dx = ((x + .5f) * WorldUnitsPerPixel - center.X) / radius;
                var distance = Mathf.Sqrt(dx * dx + dy * dy);
                var amount = (byte)Mathf.RoundToInt((1 - Mathf.SmoothStep(.80f, 1f, distance)) * 255);
                var index = y * Width + x;
                if (amount > coverage[index]) coverage[index] = amount;
            }
        }
    }
}
