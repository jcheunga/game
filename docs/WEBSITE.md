# Public website

The marketing site, privacy policy, terms and support page live in `site/` and
are served by the same Caddy instance as the API. There is no JavaScript, no
cookies and no analytics; the only third-party request is Google Fonts.

| Path | What it is |
| --- | --- |
| `site/site.json` | Launch values: domain, publisher name, support email, governing law, effective date, store links |
| `site/src/` | Page templates, images, icons, `robots.txt`, `sitemap.xml`, `security.txt` |
| `site/src/_partials/` | Shared head, navigation and footer |
| `scripts/tools/build_site.py` | Renders `site/src` into `site/dist` and checks links, anchors, images, sitemap and page heads |
| `site/dist/` | Build output (not committed). Caddy serves it from `/srv/site` |

Pages: `/`, `/support`, `/privacy`, `/terms`, plus a `404.html` for unknown paths.

## Preview locally

```sh
python3 scripts/tools/build_site.py --serve
```

This builds with visible placeholders for any unset value and serves
http://localhost:8000 with the same clean URLs Caddy uses (`/privacy` serves
`privacy.html`). `./scripts/verify_all.sh` and CI run the same build without
`--serve`, so a broken link or missing image fails verification.

## Go-live checklist

1. **Fill in `site/site.json`.** The deploy refuses to build the site until
   every field below is set:
   - `domain`: the bare hostname, for example `crownroad.game`.
   - `legal_name`: the person or company that publishes the game. It appears in
     the footer and as the data controller in the privacy policy.
   - `support_email`: a monitored inbox. It receives player support, data
     deletion requests and security reports, and is published in
     `/.well-known/security.txt`.
   - `governing_law`: for example `New South Wales, Australia`.
   - `effective_date`: `YYYY-MM-DD`, the day the privacy policy and terms apply.
2. **Have the privacy policy and terms reviewed.** They were written from what
   the code does today (see "Keeping the privacy policy true" below), not by a
   lawyer. Check them against the markets you launch in and your age rating.
3. **DNS.** Point A (and AAAA, if used) records for both the domain and
   `www.` at the deployment host, alongside the existing API record. Caddy
   issues certificates for all three names, and `www` redirects to the bare
   domain.
4. **Server environment.** Set `CROWNROAD_SITE_DOMAIN` in
   `server/.env.production` to the same domain as `site.json`. `deploy.sh`
   checks that they match.
5. **Deploy.** Run `./deploy.sh` from `server/` as described in
   [DEPLOYMENT.md](DEPLOYMENT.md). It builds the site with
   `--strict --domain "$CROWNROAD_SITE_DOMAIN"` before it validates Caddy and
   starts the stack. Later site-only changes don't need a container restart:
   rerun the build and Caddy serves the new files immediately.
6. **Check it's live.**

   ```sh
   curl -sI https://crownroad.game/ | grep -iE '^(HTTP|content-security-policy|strict-transport)'
   curl -s  https://crownroad.game/.well-known/security.txt
   curl -sI https://www.crownroad.game/ | grep -i '^location'
   curl -s -o /dev/null -w '%{http_code}\n' https://crownroad.game/does-not-exist   # 404
   ```

   Then paste the home page URL into a link-preview checker, such as the
   debugger of a social network you post on, to confirm the share card
   (`/assets/img/og.jpg`) appears.
7. **Store listings.** Use these URLs in App Store Connect and Play Console:

   | Field | URL |
   | --- | --- |
   | Marketing / website URL | `https://<domain>/` |
   | Support URL | `https://<domain>/support` |
   | Privacy policy URL | `https://<domain>/privacy` |
   | Play Console data-deletion URL | `https://<domain>/support#delete-data` |

8. **Search.** Add the domain to Google Search Console and submit
   `https://<domain>/sitemap.xml`.
9. **When a store goes live,** add its listing URL under `links` in
   `site.json` (`app_store`, `google_play`, `web_play`) and redeploy. The
   closing section switches from "Coming to" to "Available on", links each
   live platform, and marks the rest "soon".

## Keeping the privacy policy true

`/privacy` describes current behaviour, and some of it depends on server
configuration. Update the page in the same change if any of these move:

