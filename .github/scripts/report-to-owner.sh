#!/usr/bin/env bash
# Reports a scheduled scan to its named owner (TASK-080, CTL-42, CTL-55).
# Record: docs/architecture/secret-and-dependency-scanning.md
#
#   report-to-owner.sh <issue title> <summary.md> <outcome>     outcome: success | failure
#
# Each scheduled scan keeps one issue, titled <issue title> and assigned to the owner named in
# .github/security/scan-owner.yaml. Every run comments on it with its outcome, its summary and a link
# to the run, so the owner is told every week whether or not anything was found, and the issue is
# the scan's weekly record. A closed issue is reopened by the next run that has findings. Nothing
# secret reaches the issue: the summaries are written from redacted reports.
#
# Needs gh, with GH_TOKEN holding `issues: write`.
set -euo pipefail

OWNER_FILE=.github/security/scan-owner.yaml
LABEL=security-scan

[ $# -eq 3 ] || { echo "usage: $0 <issue title> <summary.md> <success|failure>" >&2; exit 2; }
title=$1 summary=$2 outcome=$3
case "$outcome" in success | failure) ;; *) echo "error: outcome is '$outcome'" >&2; exit 2 ;; esac
[ -s "$summary" ] || { echo "error: $summary is missing or empty; the scan did not complete" >&2; exit 2; }

owner=$(sed -n 's/^github:[[:space:]]*\([A-Za-z0-9-]*\)[[:space:]]*$/\1/p' "$OWNER_FILE")
[ -n "$owner" ] || { echo "error: $OWNER_FILE names no github account" >&2; exit 2; }

run_url="${GITHUB_SERVER_URL:-https://github.com}/${GITHUB_REPOSITORY:?}/actions/runs/${GITHUB_RUN_ID:?}"
if [ "$outcome" = success ]; then verdict="Clean"; else verdict="**Findings — action needed**"; fi
body=$(printf '%s · %s · [run](%s) · `%s`\n\n%s\n' \
  "$verdict" "$(date -u +%Y-%m-%d)" "$run_url" "${GITHUB_SHA:0:12}" "$(cat "$summary")")

gh label create "$LABEL" --color B60205 --force \
  --description "Scheduled secret and dependency scans (TASK-080)" >/dev/null

number=$(gh issue list --label "$LABEL" --state all --limit 100 --json number,title \
  --jq "map(select(.title == \"$title\")) | first | .number // empty")

if [ -z "$number" ]; then
  gh issue create --title "$title" --label "$LABEL" --assignee "$owner" --body "$(printf '%s\n\n%s' \
    "The weekly record of this scheduled scan, owned by @$owner ($OWNER_FILE). Every run comments here; close the issue once a finding is resolved and the next run will reopen it if one returns." \
    "$body")"
  exit 0
fi

if [ "$outcome" = failure ] && [ "$(gh issue view "$number" --json state --jq .state)" = CLOSED ]; then
  gh issue reopen "$number"
fi
gh issue edit "$number" --add-assignee "$owner" >/dev/null
gh issue comment "$number" --body "$body"
