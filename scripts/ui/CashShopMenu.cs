using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class CashShopMenu : Control
{
    private PanelContainer _goldPanel = null !;
    private PanelContainer _foodPanel = null !;
    private PanelContainer _mixedPanel = null !;
    private PanelContainer _statusPanel = null !;
    private HBoxContainer _resourcesRow = null !;
    private Label _statusLabel = null !;
    private VBoxContainer _goldStack = null !;
    private VBoxContainer _foodStack = null !;
    private VBoxContainer _mixedStack = null !;
    private HBoxContainer _categoryTabs = null !;
    private Label _noticeLabel = null !;
    private Button _cancelConfirmButton = null !;
    private HBoxContainer _refillRow = null !;
    private const string PurchaseHint = "Tap Buy twice to confirm.";
    private const int RationsPage = 1;
    private int _selectedCategory;
    private string _pendingConfirmProductId = "";
    private Button _pendingConfirmButton;
    // A native purchase is open in the store sheet or waiting on server verification.
    private bool _nativePurchaseBusy;
    public override void _Ready()
    {
        BuildModalUi();
        RefreshUi();
        GameState.Instance.FoodChanged += RebuildResourcesRow;
        // A paid transaction the server has not recorded yet (no session at
        // launch, a failed validation) is validated again here.
        NativeIAPService.Instance?.RetryUnfinishedPurchases();
        ShowRefundsAsync();
    }

    /// <summary>Applies any store refunds before the player shops, and says what they took back.</summary>
    private async void ShowRefundsAsync()
    {
        await WalletAdjustmentSync.SyncAsync(force: true);
        if (!IsOpen()) return;
        var notice = GameState.Instance.ConsumeWalletAdjustmentNotice();
        if (notice.Length == 0) return;
        // Leave a Buy the player has started alone; only the balances and the ledger change.
        RebuildResourcesRow();
        RefreshStatus();
        _noticeLabel.Text = notice;
    }

    public override void _ExitTree()
    {
        if (GameState.Instance != null) GameState.Instance.FoodChanged -= RebuildResourcesRow;
    }

    private void BuildModalUi()
    {
        var root = new VBoxContainer
        {
            Name = "StorehouseLayout"
        };
        root.AddThemeConstantOverride("separation", 8);
        AddChild(root);
        root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        // On the phone-sized canvas every row counts: the balances sit at the end of the tab row
        // instead of taking a strip of their own, leaving the height to the pack cards.
        var bar = new HBoxContainer { Name = "StorehouseBar" };
        bar.AddThemeConstantOverride("separation", 10);
        root.AddChild(bar);
        _categoryTabs = RealmUi.Tabs(bar, SelectCategory, "Gold", "Rations", "Bundles", "Purchase info");
        // The four tabs and the balances always fit, so the row needs no room for a scrollbar.
        ((Control)_categoryTabs.GetParent()).CustomMinimumSize = new Vector2(0, 48);
        var balances = new PanelContainer { Name = "StorehouseBalances", SizeFlagsVertical = SizeFlags.ShrinkCenter };
        balances.SetMeta("modal_unframed", true);
        balances.AddThemeStyleboxOverride("panel", new ModalSurface(ModalMaterial.Inset, 6));
        bar.AddChild(balances);
        _resourcesRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        _resourcesRow.AddThemeConstantOverride("separation", 20);
        balances.AddChild(_resourcesRow);
        BuildCatalog(root);
        RealmModal.Polish(root);
    }

    private void BuildCatalog(VBoxContainer body)
    {
        // The pages take the remaining space; purchase actions stay inside each card.
        var pages = new Control
        {
            Name = "CatalogPages",
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        body.AddChild(pages);
        _goldStack = CreateCatalogPage(pages, out _goldPanel);
        _foodStack = CreateCatalogPage(pages, out _foodPanel);
        _mixedStack = CreateCatalogPage(pages, out _mixedPanel);
        var info = CreateCatalogPage(pages, out _statusPanel);
        var statusStack = RealmUi.Scroll(info);
        statusStack.AddChild(RealmUi.Heading("The merchant's ledger"));
        _statusLabel = RealmUi.Label("");
        statusStack.AddChild(_statusLabel);
        // One fixed height whatever the footer holds, so the cards above never change size.
        var notice = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, 42)
        };
        notice.AddThemeConstantOverride("separation", 12);
        // Ends where the cards end, clear of the frame's rim.
        var footer = new MarginContainer();
        footer.AddThemeConstantOverride("margin_right", 8);
        body.AddChild(footer);
        footer.AddChild(notice);
        // The gold-funded refill and its recharge share the footer on the Rations page, where four
        // packs already fill the page. They make way while a pack waits for its confirmation.
        _refillRow = new HBoxContainer { Name = "RationRefill" };
        _refillRow.AddThemeConstantOverride("separation", 12);
        notice.AddChild(_refillRow);
        var refill = RealmUi.Button("food", "10 food · 100 gold", () =>
        {
            AudioDirector.Purchased(GameState.Instance.TryBuyFoodRefill(out var message));
            RefreshUi();
            _noticeLabel.Text = message;
        }, true);
        refill.CustomMinimumSize = new Vector2(refill.CustomMinimumSize.X, 40);
        _refillRow.AddChild(refill);
        var recharge = RealmUi.Label("+2 food every 5 minutes · Up to 24", 14, true);
        recharge.AutowrapMode = TextServer.AutowrapMode.Off;
        recharge.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        recharge.VerticalAlignment = VerticalAlignment.Center;
        _refillRow.AddChild(recharge);
        _noticeLabel = RealmUi.Label(PurchaseHint, 18, true);
        _noticeLabel.Name = "PurchaseNotice";
        _noticeLabel.VerticalAlignment = VerticalAlignment.Center;
        notice.AddChild(_noticeLabel);
        _cancelConfirmButton = new RealmButton
        {
            Text = "Cancel",
            Visible = false,
            CustomMinimumSize = new Vector2(88, 40)
        };
        _cancelConfirmButton.Pressed += () => CancelPendingPurchase();
        notice.AddChild(_cancelConfirmButton);
        SelectCategory(0);
    }

    private static VBoxContainer CreateCatalogPage(Control host, out PanelContainer panel)
    {
        panel = new PanelContainer();
        panel.SetMeta("modal_unframed", true);
        panel.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        host.AddChild(panel);
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var stack = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        panel.AddChild(scroll); scroll.AddChild(stack);
        return stack;
    }

    private void SelectCategory(int index)
    {
        if (index != _selectedCategory)
            CancelPendingPurchase();
        _selectedCategory = index;
        var pages = new[]
        {
            _goldPanel,
            _foodPanel,
            _mixedPanel,
            _statusPanel
        };
        for (var i = 0; i < pages.Length; i++)
            pages[i].Visible = i == index;
        _categoryTabs.GetChild<Button>(index).ButtonPressed = true;
        ShowRefill();
    }

    private void ShowRefill()
    {
        _refillRow.Visible = _selectedCategory == RationsPage && _pendingConfirmProductId.Length == 0;
        // Beside the refill, the notice keeps to the far end so a result doesn't run on from the recharge.
        _noticeLabel.HorizontalAlignment = _refillRow.Visible ? HorizontalAlignment.Right : HorizontalAlignment.Left;
    }

    private void RefreshUi(bool preserveStatus = false)
    {
        CancelPendingPurchase(resetNotice: !preserveStatus);
        RebuildResourcesRow();
        RebuildGoldPacks();
        RebuildFoodPacks();
        RebuildMixedPacks();
        if (!preserveStatus)
        {
            RefreshStatus();
        }
    }

    private void RebuildResourcesRow()
    {
        RealmUi.Clear(_resourcesRow);
        foreach (var balance in new[] {
            HomeResourceUi.Amount("gold", GameState.Instance.Gold.ToString("N0"), $"Gold: {GameState.Instance.Gold:N0}", 28),
            HomeResourceUi.Amount("food", $"{GameState.Instance.Food}/{GameState.FoodRechargeCap}", GameState.Instance.FoodRechargeText, 28) })
        {
            balance.GetChild<Label>(1).AddThemeFontSizeOverride("font_size", 20);
            _resourcesRow.AddChild(balance);
        }
        RealmModal.Polish(_resourcesRow);
    }

    private void RebuildGoldPacks() => RebuildCategory(_goldStack, "gold");
    private void RebuildFoodPacks() => RebuildCategory(_foodStack, "food");

    private void RebuildMixedPacks() => RebuildCategory(_mixedStack, "mixed");
    private void RebuildCategory(VBoxContainer stack, string category)
    {
        ClearChildren(stack);
        stack.AddThemeConstantOverride("separation", 8);
        var row = new HBoxContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        row.AddThemeConstantOverride("separation", 8);
        stack.AddChild(row);
        var products = ShopProductCatalog.GetByCategory(category);
        foreach (var product in products)
            AddProductCard(row, product, product.OneTimePurchase && GameState.Instance.HasPurchasedProduct(product.Id), wide: products.Count <= 2);
        RealmModal.Polish(row);
    }

    /// <summary>
    /// A pack card: name, supplies, bonus and the purchase button pinned to the bottom. Four packs share a row on
    /// the phone-sized canvas, so their names take the card's full width (two lines at most) and the whole card
    /// fits without scrolling. Bundles, two to a row, also carry their emblem and the Details of their terms.
    /// </summary>
    private void AddProductCard(Control host, ShopProduct product, bool forceDisabled, bool wide)
    {
        var panel = new PanelContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        host.AddChild(panel);
        var card = new VBoxContainer();
        card.AddThemeConstantOverride("separation", 6);
        panel.AddChild(card);
        var top = new HBoxContainer();
        top.AddThemeConstantOverride("separation", 10);
        card.AddChild(top);
        var title = RealmUi.Heading(product.DisplayName, 16);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        title.VerticalAlignment = VerticalAlignment.Center;
        if (wide)
        {
            top.AddChild(new HeraldicEmblem { Symbol = product.Category == "gold" ? "crown" : product.Category == "food" ? "food" : "gift", CustomMinimumSize = new Vector2(40, 40) });
            top.AddChild(title);
            var details = new RealmButton
            {
                Text = "Details",
                CustomMinimumSize = new Vector2(82, 40),
                AccessibilityName = product.DisplayName + " details",
                TooltipText = product.DisplayName + " details"
            };
            details.Pressed += () => RealmUi.Details(this, product.DisplayName, $"{product.FormattedReward}\n\n{product.Description}\n\nPrice: {LocalizedPrice(product)}" + (product.OneTimePurchase ? "\nAvailable once per account." : ""));
            top.AddChild(details);
        }
        else
        {
            // Two lines for every name, so the supplies line up across the row.
            title.CustomMinimumSize = new Vector2(0, 2 * RealmUi.HeadingFont.GetHeight(title.GetThemeFontSize("font_size")) + 3);
            top.AddChild(title);
        }
        card.AddChild(BuildProductRewardRow(product));
        var extra = product.GrantsUnitUnlock ? "+ Unit unlock" : product.BonusAmount > 0 ? $"Includes {product.BonusAmount:N0} bonus" : "";
        var bonus = RealmUi.Label(extra, 14, true);
        bonus.CustomMinimumSize = new Vector2(0, 24);
        card.AddChild(bonus);
        var localizedPrice = LocalizedPrice(product);
        var purchaseButton = new RealmButton
        {
            Text = forceDisabled ? "Purchased" : $"Buy — {localizedPrice}",
            CustomMinimumSize = new Vector2(0, 48),
            // Takes the card's spare height but stays at the bottom, so the buttons line up across the row.
            SizeFlagsVertical = SizeFlags.ShrinkEnd | SizeFlags.Expand,
            Disabled = forceDisabled
        };
        purchaseButton.SetMeta("realm_primary", true);
        purchaseButton.SetMeta("store_product_id", product.Id);
        purchaseButton.AccessibilityName = $"Buy {product.DisplayName} for {localizedPrice}";
        purchaseButton.Pressed += () => OnPurchasePressed(product.Id, purchaseButton);
        card.AddChild(purchaseButton);
    }

    private static HBoxContainer BuildProductRewardRow(ShopProduct product)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        if (product.CurrencyType.Equals("gold", StringComparison.OrdinalIgnoreCase))
        {
            row.AddChild(UiBadgeFactory.CreateRewardBadge("gold", "", product.FormattedReward, new Vector2(28f, 28f)));
        }
        else if (product.CurrencyType.Equals("food", StringComparison.OrdinalIgnoreCase))
        {
            row.AddChild(UiBadgeFactory.CreateRewardBadge("food", "", product.FormattedReward, new Vector2(28f, 28f)));
        }
        else if (product.CurrencyType.Equals("mixed", StringComparison.OrdinalIgnoreCase))
        {
            row.AddChild(UiBadgeFactory.CreateRewardBadge("gold", "", product.FormattedReward, new Vector2(28f, 28f)));
        }

        var reward = RealmUi.Label(product.CurrencyType == "mixed" ? $"{product.GoldAmount:N0} gold · {product.FoodAmount:N0} food" : $"{product.TotalCurrencyAmount:N0} {product.CurrencyType}", 24);
        reward.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(reward);
        return row;
    }

    private static string LocalizedPrice(ShopProduct product) => NativeIAPService.Instance?.GetLocalizedPrice(product.Id) ?? product.FormattedPrice;
    private void CancelPendingPurchase(bool resetNotice = true)
    {
        if (GodotObject.IsInstanceValid(_pendingConfirmButton))
        {
            var product = ShopProductCatalog.GetById(_pendingConfirmProductId);
            if (product != null)
                _pendingConfirmButton.Text = $"Buy — {LocalizedPrice(product)}";
        }

        _pendingConfirmProductId = "";
        _pendingConfirmButton = null;
        _cancelConfirmButton.Hide();
        ShowRefill();
        if (resetNotice)
            _noticeLabel.Text = PurchaseHint;
    }

    private void OnPurchasePressed(string productId, Button button)
    {
        if (_nativePurchaseBusy)
        {
            _noticeLabel.Text = "Finishing your last purchase. Try again in a moment.";
            return;
        }

        if (_pendingConfirmProductId == productId)
        {
            SelectCategory(3);
            _noticeLabel.Text = "Your purchase status is shown above.";
            ExecutePurchase(productId);
            _pendingConfirmProductId = "";
            _pendingConfirmButton = null;
            return;
        }

        CancelPendingPurchase();
        // Show confirm state
        _pendingConfirmProductId = productId;
        _pendingConfirmButton = button;
        var selected = ShopProductCatalog.GetById(productId);
        button.Text = $"Confirm — {LocalizedPrice(selected)}";
        _noticeLabel.Text = $"{selected.DisplayName} · {LocalizedPrice(selected)}. Tap Confirm to purchase.";
        _cancelConfirmButton.Show();
        ShowRefill();
    }

    private void ExecutePurchase(string productId)
    {
        var product = ShopProductCatalog.GetById(productId);
        if (product == null)
        {
            _statusLabel.Text = "Unknown product.";
            RefreshUi(preserveStatus: true);
            return;
        }

        var endpoint = GameState.Instance.PurchaseValidationEndpoint;
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            _statusLabel.Text = "The store is unavailable in this build. Connect a verified commerce server before accepting payments.";
            RefreshUi(preserveStatus: true);
            return;
        }

        if (string.IsNullOrWhiteSpace(GameState.Instance.PlayerAuthToken) && !PlayerProfileSyncService.RefreshProfileForBackendEndpoint(endpoint, out var sessionMessage))
        {
            _statusLabel.Text = $"Unable to establish a secure store session: {sessionMessage}";
            RefreshUi(preserveStatus: true);
            return;
        }

        var platform = DetectPlatform();
        var nativeIap = NativeIAPService.Instance;
        if (platform == "stripe")
        {
            ExecuteStripePurchase(product);
            return;
        }

        // Native IAP path (Apple/Google)
        if (nativeIap != null && nativeIap.IsAvailable && nativeIap.Platform != IAPPlatform.Stripe)
        {
            ExecuteNativeIAPPurchase(product, platform);
            return;
        }

        _statusLabel.Text = "Native billing is unavailable. This purchase has not been charged.";
        RefreshUi(preserveStatus: true);
    }

    private void ExecuteStripePurchase(ShopProduct product)
    {
        _statusLabel.Text = $"Creating checkout for {product.DisplayName}...";
        try
        {
            var provider = new HttpApiPurchaseValidationProvider(GameState.Instance.PurchaseValidationEndpoint);
            var checkout = provider.CreateStripeCheckout(GameState.Instance.PlayerProfileId, product.Id);
            if (checkout.Status == "ok" && !string.IsNullOrWhiteSpace(checkout.CheckoutUrl))
            {
                _statusLabel.Text = $"Opening payment page for {product.DisplayName}...\n" + $"Price: ${checkout.PriceCents / 100.0:F2}\n\n" + "Complete payment in your browser.\n" + "Your account will be credited automatically.";
                OS.ShellOpen(checkout.CheckoutUrl);
            }
            else
            {
                _statusLabel.Text = $"Checkout failed: {checkout.Message}";
            }
        }
        catch (Exception e)
        {
            _statusLabel.Text = $"Checkout error: {e.Message}";
        }
    }

    private void ExecuteNativeIAPPurchase(ShopProduct product, string platform)
    {
        _nativePurchaseBusy = true;
        _statusLabel.Text = $"Starting purchase: {product.DisplayName}...";
        NativeIAPService.Instance.PurchaseProduct(product.Id, iapResult => OnNativePurchaseReturned(product, platform, iapResult));
    }

    private async void OnNativePurchaseReturned(ShopProduct product, string platform, IAPPurchaseResult iapResult)
    {
        if (!iapResult.Success)
        {
            _nativePurchaseBusy = false;
            if (!IsOpen()) return;
            _statusLabel.Text = iapResult.Cancelled ? "Purchase cancelled."
                : iapResult.Pending ? iapResult.ErrorMessage
                : $"Purchase not completed: {iapResult.ErrorMessage}";
            RefreshUi(preserveStatus: true);
            return;
        }

        if (IsOpen())
        {
            _statusLabel.Text = $"Validating receipt for {product.DisplayName}...";
            _noticeLabel.Text = "Validating your purchase…";
        }

        // Verification runs off the main thread, so the shop keeps drawing and responding. The
        // service applies the grant and finishes the store purchase even if the shop closes.
        PurchaseFulfillment fulfillment;
        try
        {
            fulfillment = await NativeIAPService.Instance.FulfillPurchaseAsync(iapResult, platform);
        }
        finally
        {
            _nativePurchaseBusy = false;
        }

        if (!IsOpen()) return;
        var result = fulfillment.Result;
        if (fulfillment.Granted)
        {
            _statusLabel.Text = $"Purchased {product.DisplayName}!\n";
            if (result.GoldCredited > 0)
                _statusLabel.Text += $"+{result.GoldCredited} Gold  ";
            if (result.FoodCredited > 0)
                _statusLabel.Text += $"+{result.FoodCredited} Food  ";
            if (result.GrantedUnitUnlock)
                _statusLabel.Text += "\n+ Unit unlock granted!";
            AudioDirector.Instance?.PlayRewardClaim();
        }
        else if (result.Status == "refunded")
        {
            _statusLabel.Text = $"The store refunded this {product.DisplayName}, so it adds nothing.";
        }
        else if (fulfillment.Finished)
        {
            _statusLabel.Text = $"{product.DisplayName} was already added to your account.";
        }
        else if (NativeIAPService.Instance?.Platform == IAPPlatform.Google && result.Status != "in_progress")
        {
            _statusLabel.Text = $"Not verified yet: {result.Message}\nGoogle Play keeps the payment open, and the game checks it again the next time it starts.";
        }
        else
        {
            _statusLabel.Text = $"Validation failed: {result.Message}";
        }

        RefreshUi(preserveStatus: true);
        _noticeLabel.Text = "Your purchase status is shown above.";
    }

    private bool IsOpen() => IsInstanceValid(this) && IsInsideTree() && !IsQueuedForDeletion();

    private void RefreshStatus()
    {
        if (_nativePurchaseBusy || (!string.IsNullOrWhiteSpace(_statusLabel.Text) && _statusLabel.Text.Contains("Purchased")))
        {
            return;
        }

        var lines = new List<string>
        {
            $"Purchases: {GameState.Instance.TotalPurchaseCount}",
            $"Gold: {GameState.Instance.Gold}",
            $"Food: {GameState.Instance.Food}"};
        var refunds = GameState.Instance.GetAppliedWalletAdjustments().Count(a => a.Kind == "refund" && (a.GoldApplied != 0 || a.FoodApplied != 0 || a.GoldUnrecovered != 0 || a.FoodUnrecovered != 0));
        if (refunds > 0)
            lines.Add($"Refunded purchases taken back: {refunds}");
        var endpoint = GameState.Instance.PurchaseValidationEndpoint;
        lines.Add(string.IsNullOrWhiteSpace(endpoint) ? "Mode: Store disabled" : "Mode: Online");
        var nativeIap = NativeIAPService.Instance;
        if (nativeIap != null)
        {
            var platformLabel = nativeIap.Platform switch
            {
                IAPPlatform.Apple => "Apple StoreKit",
                IAPPlatform.Google => "Google Play",
                IAPPlatform.Stripe => "Stripe (web)",
                _ => "None"
            };
            lines.Add($"Payment: {platformLabel}");
            if (nativeIap.Platform != IAPPlatform.Stripe)
            {
                lines.Add($"Store: {(nativeIap.IsAvailable ? "Connected" : "Unavailable")}");
            }
        }

        lines.Add("");
        lines.Add("Packs credit your account after payment is verified. The Adventurer's Kit is available once per account.");
        _statusLabel.Text = string.Join("\n", lines);
    }

    private static string DetectPlatform()
    {
        if (OS.HasFeature("ios"))
            return "apple";
        if (OS.HasFeature("android"))
            return "google";
        return "stripe";
    }

    private static void ClearChildren(Control parent) => RealmUi.Clear(parent);
}
