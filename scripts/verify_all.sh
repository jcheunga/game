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

echo "--- Done ---"
