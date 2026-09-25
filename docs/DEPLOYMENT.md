# Crownroad backend deployment

This is a production baseline for one region: Docker Compose runs PostgreSQL
and the API on a private network, while Caddy serves the public HTTPS endpoint.
The PostgreSQL data directory lives in a persistent Docker volume. It is
appropriate for internal testing and a small, single-server launch; it is not a
high-availability setup. Move the same API to a managed PostgreSQL provider
before a large public launch.

## What this deploys

- HTTPS with managed certificates and HTTP-to-HTTPS redirect.
- A private API container: port 8080 is not exposed on the host.
- Private PostgreSQL database, persistent storage, and health checks.
- Docker-mounted provider credentials rather than credentials in images or
  application environment variables.
- An independent operator key for `/admin`, `/stats`, `/analytics/summary`,
  and `/admin/balance`.

The API never creates database dumps in its own container. Use PostgreSQL
provider backups, point-in-time recovery, or encrypted off-host dumps and
rehearse a restore before releasing paid currency.

## Before the first deploy

1. Create a Linux VM and install Docker Engine plus the Docker Compose v2
   plugin. Use a non-root deployment account.
2. Point an A (and, if used, AAAA) record such as `api.your-domain.example` at
   the VM. Make ports 80 and 443 available from the public internet. Caddy uses
   that reachability to provision and renew certificates.
3. Copy the release repository to the server and work from its `server/`
   directory.
4. Copy `.env.production.example` to `.env.production`, then set the API
   hostname, a monitored email address, and the exact browser origins. Do not
   use `*` as an origin.
5. Create `secrets/` (the repository path is `server/secrets/`) with owner-only permissions and add four files:
   `postgres-password`, `admin-api-key`, `google-play-service-account.json`,
   and the App Store private-key `.p8` file. Point the four `*_SOURCE` entries in
   `.env.production` at those paths. Generate a unique, high-entropy admin key.
   Never commit this directory or the populated environment file.
6. Set `POSTGRES_DB` and `POSTGRES_USER` to new, dedicated values; the password
   exists only in `postgres-password`. Fill the Google Play and App Store
   identifiers. Do not set
   `CROWNROAD_ALLOW_TEST_PURCHASE_CLAIMS`; the server rejects it in production.
   Leave Stripe keys empty unless a separately compliant desktop/web checkout
   is being launched.

If iOS is not ready, do not fabricate an Apple key to make the deployment
start. Finish the StoreKit bridge and configure its real App Store key before
you offer iOS purchases.

## Release command

From the server directory on the deployment VM:

```sh
./deploy.sh
```

The command checks the production Compose configuration, runs the server and
game-data checks, builds the image, then starts the API and Caddy. It does not
create DNS records, buy a domain, upload store credentials, or configure your
backup provider.

After the hostname resolves, verify the public health endpoint:

```sh
curl --fail --show-error https://api.your-domain.example/health
```

Opening `https://api.your-domain.example/admin` displays a browser credential
prompt. Use any username and the `admin-api-key` file’s value as the password.
For an API client, send that value in an `X-Admin-Api-Key` request header. Do
not use the administrator credential in the game client.

## Operations after launch

- Monitor HTTPS certificate renewal, `/health`, container restarts, API error
  rate, payment-verification failures, and backup completion. Attach alerts to
  an on-call destination before enabling paid purchases.
- Enable encrypted PostgreSQL backups and point-in-time recovery with your
  hosting provider, or create encrypted off-host PostgreSQL dumps at least
  daily; retain the wallet ledger for reconciliation.
- Before each release, run the verification suite, create an off-host backup,
  deploy, verify `/health`, and test a sandbox purchase. Roll back by restoring
  the prior application version only after preserving the current database.
- Rotate the admin key and store credentials through the secret files, then
  rerun `./deploy.sh`. Treat a leaked key as an incident and rotate it promptly.

## Migrating an existing SQLite deployment

Do not mount an existing `crownroad.db` file into the PostgreSQL stack. Take a
read-only backup, schedule maintenance, migrate into a separate PostgreSQL
database, validate player, purchase, wallet, and ledger counts, then cut the
API over to PostgreSQL. Keep the SQLite snapshot immutable until the recovery
window closes. A new deployment has no data migration step.

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

## Remaining public-launch blockers

This deployment protects the operator surface. Every player-owned, competitive,
social, cloud-save, commerce, and room-relay route now requires the server
session as well as the claimed profile ID; the relay additionally requires an
active room seat. Challenge uploads are acknowledged per submission and are
idempotent, so mobile retry cannot double-record a score.

The current player identity is still anonymous and bound to a device session,
so it cannot restore paid currency after a lost device. Google/Apple account
sign-in and recovery are required before a public paid launch. The game also
does not yet connect its battle simulation to the authenticated WebSocket
relay; keep internet rooms in closed testing until that client transport and
real-device latency/reconnect testing are complete.

For the full mobile, store, assets, payments, and account checklist, see
[PRODUCTION_LAUNCH.md](PRODUCTION_LAUNCH.md).
