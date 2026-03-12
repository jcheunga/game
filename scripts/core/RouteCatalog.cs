using System;
using System.Collections.Generic;
using Godot;

public sealed class RouteDefinition
{
    public RouteDefinition(
        string id,
        string title,
        string campaignSubtitle,
        string endlessSummary,
        string pressureSummary,
        Color bannerAccent,
        Color bannerPanel,
        Color backgroundTop,
        Color backgroundBottom,
        Color routeColor,
        Color routeGlow,
        Color unlockedNode,
        Color lockedNode,
        Color selectedNode,
        Color accent)
    {
        Id = id;
        Title = title;
        CampaignSubtitle = campaignSubtitle;
        EndlessSummary = endlessSummary;
        PressureSummary = pressureSummary;
        BannerAccent = bannerAccent;
        BannerPanel = bannerPanel;
        BackgroundTop = backgroundTop;
        BackgroundBottom = backgroundBottom;
        RouteColor = routeColor;
        RouteGlow = routeGlow;
        UnlockedNode = unlockedNode;
        LockedNode = lockedNode;
        SelectedNode = selectedNode;
        Accent = accent;
    }

    public string Id { get; }
    public string Title { get; }
    public string CampaignSubtitle { get; }
    public string EndlessSummary { get; }
    public string PressureSummary { get; }
    public Color BannerAccent { get; }
    public Color BannerPanel { get; }
    public Color BackgroundTop { get; }
    public Color BackgroundBottom { get; }
    public Color RouteColor { get; }
    public Color RouteGlow { get; }
    public Color UnlockedNode { get; }
    public Color LockedNode { get; }
    public Color SelectedNode { get; }
    public Color Accent { get; }
}

public static class RouteCatalog
{
    public const string CityId = "city";
    public const string HarborId = "harbor";
    public const string FoundryId = "foundry";
    public const string QuarantineId = "quarantine";

    private static readonly Dictionary<string, RouteDefinition> Definitions = new(StringComparer.OrdinalIgnoreCase)
    {
        {
            CityId,
            new RouteDefinition(
                CityId,
                "City Route",
                "Suburban highways and metro choke points. Faster pacing, mixed infected, and earlier ranged pressure.",
                "City Route favors faster infected, early spitters, and more frequent surge timing.",
                "Faster runners, frequent spitters, and tighter surge cadence over time.",
                new Color("ffd166"),
                new Color("243b53"),
                new Color("22304a"),
                new Color("111b2d"),
                new Color("6fffe9"),
                new Color("c9fff8"),
                new Color("5bc0be"),
                new Color("25314d"),
                new Color("ffd166"),
                new Color("ffb703"))
        },
        {
            HarborId,
            new RouteDefinition(
                HarborId,
                "Harbor Front",
                "Flooded terminals, cranes, and shipbreak lanes. Heavier zombie density and late-battle pressure.",
                "Harbor Front favors heavier infected, bloaters, and later boss pressure around the choke point.",
                "Heavier bodies, bloaters, and crusher spikes as the clock climbs.",
                new Color("80ed99"),
                new Color("1d3557"),
                new Color("173753"),
                new Color("0f2438"),
                new Color("5bc0eb"),
                new Color("9bdaf1"),
                new Color("3aaed8"),
                new Color("244a63"),
                new Color("ffd166"),
                new Color("80ed99"))
        },
        {
            FoundryId,
            new RouteDefinition(
                FoundryId,
                "Foundry Line",
                "Freight cuts, smelter rows, and furnace crowns. Splitter packs, saboteurs, and crushers hit harder as the district heats up.",
                "Foundry Line favors splitter packs, saboteur dives, and crusher escorts through furnace lanes.",
                "Saboteur dives, splitter nests, and steadier heavy surges build as the furnace route heats up.",
                new Color("ff9f1c"),
                new Color("4b2e1f"),
                new Color("402218"),
                new Color("1f1512"),
                new Color("ff7b00"),
                new Color("ffd6a5"),
                new Color("d97706"),
                new Color("493628"),
                new Color("ffe66d"),
                new Color("f4a261"))
        },
        {
            QuarantineId,
            new RouteDefinition(
                QuarantineId,
                "Quarantine Wall",
                "Decon corridors, triage tents, and blacksite checkpoints. Spitters, howlers, and saboteurs stack behind sealed containment lines.",
                "Quarantine Wall favors ranged infected support, saboteur breach timing, and hazard-heavy toxic checkpoint segments.",
                "Spitters and howlers back repeated saboteur dives while toxic purge hazards punish overcommits.",
                new Color("d9ed92"),
                new Color("1b4332"),
                new Color("204e4a"),
                new Color("102a29"),
                new Color("95d5b2"),
                new Color("d8f3dc"),
                new Color("52b788"),
                new Color("29443d"),
                new Color("fef08a"),
                new Color("d9ed92"))
        }
    };

    public static RouteDefinition Get(string routeId)
    {
        var normalizedId = Normalize(routeId);
        return Definitions.TryGetValue(normalizedId, out var route)
            ? route
            : Definitions[CityId];
    }

    public static IReadOnlyList<RouteDefinition> GetAll()
    {
        return new[]
        {
            Definitions[CityId],
            Definitions[HarborId],
            Definitions[FoundryId],
            Definitions[QuarantineId]
        };
    }

    public static string Normalize(string routeId)
    {
        return string.IsNullOrWhiteSpace(routeId)
            ? CityId
            : routeId.Trim().ToLowerInvariant();
    }
}