| Statement | Where it is enforced |
| --- | --- |
| Analytics deleted after 30 days, live race updates after 24 hours, player reports and crash reports after 90 days | `server/StaleDataCleanup.cs` |
| Sessions stop working after 30 days; only a hash of the token is stored; expired and revoked session rows are kept until a deletion request | `server/SessionAuth.cs`, `server/StaleDataCleanup.cs` (no session cleanup) |
| Sign-in keeps the email address (email sign-in) or the provider's account ID only; Google's email is checked and dropped; Apple is asked for no name or email; unfinished sign-ins expire after 10 minutes; Game Center signature hashes after 20 | `server/AccountAuth.cs` |
| Play Games signs in automatically on Android unless progress is at risk; signing out stops it | `TryAutoSignIn` in `scripts/core/PlatformSignIn.cs` |
| Guests register their player ID and nickname whenever the server is reachable | `OnServerReachable` in `scripts/core/AccountSignIn.cs` |
| Challenge and daily leaderboards show player IDs; room scoreboards show player IDs and nicknames | `ChallengeLeaderboard`, `DailyLeaderboard`, `RoomScoreboard`, `RoomDirectory` in `server/Endpoints.cs` |
| Google and Apple purchase tokens kept only as a one-way fingerprint (Stripe's payment ID is stored as is); purchases limited to 10 an hour | `server/Endpoints.cs`, `server/StorePurchaseVerification.cs` |
| Hashed IPs for rate limiting expire after about a minute | `server/RateLimitStore.cs` |
| API access logs and the server's request logs (with player IDs) rotate (5 × 20 MB per container) | `caddy` and `crownroad-server` logging options in `server/docker-compose.production.yml` |
| No website access logs | No `log` directive in the website block of `server/Caddyfile` |
| Analytics off until the player opts in | `scripts/core/AnalyticsService.cs`, and the Privacy section of the Account tab in `scripts/ui/SettingsMenu.cs` (there is no first-run prompt) |
| Crash reports off until a separate opt-in; withdrawing analytics discards queued events | `scripts/core/CrashReporter.cs`, `AnalyticsService.cs`, and the Account tab in `scripts/ui/SettingsMenu.cs` |
| Cloud saves exclude session credentials, server settings and consent choices; restores preserve the current device's identity and choices | Shared `scripts/core/CloudSavePrivacy.cs`, `CloudSaveService.cs`, `GameState.cs`, `server/Endpoints.cs`; schema migration 5 in `server/Database.cs` scrubs existing rows |
| No third-party analytics or ad SDKs | `Game.csproj`, `addons/`, `android/plugins/`, `ios/plugins/` |
| The player ID is shown under Records & replay on the multiplayer screen | `scripts/ui/MultiplayerMenu.cs` |
| When a player asks Apple for a refund, Apple is told only that the purchase was delivered, that the player agreed and that there was no free sample; never for a deleted account; nothing until `APPLE_IAP_SEND_CONSUMPTION_INFO=1`. `/terms` section 3 says the same | `server/AppStoreConsumption.cs` |

Adding an SDK, a new data type, a new processor, or a website analytics tool
means updating the policy and its effective date before release.

The 2026-09-30 privacy changes require a new analytics choice from players who
accepted the old inaccurate "anonymous / no personal information" notice.
Crash reporting remains separately disabled until enabled in Settings. The
local checks are documented in [PRODUCTION_LAUNCH.md](PRODUCTION_LAUNCH.md).
Before deploying this policy, complete the migration/backup steps in
[DEPLOYMENT.md](DEPLOYMENT.md); old database backups may still contain raw
session credentials. Publisher details, legal review and actual store privacy
declarations remain release gates. Draft store declarations, built from the
same audit as the policy, are in
[STORE_PRIVACY_ANSWERS.md](STORE_PRIVACY_ANSWERS.md); update both together.

## Challenge links

The game's **Copy share link** gives `https://<domain>/challenge/CH-04-PRS-4821`.
Caddy serves those through `site/src/challenge.html` as a template
(`server/Caddyfile`, delimiters `[[ ]]`, so they don't clash with the site
builder's `{{ }}`): the page shows the code and how to load it in the game,
or says the link is incomplete when the address doesn't match the code
format, so nothing else from the address reaches the page. The site allows
no scripts, which is why the server fills it in. The preview server doesn't
run templates, so locally the page shows the raw `[[ ]]` markers.

Opening the game straight from a link would also need a native plugin on
each platform to pass the link into the game, plus the
`apple-app-site-association` and `assetlinks.json` files; none of that is
built yet.

## Handling deletion requests

Players delete their account in the game: Settings → Account → Delete
account. When a request arrives by email instead, confirm the requester
controls the account, then delete it with `POST /admin/support/delete-account`,
which runs the same code as the in-app button. The steps are under "Account
deletion request by email" in [store/SUPPORT_PLAYBOOK.md](store/SUPPORT_PLAYBOOK.md).

## Fonts

The pages load Alegreya, Alegreya Sans SC and Grenze Gotisch from Google Fonts,
which the privacy policy discloses. To remove that third-party request,
download the WOFF2 files into `site/src/assets/fonts/`, replace the Google
Fonts `<link>` in `site/src/_partials/head.html` with `@font-face` rules in
`site.css`, and remove `fonts.googleapis.com`/`fonts.gstatic.com` from the
website Content-Security-Policy in `server/Caddyfile`. Then update the "This
website" paragraph of the privacy policy.
