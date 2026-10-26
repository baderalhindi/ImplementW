#!/usr/bin/env bash
# Runs the cross-domain journey suite against the e2e stack (TASK-085, docs/testing/e2e-suite.md §7).
#
#   src/frontend/e2e/stack/run.sh                    # build the stack from this tree, start it fresh, run every journey
#   src/frontend/e2e/stack/run.sh journeys/uj-001    # the same, for the journeys whose path matches
#   E2E_REUSE_STACK=1 src/frontend/e2e/stack/run.sh  # run against the stack already up, without rebuilding or resetting it
#
# The stack stays up afterwards for inspection; `docker compose -f src/frontend/e2e/stack/compose.yml down -v` removes it.
# The report lands in src/frontend/playwright-report, JUnit and JSON results in src/frontend/test-results. Exits with the
# suite's own status.
set -euo pipefail
compose=(docker compose -f "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/compose.yml")

if [[ "${E2E_REUSE_STACK:-}" != "1" ]]; then
  # A fresh database every run: the e2e data set is provisioned once, and journeys never meet a previous run's records.
  "${compose[@]}" down --volumes 2>/dev/null || true
  "${compose[@]}" up --detach --build --wait --quiet-pull
fi
"${compose[@]}" run --rm --no-deps e2e-runner "$@"
