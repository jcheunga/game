using System;
using System.Collections.Generic;
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

public partial class NativeIAPService : Node
{
	public static NativeIAPService Instance { get; private set; }

	public IAPPlatform Platform { get; private set; } = IAPPlatform.None;
	public bool IsAvailable { get; private set; }
	public bool IsInitialized { get; private set; }

	private readonly Dictionary<string, IAPProductInfo> _productCache = new(StringComparer.OrdinalIgnoreCase);
	private readonly object _callbackLock = new();
	private Action<IAPPurchaseResult> _pendingPurchaseCallback;
	private GodotObject _googleBilling;
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
		if (!Engine.HasSingleton("GodotGooglePlayBilling"))
		{
			GD.Print("NativeIAPService: GodotGooglePlayBilling singleton not available.");
			IsAvailable = false;
			return;
		}

		_googleBilling = Engine.GetSingleton("GodotGooglePlayBilling");

		_googleBilling.Connect("connected", Callable.From(OnGoogleConnected));
		_googleBilling.Connect("disconnected", Callable.From(OnGoogleDisconnected));
		_googleBilling.Connect("connect_error", Callable.From<int, string>(OnGoogleConnectError));
		_googleBilling.Connect("purchases_updated", Callable.From<Godot.Collections.Array>(OnGooglePurchasesUpdated));
		_googleBilling.Connect("purchase_error", Callable.From<int, string>(OnGooglePurchaseError));
		_googleBilling.Connect("sku_details_query_completed", Callable.From<Godot.Collections.Array>(OnGoogleSkuDetailsCompleted));
		_googleBilling.Connect("sku_details_query_error", Callable.From<int, string, Godot.Collections.Array>(OnGoogleSkuDetailsError));
		_googleBilling.Connect("purchase_acknowledged", Callable.From<string>(OnGooglePurchaseAcknowledged));
		_googleBilling.Connect("purchase_consumed", Callable.From<string>(OnGooglePurchaseConsumed));

		_googleBilling.Call("startConnection");
	}

	private void OnGoogleConnected()
	{
		GD.Print("NativeIAPService: Google Play Billing connected.");
		IsAvailable = true;
		IsInitialized = true;
		QueryGoogleProducts();
	}

	private void OnGoogleDisconnected()
	{
		GD.Print("NativeIAPService: Google Play Billing disconnected.");
		IsAvailable = false;
	}

	private void OnGoogleConnectError(int code, string message)
	{
		GD.PrintErr($"NativeIAPService: Google connect error {code}: {message}");
		IsAvailable = false;
	}

	private void QueryGoogleProducts()
	{
		var productIds = new Godot.Collections.Array();
		foreach (var product in ShopProductCatalog.GetAll())
		{
			if (!string.IsNullOrWhiteSpace(product.GoogleProductId))
			{
				productIds.Add(product.GoogleProductId);
			}
		}

		if (productIds.Count > 0)
		{
			_googleBilling.Call("querySkuDetails", productIds, "inapp");
		}
	}

	private void OnGoogleSkuDetailsCompleted(Godot.Collections.Array skuDetails)
	{
		foreach (var item in skuDetails)
		{
			if (item.Obj is not Godot.Collections.Dictionary dict) continue;
			var sku = dict.GetValueOrDefault("sku", Variant.CreateFrom("")).AsString();
			var title = dict.GetValueOrDefault("title", Variant.CreateFrom("")).AsString();
			var price = dict.GetValueOrDefault("price", Variant.CreateFrom("")).AsString();
			var priceMicros = (int)dict.GetValueOrDefault("price_amount_micros", Variant.CreateFrom(0)).AsInt64();

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

	private void OnGoogleSkuDetailsError(int code, string message, Godot.Collections.Array skus)
	{
		GD.PrintErr($"NativeIAPService: Google SKU query error {code}: {message}");
	}

	private void OnGooglePurchasesUpdated(Godot.Collections.Array purchases)
	{
		foreach (var item in purchases)
		{
			if (item.Obj is not Godot.Collections.Dictionary dict) continue;
			var sku = dict.GetValueOrDefault("sku", Variant.CreateFrom("")).AsString();
			var token = dict.GetValueOrDefault("purchase_token", Variant.CreateFrom("")).AsString();
			var orderId = dict.GetValueOrDefault("order_id", Variant.CreateFrom("")).AsString();

			var catalogProduct = FindProductByGoogleId(sku);

			_googleBilling.Call("consumePurchase", token);

			lock (_callbackLock)
			{
				_pendingPurchaseCallback?.Invoke(new IAPPurchaseResult
				{
					Success = true,
					ProductId = catalogProduct?.Id ?? sku,
					TransactionId = orderId,
					ReceiptToken = token
				});
				_pendingPurchaseCallback = null;
			}
		}
	}

	private void OnGooglePurchaseError(int code, string message)
	{
		GD.PrintErr($"NativeIAPService: Google purchase error {code}: {message}");
		lock (_callbackLock)
		{
			_pendingPurchaseCallback?.Invoke(new IAPPurchaseResult
			{
				Success = false,
				ErrorMessage = $"Google Play error {code}: {message}"
			});
			_pendingPurchaseCallback = null;
		}
	}

	private void OnGooglePurchaseAcknowledged(string token)
	{
		GD.Print($"NativeIAPService: Google purchase acknowledged: {token}");
	}

	private void OnGooglePurchaseConsumed(string token)
	{
		GD.Print($"NativeIAPService: Google purchase consumed: {token}");
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
				_googleBilling?.Call("purchase", product.GoogleProductId);
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
