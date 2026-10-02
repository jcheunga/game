Crownroad playtest update
========================

New saves start with one Swordsman and no spells. Existing collections remain intact. A squad needs only one equipped unit to deploy; it can hold six units and five spells. Unit selection uses a scrolling grid with separate gold selection and green equipped indicators. Preparation uses inventory portraits, with detailed statistics behind a button.

Every campaign battle, including bosses, costs 4 food. Campaign stage rewards no longer refund food. Entering a tile for the first time costs 1 food, paid step by step; routes already walked are free. Seeing a tile does not waive its first entry cost. Journeys stop at the last reached tile when food runs out, and provisions found along the way can extend travel. Food restores by 2 every 5 minutes, including time spent away, up to 24. Bought/reward food may exceed that cap. The store sells 10 food for 100 gold alongside its existing payment products. District and occasional milestone rewards still grant food.

Adventure maps now have 768 isometric tiles each, eight times their former area, with spread-out landmarks and distant boss approaches. Drawing, picking and travel share the same coordinates. Routes follow neighboring traversable tiles; water and rock are impassable, with drawn bridge crossings. Forty sparse discoveries per zone grant food, gold, tomes, essence or nearby survey knowledge on arrival. Animated smoke conceals unknown land; charted areas keep a light haze outside caravan sight. Discovery claims, paid routes and position persist. Save revision 44 relocates older caravans by their landmark ID and preserves previously collected site rewards and defeated leaders. All 60 stages now have individual battle artwork, and all 10 zones have their own main-map artwork. Battle scenery surrounds clear ground fitted to the precise movement rectangle. Zone ground and fog use the tile grid, with continuous artwork and no grid lines. See `assets/world/README.md` and its prompt manifest. Battle capture posts, supply capture rings, the minimap, and speed controls are removed.

Units retain their attack position through wind-up, contact and recovery. Ranged troops no longer retreat while firing. Engaged troops are excluded from crowd separation, acquired attack targets remain stable while in range, and ordinary hits do not move the unit model. Movement updates facing. Swordsman speed is 60, reduced from 95, and aggro is local. Destruction of either base ends battle immediately, regardless of remaining waves.

Audio includes four original looping instrumental arrangements (title, exploration, armory and battle), layered impacts, five elemental spell cues, interface cues and result stingers. Route music falls back to the complete battle arrangement. Music loops, crossfades and respects mute and volume preferences. Hover sounds and hover tooltips are removed; controls retain accessible names. Source: `scripts/tools/create_audio.py` (NumPy and FFmpeg with a Vorbis encoder).

Account sign-in
===============

The title and Settings contain an Account entry. Email uses a six-digit one-time code with a ten-minute lifetime and five attempts; Google uses browser authorization with PKCE, a random state, and a separate device poll secret. Account identities and challenges persist in the shared database. The client registers an offline guest before first sign-in; the server links that guest only with a valid session. New identities retain local guest progress. Email and Google identities are separate unless explicitly linked from the authenticated account. Verified Google subjects identify Google accounts; email addresses do not silently merge accounts. Sign-out revokes the current server session when online.

To enable live sign-in:

1. Set the game's HTTPS API origin, or the local development server endpoint in Settings → Payments → Apply Endpoint.
2. Set `SMTP_HOST`, `SMTP_PORT`, `SMTP_FROM`, `SMTP_USER`, `SMTP_PASSWORD` for email delivery with TLS.
3. Create a Google OAuth **Web application** client. Set `GOOGLE_CLIENT_ID`, `GOOGLE_CLIENT_SECRET`, `GOOGLE_REDIRECT_URI` on the server. Register that exact HTTPS callback URI, ending in `/auth/google/callback`, with Google. Local callbacks may use loopback HTTP.
4. Restart the server. `/auth/providers` reports which sign-in methods are configured. Buttons are disabled when their provider is unavailable.

Provider credentials are not included. Live email delivery and Google's consent flow require the configured services and a manual device check. The server exchanges the authorization code directly with Google and fetches the verified identity from its authenticated userinfo endpoint, following [Google's OpenID Connect flow](https://developers.google.com/identity/openid-connect/openid-connect).

Before switching accounts, the game downloads the selected account's cloud save, when available, then saves current local progress to `user://account-backups/` before changing identity. A failed download leaves the current account intact. Account labels and session secrets are excluded from cloud save payloads. Cloud upload remains available in Settings; this update does not add automatic uploads. Backup files are local and include the device session, so handle them like ordinary private game saves.

Verification
============

`./scripts/verify_all.sh` builds the game, checks data and runs server tests. `FeedbackReview.tscn` covers the starter squad, food/recharge/refill behavior, tile pathing, persistence, expanded cards, fixed attack positions, facing and base destruction. Use a unique `--save-suffix=feedback-review-...` to protect personal progress. Add `--mobile-preview` for phone HUD checks and `--capture` for review images. `ExplorationReview.tscn`, using `--save-suffix=exploration-review-...`, checks all expanded zones, sparse rewards, smoke, food exhaustion, actual caravan movement and migration. Add `--capture` and optionally `--mobile-preview` for fresh, partially explored and overview images. `CombatMotionReview.tscn` checks timed attack contacts across the roster. `WorldArtReview.tscn`, with an isolated `--save-suffix=world-art-review-...`, checks all 70 environments and their movement/tile mapping, and captures fresh/explored zone maps and battle scenes on desktop and phone.
