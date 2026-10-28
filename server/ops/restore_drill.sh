#!/usr/bin/env bash
# Timed restore drill for the Crownroad PostgreSQL database.
#
# Dumps the source database (read-only, pg_dump custom format) or takes an
# existing dump, restores it into a separate scratch database, times each
# phase, compares the key tables' row counts (and the wallet/ledger sums) with
# the source, and writes a short report to keep as launch evidence.
#
#   DRILL_SOURCE_URL=postgresql://... DRILL_TARGET_URL=postgresql://... ./restore_drill.sh
#   ./restore_drill.sh --dump backups/crownroad-20261028T0300Z.dump --target-url postgresql://...
#
# Database URLs are read from DRILL_SOURCE_URL / DRILL_TARGET_URL (preferred:
# they stay out of `ps` and shell history) or --source-url / --target-url.
# Passwords are moved into a private temporary passfile; no password is passed
# on a command line. See server/ops/README.md.
#
# Exit codes: 0 restore verified, 1 restore failed or counts differ,
# 2 refused (usage, same database, non-empty target, version mismatch).
#
# Written for bash 3.2 (macOS) and later.
set -euo pipefail
umask 077

SCRIPT_NAME="$(basename "$0")"

SOURCE_URL="${DRILL_SOURCE_URL:-}"
TARGET_URL="${DRILL_TARGET_URL:-}"
DUMP_FILE=""
EXPECTED_COUNTS=""
KEEP_DUMP_DIR=""
REPORT_FILE=""
JOBS=2
CLEAN_TARGET=0
USE_SNAPSHOT=1
LOCK_WAIT="120s"

# label|expression|table. Counts of the tables that hold players' money and
# progress, plus sums that catch a partially copied ledger.
CHECKS="players|count(*)|players
purchases|count(*)|purchases
player_wallets|count(*)|player_wallets
wallet_ledger|count(*)|wallet_ledger
wallet_adjustments|count(*)|wallet_adjustments
store_events|count(*)|store_events
cloud_saves|count(*)|cloud_saves
account_identities|count(*)|account_identities
auth_sessions|count(*)|auth_sessions
wallet_ledger sum(amount)|coalesce(sum(amount),0)|wallet_ledger
player_wallets sum(gold_balance)|coalesce(sum(gold_balance),0)|player_wallets
player_wallets sum(food_balance)|coalesce(sum(food_balance),0)|player_wallets
schema_version max(version)|coalesce(max(version),0)|schema_version"

usage() {
    cat <<EOF
Usage: $SCRIPT_NAME [options]

Source (one of):
  --source-url URL       Database to dump, read-only (or set DRILL_SOURCE_URL).
                         Prefer the provider's direct, non-pooled URL and a
                         read-only role.
  --dump FILE            Existing pg_dump custom-format file to restore instead.
  --expected-counts FILE Counts recorded when FILE was dumped (default: FILE.counts,
                         written by this script with --keep-dump).
Target:
  --target-url URL       Empty scratch database to restore into (or DRILL_TARGET_URL).
  --clean-target         Allow a non-empty target: drop the dump's objects first.
Options:
  --keep-dump DIR        Keep the dump and its .counts file in DIR (they hold
                         personal data: encrypt them off-host or delete them).
  --jobs N               Parallel restore jobs (default $JOBS).
  --report FILE          Report path (default restore-drill-<UTC time>.txt here).
  --no-snapshot          Count the source outside the dump's snapshot (for a
                         pooler that cannot export snapshots); live writes can
                         then show as small differences.
  -h, --help             This help.

Set PG_BIN to the directory of the PostgreSQL client tools if they are not on
PATH. pg_dump must be at least the source server's major version.
EOF
}

die() { echo "$SCRIPT_NAME: $*" >&2; exit 2; }
fail() { echo "$SCRIPT_NAME: $*" >&2; exit 1; }

