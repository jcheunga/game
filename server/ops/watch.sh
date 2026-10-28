#!/usr/bin/env bash
# Health alerts for the Crownroad production host. Run it every minute or two
# from a systemd timer or cron on the Docker Compose VM (see server/ops/README.md).
#
# Checks: the public /health answer and its speed, the TLS certificate's days
# to expiry, Compose service state, health and restart counts, disk space, the
# rate of Caddy 5xx answers, the API's p95 latency and failing purchase
# validations over the last few minutes, the age of the newest backup, and
# (optional) PostgreSQL connections.
#
# Problems go to ALERT_WEBHOOK_URL as one JSON POST per run (Slack, Discord,
# ntfy or a generic receiver). The state directory remembers what was already
# sent: a problem alerts when it starts or changes, repeats every
# REALERT_MINUTES while it lasts, and sends one "resolved" when it clears.
#
#   ./watch.sh             run the checks and alert
#   ./watch.sh --test      send a test alert (evidence that alerts reach on-call)
#   ./watch.sh --dry-run   run the checks and show what would be sent
#
# Exit codes: 0 all OK, 1 a warning, 2 a critical problem, 3 a usage or
# configuration error. Written for bash 3.2 (macOS) and later.
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPT_NAME="$(basename "$0")"
MODE="run"
VERBOSE=0
CONFIG_FILE=""

usage() {
    cat <<EOF
Usage: $SCRIPT_NAME [--test | --dry-run] [--config FILE] [--verbose]

  --test         Send a test alert to ALERT_WEBHOOK_URL and report the answer.
  --dry-run      Run every check and print what would be sent; send nothing,
                 change no state.
  --config FILE  Read settings from FILE (shell KEY=value lines), e.g.
                 /etc/crownroad-watch.env. Default: $SCRIPT_DIR/watch.env if present.
  --verbose      Also print details of passing checks.

Settings (environment or config file) are listed in server/ops/README.md and
watch.env.example. The most important: API_URL, ALERT_WEBHOOK_URL.
EOF
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --test) MODE="test"; shift ;;
        --dry-run) MODE="dry"; shift ;;
        --config) CONFIG_FILE="${2:-}"; [[ -n "$CONFIG_FILE" ]] || { usage >&2; exit 3; }; shift 2 ;;
        --verbose|-v) VERBOSE=1; shift ;;
        -h|--help) usage; exit 0 ;;
        *) echo "$SCRIPT_NAME: unknown option $1" >&2; usage >&2; exit 3 ;;
    esac
done

SETTINGS="API_URL ALERT_WEBHOOK_URL ALERT_FORMAT ALERT_WEBHOOK_TOKEN ALERT_NAME STATE_DIR REALERT_MINUTES
CHECKS HEALTH_TIMEOUT HEALTH_SLOW_MS TLS_WARN_DAYS TLS_CRIT_DAYS COMPOSE_PROJECT EXPECTED_SERVICES API_SERVICE
API_REPLICAS DISK_PATHS DISK_WARN_PERCENT DISK_CRIT_PERCENT CADDY_SERVICE CADDY_LOG_FILE CADDY_WINDOW_MINUTES
FIVEXX_WARN_PERCENT FIVEXX_CRIT_PERCENT FIVEXX_MIN_REQUESTS LATENCY_P95_WARN_MS PURCHASE_REFUSED_WARN BACKUP_DIR BACKUP_GLOB
BACKUP_CHECK_CMD BACKUP_MAX_AGE_HOURS PG_MONITOR_URL PG_CONN_WARN_PERCENT PG_MAX_DB_GB HEARTBEAT_URL
HOST_LABEL SERVER_DIR ENV_FILE"

if [[ -z "$CONFIG_FILE" && -r "$SCRIPT_DIR/watch.env" ]]; then CONFIG_FILE="$SCRIPT_DIR/watch.env"; fi
if [[ -n "$CONFIG_FILE" ]]; then
    [[ -r "$CONFIG_FILE" ]] || { echo "$SCRIPT_NAME: cannot read $CONFIG_FILE" >&2; exit 3; }
    # A setting given in the environment wins over the file, for one-off runs.
    overrides=""
    for name in $SETTINGS; do
        if eval "[[ -n \${$name+set} ]]"; then
            overrides="$overrides$name=$(eval "printf '%q' \"\$$name\"");"
        fi
    done
    set -a
    # shellcheck disable=SC1090
    . "$CONFIG_FILE"
    set +a
    eval "$overrides"
fi

# ── settings ──────────────────────────────────────────────────────────────

SERVER_DIR="${SERVER_DIR:-$(cd "$SCRIPT_DIR/.." && pwd)}"
ENV_FILE="${ENV_FILE:-$SERVER_DIR/.env.production}"
env_file_value() {
    [[ -r "$ENV_FILE" ]] || return 0
    sed -n "s/^$1=//p" "$ENV_FILE" | tail -n 1 | tr -d '[:space:]"'"'"
}

