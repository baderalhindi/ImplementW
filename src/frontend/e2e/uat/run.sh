#!/usr/bin/env bash
# Stages the UAT data set on the e2e stack, which stands in for UAT until it exists (TASK-088,
# docs/testing/uat/uat-execution.md §5).
#
#   src/frontend/e2e/uat/run.sh                     # build the stack from this tree, fresh database, stage every fixture
#   E2E_REUSE_STACK=1 src/frontend/e2e/uat/run.sh   # stage onto the stack already up (titles are not unique: stage once)
#
# Each fixture's ids, title and state land in src/frontend/test-results/uat/fixtures/<id>.json, which this script prints as
# the staging manifest. The stack stays up for the rehearsal and for testers.
set -euo pipefail
stack="$(cd "$(dirname "${BASH_SOURCE[0]}")/../stack" && pwd)"
compose=(docker compose -f "$stack/compose.yml")

if [[ "${E2E_REUSE_STACK:-}" != "1" ]]; then
  "${compose[@]}" down --volumes 2>/dev/null || true
  "${compose[@]}" up --detach --build --wait --quiet-pull
fi
rm -rf "$stack/../../test-results/uat"
status=0
"${compose[@]}" run --rm --no-deps e2e-runner --config e2e/uat.config.ts "$@" || status=$?
cat "$stack/../../test-results/uat/fixtures/"*.json 2>/dev/null || true
exit "$status"
