using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// Captures every concept screen through production navigation, with a save shaped like the concept
/// mock-ups, into artifacts/royal-ui/capture for comparison with art/royal/ref (art/royal/compare.py).
///   --concepts [--only=warband,spells]
/// </summary>
public partial class UiReviewSmoke
{
    private static readonly string[] ConceptScreens = { "home", "hub", "warband", "spells", "wagon", "relics", "achievements", "codex", "settings",
        "endless", "multiplayer", "forge", "season", "preparation", "battle", "victory" };

    private void SeedConceptSave()
    {
        var state = GameState.Instance; state.ResetProgress();
        var fixture = state.BuildSaveData();
        fixture.Gold = 1240; fixture.Food = 21; fixture.RelicShards = 18; fixture.Sigils = 8;
        fixture.HighestUnlockedStage = 12;
        fixture.OwnedPlayerUnitIds = GameData.GetPlayerUnits().Where(u => u.UnlockStage <= 12).Select(u => u.Id).ToArray();
        fixture.OwnedPlayerSpellIds = GameData.GetPlayerSpells().Select(s => s.Id).ToArray();
        fixture.ActiveDeckUnitIds = new[] { "player_brawler", "player_ranger", "player_shooter", "player_defender", "player_spear" };
        fixture.ActiveDeckSpellIds = new[] { "spell_fireball", "spell_barrier_ward", "spell_heal", "spell_lightning_strike" };
        fixture.OwnedEquipmentIds = new[] { "relic_iron_pendant", "relic_swift_boots", "relic_wolftooth_charm", "relic_spectral_lantern", "relic_sages_ring",
            "relic_windrunner_cloak", "relic_crown_of_valor", "relic_moonfire_talisman", "relic_tower_ascendant" };
        fixture.DiscoveredCodexIds = CodexCatalog.GetAll().Select(e => e.Id).ToArray();
        // King's Road explored up to its sixth stage, as on the atlas concept.
        var road = AdventureTileCatalog.ForMap("city");
        fixture.AdventureOpenTiles = road.Where(t => t.Site?.Kind != AdventureSiteKind.Leader || t.Site.Stage <= 7).Select(t => t.Id).ToArray();
        fixture.StageStars = Enumerable.Range(1, state.MaxStage).Select(stage => stage <= 6 ? (stage % 3 == 0 ? 2 : 3) : 0).ToArray();
        // Tier 8 with 750 of 900 XP toward tier 9, and tier 7 already claimed, as in the season concept.
        fixture.SeasonPassXP = SeasonPassCatalog.GetXPForTier(8) + 750 * (SeasonPassCatalog.GetXPForTier(9) - SeasonPassCatalog.GetXPForTier(8)) / 900;
        fixture.SeasonPassTier = 8; fixture.HasPremiumPass = true;
        fixture.UnlockedAchievementIds = new[] { "first_blood", "boss_slayer", "no_damage", "all_spells" };
        fixture.ClaimedAchievementRewardIds = new[] { "boss_slayer", "no_damage", "all_spells" };
        fixture.ClaimedSeasonFreeTiers = Enumerable.Range(1, 7).ToArray(); fixture.ClaimedSeasonPremiumTiers = Enumerable.Range(1, 7).ToArray();
        typeof(GameState).GetMethod("ApplySavedData", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(state, new object[] { fixture });
        state.SetShowHints(false); state.SetAnalyticsConsent(false);
    }

    private async Task ReviewConcepts()
    {
        _output = ProjectSettings.GlobalizePath("res://artifacts/royal-ui/capture");
        System.IO.Directory.CreateDirectory(_output);
        var only = OS.GetCmdlineUserArgs().FirstOrDefault(arg => arg.StartsWith("--only="))?["--only=".Length..].Split(',');
        bool Want(string name) => only == null || only.Contains(name);
        SeedConceptSave();
        if (Want("home")) { await Open("MainMenu"); await Capture("home"); }
        if (Want("hub")) { await Open("MainMenu"); ((MapMenu)GetTree().CurrentScene).OpenHomeDestination("more"); await Wait(.5); await Capture("hub"); }
        if (Want("hub-tabs"))
        {
            await Open("MainMenu"); ((MapMenu)GetTree().CurrentScene).OpenHomeDestination("more"); await Wait(.5);
            await Press("Caravan"); await Capture("hub-caravan"); await Press("Community"); await Capture("hub-community");
        }
        if (Want("achievements")) { await Open("MainMenu"); ((MapMenu)GetTree().CurrentScene).OpenHomeDestination("achievements"); await Wait(.5); await Capture("achievements"); }
        if (new[] { "warband", "spells", "wagon", "relics" }.Any(Want))
        {
            await Open("ShopMenu");
            if (Want("warband")) { await PressHint("Archer"); await Capture("warband"); }
            if (Want("spells")) { await Press("Spells"); await PressHint("Fireball"); await Capture("spells"); }
            if (Want("relics")) { await Press("Relics"); await PressHint("Spectral Lantern"); await Capture("relics"); }
            if (Want("wagon")) { await Press("War wagon"); await Capture("wagon"); }
        }
        foreach (var (scene, name) in new[] { ("CodexMenu", "codex"), ("SettingsMenu", "settings"), ("EndlessMenu", "endless"), ("MultiplayerMenu", "multiplayer"),
            ("ForgeMenu", "forge"), ("SeasonPassMenu", "season"), ("LoadoutMenu", "preparation") })
            if (Want(name)) { await Open(scene); await Capture(name); }
        if (Want("battle") || Want("victory") || Want("pause") || Want("battle-action"))
        {
            GameState.Instance.SetSelectedStage(1);
            GameState.Instance.PrepareCampaignBattle(); await Open("Battle"); await Wait(1.5);
            if (Want("battle")) await Capture("battle");
            if (Want("battle-action"))
            {
                var fight = (BattleController)GetTree().CurrentScene;
                typeof(BattleController).GetField("_courage", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(fight, 400f);
                var deploy = typeof(BattleController).GetMethod("DeployPlayerUnit", BindingFlags.NonPublic | BindingFlags.Instance)!;
                foreach (var id in new[] { "player_defender", "player_shooter", "player_brawler", "player_shooter" })
                {
                    typeof(BattleController).GetField("_courage", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(fight, 400f);
                    deploy.Invoke(fight, new object[] { GameData.GetUnit(id) }); await Wait(1.2);
                }
                await Wait(9); await Capture("battle-action");
            }
            if (Want("pause"))
            {
                var fight = (BattleController)GetTree().CurrentScene;
                typeof(BattleController).GetMethod("TogglePause", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(fight, null);
                await Wait(.5); await Capture("pause");
                typeof(BattleController).GetMethod("TogglePause", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(fight, null);
                await Wait(.2);
            }
            if (Want("victory"))
            {
                var battle = (BattleController)GetTree().CurrentScene;
                typeof(BattleController).GetMethod("EndBattle", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(battle, new object[] { true });
                await Wait(.8); await Capture("victory");
            }
            if (Want("defeat"))
            {
                await Open("Battle"); await Wait(1);
                var lost = (BattleController)GetTree().CurrentScene;
                typeof(BattleController).GetMethod("EndBattle", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(lost, new object[] { false });
                await Wait(.8); await Capture("defeat");
            }
        }
        var zones = OS.GetCmdlineUserArgs().FirstOrDefault(arg => arg.StartsWith("--zones="))?["--zones=".Length..].Split(',');
        if (zones != null)
        {
            var state = GameState.Instance;
            var save = state.BuildSaveData(); save.HighestUnlockedStage = state.MaxStage;
            typeof(GameState).GetMethod("ApplySavedData", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(state, new object[] { save });
            foreach (var zone in zones)
            {
                var stage = GameData.Stages.First(s => RouteCatalog.Normalize(s.MapId) == zone).StageNumber;
                state.SetSelectedStage(stage); state.PrepareCampaignBattle();
                await Open("Battle"); await Wait(1.2); await Capture("battle-" + zone);
            }
        }
        GD.Print($"CONCEPTS_RESULT: {_failures} failures"); QuitAfterAudio(0);
    }
}
