using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// A change the server recorded to the player's currency: a store refund, a refund's reversal, or a support grant
/// that makes good a pack that could not be delivered.
/// </summary>
public sealed class WalletAdjustment
{
    public string AdjustmentId { get; set; } = "";
    public string PurchaseId { get; set; } = "";
    public string ProductId { get; set; } = "";
    /// <summary>"refund", "refund_reversed" or "grant".</summary>
    public string Kind { get; set; } = "";
    /// <summary>What the purchase granted, signed: negative for a refund.</summary>
    public int GoldDelta { get; set; }
    public int FoodDelta { get; set; }
    public string ReversesAdjustmentId { get; set; } = "";
    /// <summary>The server already has a device's report of what it applied.</summary>
    public bool Acknowledged { get; set; }
}

public partial class GameState
{
    private readonly Dictionary<string, AppliedWalletAdjustment> _appliedWalletAdjustments = new(StringComparer.Ordinal);
    private string _walletAdjustmentNotice = "";

    public bool HasAppliedWalletAdjustment(string adjustmentId) =>
        !string.IsNullOrWhiteSpace(adjustmentId) && _appliedWalletAdjustments.ContainsKey(adjustmentId.Trim());

    public AppliedWalletAdjustment GetAppliedWalletAdjustment(string adjustmentId) =>
        !string.IsNullOrWhiteSpace(adjustmentId) && _appliedWalletAdjustments.TryGetValue(adjustmentId.Trim(), out var applied) ? Copy(applied) : null;

    public IReadOnlyList<AppliedWalletAdjustment> GetAppliedWalletAdjustments() => _appliedWalletAdjustments.Values.Select(Copy).ToList();

