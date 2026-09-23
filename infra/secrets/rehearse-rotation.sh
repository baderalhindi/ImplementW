#!/usr/bin/env sh
# Rehearses the TASK-019 validation check end to end, on one machine:
#
#   "Rotate one test secret end-to-end and confirm the running application picks up the new value
#    without a code change."
#
#   infra/secrets/rehearse-rotation.sh
#
# What it does. It starts PostgreSQL from the TASK-014 local stack, starts a stand-in secret store
# (stub-store.py) holding DB_CONNECTION_STRING with the WRONG password, and starts the real API
# against it with ASPNETCORE_ENVIRONMENT=Staging — so Program.cs requires the store, exactly as in a
# deployed environment. /health reports Unhealthy: the application started, read its secret from the
# store, and the value does not work. The secret is then rotated in the store and nothing else is
# touched — no restart, no redeployment, no edit to any file the application reads — until /health
# reports Healthy. The process id is checked before and after: it is the same process.
#
# What it is not. The store is a stand-in, not Secret Manager, and the rotation is a write to its
# state file rather than `gcloud secrets versions add`. It rehearses the application's half of the
# procedure — which is the half the validation check is about — and not Secret Manager's. The
# rehearsal against a provisioned DEV namespace is blocked by UGV-07 and is a release-checklist item
# (secret-rotation-runbook.md §9).
#
# Requires: docker, dotnet, python3, curl. Takes about six minutes: the refresh interval is the
# product default of five, and shortening it for the rehearsal would rehearse something else.
set -eu

repository=$(cd "$(dirname "$0")/../.." && pwd)
work=$(mktemp -d)
compose="docker compose -f $repository/infra/docker/docker-compose.yml"
api_port=${REHEARSAL_API_PORT:-5081}
store_port=${REHEARSAL_STORE_PORT:-8099}
token=rehearsal-bootstrap-credential-not-a-real-one
state="$work/store.json"
log="$work/api.log"
store_log="$work/store.log"
store_pid=''
api_pid=''
postgres_was_running=''

# The two values. Same database, same user, one wrong password: the only thing that changes between
# them is the secret, so nothing but the rotation can explain /health changing.
wrong="Host=127.0.0.1;Port=5432;Database=pmplatform;Username=pmplatform;Password=wrong-password"
right="Host=127.0.0.1;Port=5432;Database=pmplatform;Username=pmplatform;Password=pmplatform"

cleanup() {
  [ -n "$api_pid" ] && kill "$api_pid" 2>/dev/null || true
  [ -n "$store_pid" ] && kill "$store_pid" 2>/dev/null || true
  # Only stop what this script started. A stack that was already up is someone's working database.
  [ "$postgres_was_running" = "no" ] && $compose stop postgres >/dev/null 2>&1
  echo "rehearsal artefacts: $work"
}
trap cleanup EXIT

health() { curl --silent --max-time 5 "http://127.0.0.1:$api_port/health" 2>/dev/null || true; }
# Which process is actually serving /health — observed from the port, not from a variable this
# script set, so "the same process" is a measurement rather than an assumption.
serving() { lsof -ti "tcp:$api_port" -s TCP:LISTEN 2>/dev/null | head -1; }

echo "== 0. build =="
dotnet build "$repository/src/backend/PMPlatform.Api" --configuration Release >/dev/null

echo "== 1. PostgreSQL (TASK-014 local stack) =="
postgres_was_running=$([ -n "$($compose ps --quiet --status running postgres 2>/dev/null)" ] && echo yes || echo no)
$compose up --detach --wait postgres

echo "== 2. the stand-in secret store =="
printf '{"pmplatform-dev-db-connection-string": "%s"}\n' "$wrong" > "$state"
python3 "$repository/infra/secrets/stub-store.py" --port "$store_port" --token "$token" --state "$state" \
  > "$store_log" 2>&1 &
store_pid=$!
sleep 1

echo "== 3. the API, requiring the store =="
ASPNETCORE_ENVIRONMENT=Staging \
ASPNETCORE_URLS="http://127.0.0.1:$api_port" \
SECRET_STORE_ENDPOINT="http://127.0.0.1:$store_port/v1/projects/rehearsal/secrets/pmplatform-dev-" \
SECRET_STORE_AUTH_TOKEN="$token" \
  "$repository/src/backend/PMPlatform.Api/bin/Release/net10.0/PMPlatform.Api" > "$log" 2>&1 &
api_pid=$!

waited=0
while [ -z "$(health)" ]; do
  waited=$((waited + 2))
  [ "$waited" -lt 60 ] || { echo "the API did not start in ${waited}s; see $log"; exit 1; }
  kill -0 "$api_pid" 2>/dev/null || { echo "the API exited during start-up:"; tail -20 "$log"; exit 1; }
  sleep 2
done

started_pid=$(serving)
before=$(health)
echo "   started (pid $started_pid), /health: $before"
[ "$before" = "Unhealthy" ] || { echo "expected Unhealthy with the wrong password, got '$before'"; exit 1; }

echo "== 4. rotate: the store now holds the working value, and nothing else changes =="
printf '{"pmplatform-dev-db-connection-string": "%s"}\n' "$right" > "$state"
rotated_at=$(date +%s)

echo "== 5. wait for the running application to pick it up (refresh interval: 5 minutes) =="
# Two clocks. `waiting` counts the seconds this script actually spent asleep; `elapsed` is wall
# clock. They differ when the machine suspends, which stops the application's refresh timer as well
# as this loop — so a run where they diverge measures how long the laptop was shut, not how long the
# rotation took to land, and says so rather than reporting the larger number.
waiting=0
while [ "$(health)" != "Healthy" ]; do
  [ "$waiting" -lt 400 ] || { echo "still $(health) after ${waiting}s of waiting; see $log"; exit 1; }
  [ $((waiting % 30)) -eq 0 ] && echo "   ${waiting}s: $(health)"
  sleep 5
  waiting=$((waiting + 5))
done
elapsed=$(( $(date +%s) - rotated_at ))
ended_pid=$(serving)

reads=$(grep -c 'stub-store: read' "$store_log" 2>/dev/null || echo 0)

echo
echo "== result =="
echo "   /health: Unhealthy -> Healthy, after ${waiting}s of waiting (${elapsed}s wall clock)"
if [ $((elapsed - waiting)) -gt 60 ]; then
  echo "   NOTE: the machine was suspended for about $((elapsed - waiting))s during the wait. The"
  echo "         application's refresh timer was suspended with it, so ${elapsed}s is not a latency."
fi
echo "   store reads: $reads (one at start-up, $((reads - 1)) refresh) — the value was re-read, not cached"
echo "   process: $started_pid -> $ended_pid $([ "$started_pid" = "$ended_pid" ] && echo '(the same process — no restart)' || echo '(RESTARTED — the rehearsal proves nothing)')"
[ "$started_pid" = "$ended_pid" ] || exit 1
echo "   no file the application reads was edited, and no code changed."
