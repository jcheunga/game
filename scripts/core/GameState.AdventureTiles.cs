using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Exploration of the zone atlases. A tile is opened (charted ground), on the frontier (touching opened
/// ground: seen through thin mist and ready to open) or hidden under the storm cloud. Opening a frontier tile
/// costs food and gathers whatever lies on it; a frontier stage is challenged instead, and its victory opens it.
/// </summary>
public partial class GameState
{
    /// <summary>Food spent to open one frontier tile.</summary>
    public const int AdventureTileFoodCost = 2;
    private readonly HashSet<string> _openedAdventureTiles = new(StringComparer.Ordinal);
    private readonly HashSet<string> _reachedAdventureTiles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _adventureCaravanTiles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _revealedAdventureTiles = new(StringComparer.Ordinal);
    private int _revealedRevision = -1;
    public AdventureTile GetAdventureCaravanTile(string mapId) => AdventureTileCatalog.Find(mapId, _adventureCaravanTiles.GetValueOrDefault(RouteCatalog.Normalize(mapId), ""))
        ?? AdventureTileCatalog.Starting(mapId);
    /// <summary>Charted ground. Each zone's first stage starts opened.</summary>
    public bool IsAdventureTileOpened(AdventureTile tile) => tile != null &&
        (_openedAdventureTiles.Contains(tile.Id) || AdventureTileCatalog.Starting(tile.MapId).Id == tile.Id);
    /// <summary>Opened, or on the frontier beside opened ground: visible and selectable.</summary>
    public bool IsAdventureTileRevealed(AdventureTile tile) => tile != null && RevealedAdventureTiles(tile.MapId).Contains(tile.Id);
    public bool IsAdventureTileFrontier(AdventureTile tile) => IsAdventureTileRevealed(tile) && !IsAdventureTileOpened(tile);
    private HashSet<string> RevealedAdventureTiles(string mapId)
    {
        if (_revealedRevision != AdventureKnowledgeRevision) { _revealedAdventureTiles.Clear(); _revealedRevision = AdventureKnowledgeRevision; }
        mapId = RouteCatalog.Normalize(mapId);
        if (_revealedAdventureTiles.TryGetValue(mapId, out var revealed)) return revealed;
        revealed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tile in AdventureTileCatalog.ForMap(mapId).Where(IsAdventureTileOpened))
        {
            revealed.Add(tile.Id);
            foreach (var neighbor in AdventureTileCatalog.Neighbors(tile)) revealed.Add(neighbor.Id);
        }
        return _revealedAdventureTiles[mapId] = revealed;
    }
    public bool HasReachedAdventureTile(string id) => _reachedAdventureTiles.Contains(id)
        || AdventureMapCatalog.Find(id) is { Kind: AdventureSiteKind.Leader } site && AdventureTileCatalog.Starting(site.MapId).Id == id;
    public static bool IsAdventureResourceTile(AdventureTile tile) => tile?.IsResource == true;
    /// <summary>A stage is complete once won; a resource once gathered; plain ground once opened.</summary>
    public bool IsAdventureTileComplete(AdventureTile tile) => tile?.Site is { } site
        ? site.Kind == AdventureSiteKind.Leader ? GetStageStars(site.Stage) > 0 : HasVisitedAdventureSite(site.Id)
        : tile?.Discovery is { } reward ? HasClaimedAdventureDiscovery(reward.Id) : IsAdventureTileOpened(tile);
    public bool CanTravelToAdventureTile(AdventureTile tile, out string message)
    {
        if (tile == null || !IsAdventureZoneUnlocked(tile.MapId) || !IsAdventureTileRevealed(tile))
        { message = "Open the land beside this tile first."; return false; }
        if (tile.Site is { Kind: AdventureSiteKind.Leader } site)
        {
            if (!CanVisitAdventureSite(site.Id))
            { message = $"Defeat {GetAdventureBossRemainingLeaders(site.Stage)} more leaders to open the boss gate."; return false; }
            message = ""; return true;
        }
        if (IsAdventureTileOpened(tile))
        { message = tile.IsResource ? "These supplies have already been collected." : "This land is already charted."; return false; }
        if (Food < AdventureTileFoodCost)
        { message = $"Opening this tile costs {AdventureTileFoodCost} food."; return false; }
        message = ""; return true;
    }
    /// <summary>Moves the caravan onto a revealed tile; nothing is charged until the tile is opened.</summary>
    public bool TryReachAdventureTile(AdventureTile tile, out string message)
    {
        if (!CanTravelToAdventureTile(tile, out message)) return false;
        _reachedAdventureTiles.Add(tile.Id);
        _adventureCaravanTiles[tile.MapId] = tile.Id;
        if (tile.Site != null)
        {
            _adventureHeroNodes[tile.MapId] = tile.Site.Id;
            _adventureHeroPositions[tile.MapId] = tile.Site.Point;
        }
        Persist();
        return true;
    }
    /// <summary>Acts on the tile the caravan stands on: a stage is entered, anything else is opened.</summary>
    public bool TryCollectAdventureTile(AdventureTile tile, out string message)
    {
        if (tile == null || !HasReachedAdventureTile(tile.Id) || GetAdventureCaravanTile(tile.MapId).Id != tile.Id)
        { message = "Travel to this tile first."; return false; }
        if (tile.Site?.Kind == AdventureSiteKind.Leader) return TryVisitAdventureSite(tile.Site.Id, out message);
        return TryOpenAdventureTile(tile, out message);
    }
    /// <summary>Spends food to open a frontier tile, gathering its cache or find; its neighbours join the frontier.</summary>
    public bool TryOpenAdventureTile(AdventureTile tile, out string message)
    {
        if (tile?.Site?.Kind == AdventureSiteKind.Leader) { message = "Win this stage's battle to chart its tile."; return false; }
        if (!CanTravelToAdventureTile(tile, out message)) return false;
        Food -= AdventureTileFoodCost;
        OpenAdventureTile(tile);
        _reachedAdventureTiles.Add(tile.Id);
        _adventureCaravanTiles[tile.MapId] = tile.Id;
        message = "Land charted";
        if (tile.Site is { } site)
        {
            _visitedAdventureSites.Add(site.Id);
            Gold += site.GoldReward; Food += site.FoodReward;
            message = site.Kind == AdventureSiteKind.Food ? $"Supplies secured · +{site.FoodReward} food" : $"Treasury secured · +{site.GoldReward} gold";
        }
        else if (tile.Discovery is { } find)
        {
            _claimedAdventureDiscoveries.Add(find.Id);
            switch (find.Kind)
            {
                case AdventureDiscoveryKind.Food: Food += find.Amount; break;
                case AdventureDiscoveryKind.Gold: Gold += find.Amount; break;
                case AdventureDiscoveryKind.Essence: Essence += find.Amount; break;
                // A surveyor's chart charts the plain ground around it for free.
                case AdventureDiscoveryKind.Survey:
                    foreach (var neighbor in AdventureTileCatalog.Neighbors(tile).Where(neighbor => !neighbor.HasInterest)) OpenAdventureTile(neighbor);
                    break;
            }
            message = find.RewardText;
        }
        LastResultMessage = message;
        Persist();
        FoodChanged?.Invoke();
        if (tile.Discovery != null) AdventureDiscoveryFound?.Invoke(tile.Discovery);
        return true;
    }
    private void OpenAdventureTile(AdventureTile tile)
    {
        if (_openedAdventureTiles.Add(tile.Id)) AdventureKnowledgeRevision++;
    }
    /// <summary>A won stage is charted, so the land around it joins the frontier.</summary>
    private void RevealAdventureStageVictory(int stage)
    {
        var site = AdventureMapCatalog.Leader(stage);
        if (site == null) return;
        var tile = AdventureTileCatalog.Find(site.MapId, site.Id);
        _reachedAdventureTiles.Add(tile.Id);
        OpenAdventureTile(tile);
    }
    private void LoadAdventureTiles(GameSaveData saved)
    {
        _openedAdventureTiles.Clear(); _reachedAdventureTiles.Clear(); _adventureCaravanTiles.Clear();
        foreach (var map in GameData.Stages.Select(stage => stage.MapId).Distinct())
        {
            var tiles = AdventureTileCatalog.ForMap(map);
            if (saved.Version >= 47)
            {
                foreach (var id in saved.AdventureOpenTiles ?? Array.Empty<string>())
                    if (AdventureTileCatalog.Find(map, id) is { } tile) _openedAdventureTiles.Add(tile.Id);
                foreach (var id in saved.AdventureReachedTiles ?? Array.Empty<string>())
                    if (AdventureTileCatalog.Find(map, id) is { } tile) _reachedAdventureTiles.Add(tile.Id);
            }
            else ChartFormerAdventureProgress(map);
            // Won stages and gathered caches and finds are always charted.
            foreach (var tile in tiles.Where(tile => tile.Site is { Kind: AdventureSiteKind.Leader } site && GetStageStars(site.Stage) > 0
                || tile.IsResource && IsAdventureTileComplete(tile)))
            {
                _openedAdventureTiles.Add(tile.Id); _reachedAdventureTiles.Add(tile.Id);
            }
            if (saved.Version >= 47 && AdventureTileCatalog.Find(map, saved.AdventureCaravanTiles?.GetValueOrDefault(map)) is { } caravan
                && (_openedAdventureTiles.Contains(caravan.Id) || caravan.Id == AdventureTileCatalog.Starting(map).Id))
                _adventureCaravanTiles[map] = caravan.Id;
        }
        AdventureKnowledgeRevision++;
    }
    /// <summary>
    /// Saves from the smaller atlas only keep stars and gathered caches. Chart the roads between the stages they
    /// won (from the first stage), and the whole zone apart from its resources once its boss has fallen.
    /// </summary>
    private void ChartFormerAdventureProgress(string map)
    {
        var tiles = AdventureTileCatalog.ForMap(map);
        var stages = GameData.GetStagesForMap(map).OrderBy(stage => stage.StageNumber).ToArray();
        bool Won(int index) => index == 0 || GetStageStars(stages[index].StageNumber) > 0;
        if (GetStageStars(stages[^1].StageNumber) > 0)
        {
            foreach (var tile in tiles.Where(tile => !tile.IsResource)) _openedAdventureTiles.Add(tile.Id);
            return;
        }
        foreach (var (from, to) in AdventureTileCatalog.Roads.Where(road => road.To < stages.Length && Won(road.From) && Won(road.To)))
            foreach (var tile in AdventureTilePath(AdventureTileCatalog.Stage(map, from), AdventureTileCatalog.Stage(map, to)))
                _openedAdventureTiles.Add(tile.Id);
    }
    /// <summary>The fewest tiles from one tile to another, stepping only through plain ground.</summary>
    public static IReadOnlyList<AdventureTile> AdventureTilePath(AdventureTile from, AdventureTile to)
    {
        var previous = new Dictionary<string, AdventureTile> { [from.Id] = null };
        var queue = new Queue<AdventureTile>(); queue.Enqueue(from);
        while (queue.Count > 0)
        {
            var tile = queue.Dequeue();
            if (tile.Id == to.Id) break;
            foreach (var next in AdventureTileCatalog.Neighbors(tile))
                if (!previous.ContainsKey(next.Id) && (next.Id == to.Id || !next.HasInterest)) { previous[next.Id] = tile; queue.Enqueue(next); }
        }
        if (!previous.ContainsKey(to.Id)) return Array.Empty<AdventureTile>();
        var path = new List<AdventureTile>();
        for (var tile = to; tile != null; tile = previous[tile.Id]) path.Add(tile);
        path.Reverse();
        return path;
    }
}
