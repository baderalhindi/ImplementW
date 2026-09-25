#!/usr/bin/env sh
# Creates the two folders that hold the environments and binds the locality constraints to them
# (TASK-016). Run once, before any project exists, and re-run to reconcile: both steps below are
# fixed at creation time for every project underneath and cannot be corrected afterwards.
#
#   infra/environments/apply-org-policy.sh [--dry-run]
#
#   1. constraints/gcp.resourceLocations, allowlisting the named in-Kingdom region only, so an
#      apply naming any other region FAILS rather than relying on a reviewer (ADR-001 C-1, CTL-01).
#   2. the default log-bucket storage location, which is fixed when a project is created and cannot
#      be changed later (ADR-001 C-6, CTL-28).
#   3. three network constraints (TASK-021, CTL-03), each of which removes a way for a project to
#      acquire a public path nobody wrote in Terraform:
#        compute.skipDefaultNetworkCreation — no "default" VPC, which GCP otherwise creates with
#          SSH and RDP open to 0.0.0.0/0 the moment the Compute API is enabled
#        compute.vmExternalIpAccess (deny all) — no VM can hold a public address
#        sql.restrictPublicIp — no Cloud SQL instance can be given one, whatever the Terraform says
#      The first is also fixed at project creation: a default network created before it is bound
#      has to be deleted by hand.
#
# Requires: gcloud (authenticated with organisation-policy and folder-creation rights), jq.
# Record: docs/architecture/environment-separation.md
set -eu

here=$(cd "$(dirname "$0")" && pwd)
. "$here/lib/common.sh"

parse_dry_run "$@"
require_tools jq
[ "$DRY_RUN" = "1" ] || require_tools gcloud  # --dry-run prints the calls, it does not make them
require_platform_values
region=$(require_region)
require_authorisation

org=$(manifest_get '.platform.organization_id')

# set_folder_policy <folder-id> <constraint> <rules> — binds one organisation-policy constraint to a
# folder. <rules> is the YAML list under spec.rules, indented four spaces.
set_folder_policy() {
  policy=$(mktemp)
  printf 'name: folders/%s/policies/%s\nspec:\n  rules:\n%s\n' "$1" "$2" "$3" > "$policy"
  if [ "$DRY_RUN" = "1" ]; then
    echo "+ cat > $2.yaml <<EOF"
    sed 's/^/    /' "$policy"
    echo "+ gcloud org-policies set-policy $2.yaml"
  else
    gcloud org-policies set-policy "$policy"
  fi
  rm -f "$policy"
}

for folder in $(manifest_get '.platform.folders[].name'); do
  echo "== $folder =="

  if [ "$DRY_RUN" = "1" ]; then
    folder_id="<folder-id:$folder>"
    run gcloud resource-manager folders create --organization="$org" --display-name="$folder"
  else
    folder_id=$(gcloud resource-manager folders list --organization="$org" \
      --filter="displayName=$folder" --format='value(name)')
    if [ -z "$folder_id" ]; then
      gcloud resource-manager folders create --organization="$org" --display-name="$folder"
      folder_id=$(gcloud resource-manager folders list --organization="$org" \
        --filter="displayName=$folder" --format='value(name)')
    fi
  fi

  # Policies are written per folder rather than once at the organisation, because the
  # organisation may hold AHDA systems this engagement does not govern.
  set_folder_policy "$folder_id" gcp.resourceLocations "    - values:
        allowedValues:
          - in:$region-locations"

  # TASK-021. Each of these makes a public path impossible to create rather than merely absent.
  set_folder_policy "$folder_id" compute.skipDefaultNetworkCreation "    - enforce: true"
  set_folder_policy "$folder_id" compute.vmExternalIpAccess "    - denyAll: true"
  set_folder_policy "$folder_id" sql.restrictPublicIp "    - enforce: true"

  run gcloud logging settings update --folder="$folder_id" --storage-location="$region"
done

cat <<NOTE

Both folders are bound to $region. Verify before creating any project:
  gcloud org-policies describe gcp.resourceLocations --folder=<folder-id> --effective
  gcloud org-policies describe compute.skipDefaultNetworkCreation --folder=<folder-id> --effective
  gcloud logging settings describe --folder=<folder-id>
Then: infra/environments/provision-environment.sh <dev|sit|uat|prod>
NOTE
