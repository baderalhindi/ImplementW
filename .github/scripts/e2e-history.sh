#!/usr/bin/env bash
# Records a nightly cross-domain journey run where the team sees it (TASK-085). Record: docs/testing/e2e-suite.md §8
#
#   e2e-history.sh <summary.md> <success|failure>
#
# The nightly runs keep one issue, "Nightly cross-domain journeys", assigned to the QA owner named in
# .github/testing/e2e-owner.yaml. Every run comments on it with its outcome, its per-journey table and a link to the run, so
# the issue is the suite's pass/fail history; a red night reopens it if it was closed. Actions' run list for the
# e2e-journeys workflow is the same history with the reports attached.
#
# Needs gh, with GH_TOKEN holding `issues: write`.
set -euo pipefail

OWNER_FILE=.github/testing/e2e-owner.yaml
LABEL=e2e-journeys
TITLE="Nightly cross-domain journeys"

[ $# -eq 2 ] || { echo "usage: $0 <summary.md> <success|failure>" >&2; exit 2; }
summary=$1 outcome=$2
case "$outcome" in success | failure) ;; *) echo "error: outcome is '$outcome'" >&2; exit 2 ;; esac
[ -s "$summary" ] || { echo "error: $summary is missing or empty; the suite did not report" >&2; exit 2; }

owner=$(sed -n 's/^github:[[:space:]]*\([A-Za-z0-9-]*\)[[:space:]]*$/\1/p' "$OWNER_FILE")
[ -n "$owner" ] || { echo "error: $OWNER_FILE names no github account" >&2; exit 2; }

run_url="${GITHUB_SERVER_URL:-https://github.com}/${GITHUB_REPOSITORY:?}/actions/runs/${GITHUB_RUN_ID:?}"
if [ "$outcome" = success ]; then verdict="Green"; else verdict="**Red — promotion to UAT is blocked**"; fi
body=$(printf '%s · %s · [run and reports](%s) · `%s`\n\n%s\n' \
  "$verdict" "$(date -u +%Y-%m-%d)" "$run_url" "${GITHUB_SHA:0:12}" "$(cat "$summary")")

gh label create "$LABEL" --color 1D76DB --force --description "Nightly cross-domain journey runs (TASK-085)" >/dev/null

number=$(gh issue list --label "$LABEL" --state all --limit 100 --json number,title \
  --jq "map(select(.title == \"$TITLE\")) | first | .number // empty")

if [ -z "$number" ]; then
  gh issue create --title "$TITLE" --label "$LABEL" --assignee "$owner" --body "$(printf '%s\n\n%s' \
    "The pass/fail history of the nightly cross-domain journey suite (docs/testing/e2e-suite.md), owned by @$owner ($OWNER_FILE). Every night's run comments here; a red night reopens the issue." \
    "$body")"
  exit 0
fi

if [ "$outcome" = failure ] && [ "$(gh issue view "$number" --json state --jq .state)" = CLOSED ]; then
  gh issue reopen "$number"
fi
gh issue edit "$number" --add-assignee "$owner" >/dev/null
gh issue comment "$number" --body "$body"
