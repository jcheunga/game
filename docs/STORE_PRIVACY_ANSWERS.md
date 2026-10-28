### Privacy manifest

The iOS preset (`[preset.2.options]` in `export_presets.cfg`) now declares the
collected types above, so Godot writes them into `PrivacyInfo.xcprivacy`:
email address, user ID, purchase history, product interaction, gameplay
content and crash data, all linked to the player and none used for tracking.
The purposes use Godot's flags (Analytics 1, App Functionality 2): user ID is
3, product interaction 1, the rest 2. The required-reason APIs keep Godot's
defaults (file timestamps, system boot time, disk space). Edit these in the
editor's export dialog from now on, never while the editor has the file open.

# Store privacy answers (draft)

Draft answers for Google Play's **Data safety** form (Play Console → App
content → Data safety) and Apple's **App Privacy** labels (App Store Connect →
App Privacy). They come from a code audit on 2026-10-18 (main at `3bca1ed7`),
the same audit behind the privacy policy in `site/src/privacy.html`, updated
the same day for account deletion and refunds once they were merged, and on
2026-10-28 when the unused guild, arena, friends and raid endpoints went. Line
numbers in the last table are from the first audit. This is a draft for you to
review, not legal advice.

Markers:

- **[Legal]** needs your judgement, or a lawyer's.
- **[Check]** confirm in the console or in a vendor's documentation.

