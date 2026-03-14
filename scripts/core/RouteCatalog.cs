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
    public const string ThornwallId = "thornwall";
    public const string BasilicaId = "basilica";
    public const string MireId = "mire";

    private static readonly Dictionary<string, RouteDefinition> Definitions = new(StringComparer.OrdinalIgnoreCase)
    {
        {
            CityId,
            new RouteDefinition(
                CityId,
                "King's Road",
                "Outer fields, market wards, and bell-tower approaches. Faster pacing, mixed undead, and earlier curse fire.",
                "King's Road favors faster ghouls, early blight casters, and more frequent surge timing.",
                "Fast ghouls, frequent curse fire, and tighter surge cadence over time.",
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
                "Saltwake Docks",
                "Tide-broken quays, chain cranes, and wreck piers. Heavier undead density and stronger late-battle crushes.",
                "Saltwake Docks favors heavier dead, rot hulks, and later grave-lord pressure around the choke point.",
                "Heavier bodies, rot hulks, and juggernaut spikes as the tide rises.",
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
                "Emberforge March",
                "Coal spurs, smelter rows, and furnace crowns. Bone nests, sappers, and juggernauts hit harder as the forge wakes.",
                "Emberforge March favors split-brood packs, sapper dives, and juggernaut escorts through furnace lanes.",
                "Sapper dives, split broods, and steadier heavy surges build as the forge heats up.",
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
                "Ashen Ward",
                "Purge cloisters, leech tents, and sealed vault checkpoints. Blight casters, heralds, and sappers stack behind warded lines.",
                "Ashen Ward favors ranged curse support, sapper breach timing, and hazard-heavy purge segments.",
                "Blight casters and heralds back repeated sapper dives while purge hazards punish overcommits.",
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
        },
        {
            ThornwallId,
            new RouteDefinition(
                ThornwallId,
                "Thornwall Pass",
                "Cliff roads, avalanche shrines, and watch forts. Fast raid packs and mountain hazards break the lane from above.",
                "Thornwall Pass favors fast raiders, sapper breaches, and avalanche pressure across narrow hold lines.",
                "Fast raid packs, cliffside hazards, and repeated breach dives collapse the line if left unchecked.",
                new Color("cfe8ff"),
                new Color("2f3e53"),
                new Color("4b5d73"),
                new Color("19232f"),
                new Color("9ad1ff"),
                new Color("e0f2ff"),
                new Color("7db4e6"),
                new Color("2f4356"),
                new Color("fefae0"),
                new Color("cfe8ff"))
        },
        {
            BasilicaId,
            new RouteDefinition(
                BasilicaId,
                "Hollow Basilica",
                "Ruined cathedrals, ossuary courts, and reliquary vaults. Relic guardians and curse liturgy turn each fight into a ritual choke point.",
                "Hollow Basilica favors split-brood screens, blight casters, and hex support around relic choke points.",
                "Caster-backed relic guards, brood screens, and curse support bog the caravan in set-piece holds.",
                new Color("e9c46a"),
                new Color("3b3a30"),
                new Color("5b5147"),
                new Color("231f1a"),
                new Color("f4d58d"),
                new Color("fff3c4"),
                new Color("d1b892"),
                new Color("473b30"),
                new Color("fefae0"),
                new Color("e9c46a"))
        },
        {
            MireId,
            new RouteDefinition(
                MireId,
                "Mire of Saints",
                "Bog causeways, drowned chapels, and plague ferries. Attrition hazards, rot mist, and shambling hull-grind packs wear the caravan down.",
                "Mire of Saints favors rot hulks, blight casters, and split-brood pressure through bogged lanes.",
                "Attrition hazards, plague mist, and heavier body piles drag the caravan into a slow kill.",
                new Color("90be6d"),
                new Color("2d3a26"),
                new Color("415a36"),
                new Color("162018"),
                new Color("b7e4a8"),
                new Color("ecf39e"),
                new Color("84a98c"),
                new Color("33452f"),
                new Color("fefae0"),
                new Color("90be6d"))
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
            Definitions[QuarantineId],
            Definitions[ThornwallId],
            Definitions[BasilicaId],
            Definitions[MireId]
        };
    }

    public static string Normalize(string routeId)
    {
        return string.IsNullOrWhiteSpace(routeId)
            ? CityId
            : routeId.Trim().ToLowerInvariant();
    }
}
