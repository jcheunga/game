using System;
using System.Linq;
using Godot;

/// <summary>
/// Developer mode (debug builds only): testing supplies, wave versions, battle speed and progression shortcuts.
/// Every tool is refused while the mode is off, and the wave version and battle speed only apply while it is on,
/// so isolated test saves and release builds always play the standard campaign at normal speed.
/// </summary>
public partial class GameState
{
    public static readonly float[] DeveloperBattleSpeeds = { 1f, 2f, 4f, 8f };

    public bool DeveloperModeEnabled { get; private set; }
    public static bool DeveloperModeAvailable => OS.IsDebugBuild();
    public bool DeveloperToolsActive => DeveloperModeAvailable && DeveloperModeEnabled;
    public event Action DeveloperStateChanged;

    /// <summary>The wave version chosen in developer tools; it plays only while developer mode is on.</summary>
    public string DeveloperWaveVersionId { get; private set; } = WaveVersions.StandardId;
    public string ActiveWaveVersionId => DeveloperToolsActive ? DeveloperWaveVersionId : WaveVersions.StandardId;
    public float DeveloperBattleSpeed { get; private set; } = 1f;
    /// <summary>Engine time scale for battles: the developer speed while the mode is on, otherwise 1.</summary>
    public float BattleSpeedScale => DeveloperToolsActive ? DeveloperBattleSpeed : 1f;
    /// <summary>The first campaign stage without a star, or MaxStage + 1 once every stage is won.</summary>
    public int NextUnbeatenStage => Enumerable.Range(1, MaxStage).FirstOrDefault(stage => GetStageStars(stage) <= 0, MaxStage + 1);

    private bool _developerFastForward;

    public void SetDeveloperMode(bool enabled)
    {
        DeveloperModeEnabled = enabled && DeveloperModeAvailable;
        WaveVersions.Apply(ActiveWaveVersionId);
        Persist();
        DeveloperStateChanged?.Invoke();
    }

    public void SetDeveloperWaveVersion(string id)
    {
        if (!DeveloperToolsActive || WaveVersions.Find(id) is not { } version) return;
        DeveloperWaveVersionId = version.Id;
        WaveVersions.Apply(ActiveWaveVersionId);
        Persist();
        DeveloperStateChanged?.Invoke();
    }

    public void SetDeveloperBattleSpeed(float speed)
    {
        if (!DeveloperToolsActive || !DeveloperBattleSpeeds.Contains(speed)) return;
        DeveloperBattleSpeed = speed;
        Persist();
        DeveloperStateChanged?.Invoke();
    }

    private void LoadDeveloperOptions(GameSaveData saved)
    {
        DeveloperWaveVersionId = WaveVersions.Find(saved.DeveloperWaveVersionId)?.Id ?? WaveVersions.StandardId;
        DeveloperBattleSpeed = DeveloperBattleSpeeds.Contains(saved.DeveloperBattleSpeed) ? saved.DeveloperBattleSpeed : 1f;
        WaveVersions.Apply(ActiveWaveVersionId);
    }

    private void ResetDeveloperOptions()
    {
        DeveloperWaveVersionId = WaveVersions.StandardId;
        DeveloperBattleSpeed = 1f;
        WaveVersions.Apply(WaveVersions.StandardId);
    }

    /// <summary>Local testing grants bypass billing, purchase counts and progression rewards.</summary>
    public bool TryAddDeveloperResources(int gold, int food)
    {
        if (!DeveloperToolsActive || gold < 0 || food < 0
            || (gold == 0 && food == 0) || gold > int.MaxValue - Gold || food > int.MaxValue - Food)
            return false;

        Gold += gold;
        Food += food;
        Persist();
        if (food > 0) FoodChanged?.Invoke();
        DeveloperStateChanged?.Invoke();
        return true;
    }

    /// <summary>Adds Essence, relic shards or sigils.</summary>
    public bool TryAddDeveloperCurrency(string currency, int amount)
    {
        if (!DeveloperToolsActive || amount <= 0) return false;
        switch (currency)
        {
            case "essence" when amount <= int.MaxValue - Essence: Essence += amount; break;
            case "shards" when amount <= int.MaxValue - RelicShards: RelicShards += amount; break;
            case "sigils" when amount <= int.MaxValue - Sigils: Sigils += amount; break;
            default: return false;
        }
        Persist();
        DeveloperStateChanged?.Invoke();
        return true;
    }

