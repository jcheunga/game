#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

cd "$ROOT_DIR"

echo "=== Crownroad Repo Verify ==="
echo ""

echo "--- Building game ---"
dotnet build Game.csproj
echo ""

echo "--- Validating data ---"
(
  cd server
  dotnet run -- --test-data ../data
)
echo ""

echo "--- Running server tests ---"
(
  cd server
  dotnet run -- --test
)
echo ""

# The iOS StoreKit plugin's appAccountToken must match the server's (same vectors as the server tests).
if command -v xcrun > /dev/null 2>&1; then
  echo "--- Checking the StoreKit plugin's appAccountToken ---"
  native/StoreKit/test/run_tests.sh
  echo ""
fi

echo "--- Building public website ---"
python3 scripts/tools/build_site.py
echo ""

echo "--- Done ---"
