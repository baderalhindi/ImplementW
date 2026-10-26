#!/usr/bin/env sh
# TASK-091's validation check against a deployed non-PROD environment: "Simulate a database outage in
# a non-PROD environment and confirm the readiness endpoint reports unhealthy; confirm the configured
# alert fires and reaches the on-call channel."
#
#   infra/environments/drill-database-outage.sh sit
#   infra/environments/drill-database-outage.sh sit --dry-run
#   HOLD_SECONDS=600 infra/environments/drill-database-outage.sh uat
#
# Steps (record: docs/architecture/health-checks-uptime-monitoring.md §7.3):
#   D-1  /health/ready and /health/live are 200 through the environment's own address
#   D-2  the Cloud SQL instance is stopped (activation policy NEVER): the database really goes away,
#        nothing in the application is switched
#   D-3  /health/ready answers 503, and how long after D-2 is printed; /health/live stays 200, so no
#        instance is restarted for an outage a restart cannot fix
#   D-4  the outage is held for HOLD_SECONDS (default 420: the configured 5-minute alert threshold and
#        two minutes more). The on-call channel must receive the alert within 5 minutes of D-2's
#        time. This script cannot see the channel: the person running it records when the alert
#        arrived, against D-2's time, in the evidence log
#   D-5  the instance is started again (ALWAYS) and /health/ready returns to 200
#
# Once D-2 has run, the instance is started again however the script ends. PROD is refused — the check
# is non-PROD by definition — and so is DEV, which has no uptime alert to confirm.
#
# Requires: gcloud, curl, jq.
set -eu

here=$(cd "$(dirname "$0")" && pwd)
. "$here/lib/common.sh"

require_tools gcloud curl jq
require_known_environment "${1:-}"
environment=$1
shift
parse_dry_run "$@"
case $environment in
  sit | uat) ;;
  *) die "the outage drill runs in SIT or UAT only; $environment is refused (PROD is never taken down on purpose, DEV has no uptime alert)" ;;
esac

project=$(env_get "$environment" '.project_id')
instance=$(env_get "$environment" '.database.instance')
base=$(env_get "$environment" '.variables.APP_BASE_URL.value')
[ -n "$base" ] || die "APP_BASE_URL for $environment is empty in $MANIFEST (environment-separation.md F-1)."
base=${base%/}
hold=${HOLD_SECONDS:-420}
case $hold in '' | *[!0-9]*) die "HOLD_SECONDS is a whole number of seconds" ;; esac

require_authorisation

status() { curl --silent --output /dev/null --max-time 15 --write-out '%{http_code}' "$base$1" || echo 000; }
now() { date -u +%Y-%m-%dT%H:%M:%SZ; }

# wait_for <path> <status> <seconds> — polls every 5 seconds; prints how long it took.
wait_for() {
  started=$(date +%s)
  while [ "$(status "$1")" != "$2" ]; do
    [ $(($(date +%s) - started)) -lt "$3" ] || die "$1 did not answer $2 within $3 s"
    sleep 5
  done
  echo $(($(date +%s) - started))
}

set_activation() {
  run gcloud sql instances patch "$instance" --project="$project" --activation-policy="$1" --quiet
}

if [ "$DRY_RUN" = "1" ]; then
  echo "D-1"; echo_command curl "$base/health/ready"; echo_command curl "$base/health/live"
  echo "D-2"; set_activation NEVER
  echo "D-3"; echo_command curl "$base/health/ready" "(until 503)"; echo_command curl "$base/health/live"
  echo "D-4"; echo "  hold ${hold}s; record when the alert reaches the on-call channel"
  echo "D-5"; set_activation ALWAYS; echo_command curl "$base/health/ready" "(until 200)"
  exit 0
fi

echo "D-1  $(now)  /health/ready $(status /health/ready), /health/live $(status /health/live)"
[ "$(status /health/ready)" = 200 ] || die "D-1: $base/health/ready is not 200 before the drill; fix that first"
[ "$(status /health/live)" = 200 ] || die "D-1: $base/health/live is not 200 before the drill"

trap 'echo "restoring $instance" >&2; set_activation ALWAYS >&2 || true' EXIT INT TERM
outage_started=$(now)
set_activation NEVER >/dev/null
echo "D-2  $outage_started  $instance stopped — the alert is due by this time + 5 minutes"

seconds=$(wait_for /health/ready 503 600)
live=$(status /health/live)
echo "D-3  $(now)  /health/ready 503 after ${seconds}s; /health/live $live"
[ "$live" = 200 ] || echo "D-3  FAIL: /health/live is $live during a database outage; liveness must not depend on the database" >&2

echo "D-4  holding the outage ${hold}s. Record in the evidence log when the alert reached the on-call channel."
sleep "$hold"
echo "D-4  $(now)  /health/ready $(status /health/ready) at the end of the hold"

set_activation ALWAYS >/dev/null
trap - EXIT INT TERM
seconds=$(wait_for /health/ready 200 900)
echo "D-5  $(now)  $instance started; /health/ready 200 after ${seconds}s"
[ "$live" = 200 ] || exit 1
