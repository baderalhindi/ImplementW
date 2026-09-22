#!/usr/bin/env sh
# Runs TASK-016's validation drills against the provisioned environments and reports one line per
# check. Every check is a NEGATIVE test: it passes when the access it attempts is refused.
#
#   infra/environments/verify-separation.sh
#   SIT_DB_HOST=<sit db host> DEV_DB_PASSWORD=<dev app password> infra/environments/verify-separation.sh
#
# Checks, from the workbook's Validation Checks cell and ADR-001's constraints:
#   D-1  a DEV credential is rejected by the SIT database                       (workbook)
#   D-2  PROD secrets are unreadable from each non-PROD environment's principals (workbook)
#   D-3  no environment's service account holds a role in another environment's project, and
#        no service account holds a role at either folder, where it would be inherited
#   D-4  every secret and state bucket sits in the named in-Kingdom region       (ADR-001 C-1, C-4, C-5)
#
# A check that cannot be executed is reported SKIP and the run exits non-zero: an unexecuted drill
# is not a passed drill. Exit 0 only when every check is PASS.
#
# Requires: gcloud (authenticated as a principal that can read IAM policies in all four projects),
# jq; psql for D-1. Record: docs/architecture/environment-separation.md
set -eu

here=$(cd "$(dirname "$0")" && pwd)
. "$here/lib/common.sh"

require_tools gcloud jq
region=$(require_region)

failures=0
report() { # report <PASS|FAIL|SKIP> <id> <text>
  printf '%-4s %-4s %s\n' "$1" "$2" "$3"
  [ "$1" = "PASS" ] || failures=$((failures + 1))
}

prod_project=$(env_get prod '.project_id')
dev_user=$(env_get dev '.database.user')

# D-1 — a DEV database credential against the SIT database instance.
if [ -n "${SIT_DB_HOST:-}" ] && [ -n "${DEV_DB_PASSWORD:-}" ] && command -v psql >/dev/null 2>&1; then
  if PGPASSWORD="$DEV_DB_PASSWORD" PGCONNECT_TIMEOUT=10 \
     psql "host=$SIT_DB_HOST user=$dev_user dbname=$(env_get sit '.database.database') sslmode=require" \
     -c 'select 1' >/dev/null 2>&1; then
    report FAIL D-1 "the DEV credential $dev_user CONNECTED to the SIT database at $SIT_DB_HOST"
  else
    report PASS D-1 "the DEV credential $dev_user is rejected by the SIT database"
  fi
else
  report SKIP D-1 "set SIT_DB_HOST and DEV_DB_PASSWORD (and install psql) once TASK-020 has created the instances"
fi

# D-2 — PROD secrets from each non-PROD environment's principals.
for environment in dev sit uat; do
  for sa in $(env_get "$environment" '.service_accounts | to_entries[] | .value'); do
    for secret_id in $(env_get prod '.variables | to_entries[] | select(.value.kind == "secret") | .value.secret_id'); do
      if gcloud secrets versions access latest --secret="$secret_id" --project="$prod_project" \
           --impersonate-service-account="$sa" >/dev/null 2>&1; then
        report FAIL D-2 "$sa READ the PROD secret $secret_id"
      else
        report PASS D-2 "$sa cannot read the PROD secret $secret_id"
      fi
    done
  done
done

# D-3 — cross-environment IAM. Every principal in a project's policy must belong to that project;
#       the PROD principals must appear in no other project's policy, and no others in PROD's.
for environment in $(environment_names); do
  project=$(env_get "$environment" '.project_id')
  members=$(gcloud projects get-iam-policy "$project" --format='value(bindings.members)' 2>/dev/null |
    tr ';' '\n' | grep '^serviceAccount:.*iam.gserviceaccount.com$' | sed 's/^serviceAccount://' | sort -u)
  foreign=""
  for member in $members; do
    case "$member" in
      *"@$project.iam.gserviceaccount.com") ;;                  # the project's own
      *.gserviceaccount.com)
        for other in $(environment_names); do
          [ "$other" = "$environment" ] && continue
          other_project=$(env_get "$other" '.project_id')
          case "$member" in *"@$other_project.iam.gserviceaccount.com") foreign="$foreign $member";; esac
        done
        ;;
    esac
  done
  if [ -n "$foreign" ]; then
    report FAIL D-3 "$project grants a role to another environment's service account:$foreign"
  else
    report PASS D-3 "$project grants no role to another environment's service account"
  fi
done

# D-3b — folder IAM. A grant at the folder is inherited by every project underneath, so it is the
#        one way to hold a role in an environment without appearing in that project's own policy.
#        No service account belongs at this level.
for folder in $(manifest_get '.platform.folders[].name'); do
  folder_id=$(gcloud resource-manager folders list --organization="$(manifest_get '.platform.organization_id')" \
    --filter="displayName=$folder" --format='value(name)' 2>/dev/null)
  if [ -z "$folder_id" ]; then
    report SKIP D-3b "folder $folder not found; the folders are created by apply-org-policy.sh"
    continue
  fi
  accounts=$(gcloud resource-manager folders get-iam-policy "$folder_id" \
    --format='value(bindings.members)' 2>/dev/null | tr ';' '\n' | grep '^serviceAccount:' || true)
  if [ -n "$accounts" ]; then
    report FAIL D-3b "folder $folder grants roles to service accounts, inherited by every environment under it: $(echo "$accounts" | tr '\n' ' ')"
  else
    report PASS D-3b "folder $folder grants no role to any service account"
  fi
done

# D-4 — locality. Secret replication and state-bucket location, read back from the provider.
for environment in $(environment_names); do
  project=$(env_get "$environment" '.project_id')
  bucket=$(env_get "$environment" '.state_bucket')
  location=$(gcloud storage buckets describe "gs://$bucket" --format='value(location)' 2>/dev/null |
    tr '[:upper:]' '[:lower:]')
  if [ "$location" = "$region" ]; then
    report PASS D-4 "gs://$bucket is in $region"
  else
    report FAIL D-4 "gs://$bucket reports location '${location:-unreadable}', expected $region"
  fi
  for secret_id in $(env_get "$environment" '.variables | to_entries[] | select(.value.kind == "secret") | .value.secret_id'); do
    locations=$(gcloud secrets describe "$secret_id" --project="$project" \
      --format='value(replication.userManaged.replicas[].location)' 2>/dev/null | tr ';' ' ' | tr -s ' ')
    if [ "$(echo "$locations" | tr -d ' ')" = "$region" ]; then
      report PASS D-4 "$secret_id replicates to $region only"
    else
      report FAIL D-4 "$secret_id replicates to '${locations:-unreadable}', expected $region only"
    fi
  done
done

echo
if [ "$failures" -eq 0 ]; then
  echo "OK: every separation drill passed against $region."
else
  echo "$failures check(s) did not pass. A SKIP counts as not passed — the drill is still owed."
  exit 1
fi
