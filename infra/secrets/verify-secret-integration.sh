#!/usr/bin/env sh
# Verifies one provisioned environment's secret store against what the repository says it should be
# (TASK-019). Read-only: it creates, changes and deletes nothing, and it never reads a secret value —
# it asks the store about containers, replication, versions and IAM, all of which are metadata.
#
#   infra/secrets/verify-secret-integration.sh sit
#   infra/secrets/verify-secret-integration.sh sit --dry-run    # prints the calls it would make
#
# Checks, each one an invariant some other file asserts statically and only a provisioned environment
# can answer for real:
#   V-1  every secret the inventory names exists, and no other secret carries the namespace prefix
#   V-2  replication is user-managed and pinned to the named in-Kingdom region (ADR-001 C-5)
#   V-3  every secret has an enabled version — an empty container stops the application at boot
#   V-4  the only principal that can read a secret is this environment's own runtime service account
#        (CTL-02: PROD secrets unreadable from non-PROD scopes)
#   V-5  the deployed service carries SECRET_STORE_ENDPOINT and no secret value in its environment
#
# Requires: gcloud (authenticated with viewer rights on the environment's project), jq.
# Record: docs/architecture/secret-management.md
set -eu

here=$(cd "$(dirname "$0")" && pwd)
. "$here/lib.sh"

environment=${1:-}
parse_dry_run "$@"
require_tools jq
[ "$DRY_RUN" = "1" ] || require_tools gcloud
require_known_environment "$environment"
region=$(require_region)

project=$(env_get "$environment" '.project_id')
prefix=$(env_get "$environment" '.secret_store.prefix')
runtime_sa=$(env_get "$environment" '.service_accounts.runtime')
service="$(manifest_get '.platform.resource_prefix')-$environment-api"
endpoint="https://secretmanager.googleapis.com/v1/projects/$project/secrets/$prefix"

failures=0

fail() {
  echo "  FAIL $*"
  failures=$((failures + 1))
}

# gcloud_json <args...> — a read-only call; under --dry-run it prints the call and returns nothing,
# which makes every check below report "not checked" rather than a false pass.
gcloud_json() {
  if [ "$DRY_RUN" = "1" ]; then
    echo_command gcloud "$@" >&2
    echo ''
  else
    gcloud "$@"
  fi
}

echo "== $environment — project $project, namespace $prefix, region $region =="

# V-1
expected=$(secret_ids "$environment" | sort)
actual=$(gcloud_json secrets list --project="$project" --filter="name ~ ^$prefix" --format='value(name)' | sort)
if [ "$DRY_RUN" != "1" ]; then
  for id in $expected; do
    echo "$actual" | grep -qx "$id" || fail "V-1 $id does not exist in $project"
  done
  for id in $actual; do
    echo "$expected" | grep -qx "$id" || fail "V-1 $id exists in $project but is not in the inventory (infra/secrets/inventory.py)"
  done
fi

for id in $expected; do
  [ "$DRY_RUN" = "1" ] && { echo_command gcloud secrets describe "$id" --project="$project" >&2; continue; }

  # V-2 — the replication policy is the control that keeps the payload in the Kingdom.
  locations=$(gcloud secrets describe "$id" --project="$project" \
    --format='value[delimiter=","](replication.userManaged.replicas[].location)')
  [ "$locations" = "$region" ] || fail "V-2 $id replicates to '${locations:-automatic}', expected exactly $region (ADR-001 C-5)"

  # V-3
  enabled=$(gcloud secrets versions list "$id" --project="$project" --filter='state:ENABLED' --format='value(name)' | wc -l | tr -d ' ')
  [ "$enabled" -ge 1 ] || fail "V-3 $id has no enabled version; the owner named in the sheet sets it with set-secret-version.sh"

  # V-4 — accessors, exactly one, and it belongs to this environment's project.
  accessors=$(gcloud secrets get-iam-policy "$id" --project="$project" \
    --flatten='bindings[].members[]' --filter='bindings.role:roles/secretmanager.secretAccessor' \
    --format='value(bindings.members)' | sort -u)
  [ "$accessors" = "serviceAccount:$runtime_sa" ] ||
    fail "V-4 $id is readable by [${accessors:-nobody}], expected only serviceAccount:$runtime_sa"
done

# V-5 — what the running service was actually given. A secret value in the environment would mean the
# deployment is still injecting secrets rather than letting the application read them (TASK-019 §3).
deployed=$(gcloud_json run services describe "$service" --project="$project" --region="$region" \
  --format='json(spec.template.spec.containers[0].env)')
if [ "$DRY_RUN" != "1" ]; then
  configured=$(echo "$deployed" | jq -r '.. | objects | select(.name == "SECRET_STORE_ENDPOINT") | .value // ""' | head -1)
  [ "$configured" = "$endpoint" ] || fail "V-5 $service has SECRET_STORE_ENDPOINT='${configured:-unset}', expected $endpoint"
  injected=$(echo "$deployed" | jq -r '.. | objects | select(.valueFrom != null) | .name' | tr '\n' ' ')
  [ -z "$(echo "$injected" | tr -d ' ')" ] || fail "V-5 $service is still injecting secret values: $injected"
fi

if [ "$DRY_RUN" = "1" ]; then
  echo "(dry run: the calls above were printed, not made; nothing was checked)"
  exit 0
fi

if [ "$failures" -gt 0 ]; then
  echo "$failures finding(s)."
  exit 1
fi

echo "OK: $(echo "$expected" | wc -l | tr -d ' ') secrets, all in $region, all readable only by $runtime_sa; $service reads them at runtime."
