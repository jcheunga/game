using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

public partial class UiReviewSmoke
{
    private async Task ReviewStorehouse(MapMenu menu)
    {
        var state = GameState.Instance;
        var baseline = state.BuildSaveData();
        var fixture = state.BuildSaveData();
        fixture.Gold = 500;
        fixture.Food = 10;
        fixture.FoodRechargedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        fixture.PurchasedProductIds = Array.Empty<string>();
        typeof(GameState).GetMethod("ApplySavedData", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(state, new object[] { fixture });
        var canvas = Walk(menu).OfType<MapPathCanvas>().Single();
        var camera = canvas.MapOffset;
        var knowledge = state.AdventureKnowledgeRevision;
        menu.OpenHomeDestination(SceneRouter.CashShopScene);
        await Wait(.4);
        var store = Walk(menu).OfType<CashShopMenu>().Single();
        var modal = menu.GetNode<RealmModal>("HomeModal");
        var pages = store.GetNode<Control>("StorehouseLayout/CatalogPages");
        var notice = Walk(store).OfType<Label>().Single(label => label.Name == "PurchaseNotice");
        var purchaseCount = state.TotalPurchaseCount;
        foreach (var category in new[] { (Tab: "Gold", Id: "gold"), (Tab: "Rations", Id: "food"), (Tab: "Bundles", Id: "mixed") })
        {
            await TapModal(Walk(store).OfType<Button>().Single(button => button.Text == category.Tab));
            var actions = Walk(store).OfType<Button>().Where(button => button.IsVisibleInTree() && button.HasMeta("store_product_id")).ToArray();
            Check(actions.Length == ShopProductCatalog.GetByCategory(category.Id).Count, category.Tab + ": every pack has a purchase action");
            foreach (var action in actions)
            {
                var product = ShopProductCatalog.GetById((string)action.GetMeta("store_product_id"));
                Check(pages.GetGlobalRect().Encloses(action.GetGlobalRect()) && modal.Content.GetGlobalRect().Encloses(action.GetGlobalRect()), product.DisplayName + ": purchase button is fully visible without scrolling");
                // Only the first tap: the review never opens billing or sends a payment.
                await TapModal(action);
                Check(action.Text.StartsWith("Confirm — ") && notice.Text.Contains(product.DisplayName)
                    && modal.Content.GetGlobalRect().Encloses(notice.GetGlobalRect()), product.DisplayName + ": native click shows an unobstructed confirmation with the pack and price");
                Check(actions.Where(other => other != action).All(other => other.Text.StartsWith("Buy — ")), "Selecting another pack cancels its previous confirmation");
                AuditText("Storehouse / confirm " + product.Id);
            }
            await TapModal(Walk(store).OfType<Button>().Single(button => button.IsVisibleInTree() && button.Text == "Cancel"));
            Check(actions.All(button => button.Text.StartsWith("Buy — ")), category.Tab + ": Cancel restores all purchase buttons");
            AuditText("Storehouse / " + category.Id);
            await Capture("storehouse-" + category.Id);
        }
        Check(state.Gold == 500 && state.Food == 10 && state.TotalPurchaseCount == purchaseCount, "Inspecting packs and cancelling confirmations does not spend or grant resources");

        await TapModal(Walk(store).OfType<Button>().Single(button => button.Text == "Rations"));
        var refill = Walk(store).OfType<Button>().Single(button => button.IsVisibleInTree() && button.Text == "10 food · 100 gold");
        Check(pages.GetGlobalRect().Encloses(refill.GetGlobalRect()), "The gold-funded ration refill is visible beside its recharge information");
        await TapModal(refill);
        Check(state.Gold == 400 && state.Food == 20 && notice.Text == "+10 food", "Refilling spends 100 gold once, grants 10 food and keeps the result visible");
        Check(Walk(store).OfType<Button>().Where(button => button.IsVisibleInTree() && button.HasMeta("store_product_id"))
            .All(button => pages.GetGlobalRect().Encloses(button.GetGlobalRect())), "Ration purchase actions remain visible after their cards refresh");
        AuditText("Storehouse / refilled rations");
        await Capture("storehouse-refill");

        await TapModal(Walk(store).OfType<Button>().Single(button => button.Text == "Bundles"));
        await TapModal(Walk(store).OfType<Button>().Single(button => button.AccessibilityName == "Adventurer's Kit details"));
        var details = Walk(store).OfType<AcceptDialog>().Single();
        Check(details.Visible && Walk(details).OfType<Label>().Any(label => label.Text.Contains("800 Gold") && label.Text.Contains("30 Food")
            && label.Text.Contains("Unit Unlock") && label.Text.Contains("once per account")), "Details retains the complete bundle contents and one-time restriction");
        details.Hide(); details.EmitSignal(AcceptDialog.SignalName.Confirmed); await Wait(.2);
        Check(canvas.MapOffset == camera && state.AdventureKnowledgeRevision == knowledge, "Browsing packs, opening details and refilling rations never moves or reveals the map");

        fixture = state.BuildSaveData(); fixture.PurchasedProductIds = new[] { "starter_kit" };
        typeof(GameState).GetMethod("ApplySavedData", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(state, new object[] { fixture });
        knowledge = state.AdventureKnowledgeRevision; // Loading a save invalidates the knowledge cache.
        typeof(CashShopMenu).GetMethod("RefreshUi", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(store, new object[] { false });
        await Wait(.2);
        var owned = Walk(store).OfType<Button>().Single(button => button.HasMeta("store_product_id") && (string)button.GetMeta("store_product_id") == "starter_kit");
        Check(owned.Disabled && owned.Text == "Purchased" && pages.GetGlobalRect().Encloses(owned.GetGlobalRect()), "An owned one-time bundle stays visibly purchased and disabled");
        AuditText("Storehouse / owned bundle"); await Capture("storehouse-owned-bundle");

        await TapModal(Walk(store).OfType<Button>().Single(button => button.Text == "Purchase info"));
        AuditText("Storehouse / purchase info"); await Capture("storehouse-info");
        menu.CloseHomeModal();
        Check(GetTree().CurrentScene == menu && canvas.MapOffset == camera && state.AdventureKnowledgeRevision == knowledge, "Closing the store preserves the map, camera and exploration");
        typeof(GameState).GetMethod("ApplySavedData", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(state, new object[] { baseline });
        typeof(MapMenu).GetMethod("RefreshUi", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(menu, null);
    }
}
