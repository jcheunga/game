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
        foreach (var category in new[] { (Tab: "Gold", Id: "gold"), (Tab: "Rations", Id: "food"), (Tab: "Shards", Id: "shards"), (Tab: "Bundles", Id: "mixed") })
        {
            await TapModal(Walk(store).OfType<Button>().Single(button => button.Text == category.Tab));
            var actions = Walk(store).OfType<Button>().Where(button => button.IsVisibleInTree() && button.HasMeta("store_product_id")).ToArray();
            Check(actions.Length == ShopProductCatalog.GetByCategory(category.Id).Count, category.Tab + ": every pack has a purchase action");
            var cards = Walk(store).OfType<StorePackCard>().Where(card => card.IsVisibleInTree()).ToArray();
            Check(cards.All(card => Walk(card).OfType<TextureRect>().Any(art => art.Texture != null && art.Texture.ResourcePath.Contains("/items/store_" + card.Product.Id))),
                category.Tab + ": every pack shows its painted picture");
            if (category.Id != "mixed")
                Check(cards.Count(card => Walk(card).OfType<RoyalLabel>().Any(label => label.Text == "Best value")) == 1, category.Tab + ": one pack is marked best value");
            foreach (var action in actions)
            {
                var product = ShopProductCatalog.GetById((string)action.GetMeta("store_product_id"));
                Check(pages.GetGlobalRect().Encloses(action.GetGlobalRect()) && modal.Content.GetGlobalRect().Encloses(action.GetGlobalRect())
                    && action.Text == $"Buy — {product.FormattedPrice}", product.DisplayName + ": Buy and its price are fully visible without scrolling");
            }
            var balances = store.GetNode<Control>("StorehouseLayout/StorehouseBar/StorehouseBalances");
            Check(balances.AccessibilityName.Contains("Relic shards") == (category.Id == "shards"), category.Tab + ": the balances show relic shards only on the Shards page");
            AuditText("Storehouse / " + category.Id);
            await Capture("storehouse-" + category.Id);
            // One tap starts the purchase; the store's own payment sheet is the confirmation. The review build has no
            // commerce server, so the purchase stops before billing and no payment is ever sent.
            await TapModal(actions[^1]);
            Check(notice.Text == "Your purchase status is shown above." && (int)typeof(CashShopMenu).GetField("_selectedCategory", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(store)! == 4
                && Walk(store).OfType<Label>().Any(label => label.IsVisibleInTree() && label.Text.StartsWith("The store is unavailable")),
                category.Tab + ": one tap on Buy starts the purchase and shows its status");
        }
        Check(state.Gold == 500 && state.Food == 10 && state.TotalPurchaseCount == purchaseCount, "Browsing packs and starting purchases without a server spends and grants nothing");

        await TapModal(Walk(store).OfType<Button>().Single(button => button.Text == "Rations"));
        var refill = Walk(store).OfType<Button>().Single(button => button.IsVisibleInTree() && button.Text == "10 food · 100 gold");
        // Four packs fill the page, so the refill and its recharge share the footer beneath them.
        var recharge = refill.GetParent().GetChildren().OfType<Label>().Single();
        Check(modal.Content.GetGlobalRect().Encloses(refill.GetGlobalRect()) && !pages.GetGlobalRect().Intersects(refill.GetGlobalRect())
            && recharge.IsVisibleInTree() && recharge.Text.Contains("every 5 minutes") && modal.Content.GetGlobalRect().Encloses(recharge.GetGlobalRect()),
            "The gold-funded ration refill is visible beside its recharge information");
        await TapModal(refill);
        Check(state.Gold == 400 && state.Food == 20 && notice.Text == "+10 food", "Refilling spends 100 gold once, grants 10 food and keeps the result visible");
        Check(Walk(store).OfType<Button>().Where(button => button.IsVisibleInTree() && button.HasMeta("store_product_id"))
            .All(button => pages.GetGlobalRect().Encloses(button.GetGlobalRect())), "Ration purchase actions remain visible after their cards refresh");
        AuditText("Storehouse / refilled rations");
        await Capture("storehouse-refill");

        await TapModal(Walk(store).OfType<Button>().Single(button => button.Text == "Bundles"));
        // A bundle's wide card lists everything it holds, so it needs no details dialog.
        var kit = Walk(store).OfType<StorePackCard>().Single(card => card.Product.Id == "starter_kit");
        var shown = Walk(kit).OfType<Label>().Where(label => label.IsVisibleInTree()).Select(label => label.Text)
            .Concat(Walk(kit).OfType<RoyalLabel>().Where(label => label.IsVisibleInTree()).Select(label => label.Text)).ToArray();
        Check(shown.Contains("800 gold") && shown.Contains("30 food") && shown.Contains($"Recruits {GameData.GetUnit(state.NextKitUnitId()).DisplayName}") && shown.Contains("Once per account")
            && pages.GetGlobalRect().Encloses(kit.GetGlobalRect()), "The bundle card shows its complete contents and one-time restriction");
        Check(Walk(store).OfType<StorePackCard>().Where(card => card.IsVisibleInTree()).Count(card => Walk(card).OfType<RoyalLabel>().Any(label => label.Text == "Best value")) == 0,
            "Bundles carry no best-value ribbon");

        // The kit recruits a named troop, never a random one, and a player who has recruited every troop they reached still gets one.
        var kitUnit = state.NextKitUnitId();
        Check(kitUnit.Length > 0 && !state.IsUnitOwned(kitUnit) && kitUnit == state.NextKitUnitId(), "The kit names a troop the player doesn't have, the same every time");
        var kitSave = state.BuildSaveData();
        state.TryApplyPurchaseReward(new PurchaseValidationResult { Status = "ok", PurchaseId = "PUR-REVIEW-KIT", ProductId = "starter_kit", GrantedUnitUnlock = true });
        Check(state.IsUnitOwned(kitUnit) && state.LastKitUnitId == kitUnit && state.NextKitUnitId() != kitUnit, "Buying the kit recruits exactly the troop its card names");

        // A guest is asked once, after their first purchase, to sign in so it isn't lost with the phone.
        Check(state.AccountProvider.Length == 0 && state.ShouldPromptSignInAfterPurchase, "A guest who has bought something is due the sign-in prompt");
        var prompt = typeof(CashShopMenu).GetMethod("PromptGuestSignIn", BindingFlags.Instance | BindingFlags.NonPublic)!;
        prompt.Invoke(store, null);
        await Wait(.2);
        Button Labelled(string text) => Walk(store).OfType<Button>().FirstOrDefault(button => button.IsVisibleInTree() && !button.IsQueuedForDeletion() && button.Text == text);
        var notNow = Labelled("Not now");
        Check(Walk(store).OfType<Label>().Any(label => label.IsVisibleInTree() && label.Text == "Keep your purchase safe") && Labelled("Sign in") != null && notNow != null,
            "After a guest's first purchase the shop offers Sign in or Not now");
        AuditText("Storehouse / sign-in after purchase"); await Capture("storehouse-sign-in-prompt");
        if (notNow != null) await TapModal(notNow);
        prompt.Invoke(store, null);
        await Wait(.2);
        Check(Labelled("Not now") == null && !state.ShouldPromptSignInAfterPurchase, "The prompt comes once");
        typeof(GameState).GetMethod("ApplySavedData", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(state, new object[] { kitSave });
        knowledge = state.AdventureKnowledgeRevision; // Loading a save invalidates the knowledge cache.
        Check(canvas.MapOffset == camera && state.AdventureKnowledgeRevision == knowledge, "Browsing packs and refilling rations never moves or reveals the map");

        fixture = state.BuildSaveData(); fixture.PurchasedProductIds = new[] { "starter_kit" };
        typeof(GameState).GetMethod("ApplySavedData", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(state, new object[] { fixture });
        knowledge = state.AdventureKnowledgeRevision; // Loading a save invalidates the knowledge cache.
        typeof(CashShopMenu).GetMethod("RefreshUi", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(store, new object[] { false });
        await Wait(.2);
        var owned = Walk(store).OfType<Button>().Single(button => button.HasMeta("store_product_id") && (string)button.GetMeta("store_product_id") == "starter_kit");
        Check(owned.Disabled && owned.Text == "Purchased" && pages.GetGlobalRect().Encloses(owned.GetGlobalRect()), "An owned one-time bundle stays visibly purchased and disabled");
        AuditText("Storehouse / owned bundle"); await Capture("storehouse-owned-bundle");

        await TapModal(Walk(store).OfType<Button>().Single(button => button.Text == "Purchases"));
        AuditText("Storehouse / purchase info"); await Capture("storehouse-info");
        menu.CloseHomeModal();
        Check(GetTree().CurrentScene == menu && canvas.MapOffset == camera && state.AdventureKnowledgeRevision == knowledge, "Closing the store preserves the map, camera and exploration");
        typeof(GameState).GetMethod("ApplySavedData", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(state, new object[] { baseline });
        typeof(MapMenu).GetMethod("RefreshUi", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(menu, null);
    }
}
