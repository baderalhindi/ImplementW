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

  # The policy is written per folder rather than once at the organisation, because the
  # organisation may hold AHDA systems this engagement does not govern.
  policy=$(mktemp)
  cat > "$policy" <<POLICY
name: folders/$folder_id/policies/gcp.resourceLocations
spec:
  rules:
    - values:
        allowedValues:
          - in:$region-locations
POLICY
  if [ "$DRY_RUN" = "1" ]; then
    echo "+ cat > gcp.resourceLocations.yaml <<EOF"
    sed 's/^/    /' "$policy"
    echo "+ gcloud org-policies set-policy gcp.resourceLocations.yaml"
  else
    gcloud org-policies set-policy "$policy"
  fi
  rm -f "$policy"

  run gcloud logging settings update --folder="$folder_id" --storage-location="$region"
done

cat <<NOTE

Both folders are bound to $region. Verify before creating any project:
  gcloud org-policies describe gcp.resourceLocations --folder=<folder-id> --effective
  gcloud logging settings describe --folder=<folder-id>
Then: infra/environments/provision-environment.sh <dev|sit|uat|prod>
NOTE
