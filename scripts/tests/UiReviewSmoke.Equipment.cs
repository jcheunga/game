using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

public partial class UiReviewSmoke
{
    private async Task ReviewEquipmentPreview()
    {
        foreach(var phone in new[]{false,true})
        {
            GetWindow().Size=phone?new Vector2I(844,390):new Vector2I(1280,720);
            _output=ProjectSettings.GlobalizePath("res://artifacts/equipment-preview/"+(phone?"phone":"desktop"));
            System.IO.Directory.CreateDirectory(_output);
            var state=GameState.Instance;state.ResetProgress();state.SetShowHints(false);state.SetAnalyticsConsent(false);
            var fixture=state.BuildSaveData();fixture.Gold=100000;fixture.Essence=100;
            fixture.HighestUnlockedStage=100;fixture.OwnedPlayerUnitIds=GameData.PlayerRosterIds;
            fixture.UnitLevels=GameData.PlayerRosterIds.ToDictionary(id=>id,_=>20);
            fixture.ActiveDeckUnitIds=new[]{"player_brawler","player_shooter"};
            fixture.PromotedUnitIds=new[]{"player_brawler"};
            fixture.OwnedEquipmentIds=new[]{"relic_sharpened_edge","relic_guardian_shield","relic_tower_charm"};
            typeof(GameState).GetMethod("ApplySavedData",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(state,new object[]{fixture});
            state.TryEquipItem("player_brawler","relic_sharpened_edge");
            await Open("MainMenu");
            var menu=(MapMenu)GetTree().CurrentScene;
            var map=Walk(menu).OfType<MapPathCanvas>().Single().MapOffset;var food=state.Food;
            SceneRouter.Instance.GoToShop(3);await Wait(.3);
            async Task TapHint(string hint)
            {
                var button=Walk(menu).OfType<Button>().Last(b=>b.IsVisibleInTree() && (b.AccessibilityName==hint || b.TooltipText==hint));
                for(var parent=button.GetParent();parent!=null;parent=parent.GetParent())
                    if(parent is ScrollContainer scroll) {scroll.EnsureControlVisible(button);await Wait(.1);break;}
                await TapModal(button);await Wait(.15);
            }
            async Task TapAction(string name)
            {
                var button=Walk(menu).OfType<Button>().Last(b=>b.IsVisibleInTree() && !b.Disabled && (b.Text==name || b.AccessibilityName==name));
                await TapModal(button);await Wait(.15);
            }
            ShopMenu Armory()=>Walk(menu).OfType<ShopMenu>().Single();
            var modals=Walk(menu).OfType<RealmModal>().Count();
            await TapHint("Guardian Shield");await TapAction("Equip");
            Check(state.GetUnitEquipment("player_brawler")?.Id=="relic_sharpened_edge","Opening equipment choice does not change the worn relic");
            Check(Walk(menu).OfType<RealmModal>().Count()==modals && Walk(Armory()).OfType<ScrollContainer>().Any(s=>s.Name=="RelicAllies" && s.IsVisibleInTree()),
                "Equipping happens on the relics page, not in a modal");
            AuditText("Equipment / choose ally");await Capture("01-choose-ally");
            await TapAction("Back");
            Check(Walk(Armory()).OfType<RoyalButton>().Any(b=>b.IsVisibleInTree() && b.AccessibilityName=="Guardian Shield") && Armory().RelicPreview==null,
                "Back leaves equipping for the collection, armory still open");
            await TapAction("Equip");
            await TapHint("Compare Guardian Shield on Peasant");
            Check(Armory().RelicPreview.Unit.Id=="player_peasant" && Walk(Armory()).OfType<Button>().Any(b=>b.AccessibilityName=="Preview relic slot 2" && b.Disabled),"Unpromoted ally sees its locked second slot");
            AuditText("Equipment / locked slot");await Capture("02-locked-slot");
            await TapHint("Compare Guardian Shield on Swordsman");
            var preview=Armory().RelicPreview;
            Check(preview.After.Armor>preview.Current.Armor && preview.After.AttackDamage<preview.Current.AttackDamage,"Native bearer selection shows both equipment gains and replacement losses");
            Check(state.GetUnitEquipment("player_brawler")?.Id=="relic_sharpened_edge","Previewing and switching allies does not equip anything");
            Check(Walk(Armory()).OfType<Control>().Any(c=>c.AccessibilityName== $"Armor: {preview.Current.Armor:0.##} → {preview.After.Armor:0.##}"),"Armor comparison displays the exact before and after values");
            AuditText("Equipment / comparison");await Capture("03-comparison");
            var confirm=Walk(Armory()).OfType<Button>().Single(b=>b.IsVisibleInTree() && b.AccessibilityName=="Equip to slot 1");
            Check(GetViewport().GetVisibleRect().Encloses(confirm.GetGlobalRect()),"Equipment confirmation stays visible outside scrolling stats");
            await TapAction("Equip to slot 1");
            Check(Armory().RelicPreview==null && Math.Abs(state.BuildPlayerUnitStats(GameData.GetUnit("player_brawler")).Armor-preview.After.Armor)<.001f,"Native confirmation equips exactly the previewed stats");
            await TapHint("Tower Charm");await TapAction("Equip");await TapHint("Compare Tower Charm on Swordsman");
            await TapHint("Preview relic slot 2");
            Check(Armory().RelicPreview.Slot==2 && Armory().RelicPreview.Limits.Any(l=>l.Label=="Armor" && l.Excess>1.5f),"Second-slot comparison shows the shared armor limit");
            var cap=Walk(Armory()).OfType<Label>().Single(l=>l.Text.StartsWith("Armor:") && l.Text.Contains("applied"));
            var bodyScroll=Walk(Armory()).OfType<ScrollContainer>().Single(s=>s.Name=="RelicComparison");bodyScroll.EnsureControlVisible(cap);await Wait(.2);
            AuditText("Equipment / armor limit");await Capture("04-bonus-limit");
            await TapAction("Equip to slot 2");
            Check(state.GetUnitEquipment2("player_brawler")?.Id=="relic_tower_charm","Native selection equips the promoted ally's second relic slot");
            await TapAction("Unequip");
            Check(Armory().RelicPreview.Slot==2 && Armory().RelicPreview.Candidate==null && Armory().RelicPreview.After.Armor<Armory().RelicPreview.Current.Armor,"Second-slot removal is previewed before a change");
            AuditText("Equipment / removal");await Capture("05-removal");
            await TapAction("Remove from slot 2");
            Check(state.GetUnitEquipment2("player_brawler")==null && state.GetUnitEquipment("player_brawler")?.Id=="relic_guardian_shield","Native removal preserves the first-slot relic");
            var saveBefore=System.Text.Json.JsonSerializer.Serialize(state.BuildSaveData());
            await TapHint("War Brand");
            Check(Walk(menu).OfType<RoyalButton>().Single(b=>b.IsVisibleInTree() && b.AccessibilityName=="Equip").Disabled,"An unowned relic cannot be equipped");
            var bonuses=Walk(menu).OfType<ScrollContainer>().Single(s=>s.Name=="RelicBonuses");
            Check(RelicStatText.Stats(GameData.GetEquipment("relic_war_brand")).All(s=>Walk(bonuses).OfType<RoyalLabel>().Any(l=>l.Text==s.Label)),"An unowned relic still lists all its bonuses");
            var saveAfter=System.Text.Json.JsonSerializer.Serialize(state.BuildSaveData());
            using(var oldSave=System.Text.Json.JsonDocument.Parse(saveBefore))
            using(var newSave=System.Text.Json.JsonDocument.Parse(saveAfter))
            {
                // The full-food clock refreshes during real UI waits; compare every progression field.
                var changed=oldSave.RootElement.EnumerateObject().Where(p=>p.Name!="FoodRechargedAtUnixSeconds" && p.Value.GetRawText()!=newSave.RootElement.GetProperty(p.Name).GetRawText()).Select(p=>p.Name).ToArray();
                Check(changed.Length==0,"Selecting an unowned relic leaves saved progression unchanged"+(changed.Length>0?$" (changed: {string.Join(", ",changed)})":""));
            }
            AuditText("Equipment / unowned relic");await Capture("06-unowned-relic");
            Check(state.Food==food && Walk(menu).OfType<MapPathCanvas>().Single().MapOffset==map,"Equipment choices preserve map position and travel supplies");
            menu.CloseHomeModal();
        }
        GD.Print($"EQUIPMENT_PREVIEW_REVIEW_RESULT: {_failures} failures");
        QuitAfterAudio(_failures==0?0:1);
    }
}
