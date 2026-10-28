# Privacy forms, click by click

The draft answers in [STORE_PRIVACY_ANSWERS.md](../STORE_PRIVACY_ANSWERS.md),
turned into fill-ins for Google Play's Data safety form and Apple's App
Privacy questionnaire, in the order each console asks. Then export
compliance. Written on 2026-10-28 from that file, including its review items
7, 11 and 13, which were done that day (`dacc1ebb`, `22490ed6`), and item 3,
settled the same day by the 1.0 launch decisions (uncommitted at the time):
public leaderboards show anonymous tags, internet rooms are hidden, and the
iPhone offers only Game Center and email sign-in.

Markers:

- **[Item n]** depends on item n of that file's "What you must review" list.
  Both variants are given. **Variant A** is the draft's answer.
- **[Check]** confirm the wording in the console.
- **[Legal]** your judgement, or a lawyer's.

Not declared anywhere below: guilds, the arena, friends and the weekly raid.
Their server endpoints were removed on 2026-10-28 (`640846e7`, merged in
`a7e1362b`), and the game keeps them on the device. Data that never leaves
the device isn't "collected" on either form.

## Google Play: Data safety

Play Console → the app → **App content** (under Policy and programs) →
**Data safety** → **Start**. Read the overview, then **Next**.

### Step 1: Data collection and security

| # | Question | Answer | Notes |
| --- | --- | --- | --- |
| 1 | Does your app collect or share any of the required user data types? | **Yes** | |
| 2 | Is all of the user data collected by your app encrypted in transit? | **Yes** | HTTPS to the API, TLS from the server to Google, Apple and the mail service. **[Check]** the release build's `crownroad/network/api_base_url` starts with `https://` |
| 3 | Which of the following methods of account creation does your app support? | **OAuth** and **Other** | OAuth: Google, and Sign in with Apple if you set it up (it is optional and Android-only for 1.0). Each shows only when the server offers it. Other, described as: "Email with a one-time code, Google Play Games, and a guest profile the game creates automatically." **[Check]** the console's options |
| 4 | Delete account URL | `https://crownroad.game/support#delete-data` | The page explains in-app deletion (Settings → Account → Delete account) and email for players who can't open the game |
| 5 | Do you provide a way for users to request that some or all of their data is deleted, without requiring them to delete their account? | **No** | The game deletes at account level. Turning analytics or crash reports off stops new data but doesn't delete what was sent; the server deletes analytics after 30 days and crash reports after 90. If you decide support will take partial deletion requests by email, answer Yes. **[Check]** this question appears; older versions of the form asked only "Do you provide a way for users to request that their data is deleted?", which is **Yes** |
| 6 | Independent security review (optional badge) | Skip | No review done |

**Next.**

### Step 2: Data types

Tick these, and nothing else:

| Category | Tick | Covers |
| --- | --- | --- |
| Personal info | **Name** | The nickname. Play's "Name" includes nicknames |
| Personal info | **Email address** | Email sign-in |
| Personal info | **User IDs** | Player ID; linked Google, Apple and Play Games IDs; session records |
| Financial info | **Purchase history** | Purchases, refund records, paid-currency ledger |
| App activity | **App interactions** | Opt-in analytics: session starts, stage starts and finishes |
| App activity | **Other actions** | Challenge and daily results, achievements, cloud save, opt-in analytics events. Online rooms, live race updates and room reports are also covered by the draft, but 1.0 sends none of them because rooms are hidden |
| App info and performance | **Crash logs** | Opt-in crash reports |
| Device or other IDs | **Device or other IDs**, Variant A only **[Item 4]** | The player ID, which is random per install, like an installation ID |

Left unticked: Location (both), Address, Phone number, Race and ethnicity,
Political or religious beliefs, Sexual orientation, Other info, User payment
info, Credit score, Other financial info, Health info, Fitness info, Emails,
SMS or MMS, Other in-app messages, Photos, Videos, all Audio files, Files and
docs, Calendar events, Contacts, In-app search history, Installed apps, Other
user-generated content, Web browsing history, Diagnostics, Other app
performance data.

