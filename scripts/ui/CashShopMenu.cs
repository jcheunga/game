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
	private string _pendingConfirmProductId = "";
	private Button _pendingConfirmButton;

	public override void _Ready()
	{
		BuildUi();
		RefreshUi();
		AnimateEntrance(new Control[] { _titlePanel, _goldPanel, _foodPanel, _mixedPanel, _statusPanel });
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

        var body = new VBoxContainer { Position = new Vector2(24, 122), Size = new Vector2(1232, 510) };
        body.AddThemeConstantOverride("separation", 12);
        AddChild(body);
        _categoryTabs = RealmUi.Tabs(body, SelectCategory, "Gold", "Rations", "Bundles", "Purchase info");
        var pages = new Control { SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddChild(pages);
        _goldStack = CreateCatalogPage(pages, out _goldPanel);
        _foodStack = CreateCatalogPage(pages, out _foodPanel);
        _mixedStack = CreateCatalogPage(pages, out _mixedPanel);
        var info = CreateCatalogPage(pages, out _statusPanel);
        var statusStack = RealmUi.Scroll(info);
        statusStack.AddChild(RealmUi.Heading("The merchant's ledger"));
        _statusLabel = RealmUi.Label("");
        statusStack.AddChild(_statusLabel);
        SelectCategory(0);

		// Bottom nav
		var bottomPanel = new PanelContainer
		{
			Position = new Vector2(24f, 660f),
			Size = new Vector2(1232f, 56f)
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

    private static VBoxContainer CreateCatalogPage(Control host, out PanelContainer panel)
    {
        panel = new PanelContainer();
        host.AddChild(panel);
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var stack = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        panel.AddChild(stack);
        return stack;
    }

    private void SelectCategory(int index)
    {
        var pages = new[] { _goldPanel, _foodPanel, _mixedPanel, _statusPanel };
        for (var i = 0; i < pages.Length; i++) pages[i].Visible = i == index;
    }

	private void RefreshUi(bool preserveStatus = false)
	{
		_pendingConfirmButton = null;
		_pendingConfirmProductId = "";
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
		_resourcesRow.AddChild(UiBadgeFactory.CreateRewardMetric("food", "", GameState.Instance.Food.ToString("N0"), new Vector2(24f, 24f)));
	}

    private void RebuildGoldPacks() => RebuildCategory(_goldStack, "gold");
    private void RebuildFoodPacks() => RebuildCategory(_foodStack, "food");
    private void RebuildMixedPacks() => RebuildCategory(_mixedStack, "mixed");

    private void RebuildCategory(VBoxContainer stack, string category)
    {
        ClearChildren(stack);
        var row = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 12);
        stack.AddChild(row);
        foreach (var product in ShopProductCatalog.GetByCategory(category))
            AddProductCard(row, product, product.OneTimePurchase && GameState.Instance.HasPurchasedProduct(product.Id));
    }

    private void AddProductCard(Control host, ShopProduct product, bool forceDisabled)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        host.AddChild(panel);
        var card = new VBoxContainer();
        card.AddThemeConstantOverride("separation", 12);
        panel.AddChild(card);
        card.AddChild(new HeraldicEmblem { Symbol = product.Category == "gold" ? "crown" : product.Category == "food" ? "food" : "gift", CustomMinimumSize = new Vector2(64, 64), SizeFlagsHorizontal = SizeFlags.ShrinkCenter });
        var title = RealmUi.Heading(product.DisplayName, 24);
        title.CustomMinimumSize = new Vector2(0, 66);
        card.AddChild(title);
        card.AddChild(BuildProductRewardRow(product));
        card.AddChild(RealmUi.Label(product.Description));
        card.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        card.AddChild(RealmUi.Label(string.IsNullOrWhiteSpace(product.ValueLabel) ? "For the road ahead" : product.ValueLabel, 18, true));
        var localizedPrice = NativeIAPService.Instance?.GetLocalizedPrice(product.Id) ?? product.FormattedPrice;
        var purchaseButton = new RealmButton { Text = forceDisabled ? "Purchased" : $"Buy — {localizedPrice}", CustomMinimumSize = new Vector2(0, 48), Disabled = forceDisabled };
        purchaseButton.Pressed += () => OnPurchasePressed(product.Id, purchaseButton);
        card.AddChild(purchaseButton);
    }

	private static HBoxContainer BuildProductRewardRow(ShopProduct product)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);

		if (product.CurrencyType.Equals("gold", StringComparison.OrdinalIgnoreCase))
		{
			row.AddChild(UiBadgeFactory.CreateRewardBadge("gold", "", product.FormattedReward, new Vector2(34f, 34f)));
		}
		else if (product.CurrencyType.Equals("food", StringComparison.OrdinalIgnoreCase))
		{
			row.AddChild(UiBadgeFactory.CreateRewardBadge("food", "", product.FormattedReward, new Vector2(34f, 34f)));
		}
		else if (product.CurrencyType.Equals("mixed", StringComparison.OrdinalIgnoreCase))
		{
			if (product.GoldAmount > 0)
			{
				row.AddChild(UiBadgeFactory.CreateRewardBadge("gold", "", $"{product.GoldAmount} Gold", new Vector2(34f, 34f)));
			}
			if (product.FoodAmount > 0)
			{
				row.AddChild(UiBadgeFactory.CreateRewardBadge("food", "", $"{product.FoodAmount} Food", new Vector2(34f, 34f)));
			}
		}

		if (product.GrantsUnitUnlock)
		{
			row.AddChild(UiBadgeFactory.CreateRewardBadge("unit", "", "Unit Unlock", new Vector2(34f, 34f)));
		}

		row.AddChild(new Label
		{
			Text = product.FormattedReward,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			VerticalAlignment = VerticalAlignment.Center,
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		});
		return row;
	}

	private void OnPurchasePressed(string productId, Button button)
	{
		if (_pendingConfirmProductId == productId)
		{
			SelectCategory(3);
			_categoryTabs.GetChild<Button>(3).ButtonPressed = true;
			ExecutePurchase(productId);
			_pendingConfirmProductId = "";
			_pendingConfirmButton = null;
			return;
		}

		// Reset previous confirm
		if (_pendingConfirmButton != null)
		{
			var prevProduct = ShopProductCatalog.GetById(_pendingConfirmProductId);
			if (prevProduct != null)
			{
				_pendingConfirmButton.Text = $"Buy — {prevProduct.FormattedPrice}";
			}
		}

		// Show confirm state
		_pendingConfirmProductId = productId;
		_pendingConfirmButton = button;
		button.Text = "Confirm purchase";
		_statusLabel.Text = "Tap the purchase button a second time to confirm.";
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
		lines.Add("All packs are consumable and credit your account immediately.");

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
