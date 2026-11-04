using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

// --ads: the rewarded-ad placements, with a stand-in ad in place of AdMob.
public partial class UiReviewSmoke
{
    private async Task ReviewAds()
    {
        var state = GameState.Instance;
        state.ResetProgress(); state.SetShowHints(false);
        Check(!RewardedAds.Enabled, "Without the ad plugin (a desktop build) no ad is offered");
        var watched = true;
        RewardedAds.TestShow = async () => { await Wait(.2); return watched; };
        try
        {
            // A release build obeys the server's ads switch (debug builds ignore it and show Google's test ads).
            RewardedAds.IgnoreSwitch = false;
            state.ApplyAdsSwitch(false);
            Check(!RewardedAds.Enabled && !RewardedAds.Ready, "A release build offers no ad while the server's switch is off");
            state.ApplyAdsSwitch(true);
            Check(RewardedAds.Enabled && state.BuildSaveData().AdsSwitchedOn, "Switching ads on offers them, and the save keeps the switch");
            var fixture = state.BuildSaveData();
            fixture.Gold = 500; fixture.Food = 10;
            fixture.FoodRechargedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            typeof(GameState).GetMethod("ApplySavedData", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(state, new object[] { fixture });
            await Open("MainMenu");
            var menu = (MapMenu)GetTree().CurrentScene;
            menu.OpenHomeDestination(SceneRouter.CashShopScene);
            await Wait(.4);
            var store = Walk(menu).OfType<CashShopMenu>().Single();
            var notice = Walk(store).OfType<Label>().Single(label => label.Name == "PurchaseNotice");
            await TapModal(Walk(store).OfType<Button>().Single(button => button.Text == "Rations"));
            var ad = Walk(store).OfType<Button>().Single(button => button.IsVisibleInTree() && button.Text == $"{GameState.AdFoodAmount} food · ad");
            Check(!ad.Disabled && (ad.TooltipText + ad.AccessibilityName).Contains($"{GameState.AdFoodPerDay} left today"), "The storehouse offers rations for an ad beside the refill");
            await Capture("ads-storehouse");
            watched = false;
            await TapModal(ad); await Wait(.4);
            Check(state.Food == 10 && notice.Text.StartsWith("The ad didn't finish") && state.AdRewardsLeftToday(GameState.AdFood) == GameState.AdFoodPerDay,
                "An ad closed before its end pays nothing and uses nothing up");
            watched = true;
            for (var i = 0; i < GameState.AdFoodPerDay; i++) { await TapModal(ad); await Wait(.4); }
            Check(state.Food == 10 + GameState.AdFoodAmount * GameState.AdFoodPerDay && notice.Text == $"+{GameState.AdFoodAmount} food",
                $"Each watched ad pays {GameState.AdFoodAmount} food");
            Check(ad.Disabled && (ad.TooltipText + ad.AccessibilityName).Contains("No ad rations left today"), $"Ad rations stop at {GameState.AdFoodPerDay} a day");
            AuditText("Storehouse / ad rations");
            state.ApplyAdsSwitch(false);
            typeof(CashShopMenu).GetMethod("UpdateRefill", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(store, null);
            Check(!ad.IsVisibleInTree(), "Switching ads off takes the ad button out of the storehouse");
            state.ApplyAdsSwitch(true);

            // The result board: the gold tile pays the win's gold again for an ad, once.
            var gold = state.Gold;
            var board = new RoyalResult
            {
                Won = true, Stars = 3, Rewards = new List<BattleReward> { new("gold", "", 120), new("food", "", 6) },
                DoubleGold = async () => await RewardedAds.ShowAsync() && state.TryClaimAdReward(GameState.AdDoubleGold, 120, out _)
            };
            GetTree().Root.AddChild(board);
            await Wait(.3);
            var tile = Walk(board).OfType<RoyalButton>().Single(button => button.Name == "DoubleGold");
            var captions = Walk(board).OfType<RoyalLabel>();
            Check(captions.Any(label => label.Text == "×2 WITH AD") && tile.AccessibilityName == "Watch an ad to double the gold" && tile.Size.Y >= 72,
                "The victory board's gold tile offers ×2 for an ad, the tile a fingertip's size");
            await Capture("ads-victory-offer");
            tile.EmitSignal(BaseButton.SignalName.Pressed);
            await Wait(.5);
            Check(state.Gold == gold + 120 && captions.Any(label => label.Text == "DOUBLED") && captions.Any(label => label.Text == "+240") && tile.Disabled,
                "Watching it pays the gold again, shows the doubled amount, and can't be repeated");
            await Capture("ads-victory-doubled");
            AuditText("Victory / gold doubled");
            board.QueueFree();
        }
        finally
        {
            RewardedAds.TestShow = null;
            RewardedAds.IgnoreSwitch = OS.IsDebugBuild();
            state.ApplyAdsSwitch(false);
            state.ResetProgress();
        }
    }
}