- **[Item 4] User IDs or device IDs.** Variant A (the draft, cautious): tick
  both User IDs and Device or other IDs. Variant B: tick User IDs only, and
  treat the player ID as an account ID.
- **[Legal] IP addresses.** They are hashed for rate limiting (about a
  minute) and appear in rotated server logs. They are never turned into a
  location, so Location stays unticked. If you take the view that logged IPs
  are approximate location, tick Location → Approximate location, collected,
  required, for Fraud prevention, security and compliance.
- **[Item 12] SDKs.** Check Google's published data disclosures for the Play
  Billing Library and Play Games Services v2 (Google Play SDK Index). Add any
  type they list that isn't ticked above.

**Next.**

### Step 3: Data usage and handling

For each ticked type, press **Start** and answer in the console's order:

1. Is this data collected, shared, or both?
2. Is this data processed ephemerally? (shown only for some types)
3. Is this data required for your app, or can users choose whether it's
   collected?
4. Why is this user data collected? (tick all)
5. Why is this user data shared? (only when shared)

"Processed ephemerally" is **No** for every type: each is stored. The only
data used ephemerally is the Google email address read at sign-in, which
isn't declared because it isn't kept.

| Type | 1. Collected or shared | 3. Required or optional | 4. Collection purposes |
| --- | --- | --- | --- |
| Name | Collected | Required. The nickname defaults to "Lantern" and is sent when the guest registers | App functionality, Account management |
| Email address | Collected | Optional | Account management |
| User IDs | Collected | Required **[Item 2]** | App functionality, Account management, Fraud prevention, security and compliance, Analytics |
| Purchase history | Collected | Optional (only players who buy) | App functionality, Fraud prevention, security and compliance |
| App interactions | Collected | Optional (off until the player opts in) | Analytics |
| Other actions | Collected | Required | App functionality, Analytics, Fraud prevention, security and compliance |
| Crash logs | Collected | Optional (off until the player opts in) | App functionality, Analytics |
| Device or other IDs (Variant A of item 4) | Collected | Required | App functionality, Account management, Fraud prevention, security and compliance |

- **Item 3, settled: nothing is shared.** Challenge and daily leaderboards
  return an anonymous tag per board ("Player" and six hex characters from a
  hash of the player ID and the board, `LeaderboardTag`), never a player ID
  or nickname, and internet rooms are hidden in 1.0. No other player sees a
  player ID or nickname, so Name and User IDs are **Collected**, not shared.
  The room routes still return IDs on the server; if rooms are turned on
  later, decide this again. The hashed player ID sent to Google Play with
  each purchase (`obfuscatedExternalAccountId`) is a transfer the player
  starts and expects, so it isn't sharing either.
- **[Item 2] Automatic Play Games sign-in.** The game links the Play Games
  player ID without asking when no progress is at risk.
  - Variant A (the draft, keep it): User IDs stays **Required**, which it is
    anyway because of the player ID.
  - Variant B (ask first): needs a code change. The form answers don't
    change, because User IDs is required for the player ID regardless.
- **[Item 6] Apple refresh tokens.** Only if Sign in with Apple is set up for
  Android. The encrypted refresh token is then declared as part of User IDs. **[Check]** whether Google expects
  more; it has no separate type for credentials.

**Next.**

### Step 4: Preview and submit

Check the preview against the tables above, then **Submit**. Play publishes
the label with the next release.

## Apple: App Privacy

App Store Connect → Apps → Crownroad → **App Privacy** (in the sidebar).

### Step 1: Privacy policy

Next to Privacy Policy, press **Edit**:

| Field | Value |
| --- | --- |
| Privacy Policy URL | `https://crownroad.game/privacy` |
| User Privacy Choices URL (optional) | `https://crownroad.game/privacy#your-rights` |

**Save.** The game links the same policy from Settings → Account → **Privacy
policy** (`scripts/core/SiteLinks.cs`), which Apple's guideline 5.1.1(i)
also asks for.

### Step 2: Data collection

Under Data Collection press **Get Started**.

"Do you or your third-party partners collect data from this app?" → **Yes,
we collect data from this app**. **Next.**

### Step 3: Data types

