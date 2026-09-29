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

SITE_DOMAIN="$(sed -n 's/^CROWNROAD_SITE_DOMAIN=//p' "$ENV_FILE" | tail -n 1 | tr -d '[:space:]"'"'")"
if [[ -z "$SITE_DOMAIN" ]]; then
    echo "Set CROWNROAD_SITE_DOMAIN in $ENV_FILE to the public website hostname."
    exit 1
fi

if ! command -v python3 >/dev/null 2>&1; then
    echo "Python 3 is required to build the public website."
    exit 1
fi

echo "Building the public website for $SITE_DOMAIN..."
python3 ../scripts/tools/build_site.py --strict --domain "$SITE_DOMAIN"

echo "Validating deployment configuration..."
"${COMPOSE[@]}" config -q

echo "Validating HTTPS proxy configuration..."
docker run --rm --env-file "$ENV_FILE" \
    -v "$SCRIPT_DIR/Caddyfile:/etc/caddy/Caddyfile:ro" \
    caddy:2-alpine caddy validate --config /etc/caddy/Caddyfile --adapter caddyfile

echo "Running server verification..."
dotnet run -- --test
dotnet run -- --test-data ../data

echo "Building and starting the private API, website and HTTPS proxy..."
"${COMPOSE[@]}" up -d --build --remove-orphans
"${COMPOSE[@]}" ps

echo "Deployment started. After DNS has propagated, verify https://<your API domain>/health and https://$SITE_DOMAIN/."
