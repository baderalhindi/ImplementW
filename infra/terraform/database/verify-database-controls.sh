#!/usr/bin/env sh
# Verifies one provisioned environment's database against the controls TASK-020 owns. Read-only:
# it creates, changes and deletes nothing, and it reads no data out of the database.
#
#   infra/terraform/database/verify-database-controls.sh sit
#   infra/terraform/database/verify-database-controls.sh sit --dry-run   # prints the calls it would make
#   infra/terraform/database/verify-database-controls.sh sit --connect   # also attempts a non-TLS connection
#
# The acceptance criteria are three assertions about a running instance, and a static check on the
# Terraform can only prove that the code asks for them. These are the six the provider can answer:
#   D-1  encryption at rest — which key the instance is encrypted with, and where that key is
#        (CTL-17, ADR-001 C-1)
#   D-2  the instance rejects a connection that is not TLS (CTL-17); --connect attempts one
#   D-3  automated backups and point-in-time recovery are on, in the named region, and the backup
#        chain covers the whole log window (CTL-35, PTBC-048, ADR-001 C-3)
#   D-4  the backup job has actually run — a successful automated backup in the last 26 hours
#   D-5  the off-instance export exists, is scheduled, and its bucket is single-region in the
#        named region with versioning on (ADR-001 C-4)
#   D-6  at least one exported object is present, which is what TASK-023's drill restores from
#
# Requires: gcloud (authenticated with viewer rights on the environment's project), jq.
# --connect additionally requires psql and a host with a route to the private IP; there is no
# public endpoint, so it is run from inside the VPC and not from a laptop.
# Record: docs/architecture/database-provisioning.md
set -eu

here=$(cd "$(dirname "$0")" && pwd)
MANIFEST=${MANIFEST:-"$here/../../environments/environments.json"}
export MANIFEST
# shellcheck source=../../environments/lib/common.sh
. "$here/../../environments/lib/common.sh"

environment=${1:-}
parse_dry_run "$@"
CONNECT=0
for arg in "$@"; do
  [ "$arg" = "--connect" ] && CONNECT=1
done
require_tools jq
[ "$DRY_RUN" = "1" ] || require_tools gcloud
require_known_environment "$environment"
region=$(require_region)

project=$(env_get "$environment" '.project_id')
instance=$(env_get "$environment" '.database.instance')
database=$(env_get "$environment" '.database.database')
bucket="$project-db-backups"

failures=0

fail() {
  echo "  FAIL $*"
  failures=$((failures + 1))
}

note() {
  echo "  ---- $*"
}

# gcloud_json <args...> — a read-only call. Under --dry-run it prints the call and returns an empty
# object, which makes every check below report "not checked" rather than a false pass.
gcloud_json() {
  if [ "$DRY_RUN" = "1" ]; then
    echo_command gcloud "$@" >&2
    echo '{}'
  else
    gcloud "$@"
  fi
}

echo "== $environment — project $project, instance $instance, region $region =="

described=$(gcloud_json sql instances describe "$instance" --project="$project" --format=json)

# get <jq-filter> — a field of the instance description, empty string when it is absent. It does
# not use jq's `//`, which treats `false` as absent: `ipv4Enabled` being false is the answer this
# script most wants to read, and `// ""` would turn it into "unset" and report a private instance
# as having a public address.
get() {
  echo "$described" | jq -r "$1 | if . == null then \"\" else . end"
}

