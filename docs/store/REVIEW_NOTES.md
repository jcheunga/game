# Review notes and app access

Paste-ready notes for Apple's App Review and Google Play's App access
declaration, written on 2026-10-28 from the UI code in `scripts/ui` and
`scripts/core`, and updated the same day for the fixed Adventurer's Kit troop,
the website links in Settings → Account, the new deletion dialog text and the
1.0 launch decisions (internet rooms and the premium season track hidden,
Game Center and email as the iPhone's sign-in options), all uncommitted at the
time. Labels are copied from the code. Some buttons draw their label in
capitals (for example the Settings row buttons), but the code spells them in
sentence case, as below.

Counts are measured by the script in [LISTING.md](LISTING.md#how-the-counts-were-measured).

## Is a demo account needed?

No. Every feature works as a guest, and the game never asks for an account.
Sign-in only links progress to an account and backs it up. In App Store
Connect → App Review Information, leave **Sign-in required** unticked. In Play
Console → App access, choose the option that says all functionality is
available without special access.

The server must be live, on the release `crownroad/network/api_base_url`,
for the whole review. Purchases, account deletion and sign-in all need it.

## Paths used below

Checked against the code:

| What | Path in the game | Code |
| --- | --- | --- |
| First launch | "Choose a nickname" panel: type a name or tap **Suggest**, then **Confirm** | `NicknameDialog.cs` |
| Settings | The gear ring at the top right of the home map (accessible name "Settings") | `MapMenu.Home.cs` |
| Settings tabs | **Sound**, **Gameplay**, **Online**, **Account** | `SettingsMenu.cs` |
| Store | Tap the gold amount at the top right of the home map ("Royal storehouse"), or the food amount ("Refill food"), or **More** → **Caravan** → **Store** | `MapMenu.Home.cs`, `CaravanHub.cs` |
| Store tabs | **Gold**, **Rations**, **Bundles**, **Purchase info** | `CashShopMenu.cs` |
| Buying | One tap on **Buy — $0.99** opens the App Store's or Google Play's own payment sheet, which is the confirmation | `StorePackCard.cs`, `CashShopMenu.cs` |
| Account panel | Settings → **Account** tab → **Account** row → **Manage** | `SettingsMenu.cs`, `AccountDialog.cs` |
| Sign-in buttons | iPhone and iPad: **Continue with Game Center**, then the **Email address** field, **Send sign-in code**, the **6-digit code** field and **Sign in**. Android: **Continue with Play Games**, the email fields, and **Continue with Apple** and **Continue with Google** only when the server reports them configured; otherwise they are left out, not greyed out. Desktop: the same, without Play Games. The panel's own button is **Play as guest** for a guest and **Close** once signed in. Signed-in players also see **Sign out** | `AccountDialog.cs`, `SignInButton.cs` |
| Account deletion | Settings → **Account** tab → **Delete account** row → **Delete**. The dialog "Delete your account?" says it "erases your progress, cloud save, scores and currency balance from our servers", and offers **Delete account** and **Keep account** | `SettingsMenu.cs` |
| Player ID | Settings → **Account** tab → **Player ID** row (tap to copy). It also shows under **More** → **Community** → **Friends** ("Profile ID") | `SettingsMenu.cs`, `FriendsMenu.cs` |
| Privacy policy, terms, support | Settings → **Account** tab → **Privacy policy**, **Terms of use** and **Help and support** rows, each with **Open**. They open `https://crownroad.game/privacy`, `/terms` and `/support` in the browser | `SettingsMenu.cs`, `SiteLinks.cs` |
| Adventurer's Kit | **Bundles** tab. The card names the troop it recruits ("Recruits Militia" on a fresh save) and carries a "Once per account" ribbon. After buying, the shop says "+ Militia recruited" | `StorePackCard.cs`, `CashShopMenu.cs` |
| A battle | Tap a site on the map → **Prepare battle** → **Deploy** (2 food) | `MapMenu.Home.cs`, `LoadoutMenu.cs` |
| Challenges | **More** → **Community** → **Challenges**. Tabs **Records**, **Daily**, **Featured**, **Saved**, **Squad**. Footer **Back**, **Refresh**, **New code**, **Copy code**, **Start challenge**. No online rooms in 1.0 (`LaunchFeatures.InternetRooms`). Leaderboards show an anonymous tag per board ("Player" and six hex characters) and "You" for the player | `MultiplayerMenu.Royal.cs`, `LeaderboardTag.cs` |
| Season pass | **More** → **Caravan** → **Season**. Only the free track shows, with no Unlock button (`LaunchFeatures.SeasonPremiumTrack`) | `SeasonPassMenu.cs` |

## App Store: App Review notes

Paste into App Store Connect → the version → App Review Information → Notes.

<!-- field: review.apple-notes | limit: 4000 -->
**Notes** · 2677 / 4000 characters
```text
Crownroad: Siege of Ash is a landscape lane-battle strategy game for iPhone and iPad.

NO SIGN-IN NEEDED
Every feature works as a guest, so no demo account is needed. On first launch the game asks for a nickname: type one or tap Suggest, then Confirm. The home map opens on the first zone.

TO PLAY A BATTLE
Tap a site on the map, then Prepare battle, then Deploy. In battle, tap a troop card to send troops out of the war wagon, and drag a spell card onto the field. Destroying the enemy stronghold on the right wins the stage.

IN-APP PURCHASES
Open the Royal storehouse by tapping the gold amount at the top right of the home map, or from More > Caravan > Store. Packs are on the Gold, Rations and Bundles tabs. Tap Buy, then Confirm, then approve the App Store sheet. Our server checks each transaction with Apple before it adds the gold or food, and only then finishes the transaction. The Purchase info tab shows the result. The Adventurer's Kit can be bought once per account. It recruits a fixed troop, which its card names before you buy, so nothing sold is random.

ACCOUNT DELETION
Open Settings (the gear ring at the top right of the home map), choose the Account tab, then tap Delete on the Delete account row and confirm with Delete account. This works for guests and signed-in players. It deletes the account and its data from our server straight away, and starts the device again as a new guest. We keep only the purchase records the law requires, under a random code. Players who can't open the game can ask for deletion at https://crownroad.game/support#delete-data.

SIGN-IN (OPTIONAL)
Settings > Account tab > Account > Manage opens the Account panel. On iPhone and iPad the options are:
- Continue with Game Center, using the device's Game Center player.
- Email: type an address, tap Send sign-in code, then enter the 6-digit code and tap Sign in.
Play as guest closes the panel. Signing in keeps the guest's progress and backs it up. Sign out is in the same panel. The app offers no third-party or social login on iPhone and iPad.

PRIVACY
There are no ads and no tracking. Analytics and crash reports stay off until the player turns them on under Settings > Account. The privacy policy, terms of use and support page open from the Privacy policy, Terms of use and Help and support rows under Settings > Account. The player ID is shown there too. Players can't see each other's nicknames or player IDs: challenge leaderboards show an anonymous tag per board, and this version has no online rooms or chat.

ENCRYPTION
The app uses only HTTPS through the encryption built into iOS, and declares ITSAppUsesNonExemptEncryption = NO.

Contact: [support email]
```

Other App Review Information fields:

| Field | Value |
| --- | --- |
| Sign-in required | No |
| Contact first and last name | `[contact name]` |
| Contact phone | `[contact phone]` |
| Contact email | `[support email]` |
| Attachment | Optional: a short screen recording of a purchase and of account deletion |

## Google Play: App access

Play Console → App content → App access.

**Recommended answer:** all functionality is available without special access
(**[Check]** the console's exact wording). A guest can use every feature, and
the reviewer needs no account, password or code. Play Games may sign the
reviewer's device in by itself at launch. It only links a guest when no
progress is at risk (`PlatformSignIn.TryAutoSignIn`), so it doesn't block
anything.

If you choose "All or some functionality is restricted" instead, add one
instruction with no username or password, and this text in "Any other
information required to access your app":

<!-- field: review.play-access | limit: 500 -->
**Instructions** · 404 / 500 characters
```text
No account is needed. Every feature works as a guest. Optional sign-in is under Settings (gear at the top right of the home map) > Account > Account > Manage: Continue with Play Games or an email code, plus Continue with Google when offered. Purchases: tap the gold amount at the top right of the home map. Account deletion: Settings > Account > Delete account > Delete, then confirm with Delete account.
```

**[Check]** the field's limit in the console. 500 characters is assumed.

## Where the sign-in options come from

| Option | Platforms | How it works | Code |
| --- | --- | --- | --- |
| Guest | All | A player ID (`CVY-…`) is made on the device and registered with its nickname when the server answers | `AccountSignIn.cs`, `GameState.cs` |
| Game Center | iPhone, iPad | Game Center's identity signature, checked by the server | `PlatformSignIn.cs`, `server/AccountAuth.cs` |
| Google Play Games | Android | A Play Games server auth code. Also signs in by itself at launch when no progress is at risk | `PlatformSignIn.cs` |
| Sign in with Apple | Android and desktop, only when the server has it configured. Optional for 1.0 (`docs/LAUNCH_CHECKLIST.md`, section 8). Not on iPhone or iPad | Browser sign-in; no name or email requested | `AccountDialog.cs`, `server/AccountAuth.cs` |
| Google | Android and desktop, only when the server has it configured. Not on iPhone or iPad | Browser sign-in with `openid email`; only the account ID is kept | `AccountDialog.cs`, `server/AccountAuth.cs` |
| Email code | All | Six-digit code, 10 minutes, five attempts | `AccountDialog.cs`, `server/AccountAuth.cs` |

The browser sign-ins are left out entirely when the server doesn't offer
them, and are never shown on iPhone or iPad, because they would leave the game
for Safari (`AccountDialog.BrowserSignIn`). The Game Center, Play Games and
email controls stay disabled until the server reports them configured
(`/auth/providers`). Before review, check that
`https://<api domain>/auth/providers` reports Game Center and email as
available, plus Play Games and Google for the Android review.

Guideline 4.8 asks for Sign in with Apple only where an app offers a
third-party or social login. On iPhone and iPad the game offers Game Center,
which is Apple's, and its own email code, so it doesn't apply there.
**[Check]** with the current guideline text.

## Findings that could fail review

1. **The Game Center icon is still missing** from its sign-in button
   (`docs/LAUNCH_CHECKLIST.md`, section 13). The button shows an empty logo
   box until the project owner adds Apple's artwork from Apple Design
   Resources. Apple's Sign in with Apple logo is no longer needed for the iOS
   submission, because the iPhone no longer shows that button.

Resolved on 2026-10-28, in the working tree:

- Guideline 5.1.1(i) is met in the app, through the **Privacy policy** row
  under Settings → Account.
- The Adventurer's Kit's troop is fixed and named on its card.
- The deletion dialog no longer mentions guilds and friends.
- Apple and Google sign-in no longer leave the game for Safari on iPhone: the
  iPhone offers Game Center, email and guest play only.
- The season pass's premium track and its Unlock button are hidden
  (`LaunchFeatures.SeasonPremiumTrack`).
- Internet rooms are hidden (`LaunchFeatures.InternetRooms`). The Multiplayer
  screen shows Records, **New code** and **Copy code** instead.
- `README.md` now says sign-in is under Settings → Account.
