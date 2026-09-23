#!/usr/bin/env bash
# Release metadata (TASK-018). Record: docs/architecture/cicd-pipeline.md
#
# CTL-40's third clause — "every deployment traceable to a commit SHA and Task ID" — is decided
# here, once, before anything is built. The commit SHA is given; the Task ID is not, so it is read
# from the commit that is about to be released and the release stops if it is absent. A deployment
# that cannot name its task is the one this control exists to prevent, so this is a failure and not
# a warning.
#
#   .github/scripts/release-metadata.sh            # reads GITHUB_SHA, writes GITHUB_OUTPUT
#   COMMIT_SHA=<sha> .github/scripts/release-metadata.sh
set -euo pipefail

COMMIT_SHA=${COMMIT_SHA:-${GITHUB_SHA:-}}
[ -n "$COMMIT_SHA" ] || { echo "error: COMMIT_SHA (or GITHUB_SHA) is unset" >&2; exit 1; }

git cat-file -e "${COMMIT_SHA}^{commit}" 2>/dev/null ||
  { echo "error: $COMMIT_SHA is not a commit in this checkout" >&2; exit 1; }

subject=$(git log -1 --format=%s "$COMMIT_SHA")
message=$(git log -1 --format=%B "$COMMIT_SHA")

# The same pattern pr-policy.sh accepts, so a branch that passed the PR gate produces a releasable
# commit: TASK-nnn in any case, anywhere in the subject or body. Squash merges carry the PR title,
# which pr-policy.sh has already required to name a task.
task_id=$(printf '%s' "$message" | grep -oiE 'TASK-[0-9]{3}' | head -n 1 | tr '[:lower:]' '[:upper:]' || true)

if [ -z "$task_id" ]; then
  cat >&2 <<MSG
error: commit $COMMIT_SHA names no Task ID, so a deployment from it could not be traced to one.

  subject: $subject

CTL-40 requires every deployment to be traceable to a commit SHA and a Task ID. Squash-merge
subjects carry the pull request title, and .github/scripts/pr-policy.sh already requires that title
or body to name a TASK-nnn. A commit that reaches main without one was merged outside that gate.

To release this commit, revert it and re-merge through a pull request that names its task.
MSG
  exit 1
fi

short_sha=${COMMIT_SHA:0:12}
# The image tag is the commit, not a version: one artifact is built once per commit on main and
# promoted unchanged through the four environments (environment-separation.md §4). Deployments use
# the digest rather than this tag; the tag exists so a human can find the image from a commit.
image_tag="sha-${short_sha}"

if [ -n "${GITHUB_OUTPUT:-}" ]; then
  {
    echo "commit_sha=$COMMIT_SHA"
    echo "short_sha=$short_sha"
    echo "task_id=$task_id"
    echo "image_tag=$image_tag"
  } >>"$GITHUB_OUTPUT"
fi

if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
  {
    echo "### Release candidate"
    echo
    echo "| Field | Value |"
    echo "| --- | --- |"
    echo "| Commit | \`$COMMIT_SHA\` |"
    echo "| Task | $task_id |"
    echo "| Subject | $subject |"
    echo "| Image tag | \`$image_tag\` |"
  } >>"$GITHUB_STEP_SUMMARY"
fi

echo "release candidate: $task_id at $COMMIT_SHA (image tag $image_tag)"
