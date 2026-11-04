# Rewarded ads

Players can choose to watch a video ad for a reward. Nothing plays unasked, and there are no banners or ads
between battles.

| Placement | Reward | Limit |
| --- | --- | --- |
| Victory board after a campaign win: tap the gold tile ("×2 WITH AD") | The win's gold again | 5 a day |
| Storehouse, Rations page: "+5 FOOD · AD" beside the gold refill | 5 food | 3 a day |

The day is the bounty board's (UTC). An ad closed before its end pays nothing and doesn't count.

## Ready in the store builds, off until you switch them on

The store builds carry the ads from the first release, and a switch on the server decides whether players see
them. Turning ads on or off needs no app update and no store review.

The network is Google AdMob, through the CrownroadAds plugin (`native/AdMob/android-plugin`,
`native/AdMob/ios-plugin`) and `scripts/core/RewardedAds.cs`.

- **The real IDs go in project settings before the first store build.** `scripts/release/preflight.py` fails
  until they are set:
  - `crownroad/ads/android_app_id` and `crownroad/ads/android_rewarded_unit`.
  - `crownroad/ads/ios_app_id` and `crownroad/ads/ios_rewarded_unit`.
  - `admob_publisher_id` in `site/site.json`, for `app-ads.txt`.
- **The exports follow those settings.** With both Android IDs set, the Android export includes the plugin and
  Google's SDK. `scripts/release/ios_release.sh` turns the plugin on in its temporary copy of the iOS preset,
  with the app ID from `crownroad/ads/ios_app_id`, and checks that the exported Info.plist has it. Without the
  IDs, both exports leave the ad SDK out, as before. The committed iOS preset keeps the plugin off; don't tick
  it by hand.
- **The switch starts off.** While it's off, a release build never starts Google's SDK, shows no consent form,
  loads no ad and offers no ad button. Nothing reaches Google.
- **Debug builds ignore the switch.** They always use Google's test app and ad units, which serve test ads and
  pay nothing.
- **Building the plugins.** `native/AdMob/android-plugin/build.sh` and `native/AdMob/ios-plugin/build.sh`.
  The iOS script downloads Google's Mobile Ads SDK for iOS (13.12.0 on 2026-11-04) the first time.
  Google's frameworks aren't committed, so run it on each new checkout that exports iOS. The preflight
  checks they're there.
- `LaunchFeatures.RewardedAds` switches the placements off in the code, for a build that should never show
  ads whatever the server says.

### The switch

```bash
# Is it on?
curl -fsS -H "X-Admin-Api-Key: $KEY" "$API/admin/ads"
# Switch ads on (or off, with false):
curl -fsS -X POST -H "X-Admin-Api-Key: $KEY" -H "Content-Type: application/json" \
  -d '{"enabled":true}' "$API/admin/ads"
```

It lives in the database (`live_settings`, the row `ads.enabled`) beside the purchase pause. Every API replica
follows a change within 10 seconds. The game reads `GET /live-settings` when the map opens, at most every 5
minutes, and keeps the last answer in its save, so a launch without a connection keeps the last answer.

- **On:** the next time the map opens, the game asks for consent where the law needs it, then loads an ad,
  and the two ad buttons appear.
- **Off:** the buttons disappear the next time the map opens. A game that has already started Google's SDK
  keeps it running until the app restarts, but offers no ads.

Before switching ads on the first time:
1. Check AdMob shows the apps as ready, with `app-ads.txt` verified and the GDPR message published.
2. Add the sentence about ads to both store descriptions ([LISTING.md](LISTING.md)).
3. Switch ads on, then watch an ad in each placement on a phone with the store build.

## Consent

Google's consent form (the User Messaging Platform) runs once the player first reaches the map, before any ad
loads, wherever the law asks for consent: the EEA, the UK and Switzerland. Elsewhere it shows nothing.
Settings → Account then offers **Ad privacy choices** to the players it applies to. The form's wording and
choices are set in AdMob (Privacy & messaging); create a GDPR message there before launch, or Google serves
no ads in those regions.

