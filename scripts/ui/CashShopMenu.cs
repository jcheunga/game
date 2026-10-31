using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// The royal storehouse: gold and ration packs, bundles and the merchant's ledger, in the achievements screen's
/// painted tabs with the home strip's balances beside them. Each pack is a <see cref="StorePackCard"/>; buying takes
/// two taps (Buy, then Confirm) and goes to the platform store, whose verified result the ledger page reports.
/// The page sits on the 1.55x modal canvas, so its sizes are modal units.
/// </summary>
public partial class CashShopMenu : Control
{
    private static readonly string[] Categories = { "Gold", "Rations", "Shards", "Bundles", "Purchases" };
    private static readonly string[] TabGlyphs = { "storetab-gold", "storetab-rations", "storetab-shards", "storetab-bundles", "storetab-ledger" };
    private static readonly string[] PackCategories = { "gold", "food", "shards", "mixed" };
    // Five tabs and the balances share the phone canvas's width.
    private static readonly Vector2 TabSize = new(104, 40);
    private static readonly Color Muted = new("b9ad96"), Body = new("e9dfcb"), Heading = new("f3deaa");
    private const int RationsPage = 1, ShardsPage = 2, LedgerPage = 4;
    private readonly List<RoyalButton> _tabs = new();
    private readonly Control[] _pages = new Control[Categories.Length];
    private Control _balances = null !;
    private Label _statusLabel = null !;
    private Control _statusPlaceholder = null !;
    private Control _statusSpacer = null !;
    private VBoxContainer _ledgerRows = null !;
    private Label _noticeLabel = null !;
    private HBoxContainer _refillRow = null !;
    private int _selectedCategory;
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
        ShowRefundsAndPauseAsync();
        MarkOneTimePurchasesAsync();
    }

    /// <summary>
    /// After a guest's first purchase, asks once whether to sign in: until then the purchase lives only on this
    /// device, and a lost phone loses it.
    /// </summary>
    private void PromptGuestSignIn()
    {
        var state = GameState.Instance;
        if (!state.ShouldPromptSignInAfterPurchase) return;
        state.MarkSignInAfterPurchasePrompted();
        MedievalUi.ShowConfirmation(this, "Keep your purchase safe",
            "You're playing as a guest, so your progress and purchases live only on this device. Sign in and they come back on a new phone or after reinstalling.",
            "Sign in", () => AccountDialog.Show(this), "Not now");
    }

    /// <summary>Marks the Adventurer's Kit bought if the server's history says so, so an older save never offers it again.</summary>
    private async void MarkOneTimePurchasesAsync()
    {
        if (await GameState.Instance.SyncOneTimePurchasesFromServerAsync() && IsOpen()) RefreshUi(preserveStatus: true);
    }

    /// <summary>
    /// Applies any store refunds before the player shops and says what they took back. While the operator has paused
    /// purchases, every Buy is greyed out and the pause takes the notice instead: the refund's balances still change,
    /// and its message waits for the next visit after purchases resume.
    /// </summary>
    private async void ShowRefundsAndPauseAsync()
    {
        var refunds = WalletAdjustmentSync.SyncAsync(force: true);
        await GameState.Instance.RefreshPurchasesPauseAsync();
        await refunds;
        if (!IsOpen()) return;
        if (GameState.Instance.PurchasesPaused)
        {
            RefreshUi(preserveStatus: true);
            _noticeLabel.Text = GameState.Instance.PurchasesPausedMessage;
            return;
        }
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
        var bar = new HBoxContainer { Name = "StorehouseBar", CustomMinimumSize = new Vector2(0, TabSize.Y) };
        bar.AddThemeConstantOverride("separation", 4);
        root.AddChild(bar);
        for (var i = 0; i < Categories.Length; i++)
        {
            var index = i;
            var tab = RoyalButton.Over(new Rect2(Vector2.Zero, TabSize), Categories[i], () => SelectCategory(index), 4);
            tab.CustomMinimumSize = TabSize;
            bar.AddChild(tab);
            _tabs.Add(tab);
        }
        bar.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        _balances = new Control { Name = "StorehouseBalances", MouseFilter = MouseFilterEnum.Pass };
        bar.AddChild(_balances);
        var pages = new Control
        {
            Name = "CatalogPages",
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        root.AddChild(pages);
        for (var i = 0; i < PackCategories.Length; i++)
        {
            // Every pack fits on its page, so the cards share the full width and height without scrolling.
            var row = new HBoxContainer { Name = Categories[i] + "Page", MouseFilter = MouseFilterEnum.Ignore };
            row.AddThemeConstantOverride("separation", 8);
            pages.AddChild(row);
            row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            _pages[i] = row;
        }
        _pages[LedgerPage] = BuildLedger(pages);
        BuildFooter(root);
        SelectCategory(0);
    }

    /// <summary>The ledger page: the account's purchase record beside the latest purchase and how buying works.</summary>
    private Control BuildLedger(Control pages)
    {
        var page = new HBoxContainer { Name = "LedgerPage", MouseFilter = MouseFilterEnum.Ignore };
        page.AddThemeConstantOverride("separation", 8);
        pages.AddChild(page);
        page.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        VBoxContainer Panel(string title, float ratio)
        {
            var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = ratio, MouseFilter = MouseFilterEnum.Ignore };
            var surface = StorePackCard.Kit("ach-card", 9);
            surface.ContentMarginLeft = surface.ContentMarginRight = 18;
            surface.ContentMarginTop = 12; surface.ContentMarginBottom = 14;
            panel.AddThemeStyleboxOverride("panel", surface);
            page.AddChild(panel);
            var stack = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            stack.AddThemeConstantOverride("separation", 6);
            panel.AddChild(stack);
            var heading = StorePackCard.Caps(title, 14, Heading, 700);
            heading.CustomMinimumSize = new Vector2(0, 24);
            heading.ShrinkToFit = true;
            stack.AddChild(heading);
            stack.AddChild(new TextureRect { Texture = RoyalKit.Texture("codex-rule"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspect, CustomMinimumSize = new Vector2(120, 10), SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
                MouseFilter = MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.LinearWithMipmaps });
            return stack;
        }
        var ledger = Panel("The merchant's ledger", 1.1f);
        _ledgerRows = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _ledgerRows.AddThemeConstantOverride("separation", 0);
        ledger.AddChild(_ledgerRows);
        // The storehouse itself fills what the rows leave, framed like a picture on the ledger's page.
        var picture = new Control { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 40), ClipContents = true, MouseFilter = MouseFilterEnum.Ignore };
        var painting = new TextureRect { Texture = RoyalArt.Item("activity-store"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = MouseFilterEnum.Ignore, SelfModulate = new Color(.9f, .9f, .9f) };
        picture.AddChild(painting);
        painting.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var rim = new Panel { MouseFilter = MouseFilterEnum.Ignore };
        rim.AddThemeStyleboxOverride("panel", StorePackCard.Kit("tile", 6, center: false));
        picture.AddChild(rim);
        rim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ledger.AddChild(picture);
        var latest = Panel("Latest purchase", 1f);
        _statusLabel = StorePackCard.Serif("", 14, Body, 600);
        _statusLabel.Name = "PurchaseStatus";
        latest.AddChild(_statusLabel);
        // Until something is bought, the panel shows an empty state (icon, title, one line) in the space the result takes.
        var empty = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
        empty.AddThemeConstantOverride("separation", 6);
        empty.AddChild(new TextureRect { Texture = RoyalKit.Texture("storetab-ledger"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(34, 26), SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            SelfModulate = new Color(1, 1, 1, .55f), MouseFilter = MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.LinearWithMipmaps });
        var none = StorePackCard.Serif("Nothing bought yet", 15, Body, 600);
        none.HorizontalAlignment = HorizontalAlignment.Center;
        empty.AddChild(none);
        var hint = StorePackCard.Serif("Each purchase's result is written here.", 12, Muted);
        hint.HorizontalAlignment = HorizontalAlignment.Center;
        empty.AddChild(hint);
        latest.AddChild(_statusPlaceholder = empty);
        latest.AddChild(_statusSpacer = new Control { SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        foreach (var (glyph, text) in new[] {
            ("store-secure", "Packs are added once the store confirms your payment."),
            ("store-once", "The Adventurer's Kit can be bought once per account.") })
            latest.AddChild(Note(glyph, text));
        return page;
    }

    private static HBoxContainer Note(string glyph, string text)
    {
        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 10);
        row.AddChild(new TextureRect { Texture = RoyalKit.Texture(glyph), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(16, 18), SizeFlagsVertical = SizeFlags.ShrinkCenter, SelfModulate = new Color(1, 1, 1, .8f), MouseFilter = MouseFilterEnum.Ignore,
            TextureFilter = TextureFilterEnum.LinearWithMipmaps });
        var label = StorePackCard.Serif(text, 12, Muted);
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(label);
        return row;
    }

    private void BuildFooter(VBoxContainer root)
    {
        // One fixed height whatever the footer holds, so the cards above never change size.
        var notice = new HBoxContainer
        {
            Name = "StorehouseFooter",
            CustomMinimumSize = new Vector2(0, 32)
        };
        notice.AddThemeConstantOverride("separation", 12);
        // Ends where the cards end, clear of the frame's rim.
        var footer = new MarginContainer();
        footer.AddThemeConstantOverride("margin_right", 8);
        root.AddChild(footer);
        footer.AddChild(notice);
        // The gold-funded refill and its recharge share the footer on the Rations page, where four
        // packs already fill the page. They make way while a pack waits for its confirmation.
        _refillRow = new HBoxContainer { Name = "RationRefill" };
        _refillRow.AddThemeConstantOverride("separation", 12);
        notice.AddChild(_refillRow);
        var refill = FooterButton(new Vector2(176, 32), "navy", () =>
        {
            AudioDirector.Purchased(GameState.Instance.TryBuyFoodRefill(out var message));
            RefreshUi();
            _noticeLabel.Text = message;
        });
        refill.Arrange(StorePackCard.Caps("+10 food", 12, RoyalText.Cream, 700), HomeMapArt.Icon("food"), new Vector2(22, 22),
            StorePackCard.Caps("100", 12, RoyalText.Cream, 700), HomeMapArt.Icon("gold"), new Vector2(18, 18), gap: 6, rule: 10);
        refill.Text = "10 food · 100 gold";
        refill.AccessibilityName = refill.TooltipText = "Buy 10 food for 100 gold";
        _refillRow.AddChild(refill);
        var recharge = StorePackCard.Serif("+2 food every 5 minutes · Up to 24", 12, Muted);
        recharge.AutowrapMode = TextServer.AutowrapMode.Off;
        recharge.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        recharge.VerticalAlignment = VerticalAlignment.Center;
        _refillRow.AddChild(recharge);
        _noticeLabel = StorePackCard.Serif("", 13, Muted);
        _noticeLabel.Name = "PurchaseNotice";
        _noticeLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _noticeLabel.VerticalAlignment = VerticalAlignment.Center;
        _noticeLabel.MaxLinesVisible = 2;
        notice.AddChild(_noticeLabel);
    }

    private static RoyalButton FooterButton(Vector2 size, string material, Action action)
    {
        var button = RoyalButton.Over(new Rect2(Vector2.Zero, size), "", action, 5);
        button.CustomMinimumSize = size;
        button.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        button.SetStates(StorePackCard.Kit("button-" + material, 7), 5);
        return button;
    }

    private void SelectCategory(int index)
    {
        _selectedCategory = index;
        for (var i = 0; i < _pages.Length; i++)
            _pages[i].Visible = i == index;
        StyleTabs();
        ShowRefill();
        RebuildResourcesRow();
    }

    /// <summary>The achievements screen's tabs: slate with a cream glyph, the current page lit gold.</summary>
    private void StyleTabs()
    {
        for (var i = 0; i < _tabs.Count; i++)
        {
            var tab = _tabs[i];
            var selected = i == _selectedCategory;
            tab.SetStates(StorePackCard.Kit(selected ? "ach-tab-selected" : "ach-tab", 7), 4);
            tab.MarkTab(selected);
            var caption = new RoyalLabel { Text = Categories[i], Font = RoyalFonts.Body(600), FontSize = 14, CapRatio = .58f, MinimumFontSize = 12,
                Ink = selected ? new Color("f4eabf") : new Color("dcdddf"), ShadowOffset = new Vector2(0, 1), ShadowInk = new Color(0, 0, 0, .6f) };
            tab.Arrange(caption, RoyalKit.Texture(TabGlyphs[i]), new Vector2(18, 18), gap: 7);
            tab.Glyph.SelfModulate = selected ? Colors.White : new Color(.88f, .88f, .9f);
        }
    }

    private void ShowRefill()
    {
        _refillRow.Visible = _selectedCategory == RationsPage;
        // Beside the refill, the notice keeps to the far end so a result doesn't run on from the recharge.
        _noticeLabel.HorizontalAlignment = _refillRow.Visible ? HorizontalAlignment.Right : HorizontalAlignment.Left;
    }

    private void RefreshUi(bool preserveStatus = false)
    {
        if (!preserveStatus) _noticeLabel.Text = "";
        RebuildResourcesRow();
        for (var i = 0; i < PackCategories.Length; i++)
            RebuildCategory(i, PackCategories[i]);
        RefreshStatus();
    }

    /// <summary>
    /// The home strip's parchment balances: gold, then rations against their recharge cap, or relic shards
    /// on the Shards page.
    /// </summary>
    private void RebuildResourcesRow()
    {
        RealmUi.Clear(_balances);
        const float Height = 40, Icon = 24, Gap = 6, End = 20, Spacing = 10, Divider = 6;
        var state = GameState.Instance;
        var bar = new Panel { MouseFilter = MouseFilterEnum.Ignore };
        bar.AddThemeStyleboxOverride("panel", RoyalKit.Scaled("res-bar", MobilePresentation.HudScale, 19, 0, 19, 0));
        _balances.AddChild(bar);
        var x = End;
        var shards = _selectedCategory == ShardsPage;
        var pairs = new[] { (HomeMapArt.Icon("gold"), state.Gold.ToString("N0")),
            shards ? (RoyalKit.Texture("forge-shard"), state.RelicShards.ToString("N0")) : (HomeMapArt.Icon("food"), $"{state.Food}/{GameState.FoodRechargeCap}") };
        for (var i = 0; i < pairs.Length; i++)
        {
            if (i > 0)
            {
                _balances.AddChild(RoyalKit.Image("res-divider", new Rect2(x + Spacing, 7, Divider, Height - 14)));
                x += 2 * Spacing + Divider;
            }
            _balances.AddChild(new TextureRect { Texture = pairs[i].Item1, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, Position = new Vector2(x, (Height - Icon) / 2), Size = new Vector2(Icon, Icon),
                MouseFilter = MouseFilterEnum.Ignore });
            x += Icon + Gap;
            var value = new RoyalLabel { Text = pairs[i].Item2, Font = RoyalFonts.Body(700), FontSize = 16, CapRatio = .62f, Ink = new Color("1b1610"), ShrinkToFit = false };
            var width = Mathf.Ceil(value.TextWidth(value.FontSize));
            value.Position = new Vector2(x, 0); value.Size = new Vector2(width + 2, Height);
            _balances.AddChild(value);
            x += width;
        }
        x += End;
        bar.Size = new Vector2(x, Height);
        _balances.CustomMinimumSize = new Vector2(x, Height);
        _balances.TooltipText = _balances.AccessibilityName = $"Gold: {state.Gold:N0} · " + (shards ? $"Relic shards: {state.RelicShards:N0}" : state.FoodRechargeText);
    }

    private void RebuildCategory(int page, string category)
    {
        var row = _pages[page];
        RealmUi.Clear(row);
        var products = ShopProductCatalog.GetByCategory(category);
        // Where there is a choice of sizes, the pack that gives the most for the money is marked.
        var best = products.Count >= 3 ? products.OrderByDescending(p => p.TotalCurrencyAmount / Math.Max(.01, p.PriceUsd)).First() : null;
        foreach (var product in products)
        {
            var card = new StorePackCard(product, product.OneTimePurchase && GameState.Instance.HasPurchasedProduct(product.Id), product == best,
                LocalizedPrice(product), wide: products.Count <= 2, OnPurchasePressed);
            row.AddChild(card);
            if (GameState.Instance.PurchasesPaused && card.Buy != null) card.Buy.Disabled = true;
        }
    }

    private static string LocalizedPrice(ShopProduct product) => NativeIAPService.Instance?.GetLocalizedPrice(product.Id) ?? product.FormattedPrice;
    private void OnPurchasePressed(StorePackCard card)
    {
        var productId = card.Product.Id;
        if (GameState.Instance.PurchasesPaused)
        {
            _noticeLabel.Text = GameState.Instance.PurchasesPausedMessage;
            return;
        }
        if (_nativePurchaseBusy)
        {
            _noticeLabel.Text = "Finishing your last purchase. Try again in a moment.";
            return;
        }

        // One tap starts the purchase: the App Store's or Google Play's own payment sheet is the confirmation.
        SelectCategory(LedgerPage);
        _noticeLabel.Text = "Your purchase status is shown above.";
        ExecutePurchase(productId);
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
#if !GODOT_MOBILE
        if (platform == "stripe")
        {
            ExecuteStripePurchase(product);
            return;
        }
#endif

        // Native IAP path (Apple/Google)
        if (nativeIap != null && nativeIap.IsAvailable && nativeIap.Platform != IAPPlatform.Stripe)
        {
            ExecuteNativeIAPPurchase(product, platform);
            return;
        }

        _statusLabel.Text = "Native billing is unavailable. This purchase has not been charged.";
        RefreshUi(preserveStatus: true);
    }

    // Web checkout is for desktop and web builds only; phones always buy through Apple or Google.
#if !GODOT_MOBILE
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
        RefreshStatus();
    }
#endif

    private void ExecuteNativeIAPPurchase(ShopProduct product, string platform)
    {
        _nativePurchaseBusy = true;
        _statusLabel.Text = $"Starting purchase: {product.DisplayName}...";
        RefreshStatus();
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
            RefreshStatus();
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
            if (result.ShardsCredited > 0)
                _statusLabel.Text += $"+{result.ShardsCredited} Shards  ";
            if (result.GrantedUnitUnlock && GameState.Instance.LastKitUnitId.Length > 0)
                _statusLabel.Text += $"\n+ {GameData.GetUnit(GameState.Instance.LastKitUnitId).DisplayName} recruited";
            AudioDirector.Instance?.PlayRewardClaim();
            PromptGuestSignIn();
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

    /// <summary>Rewrites the ledger's rows. The latest purchase message is left as it is.</summary>
    private void RefreshStatus()
    {
        RealmUi.Clear(_ledgerRows);
        void Row(string name, string value)
        {
            // Ruled like a ledger: each entry sits on a faint line.
            if (_ledgerRows.GetChildCount() > 0)
                _ledgerRows.AddChild(new ColorRect { Color = new Color(.85f, .72f, .5f, .14f), CustomMinimumSize = new Vector2(0, 1), MouseFilter = MouseFilterEnum.Ignore });
            var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(0, 25) };
            var key = StorePackCard.Serif(name, 13, Muted);
            key.AutowrapMode = TextServer.AutowrapMode.Off;
            key.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            key.VerticalAlignment = VerticalAlignment.Center;
            var amount = StorePackCard.Serif(value, 13, Body, 600);
            amount.AutowrapMode = TextServer.AutowrapMode.Off;
            amount.HorizontalAlignment = HorizontalAlignment.Right;
            amount.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(key); row.AddChild(amount);
            _ledgerRows.AddChild(row);
        }

        Row("Purchases made", GameState.Instance.TotalPurchaseCount.ToString("N0"));
        var refunds = GameState.Instance.GetAppliedWalletAdjustments().Count(a => a.Kind == "refund" && (a.GoldApplied != 0 || a.FoodApplied != 0 || a.ShardsApplied != 0
            || a.GoldUnrecovered != 0 || a.FoodUnrecovered != 0 || a.ShardsUnrecovered != 0));
        if (refunds > 0)
            Row("Refunded purchases taken back", refunds.ToString("N0"));
        var endpoint = GameState.Instance.PurchaseValidationEndpoint;
        Row("Store", string.IsNullOrWhiteSpace(endpoint) ? "Disabled in this build" : "Online");
        var nativeIap = NativeIAPService.Instance;
        if (nativeIap != null)
        {
            var platformLabel = nativeIap.Platform switch
            {
                IAPPlatform.Apple => "Apple StoreKit",
                IAPPlatform.Google => "Google Play",
#if !GODOT_MOBILE
                IAPPlatform.Stripe => "Stripe (web)",
#endif
                _ => "None"
            };
            Row("Payment", platformLabel);
            if (nativeIap.Platform != IAPPlatform.Stripe)
            {
                Row("Store connection", nativeIap.IsAvailable ? "Connected" : "Unavailable");
            }
        }

        _statusPlaceholder.Visible = string.IsNullOrWhiteSpace(_statusLabel.Text);
        _statusLabel.Visible = _statusSpacer.Visible = !_statusPlaceholder.Visible;
    }

    private static string DetectPlatform()
    {
        if (OS.HasFeature("ios"))
            return "apple";
        if (OS.HasFeature("android"))
            return "google";
        return "stripe";
    }
}
