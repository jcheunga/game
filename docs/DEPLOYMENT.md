# Crownroad backend deployment

This is a production baseline for one region: Docker Compose runs Redis and two
API replicas on a private network, while Caddy serves the public HTTPS endpoint.
The API connects to a managed PostgreSQL provider through one `DATABASE_URL`.
Redis lives in a persistent Docker volume. The API host and its local Redis
remain single-host infrastructure; use managed Redis and multiple hosts before
a large public launch.

## What this deploys

- HTTPS with managed certificates and HTTP-to-HTTPS redirect.
- Two private API replicas by default: port 8080 is not exposed on the host.
- A managed PostgreSQL connection supplied by `DATABASE_URL`; PostgreSQL never
  shares the API container or Docker host.
- Private Redis with a password-protected persistent volume. It keeps rate
  limits consistent across API replicas, while authenticated players are
  limited by session rather than shared public IP address.
- Caddy balances ordinary HTTP traffic across replicas. Room WebSockets use
  room-path affinity so the players in one room reach the same relay process,
  even if a client later adds query parameters.
- Docker-mounted provider credentials rather than credentials in images or
  application environment variables.
- An independent operator key for `/admin`, `/stats`, `/analytics/summary`,
  and `/admin/balance`.
- The public website (home, support, privacy and terms pages) from `site/` on
  `CROWNROAD_SITE_DOMAIN`, with `www.` redirecting to it. `deploy.sh` builds it
  first and stops if any launch value is unset. See [WEBSITE.md](WEBSITE.md).

The API never creates database dumps in its own container. Use PostgreSQL
provider backups, point-in-time recovery, or encrypted off-host dumps and
rehearse a restore before releasing paid currency.

## Before the first deploy

1. Create a Linux VM and install Docker Engine plus the Docker Compose v2
   plugin. Use a non-root deployment account.
2. Point an A (and, if used, AAAA) record such as `api.your-domain.example` at
   the VM, plus records for the website domain and its `www.` alias. Make ports
   80 and 443 available from the public internet. Caddy uses that reachability
   to provision and renew certificates.
3. Copy the release repository to the server and work from its `server/`
   directory.
4. Copy `.env.production.example` to `.env.production`, then set the API
   hostname, the website hostname (`CROWNROAD_SITE_DOMAIN`), a monitored email
   address, and the exact browser origins. Do not use `*` as an origin. Fill in
   `site/site.json` with the same website domain and the publisher details
   described in [WEBSITE.md](WEBSITE.md).
5. Provision the managed PostgreSQL database and create a least-privilege
   application role. Put its **pooled** PostgreSQL URL in `DATABASE_URL` in
   `.env.production`. It must use verified TLS, such as
   `sslmode=verify-full`; do not use `Ssl Mode=Disable` for a public provider.
   Treat the populated environment file as a secret and do not commit it.
6. Create `secrets/` (the repository path is `server/secrets/`) with owner-only permissions and add the files
   `redis-password`, `admin-api-key`, `google-play-service-account.json`, and,
   for iOS, the App Store private-key `.p8` file. Point the matching `*_SOURCE`
   entries in `.env.production` at those paths. Generate a unique, high-entropy admin key.
   Never commit this directory or the populated environment file.
7. Fill the Google Play and App Store identifiers. Do not set
   `CROWNROAD_ALLOW_TEST_PURCHASE_CLAIMS`; the server rejects it in production.
   Leave Stripe keys empty unless a separately compliant desktop/web checkout
   is being launched.
8. Keep `CROWNROAD_SERVER_REPLICAS=2` and the `max_pool_size=40` URL option for
   the initial host. This caps the two API replicas at 80 pooled PostgreSQL
   connections. If either value is raised, first confirm that the provider's
   pooler, connection limit, memory, CPU, and monitoring capacity support the
   new total.