Work through [What you must review](#what-you-must-review) before submitting.
When the game's data handling changes, update this file together with the
privacy policy (see "Keeping the privacy policy true" in
[WEBSITE.md](WEBSITE.md)).

## Answers by data type

All server traffic goes over HTTPS, and every stored record carries the
player ID, so everything collected is linked to the player. Nothing is used
for tracking: the game has no ads, no advertising ID, and no third-party
analytics or ad SDKs.

| Data | Collected | Linked to player | Tracking | Purposes | Optional or required | Encrypted in transit | Deletable |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Player ID (`CVY-…` or `ACC-…`) | Yes | Yes | No | App functionality, account management, fraud prevention and security, analytics (opt-in events only) | Required. Registered automatically, with the nickname, whenever the server is reachable, even for guests | Yes | In the app (Settings → Account → Delete account) or by email |
| Session records (token hash, created, last used, expiry) | Yes | Yes | No | Security, account management | Required | Yes. Only a SHA-256 hash of the token is stored | In the app (Settings → Account → Delete account) or by email. Otherwise deleted a week after the session expires or is signed out. After a deletion, hashes of the sessions still active are kept 31 days so a repeated request gets the same answer |
| Nickname (callsign) | Yes | Yes | No | App functionality (shown to other players) | Required. Defaults to "Lantern" and is sent on registration | Yes | Change any time in Settings. Old copies stay on room records. In the app (Settings → Account → Delete account) or by email |
| Email address | Yes, for email sign-in only | Yes | No | Account management | Optional | Yes. The server sends the code to the mail provider over STARTTLS | In the app (Settings → Account → Delete account) or by email |
| Google account ID | Yes, for Google sign-in only | Yes | No | Account management | Optional | Yes | In the app (Settings → Account → Delete account) or by email |
| Google email and verified flag | Read once to check the account is verified, never stored | — | No | Account management | Optional | Yes | Not stored |
| Apple account ID and refresh token (Sign in with Apple) | Yes, if used. No name or email requested. The refresh token is stored encrypted (`ACCOUNT_TOKEN_KEY`) so deletion can revoke it | Yes | No | Account management | Optional | Yes | In the app (Settings → Account → Delete account) or by email. Deletion revokes the token with Apple, retrying for about three weeks if Apple is down |
| Google Play Games player ID | Yes | Yes | No | Account management | **Not fully optional.** On Android the game links it automatically at launch when the phone is signed in to Play Games and no progress is at risk. Signing out stops it on that device **[Legal]** | Yes | In the app (Settings → Account → Delete account) or by email |
| Game Center player ID (`teamPlayerID`) | Yes, if used | Yes | No | Account management | Optional | Yes | In the app (Settings → Account → Delete account) or by email |
| Gameplay results (challenge scores, achievements, daily scores, rooms joined and results) | Yes | Yes | No | App functionality, fraud prevention (fair play) | Required for online features | Yes | In the app (Settings → Account → Delete account) or by email |
| Live race updates | Yes | Yes | No | App functionality | Required in online races | Yes | Deleted automatically after 24 hours |
| Cloud save (progress, settings, nickname, player ID, delivered purchase IDs) | Yes | Yes | No | App functionality, account management | Required. Uploads automatically after each won stage once connected **[Legal]** | Yes | Replaced on each upload. In the app (Settings → Account → Delete account) or by email |
| Purchase history and paid-currency ledger | Yes | Yes | No | App functionality, fraud prevention | Optional (only players who buy) | Yes | Deletion removes the balance and ledger. Purchase records stay under a random `DELETED-` pseudonym for tax and accounting law **[Legal]** |
| Refund records (store notices: store, order or transaction ID, type, time; the currency taken back and any shortfall the device reports) | Yes, for refunded purchases | Yes | No | App functionality, fraud prevention | Optional (only refunded purchases) | Yes | Kept with the purchase record, under the same pseudonym after a deletion |
| Gameplay analytics events | Yes, opt-in | Yes | No | Analytics | Optional, off by default | Yes | Deleted automatically after 30 days, or with the account |
| Crash reports | Yes, opt-in | Yes | No | App functionality (fixing bugs) | Optional, off by default | Yes | Deleted automatically after 90 days, or with the account |
| Player reports (both player IDs, reason, generated note) | Yes | Yes | No | Fraud prevention and safety | Optional | Yes | Deleted automatically after 90 days |
| IP address | Transient: hashed for rate limiting, and in server logs | Logs pair it with the player ID | No | Security, connecting the game | Required | Yes | Rate-limit hashes expire in about a minute. Logs rotate at 5 × 20 MB per container |

Not collected by the game: precise or approximate location, contacts,
messages, photos or videos (screenshots stay on the device), audio, files,
calendar, health, browsing or search history, installed apps, payment card
details, advertising ID or hardware device IDs.

Guilds, the arena, friends and the weekly raid run on the device and send
nothing to the server. Their old server endpoints were removed (review
item 9).

## Google Play: Data safety

### First page

| Question | Draft answer |
| --- | --- |
| Does your app collect or share any of the required user data types? | Yes |
| Is all of the user data collected by your app encrypted in transit? | Yes. The game talks to the API over HTTPS (Caddy-managed certificates), and the server reaches Google, Apple and the mail service over TLS. **[Check]** the release build's `crownroad/network/api_base_url` is an `https://` URL |
| Which account creation methods does your app support? | OAuth (Google, Sign in with Apple) and Other (email with a one-time code, Google Play Games, and the guest profile the game creates automatically). **[Check]** the console's exact options |
| Delete account URL | `https://<domain>/support#delete-data`. It describes deleting in the app (Settings → Account → Delete account) and by email for players who can't open the game |
| Do you provide a way for users to request that their data is deleted? | Yes: in the app, which deletes at once, or by email |

### Data types

Google's definition of "shared" leaves out transfers to service providers
acting for you, transfers the user starts and expects, and legal
requirements. On that reading this draft marks nothing as shared, but two
cases need your call **[Legal]**:

- Other players, and anyone calling the public API, can see player IDs and
  nicknames next to scores. `/challenge-boards`, `/daily/leaderboard` and
  `/challenge-room-scoreboard` need no session, and `/challenge-rooms` lists
  hosts' nicknames.
- Google Play and the App Store receive a SHA-256 fingerprint of the player
  ID with each purchase (Google's `obfuscatedExternalAccountId`, Apple's
  `appAccountToken`), and the server sends them purchase tokens to verify.

"Processed ephemerally" is No for every collected type below, because each is
stored. The only ephemeral data is the Google email address read at sign-in.

| Play category → data type | Collected | Shared | Required or optional | Purposes | Covers |
| --- | --- | --- | --- | --- | --- |
| Personal info → Name | Yes | No | Required | App functionality, Account management | The nickname. Play's "Name" includes nicknames |
| Personal info → Email address | Yes | No | Optional | Account management | Email sign-in |
| Personal info → User IDs | Yes | No | Required | App functionality, Account management, Fraud prevention, security and compliance, Analytics | Player ID, linked Google, Apple, Play Games and Game Center IDs, session records. Analytics covers the opt-in events, which carry the player ID |
| Financial info → Purchase history | Yes | No | Optional | App functionality, Fraud prevention, security and compliance | Purchase records, refund records and the paid-currency ledger |
| Financial info → Payment info, Credit score, Other | No | | | | Google Play takes payment. Stripe checkout is desktop-only (`scripts/core/NativeIAPService.cs:93`) |
| App activity → Other actions | Yes | No | Required | App functionality, Analytics, Fraud prevention, security and compliance | Gameplay: challenge and daily results, achievements, online rooms, live race updates, player reports, cloud save, and the opt-in analytics events |
| App activity → App interactions | Yes | No | Optional | Analytics | Opt-in analytics: session starts, stage starts and finishes. **[Check]** You could fold this into "Other actions". Declaring both is the cautious reading |
| App info and performance → Crash logs | Yes | No | Optional | App functionality, Analytics | Opt-in crash reports |
| App info and performance → Diagnostics, Other app performance data | No | | | | The game version and platform only travel inside analytics and crash reports. **[Check]** |
| Device or other IDs | Yes **[Legal]** | No | Required | App functionality, Account management, Fraud prevention, security and compliance | The player ID is random and created per install, much like an installation ID, which Play counts here. Declaring it under both User IDs and this type is the cautious answer. No hardware IDs or advertising ID are read |
| Location (approximate and precise) | No **[Legal]** | | | | IP addresses connect the game and limit abuse. They are never used to work out location |
| Web browsing, Messages, Photos and videos, Audio, Files and docs, Calendar, Contacts, Health and fitness, Installed apps, In-app search history, Other user-generated content | No | | | | |

### SDKs in the Android build

- Google Play Billing Library, through `android/plugins/GodotGooglePlayBilling`.
- Google Play Games Services v2 (`play-services-games-v2`, in
  `native/PlayGamesSignIn/build.gradle`).

**[Check]** Google's published data disclosures for these two SDKs (Google
Play SDK Index), and add any data types they say they collect for the app.

