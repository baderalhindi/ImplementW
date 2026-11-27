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
#   D-3c the one permitted exception, bounded: the eight deploy and runtime accounts read the
#        shared Artifact Registry repository and hold nothing else there    (cicd-pipeline.md F-1)
#   D-4  every secret and state bucket sits in the named in-Kingdom region       (ADR-001 C-1, C-4, C-5)
#
# A check that cannot be executed is reported SKIP and the run exits non-zero: an unexecuted drill
# is not a passed drill. Exit 0 only when every check is PASS.
#
# A refusal counts as PASS only when the provider itself refused the access the check attempts
# (Secret Manager's PERMISSION_DENIED on secretmanager.versions.access; the SIT database rejecting
# the login). Any other failure of a provider call (no credentials, an impersonation that did not
# work, no network, an API not enabled, a project that does not exist) proves nothing and is
# reported ERROR with the provider's message. Every run ends with the summary, also when it stops
# early.
#
# Requires: gcloud (authenticated as a principal that can read IAM policies in all four projects),
# jq; psql for D-1. Record: docs/architecture/environment-separation.md
set -eu

here=$(cd "$(dirname "$0")" && pwd)
. "$here/lib/common.sh"

failures=0
finished=0
provider_errors=""
report() { # report <PASS|FAIL|SKIP|ERROR> <id> <text>
  printf '%-5s %-4s %s\n' "$1" "$2" "$3"
  [ "$1" = "PASS" ] || failures=$((failures + 1))
}

# The summary is printed on every exit. A run that stops before its last check says so and exits
# non-zero, whatever the status of the command that stopped it.
on_exit() {
  status=$?
  [ -n "$provider_errors" ] && rm -f "$provider_errors"
  [ "$finished" -eq 1 ] && return
  echo
  echo "ABORTED: the run stopped (exit $status) before its last check; $failures check(s) above did not pass and the rest were not run. The drill is still owed."
  [ "$status" -ne 0 ] || exit 1
}
trap on_exit EXIT

require_tools gcloud jq
region=$(require_region)
provider_errors=$(mktemp)

# provider <gcloud args...> — runs one read; stdout is the answer, gcloud's message is kept for
# provider_error. Its status is gcloud's.
provider() {
  gcloud "$@" 2>"$provider_errors"
}

# provider_error — the last failed call's message, on one line, for an ERROR report.
provider_error() {
  message=$(grep -v '^[[:space:]]*$' "$provider_errors" | head -n 3 | tr '\n' ' ' | sed 's/[[:space:]]*$//')
  echo "${message:-gcloud failed without a message}"
}

prod_project=$(env_get prod '.project_id')
dev_user=$(env_get dev '.database.user')

# D-1 — a DEV database credential against the SIT database instance.
if [ -n "${SIT_DB_HOST:-}" ] && [ -n "${DEV_DB_PASSWORD:-}" ] && command -v psql >/dev/null 2>&1; then
  if login_error=$(PGPASSWORD="$DEV_DB_PASSWORD" PGCONNECT_TIMEOUT=10 \
     psql "host=$SIT_DB_HOST user=$dev_user dbname=$(env_get sit '.database.database') sslmode=require" \
     -c 'select 1' 2>&1 >/dev/null); then
    report FAIL D-1 "the DEV credential $dev_user CONNECTED to the SIT database at $SIT_DB_HOST"
  else
    # Only the server's answer to the login is a rejection; a connection that never reached it is not.
    case "$login_error" in
      *"password authentication failed"* | *"no pg_hba.conf entry"* | *"permission denied for database"* | *"role \"$dev_user\" does not exist"*)
        report PASS D-1 "the DEV credential $dev_user is rejected by the SIT database" ;;
      *)
        report ERROR D-1 "the SIT database at $SIT_DB_HOST did not answer the login: $(echo "$login_error" | head -n 2 | tr '\n' ' ')" ;;
    esac
  fi
else
  report SKIP D-1 "set SIT_DB_HOST and DEV_DB_PASSWORD (and install psql) once TASK-020 has created the instances"
fi

