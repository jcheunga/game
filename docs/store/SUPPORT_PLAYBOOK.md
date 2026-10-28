# Support playbook

Step-by-step procedures for player support after launch. Written on
2026-10-28 from the server code (`server/*.cs`), `docs/DEPLOYMENT.md` and
`docs/LAUNCH_CHECKLIST.md`. The support lookup, `GET /admin/support/player`,
was added the same day (`server/SupportLookup.cs`, `dacc1ebb`), followed by
`POST /admin/support/delete-account` (uncommitted at the time). It also
reflects the 1.0 launch decisions made that day: internet rooms hidden,
anonymous leaderboard tags, and Game Center and email as the iPhone's
sign-in options. Field names below are from that version. Check them against
the live response.

Placeholders: `[support email]`, `[api domain]` (for example
`api.crownroad.game`), `[status channel]`, `[support database role]`.

## Before you start

### Tools

Run admin calls from the server VM, or a machine you trust, never from a
shared one. The admin key is in `server/secrets/admin-api-key`. Never paste it
into a ticket or chat.

```sh
export API="https://[api domain]"
export KEY="$(cat server/secrets/admin-api-key)"

# Look a player up by player ID
curl -fsS -H "X-Admin-Api-Key: $KEY" "$API/admin/support/player?profileId=CVY-0123456789" | jq .

# Or by store transaction ID (a Google order number GPA.…, an App Store transaction ID,
# a Stripe checkout ID cs_…) or a server purchase ID PUR-…
curl -fsS -H "X-Admin-Api-Key: $KEY" "$API/admin/support/player?transactionId=GPA.1234-5678-9012-34567" | jq .
```

The answer holds no session tokens, receipts or sign-in identifiers, so you
can paste it into a ticket. It has:

| Field | Meaning |
| --- | --- |
| `profileId` | The player ID. A `DELETED-…` ID means the account was deleted and only its purchase records remain |
| `player` | Nickname (`callsign`), when the player was created and last synced. `null` if the player doesn't exist (never registered, or deleted) |
| `signInProviders` | Names of the linked sign-ins: `email`, `google`, `apple`, `playgames`, `gamecenter`. Empty for a guest |
| `activeSessions`, `lastSeenAtUnixSeconds` | Devices signed in now, and when the server last saw one |
| `wallet` | Paid currency the server has credited: `gold`, `food`, `unitUnlocks`. It counts purchases only. The device's save holds the balance the player sees, including currency earned in play |
| `purchases` | Every purchase: `purchaseId` (`PUR-…`), `productId`, `platform`, `transactionId`, `goldCredited`, `foodCredited`, `grantedUnitUnlock`, `status` (`validated` or `refunded`), `purchasedAtUnixSeconds`, `refundedAtUnixSeconds` |
| `ledger` | The latest 100 wallet entries: purchases and refunds |
| `adjustments` | Refunds and reversals to apply on the device. `appliedOnDeviceAtUnixSeconds` is 0 until the device has applied one. `goldApplied` and `foodApplied` show what it could take back |
| `cloudSave` | The last cloud backup: `saveVersion`, `uploadedAtUnixSeconds`, `sizeBytes`. `null` if none |

Times are Unix seconds in UTC. Convert with `date -u -d @1761600000` on the
VM (Linux) or `date -u -r 1761600000` on a Mac.

Other admin calls used below: `GET /admin/refunds` (the latest 100 store
events), `POST /admin/refunds` (record a refund or reinstatement),
`POST /admin/refunds/poll-google` (check Google for refunds now),
`POST /admin/support/delete-account` (delete an account for an email
request), and `GET /health` (no key).

Some procedures need SQL. Use `psql` with `[support database role]` against
the managed PostgreSQL in `DATABASE_URL`, and run anything that writes inside
a transaction.

### What to ask the player for