if [[ -z "${API_URL:-}" ]]; then
    domain="$(env_file_value CROWNROAD_API_DOMAIN)"
    if [[ -n "$domain" ]]; then API_URL="https://$domain"; fi
fi
API_URL="${API_URL:-}"
API_URL="${API_URL%/}"
ALERT_WEBHOOK_URL="${ALERT_WEBHOOK_URL:-}"
ALERT_FORMAT="${ALERT_FORMAT:-auto}"
ALERT_WEBHOOK_TOKEN="${ALERT_WEBHOOK_TOKEN:-}"
ALERT_NAME="${ALERT_NAME:-crownroad}"
STATE_DIR="${STATE_DIR:-${XDG_STATE_HOME:-$HOME/.local/state}/crownroad-watch}"
REALERT_MINUTES="${REALERT_MINUTES:-240}"
CHECKS="${CHECKS:-health,tls,containers,disk,caddy,backup,postgres}"

HEALTH_TIMEOUT="${HEALTH_TIMEOUT:-5}"
HEALTH_SLOW_MS="${HEALTH_SLOW_MS:-2000}"
TLS_WARN_DAYS="${TLS_WARN_DAYS:-21}"
TLS_CRIT_DAYS="${TLS_CRIT_DAYS:-7}"

COMPOSE_PROJECT="${COMPOSE_PROJECT:-$(basename "$SERVER_DIR" | tr '[:upper:]' '[:lower:]')}"
EXPECTED_SERVICES="${EXPECTED_SERVICES:-redis crownroad-server caddy}"
API_SERVICE="${API_SERVICE:-crownroad-server}"
API_REPLICAS="${API_REPLICAS:-$(env_file_value CROWNROAD_SERVER_REPLICAS)}"
API_REPLICAS="${API_REPLICAS:-2}"

DISK_PATHS="${DISK_PATHS:-/}"
DISK_WARN_PERCENT="${DISK_WARN_PERCENT:-80}"
DISK_CRIT_PERCENT="${DISK_CRIT_PERCENT:-90}"

CADDY_SERVICE="${CADDY_SERVICE:-caddy}"
CADDY_LOG_FILE="${CADDY_LOG_FILE:-}"
CADDY_WINDOW_MINUTES="${CADDY_WINDOW_MINUTES:-5}"
FIVEXX_WARN_PERCENT="${FIVEXX_WARN_PERCENT:-1}"
FIVEXX_CRIT_PERCENT="${FIVEXX_CRIT_PERCENT:-5}"
FIVEXX_MIN_REQUESTS="${FIVEXX_MIN_REQUESTS:-20}"
LATENCY_P95_WARN_MS="${LATENCY_P95_WARN_MS:-1000}"
PURCHASE_REFUSED_WARN="${PURCHASE_REFUSED_WARN:-10}"

BACKUP_DIR="${BACKUP_DIR:-}"
BACKUP_GLOB="${BACKUP_GLOB:-*.dump*}"
BACKUP_CHECK_CMD="${BACKUP_CHECK_CMD:-}"
BACKUP_MAX_AGE_HOURS="${BACKUP_MAX_AGE_HOURS:-26}"

PG_MONITOR_URL="${PG_MONITOR_URL:-}"
PG_CONN_WARN_PERCENT="${PG_CONN_WARN_PERCENT:-80}"
PG_MAX_DB_GB="${PG_MAX_DB_GB:-}"

HEARTBEAT_URL="${HEARTBEAT_URL:-}"
HOST_LABEL="${HOST_LABEL:-$(hostname)}"

TMP="$(mktemp -d "${TMPDIR:-/tmp}/crownroad-watch.XXXXXX")"
LOCK_DIR=""
cleanup() {
    rm -rf "$TMP"
    if [[ -n "$LOCK_DIR" ]]; then rmdir "$LOCK_DIR" 2>/dev/null || true; fi
}
trap cleanup EXIT

NOW="$(date +%s)"
NOW_TEXT="$(date -u '+%Y-%m-%d %H:%M UTC')"

has() { command -v "$1" >/dev/null 2>&1; }
enabled() { case ",$CHECKS," in *",$1,"*) return 0 ;; esac; return 1; }
with_timeout() {
    local seconds="$1"; shift
    if has timeout; then timeout "$seconds" "$@"; else "$@"; fi
}

# ── results ───────────────────────────────────────────────────────────────

R_KEYS=()
R_LEVELS=()
R_MSGS=()
WORST=0

# record KEY LEVEL MESSAGE. LEVEL is ok, warn, crit or skip. Keys starting
# with "event:" are one-off events (a restart): they alert, but clear silently.
record() {
    R_KEYS+=("$1"); R_LEVELS+=("$2"); R_MSGS+=("$3")
    case "$2" in
        warn) [[ $WORST -lt 1 ]] && WORST=1 ;;
        crit) WORST=2 ;;
    esac
    if [[ "$2" != ok || $VERBOSE -eq 1 ]]; then
        printf '%-5s %-28s %s\n' "$(echo "$2" | tr '[:lower:]' '[:upper:]')" "$1" "$3"
    fi
}

