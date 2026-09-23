#!/usr/bin/env sh
# Shared loader for the secret-store scripts (TASK-019). Sourced, never executed.
# Record: docs/architecture/secret-management.md
#
# The environment manifest and its guards belong to TASK-016; this file adds only what is specific
# to handling a secret, and one rule that governs all of it: a secret value never becomes a command
# argument. common.sh's run() echoes every argument it is given, and a process's arguments are
# readable by anyone who can list processes, so values reach gcloud on stdin (--data-file=-) and are
# never printed, logged or stored in a shell history (Blueprint Section 22.1, CTL-18).

here=$(cd "$(dirname "$0")" && pwd)
MANIFEST=${MANIFEST:-"$here/../environments/environments.json"}
export MANIFEST
# shellcheck source=../environments/lib/common.sh
. "$here/../environments/lib/common.sh"

SHEET=${SHEET:-"$here/../../docs/architecture/environment-and-secrets.csv"}

# secret_id <environment> <VARIABLE_NAME> — the id the manifest records, or empty if the sheet does
# not scope that variable to that environment. The manifest is the only place ids are read from.
secret_id() {
  jq -r --arg name "$1" --arg variable "$2" \
    '.environments[] | select(.name == $name) | .variables[$variable] | select(.kind == "secret") | .secret_id // ""' \
    "$MANIFEST"
}

# secret_ids <environment> — every secret id the environment holds, one per line.
secret_ids() {
  env_get "$1" '.variables | to_entries[] | select(.value.kind == "secret") | .value.secret_id'
}

require_known_variable() {
  environment=$1
  variable=$2
  id=$(secret_id "$environment" "$variable")
  [ -n "$id" ] || die "$variable is not a secret of $environment in $MANIFEST. The inventory is derived from the Environment and Secrets sheet: add the row there, then run 'python3 infra/secrets/inventory.py --write'."
  echo "$id"
}

# sheet_column <VARIABLE_NAME> <column header> — a cell of the sheet snapshot, quoting handled.
# The runbook prints the sheet's own Verification Method rather than restating it.
sheet_column() {
  python3 -c '
import csv, sys
with open(sys.argv[1], newline="", encoding="utf-8") as handle:
    for row in csv.DictReader(handle):
        if row["Variable Name"] == sys.argv[2]:
            print(row[sys.argv[3]])
            break
' "$SHEET" "$1" "$2"
}
