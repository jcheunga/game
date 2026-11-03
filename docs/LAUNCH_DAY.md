# Launch day

The order of work from a passing preflight to a full rollout, and when and how
to roll back. Everything before this (accounts, server, sign-in, purchases,
phones, store review) is in [LAUNCH_CHECKLIST.md](LAUNCH_CHECKLIST.md).

## 1. Freeze the release

1. `python3 scripts/release/preflight.py` reports 20 of 20.
2. The end-to-end test passes against the production stack, with Docker
   running: `scripts/tests/server_e2e.sh --stack`.
3. Export the signed builds: `scripts/release/android_release.sh` and
   `scripts/release/ios_release.sh`. Upload these exact files; they are the
   builds that passed the phone tests.
4. Record what they were built from, on a clean commit that is pushed:

   ```bash
   python3 scripts/release/freeze.py builds/android/crownroad-1.aab builds/ios/crownroad.ipa
   ```

   It writes `docs/releases/<version>.json` (commit, Godot, export template
   and .NET versions, version codes, schema version, the shop catalog and each
   build's SHA-256) and prints the commit and tag commands. Run them.

## 2. Back up the database

Before deploying, take a dump and prove it restores. The restore drill does
both. Set `DRILL_SOURCE_URL` (the database's direct URL, read-only role) and
`DRILL_TARGET_URL` (a scratch database) as
[server/ops/README.md](../server/ops/README.md) describes, then:

```bash
server/ops/restore_drill.sh --keep-dump ~/crownroad-backups
```

Copy the kept dump somewhere other than the VM and the database provider.

## 3. Deploy and verify the server

On the VM, from the release tag:

```bash
git fetch --tags && git checkout v1.0.0 && cd server && ./deploy.sh
```

Then check, in order:

- `curl -fsS https://<api domain>/health` answers `"status":"healthy"`.
- `curl -fsS https://<api domain>/auth/providers` lists every sign-in method
  you set up.
- `server/ops/watch.sh --test` reaches the on-call channel, and the timer is
  running (`systemctl list-timers | grep crownroad`).
- `GET /admin/purchases` shows purchases not paused.
- A sandbox purchase on each platform is granted once, and shows in
  `/admin/support/player`.

Record the image that is now live. On the VM:

```bash
docker image inspect --format '{{.Id}}' server-crownroad-server
```

Then on your own checkout of main, which has the release record,
`python3 scripts/release/freeze.py --server-image sha256:…` adds it. Commit
the change.

## 4. Submit and roll out in stages

- **Google Play:** promote the tested internal build to production as a
  staged rollout. Start at 5 to 10 %, hold at least a day, then 25 %, 50 %
  and 100 % while the triggers below stay quiet.
- **App Store:** submit the tested TestFlight build and turn on phased
  release. Apple raises it over 7 days (1, 2, 5, 10, 20, 50, then 100 %); it
  can be paused.
- Deploy the server before either store releases the build.

## 5. On call

For the first week, one named person watches the `watch.sh` alerts, both
store consoles (crashes, ANRs, reviews) and the support inbox, with a second
person who can deploy. Keep these within reach:

- Pause purchases (every API replica follows within 10 seconds, no redeploy):

  ```bash
  curl -fsS -X POST -H "X-Admin-Api-Key: $KEY" -H "Content-Type: application/json" \
    -d '{"paused":true,"message":"The storehouse is closed for a short while."}' "$API/admin/purchases"
  ```

  Purchases already paid for are still checked and delivered. Never stop the
  API to stop sales: charged purchases would go undelivered.
- The support playbook: [store/SUPPORT_PLAYBOOK.md](store/SUPPORT_PLAYBOOK.md).

## 6. Rollback triggers

The person named as rollback decision-maker (LAUNCH_CHECKLIST, scope
decision) acts on these; anyone on call may pause purchases or halt a rollout
first and ask after.

| Signal | Trigger | First action |
|---|---|---|
| `/health` | `watch.sh` CRIT for 10 minutes after a deploy | Roll back the server |
| API errors | 5xx share CRIT (≥ 5 %) for 15 minutes, or WARN (≥ 1 %) for an hour | Roll back the server if the deploy started it; otherwise find the failing route first |
| Purchase verification | Any `watch.sh` purchases CRIT (`/purchase/validate` answering 503), or one confirmed charge that wasn't delivered | Pause purchases, then fix credentials or roll back |
| Wallet | A ledger total that disagrees with a wallet, or a grant applied twice | Pause purchases |
| Crashes | Play's user-perceived crash rate over 1.09 % or ANR rate over 0.47 % for the new version, or a crash at launch on any common device | Halt the Play rollout and pause the App Store phased release |
| Progress | Two or more reports of lost progress or currency after updating | Halt both rollouts |

### How to roll back

- **Server:** on the VM, `git checkout <last good tag> && cd server &&
  ./deploy.sh`. Schema changes so far only add tables and columns, so the
  previous server runs on the newer database. Don't restore the database to
  undo a code change: restore only for damaged data, and take a fresh dump
  first so nothing written since launch is lost. Use a forward fix whenever a
  ledger or schema change can't be safely undone.
- **Game:** an installed app can't be rolled back. Halt the Play staged
  rollout (Play Console → the release → Halt rollout) and pause the App Store
  phased release, then ship a fix with a higher version code and build
  number. Players who already updated keep the build until the fix arrives.
- **After any rollback:** reconcile every purchase made in the window against
  the ledger (`/admin/support/player`, `/admin/refunds`), and write down what
  happened.

## Last good versions

Add a row after each release that has run a day at 100 % with no trigger.

| Version | Tag | Android code | iOS build | Server image | Schema | Date |
|---|---|---|---|---|---|---|
| (none yet) | | | | | | |
