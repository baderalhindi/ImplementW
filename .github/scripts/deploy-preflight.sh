#!/usr/bin/env bash
# Deployment preflight (TASK-018). Record: docs/architecture/cicd-pipeline.md
#
# The same three hold points that stop `provision-environment.sh` (infra/environments/lib/common.sh)
# and `terraform plan` (infra/terraform/platform/guards.tf) stop the pipeline, and they are checked
# here rather than discovered as an authentication error halfway through a deploy. The values are
# read from infra/environments/environments.json, which is TASK-016's manifest and the only place
# the region, the tenancy and the environment identifiers are written down.
#
#   .github/scripts/deploy-preflight.sh                 report readiness; always exits 0
#   .github/scripts/deploy-preflight.sh --require dev   fail unless dev can be deployed to
set -euo pipefail

MANIFEST=${MANIFEST:-infra/environments/environments.json}
REQUIRE=0
ENVIRONMENT=""

while [ $# -gt 0 ]; do
  case "$1" in
    --require) REQUIRE=1 ;;
    -*) echo "error: unknown option $1" >&2; exit 2 ;;
    *) ENVIRONMENT=$1 ;;
  esac
  shift
done

command -v jq >/dev/null 2>&1 || { echo "error: jq is required and is not on PATH" >&2; exit 2; }
[ -f "$MANIFEST" ] || { echo "error: $MANIFEST not found" >&2; exit 2; }

blockers=()

manifest_get() { jq -r "$1 // \"\"" "$MANIFEST"; }

region=$(manifest_get '.platform.region')
if [ -z "$region" ]; then
  blockers+=("platform.region is empty — the in-Kingdom region name is UGV-07, owed by AHDA IT (ADR-001 R-2)")
elif ! jq -e --arg r "$region" '.platform.region_allowlist | index($r)' "$MANIFEST" >/dev/null; then
  blockers+=("region '$region' is not on platform.region_allowlist — ADR-001 C-1 admits no region outside Saudi Arabia")
fi

for field in organization_id billing_account_id; do
  [ -n "$(manifest_get ".platform.$field")" ] ||
    blockers+=("platform.$field is empty — UGV-07, owed by AHDA IT (ADR-001 R-3, tenancy ownership)")
done

[ -n "$(manifest_get '.platform.base_domain')" ] ||
  blockers+=("platform.base_domain is empty — the AHDA-owned DNS zone (environment-separation.md F-1); no managed certificate can be issued without it")

# One artifact is built once and promoted unchanged, so the four environments pull the same image
# from one registry. Which project holds that registry is nobody's declared value yet (cicd-pipeline.md
# F-1), and the registry lives in the named region like everything else (ADR-001 C-1), so neither the
# hostname nor the repository path can be formed today. This is why the pipeline cannot push an image,
# not merely why it cannot deploy one.
[ -n "$(manifest_get '.platform.artifact_registry.project_id')" ] ||
  blockers+=("platform.artifact_registry.project_id is empty — the project holding the shared Artifact Registry (cicd-pipeline.md F-1); owed by the DevOps/Platform Lead")

# ADR-001 §9's provisioning control, carried into the pipeline: no apply against any environment,
# DEV included, until the written confirmation closes. The reference is recorded on the deployment.
[ -n "${ADR001_CONFIRMATION_REF:-}" ] ||
  blockers+=("ADR001_CONFIRMATION_REF is unset — ADR-001 §9 holds every environment until R-1, R-2 and R-3 are closed in writing; set it as a repository variable naming the written confirmation")

if [ -n "$ENVIRONMENT" ]; then
  if jq -e --arg e "$ENVIRONMENT" '.environments[] | select(.name == $e)' "$MANIFEST" >/dev/null; then
    # The post-deploy smoke test calls this environment's own origin, so a deployment that cannot be
    # verified is not attempted (environment-separation.md D-1: one origin per environment).
    [ -n "$(jq -r --arg e "$ENVIRONMENT" '.environments[] | select(.name == $e) | .variables.APP_BASE_URL.value // ""' "$MANIFEST")" ] ||
      blockers+=("APP_BASE_URL is empty for '$ENVIRONMENT' (environment-separation.md F-1) — the deployment could not be smoke-tested")
  else
    blockers+=("'$ENVIRONMENT' is not an environment in $MANIFEST")
  fi
fi

# Nothing is resolved while anything is outstanding: a half-formed registry path or project id is a
# value a later step would use as if it were real.
ready=false
image_repository=""
project_id=""
deploy_service_account=""
app_base_url=""
if [ ${#blockers[@]} -eq 0 ]; then
  ready=true
  image_repository="${region}-docker.pkg.dev/$(manifest_get '.platform.artifact_registry.project_id')/$(manifest_get '.platform.artifact_registry.repository')/$(manifest_get '.platform.artifact_registry.image')"
  if [ -n "$ENVIRONMENT" ]; then
    project_id=$(jq -r --arg e "$ENVIRONMENT" '.environments[] | select(.name == $e) | .project_id' "$MANIFEST")
    deploy_service_account=$(jq -r --arg e "$ENVIRONMENT" '.environments[] | select(.name == $e) | .service_accounts.deploy' "$MANIFEST")
    app_base_url=$(jq -r --arg e "$ENVIRONMENT" '.environments[] | select(.name == $e) | .variables.APP_BASE_URL.value' "$MANIFEST")
  fi
fi

if [ -n "${GITHUB_OUTPUT:-}" ]; then
  {
    echo "ready=$ready"
    echo "region=$region"
    echo "project_id=$project_id"
    echo "image_repository=$image_repository"
    echo "deploy_service_account=$deploy_service_account"
    echo "app_base_url=$app_base_url"
  } >>"$GITHUB_OUTPUT"
fi

if [ "$ready" = "true" ]; then
  echo "preflight: ready${ENVIRONMENT:+ for $ENVIRONMENT} — region $region, authorised by ${ADR001_CONFIRMATION_REF:-}"
  exit 0
fi

printf 'preflight: not ready%s. %d item(s) outstanding:\n' "${ENVIRONMENT:+ for $ENVIRONMENT}" "${#blockers[@]}" >&2
for blocker in "${blockers[@]}"; do
  printf '  - %s\n' "$blocker" >&2
done

if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
  {
    echo "### Deployment preflight: not ready"
    echo
    echo "No environment can be deployed to until these close. The build and the gates above ran in full."
    echo
    for blocker in "${blockers[@]}"; do
      echo "- $blocker"
    done
  } >>"$GITHUB_STEP_SUMMARY"
fi

if [ "$REQUIRE" = "1" ]; then
  exit 1
fi
exit 0