    /// <summary>
    /// Applies a store refund (or its reversal) to the save at most once per adjustment ID; the ID
    /// and the change are written in the same save. A refund takes back what its purchase granted,
    /// but only from a save that holds that grant, and never below zero: currency the player already
    /// spent is recorded as unrecovered instead of leaving a negative balance. A reversal gives back
    /// exactly what this save's refund took. The Adventurer's Kit's unit stays unlocked. Returns what
    /// was applied, or null when nothing was (already applied, or a kind this build does not know).
    /// </summary>
    public AppliedWalletAdjustment TryApplyWalletAdjustment(WalletAdjustment adjustment)
    {
        var id = adjustment?.AdjustmentId?.Trim() ?? "";
        if (id.Length == 0 || _appliedWalletAdjustments.ContainsKey(id))
        {
            return null;
        }

        var applied = new AppliedWalletAdjustment
        {
            AdjustmentId = id,
            PurchaseId = adjustment.PurchaseId?.Trim() ?? "",
            Kind = adjustment.Kind,
            AppliedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
        switch (adjustment.Kind)
        {
            case "refund":
                // A save that never received the grant (its response was lost, or the save
                // predates the purchase) has nothing to give back.
                if (HasAppliedPurchase(applied.PurchaseId))
                {
                    var owedGold = Math.Max(0, -adjustment.GoldDelta);
                    var owedFood = Math.Max(0, -adjustment.FoodDelta);
                    applied.GoldApplied = -Math.Min(Math.Max(0, Gold), owedGold);
                    applied.FoodApplied = -Math.Min(Math.Max(0, Food), owedFood);
                    applied.GoldUnrecovered = owedGold + applied.GoldApplied;
                    applied.FoodUnrecovered = owedFood + applied.FoodApplied;
                }
                break;
            case "refund_reversed":
                if (_appliedWalletAdjustments.TryGetValue(adjustment.ReversesAdjustmentId?.Trim() ?? "", out var refund))
                {
                    applied.GoldApplied = Math.Min(-refund.GoldApplied, Math.Max(0, adjustment.GoldDelta));
                    applied.FoodApplied = Math.Min(-refund.FoodApplied, Math.Max(0, adjustment.FoodDelta));
                }
                break;
            case "grant":
                // Support makes good what a purchase failed to deliver; it belongs to no purchase.
                applied.GoldApplied = Math.Max(0, adjustment.GoldDelta);
                applied.FoodApplied = Math.Max(0, adjustment.FoodDelta);
                break;
            default:
                // Left for a newer build, which will apply it under the same ID.
                return null;
        }

        Gold += applied.GoldApplied;
        Food += applied.FoodApplied;
        _appliedWalletAdjustments[id] = applied;
        if (DescribeWalletAdjustment(applied, adjustment.ProductId) is { Length: > 0 } notice)
        {
            _walletAdjustmentNotice = notice;
        }

        Persist();
        if (applied.FoodApplied != 0)
        {
            FoodChanged?.Invoke();
        }

        return Copy(applied);
    }

    /// <summary>True when the save on disk holds this adjustment, so the server may be told what was applied.</summary>
    public bool IsWalletAdjustmentSaved(string adjustmentId) =>
        HasAppliedWalletAdjustment(adjustmentId) && !GameData.LoadFailed && SaveSystem.Instance is { LastSaveSucceeded: true };

    /// <summary>The latest refund message for the player, once.</summary>
    public string ConsumeWalletAdjustmentNotice()
    {
        var notice = _walletAdjustmentNotice;
        _walletAdjustmentNotice = "";
        return notice;
    }

    private static string DescribeWalletAdjustment(AppliedWalletAdjustment applied, string productId)
    {
        var product = ShopProductCatalog.GetById(productId)?.DisplayName ?? "A purchase";
        static string Amounts(int gold, int food) => string.Join(" and ", new[]
        {
            gold > 0 ? $"{gold:N0} gold" : "",
            food > 0 ? $"{food:N0} food" : ""
        }.Where(part => part.Length > 0));

        if (applied.Kind == "refund")
        {
            var taken = Amounts(-applied.GoldApplied, -applied.FoodApplied);
            var spent = Amounts(applied.GoldUnrecovered, applied.FoodUnrecovered);
            if (taken.Length == 0 && spent.Length == 0) return "";
            var message = taken.Length > 0
                ? $"{product} was refunded by the store, so its {taken} {(applied.GoldApplied != 0 && applied.FoodApplied != 0 ? "were" : "was")} taken back."
                : $"{product} was refunded by the store.";
            return spent.Length > 0 ? $"{message} The other {spent} had already been spent." : message;
        }

        var returned = Amounts(applied.GoldApplied, applied.FoodApplied);
        if (applied.Kind == "grant") return returned.Length > 0 ? $"Support added {returned} to your caravan." : "";
        return returned.Length > 0 ? $"The store reversed the refund of {product}, so {returned} came back." : "";
    }

    private void ClearWalletAdjustments()
    {
        _appliedWalletAdjustments.Clear();
        _walletAdjustmentNotice = "";
    }

    private void LoadWalletAdjustments(GameSaveData saved)
    {
        // The notice is for the player, not the save, so a reload keeps it.
        _appliedWalletAdjustments.Clear();
        foreach (var applied in saved.AppliedWalletAdjustments ?? [])
        {
            if (!string.IsNullOrWhiteSpace(applied?.AdjustmentId))
            {
                var copy = Copy(applied);
                copy.AdjustmentId = copy.AdjustmentId.Trim();
                _appliedWalletAdjustments[copy.AdjustmentId] = copy;
            }
        }
    }

    private List<AppliedWalletAdjustment> SaveWalletAdjustments() => _appliedWalletAdjustments.Values.Select(Copy).ToList();

    private static AppliedWalletAdjustment Copy(AppliedWalletAdjustment applied) => new()
    {
        AdjustmentId = applied.AdjustmentId ?? "",
        PurchaseId = applied.PurchaseId ?? "",
        Kind = applied.Kind ?? "",
        GoldApplied = applied.GoldApplied,
        FoodApplied = applied.FoodApplied,
        GoldUnrecovered = applied.GoldUnrecovered,
        FoodUnrecovered = applied.FoodUnrecovered,
        AppliedAtUnixSeconds = applied.AppliedAtUnixSeconds
    };
}
