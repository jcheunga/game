# Sign-in buttons

The Account dialog's Google, Apple, Play Games and Game Center buttons follow their companies' published button
rules instead of the game's royal style. `scripts/ui/shared/SignInButton.cs` draws them;
`UiReviewSmoke --account [--mobile-preview]` measures them against the rules below and captures them in the dialog,
held down, and on dark, parchment and white backgrounds (`artifacts/account-phone`).

Rules read on 2026-10-23.

## Sources and licences

| Asset in the game | Source | Licence and terms |
| --- | --- | --- |
| `assets/ui/signin/google-g.png` | Google's pre-approved button bundle, [signin-assets.zip](https://developers.google.com/static/identity/images/signin-assets.zip) (zip SHA-256 `ba884069…c1da1`, last modified 2026-06-29), from the [Sign in with Google branding guidelines](https://developers.google.com/identity/branding-guidelines#download-brand-icons). Cut from `iOS/PNG @4x/Light/Theme=Light, Show text=No, Shape=Square, Platform=iOS@4x.png` around its 20 pt logo box, with 1 pt of the white fill kept round it. | Google's brand features may be used only as the branding guidelines describe, without further approval; anything else needs Google's written consent ([Guidelines for Third Party Use of Google Brand Features](https://about.google/intl/en/brand-resource-center/), where the guidelines link). The guidelines say to start any custom-size G from the bundle's sizes. |
| `assets/ui/signin/play-games-controller.png` | Google Play Games Services [games-branding-icons.zip](https://developer.android.com/static/games/pgs/download-files/games-branding-icons.zip) (SHA-256 `a5ffc902…a9ce`), `Individual_Badges_PNG/games_controller.png`, unchanged, from the [Play Games Services downloads](https://developer.android.com/games/pgs/downloads#graphics). | Usable without pre-approval when the [Play Games Services branding guidelines](https://developer.android.com/games/pgs/branding) are followed; other uses need Google's written consent. Colour, texture and size may change to suit the game. |
| `assets/fonts/GoogleSans-Medium.ttf`, `GoogleSans-OFL.txt` | Google Sans from [google/fonts](https://github.com/google/fonts/tree/main/ofl/googlesans) (`GoogleSans[GRAD,opsz,wght].ttf`, version 14.000), set to Medium (wght 500, opsz 17, GRAD 0) and cut to Latin text by `build.py`. | SIL Open Font License 1.1 with no Reserved Font Name. "Google" and "Google Sans" are Google trademarks and may not suggest Google endorses the game (the font's TRADEMARKS.md). |
| `assets/ui/signin/apple-logo.svg` (not added yet) | Apple Design Resources, [Logo-Sign-in-with-Apple.dmg](https://devimages-cdn.apple.com/design/resources/download/Logo-Sign-in-with-Apple.dmg): the black logo file for buttons with text, SVG. | Apple Developer Artwork License Agreement for Sign in with Apple (below). |
| `assets/ui/signin/game-center.png` (not added yet) | Apple Design Resources, [Icon-Game-Center.dmg](https://devimages-cdn.apple.com/design/resources/download/Icon-Game-Center.dmg): the Game Center icon, PNG. | Apple Developer Artwork License Agreement for Game Center (below). |

Rebuild Google's art and the font with `build.py` (Pillow and fontTools; the usage lines are at the top of the
file). Keep the downloads out of the repository.

## Adding Apple's art

Until Apple's two files are added, the Apple and Game Center buttons keep their size, title and action with an
empty logo box. Apple forbids any home-made Apple logo, so there is no stand-in. `UiReviewSmoke --account` prints
a `PENDING` line for each missing file, and both must be in before an App Store submission.

1. Join the Apple Developer Program (the licences cover members only).
2. From [Apple Design Resources](https://developer.apple.com/design/resources/), download **Sign in with Apple Logo**
   (`Logo-Sign-in-with-Apple.dmg`, PNG, PDF and SVG) and **Games and Game Center** (`Icon-Game-Center.dmg`), open
   each and accept its licence.
3. Choose two files:
   - Sign in with Apple: the **logo-and-text** artwork (not logo-only), **black** (the button is white),
     **SVG** (Apple allows its PNGs only in buttons exactly 44 pt tall). It comes in small, medium and large; take
     the size whose logo is closest to the Google G, which fills 45 to 50% of the button's height. The review
     prints both (`APPLE_LOGO fills …`), so try one and switch if it looks off.
   - Game Center: the Game Center icon as a **PNG**, the largest size in the bundle.
4. From the repository root, wherever the two files are:

   ```bash
   python3 art/signin/build.py --apple-logo "/path/to/the Apple logo file.svg" --game-center "/path/to/the Game Center icon.png"
   ```

   It copies them unchanged to `assets/ui/signin/apple-logo.svg` and `game-center.png`, sets the SVG to import at
   4x (the button is 80 px tall) and reimports the project. It refuses a square SVG (the logo-only file) or a
   non-PNG icon.
5. Check with `godot --path . scenes/tests/UiReviewSmoke.tscn -- --save-suffix=ui-review-account --account --mobile-preview`:
   it must report 0 failures and no `PENDING` lines, and the captures in `artifacts/account-phone` show the logos.

## Google: Sign in with Google

From the [branding guidelines](https://developers.google.com/identity/branding-guidelines), "Create a custom Sign in
with Google button". Google's own SDK button cannot be used: Godot draws the whole UI, so the game builds a custom
button, which the guidelines allow when it follows these rules.

| Rule | How the button meets it |
| --- | --- |
| Scale as needed, but keep the aspect ratio so the G is not stretched. | Every measure is Google's spec times one factor (`SignInButton.Unit`), so the 80 px button is the spec scaled evenly. The G is drawn square (checked). |
| Spec sizes: Android and web 40 tall, 12 before the G, 10 after, 12 after the text; iOS 44 tall, 16, 12 and 16. G 20 × 20, corners 4 (rectangle) or a pill. | The iOS build uses the iOS spec, the others the Android spec. 80 px is 44 pt on a 390 pt phone; the G is 20 of the spec's 40 or 44 (40 or 36 px), corners 4. |
| Text "Sign in with Google", "Sign up with Google" or "Continue with Google"; it must be clear the player signs in to this game with a Google account. | "Continue with Google". |
| Light theme: fill #FFFFFF, 1 px #747775 stroke inside, text #1F1F1F Google Sans Medium 14/20. (Dark: #131314, #8E918F, #E3E3E3. Neutral: #F2F2F2, no stroke, #1F1F1F.) | Light theme exactly: white fill, a 1 pt #747775 rim inside the edge, #1F1F1F Google Sans Medium at the spec's 14 (`GoogleSans-Medium.ttf`). |
| The G must be the standard full-colour logo on a white background, at its fixed size and padding; never monochrome, recoloured, outdated or home-made. | The G is cut, unchanged, from Google's 2026 bundle and sits on the white fill. A held press darkens the whole face with ButtonFeel, as a Material press state does; a disabled button fades it with the face. |
| Don't use the G alone without the button boundary and text, or "Google" alone as the label. | The G always sits in the full button with its title. |
| Choose the colour mode for accessibility and equal prominence. | White reads clearly on the dark dialog and keeps its grey rim on parchment or white (`sign-in-buttons-*.png`). |
| Show Sign in with Google at least as prominently as other third-party sign-ins: about the same size and visual weight. | Every sign-in button is the same size and face (checked). |

## Apple: Sign in with Apple

From the [Human Interface Guidelines](https://developer.apple.com/design/human-interface-guidelines/sign-in-with-apple),
"Creating a custom Sign in with Apple button" (custom buttons are allowed on iOS, macOS and the web, for example to
line up logos across several sign-in buttons or to use the app's own font; App Review checks every custom button).
The game cannot use `ASAuthorizationAppleIDButton` because Godot draws its own UI, and it shows the same button on
Android, where Apple's licence allows versions of the app with the same title.

| Rule | How the button meets it |
| --- | --- |
| People must recognise it instantly as Sign in with Apple. | Apple's white style: white face, black Apple logo, black title. |
| Use only Apple's downloadable logo files, never a custom Apple logo; don't crop the file, don't add vertical padding, and match its height to the button's. | Only Apple's file is drawn, as tall as the button from its top edge, whole and unpadded (checked once the file is added). Until then the logo box stays empty. |
| Titles only "Sign in with Apple", "Sign up with Apple" or "Continue with Apple"; keep the capitalisation unless the UI is all capitals. | "Continue with Apple", in the default capitalisation. |
| Logo-and-text buttons are rectangular. | A rectangle with 4 pt corners (Apple allows any corner radius that matches the app's other buttons). |
| Logo and title both black or both white; the background stays black or white. | Black logo and title on a white face. |
| Any title font, weight and size, but the title is 43% of the button's height. | Google Sans Medium at 34 px on the 80 px button (checked). |
| Optional bezel stroke or shadow. | The same 1 pt #747775 rim as the other sign-in buttons, so the white button keeps its edge on light backgrounds too. |
| Inset the logo from the leading edge only to align it with other sign-in logos. | The file is moved only to centre the Apple logo on the G's column (checked to 1 px); its own trailing padding stays clear of the title (checked). |
| At least 8% of the button's width between the title and the trailing edge. | Checked by the review. |
| At least 140 × 30 pt, with a clear margin of a tenth of the height around it. | 80 px tall (44 pt on a phone) and as wide as the dialog (about 257 pt); nothing comes within 12 px (checked). |
| No smaller than other sign-in buttons, and visible without scrolling. | Same size as every sign-in button; the dialog is tall enough that Apple and Google show without scrolling (checked). Apple comes before Google, after the phone's own game account. |
| White style on dark backgrounds; black or white-with-outline on light ones. | The dialog is dark slate, so white. On light backgrounds the grey rim gives it an edge (`sign-in-buttons-parchment.png`, `-white.png`). |

## Google Play Games

From the [Play Games Services branding guidelines](https://developer.android.com/games/pgs/branding). They cover
Play Games features; for sign-in they point to the Sign in with Google guidelines, so the button shares Google's
light face.

| Rule | How the button meets it |
| --- | --- |
| Use the Google Play game controller icon for Play Games entry points. | Google's controller badge, in its own green, unchanged. |
| Icons may change colour, texture and size; never obscure them, put them on busy backgrounds, or let them touch other icons. | Drawn at the G's size on the plain white face with Google's padding. |
| Name it "Google Play", "Google Play Games" or "Play Games", nominatively. | "Continue with Play Games". |
| Don't use Google marks in the game's title or icon. | Untouched: the game's name and icon carry no Google marks. |

## Game Center

From the [Human Interface Guidelines](https://developer.apple.com/design/human-interface-guidelines/game-center),
"Using custom UI".

| Rule | How the button meets it |
| --- | --- |
| Use Game Center's official artwork from Apple Design Resources in custom UI; keep its appearance, dimensions and effects. | Only Apple's icon file, drawn whole and square in the 20 pt logo box. Until it is added the box stays empty. |
| Spell it "Game Center" (not GameCenter, GameKit or game center). | "Continue with Game Center". |

## Apple's artwork licences

Both Apple downloads, [Logo-Sign-in-with-Apple.dmg](https://devimages-cdn.apple.com/design/resources/download/Logo-Sign-in-with-Apple.dmg)
and [Icon-Game-Center.dmg](https://devimages-cdn.apple.com/design/resources/download/Icon-Game-Center.dmg) from
[Apple Design Resources](https://developer.apple.com/design/resources/), open with an Apple Developer Artwork License
Agreement, accepted by using the artwork; the project owner accepts it, not a tool. Its main terms: the artwork may
be used only in the app's own user interface (and its screenshots) to show that it supports Sign in with Apple or
Game Center, in apps on Apple's platforms and in other versions with the same title and features; only by Apple
Developer Program members; never modified, used on its own, redistributed or repackaged; and the right ends if the
membership ends.
