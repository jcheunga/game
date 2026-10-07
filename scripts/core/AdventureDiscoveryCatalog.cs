using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public enum AdventureDiscoveryKind { Food, Gold, Essence, Survey }
/// <summary>A find on one of a zone's atlas tiles, gathered when that tile is opened.</summary>
public sealed record AdventureDiscovery(string Id, string MapId, int Column, int Row, AdventureDiscoveryKind Kind, int Amount)
{
    public Vector2 Point => AdventureAtlasLandscape.SitePoint(MapId, Column, Row);
    public string Icon => Kind switch { AdventureDiscoveryKind.Food => "food", AdventureDiscoveryKind.Gold => "gold", AdventureDiscoveryKind.Essence => "flame", _ => "map" };
    public string Title => Kind switch { AdventureDiscoveryKind.Food => "Hidden provisions", AdventureDiscoveryKind.Gold => "Lost coin purse", AdventureDiscoveryKind.Essence => "Ancient essence", _ => "Surveyor's chart" };
    public string RewardText => Kind == AdventureDiscoveryKind.Survey ? "Surrounding land charted" : $"+{Amount} {Kind.ToString().ToLowerInvariant()}";
}

/// <summary>The finds scattered over each zone's atlas (placed by AdventureTileCatalog). IDs are claim keys.</summary>
public static class AdventureDiscoveryCatalog
{
    private static readonly Dictionary<string, IReadOnlyList<AdventureDiscovery>> Cache = new();
    /// <summary>The zone's finds in the order they were placed: the first lies close to the first stage.</summary>
    public static IReadOnlyList<AdventureDiscovery> ForMap(string map)
    {
        map = RouteCatalog.Normalize(map);
        if (Cache.TryGetValue(map, out var found)) return found;
        return Cache[map] = AdventureTileCatalog.ForMap(map).Where(tile => tile.Discovery != null)
            .Select(tile => tile.Discovery).OrderBy(AdventureTileCatalog.FindOrder).ToArray();
    }
    public static AdventureDiscovery Create(string map, int column, int row, AdventureDiscoveryKind kind)
    {
        var hash = AdventureTerrain.Hash(AdventureTerrain.Seed(map), row * AdventureTileCatalog.Columns + column + 9000);
        var index = Math.Max(0, Array.IndexOf(AssetCoverageCatalog.RouteIds, map));
        var amount = kind switch { AdventureDiscoveryKind.Food => 6 + (int)(hash % 3),
            AdventureDiscoveryKind.Gold => 40 + index * 10 + (int)(hash % 21),
            AdventureDiscoveryKind.Essence => 2 + (int)(hash % 2), _ => 1 };
        return new($"find-{map}-{column}-{row}", map, column, row, kind, amount);
    }
}
