#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

ENV_FILE=".env.production"
COMPOSE_FILE="docker-compose.production.yml"

if [[ ! -f "$ENV_FILE" ]]; then
    echo "Missing $ENV_FILE. Copy .env.production.example, set the production values, and create the referenced secret files."
    exit 1
fi

if ! command -v docker >/dev/null 2>&1 || ! docker compose version >/dev/null 2>&1; then
    echo "Docker Engine with the Compose v2 plugin is required."
    exit 1
fi

if ! docker info >/dev/null 2>&1; then
    echo "Docker is installed but its daemon is not available. Start Docker before deploying."
    exit 1
fi

COMPOSE=(docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE")

echo "Validating deployment configuration..."
"${COMPOSE[@]}" config -q

echo "Running server verification..."
dotnet run -- --test
dotnet run -- --test-data ../data

echo "Building and starting the private API and HTTPS proxy..."
"${COMPOSE[@]}" up -d --build --remove-orphans
"${COMPOSE[@]}" ps

echo "Deployment started. Verify https://<your API domain>/health after DNS has propagated."
