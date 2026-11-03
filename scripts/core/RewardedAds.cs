using System;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// Rewarded video ads: AdMob through the CrownroadAds plugin (native/AdMob) on Android and iOS. A player chooses to
/// watch one for a reward, never unasked. Debug builds use Google's test ads. A release build shows ads only once the
/// real AdMob IDs are in the project settings (crownroad/ads/...); until then the export leaves the plugin out, so the
/// build carries no ad SDK at all. Google's consent form runs first wherever the law asks for one (EEA, UK,
/// Switzerland), and Settings → Account then offers the player's ad privacy choices.
/// </summary>
public static class RewardedAds
{
    public const string Singleton = "CrownroadAds";
    // Google's published test IDs: they serve test ads, which pay nothing and are safe to tap.
    public const string TestAndroidUnit = "ca-app-pub-3940256099942544/5224354917";
    public const string TestIosUnit = "ca-app-pub-3940256099942544/1712485313";

    /// <summary>A stand-in ad for review drivers: returns whether the player earned the reward.</summary>
    public static Func<Task<bool>> TestShow { get; set; }

    private static GodotObject _plugin;
    private static bool _started, _canRequestAds, _loaded;
    private static TaskCompletionSource<bool> _showing;
    private static bool _earned;

    /// <summary>Whether Google's consent asks this player for a way to change their choice (Settings shows it).</summary>
    public static bool PrivacyOptionsRequired { get; private set; }

    public static event Action Changed;

    /// <summary>The rewarded ad unit for this platform: the project setting, or Google's test unit in a debug build.</summary>
    public static string UnitId => OS.GetName() switch
    {
        "Android" => Setting("crownroad/ads/android_rewarded_unit", TestAndroidUnit),
        "iOS" => Setting("crownroad/ads/ios_rewarded_unit", TestIosUnit),
        _ => ""
    };

    private static string Setting(string name, string test)
    {
        var value = ProjectSettings.GetSetting(name, "").AsString().Trim();
        return value.Length > 0 ? value : OS.IsDebugBuild() ? test : "";
    }

    /// <summary>Whether this build offers ads at all.</summary>
    public static bool Enabled => LaunchFeatures.RewardedAds && (TestShow != null || (UnitId.Length > 0 && Engine.HasSingleton(Singleton)));

    /// <summary>Whether an ad can be shown now: consent allows ads and one has loaded.</summary>
    public static bool Ready => Enabled && (TestShow != null || (_canRequestAds && _loaded));

    /// <summary>Starts consent and the ad SDK once, which then loads the first ad. Safe to call again.</summary>
    public static void Start()
    {
        if (_started || TestShow != null || !Enabled) return;
        _started = true;
        _plugin = Engine.GetSingleton(Singleton);
        _plugin.Connect("consent", Callable.From<bool, bool>((canRequestAds, privacyRequired) =>
        {
            _canRequestAds = canRequestAds;
            PrivacyOptionsRequired = privacyRequired;
            Changed?.Invoke();
        }));
        _plugin.Connect("loaded", Callable.From(() => { _loaded = true; Changed?.Invoke(); }));
        _plugin.Connect("failed", Callable.From<string>(message =>
        {
            GD.Print($"Rewarded ad: {message}");
            _loaded = false;
            _showing?.TrySetResult(false);
            Changed?.Invoke();
        }));
        _plugin.Connect("rewarded", Callable.From<string, int>((_, _) => _earned = true));
        _plugin.Connect("closed", Callable.From(() =>
        {
            // The plugin loads the next ad as this one closes.
            _loaded = false;
            _showing?.TrySetResult(_earned);
            Changed?.Invoke();
        }));
        // Children are not a launch audience (docs/store/RATINGS.md), so ads are not tagged for under-age users.
        _plugin.Call("start", UnitId, false);
    }

    /// <summary>Shows an ad and returns whether the player watched it through to its reward.</summary>
    public static async Task<bool> ShowAsync()
    {
        if (TestShow != null) return await TestShow();
        if (!Ready || _showing is { Task.IsCompleted: false }) return false;
        _earned = false;
        _showing = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _plugin.Call("show");
        return await _showing.Task;
    }

    /// <summary>Google's form for changing ad consent, for the Settings row that offers it.</summary>
    public static void ShowPrivacyOptions() => _plugin?.Call("privacy");
}
