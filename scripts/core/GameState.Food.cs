using System;
using Godot;

public partial class GameState
{
    public const int FoodRechargeCap = 24;
    public const int FoodRechargeAmount = 2;
    public const int FoodRechargeSeconds = 300;
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
        if (Gold < 100) { message = "Need 100 gold."; return false; }
        Gold -= 100; Food += 10;
        Persist(); FoodChanged?.Invoke(); message = "+10 food"; return true;
    }

    private bool TrySpendExplorationFood(int cost)
    {
        RefreshFoodRecharge();
        if (Food < cost) return false;
        Food -= cost; Persist(); FoodChanged?.Invoke(); return true;
    }
}
