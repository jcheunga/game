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
   - `domain`: the bare hostname, for example `crownroad.game`. The game's
     rating prompt already opens `https://crownroad.game` on desktop
     (`scripts/core/AppRatingPrompt.cs`), so change that too if you use a
     different domain.
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
| Sessions expire after 30 days; only a hash of the token is stored | `server/SessionAuth.cs` |
| Store receipts kept only as a one-way fingerprint | `server/Endpoints.cs`, `server/StorePurchaseVerification.cs` |
| Hashed IPs for rate limiting expire after about a minute | `server/RateLimitStore.cs` |
| API access logs rotate (5 × 20 MB) | `caddy` logging options in `server/docker-compose.production.yml` |
| No website access logs | No `log` directive in the website block of `server/Caddyfile` |
| Analytics off until the player opts in | `scripts/core/AnalyticsService.cs`, first-run prompt in `scripts/ui/MainMenu.cs` |
| Crash reports off until a separate opt-in; withdrawing analytics discards queued events | `scripts/core/CrashReporter.cs`, `AnalyticsService.cs`, and the Account tab in `scripts/ui/SettingsMenu.cs` |
| Cloud saves exclude session credentials, server settings and consent choices; restores preserve the current device's identity and choices | Shared `scripts/core/CloudSavePrivacy.cs`, `CloudSaveService.cs`, `GameState.cs`, `server/Endpoints.cs`; schema migration 5 in `server/Database.cs` scrubs existing rows |
| No third-party analytics or ad SDKs | `Game.csproj`, `addons/` |

Adding an SDK, a new data type, a new processor, or a website analytics tool
means updating the policy and its effective date before release.

The 2026-09-30 privacy changes require a new analytics choice from players who
accepted the old inaccurate "anonymous / no personal information" notice.
Crash reporting remains separately disabled until enabled in Settings. The
local checks are documented in [PRODUCTION_LAUNCH.md](PRODUCTION_LAUNCH.md).
Before deploying this policy, complete the migration/backup steps in
[DEPLOYMENT.md](DEPLOYMENT.md); old database backups may still contain raw
session credentials. Publisher details, legal review and actual store privacy
declarations remain release gates.

## Handling deletion requests

There is no self-service deletion yet. When a request arrives at the support
inbox:

1. Confirm the requester controls the profile. For example, ask for their
   callsign and a recent result shown on their device, and compare it with the
   server data for the player ID they gave.
2. Delete that `profile_id`'s rows from the player tables in PostgreSQL
   (`players`, `auth_sessions`, `cloud_saves`, `challenge_results`,
   `achievements`, `daily_completions`, `analytics_events`, `crash_reports`,
   `room_seats`, `room_telemetry`, `room_reports`, `arena_*`,
   `guild_members`, `friendships`, `friend_gifts`, `raid_contributions`).
   Keep `purchases`, `player_wallets` and `wallet_ledger` rows only as long as
   tax and accounting law requires.
3. Reply within 30 days to confirm, as the support page promises.

Building an operator endpoint or script for this before launch is strongly
recommended. Google Play also expects deletion to be requestable from inside
the app.

## Fonts

The pages load Alegreya, Alegreya Sans SC and Grenze Gotisch from Google Fonts,
which the privacy policy discloses. To remove that third-party request,
download the WOFF2 files into `site/src/assets/fonts/`, replace the Google
Fonts `<link>` in `site/src/_partials/head.html` with `@font-face` rules in
`site.css`, and remove `fonts.googleapis.com`/`fonts.gstatic.com` from the
website Content-Security-Policy in `server/Caddyfile`. Then update the "This
website" paragraph of the privacy policy.
