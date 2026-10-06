using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

public partial class UiReviewSmoke
{
    private async Task ReviewRoyalUi()
    {
        _output = ProjectSettings.GlobalizePath("res://artifacts/royal-ui/approved");
        System.IO.Directory.CreateDirectory(_output);
        var state = GameState.Instance; state.ResetProgress();
        var fixture = state.BuildSaveData();
        fixture.Gold = 1240; fixture.Food = 21; fixture.RelicShards = 18; fixture.Tomes = 12; fixture.Sigils = 8;
        fixture.HighestUnlockedStage = state.MaxStage;
        fixture.OwnedPlayerUnitIds = GameData.GetPlayerUnits().Select(u => u.Id).ToArray();
        fixture.OwnedPlayerSpellIds = GameData.GetPlayerSpells().Select(s => s.Id).ToArray();
        fixture.ActiveDeckUnitIds = new[] { "player_brawler", "player_shooter", "player_defender", "player_spear", "player_ranger", "player_marksman" };
        fixture.ActiveDeckSpellIds = GameData.GetPlayerSpells().Take(5).Select(s => s.Id).ToArray();
        fixture.OwnedEquipmentIds = new[] { "relic_iron_pendant", "relic_sharpened_edge", "relic_swift_boots", "relic_wolftooth_charm", "relic_spectral_lantern" };
        fixture.DiscoveredCodexIds = CodexCatalog.GetAll().Select(e => e.Id).ToArray();
        fixture.SeasonPassXP = 4350; fixture.HasPremiumPass = true;
        fixture.AdventureOpenTiles = GameData.Stages.SelectMany(s => AdventureTileCatalog.ForMap(s.MapId)).Where(t => t.HasInterest).Select(t => t.Id).Distinct().ToArray();
        typeof(GameState).GetMethod("ApplySavedData", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(state, new object[] { fixture });
        state.SetShowHints(false); state.SetAnalyticsConsent(false);
        await Open("MainMenu"); await Capture("01-home");
        ((MapMenu)GetTree().CurrentScene).OpenHomeDestination("more"); await Wait(.4);
        AuditText("Royal hub"); await Capture("02-caravan-hub");
        await Open("ShopMenu"); await PressHint("Archer"); AuditText("Royal warband"); await Capture("03-warband");
        await Press("Spells"); AuditText("Royal spells"); await Capture("04-spells");
        await Press("Relics"); await PressHint("Spectral Lantern"); AuditText("Royal relics"); await Capture("06-relics");
        await Press("War wagon"); AuditText("Royal wagon"); await Capture("05-wagon");
        var before = state.Gold;
        await Open("ForgeMenu"); AuditText("Royal forge populated"); await Capture("07-forge");
        await Press("Dismantle");
        Check(!state.GetOwnedEquipment().Contains("relic_iron_pendant"), "Royal dismantle selection consumes the chosen relic");
        await PressHint("Iron Pendant"); await Press("Craft");
        Check(state.GetOwnedEquipment().Contains("relic_iron_pendant") && state.Gold < before, "Royal craft selection grants the recipe and spends gold");
        foreach (var id in new[] { "relic_iron_pendant", "relic_sharpened_edge", "relic_swift_boots" })
        {
            var name = GameData.GetEquipment(id).DisplayName;
            await PressHint(name);
            await PressHint("Place " + name);
        }
        await Press("Fuse");
        Check(new[] { "relic_iron_pendant", "relic_sharpened_edge", "relic_swift_boots" }.All(id => !state.GetOwnedEquipment().Contains(id)), "Royal fuse sockets consume the selected three relics");
        foreach (var (scene, name) in new[] { ("CodexMenu", "08-codex"), ("SettingsMenu", "09-settings"), ("EndlessMenu", "10-survival"), ("MultiplayerMenu", "11-challenges"), ("SeasonPassMenu", "12-season"), ("LoadoutMenu", "13-preparation") })
        { await Open(scene); AuditText("Royal " + scene); await Capture(name); }
        state.PrepareCampaignBattle(); await Open("Battle"); await Capture("14-battle"); AuditText("Royal battle");
        var battle = (BattleController)GetTree().CurrentScene;
        typeof(BattleController).GetMethod("EndBattle", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(battle, new object[] { true });
        await Wait(.4); AuditText("Royal victory"); await Capture("15-victory");
        GD.Print($"ROYAL_UI_RESULT: {_failures} failures"); QuitAfterAudio(_failures == 0 ? 0 : 1);
    }
}
