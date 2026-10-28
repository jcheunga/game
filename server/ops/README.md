# Crownroad operations scripts

Three tools for the backend launch gates in
[PRODUCTION_LAUNCH.md](../../docs/PRODUCTION_LAUNCH.md), section 3
"Production backend and resilience". Each produces evidence to keep with the
launch records.

| Script | What it does | Launch gate |
| --- | --- | --- |
| `restore_drill.sh` | Dumps the database read-only (or takes a dump), restores it into a separate scratch database, times each phase and compares the key tables | "Configure provider backups/PITR or encrypted off-host dumps and perform a timed restore drill into a separate database" |
| `watch.sh` | Health checks every minute from the VM, alerts to Slack, Discord, ntfy or any JSON webhook, without repeating itself | "Monitor and alert on public `/health`, TLS renewal, API 5xx/latency, container restarts, … backup failures. Test alerts reaching the on-call owner." |
| `loadtest/loadtest.py` | Ramped load test with the game's own request mix, p50/p95/p99 and errors per route, pass/fail thresholds | "Load-test the deployed API with a realistic mix … then set a measured concurrent-player target and alert thresholds" |

The shell scripts run on bash 3.2 (macOS) and later. They were tested on macOS
against a local PostgreSQL 18 cluster and a local API; the production VM's
Linux tools (GNU `date`, `stat`, `timeout`) are used when present.

---

## 1. Restore drill: `restore_drill.sh`

### What it does

1. **Preflight.** Connects to both databases and **refuses** (exit 2) when the
   target is the source: same host, port and name in the URLs, or the same
   database on the same server however it was reached (it compares
   `current_database()` and the cluster's system identifier or start time, so
   a pooler host name or `localhost` vs `127.0.0.1` does not fool it). It also
   refuses a target that already has tables, unless `--clean-target`, and a
   `pg_dump` older than the source server.
2. **Dump** with `pg_dump --format=custom --no-owner --no-acl`. The script
   opens one `REPEATABLE READ, READ ONLY` transaction on the source, exports
   its snapshot, counts the key tables in it and has `pg_dump` read the same
   snapshot, so the counts match the dump exactly while players keep writing.
   Nothing is ever written to the source: every source statement runs in a
   read-only transaction, and `pg_dump` is read-only. A read-only role works
   (it was tested with a role that has only `pg_read_all_data`).
3. **Restore** with `pg_restore --no-owner --no-acl --exit-on-error --jobs N`.
4. **Analyze** the restored tables (until then the API would run slowly; this
   is part of the recovery time).
5. **Verify**: row counts of `players`, `purchases`, `player_wallets`,
   `wallet_ledger`, `wallet_adjustments`, `store_events`, `cloud_saves`,
   `account_identities` and `auth_sessions`, plus `sum(amount)` of the ledger,
   the summed wallet balances and the schema version, source against target.
6. **Report**: phase timings, dump size and SHA-256, the comparison and
   PASS/FAIL, printed and saved as `restore-drill-<UTC time>.txt`.

Database passwords never appear on a command line: the URLs are read from the
environment, and passwords go into a private temporary passfile. Query options
the API uses but libpq does not know (`pooling`, `max_pool_size`, …) are
dropped, so `DATABASE_URL` can be pasted as is.

### Prerequisites

- PostgreSQL client tools (`psql`, `pg_dump`, `pg_restore`) **of at least the
  source server's major version**. On the Ubuntu/Debian VM install
  `postgresql-client-<major>` from the PostgreSQL apt repository, or run the
  drill in the official image (no install on the host):

  ```sh
  docker run --rm -it --network host -v "$PWD:/work" -w /work \
    -e DRILL_SOURCE_URL -e DRILL_TARGET_URL postgres:18 \
    bash server/ops/restore_drill.sh --keep-dump /work/drill-dumps
  ```

  Or set `PG_BIN` to the directory holding the client binaries.
- An **empty scratch database** to restore into, ideally on a separate
  cluster (a restore into the production cluster competes with players for
  its CPU and I/O; the script warns). Creating one is provider-specific; it
  only needs a role that can create tables in it.
- The source URL: prefer the provider's **direct (non-pooled)** URL and a
  **read-only role**. `pg_dump` needs a session; transaction-pooling poolers
  such as PgBouncer break it.
