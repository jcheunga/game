using System;

public partial class GameState
{
    public const int FoodRechargeCap = 24;
    public const int FoodRechargeAmount = 2;
    public const int FoodRechargeSeconds = 300;
    /// <summary>Rations paid for a campaign stage's first win, so exploring a zone's atlas doesn't rest on the recharge alone.</summary>
    public const int CampaignFirstClearFood = 6;
    /// <summary>
    /// The storehouse's gold-for-food refill: 30 gold a ration, so every food pack buys more food per dollar than a
    /// gold pack spent on refills (the best gold rate, 550 a dollar, refills 18 food; the weakest food pack is 20).
    /// </summary>
    public const int FoodRefillGold = 300, FoodRefillAmount = 10;
    public long FoodRechargedAtUnixSeconds { get; private set; }
    private double _foodRefreshClock;
    public event Action FoodChanged;

    public override void _Process(double delta)
    {
        _foodRefreshClock += delta;
        if (_foodRefreshClock < 1) return;
        _foodRefreshClock = 0;
        if (RefreshFoodRecharge()) { Persist(); FoodChanged?.Invoke(); }
    }

    public bool RefreshFoodRecharge(long now = 0)
    {
        if (now == 0) now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (FoodRechargedAtUnixSeconds <= 0 || FoodRechargedAtUnixSeconds > now || Food >= FoodRechargeCap)
        { FoodRechargedAtUnixSeconds = now; return false; }
        var ticks = (now - FoodRechargedAtUnixSeconds) / FoodRechargeSeconds;
        if (ticks == 0) return false;
        Food = (int)Math.Min(FoodRechargeCap, Food + ticks * FoodRechargeAmount);
        FoodRechargedAtUnixSeconds = Food >= FoodRechargeCap ? now : FoodRechargedAtUnixSeconds + ticks * FoodRechargeSeconds;
        return true;
    }

    public string FoodRechargeText
    {
        get
        {
            if (Food >= FoodRechargeCap) return $"{Food} / {FoodRechargeCap}";
            var seconds = Math.Max(0, FoodRechargeSeconds - (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - FoodRechargedAtUnixSeconds));
            return $"{Food} / {FoodRechargeCap}  ·  +2 in {seconds / 60:00}:{seconds % 60:00}";
        }
    }

    public bool TryBuyFoodRefill(out string message)
    {
        RefreshFoodRecharge();
        if (Gold < FoodRefillGold) { message = $"Need {FoodRefillGold} gold."; return false; }
        Gold -= FoodRefillGold; Food += FoodRefillAmount;
        Persist(); FoodChanged?.Invoke(); message = $"+{FoodRefillAmount} food"; return true;
    }
}
