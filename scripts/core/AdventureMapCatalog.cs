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
    public int GoldReward => Kind == AdventureSiteKind.Gold ? 35 + Stage * 9 / 5 : 0;
    // Supply wagons pay well above the food spent opening their tile.
    public int FoodReward => Kind == AdventureSiteKind.Food ? 5 + Stage / 25 : 0;
}

/// <summary>Stable node IDs are save keys. Exploration layout is independent of battle balance.</summary>
public static class AdventureMapCatalog
{
    public static Vector2 WorldSize => AdventureTerrain.WorldSize;
    private static readonly Vector2I[] LeaderCells = { new(7,17), new(12,20), new(9,12), new(5,6), new(11,3), new(17,7), new(15,14), new(21,19), new(25,12), new(29,3) };
    private static readonly Vector2I[] SupplyCells = { new(11,15), new(7,22), new(3,12), new(7,1), new(14,6), new(16,10), new(13,17), new(25,22), new(30,14), new(24,2) };
    private static readonly Vector2I[] LandmarkCells = { new(1,17), new(17,22), new(10,8), new(1,8), new(14,2), new(19,3), new(19,15), new(25,16), new(22,9), new(28,7) };
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
    // Ten rivals per zone; the last is the zone's ruler behind the boss gate.
    private static readonly string[][] Names = {
        new[] { "Rolf", "Isolde", "Wendel", "Aldric", "Harrow", "Mora", "Brannoc", "Varr", "Sable", "Osric" },
        new[] { "Brann", "Selene", "Orrin", "Garrick", "Teague", "Neris", "Marrow", "Korr", "Fennick", "Mordain" },
        new[] { "Hagen", "Veyra", "Dorran", "Ulric", "Kessa", "Cinder", "Brakk", "Tarek", "Soren", "Malrec" },
        new[] { "Drest", "Sybelle", "Lisle", "Torvald", "Pell", "Vesper", "Ambrose", "Rhaz", "Ottilie", "Corvin" },
        new[] { "Bryn", "Freya", "Halvar", "Hadrik", "Sigrun", "Eira", "Ulfar", "Orun", "Ketil", "Skarn" },
        new[] { "Lucan", "Aveline", "Anselm", "Severin", "Cassius", "Mercy", "Prosper", "Damas", "Benedikt", "Valerius" },
        new[] { "Gorse", "Mirelle", "Silt", "Fenrick", "Wynn", "Helle", "Moss", "Boros", "Garrow", "Morth" },
        new[] { "Arven", "Saran", "Batu", "Baldric", "Yesui", "Orla", "Khasar", "Temur", "Jochi", "Kharon" },
        new[] { "Rook", "Elowen", "Ash", "Thorne", "Bramble", "Nyra", "Corwin", "Kael", "Sloe", "Oberyn" },
        new[] { "Draven", "Ravena", "Aurel", "Godric", "Castor", "Veyl", "Vidar", "Azar", "Sabine", "Ashen King" }
    };
    private static readonly string[] Titles = { "the Tollkeeper", "the Oathbreaker", "the Grave Warden", "the Iron Marshal", "the Bone Herald",
        "the Veiled Abbess", "the Ash Reaver", "the Warbound", "the Black Seneschal", "the Hollow Crown" };
    private static readonly string[] LeaderDescriptions = {
        "A brigand captain commands the dead at this crossing. Break his barricade to open the road.",
        "An oathless knight holds these lands with an undead retinue. Challenge her banner.",
        "A grave warden marches the restless dead along the old road. Scatter the procession.",
        "A fallen marshal keeps a relentless watch over the approach. Defeat his garrison.",
        "A herald in bone mail rallies the dead with a war horn. Silence the horn before the host gathers.",
        "The abbess rings her bells for the restless dead. Silence her stronghold.",
        "A reaver burns the farmsteads ahead of the host. Run down the raiders.",
        "A warlord has bound the local dead to his command. Shatter his siege line.",
        "The ruler's steward musters the last garrison before the gate. Break the muster.",
        "The district's ruler waits behind the final gate. Break the stronghold and reclaim the road."
    };
    // Six portraits: rivals cycle the first five, the ruler always wears the sixth.
    private static int Portrait(int index, int count) => index == count - 1 ? 5 : index % 5;
    private static readonly Dictionary<string, AdventureMapNode> ById = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, IReadOnlyList<AdventureMapNode>> Cache = new();
    public static IReadOnlyList<AdventureMapNode> ForMap(string mapId)
    {
        mapId = RouteCatalog.Normalize(mapId);
        if (Cache.TryGetValue(mapId, out var cached)) return cached;
        var stages = GameData.GetStagesForMap(mapId).OrderBy(x => x.StageNumber).ToArray();
        var nodes = new List<AdventureMapNode>();
        if (stages.Length == 0) return nodes;
        // Retain legacy camp coordinates for saved exploration and deterministic discovery IDs.
        // The playable atlas uses plain terrain at its former location.
        nodes.Add(new($"camp-{mapId}", mapId, stages[0].StageNumber, AdventureSiteKind.Camp, LayoutPoint(mapId,new Vector2I(3,20)), "Lantern camp", "Retired camp retained for older saves."));
        for (var i = 0; i < stages.Length; i++)
        {
            var stage = stages[i].StageNumber; var point = LayoutPoint(mapId,LeaderCells[i % LeaderCells.Length]);
            var faction = Array.IndexOf(Maps, mapId);
            // The ruler always takes the last name and title, whatever the zone's length.
            var rival = i == stages.Length - 1 ? Titles.Length - 1 : i % (Titles.Length - 1);
            nodes.Add(new($"leader-{stage}", mapId, stage, AdventureSiteKind.Leader, point, $"{Names[faction][rival]} {Titles[rival]}", LeaderDescriptions[rival], Portrait(i, stages.Length)));
            var resource = i % 2 == 0 ? AdventureSiteKind.Gold : AdventureSiteKind.Food;
            nodes.Add(new($"supply-{stage}", mapId, stage, resource, LayoutPoint(mapId,SupplyCells[i % SupplyCells.Length]), resource == AdventureSiteKind.Gold ? "Abandoned treasury" : "Supply wagon", "Supplies hidden off the main approaches. Gather this cache once; its contents belong to your caravan."));
            var bonus = i % 2 == 0 ? AdventureSiteKind.Watchtower : AdventureSiteKind.Shrine;
            // Retired towers and shrines retain legacy coordinates/IDs for saves and discovery placement.
            nodes.Add(new($"landmark-{stage}", mapId, stage, bonus, LayoutPoint(mapId,LandmarkCells[i % LandmarkCells.Length]), bonus == AdventureSiteKind.Shrine ? "Shrine of resolve" : "Old watchtower", "Retired landmark retained for older saves."));
        }
        nodes.Add(new($"hidden-{mapId}", mapId, stages[0].StageNumber, AdventureSiteKind.Gold, LayoutPoint(mapId,new Vector2I(2,2)), "Forgotten treasury",
            "A forgotten cache lies beyond the nearby roads. Open its tile and gather its gold."));
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
