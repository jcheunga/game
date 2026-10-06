# Crownroad production launch runbook

Last implementation update: 2026-10-01; baseline repository audit: 2026-09-29.
This is the release gate for an Android/iOS
launch with paid consumables and internet rooms. Repository checks do **not**
prove that a signed store build, physical-device flow, or live deployment works.
Do not submit or enable purchases until every applicable unchecked gate below
has an owner and passing evidence. If a feature is removed from launch scope,
hide it in the release build and store copy; do not mark its gate complete.

## Where we stand

| Area | Verified in this repository | Still needed for public release |
| --- | --- | --- |
| Build and tests | Game build, 11,992 data checks and 86 server tests pass locally; Godot desktop/phone checks cover the account UI and safe account switching. The privacy smoke test also exercises consent and cloud restore. See the evidence below. | Signed mobile exports and testing of those exact builds on devices; CI currently verifies server/data, not mobile exports. |
| Backend | .NET API, server-issued anonymous and email/Google account sessions, purchase ledger/wallet, managed PostgreSQL `DATABASE_URL`, Redis rate limits, and two API replicas in production Compose. Login is connected to the existing session, wallet and cloud-save APIs. | Deploy and operate the stack, configure email/Google providers, prove live account recovery and restore/alerts/load capacity. The Compose host and its local Redis remain single points of failure. |
| Payments | Server-side Play/App Store verification and Android billing bridge are present; paid grants fail closed without valid store verification. | Real store configuration and device tests, secure iOS StoreKit 2 bridge, refund/void reconciliation, and production support procedures. |
| Multiplayer | Authenticated room relay exists server-side. | Battle client transport and physical-device reconnect/latency tests, or remove internet rooms from the public build. |
| Art and audio | Each of the ten zones battles in front of its own layered parallax backdrop, with 60 painted stage plates as a fallback. The adventure map is drawn at runtime from painted terrain materials and scenery atlases. Existing sprites, structures, icons and branding remain present. An original 19-track score (scene themes, a battle track per zone and boss themes), 212 sound-effect cues and 15 ambience soundscapes are implemented, rendered from CC0 recordings by `art/audio` (sources and licences in its README). | Approve visual quality/rights and test the signed build on physical devices. Listen to the score and effects on devices and approve the mix; some screen variants still use shared fallbacks. Stage-plate prompts and hashes are in `assets/world/manifest.json`; backdrop and map-atlas sources are described in [world artwork notes](../assets/world/README.md). |
| Store presence | Android preset targets API 36 and AAB; iOS preset has IAP capability and icon. | Console accounts, agreements, signing, listings, privacy disclosures, screenshots, ratings, testing tracks, and review approval. |

## Implementation progress and next work

Checked items here mean **implemented and verified locally**, not deployed or
approved by a store. The broader release gates below stay open until their
remaining requirements have evidence.

- [x] Disclose player-linked analytics in Settings, with no startup modal.
  Invalidate choices accepted under the old inaccurate notice.
  Analytics defaults off, requires consent at collection and sending, and
  discards queued events when consent is withdrawn.
- [x] Add a separate crash-reporting opt-in in Settings, disabled by default;
  opting into analytics does not enable crash reports. Update website wording
  to describe both choices and the data sent.
- [x] Remove session credentials, connection settings and consent choices
  from cloud-save uploads on the client and API. Filter legacy downloads;
  restore gameplay while keeping the current device's profile, active token,
  endpoints and consent. Report the actual save version instead of hardcoded 31.
- [x] Add schema migration 5 to scrub existing cloud-save rows and recompute
  hashes without deleting progress. Test pagination, repeat runs, and recovery
  after an invalid row prevents migration completion.
- [x] Correct Docker build inputs to include linked server/game sources.
  A release publish from those exact copied sources passes. Actual container
  build/start remains unverified because the local Docker daemon is unavailable.
