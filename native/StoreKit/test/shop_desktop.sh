#!/bin/sh
# Checks the shop's App Store purchase flow (NativeIAPService and CashShopMenu) on desktop, with a
# stand-in for the StoreKit plugin and a throwaway local server that accepts test purchase claims:
# grants only after the server recorded a transaction, finishes only after that, and handles
# cancelled, pending (Ask to Buy), failed, interrupted and replayed purchases. The plugin itself
# is checked on a simulator by simulator_e2e.sh.
set -u
cd "$(dirname "$0")/../../.."
export PATH="/usr/local/share/dotnet:$PATH"
port=${STOREKIT_SHOP_PORT:-8099}
work=$(mktemp -d "${TMPDIR:-/tmp}/crownroad-storekit-shop.XXXXXX")
server=""
stop_server() { [ -n "$server" ] && kill "$server" 2> /dev/null && wait "$server" 2> /dev/null; server=""; }
trap stop_server EXIT

dotnet build server/Server.csproj -v q -nologo > /dev/null || exit 1
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS="http://127.0.0.1:$port" DbPath="$work/server.db" \
CROWNROAD_ALLOW_TEST_PURCHASE_CLAIMS=1 \
    dotnet run --project server --no-build --no-launch-profile > "$work/server.log" 2>&1 &
server=$!
for _ in $(seq 1 60); do curl -sf "http://127.0.0.1:$port/health" > /dev/null && break; sleep 0.5; done

godot --headless --path . res://scenes/tests/StoreKitPurchaseReview.tscn -- --save-suffix="storekit-shop-$(date +%s)" \
    --server="http://127.0.0.1:$port" 2>&1 | grep -E "^STOREKIT_|SCRIPT ERROR|Unhandled" | tee "$work/results.txt"
echo "$(grep -c "^STOREKIT_CHECK: PASS" "$work/results.txt") checks passed, $(grep -c "^STOREKIT_CHECK: FAIL" "$work/results.txt") failed (logs in $work)"
grep -q "^STOREKIT_RESULT: 0 failures" "$work/results.txt"
