#!/bin/sh
# End-to-end test against a real server: starts a throwaway local server, plays a first install
# (guest, cloud backup, email sign-in, every account feature, sign-out, sign back in), restarts the
# server, then plays a second install that signs in to the same account, restores it and deletes it.
# Last the first install opens again, still signed in, and must notice the account is gone.
# The server prints sign-in codes to its log (CROWNROAD_DEV_SIGN_IN), so no email is sent.
#
# --stack runs the same test against the production stack instead: docker-compose.production.yml with
# the docker-compose.e2e.yml overlay (the production image, two API replicas behind Caddy, Redis and a
# PostgreSQL of its own), reached through Caddy. The restart then restarts every replica. Needs Docker.
set -u
cd "$(dirname "$0")/../.."
export PATH="/usr/local/share/dotnet:$PATH"
stack=0
[ "${1:-}" = "--stack" ] && stack=1
port=${E2E_PORT:-8097}
work=$(mktemp -d "${TMPDIR:-/tmp}/crownroad-e2e.XXXXXX")
log="$work/server.log"
results="$work/results.txt"
server=""
follower=""
settle=0
# Operator refunds (/admin/refunds) stand in for the stores' refund signals.
admin_key="e2e-admin-$(date +%s)"

start_server() {
    ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS="http://127.0.0.1:$port" DbPath="$work/e2e.db" \
    CROWNROAD_DEV_SIGN_IN=1 CROWNROAD_ALLOW_TEST_PURCHASE_CLAIMS=1 ADMIN_API_KEY="$admin_key" \
        dotnet run --project server --no-build --no-launch-profile >> "$log" 2>&1 &
    server=$!
    wait_healthy
}
stop_server() { [ -n "$server" ] && kill "$server" 2> /dev/null && wait "$server" 2> /dev/null; server=""; }
wait_healthy() {
    for _ in $(seq 1 120); do
        curl -sf "http://127.0.0.1:$port/health" > /dev/null && return 0
        sleep 0.5
    done
    echo "Server did not start; see $log"
    return 1
}
player_rows() { sqlite3 "$work/e2e.db" "SELECT COUNT(*) FROM players WHERE profile_id = '$1'" 2>&1; }

if [ $stack = 1 ]; then
    # Each replica reads operator switches (the purchase pause) at most every 10 s.
    settle=10.5
    export E2E_PORT="$port" E2E_SITE="$work/site" E2E_POSTGRES_PASSWORD="e2e-$(date +%s)"
    mkdir -p "$work/site" "$work/secrets"
    printf '%s' "e2e-redis-$(date +%s)" > "$work/secrets/redis-password"
    printf '%s' "$admin_key" > "$work/secrets/admin-api-key"
    : > "$work/secrets/google-play-service-account.json"
    cat > "$work/env" <<EOF
DATABASE_URL=postgresql://crownroad:$E2E_POSTGRES_PASSWORD@postgres:5432/crownroad?pooling=true&min_pool_size=5&max_pool_size=40&application_name=crownroad-e2e
REDIS_PASSWORD_SOURCE=$work/secrets/redis-password
ADMIN_API_KEY_SOURCE=$work/secrets/admin-api-key
GOOGLE_PLAY_SERVICE_ACCOUNT_SOURCE=$work/secrets/google-play-service-account.json
EOF
    compose() {
        docker compose -p crownroad-e2e --env-file "$work/env" \
            -f server/docker-compose.production.yml -f server/docker-compose.e2e.yml "$@"
    }
    start_server() {
        if [ -z "$follower" ]; then
            echo "Building and starting the stack..."
            compose up -d --build --wait >> "$work/compose.log" 2>&1 || { echo "The stack did not start; see $work/compose.log"; return 1; }
            # Both replicas' logs, rewritten every second, stand in for the local server's log.
            (while :; do compose logs --no-log-prefix crownroad-server > "$log.new" 2> /dev/null && mv "$log.new" "$log"; sleep 1; done) &
            follower=$!
        else
            compose start crownroad-server >> "$work/compose.log" 2>&1
        fi
        wait_healthy
    }
    # Redis holds only rate-limit counters. Clearing them matches a local server's restart, which forgets its
    # in-memory ones: three installs signing in from one address within minutes is not one player's traffic.
    stop_server() {
        [ -n "$follower" ] || return 0
        compose stop crownroad-server >> "$work/compose.log" 2>&1
        compose exec -T redis sh -c 'redis-cli --no-auth-warning -a "$(cat /run/secrets/redis_password)" FLUSHDB' >> "$work/compose.log" 2>&1
    }
    finish() {
        [ -n "$follower" ] && kill "$follower" 2> /dev/null
        compose logs --no-log-prefix > "$work/stack.log" 2>&1
        compose down -v --remove-orphans >> "$work/compose.log" 2>&1
    }
    player_rows() { compose exec -T postgres psql -U crownroad -d crownroad -tAc "SELECT COUNT(*) FROM players WHERE profile_id = '$1'" 2>&1; }
    trap finish EXIT
else
    trap stop_server EXIT
fi

# play <role> [install]: the install's save defaults to the role's own.
play() {
    godot --headless --path . res://scenes/tests/ServerEndToEnd.tscn -- --save-suffix="server-e2e-${2:-$1}" --role="$1" \
        --server="http://127.0.0.1:$port" --server-log="$log" --handoff="$work/handoff.json" --admin-key="$admin_key" --settle="$settle" 2>&1 | grep -E "^E2E_" | tee -a "$results"
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
rows=$(player_rows "$account")
[ "$rows" = 0 ] && echo "E2E_CHECK: PASS The deleted account has no player row on the server" ||
    echo "E2E_CHECK: FAIL The deleted account has no player row on the server ($rows)"
echo "Server log: $log"
[ $stack = 1 ] && echo "Stack logs: $work/stack.log"
# Every run must report a result with no failures, and the database must agree.
[ "$(grep -c '^E2E_RESULT: 0 failures' "$results")" -eq 3 ] && [ "$rows" = 0 ]
