#!/usr/bin/env bash
# Deployment record (TASK-018). Record: docs/architecture/cicd-pipeline.md
#
# CTL-40 asks for two things this script produces. Traceability — every deployment tied to a commit
# SHA and a Task ID — and, for PROD, "an explicit approval step recorded with approver identity and
# timestamp". GitHub halts the job until a reviewer approves; what it does not do is leave that
# approval anywhere an auditor can read a year later. So the approval is read back from the API
# that recorded it and written into the deployment record, and a gated environment whose approval
# cannot be read back does not get deployed to: an unevidenced approval is not an approval.
#
#   ENVIRONMENT=prod IMAGE_DIGEST=sha256:… TASK_ID=TASK-018 .github/scripts/deployment-record.sh
set -euo pipefail

: "${ENVIRONMENT:?ENVIRONMENT is unset}"
: "${TASK_ID:?TASK_ID is unset}"
: "${IMAGE_DIGEST:?IMAGE_DIGEST is unset}"
: "${GITHUB_SHA:?GITHUB_SHA is unset}"

GATES=${GATES:-infra/environments/github/environments.json}
OUTPUT=${OUTPUT:-artifacts/deployment-record-$ENVIRONMENT.json}
REPOSITORY=${GITHUB_REPOSITORY:-}
RUN_ID=${GITHUB_RUN_ID:-}

command -v jq >/dev/null 2>&1 || { echo "error: jq is required and is not on PATH" >&2; exit 2; }
[ -f "$GATES" ] || { echo "error: $GATES not found" >&2; exit 2; }

# Whether this environment's gate requires a human is declared once, in TASK-016's manifest, and
# read here. The pipeline does not carry a second list of which environments are gated.
reviewers=$(jq -r --arg e "$ENVIRONMENT" \
  '.environments[] | select(.name == $e) | .reviewer_teams | join(", ")' "$GATES")
gate=$(jq -r --arg e "$ENVIRONMENT" '.environments[] | select(.name == $e) | .gate' "$GATES")

approver="" ; approved_at="" ; approval_comment=""

if [ -n "$reviewers" ]; then
  [ -n "${GH_TOKEN:-}" ] || { echo "error: GH_TOKEN is unset; the approval on a gated environment cannot be read back" >&2; exit 1; }
  [ -n "$REPOSITORY" ] && [ -n "$RUN_ID" ] || { echo "error: GITHUB_REPOSITORY and GITHUB_RUN_ID are required to read the approval" >&2; exit 1; }

  approvals=$(gh api "repos/$REPOSITORY/actions/runs/$RUN_ID/approvals") ||
    { echo "error: could not read the approvals for run $RUN_ID" >&2; exit 1; }

  # One run can carry approvals for several environments; take this environment's own.
  this=$(printf '%s' "$approvals" | jq -c --arg e "$ENVIRONMENT" \
    '[.[] | select(.state == "approved") | select(.environments[]?.name == $e)] | last // empty')

  if [ -z "$this" ]; then
    cat >&2 <<MSG
error: no recorded approval for the '$ENVIRONMENT' environment on run $RUN_ID.

The environment declares reviewers ($reviewers), so this job ran only because someone approved it,
and CTL-40 requires that approval to be recorded with an identity and a timestamp. The approvals
API returned none, so nothing can be recorded and the deployment does not proceed.
MSG
    exit 1
  fi

  approver=$(printf '%s' "$this" | jq -r '.user.login // ""')
  approval_comment=$(printf '%s' "$this" | jq -r '.comment // ""')
  # The approval's own timestamp is the moment GitHub released the environment for this run.
  approved_at=$(printf '%s' "$this" | jq -r --arg e "$ENVIRONMENT" \
    '(.environments[] | select(.name == $e) | .updated_at) // ""')

  [ -n "$approver" ] || { echo "error: the approval for '$ENVIRONMENT' carries no approver identity" >&2; exit 1; }
  [ -n "$approved_at" ] || { echo "error: the approval for '$ENVIRONMENT' carries no timestamp" >&2; exit 1; }
fi

mkdir -p "$(dirname "$OUTPUT")"

jq -n \
  --arg environment "$ENVIRONMENT" \
  --arg gate "$gate" \
  --arg task_id "$TASK_ID" \
  --arg commit_sha "$GITHUB_SHA" \
  --arg image_digest "$IMAGE_DIGEST" \
  --arg reviewers "$reviewers" \
  --arg approver "$approver" \
  --arg approved_at "$approved_at" \
  --arg approval_comment "$approval_comment" \
  --arg deployed_at "$(date -u +%Y-%m-%dT%H:%M:%SZ)" \
  --arg triggered_by "${GITHUB_ACTOR:-}" \
  --arg run_url "${REPOSITORY:+https://github.com/$REPOSITORY/actions/runs/$RUN_ID}" \
  --arg authorisation "${ADR001_CONFIRMATION_REF:-}" \
  '{
    environment: $environment,
    gate: $gate,
    task_id: $task_id,
    commit_sha: $commit_sha,
    image_digest: $image_digest,
    approval: (if $reviewers == "" then
        { required: false, reviewer_teams: [] }
      else
        { required: true,
          reviewer_teams: ($reviewers | split(", ")),
          approver: $approver,
          approved_at: $approved_at,
          comment: $approval_comment }
      end),
    deployed_at: $deployed_at,
    triggered_by: $triggered_by,
    run_url: $run_url,
    authorisation: $authorisation
  }' >"$OUTPUT"

echo "deployment record: $OUTPUT"
cat "$OUTPUT"

if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
  {
    echo "### Deployment record — $ENVIRONMENT"
    echo
    echo "| Field | Value |"
    echo "| --- | --- |"
    echo "| Gate | $gate |"
    echo "| Task | $TASK_ID |"
    echo "| Commit | \`$GITHUB_SHA\` |"
    echo "| Image | \`$IMAGE_DIGEST\` |"
    if [ -n "$reviewers" ]; then
      echo "| Approved by | **$approver** at $approved_at |"
      echo "| Reviewer teams | $reviewers |"
    else
      echo "| Approval | not required — $gate is automatic |"
    fi
    echo "| Authorisation | ${ADR001_CONFIRMATION_REF:-—} |"
  } >>"$GITHUB_STEP_SUMMARY"
fi