## Apple: App Privacy

### Tracking

Answer **No** for every data type, and the app needs no App Tracking
Transparency prompt. The app doesn't combine its data with other companies'
data for advertising, doesn't share it with data brokers, and doesn't read the
advertising identifier. The hashed player ID goes to Apple only to verify App
Store purchases.

### Data types

Apple counts data as "collected" when it leaves the device and is kept longer
than needed to answer the request. Data read only to answer the request (the
Google email at sign-in) isn't collected. Every collected type is linked to
the player through the player ID.

| Apple category → data type | Collected | Linked | Tracking | Purposes | Covers |
| --- | --- | --- | --- | --- | --- |
| Contact info → Email address | Yes | Yes | No | App Functionality | Email sign-in. Sign in with Apple requests no email |
| Contact info → Name | No **[Check]** | | | | The nickname is a handle, which Apple lists under User ID |
| Identifiers → User ID | Yes | Yes | No | App Functionality, Analytics | Player ID, nickname, Game Center `teamPlayerID`, Apple, Google and Play Games account IDs. Analytics because opt-in events carry the player ID |
| Identifiers → Device ID | No **[Legal]** | | | | Neither IDFA nor IDFV is read. The player ID is treated as an account-level ID. If you disagree, declare it here as well |
| Purchases → Purchase history | Yes | Yes | No | App Functionality | Purchase records, refund records and the paid-currency ledger |
| Usage data → Product interaction | Yes | Yes | No | Analytics | Opt-in analytics: session starts, stage starts and finishes |
| User content → Gameplay content | Yes | Yes | No | App Functionality | Cloud saves, challenge and daily results, achievements, online rooms, live race updates, player reports |
| Diagnostics → Crash data | Yes | Yes | No | App Functionality | Opt-in crash reports. Apple's App Functionality purpose includes reducing crashes |
| Diagnostics → Performance data, Other diagnostic data | No | | | | |
| Location, Financial info, Contacts, Health and fitness, Sensitive info, Browsing history, Search history, Other user content, Audio, Photos or videos, Other data | No | | | | Payment goes through the App Store |

