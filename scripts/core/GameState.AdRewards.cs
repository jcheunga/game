using System;
using System.Threading.Tasks;

/// <summary>
/// Rewards for watching an ad (RewardedAds), capped each day on the bounty board's day (UTC) like the gold refill:
/// a win's gold paid again, and a handful of rations from the storehouse. Release builds offer them only while the
/// operator's switch on the server is on (<see cref="AdsSwitchedOn"/>).
/// </summary>
public partial class GameState
{
    public const string AdDoubleGold = "double_gold", AdFood = "food";
    public const int AdDoubleGoldPerDay = 5, AdFoodPerDay = 3, AdFoodAmount = 5;

    private static readonly TimeSpan AdsSwitchRereadAfter = TimeSpan.FromMinutes(5);

    private string _adRewardDay = "";
    private int _adDoubleGoldOnDay, _adFoodOnDay;
    private DateTime _adsSwitchReadAt = DateTime.MinValue;

    /// <summary>
    /// Whether the operator has switched rewarded ads on (server /live-settings). Off until the server first says on,
    /// and kept in the save, so a launch without a connection keeps the last answer.
    /// </summary>
    public bool AdsSwitchedOn { get; private set; }

    /// <summary>Asks the server for the ads switch, at most every 5 minutes unless forced. Unreachable keeps the last answer.</summary>
    public async Task RefreshAdsSwitchAsync(bool force = false)
    {
        if (string.IsNullOrWhiteSpace(_purchaseValidationEndpoint)) return;
        if (!force && DateTime.UtcNow - _adsSwitchReadAt < AdsSwitchRereadAfter) return;
        var endpoint = _purchaseValidationEndpoint;
        bool? on;
        try { on = await Task.Run(() => new HttpApiPurchaseValidationProvider(endpoint).GetAdsSwitch()); }
        catch (Exception) { on = null; }
        if (on is not bool value) return;
        _adsSwitchReadAt = DateTime.UtcNow;
        ApplyAdsSwitch(value);
    }

    /// <summary>Takes the server's answer, saving it when it changes.</summary>
    public void ApplyAdsSwitch(bool on)
    {
        if (AdsSwitchedOn == on) return;
        AdsSwitchedOn = on;
        Persist();
    }

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

    // The switch belongs to the server, not the player, so a reset keeps it.
    private void ResetAdRewards() { _adRewardDay = ""; _adDoubleGoldOnDay = 0; _adFoodOnDay = 0; }

    private void LoadAdRewards(GameSaveData saved)
    {
        _adRewardDay = saved.AdRewardDay?.Trim() ?? "";
        _adDoubleGoldOnDay = Math.Max(0, saved.AdDoubleGoldOnDay);
        _adFoodOnDay = Math.Max(0, saved.AdFoodOnDay);
        AdsSwitchedOn = saved.AdsSwitchedOn;
    }

    private void SaveAdRewards(GameSaveData data)
    {
        data.AdRewardDay = _adRewardDay;
        data.AdDoubleGoldOnDay = _adDoubleGoldOnDay;
        data.AdFoodOnDay = _adFoodOnDay;
        data.AdsSwitchedOn = AdsSwitchedOn;
    }
}