- Disk space for the dump (custom format is compressed; expect a fraction of
  the database size).

### Commands

```sh
cd server/ops
# Keep URLs out of shell history: read them, then export.
read -rs DRILL_SOURCE_URL && export DRILL_SOURCE_URL   # postgresql://reader:...@db-direct:5432/crownroad?sslmode=verify-full&sslrootcert=system
read -rs DRILL_TARGET_URL && export DRILL_TARGET_URL   # postgresql://drill:...@scratch-db:5432/crownroad_drill?sslmode=verify-full&sslrootcert=system

./restore_drill.sh                                    # dump, restore, verify; dump deleted afterwards
./restore_drill.sh --keep-dump ./drill-dumps --jobs 4  # keep the dump + .counts (personal data!)

# Restore a dump made earlier by --keep-dump (expected counts come from its .counts file):
./restore_drill.sh --dump ./drill-dumps/crownroad-20261028T105152Z.dump --clean-target
```

Options: `--report FILE`, `--clean-target` (drop the dump's objects in a
non-empty target first), `--no-snapshot` (only for a pooler that cannot
export snapshots; live writes may then show as differences),
`--expected-counts FILE`. Exit codes: 0 PASS, 1 failed or counts differ (or
nothing to compare), 2 refused.

Afterwards **drop the scratch database**: it is a full copy of production.
Encrypt or delete a kept dump; both hold players' personal data.

### Provider point-in-time restore (document, not scripted)

Point-in-time recovery (PITR) is provider-specific, so it is not scripted.
Rehearse it once before launch and after any provider change:

1. In the provider's console or CLI, restore to a **new** cluster at a chosen
   time (for example 10 minutes ago). Never restore over the live database.
   Examples: DigitalOcean "Restore from backup" (new cluster, point in time),
   AWS RDS `restore-db-instance-to-point-in-time`, Google Cloud SQL
   point-in-time clone, Neon "branch from a point in time". Check the
   provider's current documentation and retention window.
2. Record when you started, when the new cluster accepted connections (the
   recovery time), and the newest data it holds (the recovery point):

   ```sql
   SELECT to_timestamp(max(created_at)) AS newest_ledger_entry FROM wallet_ledger;
   SELECT to_timestamp(max(purchased_at)) AS newest_purchase FROM purchases;
   SELECT to_timestamp(max(uploaded_at)) AS newest_cloud_save FROM cloud_saves;
   SELECT max(version) AS schema_version FROM schema_version;
   ```

3. Point a staging API at the clone (`DATABASE_URL`), check `/health`, sign in
   with a test account and read its wallet.
4. Delete the clone.

### Evidence to record

- The report file of a PASS run against production (or a staging copy of it),
  with the date and who ran it.
- The "recovery (restore to verified)" time: the restore-from-dump recovery
  time. With nightly dumps the recovery point is up to a day; with PITR it is
  the provider's (minutes).
- For PITR: the four times and query results above.
- Where backups live, their retention, and that they are encrypted off-host.

---

## 2. Health alerts: `watch.sh`

### Checks

| Check | How | Alerts |
| --- | --- | --- |
| `health` | `GET $API_URL/health` must answer 200 with `"status":"healthy"` within `HEALTH_TIMEOUT` (5 s); retried once after 5 s | CRIT no answer or not healthy (the API answers 503 when PostgreSQL or Redis is down); WARN slower than `HEALTH_SLOW_MS` (2000) |
| `tls` | `openssl s_client` on the API host, certificate `notAfter` | WARN < 21 days, CRIT < 7 days (Caddy renews ~30 days ahead, so either means renewal is failing). Invalid certificates already fail `health`. |
| `containers` | `docker ps`/`docker inspect` on Compose project `server` (label `com.docker.compose.project`) | CRIT fewer than 1 redis / `API_REPLICAS` (2) crownroad-server / 1 caddy running and healthy, or Docker unreachable; WARN starting/unhealthy extras; WARN once when a restart count rose since the last run |
| `disk` | `df -P` on `/` and Docker's data directory | WARN ≥ 80 %, CRIT ≥ 90 % |
| `caddy`, `latency`, `purchases` | `docker logs --since 5m` of the caddy container. The Caddyfile logs API requests as JSON to stdout (`log { output stdout format json }`), which Docker keeps in its rotated json-file log; the parser reads `ts`, `status`, `duration` and `request.uri` | 5xx share: WARN ≥ 1 %, CRIT ≥ 5 % (from 20 requests). p95 latency (WebSocket relay excluded): WARN > 1000 ms. Purchases: CRIT on any `503` from `/purchase/validate` (store verification broken, paid purchases not granted); WARN on ≥ 10 refused (400) |
| `backup` | Newest file matching `BACKUP_GLOB` in `BACKUP_DIR`, or `BACKUP_CHECK_CMD` printing the newest backup's time | CRIT older than 26 h, empty, or missing. Skipped when neither is set. |
| `postgres` (optional) | `PG_MONITOR_URL`, read-only: client connections vs `max_connections`, database size | WARN ≥ 80 % of connections, or over `PG_MAX_DB_GB` |