Apple's labels have no "optional" field. The opt-in analytics and crash
reports are still declared, because they are collected repeatedly and so
don't meet every condition for Apple's optional-disclosure exemption.
**[Check]**

### Privacy manifest

The iOS export preset (`[preset.2.options]` in `export_presets.cfg`) has no
privacy-manifest settings yet. Godot writes `PrivacyInfo.xcprivacy` from the
iOS preset's Privacy options (collected data types and required-reason APIs).
Set them in the editor's export dialog to match the table above. Don't edit
`export_presets.cfg` while the editor is open, because it rewrites the file
from memory. **[Check]** the option names in the editor, and which
required-reason APIs Godot's iOS template declares.

## What you must review

1. **[Legal] How long purchase records are kept.** The policy says "as long
   as tax and accounting law requires". No code enforces a period:
   `purchases`, `player_wallets` and `wallet_ledger` rows are never deleted.
   Pick a period for your jurisdiction, and say it in the policy if you want
   to be specific.
2. **[Legal] Automatic Play Games sign-in.** It links the Play Games player ID
   without asking (`scripts/core/PlatformSignIn.cs:48`). The policy relies on
   legitimate interests for this, and the Play form treats the ID as
   required. If you would rather ask first, the game needs a change.
3. **[Legal] Public player IDs.** The leaderboard and room endpoints return
   player IDs to anyone, with no session needed. Decide whether that counts
   as sharing, or change them to return nicknames only (a code change).
4. **[Legal] User IDs or device IDs.** The answers above declare the player ID
   as a user ID on both stores and also as a device ID on Play. Decide which
   reading you take.
5. **[Legal] Children and age rating.** The policy says the game isn't
   directed at under-13s. Check that against your store age ratings. If the
   Play target audience includes children, the Families policy applies and
   email collection needs more care.
6. **Account deletion (built).** Settings → Account → Delete account removes
   everything keyed by the player, including `account_identities` and pending
   `account_challenges`, and keeps purchase and refund records under a random
   pseudonym (`server/AccountDeletion.cs`). Sign in with Apple refresh tokens
   are now stored, encrypted, so deletion can revoke them. They are declared
   above under the Apple account ID. **[Check]** whether either store wants
   them declared separately.
7. **Session records (done 2026-10-28).** Sessions stop working after 30 days or at sign-out, and the
   server deletes each record a week after that (`SessionAuth.PurgeEndedSessions`, run by
   `StaleDataCleanup`). The policy's player ID row says so.

8. **Name the email provider.** The policy says "our email delivery
   provider". Once you choose one (Postmark, Amazon SES, Resend…), name it in
   the policy's sharing list and sign its data processing agreement.
9. **Unused server endpoints (removed).** `/guild/*`, `/arena/*`,
   `/friends/*`, `/raid/*` and `/leaderboard/arena` are gone. The game never
   called them: guilds, the arena, friends and the raid keep their state in
   the save. Their tables stay but nothing writes them, and account deletion
   still erases any rows stored before (`server/AccountDeletion.cs`). Nothing
   to declare.
10. **Refunds (built).** App Store Server Notifications and Google Play's
    voided-purchases list mark purchases refunded, and the device takes the
    currency back once (`server/PurchaseRefunds.cs`). The new refund records
    are declared above under purchase history. Polling Google sends Google
    nothing new about the player. Answers to Apple's refund requests
    (`server/AppStoreConsumption.cs`, on once `APPLE_IAP_SEND_CONSUMPTION_INFO=1`)
    tell Apple only that a purchase was delivered, with no free sample, and
    that the player agreed through the terms. The reason Apple passes on is
    kept in the refund record. No new data type.
11. **Finding the player ID (done 2026-10-28).** Settings → Account shows it in a Player ID row,
    and tapping it copies it. The support page points there.

12. **[Check] SDK disclosures** for Play Billing and Play Games Services
    (above).
