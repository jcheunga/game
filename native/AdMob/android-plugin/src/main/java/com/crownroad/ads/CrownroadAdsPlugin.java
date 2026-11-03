package com.crownroad.ads;

import android.app.Activity;

import androidx.annotation.NonNull;

import com.google.android.gms.ads.AdError;
import com.google.android.gms.ads.AdRequest;
import com.google.android.gms.ads.FullScreenContentCallback;
import com.google.android.gms.ads.LoadAdError;
import com.google.android.gms.ads.MobileAds;
import com.google.android.gms.ads.rewarded.RewardedAd;
import com.google.android.gms.ads.rewarded.RewardedAdLoadCallback;
import com.google.android.ump.ConsentInformation;
import com.google.android.ump.ConsentRequestParameters;
import com.google.android.ump.UserMessagingPlatform;

import org.godotengine.godot.Godot;
import org.godotengine.godot.plugin.GodotPlugin;
import org.godotengine.godot.plugin.SignalInfo;
import org.godotengine.godot.plugin.UsedByGodot;

import java.util.HashSet;
import java.util.Set;

/**
 * Rewarded video ads from AdMob, for the game's RewardedAds. start() runs Google's consent form where the law asks for
 * one, then starts the SDK and loads an ad; show() plays it when the player asks. Every call runs on the UI thread, and
 * the result comes back as a signal: consent, loaded, failed, rewarded, closed. The export adds the AdMob app ID the
 * SDK needs to the manifest (addons/CrownroadAds/export_plugin.gd).
 */
public class CrownroadAdsPlugin extends GodotPlugin {
    private String unitId = "";
    private boolean started, initialized, loading;
    private RewardedAd ad;

    public CrownroadAdsPlugin(Godot godot) {
        super(godot);
    }

    @NonNull
    @Override
    public String getPluginName() {
        return "CrownroadAds";
    }

    @NonNull
    @Override
    public Set<SignalInfo> getPluginSignals() {
        Set<SignalInfo> signals = new HashSet<>();
        signals.add(new SignalInfo("consent", Boolean.class, Boolean.class));
        signals.add(new SignalInfo("loaded"));
        signals.add(new SignalInfo("failed", String.class));
        signals.add(new SignalInfo("rewarded", String.class, Integer.class));
        signals.add(new SignalInfo("closed"));
        return signals;
    }

    /** Asks for consent where needed, then starts the SDK and loads the first ad. Only the first call counts. */
    @UsedByGodot
    public void start(String rewardedUnitId, boolean underAgeOfConsent) {
        Activity activity = getActivity();
        if (activity == null || started) return;
        started = true;
        unitId = rewardedUnitId;
        activity.runOnUiThread(() -> {
            ConsentInformation consent = UserMessagingPlatform.getConsentInformation(activity);
            ConsentRequestParameters parameters = new ConsentRequestParameters.Builder()
                .setTagForUnderAgeOfConsent(underAgeOfConsent)
                .build();
            consent.requestConsentInfoUpdate(activity, parameters,
                () -> UserMessagingPlatform.loadAndShowConsentFormIfRequired(activity, formError -> afterConsent(activity, consent)),
                requestError -> afterConsent(activity, consent));
        });
    }

    private void afterConsent(Activity activity, ConsentInformation consent) {
        boolean privacyRequired = consent.getPrivacyOptionsRequirementStatus() == ConsentInformation.PrivacyOptionsRequirementStatus.REQUIRED;
        emitSignal("consent", consent.canRequestAds(), privacyRequired);
        if (!consent.canRequestAds() || initialized) return;
        initialized = true;
        MobileAds.initialize(activity, status -> loadOnUiThread(activity));
    }

    /** Loads the next ad, unless one is loaded or loading. */
    @UsedByGodot
    public void load() {
        Activity activity = getActivity();
        if (activity != null) activity.runOnUiThread(() -> loadOnUiThread(activity));
    }

    private void loadOnUiThread(Activity activity) {
        if (!initialized || loading || ad != null) return;
        loading = true;
        RewardedAd.load(activity, unitId, new AdRequest.Builder().build(), new RewardedAdLoadCallback() {
            @Override
            public void onAdLoaded(@NonNull RewardedAd loaded) {
                loading = false;
                ad = loaded;
                emitSignal("loaded");
            }

            @Override
            public void onAdFailedToLoad(@NonNull LoadAdError error) {
                loading = false;
                emitSignal("failed", "Couldn't load an ad: " + error.getMessage());
            }
        });
    }

    /** Plays the loaded ad. Emits rewarded once it has been watched, closed when it goes, and loads the next. */
    @UsedByGodot
    public void show() {
        Activity activity = getActivity();
        if (activity == null) return;
        activity.runOnUiThread(() -> {
            RewardedAd shown = ad;
            if (shown == null) {
                emitSignal("failed", "No ad is ready.");
                return;
            }
            ad = null;
            shown.setFullScreenContentCallback(new FullScreenContentCallback() {
                @Override
                public void onAdDismissedFullScreenContent() {
                    emitSignal("closed");
                    loadOnUiThread(activity);
                }

                @Override
                public void onAdFailedToShowFullScreenContent(@NonNull AdError error) {
                    emitSignal("failed", "Couldn't show the ad: " + error.getMessage());
                    loadOnUiThread(activity);
                }
            });
            shown.show(activity, reward -> emitSignal("rewarded", reward.getType(), reward.getAmount()));
        });
    }

    /** Google's form for changing ad consent (Settings → Account → Ad privacy choices). */
    @UsedByGodot
    public void privacy() {
        Activity activity = getActivity();
        if (activity == null) return;
        activity.runOnUiThread(() -> UserMessagingPlatform.showPrivacyOptionsForm(activity, formError -> {
            ConsentInformation consent = UserMessagingPlatform.getConsentInformation(activity);
            afterConsent(activity, consent);
        }));
    }
}