Redis is covered twice: `/health` answers 503 "redis unavailable", and the
Compose health check marks the redis container unhealthy.

### Alerts and memory

Each run sends **one** POST with everything that changed: a new problem, a
change of level, a reminder every `REALERT_MINUTES` (240) while a problem
lasts, and a single RESOLVED when it clears. Restart events alert once and
clear silently. The state lives in `STATE_DIR`
(default `~/.local/state/crownroad-watch`); a problem counts as alerted only
after the webhook answered 2xx, so a failed delivery is retried next run.

Payload shapes (`ALERT_FORMAT=auto` picks one from the URL):

| Format | Picked for | Body |
| --- | --- | --- |
| `slack` | `https://hooks.slack.com/…` | `{"text": "*[CRIT] crownroad on vm-1: 1 problem(s)*\n• CRIT health: …"}` |
| `discord` | `https://discord.com/api/webhooks/…` | `{"content": "**[CRIT] …**\n• CRIT health: …"}` (cut to 1900 characters) |
| `ntfy` | `https://ntfy.sh/<topic>`, `…/ntfy/…` | Plain-text body with headers `Title`, `Priority` (5 critical, 4 warning, 3 resolved/test) and `Tags`; `ALERT_WEBHOOK_TOKEN` is sent as a bearer token for protected servers |
| `generic` | anything else | `{"text", "title", "level", "host", "api", "time", "alerts": [{"check", "level", "message"}]}`; `text` also suits Mattermost, Rocket.Chat and Google Chat |

`--test` sends `[TEST] crownroad alerts from <host>` and prints the HTTP
status; it exits 1 if the receiver did not accept it.

### Install on the VM

```sh
sudo install -m 600 -o deploy server/ops/watch.env.example /etc/crownroad-watch.env
sudoedit /etc/crownroad-watch.env           # ALERT_WEBHOOK_URL, BACKUP_* , HOST_LABEL
server/ops/watch.sh --config /etc/crownroad-watch.env --dry-run   # see what it would send
server/ops/watch.sh --config /etc/crownroad-watch.env --test      # evidence: on-call receives it
```

systemd (edit `User` and the path in the service first):

```sh
sudo cp server/ops/systemd/crownroad-watch.{service,timer} /etc/systemd/system/
sudo systemctl daemon-reload && sudo systemctl enable --now crownroad-watch.timer
systemctl list-timers crownroad-watch.timer && journalctl -u crownroad-watch -n 20
```

or cron for the deploy user (`crontab -e`):

```cron
* * * * * /home/deploy/crownroad/server/ops/watch.sh --config /etc/crownroad-watch.env 2>&1 | logger -t crownroad-watch
```

The user needs the `docker` group (container state and Caddy's log), `curl`,
`openssl` and, for `postgres`, `psql`. Exit codes: 0 OK, 1 warning,
2 critical, 3 configuration error (the service treats 1 and 2 as success).

**A script on the VM cannot report the VM itself being down.** Set
`HEARTBEAT_URL` to a dead man's switch (for example a healthchecks.io check
that pages the same on-call destination when pings stop), or add an external
uptime check on `https://<api>/health`.

### Not covered