Tick these, in the console's order:

| Category | Tick | Covers |
| --- | --- | --- |
| Contact Info | **Email Address** | Email sign-in, the only sign-in on iPhone and iPad that uses an address |
| Contact Info | Name: **[Check]** see below | The nickname |
| User Content | **Gameplay Content** | Cloud saves, challenge and daily results, achievements. 1.0 sends no online room data: rooms are hidden |
| Identifiers | **User ID** | Player ID, nickname, Game Center `teamPlayerID`. The iPhone and iPad offer no Apple or Google sign-in, so the iOS app collects no Apple, Google or Play Games account ID |
| Identifiers | Device ID: **[Item 4]** see below | |
| Purchases | **Purchase History** | Purchases, refund records, paid-currency ledger |
| Usage Data | **Product Interaction** | Opt-in analytics: session starts, stage starts and finishes |
| Diagnostics | **Crash Data** | Opt-in crash reports |

Left unticked: Phone Number, Physical Address, Other User Contact Info, all
Health & Fitness, all Financial Info (Payment Info, Credit Info, Other
Financial Info), Precise and Coarse Location, Sensitive Info, Contacts, Emails
or Text Messages, Photos or Videos, Audio Data, Customer Support, Other User
Content, Browsing History, Search History, Advertising Data, Other Usage Data,
Performance Data, Other Diagnostic Data, Surroundings, Body, Other Data Types.
Payment goes through the App Store.

- **Name.** Variant A (the draft): untick. The nickname is a handle, which
  Apple counts under User ID. Variant B: tick Contact Info → Name, App
  Functionality, linked, not tracking, and also set
  `privacy/collected_data/name/collected=true` in the iOS preset so the
  privacy manifest matches.
- **[Item 4] Device ID.** Variant A (the draft): untick. The game reads
  neither IDFA nor IDFV, and the player ID is treated as an account ID.
  Variant B: tick Identifiers → Device ID with the same answers as User ID,
  and add `device_id` to the iOS preset's privacy settings.
- **Customer Support** stays unticked: support works by email outside the
  app, and the app has no support form.

**Save.**

### Step 4: Set up each data type

Each ticked type now shows **Set Up**. Answer three screens for each:

1. "Indicate all purposes for which the data is used" (tick)
2. "Is [type] linked to the user's identity?"
3. "Do you or your third-party partners use [type] for tracking purposes?"

| Type | 1. Purposes | 2. Linked | 3. Tracking |
| --- | --- | --- | --- |
| Email Address | App Functionality | Yes | No |
| Gameplay Content | App Functionality | Yes | No |
| User ID | App Functionality, Analytics | Yes | No |
| Purchase History | App Functionality | Yes | No |
| Product Interaction | Analytics | Yes | No |
| Crash Data | App Functionality | Yes | No |

Apple's App Functionality purpose already covers authentication, fraud
prevention, security and reducing crashes. Every type is linked, because
every stored record carries the player ID. Nothing is used for tracking: no
ads, no advertising ID, no data brokers, and the hashed player ID goes to
Apple only to verify purchases. So the app needs no App Tracking
Transparency prompt.

Apple's labels have no "optional" field. The opt-in analytics and crash
reports are still declared, because they are collected repeatedly and don't
meet every condition for Apple's optional-disclosure exemption. **[Check]**

Item 3 never changed Apple's answers: Apple asks about collection, linking
and tracking, not sharing with other users.

### Step 5: Publish

Press **Publish** at the top right. The label shows on the App Store once
published, independent of app review.

### Privacy manifest

The iOS preset (`[preset.2.options]` in `export_presets.cfg`, set in
`22490ed6`) declares the same six types: email address,
user ID, purchase history, product interaction, gameplay content and crash
data, all linked, none for tracking. **[Item 13] [Check]** the exported
`PrivacyInfo.xcprivacy` against the table in Step 4 once, and update the
preset if you choose Variant B for Name or Device ID.

## Export compliance

### App Store

