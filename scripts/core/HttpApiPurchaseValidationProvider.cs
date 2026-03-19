using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;

public sealed class PurchaseValidationRequest
{
	public string PlayerProfileId { get; set; } = "";
	public string ProductId { get; set; } = "";
	public string Platform { get; set; } = "";
	public string ReceiptToken { get; set; } = "";
	public string TransactionId { get; set; } = "";
	public long RequestedAtUnixSeconds { get; set; }
}

public sealed class PurchaseValidationResult
{
	public string Status { get; set; } = "";
	public string Message { get; set; } = "";
	public string PurchaseId { get; set; } = "";
	public string ProductId { get; set; } = "";
	public int GoldCredited { get; set; }
	public int FoodCredited { get; set; }
	public bool GrantedUnitUnlock { get; set; }
	public long ValidatedAtUnixSeconds { get; set; }
}

public sealed class PurchaseHistoryEntry
{
	public string PurchaseId { get; set; } = "";
	public string ProductId { get; set; } = "";
	public string Platform { get; set; } = "";
	public int GoldCredited { get; set; }
	public int FoodCredited { get; set; }
	public long PurchasedAtUnixSeconds { get; set; }
}

public sealed class StripeCheckoutResult
{
	public string Status { get; set; } = "";
	public string Message { get; set; } = "";
	public string CheckoutUrl { get; set; } = "";
	public string SessionId { get; set; } = "";
	public string ProductId { get; set; } = "";
	public int PriceCents { get; set; }
}

public sealed class StripeCheckoutStatusResult
{
	public string Status { get; set; } = "";
	public string PaymentStatus { get; set; } = "";
	public string ProductId { get; set; } = "";
	public bool Completed { get; set; }
}

