#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

IMAGE_NAME="${CROWNROAD_IMAGE:-crownroad-server}"
IMAGE_TAG="${CROWNROAD_TAG:-latest}"

echo "=== Crownroad Server Deploy ==="
echo ""

# Run verification first
echo "--- Running server tests ---"
dotnet run -- --test
echo ""

echo "--- Validating game data ---"
dotnet run -- --test-data ../data
echo ""

# Build Docker image
echo "--- Building Docker image: ${IMAGE_NAME}:${IMAGE_TAG} ---"
docker build -t "${IMAGE_NAME}:${IMAGE_TAG}" .
echo ""

# If a registry is set, push
if [ -n "${CROWNROAD_REGISTRY:-}" ]; then
    FULL_TAG="${CROWNROAD_REGISTRY}/${IMAGE_NAME}:${IMAGE_TAG}"
    echo "--- Pushing to ${FULL_TAG} ---"
    docker tag "${IMAGE_NAME}:${IMAGE_TAG}" "${FULL_TAG}"
    docker push "${FULL_TAG}"
    echo ""
fi

echo "--- Done ---"
echo ""
echo "To run locally:"
echo "  docker compose up -d"
echo ""
echo "To run with Stripe:"
echo "  STRIPE_SECRET_KEY=sk_live_... STRIPE_WEBHOOK_SECRET=whsec_... docker compose up -d"
echo ""
echo "Health check:  curl http://localhost:8080/health"
echo "Full stats:    curl http://localhost:8080/stats"
