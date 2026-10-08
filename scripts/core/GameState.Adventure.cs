using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Campaign zone access, site visits and find claims. Tile exploration lives in GameState.AdventureTiles.</summary>
public partial class GameState
{
    private readonly HashSet<string> _visitedAdventureSites = new(StringComparer.Ordinal);
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
            || AdventureTileCatalog.ForMap(map).Any(tile => tile.Id != AdventureTileCatalog.Starting(map).Id && _reachedAdventureTiles.Contains(tile.Id)));
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
        IsAdventureTileRevealed(AdventureTileCatalog.Find(node.MapId, node.Id));
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
        // Caches are gathered by opening their tile.
        if (node.Kind != AdventureSiteKind.Leader)
        {
            if (HasVisitedAdventureSite(id)) { message = "Already visited. These rewards have been collected."; return false; }
            return TryOpenAdventureTile(tile, out message);
        }
        SelectedStage = node.Stage;
        _visitedAdventureSites.Add(id);
        message = LastResultMessage = $"You face {node.Title}. Prepare your warband to challenge this leader.";
        Persist(); return true;
    }
    private void ResetAdventureProgress()
    {
        _visitedAdventureSites.Clear(); _claimedAdventureDiscoveries.Clear();
        _openedAdventureTiles.Clear(); _reachedAdventureTiles.Clear(); _adventureCaravanTiles.Clear(); AdventureKnowledgeRevision++;
    }
    /// <summary>Visited sites and claimed finds; the atlas tiles load later, once the stars are in (LoadAdventureTiles).</summary>
    private void LoadAdventureProgress(GameSaveData saved)
    {
        ResetAdventureProgress();
        foreach (var id in saved.VisitedAdventureSites ?? Array.Empty<string>())
            if (AdventureMapCatalog.Find(id) != null) _visitedAdventureSites.Add(id);
        var claimed = (saved.ClaimedAdventureDiscoveries ?? Array.Empty<string>()).ToHashSet(StringComparer.Ordinal);
        foreach (var map in GameData.Stages.Select(x => x.MapId).Distinct())
            foreach (var find in AdventureDiscoveryCatalog.ForMap(map))
                if (claimed.Contains(find.Id)) _claimedAdventureDiscoveries.Add(find.Id);
    }
}
