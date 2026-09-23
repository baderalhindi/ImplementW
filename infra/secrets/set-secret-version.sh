#!/usr/bin/env sh
# Writes one secret value into the approved secret store (TASK-019). The value is read from stdin and
# is never an argument, never echoed and never written to a file.
#
#   printf %s "$value" | infra/secrets/set-secret-version.sh sit AD_BIND_PASSWORD
#   infra/secrets/set-secret-version.sh sit AD_BIND_PASSWORD --dry-run < /dev/null
#
# The owner named in the Environment and Secrets sheet runs this, for their own variables. It adds a
# version; it never disables or destroys one, so a write that turns out to be wrong is undone by
# rotating again and the previous value is still there (infra/secrets/rotate-secret.sh --rollback).
#
# Requires: gcloud (authenticated as a principal with secretmanager.versions.add on the secret), jq.
# Record: docs/architecture/secret-management.md
set -eu

here=$(cd "$(dirname "$0")" && pwd)
. "$here/lib.sh"

environment=${1:-}
variable=${2:-}
parse_dry_run "$@"
require_tools jq
[ "$DRY_RUN" = "1" ] || require_tools gcloud
require_known_environment "$environment"
[ -n "$variable" ] || die "usage: $(basename "$0") <dev|sit|uat|prod> <VARIABLE_NAME> [--dry-run]   # value on stdin"

id=$(require_known_variable "$environment" "$variable")
project=$(env_get "$environment" '.project_id')

# Read the value before anything is called, so a store that would reject it is never contacted, and
# check it here rather than letting an empty version be created: the application treats a secret with
# no value and a secret that does not exist alike, and both stop it from starting.
value=$(cat)
if [ -z "$value" ] && [ "$DRY_RUN" != "1" ]; then
  die "no value on stdin for $variable. Pipe it in: printf %s \"\$value\" | $(basename "$0") $environment $variable"
fi

echo "== $variable -> $id (project $project) =="
echo "   classification: $(sheet_column "$variable" Classification) | owner: $(sheet_column "$variable" Owner)"

if [ "$DRY_RUN" = "1" ]; then
  echo_command gcloud secrets versions add "$id" --project="$project" --data-file=-
  echo "   (dry run: $(printf %s "$value" | wc -c | tr -d ' ') bytes on stdin were read and discarded)"
  exit 0
fi

# --data-file=- keeps the value on stdin. printf %s adds no trailing newline: a connection string
# with a stray \n at the end fails in ways that are hard to see in a log.
printf %s "$value" | gcloud secrets versions add "$id" --project="$project" --data-file=- >/dev/null

version=$(gcloud secrets versions list "$id" --project="$project" --filter='state:ENABLED' \
  --sort-by=~createTime --limit=1 --format='value(name)')

cat <<NOTE
   version $version is now the latest enabled version.

Verification (Environment and Secrets sheet, Verification Method):
   $(sheet_column "$variable" 'Verification Method')

A running application picks the new value up within its refresh interval (SecretStoreOptions.RefreshInterval,
5 minutes by default) without a restart. To retire the value this one replaces, and to rehearse the
rollback first, use infra/secrets/rotate-secret.sh.
NOTE
