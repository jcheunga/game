using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// The iOS ad plugin on a simulator (native/StoreKit/test/simulator_e2e.sh, phase "ads"): a debug build carries the
/// CrownroadAds plugin with Google's test ad unit, Google's consent check completes, and the test rewarded ad loads, so
/// the plugin, Google's frameworks and the app ID in the Info.plist all work. Watching an ad needs a person.
/// </summary>
public partial class RewardedAdsReview : Node
{
    private int _failures;

    public override void _Ready() => Callable.From(Run).CallDeferred();

    private void Check(bool ok, string description) { GD.Print($"STOREKIT_CHECK: {(ok ? "PASS" : "FAIL")} {description}"); if (!ok) _failures++; }
    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    private async void Run()
    {
        try
        {
            if (!OS.HasFeature("ios")) throw new Exception("Run this on an iOS simulator with native/StoreKit/test/simulator_e2e.sh.");
            Check(Engine.HasSingleton(RewardedAds.Singleton) && RewardedAds.Enabled && RewardedAds.UnitId == RewardedAds.TestIosUnit,
                "A debug build carries the ad plugin with Google's test ad unit");
            RewardedAds.Start();
            var deadline = DateTime.UtcNow.AddSeconds(60);
            while (!RewardedAds.Ready && DateTime.UtcNow < deadline) await Wait(.25);
            Check(RewardedAds.Ready, "Google's consent check completes and a test rewarded ad loads");
        }
        catch (Exception e) { Check(false, $"Driver error: {e}"); }
        GD.Print($"STOREKIT_RESULT: ads {_failures} failures");
    }
}
