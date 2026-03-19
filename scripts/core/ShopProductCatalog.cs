using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

public sealed class ShopProduct
{
	public string Id { get; set; } = "";
	public string Category { get; set; } = "";
	public string DisplayName { get; set; } = "";
	public string Description { get; set; } = "";
	public string CurrencyType { get; set; } = "";
	public int CurrencyAmount { get; set; }
	public int BonusAmount { get; set; }
	public double PriceUsd { get; set; }
	public string AppleProductId { get; set; } = "";
	public string GoogleProductId { get; set; } = "";
	public string StripePriceId { get; set; } = "";
	public string ValueLabel { get; set; } = "";
	public int SortOrder { get; set; }
	public int GoldAmount { get; set; }
	public int FoodAmount { get; set; }
	public bool GrantsUnitUnlock { get; set; }
	public bool OneTimePurchase { get; set; }

	public int TotalCurrencyAmount => CurrencyAmount + BonusAmount;

	public string FormattedPrice => PriceUsd switch
	{
		< 1.0 => $"${PriceUsd:F2}",
		_ => $"${PriceUsd:F2}"
	};

	public string FormattedReward
	{
		get
		{
			if (CurrencyType == "mixed")
			{
				var parts = new List<string>();
				if (GoldAmount > 0) parts.Add($"{GoldAmount} Gold");
				if (FoodAmount > 0) parts.Add($"{FoodAmount} Food");
				if (GrantsUnitUnlock) parts.Add("+ Unit Unlock");
				return string.Join("  +  ", parts);
			}

			var label = CurrencyType == "gold" ? "Gold" : "Food";
			return BonusAmount > 0
				? $"{CurrencyAmount} + {BonusAmount} bonus {label}"
				: $"{CurrencyAmount} {label}";
		}
	}
}

public static class ShopProductCatalog
{
	private static readonly List<ShopProduct> Products = new();
	private static bool _loaded;

	public static void EnsureLoaded()
	{
		if (_loaded) return;
		_loaded = true;

		try
		{
			using var file = FileAccess.Open("res://data/shop_products.json", FileAccess.ModeFlags.Read);
			if (file == null) return;
			var json = file.GetAsText();
			var options = new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true
			};
			var items = JsonSerializer.Deserialize<List<ShopProduct>>(json, options);
			if (items != null)
			{
				Products.AddRange(items.OrderBy(p => p.SortOrder));
			}
		}
		catch (Exception e)
		{
			GD.PrintErr($"ShopProductCatalog: failed to load shop_products.json: {e.Message}");
		}
	}

	public static IReadOnlyList<ShopProduct> GetAll()
	{
		EnsureLoaded();
		return Products;
	}

	public static IReadOnlyList<ShopProduct> GetByCategory(string category)
	{
		EnsureLoaded();
		return Products.Where(p => p.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();
	}

	public static ShopProduct GetById(string id)
	{
		EnsureLoaded();
		return Products.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
	}
}