**No personalized ads on iPhone (decided 2026-11-04).** The build never asks for Apple's tracking permission
(App Tracking Transparency), so iPhone ads use no advertising identifier. They are less targeted and earn
roughly a third to a half as much, in exchange for no permission prompt and "Tracking: No" on the App Store
privacy label. Don't add `NSUserTrackingUsageDescription` to the plist. Reversing this later needs that key,
an ATT prompt before the first ad, and "Tracking: Yes" for the advertising data on the label.

**Android ads stay personalized (decided 2026-11-04).** Android has no tracking prompt, so the iPhone reasons
don't apply. Google's SDK reads the advertising ID, players in consent regions choose on the consent form,
and anyone can opt out in the phone's own settings. The Data safety answers below already list the
advertising ID.

## What changes in the stores and documents

The store builds carry Google's ad SDK, and the switch can turn ads on without a review, so give these
answers from the first release, even while the switch is off. Declaring ads the build can show is accurate;
leaving them out until the switch flips would be wrong for that whole time. Google publishes the exact answers
its SDK needs; **[Check]** each against AdMob's current help pages ("Data disclosure for Google Play" and
"Apple's App Store privacy details").

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
  - "Used to track you": No, because the build doesn't request ATT (decided above).
  - GoogleMobileAds and UserMessagingPlatform ship their own privacy manifests, which Xcode merges.
- **Age rating (IARC):** "Does the app contain ads?" becomes Yes. The rating itself doesn't change. Keep the
  target audience 13 and over: a younger audience needs Google's Families-certified ad settings.
- **Listing:** the descriptions say nothing about ads while the switch is off. Add the sentence in
  [LISTING.md](LISTING.md) when you switch them on. Play shows "Contains ads" from the first release either way.
- **Review notes:** tell App Review and Play that the game has optional rewarded ads switched by the server,
  where they appear (the victory board's gold tile, the storehouse's Rations page), and whether they're on
  during review ([REVIEW_NOTES.md](REVIEW_NOTES.md)). Apple asks that no feature be hidden from review.
- **Privacy policy:** the Ads section (site/src/privacy.html) names Google as the ad provider, says what it
  collects and links the consent choices.
- **Website:** set `admob_publisher_id` in site/site.json. The site build then publishes `app-ads.txt`,
  which AdMob checks before it serves ads to the apps.

## Accounts

Do these before the first store build; the release preflight needs the IDs.

1. Create a Google AdMob account (Google signs you in with the account that owns the Play Console app) and add
   payment and tax details.
2. Add two apps, Android `com.crownroad.game` and iOS `com.crownroad.game`, and a **Rewarded** ad unit in each.
3. Copy the two app IDs (`ca-app-pub-…~…`) and two ad unit IDs (`ca-app-pub-…/…`) into the settings above,
   and the publisher ID (`pub-…`) into site/site.json.
4. Create the GDPR consent message in Privacy & messaging.
5. Link each AdMob app to its store listing once the stores have published the app.

## Testing

- `UiReviewSmoke --ads` checks both placements, the daily limits, an unfinished ad and the switch (a release
  build offers nothing while it's off), with a stand-in ad.
- The server tests (`AdsSwitch`) and `scripts/tests/server_e2e.sh` check the switch starts off, needs the
  admin key, and reaches the game, its save and the ad buttons.
- `scripts/tests/android_e2e.sh` (emulator) and `native/StoreKit/test/simulator_e2e.sh` (its `ads` phase, iOS
  simulator) check a debug build carries the plugin, that consent completes and that Google's test rewarded
  ad loads.
- On a phone with a debug build, watch a test ad in each placement and check the reward arrives once. Use
  Google's test ads or a registered test device; never tap your own live ads. From an EEA connection (or
  AdMob's debug geography), check the consent form appears and Settings offers Ad privacy choices.
