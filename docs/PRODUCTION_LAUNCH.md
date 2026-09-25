# Crownroad production launch gate

## Current status

The game can build and its backend tests pass, but it is **not ready to submit
to either store today**. Paid currency is deliberately fail-closed until the
store credentials below are installed. This protects players from an accidental
charge without a durable server grant.

Implemented in this repository:

- A server-issued player session is required for every player-owned,
  competitive, social, cloud-save, commerce, and room-relay route. A relay
  connection also needs a live seat in that room.
- Challenge sync uses the game’s camel-case payload contract, acknowledges
  individual submission IDs, and records each ID only once. The real Godot
  smoke flow obtains a session before it submits, and has been verified against
  PostgreSQL.
- Every paid grant is recorded in a server wallet and immutable per-purchase
  ledger. Replayed receipts are idempotent; a recovery retry can finish a
  previously granted Play purchase without crediting it again.
- The server verifies Google Play products directly with the Android Publisher
  API, and verifies App Store transactions through the App Store Server API
  once credentials are present. No store token is kept in plaintext.
- Android is configured for API level 36/AAB delivery and includes the official
  Godot Google Play Billing 3.3.0 plug-in. A local debug AAB builds with the
  expected `com.crownroad.game` package and billing dependency, while excluding
  backend source and internal documentation. It validates with the server before
  consuming a consumable and rechecks unfinished purchases after relaunch.
- The iOS export has the app icon and In-App Purchase capability configured.
- Store artwork is available in `assets/branding/`.

## Blocking work before submission

| Gate | Owner | Definition of done |
| --- | --- | --- |
| Real player account/recovery | Product + engineering | Sign in with Apple/Google or an equivalent account is implemented. The present device-backed anonymous session is not sufficient to recover paid currency after a lost device. |
| iOS StoreKit bridge | Engineering + Xcode | A StoreKit 2 plug-in submits the verified transaction ID/JWS and a deterministic `appAccountToken` to the backend, then calls `finish()` only after backend success. The current public wrapper finishes too early, so it is intentionally not included. |
| Android device test | Release engineering | Install an internal-test AAB on a physical Play test device; purchase every consumable; force-close before and after validation; verify exactly one grant and eventual consume. |
| iOS device test | Release engineering | Repeat the same interruption tests in StoreKit sandbox/TestFlight after the secure bridge is built. |
| Server deployment | Operations | Follow [DEPLOYMENT.md](DEPLOYMENT.md): DNS and public ports are ready, the HTTPS stack is live, secrets are mounted, off-host backups and alerts have been tested, CORS is restricted, and `CROWNROAD_ALLOW_TEST_PURCHASE_CLAIMS` is absent. |
| Store listings | Product/marketing | Legal name, support URL/email, privacy policy, age/content ratings, screenshots, localized copy, and pricing are entered in both consoles. |
| In-game art and audio | Art + design | Replace the fallback atlas treatment with licensed final art/audio, or explicitly approve the fallback assets as final and update the coverage catalog. The current audit still reports many named unit, environment, icon, music, and SFX slots without dedicated assets. |
| Real-time room transport | Engineering + QA | Connect battle simulation to the authenticated WebSocket relay, then prove reconnect, host migration/failure, latency, and duplicate-event handling on physical devices. The server relay is secure, but the game currently uses room polling and has no battle transport. |
| Economy and abuse review | Design + engineering | Reconcile all premium grants/spends from server state, tune score plausibility checks with live telemetry, and complete a fraud/moderation review before public competitive play. |

## Store assets

| Asset | File | Ready for |
| --- | --- | --- |
| iOS master icon | `assets/branding/crownroad-app-icon-1024.png` | App Store Connect (1024 × 1024) |
| Google Play icon | `assets/branding/crownroad-play-icon-512.png` | Play Console (512 × 512) |
| Google Play feature graphic | `assets/branding/crownroad-play-feature-1024x500.png` | Play Console (1024 × 500) |

