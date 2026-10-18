# StoreKit iOS plugin

In-app purchases on iOS with StoreKit 2, for `scripts/core/NativeIAPService.cs`.
The server checks each transaction with the App Store Server API
(`VerifyAppleAsync` in `server/StorePurchaseVerification.cs`); this plugin buys,
hands the transaction to the game, and finishes it once the server has recorded
the grant.

StoreKit 2 is Swift-only, so `src/StoreKitBridge.swift` does the store work and
`src/godot_store_kit.mm` registers it as the `StoreKit` engine singleton. A
public Godot StoreKit wrapper was not used because it finishes transactions
before the game can validate them, which loses a paid purchase if validation
fails or the app closes.

## API

| Method | What it does |
|---|---|
| `configure(profile_id) -> bool` | Ties purchases to a player. Every purchase carries the `appAccountToken` the server derives from the same profile ID (`src/AppAccountToken.swift`). Returns false, and purchases stay disabled, for a blank ID. |
| `request_products(product_ids: Array)` | Loads products with `Product.products(for:)`. |
| `purchase(product_id)` | Buys with `product.purchase(options:)` (`confirmIn:` the game's view controller on iOS 18.2 and later). |
| `finish_transaction(transaction_id) -> bool` | Finishes a transaction. Call it only after the server answered `ok` or `already_recorded`. |
| `check_unfinished()` | Starts listening to `Transaction.updates` (once), and hands over every transaction in `Transaction.unfinished` again. |

| Signal | Arguments |
|---|---|
| `products_received` | `products` (dictionaries with `product_id`, `localized_title`, `localized_description`, `localized_price`, `price_micros`, `currency_code`, `type`) and `invalid_product_ids` |
| `products_failed` | `message` |
| `purchase_completed` | a dictionary: `product_id`, `transaction_id`, `original_transaction_id`, `receipt` (the signed transaction, JWS), `app_account_token`, `purchase_date`, `environment` (`Xcode`, `Sandbox` or `Production`) and `restored` |
| `purchase_failed` | `product_id`, `reason` and `message`. `reason` is `cancelled`, `pending` (Ask to Buy, or a purchase waiting on the bank), `not_configured`, `busy`, `unknown_product`, `unverified`, `not_allowed`, `network` or `error`. Nothing is granted. |
| `transaction_finished` | `transaction_id` |

`restored` is false for the answer to `purchase`, and true for a transaction
found at launch, approved later through Ask to Buy, or bought outside the game.
The game validates those through `ReconcileRecoveredPurchase`, the same path as
Google Play's recovered purchases. Only verified transactions are handed over.
A transaction Apple has revoked is finished without a grant. Signals are emitted
on the main thread, deferred into the game's frame.

## The account token

The server rejects a transaction whose `appAccountToken` is not
`ProfileAppAccountToken(profileId)`: the first 16 bytes of SHA-256 of the
trimmed profile ID, with the version nibble set to 5 and the RFC 4122 variant
bits, formatted by .NET's `new Guid(bytes).ToString()`. That constructor reads
the first three fields little-endian, so the Swift code reverses those bytes
before making the `UUID`. `test/run_tests.sh` checks the Swift code against
`server/testdata/apple_app_account_tokens.json`, and the server tests check
`ProfileAppAccountToken` against the same file. `build.sh` runs the Swift check
first and stops if it fails.

## Building

```bash
native/StoreKit/build.sh
```

Like `native/GameCenter`, the script needs Xcode, git and python3. It compiles
the Swift bridge with `swiftc`, which also writes the Objective-C header the
glue imports, compiles the glue against the headers of the Godot version the
game exports with (`GODOT_TAG`, currently `4.6.1-stable`), and installs debug
and release xcframeworks (arm64 device, arm64 and x86_64 simulator) with
`storekit.gdip` in `ios/plugins/storekit`. It reuses the Godot checkout in
`native/GameCenter/build` when there is one. Godot's iOS export template
already contains a Swift file, so the exported Xcode project links the Swift
runtime the bridge needs. The iOS export preset enables the plugin
(`plugins/StoreKit`).

**Rebuild the plugin whenever the project moves to another Godot version**,
after updating `GODOT_TAG`.

## Testing purchases locally

`Crownroad.storekit` lists the ten products of `data/shop_products.json`, all
consumables, with their US prices. Run `make_storekit_config.py` again after
changing the catalog. With it, Xcode serves the products itself, so purchases
work in a simulator without App Store Connect or a sandbox account. Apple's
server does not know the transactions Xcode signs, so they only pass a
development server started with `CROWNROAD_ALLOW_TEST_PURCHASE_CLAIMS=1`.

```bash
native/StoreKit/test/simulator_e2e.sh
```

runs the plugin on a simulator against a throwaway local server, without
Xcode's window: it exports a debug iOS project, builds it for the simulator,
and launches the game once per phase. Most phases run
`scenes/tests/StoreKitPluginReview.tscn`, which calls the `StoreKit` singleton
and validates each transaction with the server the way `NativeIAPService` does.
The shop phase runs `scenes/tests/StoreKitShopReview.tscn`, which plays the
game itself. Every phase fails if the game logs that it could not read its data,
catalog or save:

| Phase | Checks |
|---|---|
| buy | a blank profile disables purchases; the ten products load with localized prices, micros and currency, and an unknown ID is reported; a purchase returns a transaction whose signed payload carries the `appAccountToken` the server derives from the player's profile ID; it stays unfinished until the server has recorded it, then finishes; the server wallet holds the gold |
| clean (after a relaunch) | nothing is left unfinished |
| unverified | a purchase whose signature fails verification returns no transaction to grant |
| ask-to-buy | the purchase reports pending without a transaction; once approved it arrives through `Transaction.updates`, is recorded and finished |
| interrupt, recover | with the server unreachable the transaction stays unfinished, and the plugin does not finish it; on the next launch it comes back, is recorded once (a second validation says `already_recorded`) and is finished |
| shop | the game's data, wave versions, shop catalog and save load under NativeAOT; the player gets a server session and a cloud backup that lists and restores; the shop shows App Store prices, and buying a pack through the shop and `NativeIAPService` grants its gold once, records it on the server, saves it with its purchase ID and finishes the transaction |

`test/StoreKitTestSession.swift` is loaded into the game for these runs. It
opens an `SKTestSession` from `Crownroad.storekit`, which is what an Xcode run
with that configuration does, confirms purchase sheets, and sets up Ask to Buy
or a simulated verification failure where a phase needs it.

Cancelling and declining can't be checked in Xcode's test store: it reports a
simulated cancellation as an unknown error and still records the purchase, and
signs a declined Ask to Buy request exactly like a completed purchase. Check
both on a device with a sandbox account, or in an Xcode run by cancelling the
payment sheet by hand.

The iOS build is NativeAOT, which cannot run reflection-based
System.Text.Json, so the game serializes through source-generated contexts
(`scripts/core/GameJson.cs`, see `docs/DEPLOYMENT.md`). The shop phase buys only
a completed purchase; the shop's handling of every StoreKit outcome is checked
on desktop, against a stand-in for the plugin and a real local server:

```bash
native/StoreKit/test/shop_desktop.sh
```

It runs `scenes/tests/StoreKitPurchaseReview.tscn`: the shop shows App Store
prices; a purchase is granted once the server records it and finished only
after that; a transaction the server already recorded is finished without a
second grant; cancelled, pending and failed purchases grant nothing; an Ask to
Buy approval, an interrupted validation retried later and a transaction with no
purchase waiting are each granted once and finished; an unknown product is
neither granted nor finished.

Godot 4.6.1's official iOS template has an x86_64-only simulator engine
library, while iOS 27 simulators only run arm64 apps. `simulator_e2e.sh`
therefore compiles the engine for the arm64 simulator once from the Godot
source that `build.sh` downloaded (a few minutes; kept in `build/engine`) and
puts it in the exported project. That build also allows a scene path on the
command line, which official templates refuse. Godot's log on iOS goes to the
simulator's unified log, which the script reads. Device builds use the
official library.

For a run from Xcode, `native/StoreKit/test/simulator_e2e.sh --prepare`
exports and builds the same project, with `Crownroad.storekit` selected in its
scheme by `use_storekit_config.sh` (Product → Scheme → Edit Scheme → Run →
Options → StoreKit Configuration). Open it in Xcode and run it on a simulator.
Debug → StoreKit → Manage Transactions shows each transaction and whether it
was finished. To buy by hand, point the game at a development server
(Settings → Account → Payments → Connection details; `http://127.0.0.1:<port>`
reaches the Mac) and buy from the shop, cancelling the payment sheet once to
check that path.

Transactions signed by Xcode are not real verification. That needs sandbox
purchases from a TestFlight or development build signed for
`com.crownroad.game`, with the products created in App Store Connect and
`APPLE_IAP_*` set on the server.
