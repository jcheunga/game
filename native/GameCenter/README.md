# GameCenter iOS plugin

Signs players in to Game Center on iOS and gives the game a GameKit identity
signature for the server to verify (`scripts/core/PlatformSignIn.cs`,
`/auth/gamecenter` in `server/AccountAuth.cs`).

The sources in `src/` are Godot's official GameCenter plugin from
[godot-ios-plugins](https://github.com/godotengine/godot-ios-plugins) at
commit `caafb2c` (MIT, see `LICENSE`). The plugin registers a `GameCenter`
engine singleton with `authenticate`, `is_authenticated`,
`request_identity_verification_signature`, `get_pending_event_count` and
`pop_pending_event`, plus the upstream leaderboard and achievement calls.
Results arrive as queued dictionaries:

| `type` | Fields when `result` is `"ok"` |
|---|---|
| `authentication` | `player_id` (`teamPlayerID`), `alias`, `displayName` |
| `identity_verification_signature` | `player_id`, `public_key_url`, `signature` and `salt` (base64), `timestamp` (milliseconds) |

On failure, `result` is `"error"` with `error_code` and `error_description`.
The signature comes from `fetchItemsForIdentityVerificationSignature`, which
signs the `teamPlayerID`, so the server checks the same ID the event reports.

Changes from upstream:

- Events are queued on the main thread, where the game reads them. GameKit
  does not promise which thread runs its completion handlers.
- `build.sh` compiles with the defines of Godot's official iOS templates,
  including `THREADS_ENABLED`. The upstream SConstruct leaves it out, but it
  changes the layout of `Object` (through `Mutex`), and the plugin's
  `GameCenter` class must match the engine it is linked into.
- The plugin targets iOS 15, the oldest version Xcode 27 builds for.

## Building

```bash
native/GameCenter/build.sh
```

The script needs Xcode, git and python3. On first run it downloads the Godot
source tag the game exports with (`GODOT_TAG`, currently `4.6.1-stable`) and
SCons into `build/`, which is not committed. SCons only generates a few engine
headers; the engine itself is not compiled. The script builds debug and release
xcframeworks (arm64 device, arm64 and x86_64 simulator) and installs them with
`gamecenter.gdip` in `ios/plugins/gamecenter`. The iOS export preset enables
the plugin (`plugins/GameCenter`) and the Game Center entitlement.

**Rebuild the plugin whenever the project moves to another Godot version**,
after updating `GODOT_TAG`. An iOS plugin compiled against other engine
headers can crash at launch.
