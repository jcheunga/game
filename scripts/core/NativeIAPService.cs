using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public enum IAPPlatform
{
	None,
	Apple,
	Google,
	Stripe
}

public sealed class IAPProductInfo
{
	public string ProductId { get; set; } = "";
	public string NativeProductId { get; set; } = "";
	public string Title { get; set; } = "";
	public string FormattedPrice { get; set; } = "";
	public string CurrencyCode { get; set; } = "USD";
	public int PriceMicros { get; set; }
}

public sealed class IAPPurchaseResult
{
	public bool Success { get; set; }
	public string ProductId { get; set; } = "";
	public string TransactionId { get; set; } = "";
	public string ReceiptToken { get; set; } = "";
	public string ErrorMessage { get; set; } = "";
}

public sealed class PurchaseFulfillment
{
	public PurchaseValidationResult Result { get; init; } = new();
	/// <summary>This call added the server's grant to the save.</summary>
	public bool Granted { get; init; }
	/// <summary>The save on disk holds the grant (from this call or an earlier one), so the store purchase was finished.</summary>
	public bool Finished { get; init; }
}

public partial class NativeIAPService : Node
{
	public static NativeIAPService Instance { get; private set; }

	public IAPPlatform Platform { get; private set; } = IAPPlatform.None;
	public bool IsAvailable { get; private set; }
	public bool IsInitialized { get; private set; }

	private readonly Dictionary<string, IAPProductInfo> _productCache = new(StringComparer.OrdinalIgnoreCase);
	private readonly object _callbackLock = new();
	private Action<IAPPurchaseResult> _pendingPurchaseCallback;
	private readonly HashSet<string> _fulfillingReceipts = new(StringComparer.Ordinal);
	private Node _googleBridge;
	private GodotObject _appleStore;

	public override void _EnterTree()
	{
		Instance = this;
	}

	public override void _ExitTree()
	{
		if (Instance == this)
		{
			Instance = null;
		}
	}

	public override void _Ready()
	{
		DetectPlatform();
	}

	private void DetectPlatform()
	{
		if (OS.HasFeature("ios"))
		{
			Platform = IAPPlatform.Apple;
			TryInitializeApple();
		}
		else if (OS.HasFeature("android"))
		{
			Platform = IAPPlatform.Google;
			TryInitializeGoogle();
		}
		else
		{
			Platform = IAPPlatform.Stripe;
			IsAvailable = true;
			IsInitialized = true;
		}
	}

	private void TryInitializeGoogle()
	{
		var bridgeScript = ResourceLoader.Load<Script>("res://scripts/platform/GooglePlayBillingBridge.gd");
		if (bridgeScript == null)
		{
			GD.PrintErr("NativeIAPService: Google Play Billing bridge is missing.");
			IsAvailable = false;
			return;
		}

		_googleBridge = new Node { Name = "GooglePlayBillingBridge" };
		_googleBridge.SetScript(bridgeScript);
		AddChild(_googleBridge);
		_googleBridge.Connect("billing_ready", Callable.From(OnGoogleConnected));
		_googleBridge.Connect("billing_unavailable", Callable.From<string>(OnGoogleUnavailable));
		_googleBridge.Connect("product_details_received", Callable.From<Godot.Collections.Array>(OnGoogleProductDetailsReceived));
		_googleBridge.Connect("purchase_received", Callable.From<string, string, string, bool>(OnGooglePurchaseReceived));
		_googleBridge.Connect("purchase_failed", Callable.From<string>(OnGooglePurchaseFailed));
		_googleBridge.Connect("consume_finished", Callable.From<string, bool, string>(OnGoogleConsumeFinished));

		var nativeProductIds = new Godot.Collections.Array<string>();
		foreach (var product in ShopProductCatalog.GetAll())
		{
			if (!string.IsNullOrWhiteSpace(product.GoogleProductId))
			{
				nativeProductIds.Add(product.GoogleProductId);
			}
		}

		_googleBridge.Call("configure", nativeProductIds, GameState.Instance?.PlayerProfileId ?? "");
	}

	private void OnGoogleConnected()
	{
		GD.Print("NativeIAPService: Google Play Billing connected.");
		IsAvailable = true;
		IsInitialized = true;
	}

