#!/usr/bin/env bash
# PR policy check (TASK-012). Fails unless the pull request:
#   1. comes from a branch named type/task-nnn-short-description (dependabot/renovate branches exempt);
#   2. names a Task ID (TASK-nnn) in its title or body;
#   3. has a filled-in "## Test evidence" section;
#   4. has a "## Security checklist" section with every box ticked.
# Inputs are environment variables so that PR text is never interpolated into shell code:
#   PR_HEAD_REF, PR_TITLE, PR_BODY
set -euo pipefail

branch_types='feat|fix|chore|docs|refactor|test|ci|build|perf|revert|hotfix'
failures=0

fail() { echo "::error::$1"; failures=$((failures + 1)); }

# Body with HTML comments removed, so template guidance never counts as content.
body=$(printf '%s\n' "${PR_BODY:-}" | perl -0pe 's/<!--.*?-->//gs')

# Returns the lines of one "## <heading>" section, up to the next "## ".
section() {
  printf '%s\n' "$body" | awk -v h="## $1" '
    $0 == h { on = 1; next }
    on && /^## / { exit }
    on { print }'
}

# 1. Branch name
case "${PR_HEAD_REF:-}" in
  dependabot/*|renovate/*) ;;
  *)
    if ! printf '%s' "${PR_HEAD_REF:-}" | grep -Eq "^(${branch_types})/task-[0-9]{3}-[a-z0-9]+(-[a-z0-9]+)*$"; then
      fail "Branch '${PR_HEAD_REF:-}' must be named type/task-nnn-short-description, type in {${branch_types//|/, }}."
    fi ;;
esac

# 2. Task ID
if ! printf '%s\n%s' "${PR_TITLE:-}" "$body" | grep -Eiq 'TASK-[0-9]{3}'; then
  fail "The PR title or body must name a Task ID (TASK-nnn)."
fi

# 3-4 need a description. GitHub pre-fills the template only once it exists on the default branch,
# so a PR opened before then (including the one that introduces it) must paste it in by hand.
if ! printf '%s\n' "$body" | grep -q '[^[:space:]]'; then
  fail "The PR description is empty. Paste .github/PULL_REQUEST_TEMPLATE.md into the description and fill it in."
  echo "pr-policy: $failures failure(s). See .github/PULL_REQUEST_TEMPLATE.md and docs/architecture/branching-strategy.md."
  exit 1
fi

# 3. Test evidence: at least one non-blank line that is not a code fence and not an unfilled template line ("... ->").
if ! section 'Test evidence' | grep -Ev '^\s*$|^\s*```|->\s*$' | grep -q .; then
  fail "The '## Test evidence' section is missing or empty."
fi

# 4. Security checklist: present, and no unticked box.
checklist=$(section 'Security checklist')
if ! printf '%s\n' "$checklist" | grep -Eq '^\s*- \[[xX]\]'; then
  fail "The '## Security checklist' section is missing or has no items."
fi
if printf '%s\n' "$checklist" | grep -Eq '^\s*- \[ \]'; then
  fail "Every item in '## Security checklist' must be ticked; items are worded so that each is either satisfied or does not apply."
fi

if [ "$failures" -gt 0 ]; then
  echo "pr-policy: $failures failure(s). See .github/PULL_REQUEST_TEMPLATE.md and docs/architecture/branching-strategy.md."
  exit 1
fi
echo "pr-policy: ok"
