#!/usr/bin/env bash
# Migration dry-run (TASK-018). Record: docs/architecture/cicd-pipeline.md
#
# CTL-40 makes a failed migration dry-run block promotion, so the dry-run has to be something that
# can fail for the right reasons. It does three things to the release's migrations, none of which
# touches a deployed database:
#
#   1. generates the idempotent SQL script the deployment would apply, which fails if the migrations
#      do not compile or the model and the migration history disagree;
#   2. applies it to an empty scratch database and then applies it a second time, which is what
#      "idempotent" has to mean for a script that may be re-run after a partial failure;
#   3. refuses any statement that destroys a column or a table, because CTL-37 requires a schema
#      change to be backward-compatible with the running release (expand-then-contract) — a deploy
#      that drops a column the previous release still reads cannot be rolled back.
#
# The migration framework is TASK-024 (docs/architecture/database-migrations.md). A release with no
# migration reports that rather than inventing a pass. Rollback is not tested here: every migration's
# Down is executed against PostgreSQL by the backend quality gate before it can merge.
#
#   DATABASE_URL=postgres://user:pass@localhost:5432/scratch .github/scripts/migration-dry-run.sh
set -euo pipefail

PROJECT=${PROJECT:-src/backend/PMPlatform.Infrastructure}
# Infrastructure is its own startup project: its design-time factory builds the context without the
# API host or the secret store, and EF Core's design package never enters the API image (TASK-024).
STARTUP_PROJECT=${STARTUP_PROJECT:-$PROJECT}
MIGRATIONS_DIR=${MIGRATIONS_DIR:-$PROJECT/Persistence/Migrations}
OUTPUT=${OUTPUT:-artifacts/migrations.sql}

note() { printf '%s\n' "$*"; }
die() { printf 'migration dry-run: %s\n' "$*" >&2; exit 1; }

summary() {
  [ -n "${GITHUB_STEP_SUMMARY:-}" ] || return 0
  printf '%s\n' "$*" >>"$GITHUB_STEP_SUMMARY"
}

# TASK-011 scaffolded the directory with a .gitkeep, so its existence proves nothing. A migration is
# a compiled class, so the release has migrations when the directory holds at least one .cs file.
migration_count=$(find "$MIGRATIONS_DIR" -maxdepth 1 -name '*.cs' 2>/dev/null | wc -l | tr -d ' ')

if [ "$migration_count" = "0" ]; then
  note "no migrations in this release: $MIGRATIONS_DIR holds no migration."
  note "The migration framework is TASK-024 (Establish Database Migration Framework & Conventions)."
  note "Nothing is applied and nothing is asserted; this gate becomes load-bearing with the first migration."
  summary "### Migration dry-run: no migrations in this release"
  summary ""
  summary "\`$MIGRATIONS_DIR\` holds no migration. The migration framework is TASK-024; this gate runs in full from the first migration onwards."
  exit 0
fi

command -v dotnet >/dev/null 2>&1 || die "dotnet is required and is not on PATH"
[ -n "${DATABASE_URL:-}" ] || die "DATABASE_URL is unset — the scratch database the script is applied to"
command -v psql >/dev/null 2>&1 || die "psql is required and is not on PATH"

mkdir -p "$(dirname "$OUTPUT")"

# --idempotent guards every migration with a check against the history table, which is what makes a
# re-run after a partial failure safe. dotnet-ef is the version pinned in .config/dotnet-tools.json,
# restored by the caller (`dotnet tool restore`).
note "generating $OUTPUT from $MIGRATIONS_DIR"
dotnet ef migrations script \
  --idempotent \
  --project "$PROJECT" \
  --startup-project "$STARTUP_PROJECT" \
  --configuration Release \
  --no-build \
  --output "$OUTPUT" ||
  die "could not generate the migration script; the migrations do not build or the model and the migration history disagree"

[ -s "$OUTPUT" ] || die "$OUTPUT is empty"

# CTL-37, expand-then-contract. A contraction is legitimate once the release that stopped reading
# the column is live, which is a later release and therefore a later migration; the marker records
# that the author checked, and shows up in review as the thing to check.
note "checking for destructive statements"
destructive=$(grep -nEi '^[[:space:]]*(DROP[[:space:]]+TABLE|TRUNCATE|ALTER[[:space:]]+TABLE[[:space:]]+[^;]*DROP[[:space:]]+(COLUMN|CONSTRAINT))' "$OUTPUT" |
  grep -vi 'EXPAND-THEN-CONTRACT-REVIEWED' || true)

if [ -n "$destructive" ]; then
  printf 'migration dry-run: the release drops schema the running release may still read (CTL-37):\n\n%s\n\n' "$destructive" >&2
  cat >&2 <<'MSG'
A migration that removes a column or a table is not backward-compatible with the release it is
deployed alongside, so the deployment cannot be rolled back by redeploying the previous image.
Split it: one release stops using the column, a later release drops it. If this drop is the second
half of an expand-then-contract already completed, say so in the migration with a comment
containing EXPAND-THEN-CONTRACT-REVIEWED and name the release that stopped reading it.
MSG
  exit 1
fi

note "applying to the scratch database (first pass, from empty)"
psql "$DATABASE_URL" --quiet --set ON_ERROR_STOP=on --file "$OUTPUT" ||
  die "the script failed against an empty database"

note "applying to the scratch database (second pass, idempotency)"
psql "$DATABASE_URL" --quiet --set ON_ERROR_STOP=on --file "$OUTPUT" ||
  die "the script is not idempotent: a second application failed, so a re-run after a partial failure would not be safe"

statements=$(grep -cE ';[[:space:]]*$' "$OUTPUT" || true)
note "migration dry-run passed: $statements statement(s), applied twice, no destructive statement"
summary "### Migration dry-run: passed"
summary ""
summary "\`$OUTPUT\` applied cleanly to an empty database and again to the result ($statements statements). No statement drops a table, column or constraint."
