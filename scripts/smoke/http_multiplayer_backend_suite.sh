#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

BACKEND_SCRIPTS=(
	"scripts/smoke/http_player_profile_sync_smoke.sh"
	"scripts/smoke/http_challenge_sync_smoke.sh"
	"scripts/smoke/http_challenge_leaderboard_smoke.sh"
	"scripts/smoke/http_challenge_feed_smoke.sh"
	"scripts/smoke/http_online_room_suite.sh"
)

cd "$ROOT_DIR"

echo "Running multiplayer backend smoke suite..."
for script in "${BACKEND_SCRIPTS[@]}"; do
	bash "$script"
done

echo "Multiplayer backend smoke suite passed."
echo "Scripts run: ${#BACKEND_SCRIPTS[@]}"
