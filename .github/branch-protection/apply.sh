#!/usr/bin/env sh
# Applies protected-branches.ruleset.json to the repository as a GitHub ruleset (TASK-012).
# Idempotent: updates the ruleset if one with the same name exists, otherwise creates it.
# Requires: gh (authenticated as a repository admin), jq.
# Usage: .github/branch-protection/apply.sh [owner/repo]   (defaults to the current repository)
set -eu

here=$(cd "$(dirname "$0")" && pwd)
ruleset="$here/protected-branches.ruleset.json"
repo=${1:-$(gh repo view --json nameWithOwner --jq .nameWithOwner)}
name=$(jq -r .name "$ruleset")

existing=$(gh api "repos/$repo/rulesets" --jq ".[] | select(.name == \"$name\") | .id")

if [ -n "$existing" ]; then
  gh api --method PUT "repos/$repo/rulesets/$existing" --input "$ruleset" --jq '"updated ruleset \(.id) \(.name) [\(.enforcement)]"'
else
  gh api --method POST "repos/$repo/rulesets" --input "$ruleset" --jq '"created ruleset \(.id) \(.name) [\(.enforcement)]"'
fi

# Show the effective rules on main so the result can be pasted into the record.
gh api "repos/$repo/rules/branches/main" --jq '.[] | .type'
