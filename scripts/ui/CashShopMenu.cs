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

		// Gold packs panel
		_goldPanel = new PanelContainer
		{
			Position = new Vector2(24f, 122f),
			Size = new Vector2(380f, 520f)
		};
		AddChild(_goldPanel);

		var goldPadding = CreatePaddedContainer();
		_goldPanel.AddChild(goldPadding);

		var goldScroll = new ScrollContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		goldPadding.AddChild(goldScroll);

		_goldStack = new VBoxContainer();
		_goldStack.AddThemeConstantOverride("separation", 12);
		goldScroll.AddChild(_goldStack);

		// Food packs panel
		_foodPanel = new PanelContainer
		{
			Position = new Vector2(420f, 122f),
			Size = new Vector2(380f, 520f)
		};
		AddChild(_foodPanel);

		var foodPadding = CreatePaddedContainer();
		_foodPanel.AddChild(foodPadding);

		var foodScroll = new ScrollContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		foodPadding.AddChild(foodScroll);

		_foodStack = new VBoxContainer();
		_foodStack.AddThemeConstantOverride("separation", 12);
		foodScroll.AddChild(_foodStack);

		// Mixed / starter packs panel
		_mixedPanel = new PanelContainer
		{
			Position = new Vector2(816f, 122f),
			Size = new Vector2(220f, 280f)
		};
		AddChild(_mixedPanel);

		var mixedPadding = CreatePaddedContainer();
		_mixedPanel.AddChild(mixedPadding);

		var mixedScroll = new ScrollContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		mixedPadding.AddChild(mixedScroll);

		_mixedStack = new VBoxContainer();
		_mixedStack.AddThemeConstantOverride("separation", 12);
		mixedScroll.AddChild(_mixedStack);

		// Status panel
		_statusPanel = new PanelContainer
		{
			Position = new Vector2(816f, 418f),
			Size = new Vector2(220f, 224f)
		};
		AddChild(_statusPanel);

		var statusPadding = CreatePaddedContainer();
		_statusPanel.AddChild(statusPadding);

		var statusStack = new VBoxContainer();
		statusStack.AddThemeConstantOverride("separation", 8);
		statusPadding.AddChild(statusStack);

		statusStack.AddChild(new Label { Text = "Purchase Info" });

		_statusLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(0f, 120f)
		};
		statusStack.AddChild(_statusLabel);

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

		var titleButton = new Button
		{
			Text = "Back To Title",
			CustomMinimumSize = new Vector2(180f, 0f)
		};
		titleButton.Pressed += () => SceneRouter.Instance.GoToMainMenu();
		bottomRow.AddChild(titleButton);

		var mapButton = new Button
		{
			Text = "Back To Map",
			CustomMinimumSize = new Vector2(180f, 0f)
		};
		mapButton.Pressed += () => SceneRouter.Instance.GoToMap();
		bottomRow.AddChild(mapButton);

		var armoryButton = new Button
		{
			Text = "Caravan Armory",
			CustomMinimumSize = new Vector2(180f, 0f)
		};
		armoryButton.Pressed += () => SceneRouter.Instance.GoToShop();
		bottomRow.AddChild(armoryButton);

		var settingsButton = new Button
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

		var multiplayerButton = new Button
		{
			Text = "Multiplayer",
			CustomMinimumSize = new Vector2(160f, 0f)
		};
		multiplayerButton.Pressed += () => SceneRouter.Instance.GoToMultiplayer();
		bottomRow.AddChild(multiplayerButton);
	}

	private void RefreshUi()
	{
		RebuildResourcesRow();
		RebuildGoldPacks();
		RebuildFoodPacks();
		RebuildMixedPacks();
		RefreshStatus();
	}

	private void RebuildResourcesRow()
	{
		foreach (var child in _resourcesRow.GetChildren())
		{
			child.QueueFree();
		}

		_resourcesRow.AddChild(UiBadgeFactory.CreateRewardMetric("gold", "", GameState.Instance.Gold.ToString("N0"), new Vector2(24f, 24f)));
		_resourcesRow.AddChild(UiBadgeFactory.CreateRewardMetric("food", "", GameState.Instance.Food.ToString("N0"), new Vector2(24f, 24f)));
	}

	private void RebuildGoldPacks()
	{
		ClearChildren(_goldStack);
		_goldStack.AddChild(new Label { Text = "Gold Packs" });

		foreach (var product in ShopProductCatalog.GetByCategory("gold"))
		{
			AddProductCard(_goldStack, product);
		}
	}

	private void RebuildFoodPacks()
	{
		ClearChildren(_foodStack);
		_foodStack.AddChild(new Label { Text = "Food Packs" });

		foreach (var product in ShopProductCatalog.GetByCategory("food"))
		{
			AddProductCard(_foodStack, product);
		}
	}

	private void RebuildMixedPacks()
	{
		ClearChildren(_mixedStack);
		_mixedStack.AddChild(new Label { Text = "Starter & Bundles" });

		foreach (var product in ShopProductCatalog.GetByCategory("mixed"))
		{
			var isDisabled = product.OneTimePurchase && GameState.Instance.HasPurchasedProduct(product.Id);
			AddProductCard(_mixedStack, product, isDisabled);
		}
	}

	private void AddProductCard(VBoxContainer stack, ShopProduct product, bool forceDisabled = false)
	{
		var card = new VBoxContainer();
		card.AddThemeConstantOverride("separation", 4);

		var nameRow = new HBoxContainer();
		nameRow.AddThemeConstantOverride("separation", 8);

		nameRow.AddChild(new Label
		{
			Text = product.DisplayName,
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		});

		if (!string.IsNullOrWhiteSpace(product.ValueLabel))
		{
			nameRow.AddChild(new Label
			{
				Text = $"[{product.ValueLabel}]",
				HorizontalAlignment = HorizontalAlignment.Right
			});
		}

		card.AddChild(nameRow);

		card.AddChild(BuildProductRewardRow(product));

		card.AddChild(new Label
		{
			Text = product.FormattedReward,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		});

		card.AddChild(new Label
		{
			Text = product.Description,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		});

		var buttonRow = new HBoxContainer();
		buttonRow.AddThemeConstantOverride("separation", 8);

		var localizedPrice = NativeIAPService.Instance?.GetLocalizedPrice(product.Id) ?? product.FormattedPrice;
		var purchaseButton = new Button
		{
			Text = forceDisabled ? "Purchased" : $"Buy — {localizedPrice}",
			CustomMinimumSize = new Vector2(160f, 0f),
			Disabled = forceDisabled
		};

		var productId = product.Id;
		purchaseButton.Pressed += () => OnPurchasePressed(productId, purchaseButton);
		buttonRow.AddChild(purchaseButton);

		card.AddChild(buttonRow);

		card.AddChild(new HSeparator());
		stack.AddChild(card);
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
		button.Text = "Tap again to confirm";
		_statusLabel.Text = "Tap the purchase button a second time to confirm.";
	}

	private void ExecutePurchase(string productId)
	{
		var product = ShopProductCatalog.GetById(productId);
		if (product == null)
		{
			_statusLabel.Text = "Unknown product.";
			RefreshUi();
			return;
		}

		var endpoint = GameState.Instance.PurchaseValidationEndpoint;
		if (string.IsNullOrWhiteSpace(endpoint))
		{
			ApplyLocalPurchase(product);
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

		// Fallback: direct server validation
		_statusLabel.Text = $"Validating purchase: {product.DisplayName}...";

		var transactionId = $"local-{Guid.NewGuid():N}";
		var receiptToken = $"receipt-{transactionId}";

		var result = GameState.Instance.ValidatePurchaseWithServer(productId, platform, receiptToken, transactionId);

		if (result.Status == "ok")
		{
			GameState.Instance.TryApplyPurchaseReward(result);
			_statusLabel.Text = $"Purchased {product.DisplayName}!\n";
			if (result.GoldCredited > 0) _statusLabel.Text += $"+{result.GoldCredited} Gold  ";
			if (result.FoodCredited > 0) _statusLabel.Text += $"+{result.FoodCredited} Food  ";
			if (result.GrantedUnitUnlock) _statusLabel.Text += "\n+ Unit unlock granted!";
			AudioDirector.Instance?.PlayUpgradeConfirm();
		}
		else
		{
			_statusLabel.Text = $"Purchase failed: {result.Message}";
		}

		RefreshUi();
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
				RefreshUi();
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

			RefreshUi();
		});
	}

	private void ApplyLocalPurchase(ShopProduct product)
	{
		var goldAmount = product.CurrencyType == "gold" ? product.TotalCurrencyAmount :
			product.CurrencyType == "mixed" ? product.GoldAmount : 0;
		var foodAmount = product.CurrencyType == "food" ? product.TotalCurrencyAmount :
			product.CurrencyType == "mixed" ? product.FoodAmount : 0;

		var result = new PurchaseValidationResult
		{
			Status = "ok",
			Message = "Local purchase applied.",
			ProductId = product.Id,
			GoldCredited = goldAmount,
			FoodCredited = foodAmount,
			GrantedUnitUnlock = product.GrantsUnitUnlock,
			ValidatedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
		};

		GameState.Instance.TryApplyPurchaseReward(result);

		_statusLabel.Text = $"Purchased {product.DisplayName}!\n";
		if (goldAmount > 0) _statusLabel.Text += $"+{goldAmount} Gold  ";
		if (foodAmount > 0) _statusLabel.Text += $"+{foodAmount} Food  ";
		if (product.GrantsUnitUnlock) _statusLabel.Text += "\n+ Unit unlock granted!";

		AudioDirector.Instance?.PlayUpgradeConfirm();
		RefreshUi();
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
		lines.Add(string.IsNullOrWhiteSpace(endpoint) ? "Mode: Local (offline)" : "Mode: Online");

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

	private static MarginContainer CreatePaddedContainer()
	{
		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 18);
		margin.AddThemeConstantOverride("margin_right", 18);
		margin.AddThemeConstantOverride("margin_top", 18);
		margin.AddThemeConstantOverride("margin_bottom", 18);
		return margin;
	}

	private static void ClearChildren(Control parent)
	{
		foreach (var child in parent.GetChildren())
		{
			child.QueueFree();
		}
	}
}
