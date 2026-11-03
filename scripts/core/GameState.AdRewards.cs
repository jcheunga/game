using System;

/// <summary>
/// Rewards for watching an ad (RewardedAds), capped each day on the bounty board's day (UTC) like the gold refill:
/// a win's gold paid again, and a handful of rations from the storehouse.
/// </summary>
public partial class GameState
{
    public const string AdDoubleGold = "double_gold", AdFood = "food";
    public const int AdDoubleGoldPerDay = 5, AdFoodPerDay = 3, AdFoodAmount = 5;

    private string _adRewardDay = "";
    private int _adDoubleGoldOnDay, _adFoodOnDay;

    public int AdRewardsLeftToday(string placement)
    {
        var today = BountyBoardCatalog.GetDateKey() == _adRewardDay;
        return placement switch
        {
            AdDoubleGold => AdDoubleGoldPerDay - (today ? _adDoubleGoldOnDay : 0),
            AdFood => AdFoodPerDay - (today ? _adFoodOnDay : 0),
            _ => 0
        };
    }

    /// <summary>Pays an ad's reward once the player has watched it: a win's gold again, or rations.</summary>
    public bool TryClaimAdReward(string placement, int winGold, out string message)
    {
        if (AdRewardsLeftToday(placement) <= 0) { message = "That reward is used up for today."; return false; }
        if (placement == AdDoubleGold && winGold <= 0) { message = "There is no gold to double."; return false; }
        var today = BountyBoardCatalog.GetDateKey();
        if (_adRewardDay != today) { _adRewardDay = today; _adDoubleGoldOnDay = 0; _adFoodOnDay = 0; }
        if (placement == AdDoubleGold)
        {
            _adDoubleGoldOnDay++;
            Gold += winGold;
            message = $"+{winGold:N0} gold";
        }
        else
        {
            _adFoodOnDay++;
            Food += AdFoodAmount;
            FoodChanged?.Invoke();
            message = $"+{AdFoodAmount} food";
        }
        Persist();
        return true;
    }

    private void ResetAdRewards() { _adRewardDay = ""; _adDoubleGoldOnDay = 0; _adFoodOnDay = 0; }

    private void LoadAdRewards(GameSaveData saved)
    {
        _adRewardDay = saved.AdRewardDay?.Trim() ?? "";
        _adDoubleGoldOnDay = Math.Max(0, saved.AdDoubleGoldOnDay);
        _adFoodOnDay = Math.Max(0, saved.AdFoodOnDay);
    }

    private void SaveAdRewards(GameSaveData data)
    {
        data.AdRewardDay = _adRewardDay;
        data.AdDoubleGoldOnDay = _adDoubleGoldOnDay;
        data.AdFoodOnDay = _adFoodOnDay;
    }
}