	private void OnGoogleUnavailable(string message)
	{
		GD.PrintErr($"NativeIAPService: Google Play Billing unavailable: {message}");
		IsAvailable = false;
	}

	private void OnGoogleProductDetailsReceived(Godot.Collections.Array productDetails)
	{
		foreach (var item in productDetails)
		{
			if (item.Obj is not Godot.Collections.Dictionary dict) continue;
			var sku = dict.GetValueOrDefault("product_id", Variant.CreateFrom("")).AsString();
			var title = dict.GetValueOrDefault("title", Variant.CreateFrom("")).AsString();
			var price = "";
			var priceMicros = 0;
			if (dict.TryGetValue("one_time_purchase_offer_details_list", out var offersVariant) &&
				offersVariant.Obj is Godot.Collections.Array offers && offers.Count > 0 &&
				offers[0].Obj is Godot.Collections.Dictionary offer)
			{
				price = offer.GetValueOrDefault("formatted_price", Variant.CreateFrom("")).AsString();
				priceMicros = (int)offer.GetValueOrDefault("price_amount_micros", Variant.CreateFrom(0)).AsInt64();
			}

			var catalogProduct = FindProductByGoogleId(sku);
			if (catalogProduct == null) continue;

			_productCache[catalogProduct.Id] = new IAPProductInfo
			{
				ProductId = catalogProduct.Id,
				NativeProductId = sku,
				Title = title,
				FormattedPrice = price,
				PriceMicros = priceMicros
			};
		}

		GD.Print($"NativeIAPService: Cached {_productCache.Count} Google product prices.");
	}

	private void OnGooglePurchaseReceived(string nativeProductId, string transactionId, string purchaseToken, bool restored)
	{
		var catalogProduct = FindProductByGoogleId(nativeProductId);
		if (catalogProduct == null || string.IsNullOrWhiteSpace(purchaseToken))
		{
			GD.PrintErr("NativeIAPService: Google returned an incomplete or unknown purchase.");
			return;
		}

		var purchase = new IAPPurchaseResult
		{
			Success = true,
			ProductId = catalogProduct.Id,
			TransactionId = transactionId,
			ReceiptToken = purchaseToken
		};

		// A restored purchase never answers the shop's purchase in flight.
		Action<IAPPurchaseResult> callback = null;
		if (!restored)
		{
			lock (_callbackLock)
			{
				callback = _pendingPurchaseCallback;
				_pendingPurchaseCallback = null;
			}
		}

		if (callback != null)
		{
			callback.Invoke(purchase);
			return;
		}

		// A restored purchase, or one that finished outside the shop (a pending payment that
		// cleared later). Fulfillment is exactly-once, so recovering it here is always safe.
		ReconcileRecoveredGooglePurchase(purchase);
	}

	private void OnGooglePurchaseFailed(string message)
	{
		GD.PrintErr($"NativeIAPService: Google purchase error: {message}");
		lock (_callbackLock)
		{
			_pendingPurchaseCallback?.Invoke(new IAPPurchaseResult
			{
				Success = false,
				ErrorMessage = message
			});
			_pendingPurchaseCallback = null;
		}
	}

	private void OnGoogleConsumeFinished(string token, bool success, string message)
	{
		if (!success)
		{
			GD.PrintErr($"NativeIAPService: Google consumption failed: {message}");
		}
	}

	private async void ReconcileRecoveredGooglePurchase(IAPPurchaseResult purchase)
	{
		if (GameState.Instance == null ||
			string.IsNullOrWhiteSpace(GameState.Instance.PurchaseValidationEndpoint) ||
			string.IsNullOrWhiteSpace(GameState.Instance.PlayerAuthToken))
		{
			// Leave the transaction untouched. It will be returned by Play again
			// once the player can authenticate to the commerce backend.
			return;
		}

		try
		{
			var fulfillment = await FulfillPurchaseAsync(purchase, "google");
			if (fulfillment.Granted)
			{
				GD.Print($"NativeIAPService: Credited interrupted purchase {fulfillment.Result.PurchaseId} ({purchase.ProductId}).");
			}
			else if (!fulfillment.Finished)
			{
				GD.PrintErr($"NativeIAPService: Interrupted purchase of {purchase.ProductId} is still unverified: {fulfillment.Result.Message}");
			}
		}
		catch (Exception e)
		{
			GD.PrintErr($"NativeIAPService: Recovering a purchase failed: {e.Message}");
		}
	}

