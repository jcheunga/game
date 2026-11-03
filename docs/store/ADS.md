# Rewarded ads

Players can choose to watch a video ad for a reward. Nothing plays unasked, and there are no banners or ads
between battles.

| Placement | Reward | Limit |
| --- | --- | --- |
| Victory board after a campaign win: tap the gold tile ("×2 WITH AD") | The win's gold again | 5 a day |
| Storehouse, Rations page: "+5 FOOD · AD" beside the gold refill | 5 food | 3 a day |

The day is the bounty board's (UTC). An ad closed before its end pays nothing and doesn't count.

## When a build has ads

The network is Google AdMob, through the CrownroadAds plugin (`native/AdMob/android-plugin`,
`native/AdMob/ios-plugin`) and `scripts/core/RewardedAds.cs`.

- **Debug builds** use Google's test app and ad units, which serve test ads and pay nothing.
- **A release build has ads only when the real IDs are set**, in project settings:
  - Android: `crownroad/ads/android_app_id` and `crownroad/ads/android_rewarded_unit`.
  - iOS: `crownroad/ads/ios_rewarded_unit`, plus the app ID in the iOS export preset's CrownroadAds option
    (GADApplicationIdentifier).

  Without them the Android export leaves the plugin and Google's SDK out, so the build has no ad SDK, no
  advertising ID permission and nothing below to declare. On iOS, turn the CrownroadAds plugin off in the
  preset for a release without ads.
- `LaunchFeatures.RewardedAds` switches the placements off altogether.

## Consent

Google's consent form (the User Messaging Platform) runs once the player first reaches the map, before any ad
loads, wherever the law asks for consent: the EEA, the UK and Switzerland. Elsewhere it shows nothing.
Settings → Account then offers **Ad privacy choices** to the players it applies to. The form's wording and
choices are set in AdMob (Privacy & messaging); create a GDPR message there before launch, or Google serves
no ads in those regions.

**[Decide] Personalized ads on iPhone.** The build doesn't ask for Apple's tracking permission (App Tracking
Transparency), so iPhone ads use no advertising identifier. They are less targeted and earn roughly half to a
third as much. Turning it on needs `NSUserTrackingUsageDescription`, an ATT prompt before the first ad, and
"Tracking: Yes" on the App Store privacy label.

## What changes in the stores and documents

These apply only to a release with ads. Google publishes the exact answers its SDK needs; **[Check]** each
against AdMob's current help pages ("Data disclosure for Google Play" and "Apple's App Store privacy details").

- **Play Console:**
  - App content → Ads: **Yes, my app contains ads**. The listing then shows "Contains ads".
  - Data safety, in addition to the current answers:
    - Location: approximate location (from the IP address).
    - Device or other IDs: the advertising ID.
    - App activity: other actions (ad interactions).
    - App info and performance: crash logs and diagnostics.

    Each is collected and shared with Google, for advertising, analytics and fraud prevention. These
    entries don't stop the account from being deleted, and data is encrypted in transit.
  - The merged manifest of the release AAB now has `com.google.android.gms.permission.AD_ID`, which is
    expected with ads.
- **App Store Connect, App Privacy:**
  - Add Coarse Location, Device ID, Product Interaction, Advertising Data, Crash Data, Performance Data and
    Other Diagnostic Data, for Third-Party Advertising and Analytics.
  - "Used to track you": No while the build doesn't request ATT.
  - GoogleMobileAds and UserMessagingPlatform ship their own privacy manifests, which Xcode merges.
- **Age rating (IARC):** "Does the app contain ads?" becomes Yes. The rating itself doesn't change. Keep the
  target audience 13 and over: a younger audience needs Google's Families-certified ad settings.
- **Listing and review notes:** say the game offers optional rewarded ads. Tell App Review where they are
  (the victory board's gold tile, the storehouse's Rations page).
- **Privacy policy:** the Ads section (site/src/privacy.html) names Google as the ad provider, says what it
  collects and links the consent choices.
- **Website:** set `admob_publisher_id` in site/site.json. The site build then publishes `app-ads.txt`,
  which AdMob checks before it serves ads to the apps.

## Accounts

1. Create a Google AdMob account (Google signs you in with the account that owns the Play Console app) and add
   payment and tax details.
2. Add two apps, Android `com.crownroad.game` and iOS `com.crownroad.game`, and a **Rewarded** ad unit in each.
3. Copy the two app IDs (`ca-app-pub-…~…`) and two ad unit IDs (`ca-app-pub-…/…`) into the settings above,
   and the publisher ID (`pub-…`) into site/site.json.
4. Create the GDPR consent message in Privacy & messaging.
5. Link each AdMob app to its store listing once the stores have published the app.

## Testing

- `UiReviewSmoke --ads` checks both placements, the daily limits and an unfinished ad, with a stand-in ad.
- `scripts/tests/android_e2e.sh` checks a debug build carries the plugin, that consent completes and that
  Google's test rewarded ad loads.
- On a phone with a debug build, watch a test ad in each placement and check the reward arrives once. Use
  Google's test ads or a registered test device; never tap your own live ads. From an EEA connection (or
  AdMob's debug geography), check the consent form appears and Settings offers Ad privacy choices.