public sealed class HttpApiPurchaseValidationProvider
{
	private static readonly HttpClient Client = new()
	{
		Timeout = TimeSpan.FromSeconds(15)
	};

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = false,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		PropertyNameCaseInsensitive = true
	};

	private readonly string _baseUrl;

	public HttpApiPurchaseValidationProvider(string baseUrl)
	{
		_baseUrl = baseUrl?.TrimEnd('/') ?? "";
	}

	public PurchaseValidationResult ValidatePurchase(PurchaseValidationRequest request)
	{
		if (string.IsNullOrWhiteSpace(_baseUrl))
		{
			throw new InvalidOperationException("Purchase validation endpoint is not configured.");
		}

		var requestBody = new
		{
			profileId = request.PlayerProfileId,
			productId = request.ProductId,
			platform = request.Platform,
			receiptToken = request.ReceiptToken,
			transactionId = request.TransactionId
		};
		var requestJson = JsonSerializer.Serialize(requestBody, JsonOptions);

		using var message = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/purchase/validate");
		message.Headers.TryAddWithoutValidation("X-Convoy-Profile", request.PlayerProfileId);
		message.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

		using var response = Client.Send(message);
		var responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException($"HTTP {(int)response.StatusCode}: {responseBody}");
		}

		using var document = JsonDocument.Parse(responseBody);
		var root = document.RootElement;
		return new PurchaseValidationResult
		{
			Status = GetString(root, "status", "error"),
			Message = GetString(root, "message", "Unknown response."),
			PurchaseId = GetString(root, "purchaseId", ""),
			ProductId = GetString(root, "productId", request.ProductId),
			GoldCredited = GetInt(root, "goldCredited", 0),
			FoodCredited = GetInt(root, "foodCredited", 0),
			GrantedUnitUnlock = GetBool(root, "grantedUnitUnlock", false),
			ValidatedAtUnixSeconds = GetLong(root, "validatedAtUnixSeconds", request.RequestedAtUnixSeconds)
		};
	}

	public List<PurchaseHistoryEntry> GetPurchaseHistory(string profileId)
	{
		if (string.IsNullOrWhiteSpace(_baseUrl))
		{
			return new List<PurchaseHistoryEntry>();
		}

		using var message = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/purchase/history?profileId={Uri.EscapeDataString(profileId)}");
		message.Headers.TryAddWithoutValidation("X-Convoy-Profile", profileId);

		using var response = Client.Send(message);
		var responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
		if (!response.IsSuccessStatusCode)
		{
			return new List<PurchaseHistoryEntry>();
		}

		using var document = JsonDocument.Parse(responseBody);
		var root = document.RootElement;
		var entries = new List<PurchaseHistoryEntry>();
		if (root.TryGetProperty("purchases", out var arr) && arr.ValueKind == JsonValueKind.Array)
		{
			foreach (var item in arr.EnumerateArray())
			{
				entries.Add(new PurchaseHistoryEntry
				{
					PurchaseId = GetString(item, "purchaseId", ""),
					ProductId = GetString(item, "productId", ""),
					Platform = GetString(item, "platform", ""),
					GoldCredited = GetInt(item, "goldCredited", 0),
					FoodCredited = GetInt(item, "foodCredited", 0),
					PurchasedAtUnixSeconds = GetLong(item, "purchasedAtUnixSeconds", 0)
				});
			}
		}

		return entries;
	}

	public StripeCheckoutResult CreateStripeCheckout(string profileId, string productId)
	{
		if (string.IsNullOrWhiteSpace(_baseUrl))
		{
			return new StripeCheckoutResult { Status = "error", Message = "Endpoint not configured." };
		}

		var requestBody = new
		{
			profileId,
			productId,
			successUrl = "",
			cancelUrl = ""
		};
		var requestJson = JsonSerializer.Serialize(requestBody, JsonOptions);

		using var message = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/purchase/stripe-checkout");
		message.Headers.TryAddWithoutValidation("X-Convoy-Profile", profileId);
		message.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

		using var response = Client.Send(message);
		var responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
		if (!response.IsSuccessStatusCode)
		{
			return new StripeCheckoutResult
			{
				Status = "error",
				Message = $"HTTP {(int)response.StatusCode}: {responseBody}"
			};
		}

		using var document = JsonDocument.Parse(responseBody);
		var root = document.RootElement;
		return new StripeCheckoutResult
		{
			Status = GetString(root, "status", "error"),
			CheckoutUrl = GetString(root, "checkoutUrl", ""),
			SessionId = GetString(root, "sessionId", ""),
			ProductId = GetString(root, "productId", productId),
			PriceCents = GetInt(root, "priceCents", 0)
		};
	}

	public StripeCheckoutStatusResult CheckStripeStatus(string sessionId)
	{
		if (string.IsNullOrWhiteSpace(_baseUrl))
		{
			return new StripeCheckoutStatusResult { Status = "error" };
		}

		using var message = new HttpRequestMessage(HttpMethod.Get,
			$"{_baseUrl}/purchase/stripe-status?sessionId={Uri.EscapeDataString(sessionId)}");

		using var response = Client.Send(message);
		var responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
		if (!response.IsSuccessStatusCode)
		{
			return new StripeCheckoutStatusResult { Status = "error" };
		}

		using var document = JsonDocument.Parse(responseBody);
		var root = document.RootElement;
		return new StripeCheckoutStatusResult
		{
			Status = GetString(root, "status", "error"),
			PaymentStatus = GetString(root, "paymentStatus", ""),
			ProductId = GetString(root, "productId", ""),
			Completed = GetBool(root, "completed", false)
		};
	}

	private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
	{
		foreach (var property in element.EnumerateObject())
		{
			if (property.NameEquals(propertyName) || property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
			{
				value = property.Value;
				return true;
			}
		}

		value = default;
		return false;
	}

	private static string GetString(JsonElement element, string propertyName, string fallback)
	{
		return TryGetProperty(element, propertyName, out var value) && value.ValueKind == JsonValueKind.String
			? value.GetString() ?? fallback
			: fallback;
	}

	private static int GetInt(JsonElement element, string propertyName, int fallback)
	{
		return TryGetProperty(element, propertyName, out var value) && value.TryGetInt32(out var parsed)
			? parsed
			: fallback;
	}

	private static long GetLong(JsonElement element, string propertyName, long fallback)
	{
		return TryGetProperty(element, propertyName, out var value) && value.TryGetInt64(out var parsed)
			? parsed
			: fallback;
	}

	private static bool GetBool(JsonElement element, string propertyName, bool fallback)
	{
		return TryGetProperty(element, propertyName, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
			? value.GetBoolean()
			: fallback;
	}
}