# ── checks ────────────────────────────────────────────────────────────────

check_health() {
    if [[ -z "$API_URL" ]]; then
        record health crit "API_URL is not set and $ENV_FILE has no CROWNROAD_API_DOMAIN"
        return
    fi
    local attempt out rc code seconds ms body err
    for attempt in 1 2; do
        out="$(curl -sS -m "$HEALTH_TIMEOUT" -o "$TMP/health.body" -w '%{http_code} %{time_total}' \
            -H 'User-Agent: crownroad-watch' "$API_URL/health" 2>"$TMP/health.err")"
        rc=$?
        code="${out%% *}"
        if [[ $rc -eq 0 && "$code" == 200 ]] && grep -Eq '"status"[[:space:]]*:[[:space:]]*"healthy"' "$TMP/health.body"; then
            break
        fi
        # One retry five seconds later, so a single dropped packet does not page anyone.
        [[ $attempt -eq 1 ]] && sleep 5
    done
    if [[ $rc -ne 0 ]]; then
        err="$(head -c 200 "$TMP/health.err" | tr '\n' ' ')"
        record health crit "$API_URL/health gave no answer within ${HEALTH_TIMEOUT}s (twice): ${err:-curl exit $rc}"
        return
    fi
    seconds="${out#* }"
    ms="$(awk -v s="$seconds" 'BEGIN { printf "%d", s * 1000 }')"
    body="$(head -c 200 "$TMP/health.body" | tr '\n' ' ')"
    if [[ "$code" != 200 ]] || ! grep -Eq '"status"[[:space:]]*:[[:space:]]*"healthy"' "$TMP/health.body"; then
        record health crit "$API_URL/health answered HTTP $code (twice): $body"
    elif [[ "$ms" -gt "$HEALTH_SLOW_MS" ]]; then
        record health warn "$API_URL/health is healthy but took ${ms} ms (limit ${HEALTH_SLOW_MS} ms)"
    else
        record health ok "healthy in ${ms} ms"
    fi
}

to_epoch() {
    # Parses dates like "Nov  9 12:00:00 2026 GMT" or ISO 8601 into epoch seconds.
    local text="$1"
    date -u -d "$text" +%s 2>/dev/null && return 0
    date -u -j -f "%b %e %H:%M:%S %Y %Z" "$text" +%s 2>/dev/null && return 0
    date -u -j -f "%Y-%m-%dT%H:%M:%SZ" "$text" +%s 2>/dev/null && return 0
    python3 - "$text" 2>/dev/null <<'PY' && return 0
import sys, email.utils, datetime
t = sys.argv[1].strip()
try:
    print(int(datetime.datetime.fromisoformat(t.replace("Z", "+00:00")).timestamp()))
except ValueError:
    print(int(datetime.datetime.strptime(t, "%b %d %H:%M:%S %Y %Z").replace(tzinfo=datetime.timezone.utc).timestamp()))
PY
    return 1
}

check_tls() {
    case "$API_URL" in
        https://*) ;;
        *) record tls skip "API_URL is not HTTPS"; return ;;
    esac
    has openssl || { record tls skip "openssl is not installed"; return; }
    local hostport host port end_text end_epoch days
    hostport="${API_URL#https://}"; hostport="${hostport%%/*}"
    host="${hostport%%:*}"; port=443
    if [[ "$hostport" == *:* ]]; then port="${hostport##*:}"; fi
    end_text="$(with_timeout 15 openssl s_client -connect "$host:$port" -servername "$host" </dev/null 2>/dev/null \
        | openssl x509 -noout -enddate 2>/dev/null | sed 's/^notAfter=//')"
    if [[ -z "$end_text" ]]; then
        record tls crit "could not read the TLS certificate of $host:$port"
        return
    fi
    if ! end_epoch="$(to_epoch "$end_text")"; then
        record tls warn "could not parse the certificate expiry '$end_text'"
        return
    fi
    days=$(( (end_epoch - NOW) / 86400 ))
    if [[ $days -lt $TLS_CRIT_DAYS ]]; then
        record tls crit "certificate for $host expires in $days day(s) ($end_text); Caddy renews about 30 days ahead, so renewal is failing: check 'docker compose logs caddy'"
    elif [[ $days -lt $TLS_WARN_DAYS ]]; then
        record tls warn "certificate for $host expires in $days days ($end_text); Caddy should already have renewed it"
    else
        record tls ok "certificate for $host valid for $days more days"
    fi
}

docker_ready() {
    if ! has docker; then return 1; fi
    if [[ -z "${DOCKER_OK:-}" ]]; then
        if docker info >/dev/null 2>"$TMP/docker.err"; then DOCKER_OK=1; else DOCKER_OK=0; fi
    fi
    [[ "$DOCKER_OK" == 1 ]]
}

state_get() { [[ -r "$STATE_DIR/$1" ]] && cat "$STATE_DIR/$1"; }
state_put() { [[ "$MODE" == run ]] && printf '%s\n' "$2" >"$STATE_DIR/$1"; return 0; }