The icon and feature graphic are original generated launch artwork. Screenshots
are still required: capture the actual running game on each target device, with
no mock UI or unsupported text overlays. Use the final 16:9/phone layout and
show campaign, combat, progression, and the shop only after it is connected to
the appropriate store sandbox.

## Payments configuration

Copy `server/.env.example` into the deployment secret store. Do not commit a
filled `.env` file, service-account JSON, `.p8` key, keystore, or provisioning
profile.

### Google Play

1. Create the Play Console app using package ID `com.crownroad.game` and keep
   its upload key outside the repository.
2. Create all ten one-time consumable products with the exact Google IDs in
   `data/shop_products.json`.
3. Link a Google Cloud service account to Play Console with Android Publisher
   permission. Mount its JSON key securely and set:
   `GOOGLE_PLAY_PACKAGE_NAME` and `GOOGLE_PLAY_SERVICE_ACCOUNT_JSON_PATH`.
4. Add license testers and exercise internal testing. Never test with a
   production buyer account.

### App Store

1. Register the App ID `com.crownroad.game`, enable In-App Purchase, and create
   matching consumable product IDs from `data/shop_products.json`.
2. Create an App Store Server API In-App Purchase key and configure
   `APPLE_IAP_ISSUER_ID`, `APPLE_IAP_KEY_ID`, `APPLE_IAP_BUNDLE_ID`, and the
   mounted `APPLE_IAP_PRIVATE_KEY_PATH`.
3. Finish and device-test the secure StoreKit bridge described above before
   enabling any App Store product for sale.
4. Add App Store Server Notifications v2 before a subscription, refund, or
   entitlement product is introduced. This launch catalog is consumable-only.

### Stripe

Stripe is retained for desktop/web checkout only. It must not be presented as a
way to buy virtual currency from the mobile apps. Configure both
`STRIPE_SECRET_KEY` and `STRIPE_WEBHOOK_SECRET`, test webhook signatures, and
use a real HTTPS return URL before enabling it outside development.

## Build and release steps

1. Use Godot 4.7.2 Mono (the project is now configured for it): it supplies an
   API-36 Android template. Install the Android build template, Android SDK API
   36/platform and build tools, Java 17, and an Android upload signing key.
   The `android/` Gradle template is generated locally and intentionally not
   versioned. Export a signed release **AAB**, then upload it to Play internal
   testing. The checked local AAB is debug-signed only and must not be uploaded.
2. Install full Xcode (not Command Line Tools), sign in with the Apple
   Developer account, select the provisioning profile/team in
   `export_presets.cfg`, build the generated iOS Xcode project, and archive it
   for TestFlight.
3. Set `AllowedOrigins` to the real HTTPS origin list. A production server with
   no origins does not expose a CORS policy.
4. Set `crownroad/network/api_base_url` to the final HTTPS API origin before
   the release export. It locks the build to that backend and derives the
   online-sync and purchase-validation addresses; leaving it blank disables
   online play and paid grants on a new install.
5. Run the verification suite from the project root:

   ```sh
   ./scripts/verify_all.sh
   ```

6. Complete the physical purchase, restore, refund, offline, and interruption
   checks in the gates above. Review live server logs and wallet ledger entries
   for every test transaction.

Godot's C# mobile exporter remains experimental. Treat the local AAB as a
build-pipeline check, not device certification; test the exact signed release
on representative Android hardware before promotion.

## Launch-day controls

- Start with internal testing, then closed testing/TestFlight; do not launch
  both stores to the public at once.
- Keep a remote kill switch or remove the commerce endpoint when verification,
  wallet, or store API alerts fire. The client already refuses to charge when
  native billing or backend validation is unavailable.
- Back up the database before each release; retain purchase and wallet ledger
  records for support/refund reconciliation.
- Publish privacy policy, support contact, terms, and age ratings before review.
- Set up on-call ownership for payment failures, refund requests, crashes, and
  server availability.
