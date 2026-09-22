#!/usr/bin/env sh
# Shared loader and guards for the environment provisioning scripts (TASK-016).
# Sourced, never executed. Record: docs/architecture/environment-separation.md
#
# Every guard here exists because a mistake it prevents cannot be corrected after the fact:
# a project created outside the named in-Kingdom region (ADR-001 C-1) or in the wrong GCP
# organisation (ADR-001 R-3) is rebuilt, not reconfigured.

MANIFEST=${MANIFEST:-"$(cd "$(dirname "$0")" && pwd)/environments.json"}
DRY_RUN=${DRY_RUN:-0}

die() {
  echo "error: $*" >&2
  exit 1
}

require_tools() {
  for tool in "$@"; do
    command -v "$tool" >/dev/null 2>&1 || die "$tool is required and is not on PATH"
  done
}

# manifest_get <jq-filter> — reads the manifest, empty string for null.
manifest_get() {
  jq -r "$1 // \"\"" "$MANIFEST"
}

# env_get <environment> <jq-filter relative to the environment object>
env_get() {
  jq -r --arg name "$1" ".environments[] | select(.name == \$name) | $2 // \"\"" "$MANIFEST"
}

environment_names() {
  jq -r '.environments[].name' "$MANIFEST"
}

require_known_environment() {
  [ -n "${1:-}" ] || die "usage: $(basename "$0") <dev|sit|uat|prod> [--dry-run]"
  environment_names | grep -qx "$1" || die "unknown environment '$1'; the manifest declares: $(environment_names | tr '\n' ' ')"
}

# The region is UGV-07 and is not in the manifest until AHDA IT names it. Nothing is created
# without it, and nothing is created in a region the allowlist does not hold: me-central1 is
# Doha, Qatar, and is out of Kingdom.
require_region() {
  region=$(manifest_get '.platform.region')
  [ -n "$region" ] || die "platform.region is empty in $MANIFEST. The in-Kingdom region name is UGV-07, owed by AHDA IT (ADR-001 R-2). Nothing is provisioned until it is named."
  jq -e --arg r "$region" '.platform.region_allowlist | index($r)' "$MANIFEST" >/dev/null ||
    die "region '$region' is not on platform.region_allowlist. ADR-001 C-1: no resource in any region outside Saudi Arabia."
  echo "$region"
}

require_platform_values() {
  for field in organization_id billing_account_id; do
    value=$(manifest_get ".platform.$field")
    [ -n "$value" ] || die "platform.$field is empty in $MANIFEST — UGV-07, owed by AHDA IT (ADR-001 R-3, tenancy ownership)."
  done
}

# ADR-001 §9's provisioning control: no apply against any environment, DEV included, until the
# written confirmation (R-1), the region (R-2) and the tenancy (R-3) are closed. The reference
# is echoed into the run so the evidence log records what authorised the run.
require_authorisation() {
  [ -n "${ADR001_CONFIRMATION_REF:-}" ] || die "ADR001_CONFIRMATION_REF is unset. ADR-001 §9 holds every environment, DEV included, until R-1, R-2 and R-3 are closed in writing. Set it to the written confirmation's reference (for example: ADR001_CONFIRMATION_REF='AHDA IT memo 2026-10-05') once it exists."
  echo "authorised by: $ADR001_CONFIRMATION_REF"
}

# run <command...> — executes, or prints under --dry-run. Every provisioning call goes through it.
# The echo quotes any argument holding a space, so a dry run can be pasted into a shell as it stands.
echo_command() {
  printf '+'
  for arg in "$@"; do
    case "$arg" in
      *" "*) printf " '%s'" "$arg" ;;
      *) printf ' %s' "$arg" ;;
    esac
  done
  printf '\n'
}

run() {
  if [ "$DRY_RUN" = "1" ]; then
    echo_command "$@"
  else
    echo_command "$@" >&2
    "$@"
  fi
}

parse_dry_run() {
  for arg in "$@"; do
    [ "$arg" = "--dry-run" ] && DRY_RUN=1
  done
  export DRY_RUN
}