    /// <summary>Raises the player level by whole levels (or to the top), leaving each level's reward to claim.</summary>
    public bool TryDeveloperLevelUp(int levels, out string message)
    {
        message = "";
        if (!DeveloperToolsActive || levels <= 0) return false;
        if (IsMaxPlayerLevel) { message = $"Already at level {PlayerLevelCatalog.MaxLevel}."; return false; }
        var target = Math.Min(PlayerLevelCatalog.MaxLevel, PlayerLevel + levels);
        AddPlayerXP(PlayerLevelCatalog.TotalXPForLevel(target) - PlayerXP);
        message = $"Player level {PlayerLevel} · {ClaimableLevelRewardCount} rewards to claim";
        DeveloperStateChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Wins every unbeaten campaign stage up to <paramref name="lastStage"/>, in order, with three stars, the stage's
    /// gold and food and its battle XP: the map, zones, boss gates and unlocks advance exactly as if each was played.
    /// </summary>
    public bool TryDeveloperWinStages(int lastStage, out string message)
    {
        message = "";
        if (!DeveloperToolsActive) return false;
        lastStage = Math.Min(lastStage, MaxStage);
        var first = NextUnbeatenStage;
        if (first > lastStage) { message = "Those stages are already won."; return false; }
        var mode = CurrentBattleMode;
        var won = 0;
        _developerFastForward = true;
        try
        {
            CurrentBattleMode = BattleRunMode.Campaign;
            for (var stage = first; stage <= lastStage; stage++)
            {
                if (GetStageStars(stage) > 0) continue;
                var data = GameData.GetStage(stage);
                AwardBattleXP(BattleRunMode.Campaign, true, stage, 0);
                ApplyVictory(stage, data.RewardGold, data.RewardFood, 3);
                won++;
            }
        }
        finally
        {
            _developerFastForward = false;
            CurrentBattleMode = mode;
        }
        SelectedStage = Math.Min(MaxStage, NextUnbeatenStage);
        LastResultMessage = message = $"Won {won} stage{(won == 1 ? "" : "s")} ({first}–{lastStage}) · player level {PlayerLevel}";
        Persist();
        FoodChanged?.Invoke();
        DeveloperStateChanged?.Invoke();
        return true;
    }

    /// <summary>The boss stage that ends the zone holding the next unbeaten stage.</summary>
    public int DeveloperZoneEndStage()
    {
        var next = Math.Min(MaxStage, NextUnbeatenStage);
        return GameData.GetStagesForMap(GameData.GetStage(next).MapId).Max(stage => stage.StageNumber);
    }

    /// <summary>
    /// Opens every zone and charts its plain ground, so each stage, cache and find sits on the open frontier. Nothing
    /// is won or gathered; boss gates still wait for their zone's leaders.
    /// </summary>
    public bool TryDeveloperOpenAllZones(out string message)
    {
        message = "";
        if (!DeveloperToolsActive) return false;
        var opened = 0;
        foreach (var map in GameData.Stages.Select(stage => stage.MapId).Distinct())
            foreach (var tile in AdventureTileCatalog.ForMap(map).Where(tile => !tile.HasInterest))
            {
                if (_openedAdventureTiles.Add(tile.Id)) opened++;
                _reachedAdventureTiles.Add(tile.Id);
            }
        AdventureKnowledgeRevision++;
        message = $"Every zone is open · {opened} tiles charted";
        Persist();
        DeveloperStateChanged?.Invoke();
        return true;
    }

    /// <summary>Owns every recruit, spell and tactical tool the shop offers.</summary>
    public bool TryDeveloperUnlockWarband(out string message)
    {
        message = "";
        if (!DeveloperToolsActive) return false;
        var added = 0;
        foreach (var unit in GameData.GetPlayerUnits().Where(unit => !unit.LegacyRecruit))
            if (_ownedPlayerUnitIds.Add(unit.Id)) { DiscoverCodexEntry(unit.Id); added++; }
        foreach (var spell in GameData.GetPlayerSpells())
            if (_ownedPlayerSpellIds.Add(spell.Id)) { DiscoverCodexEntry(spell.Id); added++; }
        message = added == 0 ? "The whole warband was already recruited." : $"Recruited {added} units, spells and tools";
        Persist();
        CheckAchievements();
        DeveloperStateChanged?.Invoke();
        return added > 0;
    }

    /// <summary>Trains owned units to the player level's cap, spells to their top level and the war wagon to its rank cap.</summary>
    public bool TryDeveloperTrainWarband(out string message)
    {
        message = "";
        if (!DeveloperToolsActive) return false;
        var unitLevel = Math.Min(MaxPlayerUnitLevel, UnitLevelCap);
        foreach (var id in _ownedPlayerUnitIds.ToArray())
            _unitUpgradeLevels[id] = Math.Max(GetUnitLevel(id), unitLevel);
        foreach (var id in _ownedPlayerSpellIds.ToArray())
            _spellUpgradeLevels[id] = MaxPlayerSpellLevel;
        foreach (var upgrade in BaseUpgradeCatalog.GetAll())
            _baseUpgradeLevels[upgrade.Id] = Math.Max(GetBaseUpgradeLevel(upgrade.Id), Math.Min(upgrade.MaxLevel, WagonRankCap));
        message = $"Units at level {unitLevel} · spells at level {MaxPlayerSpellLevel} · wagon at rank {WagonRankCap}";
        Persist();
        CheckAchievements();
        DeveloperStateChanged?.Invoke();
        return true;
    }

    /// <summary>Moves the food recharge and running expeditions forward, as if the time had passed.</summary>
    public bool TryDeveloperSkipTime(int seconds, out string message)
    {
        message = "";
        if (!DeveloperToolsActive || seconds <= 0) return false;
        if (FoodRechargedAtUnixSeconds > 0) FoodRechargedAtUnixSeconds -= seconds;
        foreach (var slot in _activeExpeditions) slot.StartedAtUnixSeconds -= seconds;
        RefreshFoodRecharge();
        message = $"Skipped {seconds / 3600}h · food {Food} / {FoodRechargeCap}";
        Persist();
        FoodChanged?.Invoke();
        DeveloperStateChanged?.Invoke();
        return true;
    }
}
