using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

// Analysis-only controls. Every run requires the isolated combat-review save suffix.
public partial class CombatReviewSmoke
{
    private void CheckProgressionRewards()
    {
        var state = GameState.Instance;
        state.ResetProgress();
        state.SetAnalyticsConsent(false);
        foreach (var (id, expected) in new[] { ("relic_iron_pendant", 1), ("relic_war_brand", 3), ("relic_crown_of_valor", 9) })
        {
            var before = state.RelicShards;
            Check(state.GrantBossRelic(id, out var shards) && shards == 0 && state.RelicShards == before,
                $"First {id} boss drop grants the relic without duplicate shards");
            state.TryEquipItem("player_defender", id);
            Check(!state.GrantBossRelic(id, out shards) && shards == expected && state.RelicShards == before + expected,
                $"Duplicate {id} boss drop grants {expected} forge shards");
            Check(state.GetUnitEquipment("player_defender")?.Id == id,
                $"Duplicate {id} preserves the owned and equipped original");
            before = state.RelicShards;
            state.TryGrantEquipment(id);
            Check(state.RelicShards == before, "Idempotent non-boss grants cannot mint duplicate shards");
        }
        var savedShards = state.RelicShards;
        Check(!state.GrantBossRelic("unknown_relic", out var invalidShards) && invalidShards == 0 && state.RelicShards == savedShards,
            "Invalid boss relic cannot grant currency");
        CheckCampaignProgression();
        state.ResetProgress();
        state.SetAnalyticsConsent(false);
        state.SetShowHints(false);
        state.UnlockNextStage(59);
    }

