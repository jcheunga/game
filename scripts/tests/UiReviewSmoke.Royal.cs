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
        fixture.Gold = 1240; fixture.Food = 21; fixture.RelicShards = 18; fixture.Sigils = 8;
        fixture.HighestUnlockedStage = state.MaxStage;
        fixture.OwnedPlayerUnitIds = GameData.GetPlayerUnits().Select(u => u.Id).ToArray();
        fixture.OwnedPlayerSpellIds = GameData.GetPlayerSpells().Select(s => s.Id).ToArray();
        fixture.ActiveDeckUnitIds = new[] { "player_brawler", "player_shooter", "player_defender", "player_spear", "player_ranger", "player_marksman" };
        fixture.ActiveDeckSpellIds = GameData.GetPlayerSpells().Take(5).Select(s => s.Id).ToArray();
        fixture.OwnedEquipmentIds = new[] { "relic_iron_pendant", "relic_sharpened_edge", "relic_swift_boots", "relic_wolftooth_charm", "relic_spectral_lantern" };
        fixture.DiscoveredCodexIds = CodexCatalog.GetAll().Select(e => e.Id).ToArray();
        fixture.SeasonPassXP = 4350; fixture.HasPremiumPass = true;
        fixture.AdventureOpenTiles = GameData.Stages.SelectMany(s => AdventureTileCatalog.ForMap(s.MapId)).Where(t => !t.IsResource).Select(t => t.Id).Distinct().ToArray();
        typeof(GameState).GetMethod("ApplySavedData", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(state, new object[] { fixture });
        state.SetShowHints(false); state.SetAnalyticsConsent(false);
        await Open("MainMenu"); await Capture("01-home");
        ((MapMenu)GetTree().CurrentScene).OpenHomeDestination("more"); await Wait(.4);
        AuditText("Royal hub"); await Capture("02-caravan-hub");
        await Open("ShopMenu"); await PressHint("Archer"); AuditText("Royal warband"); await Capture("03-warband");
        await Press("Spells"); AuditText("Royal spells"); await Capture("04-spells");
        await Press("Relics"); await PressHint("Spectral Lantern"); AuditText("Royal relics"); await Capture("06-relics");
        await CheckRelicPage();
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

    /// <summary>Relic names fit their cards at the reading size, and the detail card lists every bonus.</summary>
    private async Task CheckRelicPage()
    {
        var root = LiveUiReview.ActiveRoot(GetTree());
        var cards = Walk(root).OfType<RoyalButton>().Where(b => GameData.GetAllEquipment().Any(r => r.DisplayName == b.AccessibilityName)).ToList();
        Check(cards.Count == GameData.GetAllEquipment().Count, $"Relic collection shows every relic ({cards.Count})");
        foreach (var card in cards)
        {
            var lines = card.GetChildren().OfType<RoyalLabel>().ToList();
            var fits = string.Join(" ", lines.Select(l => l.Text)) == card.AccessibilityName && lines.Count <= 2 && lines.All(l =>
            {
                var width = l.TextWidth(l.FittedSize());
                var left = l.Position.X + l.Align switch { HorizontalAlignment.Center => (l.Size.X - width) / 2, HorizontalAlignment.Right => l.Size.X - width, _ => 0 };
                return l.FittedSize() >= ReadingText.PhoneBody && left >= 4 && left + width <= card.Size.X - 4;
            });
            if (!fits) Check(false, $"Relic card name fits its card: {card.AccessibilityName}");
        }
        Check(!Walk(root).OfType<Button>().Any(b => b.IsVisibleInTree() && b.AccessibilityName == "View all bonuses"), "Relic page has no separate bonuses button");

        var richest = GameData.GetAllEquipment().MaxBy(r => RelicStatText.Stats(r).Count);
        await PressHint(richest.DisplayName);
        var list = Walk(LiveUiReview.ActiveRoot(GetTree())).OfType<ScrollContainer>().Single(s => s.Name == "RelicBonuses");
        var captions = Walk(list).OfType<RoyalLabel>().Select(l => l.Text).ToHashSet();
        Check(RelicStatText.Stats(richest).All(s => captions.Contains(s.Label)) && captions.Contains("Rarity"), $"{richest.DisplayName} lists all {RelicStatText.Stats(richest).Count} bonuses beside the collection");
        var bar = list.GetVScrollBar();
        Check(bar.MaxValue > bar.Page + 1, "More bonuses than the card holds scroll inside it");
        AuditText("Royal relic bonuses"); await Capture("06-relic-bonuses");
    }
}
