#!/usr/bin/env sh
# Rotates one secret end to end (TASK-019): new version, soak, verify, retire the old one — and the
# same steps in reverse when the new value turns out to be wrong.
#
#   printf %s "$new" | infra/secrets/rotate-secret.sh sit AD_BIND_PASSWORD
#   printf %s "$new" | infra/secrets/rotate-secret.sh sit AD_BIND_PASSWORD --wait 60 --verified
#   infra/secrets/rotate-secret.sh sit AD_BIND_PASSWORD --rollback
#
# Why the order is new-then-retire and not retire-then-new: "latest" resolves to the most recent
# ENABLED version, so adding a version switches the application over and disabling a version switches
# it back. Both directions are one API call and neither destroys anything, which is what makes the
# rollback in §5 of the runbook a step rather than an incident.
#
# Requires: gcloud, jq. Runbook: infra/secrets/secret-rotation-runbook.md
set -eu

here=$(cd "$(dirname "$0")" && pwd)
. "$here/lib.sh"

environment=${1:-}
variable=${2:-}
shift 2 2>/dev/null || true
parse_dry_run "$@"

wait_seconds=300
verified=0
rollback=0
while [ $# -gt 0 ]; do
  case $1 in
    --wait)
      wait_seconds=${2:-}
      case $wait_seconds in
        '' | *[!0-9]*) die "--wait takes a whole number of seconds" ;;
      esac
      shift 2
      ;;
    --verified) verified=1 && shift ;;
    --rollback) rollback=1 && shift ;;
    --dry-run) shift ;;
    *) die "unknown option '$1'" ;;
  esac
done

require_tools jq
[ "$DRY_RUN" = "1" ] || require_tools gcloud
require_known_environment "$environment"
[ -n "$variable" ] || die "usage: $(basename "$0") <dev|sit|uat|prod> <VARIABLE_NAME> [--wait s] [--verified] [--rollback] [--dry-run]"

id=$(require_known_variable "$environment" "$variable")
project=$(env_get "$environment" '.project_id')

# versions <state> — version numbers in that state, newest first.
versions() {
  if [ "$DRY_RUN" = "1" ]; then
    echo "<versions:$1>"
  else
    gcloud secrets versions list "$id" --project="$project" --filter="state:$1" \
      --sort-by=~createTime --format='value(name)'
  fi
}

confirm() {
  [ "$verified" = "1" ] && return 0
  [ "$DRY_RUN" = "1" ] && return 0
  [ -r /dev/tty ] || die "no terminal to confirm on; pass --verified when the verification is done another way"
  printf '%s [y/N] ' "$1"
  read -r answer </dev/tty
  case $answer in
    y | Y) return 0 ;;
    *) return 1 ;;
  esac
}

echo "== $variable -> $id (project $project, environment $environment) =="

if [ "$rollback" = "1" ]; then
  current=$(versions ENABLED | head -1)
  restore=$(versions DISABLED | head -1)
  [ -n "$current" ] || die "$id has no enabled version to roll back from"
  [ -n "$restore" ] || die "$id has no disabled version to roll back to; the previous value is gone and the owner must set one with set-secret-version.sh"
  echo "   rollback: enable version $restore, then disable version $current"
  confirm "Roll back $variable in $environment?" || die "rollback abandoned; nothing was changed"
  # Enable first: for the moment both are enabled the application still reads the newer one, so
  # there is no window in which the secret has no value at all.
  run gcloud secrets versions enable "$restore" --secret="$id" --project="$project"
  run gcloud secrets versions disable "$current" --secret="$id" --project="$project"
  echo "   version $restore is the latest enabled version again."
  exit 0
fi

previous=$(versions ENABLED | head -1)
echo "   current enabled version: ${previous:-none}"

# 1. The new value. set-secret-version.sh reads it from this script's stdin and validates it.
if [ "$DRY_RUN" = "1" ]; then
  "$here/set-secret-version.sh" "$environment" "$variable" --dry-run
else
  "$here/set-secret-version.sh" "$environment" "$variable"
fi

# 2. The soak. Long enough for every running instance to have re-read the store at least once
#    (SecretStoreOptions.RefreshInterval, 5 minutes by default) before the old value is retired.
if [ "$DRY_RUN" = "1" ]; then
  echo "   (dry run: would wait ${wait_seconds}s for running instances to re-read the store)"
else
  echo "   waiting ${wait_seconds}s for running instances to re-read the store..."
  sleep "$wait_seconds"
fi

# 3. The verification the sheet requires for this variable. It is the sheet's own wording, not a
#    restatement of it, so a change to the sheet reaches the operator running the rotation.
cat <<NOTE

Verify before the old value is retired (Environment and Secrets sheet, Verification Method):
   $(sheet_column "$variable" 'Verification Method')
NOTE

confirm "Did $variable verify in $environment?" || die "not verified. The previous value is still enabled: run '$(basename "$0") $environment $variable --rollback' to switch back to it."

# 4. Retire, do not destroy. A disabled version can be re-enabled; a destroyed one cannot, and the
#    destruction of the superseded value is governed by a retention period nobody has set (OQ-003).
if [ -n "$previous" ]; then
  run gcloud secrets versions disable "$previous" --secret="$id" --project="$project"
  echo "   version $previous disabled; it can be re-enabled with --rollback until it is destroyed."
else
  echo "   no previous version to retire."
fi

echo "== $variable rotated in $environment =="