If iOS is not ready, launch Android-only: leave `APPLE_IAP_PRIVATE_KEY_SOURCE`
unset or blank. Compose then mounts the empty `server/empty-secret` file
instead, the server refuses App Store purchases as not configured, and
`deploy.sh` prints a note saying so. Do not fabricate an Apple key. The Google
Play key stays required. The iOS purchase plugin
is in `native/StoreKit`; test it with sandbox purchases on a device before you
offer iOS purchases (see [LAUNCH_CHECKLIST.md](LAUNCH_CHECKLIST.md#10-app-store-purchases-ios)).

## Release command

From the server directory on the deployment VM:

```sh
./deploy.sh
```

The command checks the production Compose and Caddy configuration, runs the
server and game-data checks, builds the image, then starts the API and Caddy.
It does not create DNS records, buy a domain, upload store credentials, or
configure your backup provider.

The API image now builds from the repository root so linked C# sources are
available. Compose selects `server/Dockerfile` automatically. For a manual
build, run `docker build -f server/Dockerfile .` from the repository root.
`server/Dockerfile.dockerignore` allows only the required source files into
the build context; populated environment files, databases and secrets are
excluded. A release publish using those inputs was checked locally on
2026-09-30; an actual container build/start still needs a running Docker daemon.

After the hostname resolves, verify the public health endpoint:

```sh
curl --fail --show-error https://api.your-domain.example/health
```

Opening `https://api.your-domain.example/admin` displays a browser credential
prompt. Use any username and the `admin-api-key` file’s value as the password.
For an API client, send that value in an `X-Admin-Api-Key` request header. Do
not use the administrator credential in the game client.

## Privacy upgrade: schema 5

The 2026-09-30 update removes session credentials, connection settings and
consent choices from cloud saves. New uploads are filtered on both sides;
downloads are filtered even if a backup or older server introduced a legacy
row. Migration 5 rewrites existing cloud-save JSON and its hash while retaining
progress, the save format version and upload time. It leaves the purchase
ledger and active authentication sessions unchanged.

1. Rehearse against a separate restored PostgreSQL database first. The local
   migration tests use SQLite and do not certify the production provider.
2. Back up the database and stop old API replicas during the upgrade so they
   cannot write unfiltered saves while the migration runs. Start the new API
   version, confirm schema version 5, then restore traffic.
3. Invalid legacy JSON stops startup with a privacy-migration error and does
   not mark schema 5 applied. The original row is retained for repair through
   a restricted database session. Do not paste save blobs into logs or issues;
   they can contain credentials. Repair from a verified save and retry; the
   migration safely resumes already-cleaned rows.
4. Verify current-client upload/restore with an existing account: progress
   returns, the active session remains valid, and local consent choices remain
   unchanged. Analytics enabled under the old inaccurate notice is disabled
   until the player opts in through Settings; crash reporting is a separate opt-in.
5. Backups and snapshots are not rewritten by this migration. Keep them
   restricted and handle them under the approved retention/incident procedure.
   Restoring a pre-upgrade database requires migration 5 again. Avoid rolling
   back to a server that writes unfiltered save credentials.

## Operations after launch

- Monitor HTTPS certificate renewal, `/health`, container restarts, API error
  rate, PostgreSQL connection saturation, Redis availability,
  payment-verification failures, and backup completion. Attach alerts to an
  on-call destination before enabling paid purchases.
- Enable encrypted PostgreSQL backups and point-in-time recovery with your
  hosting provider, or create encrypted off-host PostgreSQL dumps at least
  daily; retain the wallet ledger for reconciliation.
- Before each release, run the verification suite, create an off-host backup,
  deploy, verify `/health`, and test a sandbox purchase. Roll back by restoring
  the prior application version only after preserving the current database.
- Rotate the admin key and store credentials through the secret files, then
  rerun `./deploy.sh`. Treat a leaked key as an incident and rotate it promptly.

## Refunds

When Google or Apple refunds a pack, the server takes back what it granted.

- **App Store:** Apple pushes App Store Server Notifications v2 to
  `POST /store/apple/notifications`. The server believes a notification only
  after checking its signature: the `x5c` chain must lead to Apple Root CA - G3
  (pinned in `server/AppStoreNotifications.cs`, never taken from the payload),
  both certificates must carry Apple's App Store markers, and the leaf must
  have made the ES256 signature. The transaction inside is checked the same
  way, and both must name `APPLE_IAP_BUNDLE_ID`. `REFUND` takes the
  purchase's currency back. `REFUND_REVERSED` gives it back. `REVOKE` is about
  Family Sharing, which consumables don't use, so it is recorded and ignored,
  like every other type. Like Apple's own library with online checks off, the
  server does not ask Apple's OCSP responder whether a certificate was revoked.
- **Google Play:** the server polls the Voided Purchases API
  (`purchases.voidedpurchases.list`) every hour with the purchase-verification
  service account. Polling needs no Pub/Sub topic or public endpoint, and it
  reports refunds, chargebacks and Google's own revocations. Purchases are
  matched by order ID, or by purchase token if Google omits the order ID. Each
  poll looks back two days past the last one, because Google can list a void
  late. Replicas share one claim, so one replica polls per interval.
- **Purchase checks:** a refunded purchase is never granted again. If the store
  hands it back, `/purchase/validate` answers `refunded`, and the game finishes
  the store transaction without a grant. If Apple reports a transaction as
  refunded before its notification arrives, the refund is recorded then.

Every signal is stored once in `store_events`, the audit trail, under a
unique key: Apple's notification UUID, or Google's order ID and void time. A
repeated notification, or a void listed by the next poll, changes nothing. A
refund marks the purchase `refunded` and writes reversing `wallet_ledger`
entries (reason `refund`) under a new `wallet_adjustments` row. If several
signals for one purchase arrive at once, one wins. Refunds of transactions the
server never credited are acknowledged and recorded as `unknown_transaction`.

The save on the device is what the player sees, so the server never edits it.
The game fetches `GET /wallet/adjustments` when the server answers (at most
every ten minutes, or at once after the account changes) and whenever the
shop opens. It applies each adjustment once, keyed by its ID (saved as
`AppliedWalletAdjustments`, like `AppliedPurchaseIds`), and the shop says what
was taken back. Once the save is on disk, the game reports what it took with
`POST /wallet/adjustments/ack`. The server keeps listing every adjustment, so
a cloud save restored from before a refund gets it applied again.

Decisions:

- **Currency already spent is not taken below zero.** The device takes what
  the balance holds and records the rest as unrecovered, in the save and on
  the server (`goldUnrecovered` and `foodUnrecovered` in `/admin/refunds`).
  Gold and food are also earned in play, and every price and the HUD assume a
  balance of zero or more. A negative balance would lock a player out of
  spending what they earn later, without saying why. The ledger still reverses
  the full grant, so the unrecovered amount stays visible for abuse review.
- **The Adventurer's Kit's unit stays unlocked.** The kit's gold and food are
  taken back, but its unit may be leveled, equipped and in the loadout by
  then. A refunded kit still counts as bought, so it cannot be bought again
  for a second unit.
- **A reversed refund gives back only what was taken**, not what the player had
  already spent.
- A refund applies only to a save that received the grant. A save whose grant
  never arrived (its response was lost) gives nothing back.

To set up:

1. **App Store Connect** → the app → App Information → App Store Server
   Notifications: choose **Version 2**. Set both the Production and Sandbox
   server URLs to `https://<domain>/store/apple/notifications`. Then ask
   Apple for a test notification, which the server requests with the
   purchase key (add `?environment=sandbox` for the sandbox URL):

   ```sh
   curl -fsS -X POST -H "X-Admin-Api-Key: $(cat secrets/admin-api-key)" https://<domain>/admin/refunds/apple-test
   ```

   Within a minute it should appear in `GET /admin/refunds` as a `TEST`
   event, outcome `ignored`.
2. On the server, `APPLE_IAP_BUNDLE_ID` must be set; without it notifications
   are answered 503 and Apple retries them for days. Optionally set
   `APPLE_IAP_APP_APPLE_ID` to the app's numeric Apple ID (App Information →
   Apple ID). Production notifications for another app ID are then refused.
3. **Play Console:** no Real-time developer notifications are needed. The
   service account that verifies purchases needs **View financial data** for
   the Voided Purchases API (the same invitation as in
   [LAUNCH_CHECKLIST.md](LAUNCH_CHECKLIST.md#9-google-play-purchases-android)).
   Optionally change the interval with `GOOGLE_PLAY_VOIDED_POLL_MINUTES`
   (5 to 1440, default 60). After deploying, run one poll by hand and check it
   answers `ok`:

   ```sh
   curl -fsS -X POST -H "X-Admin-Api-Key: $(cat secrets/admin-api-key)" https://<domain>/admin/refunds/poll-google
   ```

Operators can see the latest store events, with the adjustment each made and
what the device took back, at `GET /admin/refunds`. They can also record a
refund the stores don't report, such as a Stripe refund or a support
correction, with `POST /admin/refunds` and
`{"purchaseId": "PUR-…", "action": "refund" | "reinstate", "note": "…"}`
(`transactionId` instead of `purchaseId` also works). Both need the admin
key.

Schema 8 brings these tables. It also makes ledger entries unique per
purchase, currency and adjustment, instead of per purchase and currency. On
PostgreSQL this drops the old unique constraint; SQLite rebuilds the table. It
is safe to repeat. Rehearse it on a restored copy of the database like any
migration.

Not built yet:

- Answering Apple's `CONSUMPTION_REQUEST` with consumption information, which
  helps Apple decide refund requests. It is recorded and ignored.
- Stripe refund webhooks. Record those refunds through `/admin/refunds`.
- Partial (quantity) refunds. Every pack is sold one at a time, so a void
  always refunds the whole purchase.

## Migrating an existing SQLite deployment

Do not mount an existing `crownroad.db` file into the PostgreSQL stack. Take a
read-only backup, schedule maintenance, migrate into a separate PostgreSQL
database, validate player, purchase, wallet, and ledger counts, then cut the
API over to PostgreSQL. Keep the SQLite snapshot immutable until the recovery
window closes. A new deployment has no data migration step.

## Account sign-in

Players sign in from **Account** in the home map's More panel or in Settings.
Every option is optional: **Play as guest** keeps the journey on the device.

- **Guest.** Each install has its own profile. As soon as the server answers,
  the game registers that guest and gets a session, so automatic cloud backup
  after won battles works without signing in.
- **Google Play Games** (Android) and **Game Center** (iOS) appear first when
  the build has the platform plugin and the server is configured. The device
  proves the player to the server (a Play Games server auth code, or a GameKit
  identity signature) and the server verifies it itself.
- **Apple** (Sign in with Apple) and **Google** sign in through the browser.
  The game opens the provider's page and polls the server with a device
  secret that never passes through the browser; the browser carries only a
  random state. Google adds PKCE. Apple has no PKCE, so the server puts a nonce
  derived from a private verifier into the request and checks that Apple
  signed the same nonce into the ID token. Apple posts its answer back to the
  server (`response_mode=form_post`). The server exchanges the code using a
  five-minute client secret, an ES256 JWT signed with the team's `.p8` key. It
  then verifies the ID token against
  [Apple's published keys](https://appleid.apple.com/auth/keys) and checks its
  issuer, audience, expiry and nonce. The game asks Apple for no name or
  email, so the account is Apple's stable user ID. A cancelled Apple sign-in
  fails the challenge at once, so the game stops waiting.
- **Email** uses a six-digit one-time code (ten-minute lifetime, five attempts).

The server links the guest to the first identity only with a valid guest
session, and new identities keep local guest progress. Each identity is
separate unless linked from the signed-in account. Sign-out revokes the current
server session when online.

Play Games also signs players in by itself at launch. When that happens the
game links a guest automatically, but only when no progress is at risk: the
Play Games account is this guest, is new and takes this progress along, or
replaces an install that has not started the campaign. Otherwise the player
chooses in Account. After an explicit sign-out the device stays a guest until
the player picks an account again.

To enable live sign-in:

1. Set the game's HTTPS API origin (see below), or for local development enter
   the server endpoint under Settings → Account → Payments → Connection details.
2. Set `SMTP_HOST`, `SMTP_PORT`, `SMTP_FROM`, `SMTP_USER` and `SMTP_PASSWORD`
   for email delivery over TLS.
3. Create a Google OAuth **Web application** client. Set `GOOGLE_CLIENT_ID`,
   `GOOGLE_CLIENT_SECRET` and `GOOGLE_REDIRECT_URI` on the server, and register
   that exact HTTPS callback URI, ending in `/auth/google/callback`, with
   Google. Local callbacks may use loopback HTTP.
4. **Google Play Games.** In Play Console, set up Play Games Services for the
   app and add a *Game server* credential, which is an OAuth **Web
   application** client. Set `PLAY_GAMES_CLIENT_ID` and
   `PLAY_GAMES_CLIENT_SECRET` on the server to that client. The server hands the
   client ID to the game, so it is not built into the app. In Godot, set
   `crownroad/play_games/project_id` to the numeric Play Games project ID; the
   Android export adds it to the manifest. Build the plugin once per machine
   with `native/PlayGamesSignIn/build.sh` (JDK 17 and the Android SDK; it
   copies the AARs into `addons/PlayGamesSignIn/bin`, which is not committed).
   Without the AARs the export prints a warning and leaves Play Games out.
5. **Game Center.** Enable Game Center for the App ID and set
   `GAME_CENTER_BUNDLE_ID` (it falls back to `APPLE_IAP_BUNDLE_ID`). The iOS
   build includes Godot's official GameCenter plugin in
   `ios/plugins/gamecenter`, built from `native/GameCenter` (see its README).
   The iOS export preset enables it (`plugins/GameCenter`) together with the
   Game Center entitlement (`entitlements/game_center`). The plugin signs with
   `fetchItemsForIdentityVerificationSignature` and reports the
   `teamPlayerID`, which is the ID Apple signs. Rebuild it with
   `native/GameCenter/build.sh` whenever the project changes Godot version:
   it is compiled against that exact engine's headers. The server only fetches
   signing certificates from HTTPS `*.apple.com` URLs, requires a trusted
   chain, accepts a signature once within a 15-minute window and checks it
   against the configured bundle ID.
6. **Sign in with Apple.** In the Apple Developer account, under
   Certificates, Identifiers & Profiles:
   1. Turn on *Sign in with Apple* for the app's App ID (`com.crownroad.game`).
   2. Create a **Services ID**, for example `com.crownroad.game.signin`, and
      turn on Sign in with Apple for it. Choose the App ID as its primary App
      ID, add the API domain under *Domains and Subdomains*, and register the
      exact return URL `https://<API domain>/auth/apple/callback`. Apple
      accepts only HTTPS return URLs on a real domain, so Apple's side cannot
      be tested against `localhost`. Use staging or a tunnel.
   3. Create a **key** with Sign in with Apple enabled for that App ID and
      download its `.p8` file. Apple lets you download it only once. This key
      is separate from the In-App Purchase key in `APPLE_IAP_PRIVATE_KEY_PATH`.

   On the server, set `APPLE_SIGN_IN_CLIENT_ID` to the Services ID, along with
   `APPLE_SIGN_IN_TEAM_ID`, `APPLE_SIGN_IN_KEY_ID` (the key's 10-character
   ID) and `APPLE_SIGN_IN_REDIRECT_URI` (the return URL above). Supply the key
   as `APPLE_SIGN_IN_PRIVATE_KEY_PATH` (a mounted file) or as
   `APPLE_SIGN_IN_PRIVATE_KEY`, the PEM text on one line with newlines
   written as `\n`. With Docker Compose, put the one-line form in
   `.env.production`. A Compose secret for this key would have to exist on
   every deployment, Android-only ones included, so none is defined.

   Also set `ACCOUNT_TOKEN_KEY` to 32 random bytes in base64
   (`openssl rand -base64 32`), inline or as a file through
   `ACCOUNT_TOKEN_KEY_PATH`. The server keeps each player's Apple refresh
   token encrypted with it (AES-256-GCM), because deleting an account must
   revoke Sign in with Apple and only a refresh token can. Without the key
   Sign in with Apple stays off, and production refuses to start with
   `APPLE_SIGN_IN_CLIENT_ID` set. Never change the key: tokens stored under
   the old one can no longer be revoked.
7. Restart the server. `/auth/providers` reports which sign-in methods are
   configured; the game disables buttons for unavailable providers.

The Play Games button appears only on Android builds with the plugin and a
project ID, and the Game Center button only on iOS builds with the plugin.
**Continue with Apple** sits directly above **Continue with Google** on every
platform. App Store guideline 4.8 requires an equivalent privacy-focused login
wherever a third-party login such as Google is offered, so configure Apple
before any iOS release that shows Google. If Apple is not configured, hide the
Google button on iOS. Two App Review points are still open:

- Apple's [button guidelines](https://developer.apple.com/design/human-interface-guidelines/sign-in-with-apple)
  call for the Apple logo and an approved style. The button uses the game's
  own style and a generic icon, like the Google button.
- When account deletion is built (see
  [PRODUCTION_LAUNCH.md](PRODUCTION_LAUNCH.md)), Apple requires the app to
  [revoke the Apple tokens](https://developer.apple.com/documentation/signinwithapplerestapi/revoke-tokens)
  of a deleted account. The server keeps no Apple tokens today, so that work
  also needs to store the refresh token, encrypted, at sign-in.

### End-to-end test against a local server

`scripts/tests/server_e2e.sh` starts a throwaway server on `127.0.0.1:8097`
(change it with `E2E_PORT`), with a fresh SQLite database. It then plays the
game against it headless, in two installs:

1. **First install.** A guest gets a session and backs up after a win. It then
   signs in with email through the real Account dialog, after a wrong code is
   refused, and the account keeps the guest's progress. Then:
   - Every account feature runs: manual cloud upload, achievements, daily
     score and leaderboard, challenge results with their leaderboard and feed,
     an online room (host, list, session, ready, scoreboard, leave), and a test
     purchase credited to the wallet.
   - Purchases interrupted before the server answers are granted once. Then
     a refund (an operator refund, which takes the same path as the stores'
     signals) reaches the device. Opening the shop takes it back once and says
     why, and the server learns what was taken. A reload or Play handing the
     purchase back applies nothing more. A refund of gold already spent stops
     at zero, and both sides record the rest.
   - A late-game save goes up to the cloud. Sign-out revokes the session on the
     server; signing back in restores the account, and every one of the
     save's values comes back unchanged.
2. **Server restart**, to show accounts and saves live in the database.
3. **Second install.** It signs in to the same account and restores its
   progress, then backs up a new win. A profile ID without a session cannot
   download the save, and Settings' restore works. With the server gone, a win
   still saves on the device without delay; when the server returns, the save
   uploads again.

The account is verified without sending email: the server runs with
`CROWNROAD_DEV_SIGN_IN=1`, which prints each sign-in code to its log
instead of mailing it, and the test reads the code from there. The test also
sets `CROWNROAD_ALLOW_TEST_PURCHASE_CLAIMS=1`. The server refuses to start in
production with either variable set. It also gives the server a throwaway
`ADMIN_API_KEY` for the refunds.

### Developer sign-in

To sign in while developing without codes or providers, run a local server in
development mode with the same switch:

```sh
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:8080 CROWNROAD_DEV_SIGN_IN=1 dotnet run --project server --no-launch-profile
```

Then, in a debug build of the game:

1. Enter `http://127.0.0.1:8080` as the server endpoint under Settings →
   Account → Payments → Connection details.
2. Turn on developer mode in Settings → Account → Developer tools.
3. Open Account and press **Developer sign-in (no code)**. It signs in to the
   typed email, or `developer@example.com` when the box is empty, as a
   verified email account: the same account a real code would reach.

The button only appears in debug builds with developer mode on, and only a
server started with `CROWNROAD_DEV_SIGN_IN=1` accepts it; production refuses to
start with that variable. Printed codes still work for the normal email flow.
The game accepts plain HTTP only from this computer (loopback). A phone can't
reach the Mac's loopback address, so a phone build needs an HTTPS development
or staging server, not running in production mode, with the switch on.

Google, Apple, Play Games and Game Center are not part of this test. They need
real credentials and, for the last two, a signed phone build.

### Save round-trip review

`SaveRoundTripReview.tscn` checks the save on the device. Run it with an isolated
save suffix starting with `save-review-`:

```sh
godot --headless --path . res://scenes/tests/SaveRoundTripReview.tscn -- --save-suffix=save-review-$(date +%s)
```

It fills a late-game save across systems and every setting, reloads it from
disk and compares every GameState field. Nothing may be lost or changed;
entries that loading adds, such as every roster unit at level 1, are listed as
`SAVE_ADDED`. It then nudges every number and switch in the save file, loads
it and checks each comes back. The values the loader deliberately normalizes,
such as the consent version, are listed with their reasons. It also lists
GameState fields that have no place in the save (`SAVE_UNMATCHED`), which
should all be screen selections, in-battle flags or caches. Then it checks
store refunds. A refund is applied once, however often the server reports it
and across reloads. A refund larger than the balance stops at zero and keeps
the rest on record. A reversed refund gives back only what was taken. A refund
of a purchase the save never received takes nothing. A late-game cloud save is
about 100 KB, against the server's 512 KB limit.

### What still needs a real device

The server's provider checks run against stand-ins in the test suite. An
unsigned Debug and Release device build of an iOS export, made with Xcode 27
from Godot 4.6.1's official template, links the GameCenter plugin and GameKit
and carries the Game Center entitlement. Since 2026-10-17 this Mac has an iOS
27 simulator runtime, and the StoreKit plugin's purchases pass there
(`native/StoreKit/test/simulator_e2e.sh`, see `native/StoreKit/README.md`).
That run found two things about the iOS build itself:

- **The game cannot read its JSON on iOS.** The C# export is NativeAOT, which
  turns off System.Text.Json's reflection, and every game data file, the save
  and the server calls use it. On the simulator the game logs "Failed to load
  game data from JSON. Saving is disabled for this session", and the shop's
  catalog is empty. Turning reflection back on
  (`JsonSerializerIsReflectionEnabledByDefault`) is not enough: NativeAOT then
  lacks the generic code it needs. The serialized types need System.Text.Json
  source generation before iOS can ship.
- **Godot 4.6.1's simulator engine is x86_64 only**, and iOS 27 simulators run
  arm64 apps only, so the official template cannot run in this Mac's simulator.
  The StoreKit test compiles the engine for the arm64 simulator from source.

Before an iOS release, check on a device:

- **Game Center.** The App ID and App Store Connect record need Game Center
  enabled, and the build must be signed with a matching provisioning profile.
  Check the sign-in sheet, cancelling it (afterwards iOS expects the player to
  sign in from Settings), an already signed-in player, and a real identity
  signature verified by the server against Apple's live certificate.
  Reinstall on the same Apple Account and check that the profile comes back.
- **Sign in with Apple.** The Services ID, domain, return URL and key must be
  live. Check that Safari opens Apple's page, that a cancel stops the game
  waiting, and that a second device recovers the same profile. Check iPhone,
  Android and desktop builds, since the button shows everywhere.
- **The export itself.** The C# iOS export is still experimental in Godot
  4.6, and the game's full export (NativeAOT, signing, App Store upload) has
  not been run. Xcode 27 builds only for iOS 15 and later, so the preset's
  minimum iOS version is now 15.0. The 4.6 editor also renamed the main icon
  option to `icons/icon_1024x1024`, which is now set; the export fails while
  it is blank.

Provider credentials are not included. Live email delivery and the Google and
Apple consent pages need the configured services and a manual device check.
The server exchanges authorization codes directly with each provider. It reads
Google's verified identity from its userinfo endpoint, following
[Google's OpenID Connect flow](https://developers.google.com/identity/openid-connect/openid-connect),
and verifies Apple's ID token following
[Apple's token validation guide](https://developer.apple.com/documentation/signinwithapple/verifying-a-user).

Before switching accounts, the game downloads the selected account's cloud
save when available and writes current local progress to
`user://account-backups/`. A failed download leaves the current account intact.
Account labels, session secrets and the sign-out choice are excluded from cloud
saves. Progress uploads after each won battle once the device has a session,
and manually from Settings. Backup files include the device session, so treat
them like private saves.

### Account deletion

Settings → Account → Delete account asks for confirmation, then calls
`POST /account/delete` with the device's session (`server/AccountDeletion.cs`).
In one transaction the server deletes every row keyed by the profile: the
player, linked sign-ins (email, Google, Apple, Game Center, Play Games),
pending sign-in codes, sessions, cloud save, challenge results, achievements,
daily completions, arena snapshot and results, raid contributions, analytics,
crash reports, friendships and gifts in both directions, player reports made by
or about the player, room seats and telemetry, rooms the player hosted (their
titles carry the callsign), guild membership, wallet and wallet ledger. A guild
the player led passes to its top contributor; an empty one is deleted. Other
players' arena results keep their row with the opponent ID blanked. The
server test `AccountDeletionCoversSchema` fails when a new column looks like it
holds a profile ID and the deletion does not clear it.

Kept on purpose: `purchases` rows, for tax records, refunds and fraud checks,
and because the unique transaction ID stops a store purchase being credited
again to a new account. Their `profile_id` becomes `DELETED-` plus 32 random
hex characters, one code per deleted account, with no player, wallet or
session behind it, and `/player-profile` refuses IDs with that prefix. Refund
handling must not try to debit a wallet for such a profile. The wallet ledger
is deleted rather than kept: it repeats what each purchase row records, for a
currency that no longer exists.

The device deletes its local account backups for that profile, starts a new
guest and turns off automatic sign-in. The deleted account's session hashes go
into `deleted_sessions` for 31 days, so a repeated request (for example after
a lost response) gets `alreadyDeleted: true` instead of 401. Sign in with Apple
is revoked at `https://appleid.apple.com/auth/revoke` with the refresh token
kept from sign-in. If Apple cannot take it, the sealed token waits in
`pending_token_revocations`, and the cleanup service retries it, backing off
from 15 minutes to a day, for 30 attempts. `invalid_grant` (already revoked)
ends the retries.

Another device still signed in to the deleted account is not told yet. Its old
session gets 401 everywhere, but it keeps its local progress, and if it
registers its profile ID again through `/player-profile` that ID comes back as
a new guest, which can upload that device's progress.

## Game release configuration

Deploying the API alone does not make a mobile build use it. Before exporting a
release, set `crownroad/network/api_base_url` in the Godot project settings to
the final HTTPS origin, for example `https://api.your-domain.example`. A
non-empty value locks the release configuration and overrides any player-saved
endpoint. The build derives both service addresses from it:

- online sync: `https://api.your-domain.example/challenge-sync`
- purchase validation: `https://api.your-domain.example`

It starts blank by design so local development never accidentally talks to a
live service. The signed build must be retested against the deployed API and
the relevant store sandbox.

Android 15+ devices and Google Play require every native library to be 16 KB
page-aligned. Godot's own libraries are, but the .NET runtime libraries
(`libmonosgen-2.0.so`, `libSystem.*.Native.so`) are only 16 KB-aligned from
.NET 9 on, so `Game.csproj` must target `net10.0` or later. On `net8.0` the
phone warns that the app "is not 16 KB compatible". To check a build, extract
its `lib/arm64-v8a/*.so` files and confirm each `LOAD` segment's alignment is
`0x4000` with the NDK's `llvm-readelf -lW`.

## Remaining public-launch blockers

This deployment protects the operator surface. Every player-owned, competitive,
social, cloud-save, commerce, and room-relay route now requires the server
session as well as the claimed profile ID; the relay additionally requires an
active room seat. Challenge uploads are acknowledged per submission and are
idempotent, so mobile retry cannot double-record a score.

Email and Google sign-in are implemented but need live SMTP and OAuth
credentials and a real-device lost-device check before a public paid launch;
until then a player who never signs in stays a device-bound guest and cannot
recover paid currency after losing the device. The game also
does not yet connect its battle simulation to the authenticated WebSocket
relay; keep internet rooms in closed testing until that client transport and
real-device latency/reconnect testing are complete.

Two API replicas improve capacity and remove a single application process as a
failure point, while the managed database removes the local database volume as
a failure domain. The API host and its local Redis volume remain single points
of failure. A multi-host public launch needs managed Redis or an equivalent
shared service, off-host observability, and a load balancer in front of more
than one host.

For the full mobile, store, assets, payments, and account checklist, see
[PRODUCTION_LAUNCH.md](PRODUCTION_LAUNCH.md).