check_containers() {
    if ! has docker; then record containers skip "docker is not installed on this machine"; return; fi
    if ! docker_ready; then
        record containers crit "the Docker daemon is unreachable: $(head -c 160 "$TMP/docker.err" | tr '\n' ' ')"
        return
    fi
    local ids id line service status health restarts name
    ids="$(docker ps -aq --filter "label=com.docker.compose.project=$COMPOSE_PROJECT" 2>/dev/null)"
    if [[ -z "$ids" ]]; then
        record containers crit "no containers for Compose project '$COMPOSE_PROJECT' (set COMPOSE_PROJECT if the stack uses another name)"
        return
    fi
    : >"$TMP/containers"
    for id in $ids; do
        docker inspect --format '{{index .Config.Labels "com.docker.compose.service"}}|{{.State.Status}}|{{if .State.Health}}{{.State.Health.Status}}{{end}}|{{.RestartCount}}|{{.Name}}|{{.Id}}' "$id" \
            >>"$TMP/containers" 2>/dev/null
    done
    local svc running problems new_restarts total_restarts expected
    for svc in $EXPECTED_SERVICES; do
        running=0; problems=""; new_restarts=0; total_restarts=0
        expected=1
        [[ "$svc" == "$API_SERVICE" ]] && expected="$API_REPLICAS"
        while IFS='|' read -r service status health restarts name id; do
            [[ "$service" == "$svc" ]] || continue
            name="${name#/}"
            if [[ "$status" == running && ( -z "$health" || "$health" == healthy ) ]]; then
                running=$((running + 1))
            elif [[ "$status" == running && "$health" == starting ]]; then
                problems="$problems $name starting;"
            else
                problems="$problems $name ${status}${health:+/$health};"
            fi
            # Docker counts restarts made by the restart policy. A redeploy
            # creates new containers, which start again from zero.
            local previous
            previous="$(state_get "restarts.${id:0:12}")"
            if [[ -n "$previous" && "$restarts" -gt "$previous" ]]; then
                new_restarts=$((new_restarts + restarts - previous))
            fi
            total_restarts=$((total_restarts + restarts))
            state_put "restarts.${id:0:12}" "$restarts"
        done <"$TMP/containers"
        if [[ $running -lt $expected ]]; then
            record "container:$svc" crit "$running of $expected $svc container(s) running and healthy:${problems:- none found}"
        elif [[ -n "$problems" ]]; then
            record "container:$svc" warn "$svc:$problems"
        else
            record "container:$svc" ok "$running/$expected running and healthy, $total_restarts restart(s) in total"
        fi
        if [[ $new_restarts -gt 0 ]]; then
            record "event:restart:$svc" warn "$svc restarted $new_restarts time(s) since the last check ($total_restarts in total); see 'docker compose logs $svc'"
        else
            record "event:restart:$svc" ok "no new restarts"
        fi
    done
    # Forget containers that no longer exist.
    if [[ "$MODE" == run ]]; then
        local file short
        for file in "$STATE_DIR"/restarts.*; do
            [[ -e "$file" ]] || continue
            short="${file##*/restarts.}"
            grep -q "|$short" "$TMP/containers" || rm -f "$file"
        done
    fi
}

check_disk() {
    local paths="$DISK_PATHS" path line pct avail mount seen=" "
    if docker_ready; then
        paths="$paths $(docker info --format '{{.DockerRootDir}}' 2>/dev/null)"
    fi
    for path in $paths; do
        [[ -e "$path" ]] || continue
        line="$(df -P "$path" 2>/dev/null | awk 'NR == 2 { sub("%", "", $5); print $5, $4, $6 }')"
        [[ -n "$line" ]] || continue
        pct="${line%% *}"; line="${line#* }"; avail="${line%% *}"; mount="${line#* }"
        case "$seen" in *" $mount "*) continue ;; esac
        seen="$seen$mount "
        local free_gb
        free_gb="$(awk -v k="$avail" 'BEGIN { printf "%.1f", k / 1048576 }')"
        if [[ "$pct" -ge "$DISK_CRIT_PERCENT" ]]; then
            record "disk:$mount" crit "$mount is $pct% full ($free_gb GB free); Docker logs, images and the Redis volume live here"
        elif [[ "$pct" -ge "$DISK_WARN_PERCENT" ]]; then
            record "disk:$mount" warn "$mount is $pct% full ($free_gb GB free)"
        else
            record "disk:$mount" ok "$pct% used, $free_gb GB free"
        fi
    done
}