Wallet anomalies need database queries and a definition of "anomaly" (for
example refunds per hour, or `goldUnrecovered` from `/admin/refunds`); add
them as a `BACKUP_CHECK_CMD`-style command or a provider alert. PostgreSQL
storage and connection alerts are better set in the provider's own
monitoring; `PG_MONITOR_URL` is a fallback.

### Evidence to record

- `watch.sh --test` output and the message as received by the on-call person
  (a screenshot or a link to it), with the time.
- `systemctl list-timers crownroad-watch.timer` showing it runs.
- A fire drill: stop one API replica (`docker stop server-crownroad-server-2`;
  Caddy keeps serving from the other), wait for the CRIT alert, start it again
  and wait for RESOLVED. Record both messages.
- The heartbeat check's configuration.

---

## 3. Load test: `loadtest/loadtest.py`

k6 was not installed, so this is Python 3 with the standard library only (the
VM already needs Python 3 for `deploy.sh`). Each virtual player (VU) is a
thread with its own keep-alive connection.

### The player mix

The calls, payloads and timers come from the game's own code
(`scripts/core/*Http*`, `CloudSaveService`, `AnalyticsService`,
`NetworkStatus`, `WalletAdjustmentSync`, `AccountSignIn`, `MultiplayerMenu`):

| When | Calls |
| --- | --- |
| App start | `GET /health`; new guest `POST /player-profile`, then a profile sync `POST /player-profile` with the session (the server rotates the token); `GET /cloud-save/info` (session check); `GET /wallet/adjustments`, `GET /wallet`; `GET /cloud-save/download`; `GET /challenge-feed`. 70 % of later sessions are returning players who reuse an earlier guest. |
| Every 45 s | `GET /health` (NetworkStatus) |
| Each battle (60–180 s) | after a win (75 %): `POST /cloud-save/upload` with a 20/50/100 KB save; consenting players (35 %): `POST /analytics/ingest` per 10 events |
| Per battle, by chance | challenge (25 %): `POST /challenge-sync` + `GET /challenge-boards`; daily (10 %): `POST /daily/complete` + `GET /daily/leaderboard/{date}`; achievements (20 %); shop (15 %): refunds + wallet; restore (3 %): download |
| Rooms | 15 % per battle open the multiplayer menu and poll `GET /challenge-rooms` every 10 s; 3 % host a room: create, poll `/challenge-room-session` + `/challenge-room-scoreboard` every 10 s, leave |
| Purchases | 1 % per battle: `POST /purchase/validate` with platform `loadtest`, which the server rejects as an unsupported store **before** contacting Google or Apple (expected 400). Successful purchases are not simulated: production refuses test claims. |

At `--time-scale 1` (real players) this is about 3 requests per player-minute,
nearly half of them `/health`; the report prints the measured rate, so
`players ≈ target req/s ÷ (rate ÷ 60)`. `--time-scale 0.1` makes players ten
times busier (shorter battles and menus; the 45 s and 10 s client timers stay
real).

Not simulated: the WebSocket relay (the game does not use it yet), store
purchases that succeed, and email/Google/Apple sign-in (they need the real
providers).

### Commands

```sh
cd server/ops/loadtest
python3 loadtest.py                                   # http://localhost:8080, default ramp (3 min)
python3 loadtest.py --stages 10s:6,15s:6,5s:0 --time-scale 0.1          # 30 s smoke test
python3 loadtest.py --target https://staging-api.example.com \
  --stages 2m:50,5m:200,5m:200,1m:0 --json-out staging-$(date +%F).json  # explicit target required
```

- `--stages` is a list of `duration:VUs` ramps (linear inside each stage).
- It only ever loads `http://localhost:8080` unless `--target` is given, and
  pauses 5 s before loading a non-local target (`--yes` skips the pause).