# D-2 — PROD secrets from each non-PROD environment's principals. The impersonation is proved
#       first: a call that never acted as the principal says nothing about what the principal can
#       read. Then only Secret Manager's own refusal of the read is a PASS.
prod_secrets=$(env_get prod '.variables | to_entries[] | select(.value.kind == "secret") | .value.secret_id')
for environment in dev sit uat; do
  for sa in $(env_get "$environment" '.service_accounts | to_entries[] | .value'); do
    if ! provider auth print-access-token --impersonate-service-account="$sa" >/dev/null; then
      report ERROR D-2 "cannot act as $sa, so none of its $(echo "$prod_secrets" | wc -w | tr -d ' ') PROD secret reads was attempted: $(provider_error)"
      continue
    fi
    for secret_id in $prod_secrets; do
      if provider secrets versions access latest --secret="$secret_id" --project="$prod_project" \
           --impersonate-service-account="$sa" >/dev/null; then
        report FAIL D-2 "$sa READ the PROD secret $secret_id"
      elif grep -q "PERMISSION_DENIED" "$provider_errors" && grep -q "secretmanager.versions.access" "$provider_errors"; then
        report PASS D-2 "$sa cannot read the PROD secret $secret_id (PERMISSION_DENIED)"
      else
        report ERROR D-2 "$sa reading the PROD secret $secret_id failed without a Secret Manager refusal: $(provider_error)"
      fi
    done
  done
done

# D-3 — cross-environment IAM. Every principal in a project's policy must belong to that project;
#       the PROD principals must appear in no other project's policy, and no others in PROD's.
for environment in $(environment_names); do
  project=$(env_get "$environment" '.project_id')
  if ! policy=$(provider projects get-iam-policy "$project" --format='value(bindings.members)'); then
    report ERROR D-3 "the IAM policy of $project could not be read: $(provider_error)"
    continue
  fi
  members=$(printf '%s\n' "$policy" |
    tr ';' '\n' | grep '^serviceAccount:.*iam.gserviceaccount.com$' | sed 's/^serviceAccount://' | sort -u || true)
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
organization_id=$(manifest_get '.platform.organization_id')
for folder in $(manifest_get '.platform.folders[].name'); do
  if [ -z "$organization_id" ]; then
    report SKIP D-3b "folder $folder: platform.organization_id is empty (UGV-07), so the folders cannot be listed"
    continue
  fi
  if ! folder_id=$(provider resource-manager folders list --organization="$organization_id" \
      --filter="displayName=$folder" --format='value(name)'); then
    report ERROR D-3b "the folders of organisation $organization_id could not be listed: $(provider_error)"
    continue
  fi
  if [ -z "$folder_id" ]; then
    report SKIP D-3b "folder $folder not found; the folders are created by apply-org-policy.sh"
    continue
  fi
  if ! folder_policy=$(provider resource-manager folders get-iam-policy "$folder_id" --format='value(bindings.members)'); then
    report ERROR D-3b "the IAM policy of folder $folder ($folder_id) could not be read: $(provider_error)"
    continue
  fi
  accounts=$(printf '%s\n' "$folder_policy" | tr ';' '\n' | grep '^serviceAccount:' || true)
  if [ -n "$accounts" ]; then
    report FAIL D-3b "folder $folder grants roles to service accounts, inherited by every environment under it: $(echo "$accounts" | tr '\n' ' ')"
  else
    report PASS D-3b "folder $folder grants no role to any service account"
  fi
done

# D-3c — the one permitted cross-project binding. One artifact is built once and promoted unchanged
#         through the four environments (TASK-018), so all eight deploy and runtime accounts must be
#         able to pull from one registry. D-3 cannot see that binding: the artifacts project is not
#         an environment and is not in its loop. The exception is bounded in two directions here.
#         The grant belongs on the repository, so no environment principal may appear in the
#         project's own policy, where it would reach everything else in the project; and on the
#         repository itself the only environment principals are those eight, holding only
#         roles/artifactregistry.reader. Principals outside the four environment projects are out of
#         scope, as they are for D-3: Google's own service agents live in the project by design.
artifacts=$(manifest_get '.platform.artifact_registry.project_id')
artifacts_repository=$(manifest_get '.platform.artifact_registry.repository')
if [ -z "$artifacts" ]; then
  report SKIP D-3c "the manifest names no platform.artifact_registry.project_id"
