using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public enum AdventureSiteKind { Camp, Leader, Gold, Food, Shrine, Watchtower }

public sealed record AdventureMapNode(string Id, string MapId, int Stage, AdventureSiteKind Kind,
    Vector2 Point, string Title, string Description, int Portrait = 0, string RequiredVisit = "")
{
    public string Icon => Kind switch { AdventureSiteKind.Gold => "gold", AdventureSiteKind.Food => "food",
        AdventureSiteKind.Shrine => "bolt", AdventureSiteKind.Watchtower => "eye", AdventureSiteKind.Camp => "flag", _ => "sword" };
    public int GoldReward => Kind == AdventureSiteKind.Gold ? 35 + Stage * 3 : 0;
    public int FoodReward => Kind == AdventureSiteKind.Food ? 2 + Stage / 20 : 0;
}

/// <summary>Stable node IDs are save keys. Exploration layout is independent of battle balance.</summary>
public static class AdventureMapCatalog
{
    public static readonly Vector2 WorldSize = new(1536, 1024);
    private static readonly Vector2[] LeaderPoints = { new(330, 780), new(310, 490), new(655, 300), new(820, 560), new(1090, 435), new(1220, 185) };
    private static readonly string[][] Names = {
        new[] { "Rolf", "Isolde", "Aldric", "Mora", "Varr", "Osric" },
        new[] { "Brann", "Selene", "Garrick", "Neris", "Korr", "Mordain" },
        new[] { "Hagen", "Veyra", "Ulric", "Cinder", "Tarek", "Malrec" },
        new[] { "Drest", "Sybelle", "Torvald", "Vesper", "Rhaz", "Corvin" },
        new[] { "Bryn", "Freya", "Hadrik", "Eira", "Orun", "Skarn" },
        new[] { "Lucan", "Aveline", "Severin", "Mercy", "Damas", "Valerius" },
        new[] { "Gorse", "Mirelle", "Fenrick", "Helle", "Boros", "Morth" },
        new[] { "Arven", "Saran", "Baldric", "Orla", "Temur", "Kharon" },
        new[] { "Rook", "Elowen", "Thorne", "Nyra", "Kael", "Oberyn" },
        new[] { "Draven", "Ravena", "Godric", "Veyl", "Azar", "Ashen King" }
    };
    private static readonly string[] Titles = { "the Tollkeeper", "the Oathbreaker", "the Iron Marshal", "the Veiled Abbess", "the Warbound", "the Hollow Crown" };
    private static readonly string[] LeaderDescriptions = {
        "A brigand captain commands the dead at this crossing. Break his barricade to open the road.",
        "An oathless knight holds these lands with an undead retinue. Challenge her banner.",
        "A fallen marshal keeps a relentless watch over the approach. Defeat his garrison.",
        "The abbess rings her bells for the restless dead. Silence her stronghold.",
        "A warlord has bound the local dead to his command. Shatter his siege line.",
        "The district's ruler waits behind the final gate. Break the stronghold and reclaim the road."
    };
    private static readonly Dictionary<string, AdventureMapNode> ById = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, IReadOnlyList<AdventureMapNode>> Cache = new();
    public static IReadOnlyList<AdventureMapNode> ForMap(string mapId)
    {
        mapId = RouteCatalog.Normalize(mapId);
        if (Cache.TryGetValue(mapId, out var cached)) return cached;
        var stages = GameData.GetStagesForMap(mapId).OrderBy(x => x.StageNumber).ToArray();
        var nodes = new List<AdventureMapNode>();
        if (stages.Length == 0) return nodes;
        nodes.Add(new($"camp-{mapId}", mapId, stages[0].StageNumber, AdventureSiteKind.Camp, new(170, 880), "Lantern camp", "Your foothold in this district. Travel between discovered sites without spending food."));
        for (var i = 0; i < stages.Length; i++)
        {
            var stage = stages[i].StageNumber; var point = LeaderPoints[i % LeaderPoints.Length];
            var faction = Array.IndexOf(new[] { "city", "harbor", "foundry", "quarantine", "thornwall", "basilica", "mire", "steppe", "gloamwood", "citadel" }, mapId);
            nodes.Add(new($"leader-{stage}", mapId, stage, AdventureSiteKind.Leader, point, $"{Names[faction][i % 6]} {Titles[i % 6]}", LeaderDescriptions[i % 6], i % 6));
            var resource = i % 2 == 0 ? AdventureSiteKind.Gold : AdventureSiteKind.Food;
            nodes.Add(new($"supply-{stage}", mapId, stage, resource, point + new Vector2(-125, -105), resource == AdventureSiteKind.Gold ? "Abandoned treasury" : "Supply wagon", "Supplies left beside the road. Gather this cache once; its contents belong to your caravan."));
            var bonus = i % 2 == 0 ? AdventureSiteKind.Watchtower : AdventureSiteKind.Shrine;
            nodes.Add(new($"landmark-{stage}", mapId, stage, bonus, point + new Vector2(130, 85), bonus == AdventureSiteKind.Shrine ? "Shrine of resolve" : "Old watchtower", bonus == AdventureSiteKind.Shrine ? "Light the brazier. Your warband gains +3 starting courage in every campaign battle in this district." : "Climb the tower to lift the fog over a much wider area. Your first tower also reveals a hidden treasury."));
        }
        nodes.Add(new($"hidden-{mapId}", mapId, stages[0].StageNumber, AdventureSiteKind.Gold, new(620,840), "Forgotten treasury",
            "Your scouts spotted this hidden cache from the watchtower. Gather its gold for your caravan.", RequiredVisit: $"landmark-{stages[0].StageNumber}"));
        foreach (var node in nodes) ById[node.Id] = node;
        return Cache[mapId] = nodes;
    }
    public static AdventureMapNode Find(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        if (ById.TryGetValue(id, out var cached)) return cached;
        foreach (var map in GameData.Stages.Select(x => x.MapId).Distinct())
        { var node = ForMap(map).FirstOrDefault(x => x.Id == id); if (node != null) return node; }
        return null;
    }
    public static AdventureMapNode Leader(int stage) => Find($"leader-{stage}");
}
