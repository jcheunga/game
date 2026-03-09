using System;
using Godot;

public sealed class StageHazardDefinition
{
    public string Type { get; set; } = "hazard";
    public string Label { get; set; } = "";
    public float XRatio { get; set; } = 0.5f;
    public float YRatio { get; set; } = 0.5f;
    public float Radius { get; set; } = 64f;
    public float Interval { get; set; } = 8f;
    public float StartTime { get; set; } = 8f;
    public float WarningDuration { get; set; } = 1.6f;
    public float Damage { get; set; } = 14f;
    public string ColorHex { get; set; } = "ff7b00";

    public Color GetTint()
    {
        if (string.IsNullOrWhiteSpace(ColorHex))
        {
            return new Color("ff7b00");
        }

        return new Color(ColorHex);
    }
}
