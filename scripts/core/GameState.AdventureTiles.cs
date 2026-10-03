using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class GameState
{
    private readonly HashSet<string> _openAdventureTiles = new(StringComparer.Ordinal);
    private readonly HashSet<string> _reachedAdventureTiles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _adventureCaravanTiles = new(StringComparer.Ordinal);
    public AdventureTile GetAdventureCaravanTile(string mapId) => AdventureTileCatalog.Find(mapId, _adventureCaravanTiles.GetValueOrDefault(mapId, ""))
        ?? AdventureTileCatalog.Starting(mapId);
    public bool IsAdventureTileOpen(AdventureTile tile) => tile != null &&
        (_openAdventureTiles.Contains(tile.Id) || AdventureTileCatalog.Starting(tile.MapId).Id == tile.Id);
    public bool HasReachedAdventureTile(string id) => id.StartsWith("camp-", StringComparison.Ordinal) || _reachedAdventureTiles.Contains(id)
        || AdventureMapCatalog.Find(id) is { Kind: AdventureSiteKind.Leader } site && AdventureTileCatalog.Starting(site.MapId).Id == id;
    public int GetAdventureTileTravelFoodCost(AdventureTile tile) => 0;
    public bool IsAdventureTileComplete(AdventureTile tile) => tile?.Site is { } site
        ? site.Kind == AdventureSiteKind.Leader ? GetStageStars(site.Stage) > 0 : HasVisitedAdventureSite(site.Id) || site.Kind == AdventureSiteKind.Camp
        : tile?.Discovery is { } reward ? HasClaimedAdventureDiscovery(reward.Id) : false;
    public bool CanTravelToAdventureTile(AdventureTile tile, out string message)
    {
        if (tile == null || !tile.HasInterest || !IsAdventureZoneUnlocked(tile.MapId) || !IsAdventureTileOpen(tile))
        { message = "Complete a nearby stage or gather supplies to open this tile."; return false; }
        if (tile.Site is { } site && !CanVisitAdventureSite(site.Id))
        { message = site.Kind == AdventureSiteKind.Leader ? $"Defeat {GetAdventureBossRemainingLeaders(site.Stage)} more leaders to open the boss gate." : "This site is not available."; return false; }
        if ((tile.Discovery != null || tile.Site?.Kind is AdventureSiteKind.Gold or AdventureSiteKind.Food) && IsAdventureTileComplete(tile))
        { message = "These supplies have already been collected."; return false; }
        message = ""; return true;
    }
    // Reaching an open tile is free; battle entry is charged separately on deployment.
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
    public bool TryCollectAdventureTile(AdventureTile tile, out string message)
    {
        if (tile == null || !IsAdventureTileOpen(tile) || !HasReachedAdventureTile(tile.Id) || GetAdventureCaravanTile(tile.MapId).Id != tile.Id)
        { message = "Travel to this tile first."; return false; }
        if (tile.Site != null) return TryVisitAdventureSite(tile.Site.Id, out message);
        var reward = tile.Discovery;
        if (reward == null || !_claimedAdventureDiscoveries.Add(reward.Id))
        { message = "These supplies have already been collected."; return false; }
        switch (reward.Kind)
        {
            case AdventureDiscoveryKind.Food: Food += reward.Amount; break;
            case AdventureDiscoveryKind.Gold: Gold += reward.Amount; break;
            case AdventureDiscoveryKind.Tomes: Tomes += reward.Amount; break;
            case AdventureDiscoveryKind.Essence: Essence += reward.Amount; break;
        }
        OpenSurroundingAdventureTiles(tile);
        message = LastResultMessage = reward.RewardText;
        Persist();
        if (reward.Kind == AdventureDiscoveryKind.Food) FoodChanged?.Invoke();
        AdventureDiscoveryFound?.Invoke(reward);
        return true;
    }
    private void OpenSurroundingAdventureTiles(AdventureTile tile)
    {
        var changed = false;
        foreach (var nearby in AdventureTileCatalog.Surrounding(tile)) changed |= _openAdventureTiles.Add(nearby.Id);
        if (changed) AdventureKnowledgeRevision++;
    }
    private void RevealAdventureStageVictory(int stage)
    {
        var site = AdventureMapCatalog.Leader(stage);
        if (site == null) return;
        var tile = AdventureTileCatalog.Find(site.MapId, site.Id);
        _reachedAdventureTiles.Add(tile.Id);
        OpenSurroundingAdventureTiles(tile);
    }
    private void LoadAdventureTiles(GameSaveData saved)
    {
        _openAdventureTiles.Clear(); _reachedAdventureTiles.Clear(); _adventureCaravanTiles.Clear();
        foreach (var map in GameData.Stages.Select(stage => stage.MapId).Distinct())
        {
            var tiles = AdventureTileCatalog.ForMap(map);
            foreach (var tile in tiles)
            {
                if (saved.Version >= 45)
                {
                    var open = saved.AdventureOpenTiles ?? Array.Empty<string>();
                    var retiredOpen = !string.IsNullOrEmpty(tile.RetiredSiteId) && open.Contains(tile.RetiredSiteId);
                    if (open.Contains(tile.Id) || retiredOpen) _openAdventureTiles.Add(tile.Id);
                    if ((saved.AdventureReachedTiles ?? Array.Empty<string>()).Contains(tile.Id)) _reachedAdventureTiles.Add(tile.Id);
                }
                else
                {
                    var point = tile.Site?.Point ?? tile.Discovery?.Point ?? AdventureMapCatalog.Find(tile.RetiredSiteId)?.Point;
                    if (point.HasValue && IsAdventureCellRevealed(map, AdventureTerrain.Cell(point.Value))) _openAdventureTiles.Add(tile.Id);
                    if (point.HasValue && IsAdventureCellTravelled(map, AdventureTerrain.Cell(point.Value))) _reachedAdventureTiles.Add(tile.Id);
                }
                var cleared = tile.Site?.Kind == AdventureSiteKind.Leader && (saved.StageStars?.ElementAtOrDefault(tile.Site.Stage - 1) ?? 0) > 0;
                var retiredVisited = !string.IsNullOrEmpty(tile.RetiredSiteId) && HasVisitedAdventureSite(tile.RetiredSiteId);
                var collected = retiredVisited || tile.Site != null && HasVisitedAdventureSite(tile.Id) && tile.Site.Kind != AdventureSiteKind.Leader
                    || tile.Discovery != null && HasClaimedAdventureDiscovery(tile.Id);
                if (retiredVisited || tile.Site != null && HasVisitedAdventureSite(tile.Id))
                {
                    _openAdventureTiles.Add(tile.Id);
                    _reachedAdventureTiles.Add(tile.Id);
                }
                if (cleared || collected)
                {
                    _reachedAdventureTiles.Add(tile.Id);
                    OpenSurroundingAdventureTiles(tile);
                }
            }
            var caravanId = saved.Version >= 45 ? saved.AdventureCaravanTiles?.GetValueOrDefault(map) : GetAdventureHeroNode(map).Id;
            if (AdventureTileCatalog.Find(map, caravanId) is { } caravan && IsAdventureTileOpen(caravan))
                _adventureCaravanTiles[map] = caravan.Id;
        }
        AdventureKnowledgeRevision++;
    }
}