check_caddy() {
    local source_desc since=$((NOW - CADDY_WINDOW_MINUTES * 60))
    if [[ -n "$CADDY_LOG_FILE" ]]; then
        [[ -r "$CADDY_LOG_FILE" ]] || { record caddy crit "cannot read CADDY_LOG_FILE $CADDY_LOG_FILE"; return; }
        tail -n 200000 "$CADDY_LOG_FILE" >"$TMP/caddy.log"
        source_desc="$CADDY_LOG_FILE"
    else
        if ! has docker; then record caddy skip "docker is not installed; set CADDY_LOG_FILE to read a log file"; return; fi
        if ! docker_ready; then record caddy crit "cannot read Caddy's access log: the Docker daemon is unreachable"; return; fi
        local id
        id="$(docker ps -q --filter "label=com.docker.compose.project=$COMPOSE_PROJECT" \
            --filter "label=com.docker.compose.service=$CADDY_SERVICE" | head -n 1)"
        if [[ -z "$id" ]]; then record caddy crit "no running $CADDY_SERVICE container to read the access log from"; return; fi
        # The Caddyfile logs API requests as JSON to stdout, which Docker keeps
        # in its json-file log; Caddy's own messages go to stderr.
        if ! docker logs --since "${CADDY_WINDOW_MINUTES}m" "$id" >"$TMP/caddy.log" 2>/dev/null; then
            record caddy crit "docker logs failed for the $CADDY_SERVICE container"
            return
        fi
        source_desc="the $CADDY_SERVICE container log"
    fi
    local counts total errors unavailable refused p95
    # Caddy writes one JSON object per line: "ts" (epoch seconds), "status"
    # and "duration" (seconds) at the top level, the URI under "request".
    counts="$(awk -v since="$since" -v durations="$TMP/durations" '
        function number(key,    m) {
            if (!match($0, "\"" key "\": *[0-9.eE+-]+")) return ""
            m = substr($0, RSTART, RLENGTH); sub(/^[^:]*: */, "", m); return m + 0
        }
        $0 !~ /"logger": *"http\.log\.access/ { next }
        {
            if (since > 0) { ts = number("ts"); if (ts != "" && ts < since) next }
            status = number("status")
            total++
            if (status >= 500) errors++
            # WebSocket relay requests last as long as the connection.
            d = number("duration")
            if ($0 !~ /"uri": *"\/ws\// && d != "") print d > durations
            # Purchase validation: 503 means the store check itself is broken
            # (credentials, Google/Apple unreachable); 400 is a refused receipt.
            if ($0 ~ /"uri": *"\/purchase\/validate/) {
                if (status == 503) unavailable++
                else if (status == 400) refused++
            }
        }
        END { print total + 0, errors + 0, unavailable + 0, refused + 0 }' "$TMP/caddy.log")"
    read -r total errors unavailable refused <<<"$counts"
    if [[ "$unavailable" -gt 0 ]]; then
        record purchases crit "$unavailable purchase validation(s) answered 503 in the last $CADDY_WINDOW_MINUTES min: store verification is not working, so paid purchases are not being granted"
    elif [[ "$refused" -ge "$PURCHASE_REFUSED_WARN" ]]; then
        record purchases warn "$refused purchase validations were refused (400) in the last $CADDY_WINDOW_MINUTES min; check the API log for native_purchase_verification_failed"
    else
        record purchases ok "$refused refused, 0 unavailable purchase validations"
    fi
    if [[ "$total" -eq 0 ]]; then
        record caddy ok "no API requests in the last $CADDY_WINDOW_MINUTES min ($source_desc)"
        record latency ok "no API requests to measure"
        return
    fi
    local pct
    pct="$(awk -v e="$errors" -v t="$total" 'BEGIN { printf "%.1f", 100 * e / t }')"
    if [[ "$total" -lt "$FIVEXX_MIN_REQUESTS" ]]; then
        if [[ "$errors" -gt 0 ]]; then
            record caddy warn "$errors of $total API requests answered 5xx in the last $CADDY_WINDOW_MINUTES min (too few requests to judge a rate)"
        else
            record caddy ok "$total requests, no 5xx in the last $CADDY_WINDOW_MINUTES min"
        fi
    elif awk -v p="$pct" -v l="$FIVEXX_CRIT_PERCENT" 'BEGIN { exit !(p >= l) }'; then
        record caddy crit "$pct% of API requests answered 5xx in the last $CADDY_WINDOW_MINUTES min ($errors of $total)"
    elif awk -v p="$pct" -v l="$FIVEXX_WARN_PERCENT" 'BEGIN { exit !(p >= l) }'; then
        record caddy warn "$pct% of API requests answered 5xx in the last $CADDY_WINDOW_MINUTES min ($errors of $total)"
    else
        record caddy ok "$total requests, $errors 5xx ($pct%) in the last $CADDY_WINDOW_MINUTES min"
    fi
    if [[ -s "$TMP/durations" ]]; then
        p95="$(sort -g "$TMP/durations" | awk '{ v[NR] = $1 } END { i = int(NR * 0.95 + 0.999); if (i < 1) i = 1; if (i > NR) i = NR; printf "%d", v[i] * 1000 }')"
        if [[ "$p95" -gt "$LATENCY_P95_WARN_MS" ]]; then
            record latency warn "API p95 latency is ${p95} ms over the last $CADDY_WINDOW_MINUTES min (limit ${LATENCY_P95_WARN_MS} ms)"
        else
            record latency ok "API p95 latency ${p95} ms"
        fi
    fi
}

file_mtime() { stat -c %Y "$1" 2>/dev/null || stat -f %m "$1" 2>/dev/null; }

check_backup() {
    local newest="" when="" size=1 label
    if [[ -n "$BACKUP_CHECK_CMD" ]]; then
        # The command prints the newest backup's time: epoch seconds or ISO 8601.
        local out
        if ! out="$(with_timeout 60 sh -c "$BACKUP_CHECK_CMD" 2>"$TMP/backup.err" | tail -n 1 | tr -d '[:space:]')" || [[ -z "$out" ]]; then
            record backup crit "BACKUP_CHECK_CMD failed: $(head -c 160 "$TMP/backup.err" | tr '\n' ' ')"
            return
        fi
        if [[ "$out" =~ ^[0-9]+$ ]]; then when="$out"; else when="$(to_epoch "$out")" || when=""; fi
        [[ -n "$when" ]] || { record backup crit "BACKUP_CHECK_CMD printed '$out', not a time"; return; }
        label="provider backup"
    elif [[ -n "$BACKUP_DIR" ]]; then
        [[ -d "$BACKUP_DIR" ]] || { record backup crit "BACKUP_DIR $BACKUP_DIR does not exist"; return; }
        local file
        for file in "$BACKUP_DIR"/$BACKUP_GLOB; do
            [[ -f "$file" ]] || continue
            if [[ -z "$newest" ]] || [[ "$(file_mtime "$file")" -gt "$(file_mtime "$newest")" ]]; then newest="$file"; fi
        done
        [[ -n "$newest" ]] || { record backup crit "no backups matching $BACKUP_GLOB in $BACKUP_DIR"; return; }
        when="$(file_mtime "$newest")"
        size="$(wc -c <"$newest" | tr -d ' ')"
        label="$(basename "$newest")"
    else
        record backup skip "no backup location configured (BACKUP_DIR or BACKUP_CHECK_CMD)"
        return
    fi
    local hours
    hours=$(( (NOW - when) / 3600 ))
    if [[ "$size" -eq 0 ]]; then
        record backup crit "the newest backup $label is empty"
    elif [[ $hours -ge $BACKUP_MAX_AGE_HOURS ]]; then
        record backup crit "the newest backup ($label) is $hours hours old (limit $BACKUP_MAX_AGE_HOURS)"
    else
        record backup ok "newest backup $label is $hours hour(s) old"
    fi
}

check_postgres() {
    [[ -n "$PG_MONITOR_URL" ]] || { record postgres skip "PG_MONITOR_URL not set"; return; }
    has psql || { record postgres skip "psql is not installed"; return; }
    local out used max bytes pct
    if ! out="$(PGCONNECT_TIMEOUT=10 psql -X -q -A -t -F ' ' -d "$PG_MONITOR_URL" \
        -c "BEGIN TRANSACTION READ ONLY;" \
        -c "SELECT (SELECT count(*) FROM pg_stat_activity WHERE backend_type = 'client backend'), current_setting('max_connections'), pg_database_size(current_database())" \
        -c "COMMIT;" 2>"$TMP/pg.err")"; then
        record postgres crit "cannot query PostgreSQL: $(head -c 160 "$TMP/pg.err" | tr '\n' ' ')"
        return
    fi
    read -r used max bytes <<<"$out"
    pct=$((100 * used / max))
    local gb
    gb="$(awk -v b="$bytes" 'BEGIN { printf "%.2f", b / 1073741824 }')"
    if [[ $pct -ge $PG_CONN_WARN_PERCENT ]]; then
        record postgres warn "$used of $max PostgreSQL connections in use ($pct%); two API replicas use up to 80"
    elif [[ -n "$PG_MAX_DB_GB" ]] && awk -v g="$gb" -v l="$PG_MAX_DB_GB" 'BEGIN { exit !(g >= l) }'; then
        record postgres warn "database is $gb GB (limit $PG_MAX_DB_GB GB)"
    else
        record postgres ok "$used/$max connections, database $gb GB"
    fi
}

# ── alert delivery ────────────────────────────────────────────────────────

json_escape() {
    local s="$1"
    s="${s//\\/\\\\}"
    s="${s//\"/\\\"}"
    s="${s//$'\n'/\\n}"
    s="${s//$'\r'/\\r}"
    s="${s//$'\t'/\\t}"
    printf '%s' "$s" | LC_ALL=C tr -d '\000-\010\013\014\016-\037'
}

webhook_format() {
    if [[ "$ALERT_FORMAT" != auto ]]; then echo "$ALERT_FORMAT"; return; fi
    case "$ALERT_WEBHOOK_URL" in
        https://hooks.slack.com/*) echo slack ;;
        https://discord.com/api/webhooks/*|https://discordapp.com/api/webhooks/*|https://*.discord.com/api/webhooks/*) echo discord ;;
        https://ntfy.sh/*|*/ntfy/*) echo ntfy ;;
        *) echo generic ;;
    esac
}

