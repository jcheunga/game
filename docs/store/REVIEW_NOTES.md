# Review notes and app access

Paste-ready notes for Apple's App Review and Google Play's App access
declaration, written on 2026-10-28 from the UI code in `scripts/ui` and
`scripts/core`, and updated the same day for the fixed Adventurer's Kit troop,
the website links in Settings → Account and the new deletion dialog text
(uncommitted at the time). Labels are copied from the code. Some buttons draw their label
in capitals (for example the Settings row buttons), but the code spells them
in sentence case, as below.

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
| Buying | **Buy — $0.99** becomes **Confirm — $0.99**; **Cancel** backs out. The hint reads "Tap Buy twice to confirm." | `StorePackCard.cs`, `CashShopMenu.cs` |
| Account panel | Settings → **Account** tab → **Account** row → **Manage** | `SettingsMenu.cs`, `AccountDialog.cs` |
| Sign-in buttons | **Continue with Game Center** (iOS) or **Continue with Play Games** (Android), **Continue with Apple**, **Continue with Google**, then the **Email address** field, **Send sign-in code**, the **6-digit code** field and **Sign in**. The panel's own button is **Play as guest** for a guest and **Close** once signed in. Signed-in players also see **Sign out** | `AccountDialog.cs`, `SignInButton.cs` |
| Account deletion | Settings → **Account** tab → **Delete account** row → **Delete**. The dialog "Delete your account?" says it "erases your progress, cloud save, scores and currency balance from our servers", and offers **Delete account** and **Keep account** | `SettingsMenu.cs` |
| Player ID | Settings → **Account** tab → **Player ID** row (tap to copy). It also shows under **More** → **Community** → **Friends** ("Profile ID") | `SettingsMenu.cs`, `FriendsMenu.cs` |
| Privacy policy, terms, support | Settings → **Account** tab → **Privacy policy**, **Terms of use** and **Help and support** rows, each with **Open**. They open `https://crownroad.game/privacy`, `/terms` and `/support` in the browser | `SettingsMenu.cs`, `SiteLinks.cs` |
| Adventurer's Kit | **Bundles** tab. The card names the troop it recruits ("Recruits Militia" on a fresh save) and carries a "Once per account" ribbon. After buying, the shop says "+ Militia recruited" | `StorePackCard.cs`, `CashShopMenu.cs` |
| A battle | Tap a site on the map → **Prepare battle** → **Deploy** (2 food) | `MapMenu.Home.cs`, `LoadoutMenu.cs` |

## App Store: App Review notes

Paste into App Store Connect → the version → App Review Information → Notes.
Add the optional paragraph after it only if it is true for the build.

<!-- field: review.apple-notes | limit: 4000 -->
**Notes** · 2695 / 4000 characters
```text
Crownroad: Siege of Ash is a landscape lane-battle strategy game for iPhone and iPad.

NO SIGN-IN NEEDED
Every feature works as a guest, so no demo account is needed. On first launch the game asks for a nickname: type one or tap Suggest, then Confirm. The home map opens on the first zone.

TO PLAY A BATTLE
Tap a site on the map, then Prepare battle, then Deploy. In battle, tap a troop card to send troops out of the war wagon, and drag a spell card onto the field. Destroying the enemy stronghold on the right wins the stage.

IN-APP PURCHASES
Open the Royal storehouse by tapping the gold amount at the top right of the home map, or from More > Caravan > Store. Packs are on the Gold, Rations and Bundles tabs. Tap Buy, then Confirm, then approve the App Store sheet. Our server checks each transaction with Apple before it adds the gold or food, and only then finishes the transaction. The Purchase info tab shows the result. The Adventurer's Kit can be bought once per account. It recruits a fixed troop, which its card names before you buy, so nothing sold is random.

ACCOUNT DELETION
Open Settings (the gear ring at the top right of the home map), choose the Account tab, then tap Delete on the Delete account row and confirm with Delete account. This works for guests and signed-in players. It deletes the account and its data from our server straight away, revokes Sign in with Apple, and starts the device again as a new guest. We keep only the purchase records the law requires, under a random code. Players who can't open the game can ask for deletion at https://crownroad.game/support#delete-data.

SIGN-IN (OPTIONAL)
Settings > Account tab > Account > Manage opens the Account panel. The options are:
- Continue with Game Center, using the device's Game Center player.
- Continue with Apple. We ask Apple for no name or email.
- Continue with Google.
- Email: type an address, tap Send sign-in code, then enter the 6-digit code and tap Sign in.
Continue with Apple and Continue with Google open the provider's page in the browser. After signing in, return to the game, which finishes on its own. Play as guest closes the panel. Signing in keeps the guest's progress and backs it up. Sign out is in the same panel.

PRIVACY
There are no ads and no tracking. Analytics and crash reports stay off until the player turns them on under Settings > Account. The privacy policy, terms of use and support page open from the Privacy policy, Terms of use and Help and support rows under Settings > Account. The player ID is shown there too.

ENCRYPTION
The app uses only HTTPS through the encryption built into iOS, and declares ITSAppUsesNonExemptEncryption = NO.

Contact: [support email]
```