    private void CheckCampaignProgression()
    {
        var state = GameState.Instance;
        state.ResetProgress();
        state.PrepareCampaignBattle();
        var reward = state.ApplyVictory(4, 0, 0, 1);
        Check(reward.Contains("Iron Pendant") && state.TryEquipItem("player_defender", "relic_iron_pendant"),
            "An ordinary first boss victory guarantees an equippable relic");
        var shards = state.RelicShards;
        state.ApplyVictory(4, 0, 0, 3);
        Check(state.RelicShards == shards + 3, "Improving a clear to three stars grants the boss mastery shards");
        shards = state.RelicShards;
        state.ReloadFromDisk();
        state.PrepareCampaignBattle();
        state.ApplyVictory(4, 0, 0, 3);
        Check(state.RelicShards == shards, "Reload and replay cannot duplicate milestone or mastery rewards");
        Check(!state.BuildProgressionRewardPreview(4).Contains("milestone", StringComparison.OrdinalIgnoreCase),
            "Claimed rewards disappear from the preview");

        state.ApplyVictory(43, 0, 0, 1);
        Check(state.IsUnitOwned("player_lantern_guard") && state.GetUnitLevel("player_lantern_guard") == 4,
            "Out-of-order contract victory grants a trained specialist before its major counter fight");
        var tomes = state.Tomes;
        state.ApplyVictory(43, 0, 0, 1);
        Check(state.Tomes == tomes, "Recruit contracts award training and tomes only once");
        var levels = Read<Dictionary<string, int>>(state, "_unitUpgradeLevels");
        Read<HashSet<string>>(state, "_ownedPlayerUnitIds").Add("player_marksman");
        levels["player_marksman"] = 5;
        state.ApplyVictory(18, 0, 0, 1);
        Check(state.GetUnitLevel("player_marksman") == 5 && state.Tomes == tomes + 1,
            "Already developed recruits retain their levels and receive the advertised tome reward");

        var save = (GameSaveData)Invoke(state, "BuildSaveData");
        var roundTrip = System.Text.Json.JsonSerializer.Deserialize<GameSaveData>(System.Text.Json.JsonSerializer.Serialize(save));
        state.ResetProgress();
        Invoke(state, "ApplySavedData", roundTrip);
        state.PrepareCampaignBattle();
        tomes = state.Tomes;
        state.ApplyVictory(43, 0, 0, 1);
        Check(state.Tomes == tomes && state.GetUnitLevel("player_lantern_guard") == 4,
            "Serialized saves preserve contract claims and trained units");

        state.ResetProgress();
        state.PrepareEndlessBattle("city");
        Check((string)Invoke(state, "ClaimCampaignProgressionRewards", 43, 3) == "" && !state.IsUnitOwned("player_lantern_guard"),
            "Other game modes cannot claim campaign contracts");
        state.PrepareCampaignBattle();
        state.TryGrantEquipment("relic_iron_pendant");
        shards = state.RelicShards;
        state.ApplyVictory(4, 0, 0, 1);
        Check(state.RelicShards == shards + 1, "An owned milestone relic becomes useful crafting shards");
        var legacy = (GameSaveData)Invoke(state, "BuildSaveData");
        legacy.Version = 41;
        legacy.ClaimedProgressionMilestones = null;
        legacy.ClaimedStageMasteryRewards = null;
        Invoke(state, "ApplySavedData", legacy);
        state.PrepareCampaignBattle();
        shards = state.RelicShards;
        state.ApplyVictory(4, 0, 0, 1);
        Check(state.RelicShards == shards + 1, "Existing saves can earn newly introduced milestones once by replaying");
        state.ApplyVictory(60, 0, 0, 1);
        Check(state.TryPrestige(out _) && state.BuildProgressionRewardPreview(4).Contains("milestone"),
            "A new prestige campaign restores its progression reward track");

        Check(GameData.Stages.All(s => s.Objectives.All(o => o.Type != "enemy_defeats" ||
            o.Value <= s.Waves.Sum(w => w.Entries.Sum(e => e.Count)))),
            "Campaign kill objectives never require farming additional summoned enemies");
        Check(GameData.GetAllEquipment().Where(e => e.Rarity == "epic" && CampaignProgressionCatalog.IsCampaignRelic(e.Id)).Any() &&
            !CampaignProgressionCatalog.IsCampaignRelic("relic_raid_grave_crown") &&
            !CampaignProgressionCatalog.IsCampaignRelic("relic_tower_sentinel"),
            "Campaign boss pool retains epics while preserving raid and tower exclusivity");

        state.PrepareCampaignBattle();
        foreach (var stage in new[] { 43, 47, 53 }) state.ApplyVictory(stage, 0, 0, 1);
        save = (GameSaveData)Invoke(state, "BuildSaveData");
        save.Gold = 20000; save.Sigils = 30; save.Tomes = 10;
        var lateUnits = new[] { "player_lantern_guard", "player_ballista", "player_stormcaller" };
        foreach (var id in lateUnits) save.UnitLevels[id] = 5;
        Invoke(state, "ApplySavedData", save);
        foreach (var id in lateUnits)
        {
            var before = state.BuildPlayerUnitStats(GameData.GetUnit(id));
            Check(state.TryPromoteUnit(id, out _) && state.BuildPlayerUnitStats(GameData.GetUnit(id)).MaxHealth > before.MaxHealth,
                $"{id} has a working promotion with a real combat benefit");
            Check(state.TryUnlockSkillNode(id, id + "_t1", out _), $"{id} can develop its new skill tree");
        }
        Check(state.Gold == 12100 && state.Sigils == 14 && state.Tomes == 7,
            "Late-unit promotions and skill nodes charge the published gold, sigil and tome costs");
    }

    private void ApplyProgressionRelics(int stage)
    {
        var milestoneRelics = OS.GetCmdlineUserArgs().Contains("--milestone-relics");
        if (!OS.GetCmdlineUserArgs().Contains("--common-relics") && !milestoneRelics) return;
        var state = GameState.Instance;
        var deck = state.ActiveDeckUnitIds.ToArray();
        var relics = milestoneRelics
            ? new[] { "relic_crown_of_valor", "relic_blade_of_ruin", "relic_war_brand" }
            : new[] { "relic_battle_drum", "relic_sharpened_edge", "relic_iron_pendant" };
        if (milestoneRelics && relics.Any(id => !CampaignProgressionCatalog.GetAll().Any(m => m.RelicId == id && m.Stage < stage)))
            throw new InvalidOperationException("Milestone loadout requires all three relics to be earnable before this stage.");
        for (var i = 0; i < deck.Length; i++)
        {
            state.TryGrantEquipment(relics[i]);
            if (!state.TryEquipItem(deck[i], relics[i]))
                throw new InvalidOperationException($"Cannot equip {relics[i]} on {deck[i]}.");
        }
    }