# send LEVEL TITLE TEXT ITEMS_JSON -> 0 when the receiver answered 2xx.
send() {
    local level="$1" title="$2" text="$3" items="$4" format code body_file="$TMP/payload"
    if [[ -z "$ALERT_WEBHOOK_URL" ]]; then
        echo "ALERT_WEBHOOK_URL is not set; not sent:" >&2
        printf '%s\n%s\n' "$title" "$text" >&2
        return 1
    fi
    format="$(webhook_format)"
    local -a headers=(-H "User-Agent: crownroad-watch")
    if [[ -n "$ALERT_WEBHOOK_TOKEN" ]]; then headers+=(-H "Authorization: Bearer $ALERT_WEBHOOK_TOKEN"); fi
    case "$format" in
        slack)
            printf '{"text":"%s"}' "$(json_escape "*$title*"$'\n'"$text")" >"$body_file"
            headers+=(-H "Content-Type: application/json") ;;
        discord)
            local content="**$title**"$'\n'"$text"
            printf '{"content":"%s"}' "$(json_escape "${content:0:1900}")" >"$body_file"
            headers+=(-H "Content-Type: application/json") ;;
        ntfy)
            local priority=3 tags=white_check_mark
            case "$level" in crit) priority=5; tags=rotating_light ;; warn) priority=4; tags=warning ;; test) priority=3; tags=bell ;; esac
            printf '%s' "$text" >"$body_file"
            headers+=(-H "Title: $title" -H "Priority: $priority" -H "Tags: $tags" -H "Content-Type: text/plain; charset=utf-8") ;;
        generic)
            printf '{"text":"%s","title":"%s","level":"%s","host":"%s","api":"%s","time":"%s","alerts":[%s]}' \
                "$(json_escape "$title"$'\n'"$text")" "$(json_escape "$title")" "$level" "$(json_escape "$HOST_LABEL")" \
                "$(json_escape "$API_URL")" "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$items" >"$body_file"
            headers+=(-H "Content-Type: application/json") ;;
        *) echo "$SCRIPT_NAME: unknown ALERT_FORMAT $format (use auto, slack, discord, ntfy or generic)" >&2; return 1 ;;
    esac
    code="$(curl -sS -m 15 -o "$TMP/webhook.answer" -w '%{http_code}' -X POST "${headers[@]}" \
        --data-binary "@$body_file" "$ALERT_WEBHOOK_URL" 2>"$TMP/webhook.err")" || code="000"
    LAST_SEND_STATUS="$code"
    if [[ "$code" =~ ^2[0-9][0-9]$ ]]; then return 0; fi
    echo "$SCRIPT_NAME: webhook answered HTTP $code: $(cat "$TMP/webhook.answer" "$TMP/webhook.err" 2>/dev/null | head -c 200 | tr '\n' ' ')" >&2
    return 1
}

