using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

public partial class UiReviewSmoke
{
    private async Task ReviewEnchantments()
    {
        var originalMobile=MobilePresentation.TestOverride;
        try
        {
            foreach(var phone in new[]{false,true})
            {
                MobilePresentation.TestOverride=phone;GetWindow().Size=phone?new Vector2I(844,390):new Vector2I(1280,720);
                _output=ProjectSettings.GlobalizePath("res://artifacts/enchantments/"+(phone?"phone":"desktop"));System.IO.Directory.CreateDirectory(_output);
                var state=GameState.Instance;state.ResetProgress();state.SetAnalyticsConsent(false);state.SetShowHints(false);
                var fixture=state.BuildSaveData();fixture.Gold=5000;fixture.Essence=10;fixture.HighestUnlockedStage=100;
                fixture.OwnedPlayerUnitIds=GameData.PlayerRosterIds;fixture.UnitLevels=GameData.PlayerRosterIds.ToDictionary(id=>id,_=>20);
                fixture.PromotedUnitIds=new[]{"player_brawler"};fixture.ActiveDeckUnitIds=new[]{"player_brawler","player_shooter"};
                fixture.OwnedEquipmentIds=new[]{"relic_guardian_shield","relic_tower_charm","relic_sharpened_edge"};
                fixture.UnitEquipmentSlots=new() {{"player_brawler","relic_guardian_shield"}};
                fixture.UnitEquipmentSlot2=new() {{"player_brawler","relic_tower_charm"}};
                fixture.RelicEnchantments=new() {{"relic_guardian_shield","ench_thorns"}};
                void Restore(GameSaveData save)=>typeof(GameState).GetMethod("ApplySavedData",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(state,new object[]{save});
                Restore(fixture);await Open("MainMenu");
                var menu=(MapMenu)GetTree().CurrentScene;var map=Walk(menu).OfType<MapPathCanvas>().Single().MapOffset;var food=state.Food;
                SceneRouter.Instance.GoToForge();await Wait(.3);
                async Task Tap(string name)
                {
                    var button=Walk(menu).OfType<Button>().Last(b=>b.IsVisibleInTree() && !b.Disabled && (b.AccessibilityName==name || b.TooltipText==name || b.Text==name));
                    await TapModal(button);await Wait(.15);
                }
                ForgeEnchantmentInspector Inspector()=>Walk(menu).OfType<ForgeEnchantmentInspector>().Single();
                await Tap("Tower Charm");
                AuditText("Enchantments / Forge entry");await Capture("01-forge-entry");
                await Tap("Enchantments");
                Check(Walk(Inspector()).OfType<Button>().Count(b=>b.AccessibilityName.StartsWith("Preview enchantment "))==10,"Forge exposes all ten enchantment choices");
                Check(Walk(Inspector()).OfType<Label>().Any(l=>l.Text.Contains("Essence: 10")),"Forge enchantment screen displays the available Essence balance");
                AuditText("Enchantments / choices");await Capture("02-choices");
                var gold=state.Gold;var essence=state.Essence;
                await Tap("Preview enchantment Crit Strike");
                var preview=Inspector().Preview;var wearer=preview.Wearers.Single();
                Check(wearer.Slot==2 && wearer.Unit.Id=="player_brawler" && wearer.After.CriticalChance>wearer.Current.CriticalChance,"Enchantment choice previews the correct second-slot wearer's critical stats");
                Check(state.Gold==gold && state.Essence==essence && state.GetRelicEnchantment("relic_tower_charm")==null,"Selection and preview spend nothing and apply no effect");
                var cost=Walk(Inspector()).OfType<Label>().Single(l=>l.Name=="EnchantmentCost");
                var apply=Walk(Inspector()).OfType<Button>().Single(b=>b.Text=="Apply enchantment");
                Check(cost.Text=="Cost: 220 gold + 2 Essence" && GetViewport().GetVisibleRect().Encloses(cost.GetGlobalRect()) && GetViewport().GetVisibleRect().Encloses(apply.GetGlobalRect()),"Price and confirmation remain visible while the stat comparison scrolls");
                AuditText("Enchantments / comparison");await Capture("03-critical-preview");
                await Tap("Apply enchantment");
                Check(!Walk(menu).OfType<ForgeEnchantmentInspector>().Any() && state.Gold==gold-220 && state.Essence==essence-2 && state.GetRelicEnchantmentId("relic_tower_charm")=="ench_crit_strike","Native Forge confirmation pays once and applies the selected enchantment");
                Check(state.BuildPlayerUnitStats(GameData.GetUnit("player_brawler")).CriticalChance==wearer.After.CriticalChance,"Confirmed enchantment matches the previewed battle stats");
                gold=state.Gold;essence=state.Essence;
                Check(!state.TryApplyEnchantment("relic_tower_charm","ench_crit_strike",out _) && state.Gold==gold && state.Essence==essence,"Repeated application cannot charge the wallet again");
                state.ReloadFromDisk();
                Check(state.GetRelicEnchantmentId("relic_tower_charm")=="ench_crit_strike","Native enchantment application survives save reload");
                await Tap("Enchantments");await Tap("Preview enchantment Fortify");
                Check(Inspector().Preview.Current.Id=="ench_crit_strike" && Inspector().Preview.Wearers.Single().After.Speed<Inspector().Preview.Wearers.Single().Current.Speed,"Replacing an enchantment previews its speed tradeoff and old effect");
                Check(Inspector().Preview.Wearers.Single().Limits.Any(l=>l.Label=="Armor" && l.Excess>0),"Forge explains enchantment bonuses exceeding the equipment cap");
                AuditText("Enchantments / shared limit");await Capture("04-replacement-limit");
                await Tap("Apply enchantment");
                Check(state.Gold==gold-180 && state.Essence==essence-2 && state.GetRelicEnchantmentId("relic_tower_charm")=="ench_fortify","Replacement pays the new cost and stores one enchantment");
                await Tap("Enchantments");await Tap("Remove enchantment");
                Check(Inspector().Preview.Candidate==null && Inspector().Preview.Wearers.Single().After.Speed>Inspector().Preview.Wearers.Single().Current.Speed,"Removal has an explicit before-and-after comparison");
                AuditText("Enchantments / removal");await Capture("05-removal");
                gold=state.Gold;essence=state.Essence;await Tap("Confirm removal");
                Check(state.GetRelicEnchantment("relic_tower_charm")==null && state.Gold==gold && state.Essence==essence && state.GetUnitEquipment2("player_brawler")?.Id=="relic_tower_charm","Native removal is free and preserves the equipped relic");
                var poor=state.BuildSaveData();poor.Essence=0;Restore(poor);
                await Tap("Enchantments");await Tap("Preview enchantment Haste");
                Check(!Inspector().Preview.CanApply && Inspector().Preview.Reason.Contains("Essence") && Walk(Inspector()).OfType<Button>().Single(b=>b.Text=="Apply enchantment").Disabled,"Insufficient Essence disables confirmation with a clear reason");
                AuditText("Enchantments / insufficient Essence");await Capture("06-affordability");
                await Tap("Close details");
                await Tap("Sharpened Edge");await Tap("Enchantments");await Tap("Preview enchantment Lifesteal");
                Check(Inspector().Preview.Wearers.Count==0 && Walk(Inspector()).OfType<Label>().Any(l=>l.Text.StartsWith("Stored in your collection")),"Stored relics show their future effect without inventing a current unit stat change");
                AuditText("Enchantments / stored relic");await Capture("07-stored-relic");await Tap("Close details");
                Check(state.Food==food && Walk(menu).OfType<MapPathCanvas>().Single().MapOffset==map,"Enchanting and inspecting preserve travel supplies and map position");
                menu.CloseHomeModal();
                var empty=state.BuildSaveData();empty.OwnedEquipmentIds=Array.Empty<string>();empty.UnitEquipmentSlots.Clear();empty.UnitEquipmentSlot2.Clear();empty.RelicEnchantments.Clear();Restore(empty);
                SceneRouter.Instance.GoToForge();await Wait(.2);await Tap("Enchantments");
                Check(Inspector().Preview==null && Walk(Inspector()).OfType<Label>().Any(l=>l.Text=="No relics yet"),"A new collection receives a useful empty-state message");
                AuditText("Enchantments / empty collection");await Capture("08-empty-collection");await Tap("Back to Forge");menu.CloseHomeModal();
            }
        }
        finally {MobilePresentation.TestOverride=originalMobile;}
        GD.Print($"ENCHANTMENT_REVIEW_RESULT: {_failures} failures");QuitAfterAudio(_failures==0?0:1);
    }
}
