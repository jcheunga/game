using System;

// Authored points and hazard pockets for the longer campaign battlefield.
public sealed class StageBattlefieldDefinition
{
    public string OutpostTitle { get; set; } = "Forward beacon";
    public float OutpostXRatio { get; set; } = .4f;
    public float OutpostYRatio { get; set; } = .5f;
    public float CaptureSeconds { get; set; } = 3f;
    public int ForwardDeployments { get; set; } = 3;
    public float ForwardCooldown { get; set; } = 8f;
    public string SupplyTitle { get; set; } = "Caravan supplies";
    public float SupplyXRatio { get; set; } = .62f;
    public float SupplyYRatio { get; set; } = .25f;
    public string SupplyReward { get; set; } = "courage";
    public StageFieldPatchDefinition[] CursePatches { get; set; } = Array.Empty<StageFieldPatchDefinition>();

    public string RewardSummary => SupplyReward switch
    {
        "repair" => "Repair 12% of wagon hull and gain 8 courage",
        "siege" => "Deal 12% gate damage and interrupt enemy summons for 18s",
        _ => "Gain 25 courage and refresh unit cards by 3s"
    };
    public string Briefing => $"Capture {OutpostTitle} by holding its ring for {CaptureSeconds:0.#}s. " +
        $"It offers {ForwardDeployments} forward deployments, {ForwardCooldown:0}s apart; nearby enemies block it. " +
        $"Optional: secure {SupplyTitle}. {RewardSummary}. Advance through Approach, Crossroads, and Gate encounters; incoming packs are marked before they arrive.";
}

public sealed class StageFieldPatchDefinition
{
    public float XRatio { get; set; }
    public float YRatio { get; set; } = .5f;
    public float Width { get; set; } = 260f;
    public float Height { get; set; } = 104f;
}
