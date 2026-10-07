using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class GameState
{
    private readonly HashSet<string> _visitedAdventureSites = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _adventureHeroNodes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Vector2> _adventureHeroPositions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<int>> _adventureExploredCells = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<int>> _adventureTravelledCells = new(StringComparer.Ordinal);
    private readonly HashSet<string> _claimedAdventureDiscoveries = new(StringComparer.Ordinal);
    public int AdventureKnowledgeRevision { get; private set; }
    public event Action<AdventureDiscovery> AdventureDiscoveryFound;
    public bool HasClaimedAdventureDiscovery(string id) => _claimedAdventureDiscoveries.Contains(id);
    public bool HasVisitedAdventureSite(string id) => _visitedAdventureSites.Contains(id);
    public bool IsAdventureZoneUnlocked(string mapId)
    {
        var maps = GameData.Stages.Select(stage => stage.MapId).Distinct().ToArray();
        var index = Array.IndexOf(maps, mapId);
        if (index < 0) return false;
        if (index == 0 || GetStageStars(GameData.GetStagesForMap(maps[index - 1]).Max(stage => stage.StageNumber)) > 0)
            return true;
        // Keep previously played districts reachable when loading an existing campaign.
        // Stage numbers span districts, so HighestUnlockedStage is not a zone gate.
        return maps.Skip(index).Any(map => GameData.GetStagesForMap(map).Any(stage => GetStageStars(stage.StageNumber) > 0)
            || AdventureMapCatalog.ForMap(map).Any(site => site.Kind != AdventureSiteKind.Camp && HasVisitedAdventureSite(site.Id))
            || AdventureTileCatalog.ForMap(map).Any(tile => tile.Site?.Kind != AdventureSiteKind.Camp
                && tile.RetiredSiteId != $"camp-{map}" && _reachedAdventureTiles.Contains(tile.Id))
            || _adventureTravelledCells.TryGetValue(map, out var cells)
                && cells.Any(cell => cell != AdventureTerrain.Cell(AdventureMapCatalog.ForMap(map).First().Point)));
    }
    public bool IsAdventureBoss(int stage) => stage >= 1 && stage <= MaxStage &&
        GameData.GetStagesForMap(GameData.GetStage(stage).MapId).Max(x => x.StageNumber) == stage;
    public int GetAdventureBossRemainingLeaders(int stage) => GameData.GetStagesForMap(GameData.GetStage(stage).MapId)
        .Count(x => x.StageNumber != stage && GetStageStars(x.StageNumber) <= 0);
    public bool IsCampaignStageUnlocked(int stage) => stage >= 1 && stage <= MaxStage &&
        (!IsAdventureBoss(stage) || GetStageStars(stage) > 0 || GetAdventureBossRemainingLeaders(stage) == 0);
    public bool CanVisitAdventureSite(string id) => AdventureMapCatalog.Find(id) is { } node &&
        node.Kind is not (AdventureSiteKind.Watchtower or AdventureSiteKind.Camp or AdventureSiteKind.Shrine) &&
        (node.Kind != AdventureSiteKind.Leader || IsCampaignStageUnlocked(node.Stage)) &&
        (string.IsNullOrEmpty(node.RequiredVisit) || HasVisitedAdventureSite(node.RequiredVisit));
    public bool IsAdventureSiteDiscovered(string id) => AdventureMapCatalog.Find(id) is { } node &&
        (string.IsNullOrEmpty(node.RequiredVisit) || HasVisitedAdventureSite(node.RequiredVisit)) &&
        IsAdventureTileOpen(AdventureTileCatalog.Find(node.MapId, node.Id));
    public bool IsAdventureCellRevealed(string mapId, int cell)
    {
        if (cell < 0 || cell >= AdventureTerrain.CellCount) return false;
        mapId = RouteCatalog.Normalize(mapId);
        var camp = AdventureTerrain.Cell(AdventureMapCatalog.ForMap(mapId).First().Point);
        return AdventureTerrain.Distance(cell,camp) <= 2 ||
            (_adventureExploredCells.TryGetValue(mapId, out var cells) && cells.Contains(cell));
    }
    public bool IsAdventureCellTravelled(string mapId, int cell)
    {
        mapId = RouteCatalog.Normalize(mapId);
        return cell == AdventureTerrain.Cell(AdventureMapCatalog.ForMap(mapId).First().Point) ||
            (_adventureTravelledCells.TryGetValue(mapId,out var cells) && cells.Contains(cell));
    }
    public bool TryBeginAdventureTravel(string mapId, IReadOnlyList<int> path, out string message)
    {
        mapId = RouteCatalog.Normalize(mapId);
        if (path == null || path.Count == 0 || path[0] != AdventureTerrain.Cell(GetAdventureHeroPosition(mapId)) ||
            path.Any(cell => !AdventureTerrain.Walkable(mapId, cell)) ||
            path.Zip(path.Skip(1)).Any(pair => !AdventureTerrain.Neighbors(pair.First).Contains(pair.Second)))
        { message = "Choose a connected ground tile."; return false; }
        message = ""; return true;
    }
    // Legacy movement records explored routes without spending food.
    public bool TryPayAdventureStep(string mapId, int cell, out string message)
    {
        mapId = RouteCatalog.Normalize(mapId);
        var from = AdventureTerrain.Cell(GetAdventureHeroPosition(mapId));
        if (!AdventureTerrain.Walkable(mapId,cell) || (cell != from && !AdventureTerrain.Neighbors(from).Contains(cell)))
        { message = "Choose a connected ground tile."; return false; }
        if (IsAdventureCellTravelled(mapId,cell)) { message = ""; return true; }
        if (!_adventureTravelledCells.TryGetValue(mapId,out var cells)) _adventureTravelledCells[mapId] = cells = new();
        cells.Add(cell); Persist(); message = ""; return true;
    }
    public bool CompleteAdventureStep(string mapId, int cell)
    {
        mapId = RouteCatalog.Normalize(mapId);
        if (!IsAdventureCellTravelled(mapId,cell) || !MoveAdventureHero(mapId,AdventureTerrain.Point(cell),false)) return false;
        var discovery = AdventureDiscoveryCatalog.At(mapId,cell);
        if (discovery != null && _claimedAdventureDiscoveries.Add(discovery.Id))
        {
            switch (discovery.Kind)
            {
                case AdventureDiscoveryKind.Food: Food += discovery.Amount; FoodChanged?.Invoke(); break;
                case AdventureDiscoveryKind.Gold: Gold += discovery.Amount; break;
                case AdventureDiscoveryKind.Essence: Essence += discovery.Amount; break;
                case AdventureDiscoveryKind.Survey: RevealAdventurePoint(mapId,discovery.Point,discovery.Amount); break;
            }
            LastResultMessage = discovery.RewardText;
            AdventureDiscoveryFound?.Invoke(discovery);
        }
        Persist(); return true;
    }
    public AdventureMapNode GetAdventureHeroNode(string mapId)
    {
        mapId = RouteCatalog.Normalize(mapId);
        return _adventureHeroNodes.TryGetValue(mapId, out var id) && AdventureMapCatalog.Find(id) is { } node
            ? node : AdventureMapCatalog.ForMap(mapId).First();
    }
    public Vector2 GetAdventureHeroPosition(string mapId) => _adventureHeroPositions.TryGetValue(RouteCatalog.Normalize(mapId), out var point)
        ? point : GetAdventureHeroNode(mapId).Point;
    public bool MoveAdventureHero(string mapId, Vector2 point, bool persist = true)
    {
        mapId = RouteCatalog.Normalize(mapId);
        if (!GameData.Stages.Any(x => x.MapId == mapId) || !float.IsFinite(point.X) || !float.IsFinite(point.Y)) return false;
        var cell = AdventureTerrain.Cell(point);
        if (!AdventureTerrain.Walkable(mapId, cell)) return false;
        RevealAdventurePoint(mapId, point);
        _adventureHeroPositions[mapId] = point;
        if (persist) Persist();
        return true;
    }
    private void RevealAdventurePoint(string mapId, Vector2 point, int radius = 2)
    {
        if (!_adventureExploredCells.TryGetValue(mapId, out var cells)) _adventureExploredCells[mapId] = cells = new();
        var cell = AdventureTerrain.Cell(point);
        if (cell < 0) return;
        var changed = false;
        foreach (var nearby in AdventureTerrain.Area(cell,radius)) changed |= cells.Add(nearby);
        if (changed) AdventureKnowledgeRevision++;
    }
    public bool TryVisitAdventureSite(string id, out string message)
    {
        var node = AdventureMapCatalog.Find(id);
        if (node == null || !CanVisitAdventureSite(id) || !IsAdventureSiteDiscovered(id))
        {
            message = node?.Kind == AdventureSiteKind.Leader && IsAdventureBoss(node.Stage)
                ? $"Defeat {GetAdventureBossRemainingLeaders(node.Stage)} more leaders in this district to open the boss gate."
                : "This site has not been discovered.";
            return false;
        }
        var tile = AdventureTileCatalog.Find(node.MapId, node.Id);
        if (!HasReachedAdventureTile(id) || GetAdventureCaravanTile(node.MapId).Id != id)
        { message = "Travel to this tile first."; return false; }
        _adventureHeroPositions[node.MapId] = node.Point;
        _adventureHeroNodes[node.MapId] = id;
        var resource = node.Kind is AdventureSiteKind.Gold or AdventureSiteKind.Food;
        if (resource && !HasVisitedAdventureSite(id) && Food < AdventureTileFoodCost)
        { message = $"Opening this tile costs {AdventureTileFoodCost} food."; return false; }
        if (node.Kind == AdventureSiteKind.Leader) SelectedStage = node.Stage;
        var firstVisit = _visitedAdventureSites.Add(id);
        if (firstVisit)
        {
            if (resource) Food -= AdventureTileFoodCost;
            Gold += node.GoldReward; Food += node.FoodReward;
            if (node.Kind != AdventureSiteKind.Leader) OpenSurroundingAdventureTiles(tile);
        }
        message = node.Kind switch
        {
            AdventureSiteKind.Leader => $"You face {node.Title}. Prepare your warband to challenge this leader.",
            _ when !firstVisit => "Already visited. These rewards have been collected.",
            AdventureSiteKind.Gold => $"Treasury secured · +{node.GoldReward} gold",
            AdventureSiteKind.Food => $"Supplies secured · +{node.FoodReward} food",
            _ => "Nearby tiles opened."
        };
        LastResultMessage = message;
        if (firstVisit && resource) FoodChanged?.Invoke();
        Persist(); return true;
    }
    private void ResetAdventureProgress()
    {
        _visitedAdventureSites.Clear(); _adventureHeroNodes.Clear(); _adventureHeroPositions.Clear(); _adventureExploredCells.Clear();
        _adventureTravelledCells.Clear(); _claimedAdventureDiscoveries.Clear();
        _openAdventureTiles.Clear(); _reachedAdventureTiles.Clear(); _adventureCaravanTiles.Clear(); AdventureKnowledgeRevision++;
    }
    private void LoadAdventureProgress(GameSaveData saved)
    {
        ResetAdventureProgress();
        foreach (var id in saved.VisitedAdventureSites ?? Array.Empty<string>())
            if (AdventureMapCatalog.Find(id) != null) _visitedAdventureSites.Add(id);
        foreach (var pair in saved.AdventureHeroNodes ?? new Dictionary<string, string>())
            if (AdventureMapCatalog.Find(pair.Value) is { } node && node.MapId == pair.Key) _adventureHeroNodes[pair.Key] = pair.Value;
        foreach (var map in GameData.Stages.Select(x => x.MapId).Distinct())
        {
            var camp = AdventureTerrain.Cell(AdventureMapCatalog.ForMap(map)[0].Point);
            if (saved.Version >= 44)
            {
                if (saved.AdventureExploredCells?.TryGetValue(map,out var explored) == true && explored != null)
                    _adventureExploredCells[map] = explored.Where(c => c >= 0 && c < AdventureTerrain.CellCount).ToHashSet();
                if (saved.AdventureTravelledCells?.TryGetValue(map,out var travelled) == true && travelled != null)
                    _adventureTravelledCells[map] = travelled.Where(c => AdventureTerrain.Walkable(map,c)).ToHashSet();
                if (saved.AdventureHeroPositions?.TryGetValue(map,out var p) == true && p is {Length:2} && float.IsFinite(p[0]) && float.IsFinite(p[1])
                    && AdventureTerrain.Walkable(map,AdventureTerrain.Cell(new Vector2(p[0],p[1])))) _adventureHeroPositions[map] = new(p[0],p[1]);
                // Claims on retired finds stay recorded so their tiles keep their open surroundings.
                foreach (var reward in AdventureDiscoveryCatalog.Legacy(map))
                    if ((saved.ClaimedAdventureDiscoveries ?? Array.Empty<string>()).Contains(reward.Id)) _claimedAdventureDiscoveries.Add(reward.Id);
            }
            else
            {
                // Move the old caravan with its stable landmark ID; retain known/visited sites,
                // while the new surrounding land and all new discoveries remain unclaimed.
                _adventureHeroPositions[map] = GetAdventureHeroNode(map).Point;
                var oldCells = saved.AdventureExploredCells?.GetValueOrDefault(map) ?? Array.Empty<int>();
                foreach (var node in AdventureMapCatalog.ForMap(map))
                    if (HasVisitedAdventureSite(node.Id) || oldCells.Contains(AdventureMapCatalog.LegacyCell(node))
                        || node.Kind == AdventureSiteKind.Leader && ((saved.StageStars?.ElementAtOrDefault(node.Stage - 1) ?? 0) > 0 || saved.Version < 43 && node.Stage <= saved.HighestUnlockedStage))
                        RevealAdventurePoint(map,node.Point);
                _adventureTravelledCells[map] = AdventureMapCatalog.ForMap(map).Where(n => HasVisitedAdventureSite(n.Id)).Select(n => AdventureTerrain.Cell(n.Point)).Append(camp).ToHashSet();
            }
            RevealAdventurePoint(map,GetAdventureHeroPosition(map));
        }
        LoadAdventureTiles(saved);
    }
}