if [ "$DRY_RUN" != "1" ]; then
  # D-1. Encryption at rest is a property of the platform and cannot be turned off, so there is no
  # field that reads "encrypted: true" and none is invented here. What the API does answer is which
  # key, and a customer-managed key is only compliant if it is itself in the Kingdom.
  key=$(get '.diskEncryptionConfiguration.kmsKeyName')
  if [ -z "$key" ]; then
    note "D-1 encrypted at rest with Google-managed keys; no customer-managed key is configured (ADR-001 R-4 unanswered, infrastructure-as-code.md F-6)"
  else
    note "D-1 encrypted at rest with $key"
    case "$key" in
      */locations/"$region"/*) ;;
      *) fail "D-1 the encryption key is not in $region. ADR-001 C-1: the key that makes in-Kingdom data readable is held in the Kingdom." ;;
    esac
  fi

  # D-2. Two facts, both read from the provider: the instance refuses an unencrypted connection,
  # and it has no public address for one to arrive on.
  ssl_mode=$(get '.settings.ipConfiguration.sslMode')
  [ "$ssl_mode" = "ENCRYPTED_ONLY" ] ||
    fail "D-2 sslMode is '${ssl_mode:-unset}', expected ENCRYPTED_ONLY. A non-TLS connection would be accepted (CTL-17)."
  public=$(get '.settings.ipConfiguration.ipv4Enabled')
  [ "$public" = "false" ] ||
    fail "D-2 ipv4Enabled is '$public'; the instance has a public address (CTL-03, ADR-001 C-3)."

  # D-3. The backup mechanism itself. RPO 1 hour is met by point-in-time recovery, not by the daily
  # backup, so PITR being off is an RPO failure and not a missing nicety.
  [ "$(get '.settings.backupConfiguration.enabled')" = "true" ] ||
    fail "D-3 automated backups are disabled (CTL-35, PTBC-048)."
  [ "$(get '.settings.backupConfiguration.pointInTimeRecoveryEnabled')" = "true" ] ||
    fail "D-3 point-in-time recovery is disabled. Automated backups run once a day, so without PITR the RPO is 24 hours and PTBC-048 asks for 1."
  backup_region=$(get '.settings.backupConfiguration.location')
  [ "$backup_region" = "$region" ] ||
    fail "D-3 backups are stored in '${backup_region:-unset}', expected $region (ADR-001 C-3: backups stay in the Kingdom)."

  backup_start=$(get '.settings.backupConfiguration.startTime')
  log_days=$(get '.settings.backupConfiguration.transactionLogRetentionDays')
  retained=$(get '.settings.backupConfiguration.backupRetentionSettings.retainedBackups')
  note "D-3 backup window $backup_start UTC, PITR $log_days day(s), ${retained:-provider default} backup(s) retained"
  if [ -n "$retained" ] && [ -n "$log_days" ] && [ "$retained" -lt "$log_days" ]; then
    fail "D-3 $retained retained backup(s) do not cover a $log_days-day log window. Recovery replays the log forward from a backup, so the oldest days of log cannot be recovered to."
  fi
  if [ "$environment" = "prod" ] && [ -z "$retained" ]; then
    fail "D-3 PROD is running on the provider's default retention. The period is OQ-003 / PTBC-048, owed by AHDA Cybersecurity and Records."
  fi
fi

# D-4. The configuration above says a backup is scheduled; this says one happened. The window is
# 26 hours rather than 24 so that a backup running late is not reported as a backup not running.
runs=$(gcloud_json sql backups list --instance="$instance" --project="$project" \
  --filter='status=SUCCESSFUL' --sort-by=~windowStartTime --limit=1 --format=json)
if [ "$DRY_RUN" != "1" ]; then
  latest=$(echo "$runs" | jq -r '.[0].windowStartTime // ""')
  if [ -z "$latest" ]; then
    fail "D-4 no successful automated backup exists. The acceptance criterion is that the job runs on the documented schedule, not that it is configured to."
  else
    age_hours=$(python3 -c '
import datetime, sys
stamp = datetime.datetime.fromisoformat(sys.argv[1].replace("Z", "+00:00"))
now = datetime.datetime.now(datetime.timezone.utc)
print(int((now - stamp).total_seconds() // 3600))
' "$latest")
    note "D-4 last successful backup $latest (${age_hours}h ago)"
    [ "$age_hours" -le 26 ] ||
      fail "D-4 the newest successful backup is ${age_hours}h old. Backups are daily; anything past 26h means the job is not running."
    backup_location=$(echo "$runs" | jq -r '.[0].location // ""')
    [ "$backup_location" = "$region" ] ||
      fail "D-4 that backup is stored in '${backup_location:-unknown}', expected $region (ADR-001 C-3)."
  fi
fi

# D-5 and D-6. The copy that outlives the instance. DEV has no backup bucket, because the
# Environment and Secrets sheet scopes DB_BACKUP_STORAGE_CONNECTION_STRING to SIT, UAT and PROD.
if [ "$environment" = "dev" ]; then
  note "D-5 skipped: DEV has no backup bucket and no export (Environment and Secrets scopes the row to SIT/UAT/PROD)"
else
  job=$(gcloud_json scheduler jobs describe "$instance-backup-export" --project="$project" \
    --location="$region" --format=json)
  bucket_described=$(gcloud_json storage buckets describe "gs://$bucket" --format=json)
  # An empty prefix is a finding, not an error, so a non-zero exit here is absorbed and read below.
  objects=$(gcloud_json storage ls --long "gs://$bucket/$instance/" || echo '')

  if [ "$DRY_RUN" != "1" ]; then
    schedule=$(echo "$job" | jq -r '.schedule // ""')
    state=$(echo "$job" | jq -r '.state // ""')
    if [ -z "$schedule" ]; then
      fail "D-5 no export job '$instance-backup-export' exists in $region. Cloud SQL's automated backups are deleted with the instance; this is the copy that is not."
    else
      note "D-5 export scheduled '$schedule' UTC, state $state, last attempt $(echo "$job" | jq -r '.status.code // "none recorded"')"
      [ "$state" = "ENABLED" ] || fail "D-5 the export job is $state, so no copy is being written."
    fi

    location=$(echo "$bucket_described" | jq -r '.location // ""' | tr 'A-Z' 'a-z')
    location_type=$(echo "$bucket_described" | jq -r '.location_type // .locationType // ""')
    [ "$location" = "$region" ] ||
      fail "D-5 $bucket is in '${location:-unknown}', expected $region (ADR-001 C-4)."
    [ "$location_type" = "region" ] ||
      fail "D-5 $bucket has location type '${location_type:-unknown}', expected region — a multi-region or dual-region bucket puts a copy outside the Kingdom."
    [ "$(echo "$bucket_described" | jq -r '.versioning_enabled // .versioning.enabled // false')" = "true" ] ||
      fail "D-5 versioning is off on $bucket. Each export overwrites one object name, so without versioning there is exactly one copy and no chain."

    # D-6
    if [ -z "$objects" ]; then
      fail "D-6 $bucket holds no export for $instance. Nothing has been written to the target DB_BACKUP_STORAGE_CONNECTION_STRING names, so there is nothing for TASK-023's drill to restore."
    else
      note "D-6 exports present: $(echo "$objects" | grep -c "$database" || true) object(s) under gs://$bucket/$instance/"
    fi
  fi
fi

# D-2, executed rather than read. There is no public endpoint, so this runs from a host inside the
# VPC; from anywhere else it fails to connect for the wrong reason.
#
# A failed psql is not by itself the evidence. An unreachable host, a timeout and a name that does
# not resolve all fail too, and reading any of them as "the instance refused it" would report the
# control as proved on a day nothing was tested. Three outcomes, and only one of them passes:
# the connection was accepted (the control is broken), the server answered and refused it for an
# encryption reason (the evidence), or the attempt never reached a server (nothing was proved).
if [ "$CONNECT" = "1" ] && [ "$DRY_RUN" != "1" ]; then
  require_tools psql
  private_ip=$(get '.ipAddresses[]? | select(.type == "PRIVATE") | .ipAddress' | head -1)
  if [ -z "$private_ip" ]; then
    fail "D-2 the instance reports no private address to attempt a connection against."
  else
    attempt=$(PGSSLMODE=disable PGCONNECT_TIMEOUT=10 psql -h "$private_ip" -U postgres \
      -d "$database" -c 'select 1' 2>&1) && accepted=1 || accepted=0
    if [ "$accepted" = "1" ]; then
      fail "D-2 an unencrypted connection to $private_ip was ACCEPTED. This is the control CTL-17 names and the instance must refuse it."
    elif echo "$attempt" | grep -Eiq 'no encryption|ssl|encrypt'; then
      note "D-2 an unencrypted connection to $private_ip was refused by the server: $(echo "$attempt" | grep -Ei 'no encryption|ssl|encrypt' | head -1 | sed 's/^[[:space:]]*//')"
    else
      fail "D-2 the attempt on $private_ip never reached a server, so nothing was proved about how it treats an unencrypted connection. Run this from a host with a route to the private IP. psql said: $(echo "$attempt" | head -1)"
    fi
  fi
fi

if [ "$DRY_RUN" = "1" ]; then
  echo "(dry run: the calls above were printed, not made; nothing was checked)"
  exit 0
fi

if [ "$failures" -gt 0 ]; then
  echo "$failures finding(s)."
  exit 1
fi

echo "OK: $instance is encrypted at rest, refuses non-TLS connections, backs up daily into $region with point-in-time recovery, and has an off-instance copy."