level_rank() { case "$1" in crit) echo 2 ;; warn) echo 1 ;; *) echo 0 ;; esac; }
key_file() { printf 'alert.%s' "$(printf '%s' "$1" | tr -c 'A-Za-z0-9._-' '_')"; }

# ── main ──────────────────────────────────────────────────────────────────

if [[ "$MODE" == test ]]; then
    LAST_SEND_STATUS=""
    title="[TEST] $ALERT_NAME alerts from $HOST_LABEL"
    text="Test alert sent at $NOW_TEXT by watch.sh --test for ${API_URL:-the Crownroad API}. If the on-call owner can read this, alerts reach them."
    items="{\"check\":\"test\",\"level\":\"test\",\"message\":\"$(json_escape "$text")\"}"
    if send test "$title" "$text" "$items"; then
        echo "Test alert delivered: $(webhook_format) format, HTTP $LAST_SEND_STATUS, $NOW_TEXT."
        mkdir -p "$STATE_DIR" && echo "$NOW_TEXT HTTP $LAST_SEND_STATUS" >"$STATE_DIR/last-test"
        exit 0
    fi
    echo "Test alert NOT delivered (HTTP ${LAST_SEND_STATUS:-none})." >&2
    exit 1
fi

if [[ "$MODE" == run ]]; then
    mkdir -p "$STATE_DIR" || { echo "$SCRIPT_NAME: cannot create $STATE_DIR" >&2; exit 3; }
    chmod 700 "$STATE_DIR" 2>/dev/null || true
    # One run at a time; a lock older than 15 minutes is from a killed run.
    if ! mkdir "$STATE_DIR/.lock" 2>/dev/null; then
        lock_age=$(( NOW - $(file_mtime "$STATE_DIR/.lock" || echo "$NOW") ))
        if [[ $lock_age -gt 900 ]]; then
            rmdir "$STATE_DIR/.lock" 2>/dev/null; mkdir "$STATE_DIR/.lock" 2>/dev/null || exit 0
        else
            echo "$SCRIPT_NAME: another run is in progress" >&2
            exit 0
        fi
    fi
    LOCK_DIR="$STATE_DIR/.lock"