	/// <summary>
	/// Verifies a store purchase with the server off the main thread, applies the server's
	/// grant to the save at most once (keyed by the server's purchase ID), and finishes the
	/// store purchase only once that grant is on disk. The shop and relaunch recovery both
	/// come through here, so a purchase interrupted between the server's commit and its
	/// response (timeout, dropped connection, app killed) is still granted exactly once: the
	/// store re-delivers the unfinished purchase, the server answers "already_recorded" with
	/// the same purchase ID and grant, and the save applies it only if it has not seen that ID.
	/// </summary>
	public async Task<PurchaseFulfillment> FulfillPurchaseAsync(IAPPurchaseResult purchase, string platform)
	{
		var state = GameState.Instance;
		if (state == null || purchase == null || string.IsNullOrWhiteSpace(purchase.ReceiptToken))
		{
			return new PurchaseFulfillment { Result = new PurchaseValidationResult { Status = "error", Message = "The store returned an incomplete purchase." } };
		}

		if (!_fulfillingReceipts.Add(purchase.ReceiptToken))
		{
			return new PurchaseFulfillment { Result = new PurchaseValidationResult { Status = "in_progress", Message = "This purchase is already being verified." } };
		}

		try
		{
			var result = await state.ValidatePurchaseWithServerAsync(purchase.ProductId, platform, purchase.ReceiptToken, purchase.TransactionId);
			// Back on the main thread: the save is only touched here.
			var granted = state.TryApplyPurchaseReward(result);
			var finished = state.IsPurchaseGrantSaved(result.PurchaseId);
			if (finished)
			{
				ConfirmServerFulfillment(purchase);
			}

			return new PurchaseFulfillment { Result = result, Granted = granted, Finished = finished };
		}
		finally
		{
			_fulfillingReceipts.Remove(purchase.ReceiptToken);
		}
	}

	private void TryInitializeApple()
	{
		if (!Engine.HasSingleton("StoreKit"))
		{
			GD.Print("NativeIAPService: StoreKit singleton not available.");
			IsAvailable = false;
			return;
		}

		_appleStore = Engine.GetSingleton("StoreKit");

		_appleStore.Connect("products_received", Callable.From<Godot.Collections.Array>(OnAppleProductsReceived));
		_appleStore.Connect("purchase_completed", Callable.From<Godot.Collections.Dictionary>(OnApplePurchaseCompleted));
		_appleStore.Connect("purchase_failed", Callable.From<string, string>(OnApplePurchaseFailed));

		IsAvailable = true;
		IsInitialized = true;
		QueryAppleProducts();
	}

	private void QueryAppleProducts()
	{
		var productIds = new Godot.Collections.Array();
		foreach (var product in ShopProductCatalog.GetAll())
		{
			if (!string.IsNullOrWhiteSpace(product.AppleProductId))
			{
				productIds.Add(product.AppleProductId);
			}
		}

		if (productIds.Count > 0)
		{
			_appleStore.Call("request_products", productIds);
		}
	}

	private void OnAppleProductsReceived(Godot.Collections.Array products)
	{
		foreach (var item in products)
		{
			if (item.Obj is not Godot.Collections.Dictionary dict) continue;
			var appleId = dict.GetValueOrDefault("product_id", Variant.CreateFrom("")).AsString();
			var title = dict.GetValueOrDefault("localized_title", Variant.CreateFrom("")).AsString();
			var price = dict.GetValueOrDefault("localized_price", Variant.CreateFrom("")).AsString();

			var catalogProduct = FindProductByAppleId(appleId);
			if (catalogProduct == null) continue;

			_productCache[catalogProduct.Id] = new IAPProductInfo
			{
				ProductId = catalogProduct.Id,
				NativeProductId = appleId,
				Title = title,
				FormattedPrice = price
			};
		}

		GD.Print($"NativeIAPService: Cached {_productCache.Count} Apple product prices.");
	}

