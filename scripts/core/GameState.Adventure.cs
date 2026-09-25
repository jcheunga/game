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
    private const int AdventureCellSize = 128, AdventureColumns = 12, AdventureRows = 8;

    public bool HasVisitedAdventureSite(string id) => _visitedAdventureSites.Contains(id);
    public bool IsAdventureBoss(int stage) => stage >= 1 && stage <= MaxStage &&
        GameData.GetStagesForMap(GameData.GetStage(stage).MapId).Max(x => x.StageNumber) == stage;
    public int GetAdventureBossRemainingLeaders(int stage) => GameData.GetStagesForMap(GameData.GetStage(stage).MapId)
        .Count(x => x.StageNumber != stage && GetStageStars(x.StageNumber) <= 0);
    // All ordinary encounters are independent. Previously defeated bosses remain replayable.
    public bool IsCampaignStageUnlocked(int stage) => stage >= 1 && stage <= MaxStage &&
        (!IsAdventureBoss(stage) || GetStageStars(stage) > 0 || GetAdventureBossRemainingLeaders(stage) == 0);
    public bool CanVisitAdventureSite(string id) => AdventureMapCatalog.Find(id) is { } node &&
        (node.Kind != AdventureSiteKind.Leader || IsCampaignStageUnlocked(node.Stage)) &&
        (string.IsNullOrEmpty(node.RequiredVisit) || HasVisitedAdventureSite(node.RequiredVisit));
    public bool IsAdventureSiteDiscovered(string id)
    {
        var node = AdventureMapCatalog.Find(id);
        return node != null && (string.IsNullOrEmpty(node.RequiredVisit) || HasVisitedAdventureSite(node.RequiredVisit)) &&
            GetAdventureRevealAreas(node.MapId).Any(area => node.Point.DistanceTo(new Vector2(area.X, area.Y)) < area.Z - 35);
    }
    public AdventureMapNode GetAdventureHeroNode(string mapId)
    {
        mapId = RouteCatalog.Normalize(mapId);
        return _adventureHeroNodes.TryGetValue(mapId, out var id) && AdventureMapCatalog.Find(id) is { } node
            ? node : AdventureMapCatalog.ForMap(mapId).First();
    }
    public Vector2 GetAdventureHeroPosition(string mapId) => _adventureHeroPositions.TryGetValue(RouteCatalog.Normalize(mapId), out var point)
        ? point : GetAdventureHeroNode(mapId).Point;
    public int GetAdventureStartingCourageBonus(int stage) => AdventureMapCatalog.ForMap(GameData.GetStage(stage).MapId)
        .Count(x => x.Kind == AdventureSiteKind.Shrine && HasVisitedAdventureSite(x.Id)) * 3;

    // A bounded grid records exploration along actual movement, independent of victories.
    public IReadOnlyList<Vector3> GetAdventureRevealAreas(string mapId)
    {
        mapId = RouteCatalog.Normalize(mapId);
        var camp = AdventureMapCatalog.ForMap(mapId).First();
        var result = new List<Vector3> { new(camp.Point.X, camp.Point.Y, 245) };
        if (_adventureExploredCells.TryGetValue(mapId, out var cells))
            foreach (var cell in cells.OrderBy(x => x))
                result.Add(new Vector3((cell % AdventureColumns + .5f) * AdventureCellSize, (cell / AdventureColumns + .5f) * AdventureCellSize, 225));
        foreach (var node in AdventureMapCatalog.ForMap(mapId).Where(x => HasVisitedAdventureSite(x.Id)))
            result.Add(new Vector3(node.Point.X, node.Point.Y, node.Kind == AdventureSiteKind.Watchtower ? 390 : 210));
        return result;
    }
    public bool MoveAdventureHero(string mapId, Vector2 point, bool persist = true)
    {
        mapId = RouteCatalog.Normalize(mapId);
        if (!GameData.Stages.Any(x => x.MapId == mapId) || !float.IsFinite(point.X) || !float.IsFinite(point.Y)) return false;
        point = point.Clamp(Vector2.Zero, AdventureMapCatalog.WorldSize - Vector2.One);
        var origin = GetAdventureHeroPosition(mapId);
        var steps = Math.Max(1, (int)Math.Ceiling(origin.DistanceTo(point) / 64));
        for (var i = 0; i <= steps; i++) RevealAdventurePoint(mapId, origin.Lerp(point, (float)i / steps));
        _adventureHeroPositions[mapId] = point;
        if (persist) Persist();
        return true;
    }
    private void RevealAdventurePoint(string mapId, Vector2 point)
    {
        if (!_adventureExploredCells.TryGetValue(mapId, out var cells)) _adventureExploredCells[mapId] = cells = new();
        cells.Add(Math.Clamp((int)(point.Y / AdventureCellSize), 0, AdventureRows - 1) * AdventureColumns + Math.Clamp((int)(point.X / AdventureCellSize), 0, AdventureColumns - 1));
    }
    public bool TryVisitAdventureSite(string id, out string message)
    {
        var node = AdventureMapCatalog.Find(id);
        if (node == null || !CanVisitAdventureSite(id))
        {
            message = node?.Kind == AdventureSiteKind.Leader && IsAdventureBoss(node.Stage)
                ? $"Defeat {GetAdventureBossRemainingLeaders(node.Stage)} more leaders in this district to open the boss gate."
                : "This site has not been discovered.";
            return false;
        }
        MoveAdventureHero(node.MapId, node.Point, false);
        _adventureHeroNodes[node.MapId] = id;
        if (node.Kind == AdventureSiteKind.Leader) SelectedStage = node.Stage;
        var firstVisit = _visitedAdventureSites.Add(id);
        if (firstVisit) { Gold += node.GoldReward; Food += node.FoodReward; }
        message = node.Kind switch
        {
            AdventureSiteKind.Leader => $"You face {node.Title}. Prepare your warband to challenge this leader.",
            AdventureSiteKind.Camp => "The caravan has returned to camp.",
            _ when !firstVisit => "Already visited. These rewards have been collected.",
            AdventureSiteKind.Gold => $"Treasury secured · +{node.GoldReward} gold",
            AdventureSiteKind.Food => $"Supplies secured · +{node.FoodReward} food",
            AdventureSiteKind.Shrine => $"Shrine kindled · +3 starting courage in {RouteCatalog.Get(node.MapId).Title}",
            _ => "Watchtower charted · the surrounding fog has lifted."
        };
        LastResultMessage = message;
        Persist();
        return true;
    }
    private void ResetAdventureProgress()
    {
        _visitedAdventureSites.Clear(); _adventureHeroNodes.Clear(); _adventureHeroPositions.Clear(); _adventureExploredCells.Clear();
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
            if (saved.AdventureExploredCells?.TryGetValue(map, out var cells) == true && cells != null)
                _adventureExploredCells[map] = cells.Where(x => x >= 0 && x < AdventureColumns * AdventureRows).ToHashSet();
            if (saved.AdventureHeroPositions?.TryGetValue(map, out var coordinates) == true && coordinates is { Length: 2 } && float.IsFinite(coordinates[0]) && float.IsFinite(coordinates[1]))
                _adventureHeroPositions[map] = new Vector2(coordinates[0], coordinates[1]).Clamp(Vector2.Zero, AdventureMapCatalog.WorldSize - Vector2.One);
            // Preserve the main-road knowledge of older saves without granting any resources.
            if (saved.Version < 41)
                foreach (var node in AdventureMapCatalog.ForMap(map).Where(x => x.Kind == AdventureSiteKind.Leader && x.Stage <= saved.HighestUnlockedStage))
                    RevealAdventurePoint(map, node.Point);
        }
    }
}