fi

echo "crownroad watch · ${API_URL:-no API_URL} · $NOW_TEXT"
enabled health && check_health
enabled tls && check_tls
enabled containers && check_containers
enabled disk && check_disk
enabled caddy && check_caddy
enabled backup && check_backup
enabled postgres && check_postgres

# Decide what to send: new or changed problems, reminders, and resolutions.
SEND_LINES=""
SEND_ITEMS=""
SEND_LEVEL=0
SEND_KEYS=()
SEND_STATES=()
problems=0
resolved=0
i=0
while [[ $i -lt ${#R_KEYS[@]} ]]; do
    key="${R_KEYS[$i]}"; level="${R_LEVELS[$i]}"; msg="${R_MSGS[$i]}"
    i=$((i + 1))
    [[ "$level" == skip ]] && continue
    file="$(key_file "$key")"
    prev="$(state_get "$file" || true)"
    prev_level="${prev%% *}"; rest="${prev#* }"; prev_since="${rest%% *}"; prev_alerted="${rest#* }"
    [[ -n "$prev" ]] || { prev_level=ok; prev_since="$NOW"; prev_alerted=0; }
    reason=""
    if [[ "$level" == ok ]]; then
        if [[ "$prev_level" != ok && "$prev_alerted" -gt 0 && "$key" != event:* ]]; then
            reason="resolved"
        fi
        new_state="ok $NOW 0"
    else
        since="$prev_since"
        [[ "$prev_level" == ok ]] && since="$NOW"
        if [[ "$prev_level" != "$level" ]]; then
            reason="new"
        elif [[ $((NOW - prev_alerted)) -ge $((REALERT_MINUTES * 60)) ]]; then
            reason="reminder"
        fi
        new_state="$level $since $prev_alerted"
    fi
    if [[ -n "$reason" ]]; then
        tag="$(echo "$level" | tr '[:lower:]' '[:upper:]')"
        line="$msg"
        case "$reason" in
            resolved) tag="RESOLVED"; problems=$problems; resolved=$((resolved + 1)) ;;
            reminder) line="$msg (since $(date -u -r "$since" '+%H:%M UTC' 2>/dev/null || date -u -d "@$since" '+%H:%M UTC' 2>/dev/null))"; problems=$((problems + 1)) ;;
            *) problems=$((problems + 1)) ;;
        esac
        SEND_LINES="$SEND_LINES• $tag ${key#event:}: $line"$'\n'
        SEND_ITEMS="$SEND_ITEMS${SEND_ITEMS:+,}{\"check\":\"$(json_escape "$key")\",\"level\":\"$(echo "$tag" | tr '[:upper:]' '[:lower:]')\",\"message\":\"$(json_escape "$line")\"}"
        rank="$(level_rank "$level")"
        [[ $rank -gt $SEND_LEVEL ]] && SEND_LEVEL=$rank
        # Marked as alerted only once the webhook accepts it.
        if [[ "$level" == ok ]]; then
            SEND_STATES+=("ok $NOW 0")
        else
            SEND_STATES+=("$level $since $NOW")
        fi
        SEND_KEYS+=("$file")
    fi
    if [[ "$MODE" == run ]]; then state_put "$file" "$new_state"; fi
done

if [[ -n "$SEND_LINES" ]]; then
    case $SEND_LEVEL in 2) level=crit ;; 1) level=warn ;; *) level=ok ;; esac
    summary=""
    [[ $problems -gt 0 ]] && summary="$problems problem(s)"
    [[ $resolved -gt 0 ]] && summary="${summary:+$summary, }$resolved resolved"
    title="[$(echo "$level" | tr '[:lower:]' '[:upper:]' | sed 's/^OK$/RESOLVED/')] $ALERT_NAME on $HOST_LABEL: $summary"
    text="${SEND_LINES}${API_URL:+API $API_URL · }$NOW_TEXT"
    if [[ "$MODE" == dry ]]; then
        echo "--- would send ($(webhook_format) format) ---"
        echo "$title"
        echo "$text"
    elif send "$level" "$title" "$text" "$SEND_ITEMS"; then
        echo "Alert sent (HTTP $LAST_SEND_STATUS): $title"
        j=0
        while [[ $j -lt ${#SEND_KEYS[@]} ]]; do
            state_put "${SEND_KEYS[$j]}" "${SEND_STATES[$j]}"
            j=$((j + 1))
        done
    else
        echo "Alert NOT sent; it will be retried on the next run." >&2
    fi
fi

if [[ "$MODE" == run && -n "$HEARTBEAT_URL" ]]; then
    # A dead man's switch: an external monitor alerts when these pings stop,
    # which covers this VM (and this script) being down.
    curl -fsS -m 10 -o /dev/null "$HEARTBEAT_URL" 2>/dev/null || echo "$SCRIPT_NAME: heartbeat ping failed" >&2
fi

exit $WORST