	private void OnApplePurchaseCompleted(Godot.Collections.Dictionary dict)
	{
		var appleId = dict.GetValueOrDefault("product_id", Variant.CreateFrom("")).AsString();
		var transactionId = dict.GetValueOrDefault("transaction_id", Variant.CreateFrom("")).AsString();
		var receipt = dict.GetValueOrDefault("receipt", Variant.CreateFrom("")).AsString();

		var catalogProduct = FindProductByAppleId(appleId);

		lock (_callbackLock)
		{
			_pendingPurchaseCallback?.Invoke(new IAPPurchaseResult
			{
				Success = true,
				ProductId = catalogProduct?.Id ?? appleId,
				TransactionId = transactionId,
				ReceiptToken = receipt
			});
			_pendingPurchaseCallback = null;
		}
	}

	private void OnApplePurchaseFailed(string productId, string error)
	{
		GD.PrintErr($"NativeIAPService: Apple purchase failed for {productId}: {error}");
		lock (_callbackLock)
		{
			_pendingPurchaseCallback?.Invoke(new IAPPurchaseResult
			{
				Success = false,
				ProductId = productId,
				ErrorMessage = error
			});
			_pendingPurchaseCallback = null;
		}
	}

	public void PurchaseProduct(string productId, Action<IAPPurchaseResult> callback)
	{
		if (!IsAvailable || !IsInitialized)
		{
			callback?.Invoke(new IAPPurchaseResult
			{
				Success = false,
				ProductId = productId,
				ErrorMessage = "IAP service is not available."
			});
			return;
		}

		var product = ShopProductCatalog.GetById(productId);
		if (product == null)
		{
			callback?.Invoke(new IAPPurchaseResult
			{
				Success = false,
				ProductId = productId,
				ErrorMessage = "Unknown product."
			});
			return;
		}

		lock (_callbackLock)
		{
			_pendingPurchaseCallback = callback;
		}

		switch (Platform)
		{
			case IAPPlatform.Google:
				_googleBridge?.Call("begin_purchase", product.GoogleProductId);
				break;

			case IAPPlatform.Apple:
				_appleStore?.Call("purchase", product.AppleProductId);
				break;

			default:
				lock (_callbackLock)
				{
					_pendingPurchaseCallback?.Invoke(new IAPPurchaseResult
					{
						Success = false,
						ProductId = productId,
						ErrorMessage = "Native IAP not available on this platform. Use Stripe."
					});
					_pendingPurchaseCallback = null;
				}
				break;
		}
	}

	public IAPProductInfo GetCachedProductInfo(string productId)
	{
		return _productCache.TryGetValue(productId, out var info) ? info : null;
	}

	/// <summary>
	/// Google consumables must not be consumed until the secure backend has accepted
	/// and recorded the store transaction. Calling this before server fulfillment
	/// would make a payment unrecoverable if validation fails or the app closes.
	/// </summary>
	public void ConfirmServerFulfillment(IAPPurchaseResult purchase)
	{
		if (purchase == null || string.IsNullOrWhiteSpace(purchase.ReceiptToken))
		{
			return;
		}

		if (Platform == IAPPlatform.Google)
		{
			_googleBridge?.Call("confirm_consumed", purchase.ReceiptToken);
		}
	}

	public string GetLocalizedPrice(string productId)
	{
		var info = GetCachedProductInfo(productId);
		if (info != null && !string.IsNullOrWhiteSpace(info.FormattedPrice))
		{
			return info.FormattedPrice;
		}

		var product = ShopProductCatalog.GetById(productId);
		return product?.FormattedPrice ?? "";
	}

	private static ShopProduct FindProductByGoogleId(string googleId)
	{
		foreach (var product in ShopProductCatalog.GetAll())
		{
			if (product.GoogleProductId.Equals(googleId, StringComparison.OrdinalIgnoreCase))
			{
				return product;
			}
		}
		return null;
	}

	private static ShopProduct FindProductByAppleId(string appleId)
	{
		foreach (var product in ShopProductCatalog.GetAll())
		{
			if (product.AppleProductId.Equals(appleId, StringComparison.OrdinalIgnoreCase))
			{
				return product;
			}
		}
		return null;
	}
}
