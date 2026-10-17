#!/usr/bin/env bash
# Runs the load test against the load stack (TASK-086, docs/testing/load-test.md §7): the e2e stack with the load overlay
# (compose.yml here), on a fresh database carrying the representative volume.
#
#   docs/testing/load-test/run.sh                          # build from this tree, fresh database, acceptance profile, report
#   LOAD_PROFILE=saturation docs/testing/load-test/run.sh  # or smoke
#   LOAD_REUSE_STACK=1 docs/testing/load-test/run.sh       # against the load stack already up, without rebuilding or resetting
#
# Results land in artifacts/load-test/<profile>-<UTC time>/: every request (requests.csv.gz), k6's summary, the statements
# PostgreSQL executed during the run (statements.csv), the containers' CPU and memory every 10 s (resources.csv), and the
# report load-test-report.py writes from them (report.md). The stack stays up afterwards; remove it with
#   docker compose -f src/frontend/e2e/stack/compose.yml -f docs/testing/load-test/compose.yml down -v
# (never --remove-orphans: docs/testing/e2e-suite.md §7). Exits with k6's status if it failed, else load-test-report.py's.
set -euo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo="$(cd "$here/../../.." && pwd)"
compose=(docker compose -f "$repo/src/frontend/e2e/stack/compose.yml" -f "$here/compose.yml")
profile="${LOAD_PROFILE:-acceptance}"
name="$profile-$(date -u +%Y%m%dT%H%M%SZ)"
results="$repo/artifacts/load-test/$name"
mkdir -p "$results"
# k6's image runs as an unprivileged user, which on Linux cannot write into a folder the runner's user owns.
chmod a+rwx "$results"

if [[ "${LOAD_REUSE_STACK:-}" != "1" ]]; then
  "${compose[@]}" down --volumes 2>/dev/null || true
  started=$(date +%s)
  "${compose[@]}" up --detach --build --wait --quiet-pull e2e-api
  echo "stack up with the volume loaded in $(($(date +%s) - started)) s"
fi

psql() { docker exec AHDA-e2e-postgres psql --no-psqlrc --quiet --username=pmplatform --dbname=pmplatform "$@"; }
psql --command='CREATE EXTENSION IF NOT EXISTS pg_stat_statements' --command='SELECT pg_stat_statements_reset()' >/dev/null

# The API's and the database's CPU and memory, sampled through the run.
(
  echo 'time,container,cpu_percent,memory'
  while true; do
    docker stats --no-stream --format '{{.Name}},{{.CPUPerc}},{{.MemUsage}}' AHDA-e2e-api AHDA-e2e-postgres 2>/dev/null |
      sed "s/^/$(date -u +%H:%M:%S),/"
    sleep 10
  done
) >"$results/resources.csv" &
sampler=$!
trap 'kill "$sampler" 2>/dev/null || true' EXIT

status=0
LOAD_PROFILE="$profile" "${compose[@]}" run --rm --no-deps load-runner \
  --quiet --out "csv=/results/$name/requests.csv.gz" --summary-export "/results/$name/summary.json" load-test.js || status=$?
kill "$sampler" 2>/dev/null || true
wait "$sampler" 2>/dev/null || true
echo "k6 exited $status"

psql --command="\\copy (SELECT s.calls, round(s.total_exec_time::numeric, 3) AS total_ms, round(s.mean_exec_time::numeric, 3) AS mean_ms,
  round(s.max_exec_time::numeric, 3) AS max_ms, s.rows, s.shared_blks_hit, s.shared_blks_read, regexp_replace(s.query, '\\s+', ' ', 'g') AS query
  FROM pg_stat_statements s JOIN pg_database d ON d.oid = s.dbid WHERE d.datname = 'pmplatform' ORDER BY s.total_exec_time DESC) TO STDOUT WITH CSV HEADER" \
  >"$results/statements.csv"

if [[ ! -s "$results/requests.csv.gz" ]]; then
  echo "k6 wrote no requests to $results; nothing to report" >&2
  exit $((status != 0 ? status : 1))
fi
report=0
python3 "$here/load-test-report.py" "$results" >"$results/report.md" || report=$?
cat "$results/report.md"
# k6 fails only the smoke profile (a request that did not answer 2xx); the report fails a breach with no backlog row.
exit $((status != 0 ? status : report))