- [x] Implement email-code and Google browser sign-in in the game and backend.
  Persist provider identities and challenges, link a verified identity to an
  authenticated guest, issue existing API sessions, revoke the current session
  on online sign-out, and protect local progress during account switching.
  Provider credentials, live delivery/consent and mobile recovery remain open
  in the [login rollout gates](#email-and-google-login-rollout) below.
- [x] Give each of the ten zones a layered parallax battle backdrop (far, mid
  and near Blender layers) that covers the battle world and band, with the
  painted stage plates kept as a fallback. `WorldArtReview.tscn` checks the ten
  backdrops, the map atlas and each zone's live map and first battle on desktop
  and phone previews. See [world artwork notes](../assets/world/README.md). This
  does not replace physical-device or public-release creative approval.

- [x] Lay out every zone as a 63-tile (9 × 7) atlas holding ten stages, ten
  supply caches, a forgotten treasury and sparse discoveries, under an opaque
  cloud layer. Travel and collection are free; each
  battle costs 4 food. Save version 46 keeps site claims and migrates older
  tile, landmark and 60-stage saves. The adventure map smoke test and the
  `--home-map` review cover rewards, free travel, entry-only charges and
  migration. See [ADVENTURE_MAP.md](ADVENTURE_MAP.md).

| Order | Next deliverable | Completion evidence / dependency |
| --- | --- | --- |
| 1 | Activate and verify the implemented email/Google account recovery | Provision SMTP and Google OAuth, deploy to staging, and pass real-device lost-device/account-switch checks for the server wallet and uploaded progress. Decide cloud-upload behavior and cross-provider linking support before launch. |
| 2 | Implement authenticated deletion and support handling | In-app request, public request page, session revocation, documented financial-record retention and end-to-end deletion evidence. |
| 3 | Deploy the privacy update to staging | Schema 5 migration and backup restore on PostgreSQL, container build/start, and device tests of updated consent/cloud restore. Follow the migration notes in DEPLOYMENT.md. |
| 4 | Complete commerce for the chosen launch platforms | Confirm Android/iOS scope, provision products, implement the iOS bridge if included, and test refund/void reconciliation. |
| 5 | Complete release infrastructure and feature acceptance | Measured load/alerts/restore; multiplayer decision; signed store-delivered device matrix and creative/localization sign-off. |

The owner decisions below remain unconfirmed. The login implementation uses
email codes and Google OAuth; provider accounts and credentials have not been
provisioned by this work. No launch platform, legal publisher or rollout date has been
selected by this work.

### Email and Google login rollout

**Status: connected to the backend and tested locally; production rollout and
live provider operation remain unverified.** The release API origin is currently
unset. The title/Settings
Account screen uses `AccountSignIn` to call the same .NET API as purchases and
cloud saves. `AccountAuth.Map(app)` registers the routes; database startup
creates `account_identities` and `account_challenges` in the shared database.
Successful sign-in uses `SessionAuth.Issue`, so the returned session authorizes
the existing protected APIs rather than a separate account service.

| Flow | Backend routes | Implemented behavior |
| --- | --- | --- |
| Availability | `GET /auth/providers` | Disable sign-in options whose server credentials are missing. |
| Email | `POST /auth/email/start`, `POST /auth/email/verify` | TLS email delivery, six-digit code, ten-minute expiry, five attempts and single-use verification. |
| Google | `POST /auth/google/start`, `GET /auth/google/callback`, `POST /auth/google/poll` | Browser OAuth with PKCE/state; the server exchanges the code with Google and verifies the identity; a separate device secret retrieves the session once. |
| Sign-out | `POST /auth/signout` | Revoke the current server session when online; local sign-out remains available offline. |
| Progress and wallet | Existing `/player-profile`, `/wallet` and `/cloud-save/*` | Register an offline guest before linking; recover the same provider's profile and wallet; download available cloud progress before switching local identity. |

- [ ] Provision a TLS-capable email service and set `SMTP_HOST`, `SMTP_PORT`
  (default `587`), `SMTP_FROM`, `SMTP_USER`, and `SMTP_PASSWORD` in the untracked
  deployment environment. Verify sender/domain delivery and failure handling
  with real inboxes. The code uses SMTP STARTTLS; choose a compatible service.
- [ ] Provision a Google OAuth **Web application** client. Set
  `GOOGLE_CLIENT_ID`, `GOOGLE_CLIENT_SECRET`, and `GOOGLE_REDIRECT_URI`.
  Register the exact callback `https://<API-host>/auth/google/callback` with
  Google; replace the example URI in `server/.env.production.example`.
  Complete the provider's production access/consent setup and test with the
  intended audience. OAuth and SMTP secrets stay on the server.
- [ ] Deploy the updated API to staging using the existing
  `server/docker-compose.production.yml`. Its `env_file` passes the login
  variables through to the API; database startup adds the account tables.
  Verify them on PostgreSQL, preservation of existing wallets/cloud saves,
  and challenges completing across both API replicas. Repeat the backend
  suite against an isolated PostgreSQL test database before rollout.
- [ ] Set `crownroad/network/api_base_url` to the final HTTPS API origin in
  every signed release. Confirm `GET /auth/providers` reports the enabled
  methods and that sign-in, wallet and cloud-save requests use that origin.
  Development can use the endpoint in Settings; do not rely on a player's
  saved endpoint for production configuration.
- [ ] Test email delivery/verification and the complete Google browser
  callback/poll flow on signed Android/iOS builds included in launch scope.
  Cover background/resume, cancellation, expired/wrong/reused codes, failed
  delivery/network, unavailable providers, rate limits and revoked sessions.
- [ ] Test guest-to-account linking, reinstall/lost-device recovery and
  account switching with real server wallet balances and uploaded saves.
  Confirm a failed cloud download keeps the current local account intact,
  and sign-out/account switches create a local progress backup. Current
  cloud uploads are **manual in Settings**: decide whether to retain this
  behavior and explain it to players or add/test automatic uploads. Signing
  in cannot recover progress that was never uploaded.
- [ ] Confirm the cross-provider account policy. An unused email or Google
  identity can link to the currently authenticated profile. Matching email
  addresses do not silently merge identities, and existing accounts/wallets
  are not merged. Test adding a second provider and an identity already tied
  to another account; define any support recovery/merge procedure.
- [ ] Complete the existing deletion/privacy/support gates below for the
  new account records and local backups before public account creation.
  Record the staging/device evidence, deployment version and rollout owner;
  then repeat provider/recovery checks against the production origin.

Local evidence: the **86-test backend suite** covers email guest linking,
same-identity recovery, wallet authorization, challenge expiry/attempts/replay,
Google PKCE/state/poll secrets, unavailable providers and session revocation.
Google success is simulated after provider verification, and email delivery
is captured by a test sender; these do not prove live provider operation.
`FeedbackReview.tscn` passes **42 checks on desktop and 42 on phone**, including
account availability/UI, guest preservation, authenticated cloud download and
failed-switch protection using a loopback service. See
[DEPLOYMENT.md](DEPLOYMENT.md#account-sign-in) for sign-in setup and
account-switch behaviour.

### Local evidence for the privacy update

- `./scripts/verify_all.sh`: game build, **11,992 data checks**, **86 server
  tests**, and public-site preview/link validation. Backend tests use a
  temporary SQLite database; this does not certify PostgreSQL deployment.
- `./scripts/smoke/privacy_smoke.sh`: **23 passing checks** in Godot 4.6.1
  Mono using a unique test save and loopback HTTP service. Covers independent
  opt-ins, withdrawal, stale consent, credential-free upload bodies,
  authenticated requests and restores from current/legacy save formats.
  `--capture` additionally checks prompt-free startup, disabled defaults and
  available Settings choices, and writes
  screenshots and a log to ignored `artifacts/privacy-review/`.
- A release publish from only the Dockerfile's copied source inputs passes.
  Neither a running Docker stack nor a production PostgreSQL instance was
  used. Re-run the backend suite against an **isolated test database** via
  `CROWNROAD_TEST_POSTGRES_CONNECTION` before staging; the tests mutate data.
- Website preview still has unset publisher/contact/legal/effective-date
  fields. It is not a production website sign-off.

The project declares Godot **4.6** in `project.godot`; the local audited editor
was **4.6.1 Mono**. Do not assume a 4.7.2 toolchain or call it a project
requirement until a migration and both signed exports have been verified.

## Decisions and access the owner must provide

- [ ] Choose the first release scope: Android, iOS, or both; whether internet
  rooms and desktop/web checkout are included. Record target countries, age
  audience, languages, launch date, and a rollback decision-maker.
- [ ] Provide legal publisher name, support contact, public website/domain,
  privacy-policy URL, terms/support/refund process, and rights/approval for all
  code, art, music, fonts, and marketing copy. Have an appropriate reviewer
  check privacy and consumer-law obligations in the launch countries.
- [ ] Establish the Apple Developer/App Store Connect and Google Play Console
  accounts. For paid products, complete each store's applicable agreements,
  payments profile, tax, and banking setup. Grant release staff least-privilege
  console access; do not share owner passwords.
- [ ] Provide a production domain and hosting account, a managed PostgreSQL
  provider with a pooled verified-TLS URL, a VM or equivalent Docker host,
  monitored backup/restore capability, and an on-call alert destination.
- [ ] Provide Android upload-key custody, a Mac with full Xcode and Apple
  signing/provisioning access, and representative physical Android/iOS test
  devices plus store sandbox/tester accounts. Keep keys and certificates out of
  Git, issue trackers, and chat.

## Engineering gates, in dependency order

### 1. Recoverable identity, privacy, and support

- [ ] Complete the [email/Google rollout](#email-and-google-login-rollout)
  before taking public paid purchases. Provider sign-in, guest linking and
  current-session revocation are implemented locally; production credentials,
  session lifecycle review, and real cross-device restore of the **server
  wallet and uploaded progression** still need evidence. A store's consumable
  purchase history is not a substitute for restoring a player's spent balance.
  Test lost-device and account-switch cases.
- [ ] Implement an in-app account-deletion request/path and a public web
  deletion-request page when account creation is offered. Define retention
  exceptions for financial/fraud records, revoke sessions, and verify the
  deletion workflow end to end. Apple and Google publish account-deletion
  requirements; review the current rules before submission.
- [ ] Audit the actual data collected by the client, API, crash/analytics
  paths, logs, and third-party SDKs. Make in-app consent and privacy wording
  accurate, publish a privacy policy in-app and on the web, and make the App
  Privacy and Play Data safety answers match it. Review retention, encryption,
  access controls, and a way for support to handle data requests.
  The website in `site/` publishes `/privacy`, `/terms`, `/support` and a web
  deletion-request section (`/support#delete-data`), written from a code audit
  on 2026-09-29 and updated for the privacy changes on 2026-09-30.
  [WEBSITE.md](WEBSITE.md) lists the launch values to fill in, store-listing
  URLs, and code each policy statement depends on. The inaccurate consent
  notice, unconditional crash uploads, and cloud-save credential fields are
  fixed locally as recorded above. Still open: staging/device verification,
  public and in-app policy availability, legal/store disclosure review,
  retention/backup handling and authenticated deletion/support.
- [ ] Define customer-support flows for a missing purchase, duplicate grant,
  account recovery, refund, abuse report, and outage. Give support a safe way
  to inspect ledger entries without exposing tokens or an admin key to the
  client.

### 2. Store commerce and ledger safety

- [ ] Reconcile all **ten** products in `data/shop_products.json` with the
  exact Apple/Google product IDs, product types, entitlements, pricing regions,
  and store-localized display prices. Review the once-per-account `starter_kit`
  semantics (it grants currency and a unit unlock) with the chosen store type.
  Do not rely on the catalog's USD fallback as a localized mobile price.
- [ ] Google Play: link the service account to the Play Console app with
  Android Publisher access; create/activate products; configure license
  testers. Test `PENDING` versus `PURCHASED`, validation-before-consume,
  interruptions, re-query on relaunch, duplicate tokens, and the one-time
  starter-kit limit. Do not grant a pending purchase.
- [ ] iOS: build a **secure StoreKit 2 bridge** that sets the deterministic
  `appAccountToken` on the StoreKit purchase, sends the transaction ID and
  verification payload to the backend, observes unfinished transactions on
  launch, and calls `finish()` only after the durable server grant. The public
  wrapper that finishes too early is intentionally not shipped. Test
  sandbox/TestFlight purchases, interrupted validation, retry, and account
  mismatch.
- [ ] Implement a refund/void signal and reconciliation path for **these
  consumables**, not just future subscriptions: App Store Server Notifications
  v2 (including `REFUND`) and Google Play voided-purchase notifications/API,
  or a documented, tested alternative with an owner and response time. Make
  ledger adjustments idempotent and auditable; decide how already-spent
  currency is handled. Reconcile store reports against grants regularly.
- [ ] Keep Stripe **off mobile** for this release. If desktop/web checkout is
  in scope, separately configure its keys, signed webhook, return URL, policy
  review, and end-to-end tests; otherwise leave it disabled.

### 3. Production backend and resilience

- [ ] Follow [DEPLOYMENT.md](DEPLOYMENT.md) to deploy a staging stack first,
  then production. Copy `server/.env.production.example` to the untracked
  `server/.env.production` **on the deployment host** and set `DATABASE_URL` to
  the provider's pooled PostgreSQL URL with verified TLS. The database is
  **not** in the API/Redis/Caddy Docker Compose stack. Restrict `AllowedOrigins`
  to real browser origins; never use `*` for production.
- [ ] Provision DNS, ports 80/443, HTTPS, managed-PostgreSQL role/connection
  limits, private Redis secret, operator key, and real store credentials in
  owner-readable secret files. `server/deploy.sh` validates the Compose/Caddy
  config, runs server/data checks, and starts the stack. The current Compose
  file mounts **both** Apple and Google credential files: an Android-only
  deployment still needs those mounts made optional in code or real credentials
  provisioned; do not insert a fake Apple key merely to make Compose start.
  Never set `CROWNROAD_ALLOW_TEST_PURCHASE_CLAIMS` in production.
- [ ] Configure provider backups/PITR or encrypted off-host dumps and perform
  a timed restore drill into a separate database. Define retention, migration,
  rollback, incident, and secret-rotation procedures. Preserve the wallet and
  purchase ledger across releases; never roll a database backward blindly.
- [ ] Monitor and alert on public `/health`, TLS renewal, API 5xx/latency,
  container restarts, Redis, PostgreSQL connections/storage, purchase
  verification failures, wallet anomalies, and backup failures. Test alerts
  reaching the on-call owner. Keep the admin key server-side only.
- [ ] Load-test the **deployed** API with a realistic mix of room polling,
  authentication, writes, WebSockets, and purchases, then set a measured
  concurrent-player target and alert thresholds. Two API replicas do not
  establish a player-capacity number. For multi-host availability/scale,
  replace host-local Redis with managed/shared Redis and put a load balancer
  in front; validate WebSocket room affinity across hosts.
- [ ] Before each signed client build, set
  `crownroad/network/api_base_url` to the final HTTPS API origin. It is blank
  by default; a blank release disables new-install online play and paid grants.
  Verify no production secret or admin endpoint is embedded in the client.

### 4. Multiplayer, gameplay, and creative sign-off

- [ ] If internet rooms are in scope, connect the battle simulation to the
  authenticated WebSocket relay and test duplicate/out-of-order events,
  reconnect, seat loss, host/process failure, latency, and abusive clients on
  real devices. Otherwise hide/disable internet rooms and remove promises of
  live multiplayer from the listing. LAN/async features need their own device
  acceptance tests if advertised.
- [ ] Decide whether current visual assets are final. Approve the score, sound
  effects and ambience by ear on devices (all audio sources are CC0, listed in
  `art/audio/README.md`) and optional route-specific screens; confirm commercial
  rights and attribution obligations. Check full-screen/cropped art on target
  devices, accessibility/legibility, touch controls, performance, battery,
  memory, cold launch, and no network/offline UX.
- [ ] Have native speakers review all advertised localizations and store copy.
  Run the data validator for missing keys/format placeholders; do not advertise
  a language based solely on machine-complete keys.
- [ ] Review economy balance, premium-grant/spend reconciliation, score
  plausibility, fraud/moderation response, crash handling, and support tools.

## Signing, store listings, and submission

### Android / Google Play

- [ ] Create the Play Console app for `com.crownroad.game`; complete developer
  verification and Play App Signing, secure the upload keystore, and create
  the ten one-time products from the catalog. The preset targets API 36, but
  recheck [Google's current target-API rule](https://support.google.com/googleplay/android-developer/answer/11926878)
  at submission time.
- [ ] Use a tested Godot Mono version with matching Android export templates,
  SDK/platform/build tools, Java, and billing plug-in. Increment the Android
  `version/code` for each uploaded build. Export a **release-signed AAB** and
  inspect its package ID, target API, billing dependency, permissions, native
  architectures, size, and excluded internal files. A local debug AAB is only
  a build-pipeline check, never the store artifact.
- [ ] Upload to Play internal testing, install **from Play** on physical
  devices, and complete the payment/interruption matrix below. Move through
  any required closed-testing and production-access steps shown in the
  Console; do not assume internal-test success grants production access.
- [ ] Complete app content/policy declarations, age/content rating, ads and
  data-safety forms, account-deletion link, support/privacy links, countries,
  pricing, and store listing. Use the original
  `assets/branding/crownroad-play-icon-512.png` and
  `assets/branding/crownroad-play-feature-1024x500.png`; capture real game
  screenshots for the required device types/locales. Check the Console's live
  asset specifications before upload.

### iOS / App Store

- [ ] Create the App ID and App Store Connect record for `com.crownroad.game`,
  enable In-App Purchase, accept the Paid Apps Agreement, and complete tax/
  banking details. Create the ten matching IAP products with localizations,
  pricing, and review metadata. Apple's [first IAP of a type must be submitted
  with a new app version](https://developer.apple.com/help/app-store-connect/manage-submissions-to-app-review/submit-an-in-app-purchase/).
- [ ] Finish the secure StoreKit bridge first. On a Mac with full Xcode,
  configure team/provisioning in `export_presets.cfg`, use a verified Godot
  Mono iOS export setup, increment iOS version/build, archive a **release**
  build, and upload it to App Store Connect. The preset's team/profile fields
  are currently blank; the icon and IAP capability are configured.
- [ ] Test the exact TestFlight build and sandbox products on physical iPhone
  and any supported iPad size. Complete App Privacy, age rating, export-
  compliance questions, support/privacy URLs, countries/pricing, reviewer
  notes/test access, localized listing text, and actual-device screenshots.
  Use `assets/branding/crownroad-app-icon-1024.png`; confirm live screenshot
  requirements in [App Store Connect Help](https://developer.apple.com/help/app-store-connect/manage-app-information/upload-app-previews-and-screenshots/).

### Physical-device acceptance matrix — both stores

Record device/OS, build hash, store environment, tester, transaction ID,
server ledger ID, and result for each case. Do not treat simulator, local
receipt stub, or a backend unit test as store certification.

- [ ] Fresh install, update, offline launch, low-memory/background/resume,
  slow/failed API, and accessibility/touch/orientation checks.
- [ ] Every catalog product: localized price/description, completed purchase,
  exact one-time server grant, balance refresh, and receipt/support record.
- [ ] Pending/canceled/declined purchase: no grant. Kill before store callback,
  before backend response, after grant but before acknowledge/consume/finish,
  and on relaunch; no lost charge or duplicate grant.
- [ ] Repeat/replay purchase token or transaction ID, account mismatch,
  lost-device login, account switch, starter-kit repurchase, refund/void, and
  support recovery. Confirm the chosen refund policy appears in the ledger.
- [ ] Verify an unavailable store or backend yields a clear recoverable state,
  never a silent lost charge, and that no mobile flow opens Stripe. Watch
  server logs/alerts during the tests.

## Final go/no-go and launch day

1. Freeze the release commit and record Godot/export-template/SDK versions,
   signed artifact hashes, backend image digest, schema version, and product
   catalog. Run `./scripts/verify_all.sh`; keep the `Verify Crownroad` workflow
   green. Neither currently builds or certifies a mobile store artifact.
2. Verify the production hostname and `/health`, restore drill, alerts,
   purchase verification, real `DATABASE_URL`, restricted origins, and absence
   of test-claim flags. Make an off-host backup before migration/deploy.
3. Use the exact store-delivered build that passed the device matrix. Submit
   complete listing/IAP metadata with reviewer notes; start in internal/closed
   testing or TestFlight, then stage a controlled public rollout.
4. Assign on-call coverage for API/store errors, crashes, failed payments,
   refunds, moderation, and support. Keep a tested commerce-disable path. Do
   not disable the API without accounting for already-charged purchases and
   unfinished store transactions.
5. Define rollback triggers and the previous known-good client/server versions.
   Preserve the current database before any rollback; use a forward repair
   when schema or ledger changes cannot be safely reversed. Reconcile every
   test and early-live purchase with the server ledger.

## Official policy/reference checks

Store rules change. Recheck these sources at submission rather than treating
this dated runbook as legal or store-policy approval:

- [Apple App Store Connect workflow and paid-app setup](https://developer.apple.com/help/app-store-connect/get-started/app-store-connect-workflow/),
  [App Privacy](https://developer.apple.com/help/app-store-connect/manage-app-information/manage-app-privacy/),
  [account deletion](https://developer.apple.com/support/offering-account-deletion-in-your-app),
  and [StoreKit transaction finishing](https://developer.apple.com/documentation/storekit/transaction/finish%28%29).
- [Apple refund notifications](https://developer.apple.com/documentation/appstoreservernotifications/notificationtype)
  and [Google Play voided-purchase notifications](https://developer.android.com/google/play/billing/rtdn-reference).
- [Google Play user-data policy](https://support.google.com/googleplay/android-developer/answer/10144311),
  [account deletion](https://support.google.com/googleplay/android-developer/answer/13327111),
  and [signed App Bundle testing](https://developer.android.com/guide/app-bundle/test).