13. **iOS privacy manifest (set 2026-10-28)** (above). **[Check]** the exported
    `PrivacyInfo.xcprivacy` once against the App Privacy answers before submitting.
14. **Publishing.** Set `effective_date` in `site/site.json` when the policy
    goes live, and have the policy reviewed together with these answers.

## Where each answer comes from

| Statement | Code |
| --- | --- |
| Player ID format and creation | `scripts/core/GameState.cs:6052` (`CVY-`), `server/AccountAuth.cs:507` (`ACC-`, default nickname "Caravan") |
| Guests register automatically when the server is reachable | `scripts/core/AccountSignIn.cs:38`, `server/Endpoints.cs:18` |
| Players table (ID, nickname, created and synced times) | `server/Database.cs:204` |
| Sessions: hashed tokens, 30-day lifetime, no purge | `server/Database.cs:526`, `server/SessionAuth.cs:14`, `server/StaleDataCleanup.cs` (no session cleanup) |
| Linked sign-in identities, including email addresses | `server/AccountAuth.cs:100` |
| Pending sign-ins expire in 10 minutes and are cleared lazily | `server/AccountAuth.cs:104`, `server/AccountAuth.cs:135` |
| Sign-in code emailed through SMTP | `server/AccountAuth.cs:155` |
| Google: `openid email` scope, keeps only the account ID | `server/AccountAuth.cs:212`, `server/AccountAuth.cs:244` |
| Apple: no scopes, keeps the account ID and an encrypted refresh token | `server/AccountAuth.cs`, `server/CredentialProtection.cs` |
| Play Games: player ID only, automatic sign-in | `server/AccountAuth.cs:427`, `scripts/core/PlatformSignIn.cs:48`, `native/PlayGamesSignIn/src/main/java/com/crownroad/playgames/PlayGamesSignInPlugin.java:87` |
| Game Center: `teamPlayerID`, signature hash kept 20 minutes | `server/AccountAuth.cs:475`, `native/GameCenter/src/game_center.mm` |
| Gameplay tables | `server/Database.cs:216` (challenge results), `:252` (room seats), `:310` (achievements), `:318` (daily completions) |
| Public leaderboards and scoreboards | `server/Endpoints.cs:140`, `:211`, `:587`, `:978` |
| Live race updates 24 h, reports and crash reports 90 days, analytics 30 days | `server/StaleDataCleanup.cs:10` |
| Cloud save contents, exclusions, automatic upload | `scripts/core/CloudSavePrivacy.cs:10`, `server/Endpoints.cs:1790`, `scripts/core/GameState.cs:6554` |
| Local copy of progress when switching accounts or signing out | `scripts/core/GameState.Account.cs:15` |
| Purchase records, token fingerprint, 10-an-hour limit | `server/Database.cs:327`, `server/Endpoints.cs:1452`, `server/Endpoints.cs:1497` |
| Stripe stores its payment ID unhashed | `server/Endpoints.cs:2140` |
| Hashed player ID sent to Google and Apple | `server/StorePurchaseVerification.cs:111`, `server/StorePurchaseVerification.cs:186` |
| Paid-currency wallet and ledger | `server/Database.cs:538`, `server/Database.cs:547` |
| Analytics opt-in and contents | `scripts/core/AnalyticsService.cs:21`, `scripts/core/GameState.cs:1395`, `server/Endpoints.cs:1640` |
| Crash reports opt-in and contents | `scripts/core/CrashReporter.cs:45`, `server/Endpoints.cs:1744` |
| Rate-limit keys use hashed IPs or session tokens | `server/RateLimiter.cs:84` |
| Request logs with player IDs, rotated | `server/RequestLogger.cs:27`, `logging:` blocks in `server/docker-compose.production.yml` |
| Stripe only off mobile | `scripts/core/NativeIAPService.cs:93` |
| Account deletion: what is deleted, kept and revoked | `server/AccountDeletion.cs` |
| Refunds: App Store notifications, Google voided purchases, ledger and device adjustments | `server/PurchaseRefunds.cs`, `server/AppStoreNotifications.cs`, `server/GooglePlayVoidedPurchases.cs` |