while [[ $# -gt 0 ]]; do
    case "$1" in
        --source-url) SOURCE_URL="${2:?--source-url needs a value}"; shift 2 ;;
        --target-url) TARGET_URL="${2:?--target-url needs a value}"; shift 2 ;;
        --dump) DUMP_FILE="${2:?--dump needs a file}"; shift 2 ;;
        --expected-counts) EXPECTED_COUNTS="${2:?--expected-counts needs a file}"; shift 2 ;;
        --keep-dump) KEEP_DUMP_DIR="${2:?--keep-dump needs a directory}"; shift 2 ;;
        --report) REPORT_FILE="${2:?--report needs a file}"; shift 2 ;;
        --jobs) JOBS="${2:?--jobs needs a number}"; shift 2 ;;
        --clean-target) CLEAN_TARGET=1; shift ;;
        --no-snapshot) USE_SNAPSHOT=0; shift ;;
        -h|--help) usage; exit 0 ;;
        *) usage >&2; die "unknown option: $1" ;;
    esac
done

[[ -n "$TARGET_URL" ]] || { usage >&2; die "a target database is required (--target-url or DRILL_TARGET_URL)"; }
if [[ -n "$DUMP_FILE" && -n "$SOURCE_URL" ]]; then
    die "give either a source database or a dump file, not both (expected counts come from the dump's .counts file)"
fi
[[ -n "$DUMP_FILE" || -n "$SOURCE_URL" ]] || { usage >&2; die "a source database or a dump file is required"; }
[[ "$JOBS" =~ ^[1-9][0-9]*$ ]] || die "--jobs must be a positive number"

if [[ -n "${PG_BIN:-}" ]]; then PATH="$PG_BIN:$PATH"; fi
for tool in psql pg_restore; do
    command -v "$tool" >/dev/null 2>&1 || die "$tool not found; install the PostgreSQL client tools or set PG_BIN"
done
if [[ -n "$SOURCE_URL" ]]; then
    command -v pg_dump >/dev/null 2>&1 || die "pg_dump not found; install the PostgreSQL client tools or set PG_BIN"
fi

WORK="$(mktemp -d "${TMPDIR:-/tmp}/crownroad-drill.XXXXXX")"
HOLDER_PID=""
cleanup() {
    if [[ -n "$HOLDER_PID" ]]; then
        { exec 3>&-; } 2>/dev/null || true
        kill "$HOLDER_PID" 2>/dev/null || true
        wait "$HOLDER_PID" 2>/dev/null || true
    fi
    rm -rf "$WORK"
}
trap cleanup EXIT
trap 'exit 130' INT TERM

# ── time ──────────────────────────────────────────────────────────────────

now() {
    if [[ -n "${EPOCHREALTIME:-}" ]]; then echo "${EPOCHREALTIME/,/.}"; return; fi
    local t
    t="$(date +%s.%N 2>/dev/null || true)"
    if [[ "$t" =~ ^[0-9]+\.[0-9]+$ ]]; then echo "$t"; return; fi
    python3 -c 'import time; print("%.3f" % time.time())' 2>/dev/null || date +%s
}
seconds_between() { awk -v a="$1" -v b="$2" 'BEGIN { printf "%.1f", b - a }'; }

# ── URL handling ──────────────────────────────────────────────────────────

urldecode() {
    local s="${1//+/%2B}"
    printf '%b' "${s//%/\\x}"
}

# Quotes a libpq keyword value.
ci_quote() {
    local v="$1"
    v="${v//\\/\\\\}"
    v="${v//\'/\\\'}"
    printf "'%s'" "$v"
}

# Escapes a pgpass field.
pgpass_field() {
    local v="$1"
    v="${v//\\/\\\\}"
    v="${v//:/\\:}"
    printf '%s' "$v"
}

