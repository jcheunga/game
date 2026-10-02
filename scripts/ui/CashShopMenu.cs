using System;
using System.Collections.Generic;
using Godot;

public partial class CashShopMenu : Control
{
	private ColorRect _backgroundTop = null!;
	private ColorRect _backgroundBottom = null!;
	private ColorRect _accentBand = null!;
	private MenuBackdropSet _menuBackdrop = null!;
	private PanelContainer _titlePanel = null!;
	private PanelContainer _goldPanel = null!;
	private PanelContainer _foodPanel = null!;
	private PanelContainer _mixedPanel = null!;
	private PanelContainer _statusPanel = null!;
	private HBoxContainer _resourcesRow = null!;
	private Label _statusLabel = null!;
	private VBoxContainer _goldStack = null!;
	private VBoxContainer _foodStack = null!;
	private VBoxContainer _mixedStack = null!;
	private HBoxContainer _categoryTabs = null!;
	private Label _noticeLabel = null!;
	private Button _cancelConfirmButton = null!;
	private bool _embedded;
	private int _selectedCategory;
	private string _pendingConfirmProductId = "";
	private Button _pendingConfirmButton;

	public override void _Ready()
	{
		_embedded = RealmModal.Embedded(this);
		if (_embedded) BuildModalUi(); else BuildUi();
		RefreshUi();
		if (!_embedded) AnimateEntrance(new Control[] { _titlePanel, _goldPanel, _foodPanel, _mixedPanel, _statusPanel });
	}

	private void BuildModalUi()
	{
		var root = new VBoxContainer { Name = "StorehouseLayout" };
		root.AddThemeConstantOverride("separation", 10);
		AddChild(root);
		root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_resourcesRow = new HBoxContainer();
		_resourcesRow.AddThemeConstantOverride("separation", 16);
		root.AddChild(_resourcesRow);
		BuildCatalog(root);
		RealmModal.Polish(root);
	}

	private void AnimateEntrance(Control[] panels)
	{
		for (var i = 0; i < panels.Length; i++)
		{
			var panel = panels[i];
			if (panel == null) continue;
			panel.Modulate = new Color(1f, 1f, 1f, 0f);
			var delay = 0.06f + (i * 0.05f);
			var tween = CreateTween();
			tween.TweenProperty(panel, "modulate:a", 1f, 0.22f)
				.SetDelay(delay)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
		}
	}

	private void BuildUi()
	{
		_menuBackdrop = MenuBackdropComposer.AddSplitBackdrop(
			this,
			"cash_shop",
			new Color("1a1a2e"),
			new Color("16213e"),
			new Color("e2b714"),
			104f);
		_backgroundTop = _menuBackdrop.PrimaryRect;
		_backgroundBottom = _menuBackdrop.SecondaryRect;
		_accentBand = _menuBackdrop.AccentBand;

		// Title panel
		_titlePanel = new PanelContainer
		{
			Position = new Vector2(24f, 20f),
			Size = new Vector2(1232f, 82f)
		};
		AddChild(_titlePanel);

		var titleRow = new HBoxContainer();
		titleRow.AddThemeConstantOverride("separation", 16);
		_titlePanel.AddChild(titleRow);

		titleRow.AddChild(new Label
		{
			Text = "Royal Storehouse",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			VerticalAlignment = VerticalAlignment.Center
		});

		_resourcesRow = new HBoxContainer();
		_resourcesRow.AddThemeConstantOverride("separation", 12);
		titleRow.AddChild(_resourcesRow);

        var body = new VBoxContainer { Name = "StorehouseLayout", Position = new Vector2(24, 122), Size = new Vector2(1232, 480) };
        body.AddThemeConstantOverride("separation", 12);
        AddChild(body);
        BuildCatalog(body);

		// Bottom nav
		var bottomPanel = new PanelContainer
		{
            Position = new Vector2(24f, 618f),
            Size = new Vector2(1232f, 76f)
		};
		AddChild(bottomPanel);

		var bottomRow = new HBoxContainer();
		bottomRow.AddThemeConstantOverride("separation", 12);
		bottomPanel.AddChild(bottomRow);

		var titleButton = new RealmButton
		{
			Text = "Back To Title",
			CustomMinimumSize = new Vector2(180f, 0f)
		};
		titleButton.Pressed += () => SceneRouter.Instance.GoToMainMenu();
		bottomRow.AddChild(titleButton);

		var mapButton = new RealmButton
		{
			Text = "Back To Map",
			CustomMinimumSize = new Vector2(180f, 0f)
		};
		mapButton.Pressed += () => SceneRouter.Instance.GoToMap();
		bottomRow.AddChild(mapButton);

		var armoryButton = new RealmButton
		{
			Text = "Caravan Armory",
			CustomMinimumSize = new Vector2(180f, 0f)
		};
		armoryButton.Pressed += () => SceneRouter.Instance.GoToShop();
		bottomRow.AddChild(armoryButton);

		var settingsButton = new RealmButton
		{
			Text = "Settings",
			CustomMinimumSize = new Vector2(140f, 0f)
		};
		settingsButton.Pressed += () => SceneRouter.Instance.GoToSettings();
		bottomRow.AddChild(settingsButton);

		bottomRow.AddChild(new Control
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		});

