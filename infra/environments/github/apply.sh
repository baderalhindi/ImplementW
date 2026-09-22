#!/usr/bin/env sh
# Applies environments.json to the repository as GitHub deployment environments (TASK-016), which
# is where the promotion path's approval gates are enforced once TASK-018's pipeline deploys
# through them. Idempotent: PUT creates or updates.
# Requires: gh (authenticated as a repository admin), jq.
# Usage: infra/environments/github/apply.sh [owner/repo]   (defaults to the current repository)
set -eu

here=$(cd "$(dirname "$0")" && pwd)
config="$here/environments.json"
repo=${1:-$(gh repo view --json nameWithOwner --jq .nameWithOwner)}
org=${repo%%/*}

for name in $(jq -r '.environments[].name' "$config"); do
  body=$(mktemp)
  reviewers="[]"
  for slug in $(jq -r --arg n "$name" '.environments[] | select(.name == $n) | .reviewer_teams[]' "$config"); do
    id=$(gh api "orgs/$org/teams/$slug" --jq .id 2>/dev/null) ||
      { echo "error: team $org/$slug does not exist; create it or amend environments.json" >&2; exit 1; }
    reviewers=$(echo "$reviewers" | jq --argjson id "$id" '. + [{"type": "Team", "id": $id}]')
  done

  jq --arg n "$name" --argjson reviewers "$reviewers" '
    .environments[] | select(.name == $n) | {
      wait_timer: .wait_timer,
      prevent_self_review: .prevent_self_review,
      reviewers: $reviewers,
      deployment_branch_policy: (if .protected_branches_only
        then {protected_branches: true, custom_branch_policies: false}
        else null end)
    }' "$config" > "$body"

  gh api --method PUT "repos/$repo/environments/$name" --input "$body" \
    --jq '"applied environment \(.name) [\(.protection_rules | length) protection rule(s)]"'
  rm -f "$body"
done