Optional paragraph, only if the multiplayer screen's Rooms board is visible
in the build (see the findings below):

<!-- field: review.apple-notes-rooms | limit: 4000 -->
**Rooms paragraph** · 364 / 4000 characters
```text
ONLINE ROOMS
More > Community > Challenges opens the challenge boards. The Rooms board lists online rooms that other players host. A room shows each player's nickname and score. There is no chat. A player in a room can report another with Submit report (reasons include Offensive name). The rest of the game, including the daily and featured boards, needs no room.
```

Both together come to 3061 characters with the blank line between them,
inside Apple's 4,000.

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
**Instructions** · 408 / 500 characters
```text
No account is needed. Every feature works as a guest. Optional sign-in is under Settings (gear at the top right of the home map) > Account > Account > Manage: Continue with Play Games, Continue with Apple, Continue with Google, or an email code. Purchases: tap the gold amount at the top right of the home map. Account deletion: Settings > Account > Delete account > Delete, then confirm with Delete account.
```

**[Check]** the field's limit in the console. 500 characters is assumed.

## Where the sign-in options come from

| Option | Platforms | How it works | Code |
| --- | --- | --- | --- |
| Guest | All | A player ID (`CVY-…`) is made on the device and registered with its nickname when the server answers | `AccountSignIn.cs`, `GameState.cs` |
| Game Center | iOS | Game Center's identity signature, checked by the server | `PlatformSignIn.cs`, `server/AccountAuth.cs` |
| Google Play Games | Android | A Play Games server auth code. Also signs in by itself at launch when no progress is at risk | `PlatformSignIn.cs` |
| Sign in with Apple | All | Browser sign-in; no name or email requested | `AccountDialog.cs`, `server/AccountAuth.cs` |
| Google | All | Browser sign-in with `openid email`; only the account ID is kept | `AccountDialog.cs`, `server/AccountAuth.cs` |
| Email code | All | Six-digit code, 10 minutes, five attempts | `AccountDialog.cs`, `server/AccountAuth.cs` |

A button stays disabled until the server reports its provider as configured
(`/auth/providers`). Before review, check that `https://<api domain>/auth/providers`
reports every method as available.

## Findings that could fail review

1. **Sign in with Apple and Google leave the app for Safari.**
   `AccountDialog.Browser` calls `OS.ShellOpen`, which opens the system
   browser. The server's page then says "Return to Crownroad to continue", so
   the player has to switch back by hand. App Review has rejected this
   pattern under guideline 4 (Design) and asks for `ASWebAuthenticationSession`
   or, for Apple, the native Sign in with Apple sheet. Consider switching
   before submission.
2. **Apple's logo and the Game Center icon are still missing** from their
   sign-in buttons (`docs/LAUNCH_CHECKLIST.md`, section 13). The buttons show
   an empty logo box until the project owner adds Apple's artwork.
3. **The season pass's Unlock premium button opens a store that sells no
   pass** (`SeasonPassMenu.cs`). A reviewer may treat it as an incomplete
   purchase flow. Hide it for launch.
4. **Rooms are visible.** If internet rooms stay in closed testing, hide the
   Rooms board, **Match** and **Host** in the release build. As it is, the
   multiplayer screen opens on that board, and since the room list now reads
   the server's answer correctly, a reviewer will see real rooms there.
5. **One doc still points to the wrong place.** `README.md` says `More` holds
   `Account`, `Player profile` and `Quit game`. The Account panel opens only
   from Settings → Account → **Manage**. (`docs/DEPLOYMENT.md` is fixed.)

Resolved on 2026-10-28, in the working tree: guideline 5.1.1(i) is now met in
the app, through the **Privacy policy** row under Settings → Account; the
Adventurer's Kit's troop is fixed and named on its card; and the deletion
dialog no longer mentions guilds and friends.
