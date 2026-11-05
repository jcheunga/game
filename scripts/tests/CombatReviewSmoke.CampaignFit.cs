using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;

// --campaign-fit: how much stronger a campaign stage's enemies can be before players at different investment
// levels stop beating it. Uses the playthrough sim's scripted player and squad choice.
public partial class CombatReviewSmoke
{
    /// <summary>
    /// --campaign-fit [--stages=10,30,50] [--profiles=lag,cap,grind]: for each stage and player, the strongest campaign
    /// strength s (CampaignPacing: enemy health ×(1 + s), damage ×(1 + s/2)) that the player beats on two seeds,
    /// searched from --guess (default 1) between −0.5 and 4. Each battle prints a CAMPAIGN_TRY line. Players have every troop unlocked by the stage and the XP of first clears
    /// before it: "lag" trains troops 4 levels under the cap, the wagon 2 ranks under, spells at level 1 and no relics;
    /// "cap" trains everything to the caps, spells to 3 and wears the milestone relics; "grind" is "cap" eight player
    /// levels higher (four more troop levels). Prints CAMPAIGN_FIT lines.
    /// </summary>
    private async Task FitCampaignStrength()
    {
        var stages = SimArg("stages", "5,10,20,30,40,50,60,70,80,90,100").Split(',').Select(int.Parse).ToArray();
        var profiles = SimArg("profiles", "lag,cap,grind").Split(',');
        var state = GameState.Instance;
        foreach (var number in stages)
        {
            foreach (var profile in profiles)
            {
                async Task<bool> Beats(float s)
                {
                    CampaignPacing.StrengthOverride = n => n == number ? s : CampaignPacing.EnemyStrengthFromTable(n);
                    for (var seed = 0; seed < 2; seed++)
                    {
                        SeedCampaignPlayer(number, profile);
                        var wall = Time.GetTicksMsec();
                        var (won, seconds, hull, gate) = await SimFitBattle(number, (ulong)(8000 + number * 10 + seed));
                        wall = Time.GetTicksMsec() - wall;
                        GD.Print($"CAMPAIGN_TRY: {{\"stage\":{number},\"profile\":\"{profile}\",\"s\":{s:0.000},\"seed\":{seed},\"won\":{(won ? "true" : "false")},\"seconds\":{seconds:0.0},\"hull\":{hull:0.00},\"gate\":{gate:0.00},\"wall\":{wall / 1000.0:0.0}}}");
                        if (!won) return false;
                    }
                    return true;
                }
                // Bracket from a guess in steps of half a point, then halve the bracket --steps times (4: to 0.03).
                var guess = float.Parse(SimArg("guess", "1"), System.Globalization.CultureInfo.InvariantCulture);
                var (low, high, beaten, ceiling) = (guess, guess, true, false);
                if (await Beats(guess))
                {
                    high = guess + .5f;
                    while (high <= 4f && await Beats(high)) { low = high; high += .5f; }
                    ceiling = high > 4f;
                }
                else
                {
                    low = guess - .5f;
                    while (low >= -.5f && !await Beats(low)) { high = low; low -= .5f; }
                    beaten = low >= -.5f;
                }
                if (beaten && !ceiling)
                    for (var step = 0; step < int.Parse(SimArg("steps", "4")); step++)
                    {
                        var mid = (low + high) / 2f;
                        if (await Beats(mid)) low = mid; else high = mid;
                    }
                var plain = low >= 0f;
                var lowest = beaten;
                CampaignPacing.StrengthOverride = null;
                SeedCampaignPlayer(number, profile);
                GD.Print($"CAMPAIGN_FIT: {JsonSerializer.Serialize(new { stage = number, profile, beatsPlain = plain, beatsLowest = lowest, strength = MathF.Round(low, 3), level = state.PlayerLevel, unitLevel = state.ActiveDeckUnitIds.Max(state.GetUnitLevel), wagon = state.GetBaseUpgradeLevel(BaseUpgradeCatalog.HullPlatingId), deck = state.ActiveDeckUnitIds.ToArray(), spells = state.ActiveDeckSpellIds.ToArray() })}");
            }
        }
    }

    private async Task<(bool Won, float Seconds, float Hull, float Gate)> SimFitBattle(int stage, ulong seed)
    {
        var state = GameState.Instance;
        await LiveUiReview.StopAudio(this);
        state.SetSelectedStage(stage);
        state.PrepareCampaignBattle();
        var battle = GD.Load<PackedScene>("res://scenes/Battle.tscn").Instantiate<BattleController>();
        AddChild(battle);
        battle.SetPhysicsProcess(false);
        Read<RandomNumberGenerator>(battle, "_rng").Seed = seed;
        Invoke(battle, "InitializeContent", seed);
        Invoke(battle, "InitializeCourageDrops", seed);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        return await SimPlay(battle);
    }

    /// <summary>A player about to fight <paramref name="stage"/> for the first time, at the given investment.</summary>
    private void SeedCampaignPlayer(int stage, string profile)
    {
        var state = GameState.Instance;
        state.ResetProgress();
        var save = state.BuildSaveData();
        var xp = Enumerable.Range(1, stage - 1).Sum(s => PlayerLevelCatalog.CampaignXP(s, true, true));
        if (profile == "grind")
            xp = PlayerLevelCatalog.TotalXPForLevel(Math.Min(PlayerLevelCatalog.MaxLevel, PlayerLevelCatalog.LevelForXP(xp) + 8));
        var level = PlayerLevelCatalog.LevelForXP(xp);
        var cap = PlayerLevelCatalog.UnitLevelCap(level);
        var rank = Math.Min(5, PlayerLevelCatalog.WagonRankCap(level));
        var lag = profile == "lag";
        var units = GameData.GetPlayerUnits().Where(u => u.UnlockStage <= stage && u.UnlockStage <= GameData.MaxStage).ToArray();
        var spells = SimSpellPlan.Where(id => GameData.GetSpell(id).UnlockStage <= stage).ToArray();
        save.HighestUnlockedStage = stage;
        save.StageStars = Enumerable.Range(1, GameData.MaxStage).Select(s => s < stage ? 1 : 0).ToArray();
        save.PlayerXP = xp;
        save.ClaimedLevelRewards = Enumerable.Range(1, level).ToArray();
        save.OwnedPlayerUnitIds = units.Select(u => u.Id).ToArray();
        save.UnitLevels = units.ToDictionary(u => u.Id, _ => lag ? Math.Max(1, cap - 4) : cap);
        save.OwnedPlayerSpellIds = spells;
        save.SpellLevels = spells.ToDictionary(id => id, _ => lag ? 1 : 3);
        save.BaseUpgradeLevels = SimWagonPriority.ToDictionary(id => id, _ => lag ? Math.Max(0, rank - 2) : rank);
        save.OwnedEquipmentIds = lag ? Array.Empty<string>()
            : CampaignProgressionCatalog.GetAll().Where(m => m.Stage < stage && !string.IsNullOrEmpty(m.RelicId)).Select(m => m.RelicId).ToArray();
        save.UnitEquipmentSlots = new();
        Invoke(state, "ApplySavedData", save);
        SimPickDeck(state);
        SimEquipRelics(state);
    }
}