    // Ask the real pricing functions at each level, restoring the original dictionary afterwards.
    private int[] ProgressionCosts(string field, string id, int first, int last, Func<string, int> cost)
    {
        var levels = Read<Dictionary<string, int>>(GameState.Instance, field);
        var existed = levels.TryGetValue(id, out var original);
        try
        {
            return Enumerable.Range(first, Math.Max(0, last - first)).Select(level =>
            {
                levels[id] = level;
                return cost(id);
            }).ToArray();
        }
        finally
        {
            if (existed) levels[id] = original;
            else levels.Remove(id);
        }
    }

    private object GetProgressionInvestment()
    {
        var state = GameState.Instance;
        var recruits = state.ActiveDeckUnitIds.Sum(state.GetUnitPurchaseCost);
        var units = state.ActiveDeckUnitIds.Sum(id => ProgressionCosts("_unitUpgradeLevels", id, 1,
            state.GetUnitLevel(id), state.GetUnitUpgradeCost).Sum());
        var spells = state.ActiveDeckSpellIds.Sum(id => state.GetSpellPurchaseCost(id) +
            ProgressionCosts("_spellUpgradeLevels", id, 1, state.GetSpellLevel(id), state.GetSpellUpgradeCost).Sum());
        var wagon = BaseUpgradeCatalog.GetAll().Sum(upgrade => ProgressionCosts("_baseUpgradeLevels", upgrade.Id,
            0, state.GetBaseUpgradeLevel(upgrade.Id), state.GetBaseUpgradeCost).Sum());
        return new { recruits, units, spells, wagon, gold = recruits + units + spells + wagon,
            relics = OS.GetCmdlineUserArgs().Any(x => x == "--common-relics" || x == "--milestone-relics") ? 3 : 0,
            note = "Snapshot cost, not a campaign ledger; relic acquisition is excluded from gold." };
    }

    private void ExportProgressionEconomy()
    {
        var state = GameState.Instance;
        state.ResetProgress();
        state.SetAnalyticsConsent(false);
        var units = GameData.GetPlayerUnits().Select(unit => new
        {
            id = unit.Id, name = unit.DisplayName, unlock = unit.UnlockStage,
            recruit = state.GetUnitPurchaseCost(unit.Id), courage = unit.Cost,
            upgrades = ProgressionCosts("_unitUpgradeLevels", unit.Id, 1, 5, state.GetUnitUpgradeCost),
            levels = Enumerable.Range(1, 5).Select(level =>
            {
                // Empty deck removes synergy; fresh save removes gear and other persistent power.
                var stats = state.BuildPlayerUnitStatsAtLevelForDeck(unit, level, Array.Empty<UnitDefinition>());
                return new { level, hp = stats.MaxHealth, damage = stats.AttackDamage, cooldown = stats.AttackCooldown };
            }).ToArray(),
            promotion = UnitPromotionCatalog.TryGet(unit.Id)
        }).ToArray();
        var spells = GameData.GetPlayerSpells().Select(spell => new
        {
            id = spell.Id, name = spell.DisplayName, unlock = spell.UnlockStage,
            recruit = state.GetSpellPurchaseCost(spell.Id),
            upgrades = ProgressionCosts("_spellUpgradeLevels", spell.Id, 1, 3, state.GetSpellUpgradeCost)
        }).ToArray();
        var wagon = BaseUpgradeCatalog.GetAll().Select(upgrade => new
        {
            id = upgrade.Id, name = upgrade.Title,
            upgrades = ProgressionCosts("_baseUpgradeLevels", upgrade.Id, 0, upgrade.MaxLevel, state.GetBaseUpgradeCost)
        }).ToArray();
        var output = new { schemaVersion = 1, units, spells, wagon };
        var path = ProjectSettings.GlobalizePath("res://artifacts/progression/economy-runtime.json");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(output,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        GD.Print($"PROGRESSION_EXPORT: {path}");
    }
}
