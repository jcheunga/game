#!/bin/sh
# End-to-end test against a real server: starts a throwaway local server, plays a first install
# (guest, cloud backup, email sign-in, every account feature, sign-out, sign back in), restarts the
# server, then plays a second install that signs in to the same account, restores it and deletes it.
# Last the first install opens again, still signed in, and must notice the account is gone.
# The server prints sign-in codes to its log (CROWNROAD_DEV_SIGN_IN), so no email is sent.
set -u
cd "$(dirname "$0")/../.."
export PATH="/usr/local/share/dotnet:$PATH"
port=${E2E_PORT:-8097}
work=$(mktemp -d "${TMPDIR:-/tmp}/crownroad-e2e.XXXXXX")
log="$work/server.log"
results="$work/results.txt"
server=""

start_server() {
    ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS="http://127.0.0.1:$port" DbPath="$work/e2e.db" \
    CROWNROAD_DEV_SIGN_IN=1 CROWNROAD_ALLOW_TEST_PURCHASE_CLAIMS=1 \
        dotnet run --project server --no-build --no-launch-profile >> "$log" 2>&1 &
    server=$!
    for _ in $(seq 1 60); do
        curl -sf "http://127.0.0.1:$port/health" > /dev/null && return 0
        sleep 0.5
    done
    echo "Server did not start; see $log"
    return 1
}
stop_server() { [ -n "$server" ] && kill "$server" 2> /dev/null && wait "$server" 2> /dev/null; server=""; }
trap stop_server EXIT

# play <role> [install]: the install's save defaults to the role's own.
play() {
    godot --headless --path . res://scenes/tests/ServerEndToEnd.tscn -- --save-suffix="server-e2e-${2:-$1}" --role="$1" \
        --server="http://127.0.0.1:$port" --server-log="$log" --handoff="$work/handoff.json" 2>&1 | grep -E "^E2E_" | tee -a "$results"
}

dotnet build server/Server.csproj -v q -nologo > /dev/null || exit 1
start_server || exit 1
play first
# A restart proves accounts and saves live in the database, not in server memory.
stop_server
start_server || exit 1
play second
play return first
# The returning install's refused calls must not have registered the deleted profile again.
account=$(sed -E 's/.*"account":"([^"]*)".*/\1/' "$work/handoff.json")
rows=$(sqlite3 "$work/e2e.db" "SELECT COUNT(*) FROM players WHERE profile_id = '$account'" 2>&1)
[ "$rows" = 0 ] && echo "E2E_CHECK: PASS The deleted account has no player row on the server" ||
    echo "E2E_CHECK: FAIL The deleted account has no player row on the server ($rows)"
echo "Server log: $log"
# Every run must report a result with no failures, and the database must agree.
[ "$(grep -c '^E2E_RESULT: 0 failures' "$results")" -eq 3 ] && [ "$rows" = 0 ]
