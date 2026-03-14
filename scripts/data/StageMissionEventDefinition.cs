using Godot;

public sealed class StageMissionEventDefinition
{
    public string Type { get; set; } = "ritual_site";
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public string RewardSummary { get; set; } = "";
    public string PenaltySummary { get; set; } = "";
    public float XRatio { get; set; } = 0.5f;
    public float YRatio { get; set; } = 0.5f;
    public float Radius { get; set; } = 76f;
    public float TargetSeconds { get; set; } = 8f;
    public float StartTime { get; set; } = 20f;
    public string ColorHex { get; set; } = "ffd166";

    public string NormalizedType =>
        string.IsNullOrWhiteSpace(Type)
            ? "ritual_site"
            : Type.Trim().ToLowerInvariant();

    public Color GetTint()
    {
        if (string.IsNullOrWhiteSpace(ColorHex))
        {
            return new Color("ffd166");
        }

        return new Color(ColorHex);
    }
}
