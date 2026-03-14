#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

ROOM_SCRIPTS=(
	"scripts/smoke/http_online_room_directory_smoke.sh"
	"scripts/smoke/http_online_room_join_smoke.sh"
	"scripts/smoke/http_online_room_create_smoke.sh"
	"scripts/smoke/http_online_room_matchmake_smoke.sh"
	"scripts/smoke/http_online_room_action_smoke.sh"
	"scripts/smoke/http_online_room_launch_smoke.sh"
	"scripts/smoke/http_online_room_result_smoke.sh"
	"scripts/smoke/http_online_room_scoreboard_smoke.sh"
	"scripts/smoke/http_online_room_reset_smoke.sh"
	"scripts/smoke/http_online_room_leave_smoke.sh"
	"scripts/smoke/http_online_room_telemetry_smoke.sh"
	"scripts/smoke/http_online_room_session_smoke.sh"
	"scripts/smoke/http_online_room_lease_smoke.sh"
	"scripts/smoke/http_online_room_report_smoke.sh"
	"scripts/smoke/online_room_session_scope_smoke.sh"
	"scripts/smoke/online_room_recovery_smoke.sh"
	"scripts/smoke/online_room_ticket_swap_smoke.sh"
)

cd "$ROOT_DIR"

echo "Running online room smoke suite..."
for script in "${ROOM_SCRIPTS[@]}"; do
	bash "$script"
done

echo "Online room smoke suite passed."
echo "Scripts run: ${#ROOM_SCRIPTS[@]}"
