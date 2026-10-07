using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public enum AdventureDiscoveryKind { Food, Gold, Essence, Survey }
public sealed record AdventureDiscovery(string Id, string MapId, int Cell, AdventureDiscoveryKind Kind, int Amount)
{
    public Vector2 Point => AdventureTerrain.Point(Cell);
    public string Icon => Kind switch { AdventureDiscoveryKind.Food => "food", AdventureDiscoveryKind.Gold => "gold", AdventureDiscoveryKind.Essence => "flame", _ => "map" };
    public string Title => Kind switch { AdventureDiscoveryKind.Food => "Hidden provisions", AdventureDiscoveryKind.Gold => "Lost coin purse", AdventureDiscoveryKind.Essence => "Ancient essence", _ => "Surveyor's chart" };
    public string RewardText => Kind == AdventureDiscoveryKind.Survey ? "Nearby terrain revealed" : $"+{Amount} {Kind.ToString().ToLowerInvariant()}";
}

/// <summary>The resource finds placed on a zone's atlas tiles. IDs remain claim keys across reloads.</summary>
public static class AdventureDiscoveryCatalog
{
    private static readonly Dictionary<string,IReadOnlyList<AdventureDiscovery>> Cache = new(), LegacyCache = new();
    public static IReadOnlyList<AdventureDiscovery> ForMap(string map)
    {
        map = RouteCatalog.Normalize(map);
        if (Cache.TryGetValue(map,out var found)) return found;
        // Nearest the first stage first, as the finds were originally laid out.
        var order = Legacy(map).Select((find, i) => (find.Id, i)).ToDictionary(pair => pair.Id, pair => pair.i);
        return Cache[map] = AdventureTileCatalog.ForMap(map).Where(tile => tile.Discovery != null).Select(tile => tile.Discovery)
            .OrderBy(find => order[find.Id]).ToArray();
    }
    /// <summary>A find on one of the zone's few resource tiles, keeping the ID of the former find on that tile.</summary>
    public static AdventureDiscovery Placed(AdventureDiscovery former, AdventureDiscoveryKind kind)
    {
        var hash = AdventureTerrain.Hash(AdventureTerrain.Seed(former.MapId),former.Cell + 9000);
        var index = Math.Max(0,Array.IndexOf(AssetCoverageCatalog.RouteIds,former.MapId));
        var amount = kind switch { AdventureDiscoveryKind.Food => 6 + (int)(hash % 3),
            AdventureDiscoveryKind.Gold => 40 + index * 10 + (int)(hash % 21),
            AdventureDiscoveryKind.Essence => 2 + (int)(hash % 2), _ => 4 };
        return former with { Kind = kind, Amount = amount };
    }
    /// <summary>The forty finds that once covered every free tile. Their IDs still name those tiles in older saves.</summary>
    public static IReadOnlyList<AdventureDiscovery> Legacy(string map)
    {
        map = RouteCatalog.Normalize(map);
        if (LegacyCache.TryGetValue(map,out var found)) return found;
        var seed = AdventureTerrain.Seed(map); var sites = AdventureMapCatalog.ForMap(map).Select(n => AdventureTerrain.Cell(n.Point)).ToArray();
        var camp = sites[0]; var chosen = new List<int>();
        bool Available(int c) => AdventureTerrain.Walkable(map,c) && sites.All(s => AdventureTerrain.Distance(s,c) >= 3)
            && chosen.All(s => AdventureTerrain.Distance(s,c) >= 4);
        var early = AdventureTerrain.Area(camp,6).Where(c => AdventureTerrain.Distance(c,camp) >= 3 && Available(c))
            .OrderBy(c => AdventureTerrain.Distance(c,camp)).ThenBy(c => c).First();
        chosen.Add(early);
        foreach (var cell in Enumerable.Range(0,AdventureTerrain.CellCount).OrderBy(c => AdventureTerrain.Hash(seed,c)))
            if (chosen.Count < 40 && Available(cell)) chosen.Add(cell);
        var cycle = new[] { AdventureDiscoveryKind.Food, AdventureDiscoveryKind.Gold, AdventureDiscoveryKind.Food, AdventureDiscoveryKind.Gold,
            AdventureDiscoveryKind.Food, AdventureDiscoveryKind.Essence, AdventureDiscoveryKind.Gold, AdventureDiscoveryKind.Survey,
            AdventureDiscoveryKind.Food, AdventureDiscoveryKind.Gold };
        var index = Array.IndexOf(AssetCoverageCatalog.RouteIds,map);
        return LegacyCache[map] = chosen.Select((cell,i) => {
            var kind = cycle[i % cycle.Length]; var hash = AdventureTerrain.Hash(seed,cell + 9000);
            var amount = kind switch { AdventureDiscoveryKind.Food => i == 0 ? 6 : 4 + (int)(hash % 3),
                AdventureDiscoveryKind.Gold => 20 + Math.Max(0,index) * 5 + (int)(hash % 16),
                AdventureDiscoveryKind.Essence => 1 + (int)(hash % 2), _ => 4 };
            return new AdventureDiscovery($"discovery-{map}-{cell % AdventureTerrain.Columns}-{cell / AdventureTerrain.Columns}",map,cell,kind,amount);
        }).ToArray();
    }
    public static AdventureDiscovery At(string map, int cell) => ForMap(map).FirstOrDefault(d => d.Cell == cell);
}
