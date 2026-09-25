using System;
using System.Collections.Generic;
using System.Linq;

public partial class GameState
{
    private readonly HashSet<int> _claimedProgressionMilestones = new();
    private readonly HashSet<int> _claimedStageMasteryRewards = new();

    private void ResetProgressionRewards()
    {
        _claimedProgressionMilestones.Clear();
        _claimedStageMasteryRewards.Clear();
    }

    private void LoadProgressionRewards(GameSaveData saved)
    {
        ResetProgressionRewards();
        foreach (var stage in saved.ClaimedProgressionMilestones ?? Array.Empty<int>())
            if (CampaignProgressionCatalog.Get(stage) != null) _claimedProgressionMilestones.Add(stage);
        foreach (var stage in saved.ClaimedStageMasteryRewards ?? Array.Empty<int>())
            if (stage >= 1 && stage <= MaxStage) _claimedStageMasteryRewards.Add(stage);
    }

    public string BuildProgressionRewardPreview(int stage)
    {
        var parts = new List<string>();
        var milestone = CampaignProgressionCatalog.Get(stage);
        if (milestone != null && !_claimedProgressionMilestones.Contains(stage))
        {
            if (milestone.RelicId.Length > 0)
            {
                var relic = GameData.GetEquipment(milestone.RelicId);
                parts.Add(_ownedEquipmentIds.Contains(relic.Id)
                    ? $"Victory milestone: {RelicForgeCatalog.GetDismantleShards(relic.Rarity)} relic shards (owned relic)."
                    : $"Victory milestone: {relic.DisplayName}.");
            }
            if (milestone.UnitId.Length > 0)
                parts.Add($"Victory contract: {GameData.GetUnit(milestone.UnitId).DisplayName} · level {milestone.UnitLevel}\n+{milestone.Tomes} tomes · keeps higher levels.");
        }
        if (!_claimedStageMasteryRewards.Contains(stage))
        {
            var shards = CampaignProgressionCatalog.MasteryShards(stage);
            parts.Add($"First 3-star victory: +{shards} relic shard{(shards == 1 ? "" : "s")}.");
        }
        return string.Join("\n", parts);
    }

    private string ClaimCampaignProgressionRewards(int stage, int starsEarned)
    {
        if (CurrentBattleMode != BattleRunMode.Campaign) return "";
        var parts = new List<string>();
        var discoveries = new List<string>();
        var milestone = CampaignProgressionCatalog.Get(stage);
        if (milestone != null && _claimedProgressionMilestones.Add(stage))
        {
            if (milestone.RelicId.Length > 0)
            {
                var relic = GameData.GetEquipment(milestone.RelicId);
                if (_ownedEquipmentIds.Add(relic.Id))
                {
                    discoveries.Add(relic.Id);
                    parts.Add($"Milestone relic: {relic.DisplayName}. Equip it in the armory's Relics tab.");
                }
                else
                {
                    var shards = RelicForgeCatalog.GetDismantleShards(relic.Rarity);
                    RelicShards += shards;
                    parts.Add($"Milestone duplicate: +{shards} relic shards.");
                }
            }
            if (milestone.UnitId.Length > 0)
            {
                _ownedPlayerUnitIds.Add(milestone.UnitId);
                _unitUpgradeLevels[milestone.UnitId] = Math.Max(GetUnitLevel(milestone.UnitId), milestone.UnitLevel);
                Tomes += milestone.Tomes;
                discoveries.Add(milestone.UnitId);
                parts.Add($"Contract fulfilled: {GameData.GetUnit(milestone.UnitId).DisplayName} level {GetUnitLevel(milestone.UnitId)}; +{milestone.Tomes} tomes.");
            }
        }
        if (starsEarned >= 3 && _claimedStageMasteryRewards.Add(stage))
        {
            var shards = CampaignProgressionCatalog.MasteryShards(stage);
            RelicShards += shards;
            parts.Add($"Stage mastered: +{shards} relic shards.");
        }
        // Codex discovery can persist; first finish the entire reward transaction.
        foreach (var id in discoveries) DiscoverCodexEntry(id);
        return string.Join(" ", parts);
    }

    // Boss rolls represent a newly earned copy. Ordinary idempotent grants keep their existing semantics.
    public bool GrantBossRelic(string equipmentId, out int duplicateShards)
    {
        duplicateShards = 0;
        var relic = GameData.GetAllEquipment().FirstOrDefault(item =>
            string.Equals(item.Id, equipmentId, StringComparison.OrdinalIgnoreCase));
        if (relic == null) return false;
        if (TryGrantEquipment(relic.Id)) return true;
        duplicateShards = RelicForgeCatalog.GetDismantleShards(relic.Rarity);
        RelicShards += duplicateShards;
        Persist();
        return false;
    }
}