# parse_url PREFIX URL: sets PREFIX_HOST, _PORT, _DB, _USER, _PASS, _CONNINFO
# (no password) and _DISPLAY (redacted). Query options that libpq does not
# know, like the API's pooling settings, are dropped.
parse_url() {
    local prefix="$1" url="$2" rest query="" path="" userinfo="" hostport host port user pass="" db
    case "$url" in
        postgres://*|postgresql://*) ;;
        *) die "$prefix database must be a postgres:// or postgresql:// URL" ;;
    esac
    rest="${url#*://}"
    if [[ "$rest" == *\?* ]]; then query="${rest#*\?}"; rest="${rest%%\?*}"; fi
    if [[ "$rest" == */* ]]; then path="${rest#*/}"; rest="${rest%%/*}"; fi
    if [[ "$rest" == *@* ]]; then userinfo="${rest%@*}"; rest="${rest##*@}"; fi
    hostport="$rest"
    [[ "$hostport" != *,* ]] || die "$prefix URL lists several hosts; give a single host"
    if [[ "$hostport" == \[* ]]; then
        host="${hostport#\[}"; host="${host%%\]*}"
        port="${hostport##*\]}"; port="${port#:}"
    else
        host="${hostport%%:*}"; port=""
        if [[ "$hostport" == *:* ]]; then port="${hostport##*:}"; fi
    fi
    [[ -n "$host" ]] || die "$prefix URL has no host"
    [[ -z "$port" || "$port" =~ ^[0-9]+$ ]] || die "$prefix URL has a bad port"
    port="${port:-5432}"
    user="$(urldecode "${userinfo%%:*}")"
    if [[ "$userinfo" == *:* ]]; then pass="$(urldecode "${userinfo#*:}")"; fi
    db="$(urldecode "$path")"
    [[ -n "$db" ]] || die "$prefix URL has no database name"

    local conninfo="host=$(ci_quote "$host") port=$port dbname=$(ci_quote "$db")"
    if [[ -n "$user" ]]; then conninfo="$conninfo user=$(ci_quote "$user")"; fi
    local pair key value old_ifs="$IFS"
    IFS='&'
    set -f
    for pair in $query; do
        key="${pair%%=*}"; value=""
        if [[ "$pair" == *=* ]]; then value="$(urldecode "${pair#*=}")"; fi
        case "$key" in
            sslmode|sslrootcert|sslcert|sslkey|sslcrl|sslnegotiation|sslsni|channel_binding|gssencmode|connect_timeout|target_session_attrs|keepalives|keepalives_idle|options|application_name)
                conninfo="$conninfo $key=$(ci_quote "$value")" ;;
        esac
    done
    set +f
    IFS="$old_ifs"
    # Name the drill's connections so the provider's activity view shows them.
    if [[ "$conninfo" != *application_name=* ]]; then conninfo="$conninfo application_name='crownroad-restore-drill'"; fi

    printf -v "${prefix}_HOST" '%s' "$host"
    printf -v "${prefix}_PORT" '%s' "$port"
    printf -v "${prefix}_DB" '%s' "$db"
    printf -v "${prefix}_USER" '%s' "$user"
    printf -v "${prefix}_PASS" '%s' "$pass"
    printf -v "${prefix}_CONNINFO" '%s' "$conninfo"
    printf -v "${prefix}_DISPLAY" '%s' "${user:+$user@}$host:$port/$db"
}

PASSFILE="$WORK/pgpass"
: >"$PASSFILE"
add_password() {
    local host="$1" port="$2" db="$3" user="$4" pass="$5"
    [[ -n "$pass" ]] || return 0
    printf '%s:%s:%s:%s:%s\n' "$(pgpass_field "$host")" "$port" "$(pgpass_field "$db")" \
        "$(pgpass_field "${user:-*}")" "$(pgpass_field "$pass")" >>"$PASSFILE"
}

TGT_HOST="" TGT_PORT="" TGT_DB="" TGT_USER="" TGT_PASS="" TGT_CONNINFO="" TGT_DISPLAY=""
SRC_HOST="" SRC_PORT="" SRC_DB="" SRC_USER="" SRC_PASS="" SRC_CONNINFO="" SRC_DISPLAY=""
parse_url TGT "$TARGET_URL"
add_password "$TGT_HOST" "$TGT_PORT" "$TGT_DB" "$TGT_USER" "$TGT_PASS"
if [[ -n "$SOURCE_URL" ]]; then
    parse_url SRC "$SOURCE_URL"
    add_password "$SRC_HOST" "$SRC_PORT" "$SRC_DB" "$SRC_USER" "$SRC_PASS"
fi
# Keep any passwords the operator already has in a passfile.
for existing in "${PGPASSFILE:-}" "$HOME/.pgpass"; do
    if [[ -n "$existing" && -r "$existing" ]]; then cat "$existing" >>"$PASSFILE"; break; fi
done
export PGPASSFILE="$PASSFILE"
unset PGPASSWORD PGHOST PGPORT PGDATABASE PGUSER PGSERVICE 2>/dev/null || true
export PGCONNECT_TIMEOUT="${PGCONNECT_TIMEOUT:-15}"
unset SOURCE_URL TARGET_URL DRILL_SOURCE_URL DRILL_TARGET_URL SRC_PASS TGT_PASS

lower() { printf '%s' "$1" | tr '[:upper:]' '[:lower:]'; }

# ── database helpers ──────────────────────────────────────────────────────

# Every statement against the source runs inside a READ ONLY transaction.
src_query() {
    psql -X -q -A -t -v ON_ERROR_STOP=1 -d "$SRC_CONNINFO" \
        -c "BEGIN TRANSACTION READ ONLY;" -c "$1" -c "COMMIT;"
}
tgt_query() {
    psql -X -q -A -t -v ON_ERROR_STOP=1 -d "$TGT_CONNINFO" -c "$1"
}

# Step 1: where each checked table lives ("table<TAB>schema"), preferring public.
tables_sql() {
    local names="" line table
    while IFS= read -r line; do
        [[ -n "$line" ]] || continue
        table="${line##*|}"
        names="$names${names:+, }'$table'"
    done <<EOF
$CHECKS
EOF
    cat <<SQL
SELECT DISTINCT ON (c.relname) c.relname || E'\t' || n.nspname
FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE c.relname IN ($names) AND c.relkind IN ('r', 'p')
  AND n.nspname NOT IN ('pg_catalog', 'information_schema') AND n.nspname NOT LIKE 'pg_toast%'
ORDER BY c.relname, n.nspname = 'public' DESC
SQL
}

quote_ident() { printf '"%s"' "${1//\"/\"\"}"; }

# Step 2: one query returning "label<TAB>value" per check; "absent" when the
# table does not exist (an older schema). Reads step 1's output file.
counts_sql() {
    local tables_file="$1" parts="" ord=0 label expr table schema line
    while IFS= read -r line; do
        [[ -n "$line" ]] || continue
        label="${line%%|*}"; line="${line#*|}"; expr="${line%%|*}"; table="${line#*|}"
        ord=$((ord + 1))
        schema="$(awk -F '\t' -v t="$table" '$1 == t { print $2 }' "$tables_file")"
        if [[ -n "$schema" ]]; then
            parts="$parts${parts:+ UNION ALL }SELECT $ord AS ord, '$label' || E'\t' || (SELECT $expr FROM $(quote_ident "$schema").$(quote_ident "$table"))::text AS line"
        else
            parts="$parts${parts:+ UNION ALL }SELECT $ord AS ord, '$label' || E'\t' || 'absent' AS line"
        fi
    done <<EOF
$CHECKS
EOF
    echo "SELECT line FROM ($parts) AS checks ORDER BY ord"
}

# Runs both steps with a query function (src_query or tgt_query); prints the values.
collect_counts() {
    local runner="$1" tables_file="$WORK/$2.tables"
    "$runner" "$(tables_sql)" >"$tables_file" || return 1
    "$runner" "$(counts_sql "$tables_file")"
}

# Returns "database|system identifier or empty|postmaster start time|server version number".
identity_sql_full="SELECT current_database() || '|' || (SELECT system_identifier::text FROM pg_control_system()) || '|' || pg_postmaster_start_time()::text || '|' || current_setting('server_version_num')"
identity_sql_basic="SELECT current_database() || '||' || pg_postmaster_start_time()::text || '|' || current_setting('server_version_num')"

src_identity() { src_query "$identity_sql_full" 2>/dev/null || src_query "$identity_sql_basic"; }
tgt_identity() { tgt_query "$identity_sql_full" 2>/dev/null || tgt_query "$identity_sql_basic"; }

wait_for_file() {
    local file="$1" seconds="$2" waited=0
    while [[ ! -e "$file" ]]; do
        if [[ -n "$HOLDER_PID" ]] && ! kill -0 "$HOLDER_PID" 2>/dev/null; then
            echo "The source session ended early:" >&2
            sed 's/^/  /' "$WORK/holder.log" >&2 || true
            return 1
        fi
        sleep 0.2
        waited=$((waited + 1))
        if [[ $waited -gt $((seconds * 5)) ]]; then
            echo "Timed out waiting for the source session." >&2
            return 1
        fi
    done
}

# ── preflight ─────────────────────────────────────────────────────────────

STARTED_AT="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
STAMP="$(date -u +%Y%m%dT%H%M%SZ)"
REPORT_FILE="${REPORT_FILE:-restore-drill-$STAMP.txt}"
T0="$(now)"

echo "Restore drill $STARTED_AT"
echo "  target: $TGT_DISPLAY"
if [[ -n "$DUMP_FILE" ]]; then echo "  source: dump file $DUMP_FILE"; else echo "  source: $SRC_DISPLAY (read-only)"; fi

TGT_ID="$(tgt_identity)" || fail "cannot connect to the target database"
TGT_ID_DB="${TGT_ID%%|*}"; rest="${TGT_ID#*|}"
TGT_ID_SYS="${rest%%|*}"; rest="${rest#*|}"
TGT_ID_START="${rest%%|*}"; TGT_VERSION_NUM="${rest#*|}"

SRC_VERSION_NUM=""
if [[ -z "$DUMP_FILE" ]]; then
    if [[ "$(lower "$SRC_HOST")" == "$(lower "$TGT_HOST")" && "$SRC_PORT" == "$TGT_PORT" && "$SRC_DB" == "$TGT_DB" ]]; then
        die "refusing: the target URL names the source database ($SRC_DISPLAY)"
    fi
    SRC_ID="$(src_identity)" || fail "cannot connect to the source database"
    SRC_ID_DB="${SRC_ID%%|*}"; rest="${SRC_ID#*|}"
    SRC_ID_SYS="${rest%%|*}"; rest="${rest#*|}"
    SRC_ID_START="${rest%%|*}"; SRC_VERSION_NUM="${rest#*|}"
    # Same cluster: the same system identifier, or (without permission to read
    # it) the same postmaster start time. Same cluster and database name means
    # the same database, whatever host name or pooler reached it.
    same_cluster=0
    if [[ -n "$SRC_ID_SYS" && -n "$TGT_ID_SYS" ]]; then
        [[ "$SRC_ID_SYS" == "$TGT_ID_SYS" ]] && same_cluster=1
    elif [[ "$SRC_ID_START" == "$TGT_ID_START" ]]; then
        same_cluster=1
    fi
    if [[ $same_cluster -eq 1 && "$SRC_ID_DB" == "$TGT_ID_DB" ]]; then
        die "refusing: source and target are the same database ($SRC_ID_DB on one server)"
    fi
    if [[ $same_cluster -eq 1 ]]; then
        echo "  note: target is another database on the source's server; the restore competes with production for its resources"
    fi
    PGDUMP_MAJOR="$(pg_dump --version | sed -E 's/.* ([0-9]+)(\.[0-9]+)*.*/\1/')"
    SRC_MAJOR=$((SRC_VERSION_NUM / 10000))
    if [[ "$PGDUMP_MAJOR" -lt "$SRC_MAJOR" ]]; then
        die "pg_dump $PGDUMP_MAJOR is older than the source server ($SRC_MAJOR); install PostgreSQL $SRC_MAJOR client tools or set PG_BIN"
    fi
    if [[ $((TGT_VERSION_NUM / 10000)) -lt "$SRC_MAJOR" ]]; then
        echo "  warning: target server $((TGT_VERSION_NUM / 10000)) is older than the source ($SRC_MAJOR); restore may fail"
    fi
fi

TGT_TABLES="$(tgt_query "SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace WHERE c.relkind IN ('r','p') AND n.nspname NOT IN ('pg_catalog','information_schema') AND n.nspname NOT LIKE 'pg_toast%'")"
if [[ "$TGT_TABLES" -gt 0 && $CLEAN_TARGET -eq 0 ]]; then
    die "refusing: the target already has $TGT_TABLES table(s); use an empty scratch database or pass --clean-target"
fi
T_PREFLIGHT="$(now)"

# ── dump ──────────────────────────────────────────────────────────────────

SOURCE_COUNTS="$WORK/source.counts"
DUMP_SECONDS="-"
SNAPSHOT_NOTE=""
if [[ -z "$DUMP_FILE" ]]; then
    if [[ -n "$KEEP_DUMP_DIR" ]]; then
        mkdir -p "$KEEP_DUMP_DIR"
        DUMP_FILE="$KEEP_DUMP_DIR/crownroad-$STAMP.dump"
    else
        DUMP_FILE="$WORK/crownroad-$STAMP.dump"
    fi
    PGDUMP_SNAPSHOT=()
    T_DUMP_START="$(now)"
    if [[ $USE_SNAPSHOT -eq 1 ]]; then
        # Hold one read-only transaction open, export its snapshot, count the
        # source in it and let pg_dump read the very same snapshot, so the
        # counts match the dump exactly even while players keep writing.
        mkfifo "$WORK/holder.in"
        psql -X -q -A -t -v ON_ERROR_STOP=1 -d "$SRC_CONNINFO" <"$WORK/holder.in" >"$WORK/holder.log" 2>&1 &
        HOLDER_PID=$!
        exec 3>"$WORK/holder.in"
        {
            echo "BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ, READ ONLY;"
            echo "SELECT pg_export_snapshot() \\g '$WORK/snapshot'"
            echo "\\! touch '$WORK/snapshot.ready'"
        } >&3
        wait_for_file "$WORK/snapshot.ready" 60 || fail "could not export a snapshot from the source; retry with --no-snapshot"
        SNAPSHOT="$(tr -d '[:space:]' <"$WORK/snapshot")"
        [[ -n "$SNAPSHOT" ]] || fail "the source returned no snapshot id; retry with --no-snapshot"
        {
            tables_sql
            echo " \\g '$WORK/source.tables'"
            echo "\\! touch '$WORK/tables.ready'"
        } >&3
        wait_for_file "$WORK/tables.ready" 60 || fail "could not list the source tables"
        {
            counts_sql "$WORK/source.tables"
            echo " \\g '$SOURCE_COUNTS'"
            echo "\\! touch '$WORK/counts.ready'"
        } >&3
        wait_for_file "$WORK/counts.ready" 600 || fail "could not count the source tables"
        PGDUMP_SNAPSHOT=(--snapshot="$SNAPSHOT")
        SNAPSHOT_NOTE="source counted in the dump's own snapshot ($SNAPSHOT)"
    else
        collect_counts src_query source >"$SOURCE_COUNTS" || fail "could not count the source tables"
        SNAPSHOT_NOTE="source counted just before the dump (--no-snapshot): live writes may show as differences"
    fi
    echo "Dumping the source..."
    pg_dump --format=custom --no-owner --no-acl --lock-wait-timeout="$LOCK_WAIT" \
        ${PGDUMP_SNAPSHOT[@]+"${PGDUMP_SNAPSHOT[@]}"} --file="$DUMP_FILE" --dbname="$SRC_CONNINFO" \
        || fail "pg_dump failed"
    if [[ -n "$HOLDER_PID" ]]; then
        echo "COMMIT;" >&3
        exec 3>&-
        wait "$HOLDER_PID" 2>/dev/null || true
        HOLDER_PID=""
    fi
    T_DUMP_END="$(now)"
    DUMP_SECONDS="$(seconds_between "$T_DUMP_START" "$T_DUMP_END")"
    if [[ -n "$KEEP_DUMP_DIR" ]]; then
        cp "$SOURCE_COUNTS" "$DUMP_FILE.counts"
        chmod 600 "$DUMP_FILE" "$DUMP_FILE.counts"
    fi
else
    [[ -r "$DUMP_FILE" ]] || die "cannot read dump file $DUMP_FILE"
    pg_restore --list "$DUMP_FILE" >/dev/null 2>&1 || die "$DUMP_FILE is not a pg_dump custom-format archive"
    EXPECTED_COUNTS="${EXPECTED_COUNTS:-$DUMP_FILE.counts}"
    if [[ -r "$EXPECTED_COUNTS" ]]; then
        cp "$EXPECTED_COUNTS" "$SOURCE_COUNTS"
        SNAPSHOT_NOTE="expected counts from $EXPECTED_COUNTS"
    else
        SNAPSHOT_NOTE="no expected counts for this dump ($EXPECTED_COUNTS missing): restore not verifiable"
    fi
fi

DUMP_BYTES="$(wc -c <"$DUMP_FILE" | tr -d ' ')"
if command -v sha256sum >/dev/null 2>&1; then
    DUMP_SHA="$(sha256sum "$DUMP_FILE" | cut -d' ' -f1)"
else
    DUMP_SHA="$(shasum -a 256 "$DUMP_FILE" | cut -d' ' -f1)"
fi

# ── restore ───────────────────────────────────────────────────────────────

echo "Restoring into the target..."
RESTORE_FLAGS=(--no-owner --no-acl --exit-on-error --jobs="$JOBS")
if [[ $CLEAN_TARGET -eq 1 ]]; then RESTORE_FLAGS+=(--clean --if-exists); fi
T_RESTORE_START="$(now)"
RESTORE_OK=1
if ! pg_restore "${RESTORE_FLAGS[@]}" --dbname="$TGT_CONNINFO" "$DUMP_FILE" 2>"$WORK/restore.log"; then
    RESTORE_OK=0
fi
T_RESTORE_END="$(now)"

ANALYZE_SECONDS="-"
if [[ $RESTORE_OK -eq 1 ]]; then
    # A restored database has no planner statistics until it is analyzed;
    # the API would run slowly until then, so this is part of the recovery time.
    echo "Analyzing the restored tables..."
    T_ANALYZE_START="$(now)"
    # Catalogs the restoring role does not own are skipped quietly.
    tgt_query "SET client_min_messages = error; ANALYZE" >/dev/null || RESTORE_OK=0
    T_ANALYZE_END="$(now)"
    ANALYZE_SECONDS="$(seconds_between "$T_ANALYZE_START" "$T_ANALYZE_END")"
fi

# ── verify ────────────────────────────────────────────────────────────────

T_VERIFY_START="$(now)"
TARGET_COUNTS="$WORK/target.counts"
: >"$TARGET_COUNTS"
if [[ $RESTORE_OK -eq 1 ]]; then
    collect_counts tgt_query target >"$TARGET_COUNTS" || RESTORE_OK=0
fi
T_VERIFY_END="$(now)"

lookup() { awk -F '\t' -v k="$1" '$1 == k { print $2; found = 1 } END { if (!found) print "" }' "$2"; }

MISMATCHES=0
COMPARED=0
TABLE_LINES=""
while IFS= read -r line; do
    [[ -n "$line" ]] || continue
    label="${line%%|*}"
    src_value="$(lookup "$label" "$SOURCE_COUNTS")"
    tgt_value="$(lookup "$label" "$TARGET_COUNTS")"
    if [[ -z "$src_value" ]]; then
        verdict="-"
    elif [[ "$src_value" == "$tgt_value" ]]; then
        verdict="match"; COMPARED=$((COMPARED + 1))
    else
        verdict="DIFFERS"; MISMATCHES=$((MISMATCHES + 1)); COMPARED=$((COMPARED + 1))
    fi
    TABLE_LINES="$TABLE_LINES$(printf '  %-34s %14s %14s  %s' "$label" "${src_value:--}" "${tgt_value:--}" "$verdict")
"
done <<EOF
$CHECKS
EOF

T_END="$(now)"
RESTORE_SECONDS="$(seconds_between "$T_RESTORE_START" "$T_RESTORE_END")"
VERIFY_SECONDS="$(seconds_between "$T_VERIFY_START" "$T_VERIFY_END")"
RECOVERY_SECONDS="$(seconds_between "$T_RESTORE_START" "$T_VERIFY_END")"
TOTAL_SECONDS="$(seconds_between "$T0" "$T_END")"
PREFLIGHT_SECONDS="$(seconds_between "$T0" "$T_PREFLIGHT")"

if [[ $RESTORE_OK -eq 0 ]]; then
    RESULT="FAIL: the restore did not complete (see the pg_restore errors below)"
    EXIT=1
elif [[ $MISMATCHES -gt 0 ]]; then
    RESULT="FAIL: $MISMATCHES value(s) differ between source and target"
    EXIT=1
elif [[ $COMPARED -eq 0 ]]; then
    RESULT="NOT VERIFIED: restored, but there were no source counts to compare"
    EXIT=1
else
    RESULT="PASS: restored and all $COMPARED compared values match"
    EXIT=0
fi

{
    echo "Crownroad restore drill"
    echo "======================="
    echo "Started:      $STARTED_AT (UTC)"
    echo "Operator:     $(id -un)@$(hostname)"
    if [[ -n "$SRC_DISPLAY" ]]; then
        echo "Source:       $SRC_DISPLAY (PostgreSQL $((SRC_VERSION_NUM / 10000)), read-only pg_dump)"
    else
        echo "Source:       dump file $DUMP_FILE"
    fi
    echo "Target:       $TGT_DISPLAY (PostgreSQL $((TGT_VERSION_NUM / 10000)), database $TGT_ID_DB)"
    echo "Tools:        $(pg_restore --version)"
    echo "Dump:         $(basename "$DUMP_FILE"), $DUMP_BYTES bytes, sha256 $DUMP_SHA"
    if [[ -n "$KEEP_DUMP_DIR" ]]; then echo "              kept at $DUMP_FILE (personal data: encrypt off-host or delete)"; fi
    echo "Counts:       $SNAPSHOT_NOTE"
    echo
    echo "Phase timings (seconds)"
    printf '  %-34s %8s\n' "preflight checks" "$PREFLIGHT_SECONDS"
    printf '  %-34s %8s\n' "dump (pg_dump, custom format)" "$DUMP_SECONDS"
    printf '  %-34s %8s\n' "restore (pg_restore, $JOBS jobs)" "$RESTORE_SECONDS"
    printf '  %-34s %8s\n' "analyze" "$ANALYZE_SECONDS"
    printf '  %-34s %8s\n' "verify counts" "$VERIFY_SECONDS"
    printf '  %-34s %8s\n' "recovery (restore to verified)" "$RECOVERY_SECONDS"
    printf '  %-34s %8s\n' "total" "$TOTAL_SECONDS"
    echo
    echo "Key tables"
    printf '  %-34s %14s %14s  %s\n' "table / value" "source" "target" "result"
    printf '%s' "$TABLE_LINES"
    echo
    if [[ -s "$WORK/restore.log" ]]; then
        echo "pg_restore messages"
        sed 's/^/  /' "$WORK/restore.log" | head -40
        echo
    fi
    echo "Result: $RESULT"
    echo "Next: point a staging API at the target and check /health, then drop the scratch database."
} | tee "$REPORT_FILE"
echo "Report saved to $REPORT_FILE"
exit "$EXIT"
