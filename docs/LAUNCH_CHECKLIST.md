# Launch checklist: server, sign-in and saves

What is still needed to take accounts, cloud saves and sign-in live. Do the
steps in order; each depends on the ones before, mostly through the domain and
the live server. `<domain>` stands for the API hostname, for example
`api.crownroad.game`. Full setup notes are in
[DEPLOYMENT.md](DEPLOYMENT.md#account-sign-in).

Already done and tested locally (2026-10-16): guest play, the guest's cloud
session, cloud backup and restore, email sign-in, account features and
sign-out, with `scripts/tests/server_e2e.sh` against a real local server
(43 checks), and `SaveRoundTripReview.tscn` for the save on the device. What
follows needs real accounts, a domain, a deployed server and phones.

## 1. Decisions

- [ ] **A domain you own.** Apple only accepts HTTPS return URLs on a real
  domain, and the server needs one for its certificate.
- [ ] **Launch platforms: Android, iOS or both.** The production Compose file
  needs both the Google Play and the App Store purchase key files to start.
  An Android-only launch needs a small code change first.

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
  `redis-password`, `admin-api-key`, `google-play-service-account.json` and the
  App Store purchase `.p8` key. Point the four `*_SOURCE` entries in
  `.env.production` at them.
- [ ] Leave `CROWNROAD_ALLOW_TEST_PURCHASE_CLAIMS` and
  `CROWNROAD_DEV_SIGN_IN` unset. The server refuses to start in
  production with either set.
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
- [ ] Fix the Android build template mismatch: the installed template is for
  Godot 4.7.2 but the project uses 4.6.1. Reinstall the 4.6.1 template
  (Project → Install Android Build Template), or move the project to 4.7.

## 7. Game Center (iOS)

- [ ] Turn on Game Center for the App ID `com.crownroad.game` in the Apple
  Developer account, and in the App Store Connect app record.
- [ ] Set `GAME_CENTER_BUNDLE_ID=com.crownroad.game` on the server. If it is
  unset, the server uses `APPLE_IAP_BUNDLE_ID`.
- [ ] In the Godot editor, under Project → Export → iOS, set:
  - Game Center entitlement: on
  - GameCenter plugin: on
  - StoreKit plugin: on (in-app purchases, section 9)
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
- [ ] Restart the server. `https://<domain>/auth/providers` should report
  every sign-in method as available.

## 9. App Store purchases (iOS)

The StoreKit plugin (`native/StoreKit`) is built and enabled in the iOS preset
(`plugins/StoreKit`). On a simulator, with Xcode-signed transactions and a
local server, it loads prices, buys, validates and only then finishes, and
handles Ask to Buy and interrupted purchases
(`native/StoreKit/test/simulator_e2e.sh`, 61 checks). The shop's side passes on
desktop (`native/StoreKit/test/shop_desktop.sh`). First:

- [ ] Make the game's JSON work in the iOS build (NativeAOT cannot run
  reflection-based System.Text.Json). Until then the game cannot load its data
  or catalog on iOS, so the shop is empty. See
  [PRODUCTION_LAUNCH.md](PRODUCTION_LAUNCH.md#ios--app-store).

Then, with Apple:

- [ ] In App Store Connect, accept the Paid Apps Agreement and fill in the tax
  and banking details.
- [ ] Create the ten in-app purchases, as **consumables**, with the product
  IDs in `data/shop_products.json` (`com.crownroad.gold_pouch` through
  `com.crownroad.campaign_resupply`), prices, localizations and review
  screenshots. The first ones go to review with a new app version.
- [ ] Create an **In-App Purchase** key (Users and Access → Integrations) and
  download its `.p8` file. It is a different key from the Sign in with Apple
  key. Set on the server:
  - `APPLE_IAP_ISSUER_ID` and `APPLE_IAP_KEY_ID`
  - `APPLE_IAP_BUNDLE_ID=com.crownroad.game`
  - `APPLE_IAP_PRIVATE_KEY_PATH` (the `.p8` file)
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

## 10. Point the game at the live server

- [ ] Set the Godot setting `crownroad/network/api_base_url` to
  `https://<domain>`, then export signed builds.
- [ ] Point the end-to-end test at a staging copy of the live server, to check
  everything except the provider sign-ins in one run.

## 11. Test on real phones

- [ ] **Android:**
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
  - A test purchase works in the store's sandbox.

## 12. Before store review

- [ ] Use the official Google and Apple sign-in button designs. The game draws
  them in its own style today, and both companies have branding rules.
- [ ] Build account deletion. Apple requires it, and it must revoke the
  deleted player's Apple sign-in tokens, which means storing them first.
- [ ] Fill in Play's Data safety form and Apple's privacy labels to cover
  email addresses and player IDs, and make sure the privacy policy covers
  them.