		var multiplayerButton = new RealmButton
		{
			Text = "Multiplayer",
			CustomMinimumSize = new Vector2(160f, 0f)
		};
		multiplayerButton.Pressed += () => SceneRouter.Instance.GoToMultiplayer();
		bottomRow.AddChild(multiplayerButton);
	}

    private void BuildCatalog(VBoxContainer body)
    {
        _categoryTabs = RealmUi.Tabs(body, SelectCategory, "Gold", "Rations", "Bundles", "Purchase info");
        // The pages take the remaining space; purchase actions stay inside each card.
        var pages = new Control { Name = "CatalogPages", SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddChild(pages);
        _goldStack = CreateCatalogPage(pages, out _goldPanel);
        _foodStack = CreateCatalogPage(pages, out _foodPanel);
        _mixedStack = CreateCatalogPage(pages, out _mixedPanel);
        var info = CreateCatalogPage(pages, out _statusPanel);
        var statusStack = RealmUi.Scroll(info);
        statusStack.AddChild(RealmUi.Heading("The merchant's ledger"));
        _statusLabel = RealmUi.Label("");
        statusStack.AddChild(_statusLabel);

        var notice = new HBoxContainer { CustomMinimumSize = new Vector2(0, 40) };
        notice.AddThemeConstantOverride("separation", 12);
        body.AddChild(notice);
        _noticeLabel = RealmUi.Label("Choose a pack. Tap Buy twice to confirm.", 18, true);
        _noticeLabel.Name = "PurchaseNotice";
        _noticeLabel.VerticalAlignment = VerticalAlignment.Center;
        notice.AddChild(_noticeLabel);
        _cancelConfirmButton = new RealmButton { Text = "Cancel", Visible = false, CustomMinimumSize = new Vector2(88, 40) };
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
        var stack = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        panel.AddChild(stack);
        return stack;
    }

    private void SelectCategory(int index)
    {
        if (index != _selectedCategory) CancelPendingPurchase();
        _selectedCategory = index;
        var pages = new[] { _goldPanel, _foodPanel, _mixedPanel, _statusPanel };
        for (var i = 0; i < pages.Length; i++) pages[i].Visible = i == index;
        _categoryTabs.GetChild<Button>(index).ButtonPressed = true;
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

		_resourcesRow.AddChild(UiBadgeFactory.CreateRewardMetric("gold", "", GameState.Instance.Gold.ToString("N0"), new Vector2(24f, 24f)));
		_resourcesRow.AddChild(new FoodBalance());
		if (_embedded) RealmModal.Polish(_resourcesRow);
	}

    private void RebuildGoldPacks() => RebuildCategory(_goldStack, "gold");
    private void RebuildFoodPacks()
    {
        RebuildCategory(_foodStack, "food");
        var refillRow = new HBoxContainer();
        refillRow.AddThemeConstantOverride("separation", 16);
        var refill = RealmUi.Button("food", "10 food · 100 gold", () => {
            GameState.Instance.TryBuyFoodRefill(out var message);
            RefreshUi();
            _noticeLabel.Text = message;
        }, true);
        refillRow.AddChild(refill);
        var recharge = RealmUi.Label("+2 food every 5 minutes · Up to 24", 18, true);
        recharge.VerticalAlignment = VerticalAlignment.Center;
        refillRow.AddChild(recharge);
        _foodStack.AddChild(refillRow); _foodStack.MoveChild(refillRow, 0);
        if (_embedded) RealmModal.Polish(refillRow);
    }
    private void RebuildMixedPacks() => RebuildCategory(_mixedStack, "mixed");

    private void RebuildCategory(VBoxContainer stack, string category)
    {
        ClearChildren(stack);
        stack.AddThemeConstantOverride("separation", 8);
        var row = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 12);
        stack.AddChild(row);
        foreach (var product in ShopProductCatalog.GetByCategory(category))
            AddProductCard(row, product, product.OneTimePurchase && GameState.Instance.HasPurchasedProduct(product.Id));
        if (_embedded) RealmModal.Polish(row);
    }

    private void AddProductCard(Control host, ShopProduct product, bool forceDisabled)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        host.AddChild(panel);
        var card = new VBoxContainer();
        card.AddThemeConstantOverride("separation", 6);
        panel.AddChild(card);
        var top = new HBoxContainer();
        card.AddChild(top);
        top.AddChild(new HeraldicEmblem { Symbol = product.Category == "gold" ? "crown" : product.Category == "food" ? "food" : "gift", CustomMinimumSize = new Vector2(40, 40) });
        top.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var details = new RealmButton { Text = "Details", CustomMinimumSize = new Vector2(82, 40), AccessibilityName = product.DisplayName + " details", TooltipText = product.DisplayName + " details" };
        details.Pressed += () => RealmUi.Details(this, product.DisplayName,
            $"{product.FormattedReward}\n\n{product.Description}\n\nPrice: {LocalizedPrice(product)}" + (product.OneTimePurchase ? "\nAvailable once per account." : ""));
        top.AddChild(details);
        var title = RealmUi.Heading(product.DisplayName, 18);
        title.CustomMinimumSize = new Vector2(0, 56);
        title.VerticalAlignment = VerticalAlignment.Center;
        card.AddChild(title);
        card.AddChild(BuildProductRewardRow(product));
        var extra = product.GrantsUnitUnlock ? "+ Unit unlock" : product.BonusAmount > 0 ? $"Includes {product.BonusAmount:N0} bonus" : "";
        var bonus = RealmUi.Label(extra, 18, true);
        bonus.CustomMinimumSize = new Vector2(0, 24);
        card.AddChild(bonus);
        card.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        var localizedPrice = LocalizedPrice(product);
        var purchaseButton = new RealmButton { Text = forceDisabled ? "Purchased" : $"Buy — {localizedPrice}", CustomMinimumSize = new Vector2(0, 48), Disabled = forceDisabled };
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

		var reward = RealmUi.Label(product.CurrencyType == "mixed"
			? $"{product.GoldAmount:N0} gold · {product.FoodAmount:N0} food"
			: $"{product.TotalCurrencyAmount:N0} {product.CurrencyType}", 24);
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
			if (product != null) _pendingConfirmButton.Text = $"Buy — {LocalizedPrice(product)}";
		}
		_pendingConfirmProductId = "";
		_pendingConfirmButton = null;
		_cancelConfirmButton.Hide();
		if (resetNotice) _noticeLabel.Text = "Choose a pack. Tap Buy twice to confirm.";
	}

	private void OnPurchasePressed(string productId, Button button)
	{
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

		if (string.IsNullOrWhiteSpace(GameState.Instance.PlayerAuthToken) &&
			!PlayerProfileSyncService.RefreshProfileForBackendEndpoint(endpoint, out var sessionMessage))
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
				_statusLabel.Text = $"Opening payment page for {product.DisplayName}...\n" +
					$"Price: ${checkout.PriceCents / 100.0:F2}\n\n" +
					"Complete payment in your browser.\n" +
					"Your account will be credited automatically.";
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
		_statusLabel.Text = $"Starting purchase: {product.DisplayName}...";

		NativeIAPService.Instance.PurchaseProduct(product.Id, (iapResult) =>
		{
			if (!iapResult.Success)
			{
				_statusLabel.Text = $"Purchase cancelled: {iapResult.ErrorMessage}";
				RefreshUi(preserveStatus: true);
				return;
			}

			// Validate with server
			_statusLabel.Text = $"Validating receipt for {product.DisplayName}...";

			var result = GameState.Instance.ValidatePurchaseWithServer(
				iapResult.ProductId,
				platform,
				iapResult.ReceiptToken,
				iapResult.TransactionId
			);

			if (result.Status == "ok")
			{
				GameState.Instance.TryApplyPurchaseReward(result);
				NativeIAPService.Instance?.ConfirmServerFulfillment(iapResult);
				_statusLabel.Text = $"Purchased {product.DisplayName}!\n";
				if (result.GoldCredited > 0) _statusLabel.Text += $"+{result.GoldCredited} Gold  ";
				if (result.FoodCredited > 0) _statusLabel.Text += $"+{result.FoodCredited} Food  ";
				if (result.GrantedUnitUnlock) _statusLabel.Text += "\n+ Unit unlock granted!";
				AudioDirector.Instance?.PlayUpgradeConfirm();
			}
			else
			{
				_statusLabel.Text = $"Validation failed: {result.Message}";
			}

			RefreshUi(preserveStatus: true);
		});
	}

	private void RefreshStatus()
	{
		if (!string.IsNullOrWhiteSpace(_statusLabel.Text) && _statusLabel.Text.Contains("Purchased"))
		{
			return;
		}

		var lines = new List<string>
		{
			$"Purchases: {GameState.Instance.TotalPurchaseCount}",
			$"Gold: {GameState.Instance.Gold}",
			$"Food: {GameState.Instance.Food}"
		};

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
		if (OS.HasFeature("ios")) return "apple";
		if (OS.HasFeature("android")) return "google";
		return "stripe";
	}

	private static void ClearChildren(Control parent) => RealmUi.Clear(parent);
}