1. **Info.plist.** `ITSAppUsesNonExemptEncryption` = `NO` (false). It is set
   through the iOS preset's `application/additional_plist_content` in
   `export_presets.cfg` (`22490ed6`). With it, App Store Connect doesn't ask
   about encryption for each build, and builds don't stop at "Missing
   Compliance".
2. **If App Store Connect asks anyway** (for example a build exported without
   the key), choose the answer that means only exempt encryption:
   - Current form: "What type of encryption algorithms does your app
     implement?" → **None of the algorithms mentioned above**.
   - Older form: "Does your app use encryption?" → **Yes**, then "Does your
     app qualify for any of the exemptions provided in Category 5, Part 2 of
     the U.S. Export Administration Regulations?" → **Yes**.
   - **[Check]** which form the console shows.
3. **No documents.** App Store Connect asks for no encryption
   documentation or French encryption declaration when the app uses only
   exempt encryption. **[Legal]** if you want the export classification
   confirmed.

Why the app is exempt, from the code:

- The game reaches the server with .NET `HttpClient` over HTTPS. On iOS, .NET
  uses the system's TLS. No game script uses
  `System.Security.Cryptography`, `FileAccess.OpenEncrypted` or Godot's
  `Crypto` class (searched in `scripts/`).
- The StoreKit plugin uses CryptoKit's SHA-256 only to derive the purchase's
  app account token from the player ID (`native/StoreKit/src/AppAccountToken.swift`).
  Hashing isn't encryption.
- LAN races use ENet without DTLS (`LanChallengeService.cs`).
- The game pack isn't encrypted: every preset's `encryption_include_filters`
  is empty.
- The server's AES-GCM encryption of Apple refresh tokens
  (`server/CredentialProtection.cs`) runs on our server, not in the app.

Ask again if a later version encrypts saves or the game pack, adds DTLS or
end-to-end encryption, or bundles its own crypto library.

### Google Play

Play has no encryption questionnaire. **[Check]** if the console asks you to
confirm compliance with US export laws when you set countries; the same
reasoning applies.

## The review list, item by item

Status of each item in "What you must review" as of 2026-10-28, and what it
changes in the forms above:

| Item | Status | Changes in the forms |
| --- | --- | --- |
| 1. How long purchase records are kept **[Legal]** | Open | Nothing in either form. Retention goes in the privacy policy only |
| 2. Automatic Play Games sign-in **[Legal]** | Open | Nothing: User IDs is required either way (Data safety step 3) |
| 3. Public player IDs | Settled in the working tree: anonymous leaderboard tags (`LeaderboardTag`), rooms hidden (`LaunchFeatures.InternetRooms`) | Data safety: Name and User IDs collected, not shared (step 3). Apple: no change |
| 4. User IDs or device IDs **[Legal]** | Open | Data safety: Device or other IDs ticked or not (step 2). Apple: Device ID ticked or not (step 3), and the privacy manifest |
| 5. Children and age rating **[Legal]** | Open | Not the forms. Play's target audience (see [RATINGS.md](RATINGS.md)). Choosing an age group under 13 would change the Data safety answers, the policy and the app |
| 6. Account deletion | Built. Sign in with Apple is now Android-only and optional, so the iOS app collects no Apple refresh token. **[Check]** whether Google wants the token declared separately if you set it up | Data safety: delete account URL (step 1). Declared under User IDs |
| 7. Session records | Done (`dacc1ebb`): records go a week after they expire or are signed out (`SessionAuth.PurgeEndedSessions`) | None. Neither form asks about retention |
| 8. Name the email provider | Open | None. A service provider acting for you isn't sharing. The policy must name it |
| 9. Unused server endpoints | Done (`a7e1362b`) | Nothing to declare |
| 10. Refunds | Built | Covered by Purchase history |
| 11. Finding the player ID | Done (`dacc1ebb`): Settings → Account → Player ID | None |
| 12. SDK disclosures **[Check]** | Open | May add Data safety types (step 2) |
| 13. iOS privacy manifest | Set (`22490ed6`). **[Check]** the exported file | Must match Apple step 4 |
| 14. Publishing | Open: `effective_date` in `site/site.json` is empty | The privacy policy URL in both forms, and the game's **Privacy policy** row, must reach a live page before submission |
