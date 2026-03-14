#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

MULTIPLAYER_SCRIPTS=(
	"scripts/smoke/lan_race_smoke.sh"
	"scripts/smoke/http_multiplayer_backend_suite.sh"
)

cd "$ROOT_DIR"

echo "Running multiplayer stack smoke suite..."
for script in "${MULTIPLAYER_SCRIPTS[@]}"; do
	bash "$script"
done

echo "Multiplayer stack smoke suite passed."
echo "Scripts run: ${#MULTIPLAYER_SCRIPTS[@]}"