- At the end it deletes every guest it created through `POST /account/delete`
  (the game's own path), so no load-test players remain; `--keep-accounts`
  keeps them. Profile IDs start with `CVY-LT`.
- Run it against **staging**, or production only in an agreed window: it
  writes real rows (saves, scores, analytics) until the cleanup.
- On a server with `CROWNROAD_ALLOW_TEST_PURCHASE_CLAIMS=1` (local tests
  only) the "rejected" purchase is credited, and the report flags it as an
  error; use `--purchase-fail-rate 0` there.

### Report and thresholds

Per stage: VUs, requests, req/s, 429s, errors, p50/p95/p99. Per route:
requests, OK, 429, errors, error %, p50/p95/p99 and a verdict. An error is a
5xx, a timeout, a connection error or any unexpected status or body; 429s are
counted separately and left out of latency.

Defaults (all flags): per route p95 ≤ 500 ms and p99 ≤ 1500 ms (cloud-save
upload/download 800/2000 ms), route errors ≤ 2 %, overall errors ≤ 1 %, 429s
≤ 1 % overall. Routes with fewer than 20 requests are judged on errors only.
Exit code 0 PASS, 1 FAIL. It also prints the load generator's CPU: above 70 %
the client, not the server, may be adding latency.

### Rate limits and a single machine

`server/RateLimiter.cs` limits every request per one-minute window (Redis
counters shared by the replicas in production; an in-memory sliding window
locally, and per replica during a Redis outage):

| Requests | Counted per | Limit / min |
| --- | --- | --- |
| `/auth/*` (sign-in attempts) | client IP | 10 (40 for `/auth/google/poll`, `/auth/apple/poll`) |
| `/player-profile` (guest registration **and** profile sync) | client IP | 20 |
| `/store/apple/notifications` | client IP | 600 |
| any request with `Authorization: Bearer …` | that token | 180 (30 for `/ws/…`) |
| everything else without a token: `/health`, `/challenge-feed`, `/challenge-boards`, `/challenge-rooms`, `/daily/leaderboard/…`, `/challenge-room-scoreboard` | client IP | 60 |

The client IP: production Compose sets `TrustForwardedHeaders=true`, so the
API takes the address from `X-Forwarded-For`, which Caddy sets to the
connecting address (the Caddyfile has no `trusted_proxies`, so Caddy replaces
whatever the client sent). The API trusts any forwarder, which is safe only
because port 8080 is not published. So every VU on one machine is **one
client IP** to the server, and:

- New players are capped at about 10 per minute in total (a registration and a
  profile sync each), however many VUs you start. VUs that are refused wait
  45 s and retry, like the game.
- The current game sends no session on public reads, so all VUs share 60 per
  minute for `/health`, feed, boards, rooms and daily leaderboards. `/health`
  every 45 s alone exhausts that at about 45 players.
- Everything authenticated (saves, wallet, sync, analytics, rooms) is per
  session and scales normally.

A single-machine run therefore measures the limiter, not capacity. The report
fails when more than 1 % of requests got 429 and says why. To measure the
deployed API properly:

1. **Several source IPs.** Run the script on several machines at once (small
   cloud VMs, each keeping its ramp under ~10 new players per minute and ~30
   players in total, which keeps its public reads under 60 per minute), give
   each a `--json-out` file, and add the results up.
   Or give one machine several addresses (secondary IPv4s, or addresses from
   an IPv6 /64 when the API has an AAAA record) and repeat `--source-ip ADDR`;
   VUs are spread across them. Spoofing `X-Forwarded-For` does not work
   through Caddy and would test something else.
2. **A staging switch** (suggested, not built): a staging-only setting that
   raises or disables the per-IP limits, which the server refuses to start
   with in Production, like `CROWNROAD_DEV_SIGN_IN`. Measure capacity on
   staging with it on, then check the limiter separately. No bypass was added
   to the server.
3. `--bearer-on-reads` sends the player's session on public reads, so they
   use the per-session bucket. That is **not** what the game does today; it
   models a client change, and is useful to separate read capacity from the
   per-IP limit.

### Setting the target

Ramp in stages at `--time-scale 1` (for example 50, 100, 200, 400 VUs, five
minutes each) from enough source IPs. The concurrent-player target is the
largest stage that passes every threshold with headroom; use its measured p95
and 5xx rate to set `LATENCY_P95_WARN_MS` and `FIVEXX_*` in `watch.sh`. Watch
PostgreSQL connections during the run: two replicas use up to 80 pooled
connections (`max_pool_size=40`).

### Evidence to record

- The console report and `--json-out` file of each staged run, the target, the
  date, the API image digest, and the number of source machines/IPs.
- The chosen concurrent-player target and the alert thresholds derived from it.
- What was not covered (WebSockets, successful purchases, provider sign-in).
