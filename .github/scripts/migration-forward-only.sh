#!/usr/bin/env bash
# Migrations are forward-only (TASK-024). Record: docs/architecture/database-migrations.md
#
# A migration on the base branch may already be applied to a database, which records its ID in
# common."__EFMigrationsHistory" and never runs it again. Editing or deleting it changes the code
# without changing any database, so environments silently diverge. A correction is a new migration.
# Fails the pull request when it:
#
#   1. modifies, deletes or renames a migration that is on the base branch; or
#   2. adds a migration whose ID sorts before the newest one on the base branch. EF Core applies
#      migrations in ID order, so an environment already past that point would run it out of
#      order, after migrations that were written without it. Regenerate it with a fresh timestamp.
#
# The model snapshot is exempt: every `dotnet ef migrations add` rewrites it.
#
#   BASE_REF=origin/dev .github/scripts/migration-forward-only.sh
set -euo pipefail

: "${BASE_REF:?BASE_REF is unset — the branch the pull request merges into, e.g. origin/dev}"
MIGRATIONS_DIR=${MIGRATIONS_DIR:-src/backend/PMPlatform.Infrastructure/Persistence/Migrations}

base=$(git merge-base "$BASE_REF" HEAD)
status=0

changed=$(git diff --name-status --no-renames "$base" HEAD -- "$MIGRATIONS_DIR" |
  awk '$1 != "A" && $2 ~ /\.cs$/ && $2 !~ /ModelSnapshot\.cs$/ { print $1 "\t" $2 }')
if [ -n "$changed" ]; then
  printf 'migration forward-only: these migrations are on %s and were changed (M) or deleted (D):\n\n%s\n\n' "$BASE_REF" "$changed" >&2
  echo "Revert them and correct the schema in a new migration: dotnet ef migrations add TASK-nnn_<Description>." >&2
  status=1
fi

migration_ids() {
  sed -nE 's#.*/([0-9]{14}_[^/]+)\.cs$#\1#p' | { grep -v '\.Designer$' || true; } | sort
}

newest_on_base=$(git ls-tree -r --name-only "$base" -- "$MIGRATIONS_DIR" | migration_ids | tail -n 1)
added=$(git diff --name-only --diff-filter=A "$base" HEAD -- "$MIGRATIONS_DIR" | migration_ids)

for id in $added; do
  if [ -n "$newest_on_base" ] && [[ "$id" < "$newest_on_base" || "$id" == "$newest_on_base" ]]; then
    printf 'migration forward-only: %s sorts before %s, the newest migration on %s.\n' "$id" "$newest_on_base" "$BASE_REF" >&2
    echo "Remove it (dotnet ef migrations remove) and add it again, which gives it a current timestamp." >&2
    status=1
  fi
done

if [ "$status" -eq 0 ]; then
  echo "migration forward-only: no migration on $BASE_REF was changed; $(printf '%s' "$added" | grep -c . || true) added after ${newest_on_base:-no earlier migration}"
fi
exit "$status"