else
  promotion_principals=$(for environment in $(environment_names); do
    env_get "$environment" '.service_accounts | to_entries[] | .value'
  done | sort -u)
  environment_domains=$(for environment in $(environment_names); do
    echo "@$(env_get "$environment" '.project_id').iam.gserviceaccount.com"
  done)

  # Is this principal one of the four environment projects' own service accounts?
  belongs_to_an_environment() {
    for domain in $environment_domains; do
      case "$1" in *"$domain") return 0;; esac
    done
    return 1
  }

  if ! project_policy=$(provider projects get-iam-policy "$artifacts" --flatten='bindings[].members' \
      --format='csv[no-heading](bindings.role,bindings.members)'); then
    report ERROR D-3c "the IAM policy of $artifacts could not be read (the registry project is created with the platform): $(provider_error)"
  else
    at_project=""
    for entry in $project_policy; do
      role=${entry%%,*}; member=${entry#*,}
      case "$member" in serviceAccount:*) account=${member#serviceAccount:};; *) continue;; esac
      belongs_to_an_environment "$account" && at_project="$at_project $account($role)"
    done
    if [ -n "$at_project" ]; then
      report FAIL D-3c "$artifacts grants environment principals a role on the whole project, not on the repository:$at_project"
    else
      report PASS D-3c "$artifacts grants no environment principal a project-wide role"
    fi

    if ! repository_policy=$(provider artifacts repositories get-iam-policy "$artifacts_repository" \
        --project="$artifacts" --location="$region" --flatten='bindings[].members' \
        --format='csv[no-heading](bindings.role,bindings.members)'); then
      report ERROR D-3c "the IAM policy of repository $artifacts_repository in $artifacts could not be read: $(provider_error)"
    else
      wrong=""
      seen=""
      for entry in $repository_policy; do
        role=${entry%%,*}; member=${entry#*,}
        case "$member" in serviceAccount:*) account=${member#serviceAccount:};; *) continue;; esac
        belongs_to_an_environment "$account" || continue
        if ! echo "$promotion_principals" | grep -qx "$account"; then
          wrong="$wrong $account(not a promotion principal)"
        elif [ "$role" != "roles/artifactregistry.reader" ]; then
          wrong="$wrong $account($role)"
        else
          seen="$seen $account"
        fi
      done
      missing=""
      for account in $promotion_principals; do
        case " $seen " in *" $account "*) ;; *) missing="$missing $account";; esac
      done
      if [ -n "$wrong" ]; then
        report FAIL D-3c "$artifacts_repository holds a binding outside the exception:$wrong"
      elif [ -n "$missing" ]; then
        report FAIL D-3c "$artifacts_repository does not grant reader to:$missing — those environments cannot pull the promoted image"
      else
        report PASS D-3c "$artifacts_repository grants roles/artifactregistry.reader to the eight promotion principals and nothing else"
      fi
    fi
  fi
fi

# D-4 — locality. Secret replication and state-bucket location, read back from the provider.
for environment in $(environment_names); do
  project=$(env_get "$environment" '.project_id')
  bucket=$(env_get "$environment" '.state_bucket')
  if ! location=$(provider storage buckets describe "gs://$bucket" --format='value(location)'); then
    report ERROR D-4 "gs://$bucket could not be described: $(provider_error)"
  elif [ "$(echo "$location" | tr '[:upper:]' '[:lower:]')" = "$region" ]; then
    report PASS D-4 "gs://$bucket is in $region"
  else
    report FAIL D-4 "gs://$bucket reports location '$location', expected $region"
  fi
  for secret_id in $(env_get "$environment" '.variables | to_entries[] | select(.value.kind == "secret") | .value.secret_id'); do
    if ! locations=$(provider secrets describe "$secret_id" --project="$project" \
        --format='value(replication.userManaged.replicas[].location)'); then
      report ERROR D-4 "the secret $secret_id in $project could not be described: $(provider_error)"
      continue
    fi
    locations=$(echo "$locations" | tr ';' ' ' | tr -s ' ')
    if [ "$(echo "$locations" | tr -d ' ')" = "$region" ]; then
      report PASS D-4 "$secret_id replicates to $region only"
    else
      report FAIL D-4 "$secret_id replicates to '${locations:-nowhere it reports}', expected $region only"
    fi
  done
done

finished=1
echo
if [ "$failures" -eq 0 ]; then
  echo "OK: every separation drill passed against $region."
else
  echo "$failures check(s) did not pass. A SKIP or ERROR counts as not passed — the drill is still owed."
  exit 1
fi
