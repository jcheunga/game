#!/usr/bin/env bash
# Builds the public website and deploys it to Cloudflare as a Worker with static assets.
#
#   scripts/release/deploy_site.sh          # strict build, then deploy to the domain in site/site.json
#   scripts/release/deploy_site.sh --dev    # preview build, served locally by Wrangler at http://localhost:8787
#
# The first deploy asks Wrangler to sign in to your Cloudflare account in the browser (or set CLOUDFLARE_API_TOKEN).
# The domain must already be a zone on that account; the deploy attaches it and its www. alias as custom domains,
# and Cloudflare issues their certificates. Nothing on the game server is involved. See docs/WEBSITE.md.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

WRANGLER_VERSION="4.156.0"
WORKER_NAME="crownroad-site"
CONFIG="site/wrangler.generated.jsonc"

if ! command -v npx >/dev/null 2>&1; then
	echo "Node.js (npx) is required to run Wrangler." >&2
	exit 1
fi

mode="deploy"
[[ "${1:-}" == "--dev" ]] && mode="dev"

DOMAIN="$(python3 -c 'import json; print(json.load(open("site/site.json"))["domain"].strip().lower())')"
[[ -n "$DOMAIN" ]] || { echo "Set \"domain\" in site/site.json first." >&2; exit 1; }

if [[ "$mode" == "deploy" ]]; then
	python3 scripts/tools/build_site.py --strict --domain "$DOMAIN"
else
	python3 scripts/tools/build_site.py
fi

# Generated so the custom domains always match site.json. Paths are relative to this file.
cat > "$CONFIG" <<EOF
{
  "name": "$WORKER_NAME",
  "main": "worker.js",
  "compatibility_date": "2026-10-01",
  "assets": {
    "directory": "dist",
    "binding": "ASSETS",
    "html_handling": "drop-trailing-slash",
    "not_found_handling": "404-page",
    "run_worker_first": true
  },
  "routes": [
    { "pattern": "$DOMAIN", "custom_domain": true },
    { "pattern": "www.$DOMAIN", "custom_domain": true }
  ],
  "workers_dev": false,
  "preview_urls": false,
  "observability": { "enabled": false }
}
EOF

if [[ "$mode" == "dev" ]]; then
	exec npx --yes "wrangler@$WRANGLER_VERSION" dev --config "$CONFIG" --local
fi

npx --yes "wrangler@$WRANGLER_VERSION" deploy --config "$CONFIG"
echo "Deployed. Check https://$DOMAIN/ and https://www.$DOMAIN/ (it should redirect)."
