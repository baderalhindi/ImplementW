#!/usr/bin/env bash
# Runs the platform accessibility audit against the e2e stack (TASK-087, docs/testing/accessibility-audit.md §6).
#
#   src/frontend/e2e/accessibility/run.sh                     # build the stack from this tree, fresh database, audit every screen
#   src/frontend/e2e/accessibility/run.sh -g A11Y-S05         # the same, for the screens whose title matches
#   E2E_REUSE_STACK=1 src/frontend/e2e/accessibility/run.sh   # against the stack already up
#
# One JSON record, ARIA snapshot and screenshot per screen and language land in src/frontend/test-results/accessibility;
# `python3 docs/testing/accessibility/accessibility-audit.py --record <name>` keeps the run's figures for the report.
set -euo pipefail
stack="$(cd "$(dirname "${BASH_SOURCE[0]}")/../stack" && pwd)"
compose=(docker compose -f "$stack/compose.yml")

if [[ "${E2E_REUSE_STACK:-}" != "1" ]]; then
  "${compose[@]}" down --volumes 2>/dev/null || true
  "${compose[@]}" up --detach --build --wait --quiet-pull
fi
rm -rf "$stack/../../test-results/accessibility"
"${compose[@]}" run --rm --no-deps e2e-runner --config e2e/accessibility.config.ts "$@"
