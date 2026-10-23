# Launch checklist: server, sign-in, saves and purchases

What is still needed to take accounts, cloud saves, sign-in and in-app
purchases live. Do the steps in order; each depends on the ones before, mostly
through the domain and the live server. `<domain>` stands for the API
hostname, for example `api.crownroad.game`. Full setup notes are in
[DEPLOYMENT.md](DEPLOYMENT.md#account-sign-in).

Already done and tested locally (2026-10-17):

- Guest play, the guest's cloud session, cloud backup and restore, email
  sign-in, account features and sign-out, with `scripts/tests/server_e2e.sh`
  against a real local server (62 checks), and `SaveRoundTripReview.tscn` for
  the save on the device.
- Purchases are checked with the server off the main thread, so the shop keeps
  running. A purchase cut off before the server answers (timeout, lost
  connection, app closed) is granted exactly once when the store hands it back
  (part of `server_e2e.sh`). Two checks of the same purchase arriving at the
  same moment, for example from two devices, credit it once, on SQLite and on
  a real PostgreSQL (server tests).
- A purchase, a cloud-save upload or a Stripe payment no longer blanks the
  player's callsign in guild, arena and friends lists (server tests).
- Account deletion from Settings → Account → Delete account (2026-10-18). The
  server erases everything keyed by the player and ends every session; a
  repeated request answers the same. Purchase records stay, moved to a random
  `DELETED-` code with no player behind it. Sign in with Apple refresh tokens
  are now kept encrypted and revoked with Apple on deletion, retried if Apple
  is down. Covered by server tests (SQLite and PostgreSQL, Apple stubbed) and
  by `server_e2e.sh`, which deletes the second install's account.
- The iOS StoreKit 2 plugin, and a purchase from the game's own shop, on a
  simulator (82 checks), and the shop's App Store flow on desktop (35 checks).
  See section 10.
- The iOS build (NativeAOT) reads its data, catalog, save and server answers
  through System.Text.Json source generation, which writes the same files and
  requests as before (`NativeJsonReview.tscn`, 2026-10-18). The game's
  blocking server calls, which failed on iOS and Android, now work on both:
  on an Android emulator a guest gets a session, and cloud backup, Cloud
  status, restore, profile sync and account deletion reach the server
  (`scripts/tests/android_e2e.sh`, 26 checks, 2026-10-18).
- The Android build template matches Godot 4.6.1: a headless debug export
  (2026-10-18) runs the Gradle build and bundles the Play Billing library.
- An Android-only deployment needs no App Store key: Compose accepts it
  missing, and the server then refuses App Store purchases as not configured
  (Compose config check, server tests).
- Refunds (2026-10-18). App Store Server Notifications are checked up to
  Apple's root certificate, and Google Play's voided purchases are polled
  every hour. Either way, the server takes back what a refunded pack granted,
  once, with an audit trail. The game applies each refund once, across
  reloads; if the currency was already spent, it stops at zero and records the
  rest. Covered by server tests on SQLite and PostgreSQL (valid, forged,
  repeated and unknown notifications; refunds larger than the balance;
  simultaneous signals), `SaveRoundTripReview` and `server_e2e.sh`. Setup is in
  sections 9 and 10, and in [DEPLOYMENT.md](DEPLOYMENT.md#refunds). A refund
  that arrives after the account was deleted marks the kept purchase refunded
  and takes nothing back.
- Answers to Apple's refund requests (`CONSUMPTION_REQUEST`). Once switched
  on (section 10), the server tells Apple that a recorded purchase was
  delivered, once per request, within Apple's 12 hours. Players agree in the
  terms, the privacy policy describes it, and a deleted account's purchases
  are never described. Covered by server tests on SQLite and PostgreSQL (the
  request body, the switch, repeated and simultaneous deliveries, unknown
  purchases, deleted accounts, and Apple refusing or unreachable).

What follows needs real accounts, a domain, a deployed server and phones.

## 1. Decisions

- [ ] **A domain you own.** Apple only accepts HTTPS return URLs on a real
  domain, and the server needs one for its certificate.
- [ ] **Launch platforms: Android, iOS or both.** For Android only, leave
  `APPLE_IAP_PRIVATE_KEY_SOURCE` unset in `.env.production` (section 3). The
  server then starts without the App Store key and refuses App Store
  purchases as not configured. Never use a placeholder key.
- [ ] **The Adventurer's Kit (`starter_kit`): consumable or not.** It is sold
  once per player, but the game treats it as a consumable on both stores, so
  a player can pay for it twice (for example after restoring an old save). The server
  then refuses the second grant. Google refunds the payment after 3 days;
  Apple keeps it. Selling it as a non-consumable stops the second payment, but
  the game must then restore it and acknowledge it on Google instead of
  consuming it.
- [ ] **Refund policy.** Refunds are now taken back automatically (see
  [DEPLOYMENT.md](DEPLOYMENT.md#refunds)). Confirm two choices:
  - Currency the player already spent is not taken below zero. The rest is
    recorded for review.
  - A refunded Adventurer's Kit keeps its unit. The kit still counts as
    bought, so it cannot be bought again.
- [x] **Answering Apple's refund requests: how players consent.** Decided
  (2026-10-18): through the terms and privacy policy, which now say so.
  Deleting the account withdraws it. When a player asks Apple for a refund,
  the server tells Apple the purchase was delivered, which helps Apple turn
  down abuse. The switch is turned on after launch (section 10). See
  [DEPLOYMENT.md](DEPLOYMENT.md#refund-requests-app-store).

## 2. Accounts

- [ ] **Google Play Console**: $25 once. Play Games and Android purchases.
- [ ] **Apple Developer Program**: $99 a year. Game Center, Sign in with Apple
  and iOS.
- [ ] **Google Cloud**: free. Google sign-in client and the Play Games server
  client.
- [ ] **A transactional email service**, such as Postmark, Amazon SES or
  Resend.
- [ ] **A Linux VM and a managed PostgreSQL database.**

## 3. Deploy the server

See [DEPLOYMENT.md](DEPLOYMENT.md#before-the-first-deploy).

- [ ] Create the VM and install Docker with the Compose v2 plugin. Use a
  non-root account.
- [ ] In DNS, point `<domain>` (and the website domain and its `www.`) at the
  VM, and open ports 80 and 443.
- [ ] Create the PostgreSQL database with a least-privilege role. Put its
  pooled URL, with `sslmode=verify-full`, in `DATABASE_URL`.
- [ ] Copy `server/.env.production.example` to `.env.production`. Set:
  - the API and website hostnames (`CROWNROAD_SITE_DOMAIN`)
  - a monitored email address
  - the exact allowed browser origins (never `*`)
- [ ] Fill in `site/site.json` with the same website domain and the publisher
  details (see [WEBSITE.md](WEBSITE.md)).
- [ ] Create `server/secrets/` with owner-only permissions, holding
  `redis-password`, `admin-api-key`, `google-play-service-account.json` and,
  for iOS, the App Store purchase `.p8` key. Point the `*_SOURCE` entries in
  `.env.production` at them (leave `APPLE_IAP_PRIVATE_KEY_SOURCE` out for an
  Android-only launch; `deploy.sh` then says App Store purchases are off).
- [ ] Leave `CROWNROAD_ALLOW_TEST_PURCHASE_CLAIMS` and
  `CROWNROAD_DEV_SIGN_IN` unset. The server refuses to start in
  production with either set.
- [ ] Leave `STRIPE_SECRET_KEY` and `STRIPE_WEBHOOK_SECRET` empty. Stripe
  checkout is only for a desktop or web release; phones must use the store's
  own billing.
- [ ] Run `./deploy.sh`, then check that `https://<domain>/health` answers.

## 4. Email sign-in

- [ ] In the email service, verify the sending domain with its SPF and DKIM
  DNS records (and ideally DMARC), so codes don't land in spam.
- [ ] Set `SMTP_HOST`, `SMTP_PORT=587`, `SMTP_FROM` (for example
  `no-reply@your-domain`), `SMTP_USER` and `SMTP_PASSWORD`. **Use port 587,
  not 465.** The server's .NET mail client only supports STARTTLS, not the
  implicit TLS that port 465 needs.
- [ ] Request a code to your own inbox, check it isn't in spam, and sign in.

## 5. Google sign-in (browser)

- [ ] In Google Cloud, set up the OAuth consent screen:
  - app name, support email, privacy policy URL and your domain
  - scopes `openid` and `email`
  - **publish it**; while it is set to Testing, only listed test users can
    sign in
- [ ] Create an OAuth client of type **Web application**, with the redirect
  URI `https://<domain>/auth/google/callback`.
- [ ] Set `GOOGLE_CLIENT_ID`, `GOOGLE_CLIENT_SECRET` and `GOOGLE_REDIRECT_URI`
  on the server.

## 6. Google Play Games (Android)

- [ ] In Play Console, create the app `com.crownroad.game`.
- [ ] Set up Play Games Services and link it to a Google Cloud project.
- [ ] Add an **Android credential** with the package name and the SHA-1
  fingerprints of both the upload key and the Play App Signing key (Play
  Console → App integrity). A missing fingerprint makes sign-in fail silently.
- [ ] Add a **Game server credential** (a Web OAuth client). Put its ID and
  secret in `PLAY_GAMES_CLIENT_ID` and `PLAY_GAMES_CLIENT_SECRET` on the
  server.
- [ ] Copy the numeric Play Games project ID into the Godot setting
  `crownroad/play_games/project_id`.
- [ ] Add yourself as a tester until the Play Games setup is published.
- [ ] On each machine that exports Android, run
  `native/PlayGamesSignIn/build.sh` once (needs JDK 17).

## 7. Game Center (iOS)

- [ ] Turn on Game Center for the App ID `com.crownroad.game` in the Apple
  Developer account, and in the App Store Connect app record.
- [ ] Set `GAME_CENTER_BUNDLE_ID=com.crownroad.game` on the server. If it is
  unset, the server uses `APPLE_IAP_BUNDLE_ID`.
- [ ] In the Godot editor, under Project → Export → iOS, set:
  - Game Center entitlement: on
  - GameCenter plugin: on
  - StoreKit plugin: on (in-app purchases, section 10)
  - minimum iOS version: 15.0
  - 1024×1024 icon: `res://assets/branding/crownroad-app-icon-1024.png`

  The open editor has reverted these values in `export_presets.cfg` twice, so
  set them in the editor rather than in the file.
- [ ] Fill in the App Store team ID in the iOS preset (it is blank) and set up
  a provisioning profile. Game Center only works in a signed build.

## 8. Sign in with Apple

- [ ] Turn on Sign in with Apple for the App ID.
- [ ] Create a **Services ID** (for example `com.crownroad.game.signin`). Add
  `<domain>` under Domains and Subdomains, and register the return URL
  `https://<domain>/auth/apple/callback`.
- [ ] Create a key with Sign in with Apple enabled and download its `.p8`
  file. Apple allows one download only. It is a different key from the
  purchase key.
- [ ] Set on the server:
  - `APPLE_SIGN_IN_CLIENT_ID` (the Services ID)
  - `APPLE_SIGN_IN_TEAM_ID`
  - `APPLE_SIGN_IN_KEY_ID` (the key's 10-character ID)
  - `APPLE_SIGN_IN_REDIRECT_URI` (the return URL above)
  - `APPLE_SIGN_IN_PRIVATE_KEY_PATH` (the `.p8` file)
  - `ACCOUNT_TOKEN_KEY`, from `openssl rand -base64 32`. It encrypts the Apple
    refresh tokens that account deletion revokes. Sign in with Apple stays off
    without it, and production refuses to start. Keep it safe and never
    change it: stored tokens cannot be read with another key.
- [ ] Restart the server. `https://<domain>/auth/providers` should report
  every sign-in method as available.

## 9. Google Play purchases (Android)

The Play Billing plugin and its bridge are in the Android build, and the
server checks every purchase with Google before it credits it. The game
consumes a purchase only after the server's grant is saved, so Play hands an
unfinished one back on the next launch. Nothing has been bought through Google
Play yet.

- [ ] In Play Console, set up a payments profile so the app can sell.
- [x] Check the release bundle against Play's size limits (2026-10-18, see
  [BUILD_SIZE.md](BUILD_SIZE.md)). A release export is now 377 MB (it was
  712 MB): the base module is 30 MB of its 500 MB limit and the install-time
  asset pack 346 MB of 1.5 GB. Play's estimated download, from bundletool, is
  375 MB. The cuts were cheaper texture compression on the painted maps and
  pictures, which looks the same on a phone, and leaving out art the game
  never loads. It is still over 200 MB, so Play asks before downloading it
  over mobile data. The iOS preset needs the same exclude filter, added in
  the editor.
- [ ] After the first upload, check Play Console → App size against those
  figures.
- [ ] Add testers under License testing, and upload a signed build to the
  internal testing track. Play only offers in-app products once a build with
  the billing library is uploaded, and purchases only work in a build Play
  knows, installed by a tester.
- [ ] Create the ten one-time products with the Google product IDs in
  `data/shop_products.json` (`gold_pouch` through `campaign_resupply`), with
  prices and titles, and activate them.
- [ ] In Google Cloud, turn on the **Google Play Android Developer API**,
  create a service account and download a JSON key for it. Save it as
  `server/secrets/google-play-service-account.json` (section 3). Keep
  `GOOGLE_PLAY_PACKAGE_NAME=com.crownroad.game`.
- [ ] In Play Console → Users and permissions, invite the service account's
  email with access to the app and the **View financial data** and **Manage
  orders and subscriptions** permissions. It can take hours before Google
  accepts its requests. **View financial data** also lets the server read
  refunds (the Voided Purchases API). Play refunds need no Real-time developer
  notifications or Pub/Sub.
- [ ] After deploying, poll for refunds once by hand. Check that it answers
  `ok` (see [DEPLOYMENT.md](DEPLOYMENT.md#refunds)):
  `curl -fsS -X POST -H "X-Admin-Api-Key: …" https://<domain>/admin/refunds/poll-google`
- [ ] On an Android phone signed in with a license tester's account:
  - Each pack arrives once, at its Play price, and the server's purchase
    ledger has the order.
  - Cancelling grants nothing.
  - With the test card that approves after a few minutes, nothing arrives
    while it waits, and the pack arrives once it clears, even with the shop
    closed. The card that declines never grants anything.
  - Closing the app before the server answers: the pack arrives on the next
    launch, once.
  - The Adventurer's Kit can be bought once per player.
  - Refund a tester's pack in Play Console (Order management → Refund). Within
    an hour (the poll interval), opening the shop takes it back and says so,
    and `GET /admin/refunds` lists it.

## 10. App Store purchases (iOS)

The StoreKit plugin (`native/StoreKit`) is built and enabled in the iOS preset
(`plugins/StoreKit`). On a simulator, with Xcode-signed transactions and a
local server, it loads prices, buys, validates and only then finishes, and
handles Ask to Buy and interrupted purchases. The game itself runs there too:
it loads its data and catalog, and a pack bought from the shop is granted once
and finished (`native/StoreKit/test/simulator_e2e.sh`, 82 checks). The shop's
handling of every outcome passes on desktop
(`native/StoreKit/test/shop_desktop.sh`). With Apple:

- [ ] In App Store Connect, accept the Paid Apps Agreement and fill in the tax
  and banking details.
- [ ] Create the ten in-app purchases, as **consumables**, with the product
  IDs in `data/shop_products.json` (`com.crownroad.gold_pouch` through
  `com.crownroad.campaign_resupply`), prices, localizations and review
  screenshots. The Adventurer's Kit's type follows the decision in section 1.
  The first ones go to review with a new app version.
- [ ] Create an **In-App Purchase** key (Users and Access → Integrations) and
  download its `.p8` file. It is a different key from the Sign in with Apple
  key. Set on the server:
  - `APPLE_IAP_ISSUER_ID` and `APPLE_IAP_KEY_ID`
  - `APPLE_IAP_BUNDLE_ID=com.crownroad.game`
  - `APPLE_IAP_PRIVATE_KEY_PATH` (the `.p8` file)
- [ ] Turn on refunds. In App Store Connect → the app → App Information →
  App Store Server Notifications, choose **Version 2**. Set both the
  Production and Sandbox server URLs to
  `https://<domain>/store/apple/notifications`. Ask Apple for a test
  notification with `POST /admin/refunds/apple-test?environment=sandbox` (and
  without `?environment=sandbox` for production). Check that each appears in
  `GET /admin/refunds` as a `TEST` event. Optionally set
  `APPLE_IAP_APP_APPLE_ID` to the app's numeric Apple ID. See
  [DEPLOYMENT.md](DEPLOYMENT.md#refunds).
- [ ] After launch, once `/terms` and `/privacy` with the refund-request
  lines are live, set `APPLE_IAP_SEND_CONSUMPTION_INFO=1` in
  `.env.production` and rerun `./deploy.sh`, so the server answers Apple's
  refund requests (section 1).
- [ ] Create a sandbox Apple Account (Users and Access → Sandbox) to buy with.
- [ ] Rebuild the plugin with `native/StoreKit/build.sh` whenever the project
  changes Godot version.
- [ ] On an iPhone with a TestFlight or development build, signed in to the
  sandbox account:
  - Each pack arrives once, at its App Store price, and the server's purchase
    ledger has the transaction. Apple's own server now checks it, which the
    simulator test cannot.
  - Cancelling the payment sheet grants nothing.
  - An Ask to Buy purchase, or a sandbox purchase interrupted by the payment
    sheet, arrives once it is approved, and a declined one never arrives.
    Xcode's test store can't check these two: it signs a declined request
    like a completed purchase.
  - Closing the app before the server answers: the purchase arrives on the
    next launch, once, and is not charged twice.
  - A purchase made while one player was signed in is not credited to a
    different player on the same Apple Account.
  - A refunded sandbox purchase (see Apple's sandbox testing guide) arrives
    as a `REFUND` notification. The next time the shop opens, its currency is
    taken back, once.
  - If you switch on `APPLE_IAP_SEND_CONSUMPTION_INFO=1` for this test,
    asking for that refund first
    shows a `CONSUMPTION_REQUEST` in `GET /admin/refunds` with outcome
    `consumption_sent`, meaning Apple accepted the answer. The server tests
    can't check that.

## 11. Point the game at the live server

- [ ] Set the Godot setting `crownroad/network/api_base_url` to
  `https://<domain>`, then export signed builds.
- [ ] Point the end-to-end test at a staging copy of the live server, to check
  everything except the provider sign-ins in one run.

## 12. Test on real phones

- [ ] **Android:**
  - A guest reaches the live server over HTTPS: in Settings → Account, Cloud
    save uploads without signing in and Cloud status shows the backup. (The
    game's blocking server calls failed on Android until 2026-10-18. An
    emulator now passes this against a local server, along with restore,
    profile sync and account deletion: `scripts/tests/android_e2e.sh`. Run
    the script on a phone too; it builds with the phone's own Vulkan
    renderer, which the emulator could not run.)
  - The game draws normally: the emulator's Vulkan froze the game, so its
    run used OpenGL instead.
  - Play Games signs you in by itself at launch and keeps your guest
    progress.
  - The Play Games button works.
  - Uninstalling and reinstalling brings your progress back.
- [ ] **iPhone:**
  - First check that the app launches at all. Apple may require a newer app
    startup method for apps built with Xcode 27, which Godot 4.6 may not meet.
  - The Game Center sheet works, as do cancelling it and an already signed-in
    player.
  - Sign in with Apple works, and cancelling it is handled.
  - Reinstalling brings your progress back.
- [ ] **Every platform:**
  - Google and email sign-in work.
  - A second device recovers the same account.
  - Cloud backup and restore work, and so does sign-out.
  - The purchase checks in sections 9 and 10.

## 13. Before store review

- [x] Use the official Google and Apple sign-in button designs. Done
  2026-10-23: Account's sign-in buttons are drawn to
  [Google's branding guidelines](https://developers.google.com/identity/branding-guidelines),
  [Apple's Sign in with Apple guidelines](https://developer.apple.com/design/human-interface-guidelines/sign-in-with-apple),
  the [Play Games branding guidelines](https://developer.android.com/games/pgs/branding)
  and the [Game Center guidelines](https://developer.apple.com/design/human-interface-guidelines/game-center),
  with Google's own G and Play Games controller. Each rule and how the button
  meets it, with sources and licences, is in
  [art/signin/README.md](../art/signin/README.md); `UiReviewSmoke --account`
  measures them.
- [ ] Add Apple's official Sign in with Apple logo and Game Center icon. They
  need Apple Developer Program membership and accepting Apple's artwork
  licences, so the project owner downloads them from
  [Apple Design Resources](https://developer.apple.com/design/resources/).
  Which files to pick and the one command that wires them in are under
  "Adding Apple's art" in [art/signin/README.md](../art/signin/README.md).
  Until then both buttons show an empty logo box, which App Review would
  reject.
- [x] Build account deletion. Apple requires it, and it must revoke the
  deleted player's Apple sign-in tokens, which means storing them first.
  Done: see "Already done" at the top.
- [ ] Once Sign in with Apple works on the live server, sign in with Apple on
  a test account, delete it in Settings, and check that Crownroad no longer
  appears under Sign in with Apple in the iPhone's Apple ID settings. Only
  Apple's real endpoint proves the revocation.
- [ ] Set how long the PostgreSQL provider keeps backups, and give that period
  in the privacy policy's "Deleting your account" section, which now says only
  that backup copies go "as those backups expire".
- [ ] Fill in Play's Data safety form and Apple's App Privacy labels from the
  draft answers in [STORE_PRIVACY_ANSWERS.md](STORE_PRIVACY_ANSWERS.md), after
  working through its review list. The privacy policy (`site/src/privacy.html`)
  was rewritten from the same audit on 2026-10-18 and covers email addresses,
  linked sign-in IDs, player IDs, purchase history, account deletion and
  refunds. Have both reviewed, then set the policy's `effective_date` in
  `site/site.json`.