- **Player ID.** In the game under Settings → Account → **Player ID**; tapping
  it copies it. It also shows under More → Community → **Friends** ("Profile
  ID"). It looks like `CVY-` and 10 characters, or `ACC-` and 32.
- **Store order or transaction ID**, for anything about a purchase:
  - Google Play: the order number (`GPA.…`) on Google's receipt email, or in
    the Play Store app under Payments & subscriptions → Budget & history.
  - App Store: the order ID on Apple's receipt email, or at
    reportaproblem.apple.com.
  - A screenshot of the receipt is fine. The player can cover payment details.
- **Approximate time**, with their time zone.
- **Device and operating system**, and the store they bought from.

Players reach the support page from the game too: Settings → Account → **Help
and support** opens `https://crownroad.game/support`.

### What never to ask for

Never ask for, and never accept, anything that lets someone act as the
player:

- The "Auth token" or "Token" lines in Records & replay. They are masked, but
  ask the player to leave them out anyway.
- The game's save file. The device's save holds the session token.
- Sign-in codes, passwords, or Apple, Google or Play account details beyond
  an order ID.
- Card numbers.

If a player sends any of these, delete the message and tell them not to send
it again.

### Proving who someone is

A player ID alone isn't proof. Leaderboards show anonymous tags rather than
player IDs, but players copy and share their ID, for example to add friends.
Before you delete, export or explain account details, confirm it with one of
these:

- **Email sign-in:** the request comes from the address linked to the
  account. Check it with SQL; the lookup doesn't show addresses:

  ```sql
  SELECT profile_id FROM account_identities WHERE provider = 'email' AND subject = lower('player@example.com');
  ```

- **Any player:** two details from the lookup that only the device owner
  would know, such as:
  - the nickname (`player.callsign`);
  - the last cloud backup, from Settings → Account → **Cloud status** →
    **Check**, which shows "Saved MM-dd HH:mm · version N · X KB"
    (`cloudSave`);
  - a purchase: product, date and its store order ID (`purchases`).

## Missing purchase

The player paid but the gold or food didn't arrive.

1. **Ask the player to retry first.** Connect to the internet, open the game
   and open the Royal storehouse: tap the gold amount at the top right of the
   home map. The game checks unfinished store purchases at launch and each
   time the storehouse opens (`NativeIAPService.RetryUnfinishedPurchases`).
   The **Purchase info** tab shows the result. Most delays end here.
2. **Look the purchase up.**
   - Google Play: look up by `transactionId=GPA.…`. The server stores
     Google's order ID.
   - App Store: the order ID on Apple's receipt isn't the transaction ID the
     server stores, and the server can't translate one into the other. Look
     up by player ID and match the product and time.
3. **Read the result.**

   | What you find | What it means | Do this |
   | --- | --- | --- |
   | A purchase with `status` `validated` under this player ID | The server credited it. The device adds it once the server's answer reaches it, and only once | If step 1 didn't bring it, compare `purchasedAtUnixSeconds` with `cloudSave.uploadedAtUnixSeconds`. A purchase after the last backup is lost if the player then reinstalled, restored an older cloud save or switched account. Go to step 4 |
   | `status` `refunded` | The store refunded it, and the server took it back | Explain. See [Refund](#refund) |
   | The purchase, under a different player ID | The store ties each purchase to the player ID that bought it. A guest who later signed in to another account, or a second account, leaves its purchases behind | It can't be moved. Explain which ID holds it. If it's a `DELETED-…` ID, that account was deleted. Offer a refund (step 4) |
   | A `starter_kit` purchase, `validated`, but the player says no troop came | The Adventurer's Kit recruits the troop its card named at purchase: the first troop the player didn't own, by unlock stage (`GameState.NextKitUnitId`). A player who owned every troop saw "Every troop recruited" and got gold and food only | Ask which troop the card named, and have the player look for it in the Warband tab, where it is already recruited |
   | The kit shows **Purchased** on a device where the player never bought it | The storehouse found a `starter_kit` purchase in this account's server history: the account bought it on another device, or before an older save was restored. The check only marks the kit bought; its gold, food and troop went to the save that bought it | Explain. If that save is lost, the purchase is part of a lost save: see [Account recovery](#account-recovery) |
   | Nothing | The server never verified the payment | Ask whether the store shows it as completed. A Google payment may be pending (the shop says "Not verified yet"), and an Ask to Buy request waits for approval. Then search the server log (below). If it's completed and step 1 doesn't bring it, go to step 4 |

   Search the log around the purchase time:

   ```sh
   cd server && docker compose --env-file .env.production -f docker-compose.production.yml logs --since 48h crownroad-server | grep purchase/validate
   ```

   Answers you may see: `rate_limited` (more than 10 purchases in an hour; it
   retries later), `native_purchase_verification_unavailable` (store
   credentials or the store's API; see [Outage](#outage)),
   `transaction_assigned` (bought by another player ID), `already_purchased`
   (a second Adventurer's Kit; see [Duplicate grant](#duplicate-grant-or-double-charge)).

4. **Resolve.** No operator tool grants gold or food, so a payment that can't
   be delivered is refunded:
   - Google Play: Play Console → Order management → search the `GPA.…`
     order → **Refund**. The server learns of it within the hour (run
     `poll-google` to check at once). A device that never received the pack
     loses nothing.
   - App Store: you can't refund. Send the player to
     reportaproblem.apple.com.
   - Google refunds by itself any purchase the game hasn't acknowledged
     within 3 days. The game acknowledges (consumes) only after the grant is
     saved, so an undelivered Google purchase refunds itself.
5. **Reply.** Say what you found and what happens next. Don't promise
   currency you can't grant.

**[Decision]** Build an operator grant ("make good") if refunds aren't
enough. Today the only lever is the store refund.

## Duplicate grant or double charge

1. **Look up the player**, and ask how many payments the store shows.
2. **Match the case:**

   | Case | What the lookup shows | Do this |
   | --- | --- | --- |
   | Charged twice for two packs | Two purchases with different `transactionId` values, both `validated` | Both were delivered. If one was a mistake, refund it (Google: Play Console; Apple: reportaproblem.apple.com). The server takes that pack's currency back automatically, never below zero |
   | Adventurer's Kit paid twice | One `starter_kit` purchase. The second payment has no purchase: the server refused it (`already_purchased` in the log) | This should be rare: each time the storehouse opens, it checks the server's purchase history and marks the kit bought if this account bought it before, so the kit isn't offered again (`SyncOneTimePurchasesFromServerAsync`). A second payment means that check couldn't reach the server. Google: the game never acknowledges the second payment, and Google refunds it within 3 days. App Store: the game leaves it unfinished, and Apple keeps the money until the player asks at reportaproblem.apple.com. After that refund the game finishes the transaction by itself |
   | One payment, currency arrived twice | One purchase | The server records each store transaction once, and the device applies each purchase ID once, even across restores (`AppliedPurchaseIds` is kept in the save and the cloud save). Check the **Purchase info** tab's "Purchases made" count against the purchases list. Bonus amounts often look like a double grant: a Chest of Gold is 2,000 plus 200 |
   | One transaction on two players | Not possible | The store ties each purchase to a hash of the player ID, and the server answers `transaction_assigned` to anyone else |

3. **A real double grant from one payment** would be a bug. Collect the
   purchase ID, device, build and time, and file it. Don't take currency back:
   `POST /admin/refunds` marks the purchase refunded and removes the whole
   pack, which is wrong when no money went back.

## Account recovery

### Lost or replaced device, signed in

1. Look up the player ID if they have it. `signInProviders` shows which
   methods the account uses. Tell the player only the method names.
2. On the new device: Settings → Account → **Account** → **Manage** → the
   same method. The game downloads that account's cloud save. What each
   device offers:
   - iPhone and iPad: **Continue with Game Center** and the email code.
   - Android: **Continue with Play Games**, the email code, and **Continue
     with Google** or **Continue with Apple** when the server offers them.
   - Desktop: the email code, and Google or Apple when offered.
3. Progress since the last backup is lost. The cloud save uploads
   automatically after each won campaign stage, and when the player taps
   Settings → Account → Cloud save → **Upload**. Compare
   `cloudSave.uploadedAtUnixSeconds` with the purchases: a pack bought after
   the last backup isn't in the restored save. Treat it as a
   [missing purchase](#missing-purchase), step 4.
4. Email codes expire after 10 minutes and allow five tries. Ask the player to
   check spam.
5. **Moving to a platform without the account's method**, for example from
   Android to an iPhone with an account that uses only Google, Apple or Play
   Games: the iPhone can't sign in to it. If the old device still works, the
   player signs in there and adds the email code from Settings → Account →
   **Account** → **Manage** (it links to the signed-in account), then uses
   email on the new device. If the old device is gone, support can't add a
   method to the account. Explain, and treat recent purchases as in step 3.

### Lost device, guest only

`signInProviders` is empty. The progress belonged to that install's guest. Its
cloud save is on the server, but nothing lets another device reach a guest
without the lost device's session, and support has no tool to move it
(`docs/DEPLOYMENT.md`, "Remaining public-launch blockers"). Explain this, and
suggest signing in on the new device so it doesn't happen again. Offer
refunds for recent purchases if the player asks.

### Switched account by mistake

Each sign-in method is its own account, unless the player added it while
signed in to the other one. Signing in to a different account replaces the
progress on the device with that account's. The device keeps a backup file
of the old progress, but the game has no button to restore it.

1. Ask for the player ID shown now, and the old one if they have it. Look up
   both. The one with the later `cloudSave` and the purchases holds the
   progress.
2. Tell the player to open Settings → Account → **Account** → **Manage** →
   **Sign out**. The device becomes a guest.
3. Then **Manage** again and sign in with a method listed in that account's
   `signInProviders`. Its cloud save downloads.
4. If the lost progress was a guest's that never signed in, it is in the
   device's backup file only. Support can't restore it.
5. To avoid this, the player can add a second method while signed in: Settings
   → Account → **Account** → **Manage** → another method links to the account
   they are signed in to, as long as no other account uses it already.

### "Account deleted" notice

The game shows "Your account was deleted on another device or by support.
You're playing as a new guest." The deletion can't be undone. Purchases stay
only as pseudonymous records. Explain, and refer to the request that deleted
it if support did.

### Play Games on Android

Play Games signs in by itself at launch, but links a guest only when no
progress is at risk (`PlatformSignIn.TryAutoSignIn`). After the player signs
out, the device stays a guest until they choose an account again.

## Refund

Store refunds are taken back automatically. Only Stripe refunds and support
corrections need an operator.

| Where the player paid | Who refunds | What the server does |
| --- | --- | --- |
| App Store | Apple only. The player asks at reportaproblem.apple.com | Apple sends a `REFUND` notification to `/store/apple/notifications`. The server marks the purchase refunded, writes reversing ledger entries and an adjustment. If `APPLE_IAP_SEND_CONSUMPTION_INFO=1`, it first answers Apple's `CONSUMPTION_REQUEST` that the purchase was delivered |
| Google Play | The player through Google, or you: Play Console → Order management → the order → **Refund** | The server polls Google's voided purchases every hour (`GOOGLE_PLAY_VOIDED_POLL_MINUTES`) and does the same as for Apple. Run `poll-google` to check at once |
| Stripe (web or desktop only; phones never use it) | You, in the Stripe Dashboard | Nothing until you record it (below). There is no Stripe refund webhook |

The device applies each adjustment once: the next time the storehouse opens,
or within about ten minutes while the game is online. The storehouse then
says what it took back.

### Record a Stripe refund or a support correction

```sh
# Stripe refund: refund in the Stripe Dashboard first, then record it.
curl -fsS -X POST -H "X-Admin-Api-Key: $KEY" -H "Content-Type: application/json" \
  -d '{"transactionId": "cs_…", "action": "refund", "note": "Stripe refund re_… · ticket [id]"}' \
  "$API/admin/refunds"

# Undo a refund recorded in error.
curl -fsS -X POST -H "X-Admin-Api-Key: $KEY" -H "Content-Type: application/json" \
  -d '{"purchaseId": "PUR-…", "action": "reinstate", "note": "Refund recorded in error · ticket [id]"}' \
  "$API/admin/refunds"
```

Use `purchaseId` (`PUR-…`, from the lookup) or `transactionId`. Always add a
note with the ticket. The answer's `status`:

| Status | Meaning |
| --- | --- |
| `refunded` | Done. The adjustment waits for the device |
| `reinstated` | Done. The device gets back what it took |
| `already_refunded` | Already refunded. Nothing changed |
| `not_refundable` | The purchase isn't in a refundable state |
| `not_refunded` | Reinstate asked for a purchase that isn't refunded |
| `unknown_transaction` | HTTP 404: no purchase with that ID |

Apple's own `REFUND_REVERSED` is applied automatically. Don't record it again.

### Check a refund

- Lookup: the purchase's `status` is `refunded`, and `adjustments` has a
  `refund` entry. `appliedOnDeviceAtUnixSeconds` shows whether the device has
  applied it, and `goldApplied` and `foodApplied` what it took back.
- `GET /admin/refunds`: each store event with its `outcome`, and
  `goldUnrecovered` and `foodUnrecovered`.

### Decisions already made

- Currency the player already spent isn't taken below zero. The shortfall is
  recorded as unrecovered.
- A refunded Adventurer's Kit keeps its troop. The kit still counts as
  bought, so it can't be bought again.
- A reversed refund gives back only what was taken.
- A refund for a deleted account is recorded on its `DELETED-…` purchase and
  takes nothing.

**[Policy]** What to do about repeated refunds with large unrecovered
amounts.

## Abuse report about a nickname

In 1.0 no other player sees a nickname. Internet rooms, the only place that
showed them, are hidden (`LaunchFeatures.InternetRooms`), and leaderboards
show an anonymous tag per board ("Player" and six hex characters). So a
nickname report can only arrive by email, about something the player saw
outside the game, and the in-room **Submit report** control isn't reachable.
Reply that other players can't see nicknames in this version, and keep the
case on file.

The steps below are for when rooms are turned on. Reports then arrive by
email, or from a room: a player picks **Offensive name** (or Harassment, Room
abuse, Suspicious score) and taps **Submit report**. Room reports are kept 90
days in `room_reports`.

1. **Collect:** the reported player's ID or nickname, the room and time if
   known, and the reporter's player ID.
2. **Find the reports:**

   ```sql
   SELECT room_id, reporter_profile_id, target_profile_id, reason, details,
          to_timestamp(reported_at) AS reported
   FROM room_reports
   WHERE target_profile_id = 'CVY-…' OR reason = 'offensive_name'
   ORDER BY reported_at DESC LIMIT 50;
   ```

3. **Check the current nickname** with the lookup (`player.callsign`).
4. **Decide** against `[nickname policy]`.
5. **Act.** The tools are thin:
   - There is no rename or ban. Changing `players.callsign` doesn't stick:
     the device sends its nickname again at every profile sync
     (`POST /player-profile`). The `can_join_rooms`, `can_submit_challenges`
     and `relay_enabled` columns are never checked.
   - What works now: remove the rooms the player hosts, whose titles carry
     the nickname.

     ```sql
     BEGIN;
     DELETE FROM room_telemetry WHERE room_id IN (SELECT room_id FROM rooms WHERE host_profile_id = 'CVY-…');
     DELETE FROM room_seats WHERE room_id IN (SELECT room_id FROM rooms WHERE host_profile_id = 'CVY-…');
     DELETE FROM rooms WHERE host_profile_id = 'CVY-…';
     COMMIT;
     ```

   - If the player uses email sign-in, you can write to them
     (`account_identities`, provider `email`) and ask them to change it in
     Settings → Gameplay → **Nickname**.
   - Don't delete their account. Deletion is the owner's choice.
6. **Reply to the reporter.** Thank them. Don't tell them what happened to the
   other player.
7. **Log** the case: IDs, the nickname, the decision.

**[Gap]** Before rooms go public: a name filter on the server
(`/player-profile` and the room endpoints), a rename the device accepts,
enforcement of `can_join_rooms`, and a block control.

## Outage

Players report "Could not reach the server.", "The server took too long to
answer.", "Unable to establish a secure store session: …" or "Sign-in needs
to be enabled on the game server.". The campaign keeps working offline.

Rate limits can look like a partial outage: answers of HTTP 429 with a
`Retry-After` header. A session's requests get their own allowance once its
token has passed a session check on that API replica; until then, and for
unknown tokens, requests count against the caller's IP. Each replica keeps
that list in memory, so right after a restart or deploy, many players behind
one carrier address can briefly share a per-IP allowance. It clears as each
session is checked again. `/health` has its own allowance of 600 a minute
per IP, so monitors polling it don't eat into players' limits.

1. **Confirm.**

   ```sh
   curl -sS "$API/health"
   # {"status":"healthy","relayRooms":0,"redis":"connected"} when all is well
   # HTTP 503 {"status":"unhealthy","error":"…"} names the failing part
   ```

2. **Look.** On the VM, in `server/`:

   ```sh
   docker compose --env-file .env.production -f docker-compose.production.yml ps
   docker compose --env-file .env.production -f docker-compose.production.yml logs --tail 200 crownroad-server
   docker compose --env-file .env.production -f docker-compose.production.yml logs --tail 200 caddy
   ```

   Then check the PostgreSQL provider's status page, the certificate (Caddy
   renews it), disk space and Redis.
3. **Fix.** Restart with `./deploy.sh`, which checks the configuration, runs
   the server and data checks, rebuilds and starts. If the database must be
   restored, preserve the current one first (`docs/DEPLOYMENT.md`,
   "Operations after launch").
4. **Purchases during the outage.**
   - A player with no session can't start a purchase, so isn't charged.
   - A payment that went through but couldn't be verified stays unfinished.
     The game retries at launch and when the storehouse opens.
   - **Google refunds unacknowledged purchases after 3 days.** Recover within
     3 days, or those payments go back to players without a grant.
   - Apple keeps unfinished transactions until the game finishes them.
   - If only verification fails (`native_purchase_verification_unavailable`),
     check the store credentials in `server/secrets/` and the service
     account's Play Console permissions, and look for an Apple or Google
     service problem.
5. **Email sign-in only:** check the mail service and the `SMTP_*` settings.
   Port 587 only. `GET /auth/providers` shows which sign-in methods the
   server has on.
6. **Tell players** on `[status channel]` and the support page. The game has
   no in-game announcements: `/config/live` returns fixed values and the game
   doesn't read it.
7. **After recovery.**
   - Run `POST /admin/refunds/poll-google` once. Each poll looks back two
     days past the last one.
   - Read `GET /admin/refunds` for failed events. Apple retries its
     notifications by itself.
   - Reconcile the outage window: Play Console orders and Apple's sales
     report against the purchases on the server.
   - Answer the tickets, then write up what happened.

There is no switch that stops purchases before payment. Leaving out the App
Store key makes the server refuse App Store verification, but only after the
player has paid.

## Account deletion request by email

The support page promises deletion within 30 days, and an email when it's
done (`/support#delete-data`).

1. **Read the request.** It should say "Delete my data" and give the player
   ID.
2. **Offer the faster way.** If the player can open the game: Settings →
   Account → **Delete account** → **Delete** → **Delete account**. It deletes
   at once. If the game says "Session expired. Email support to delete.", go
   on.
3. **Prove who they are** ([above](#proving-who-someone-is)). Never delete on
   the player ID alone.
4. **Delete** with the admin route, which runs the same code as in-app
   deletion (`server/AccountDeletion.cs`). Type the player ID twice; a
   mismatch deletes nothing:

   ```bash
   curl -fsS -X POST -H "X-Admin-Api-Key: $KEY" -H "Content-Type: application/json" \
     -d '{"profileId":"CVY-…","confirmProfileId":"CVY-…"}' "$API/admin/support/delete-account"
   ```

   It answers `{"status":"deleted"}`, or `404 not_found` if no such player
   exists (already deleted, or a typo).
5. **Check.** The lookup by player ID now shows `player: null` and no
   purchases. A lookup by one of the old transaction IDs shows a `DELETED-…`
   ID.
6. **Reply** that it's done. What stays: purchase and refund records under a
   random `DELETED-…` code, the store event log, database backups until they
   expire (`[backup retention period]`) and rotated server logs.
7. **Log** the date, the player ID and how you verified. Don't keep copies of
   the deleted data.

The player's device finds out the next time it reaches the server: it gets
`account_deleted`, starts a new guest and shows the "Account deleted"
notice. Sign in with Apple is revoked by the cleanup service within 15
minutes, retried for about three weeks if Apple is down.

## Data access request

A player asks for a copy of the data held about them.

1. **Prove who they are** ([above](#proving-who-someone-is)).
2. **Collect.**
   - The lookup answer: purchases, wallet, the latest 100 ledger entries,
     adjustments, sign-in method names, sessions and cloud-save details.
   - The rest with the SQL in [the appendix](#appendix-export-a-players-data).
3. **Leave out** secrets and other people's data: `token_hash`, `credential`
   (the encrypted Apple token), `join_token`, `ticket_id`, `receipt_token`,
   and who reported them.
4. **Explain** what isn't on the server: guilds, friends, the arena, the
   weekly raid and the local save stay on the device, and the player can see
   them in the game. Analytics and crash reports exist only if the player
   turned them on, and are kept 30 and 90 days.
5. **Send** the files to the verified address within 30 days `[check the
   deadline for the player's country]`.
6. **Log** the date and what you sent.

## Appendix: export a player's data

In `psql`, after `\set pid 'CVY-…'`. Each `\copy` writes a CSV file in the
current directory. Timestamps are converted to UTC.

```sql
\copy (SELECT profile_id, callsign, to_timestamp(created_at) AS created, to_timestamp(synced_at) AS last_synced FROM players WHERE profile_id = :'pid') TO 'player.csv' CSV HEADER
\copy (SELECT provider, subject AS account FROM account_identities WHERE profile_id = :'pid') TO 'sign_ins.csv' CSV HEADER
\copy (SELECT to_timestamp(created_at) AS created, to_timestamp(last_seen_at) AS last_used, to_timestamp(expires_at) AS expires, CASE WHEN revoked_at > 0 THEN to_timestamp(revoked_at) END AS signed_out FROM auth_sessions WHERE profile_id = :'pid') TO 'sessions.csv' CSV HEADER
\copy (SELECT save_version, to_timestamp(uploaded_at) AS uploaded, save_data FROM cloud_saves WHERE profile_id = :'pid') TO 'cloud_save.csv' CSV HEADER
\copy (SELECT board_code, score, player_won, stars_earned, elapsed_seconds, hull_remaining, enemy_defeats, to_timestamp(submitted_at) AS submitted FROM challenge_results WHERE profile_id = :'pid') TO 'challenge_results.csv' CSV HEADER
\copy (SELECT achievement_id, unlocked_at FROM achievements WHERE profile_id = :'pid') TO 'achievements.csv' CSV HEADER
\copy (SELECT daily_date, score, completed_at FROM daily_completions WHERE profile_id = :'pid') TO 'daily.csv' CSV HEADER
\copy (SELECT room_id, callsign, seat_label, status, score, elapsed_seconds, hull_remaining, enemy_defeats, race_status, to_timestamp(joined_at) AS joined FROM room_seats WHERE profile_id = :'pid') TO 'room_seats.csv' CSV HEADER
\copy (SELECT room_id, title, board_code, board_title, status, to_timestamp(created_at) AS created FROM rooms WHERE host_profile_id = :'pid') TO 'rooms_hosted.csv' CSV HEADER
\copy (SELECT room_id, elapsed_seconds, hull_ratio, enemy_defeats, race_status, to_timestamp(reported_at) AS reported FROM room_telemetry WHERE profile_id = :'pid') TO 'race_updates.csv' CSV HEADER
\copy (SELECT room_id, reason, details, to_timestamp(reported_at) AS reported FROM room_reports WHERE reporter_profile_id = :'pid') TO 'reports_made.csv' CSV HEADER
\copy (SELECT room_id, reason, to_timestamp(reported_at) AS reported FROM room_reports WHERE target_profile_id = :'pid') TO 'reports_about.csv' CSV HEADER
\copy (SELECT event_type, event_data, client_version, platform, to_timestamp(recorded_at) AS recorded FROM analytics_events WHERE profile_id = :'pid') TO 'analytics.csv' CSV HEADER
\copy (SELECT error_type, error_message, stack_trace, client_version, platform, scene, to_timestamp(reported_at) AS reported FROM crash_reports WHERE profile_id = :'pid') TO 'crash_reports.csv' CSV HEADER
\copy (SELECT purchase_id, product_id, platform, transaction_id, gold_credited, food_credited, granted_unit_unlock, status, to_timestamp(purchased_at) AS purchased, CASE WHEN refunded_at > 0 THEN to_timestamp(refunded_at) END AS refunded FROM purchases WHERE profile_id = :'pid') TO 'purchases.csv' CSV HEADER
\copy (SELECT gold_balance, food_balance, unit_unlock_balance, to_timestamp(updated_at) AS updated FROM player_wallets WHERE profile_id = :'pid') TO 'wallet.csv' CSV HEADER
\copy (SELECT entry_id, currency_type, amount, reason, purchase_id, adjustment_id, to_timestamp(created_at) AS created FROM wallet_ledger WHERE profile_id = :'pid') TO 'wallet_ledger.csv' CSV HEADER
\copy (SELECT adjustment_id, purchase_id, product_id, kind, gold_delta, food_delta, gold_applied, food_applied, to_timestamp(created_at) AS created FROM wallet_adjustments WHERE profile_id = :'pid') TO 'refund_adjustments.csv' CSV HEADER
\copy (SELECT e.source, e.event_type, e.outcome, e.detail, to_timestamp(e.received_at) AS received FROM store_events e JOIN purchases p ON p.purchase_id = e.purchase_id WHERE p.profile_id = :'pid') TO 'store_events.csv' CSV HEADER
```

The arena, raid, friends and guild tables hold only rows from before
2026-10-28, when nothing wrote them any more. Check them too
(`arena_snapshots`, `arena_results`, `raid_contributions`, `friendships`,
`friend_gifts`, `guild_members`) and add any rows found.

## Gaps found while writing this

| Gap | Effect | Suggested fix |
| --- | --- | --- |
| No operator grant | A paid pack that can't be delivered can only be refunded | An admin "make good" that creates a wallet adjustment the device applies, like a reversed refund |
| No Apple order ID lookup | An Apple receipt's order ID can't be matched directly | Call the App Store Server API's Look Up Order ID from the lookup endpoint |
| No moderation tools | Offensive nicknames can't be renamed or blocked. No one else sees them in 1.0, so this matters once rooms are turned on | Server name filter, admin rename, enforce `can_join_rooms`, a block control |
| Guest progress can't move | A guest who loses the device loses everything, purchases included | Prompt guests to sign in after their first purchase |
| No restore for local backups | Progress replaced by signing in to another account stays in a file the player can't reach | A restore choice in the Account panel |
| No in-game announcements | Players learn of an outage only from errors | Read `/config/live`'s `announcement` in the game |
| No pre-payment purchase switch | Purchases can't be paused before payment | A server flag the storehouse checks before opening the store sheet |
