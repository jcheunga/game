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
    public static Vector2 WorldSize => AdventureTerrain.WorldSize;
    private static readonly Vector2I[] LeaderCells = { new(7,17), new(15,20), new(9,9), new(22,14), new(19,4), new(29,3) };
    private static readonly Vector2I[] SupplyCells = { new(3,14), new(11,22), new(3,6), new(25,21), new(15,7), new(29,10) };
    private static readonly Vector2I[] LandmarkCells = { new(8,21), new(18,17), new(7,3), new(17,11), new(26,7), new(25,2) };
    private static readonly string[] Maps = { "city", "harbor", "foundry", "quarantine", "thornwall", "basilica", "mire", "steppe", "gloamwood", "citadel" };
    public static Vector2 LayoutPoint(string map, Vector2I grid)
    {
        var index = Math.Max(0, Array.IndexOf(Maps,map));
        if ((index & 1) != 0) grid.X = AdventureTerrain.Columns - 1 - grid.X;
        if ((index & 2) != 0) grid.Y = AdventureTerrain.Rows - 1 - grid.Y;
        if (index >= 4) { grid.X += index % 3 - 1; grid.Y += index / 3 % 3 - 1; }
        return AdventureTerrain.Point(AdventureTerrain.Index(grid.X,grid.Y));
    }
    public static int LegacyCell(AdventureMapNode node)
    {
        if (node.Kind == AdventureSiteKind.Camp) return 73;
        if (node.Id.StartsWith("hidden-")) return 89;
        var stages = GameData.GetStagesForMap(node.MapId).OrderBy(s => s.StageNumber).ToArray();
        var index = Array.FindIndex(stages,s => s.StageNumber == node.Stage);
        var cell = new[] { 74,38,17,54,45,10 }[Math.Max(0,index) % 6];
        return node.Id.StartsWith("supply-") ? cell + (index == 5 ? 12 : -12) : node.Id.StartsWith("landmark-") ? cell + 1 : cell;
    }
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
        nodes.Add(new($"camp-{mapId}", mapId, stages[0].StageNumber, AdventureSiteKind.Camp, LayoutPoint(mapId,new Vector2I(3,20)), "Lantern camp", "Your foothold in this district. Travel to a new tile costs 1 food; returning to a reached tile is free. Complete stages and gather supplies to open nearby tiles."));
        for (var i = 0; i < stages.Length; i++)
        {
            var stage = stages[i].StageNumber; var point = LayoutPoint(mapId,LeaderCells[i % LeaderCells.Length]);
            var faction = Array.IndexOf(Maps, mapId);
            nodes.Add(new($"leader-{stage}", mapId, stage, AdventureSiteKind.Leader, point, $"{Names[faction][i % 6]} {Titles[i % 6]}", LeaderDescriptions[i % 6], i % 6));
            var resource = i % 2 == 0 ? AdventureSiteKind.Gold : AdventureSiteKind.Food;
            nodes.Add(new($"supply-{stage}", mapId, stage, resource, LayoutPoint(mapId,SupplyCells[i % 6]), resource == AdventureSiteKind.Gold ? "Abandoned treasury" : "Supply wagon", "Supplies hidden off the main approaches. Gather this cache once; its contents belong to your caravan."));
            var bonus = i % 2 == 0 ? AdventureSiteKind.Watchtower : AdventureSiteKind.Shrine;
            nodes.Add(new($"landmark-{stage}", mapId, stage, bonus, LayoutPoint(mapId,LandmarkCells[i % 6]), bonus == AdventureSiteKind.Shrine ? "Shrine of resolve" : "Old watchtower", bonus == AdventureSiteKind.Shrine ? "Light the brazier. Your warband gains +3 starting courage in every campaign battle in this district." : "Climb the tower to reveal two rings of surrounding tiles. Your first tower also opens the search for a forgotten treasury."));
        }
        nodes.Add(new($"hidden-{mapId}", mapId, stages[0].StageNumber, AdventureSiteKind.Gold, LayoutPoint(mapId,new Vector2I(2,2)), "Forgotten treasury",
            "Your scouts heard of a forgotten cache beyond the watchtower. Explore to find it and gather its gold.", RequiredVisit: $"landmark-{stages[0].StageNumber}"));
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
