#!/usr/bin/env bash
# Seed and data-integrity dry-run (TASK-027). Record: docs/architecture/seed-data-and-integrity.md
#
# Runs on the scratch database that migration-dry-run.sh has just migrated, and does to it what every deployment
# does after its migrations (.github/scripts/migrate-environment.sh): the release image's `seed`, then its
# `validate-data-integrity`. Between them it seeds a second time and compares every table's row count and content
# checksum before and after (docs/operations/database-fingerprint.sql), which is the workbook's validation cell for
# this task: a re-run must leave the counts unchanged. A failure in any of the three blocks promotion, because
# migration-dry-run is a need of deploy-dev.
#
#   DATABASE_URL=postgres://…/scratch DB_CONNECTION_STRING='Host=…;Database=scratch;…' .github/scripts/seed-dry-run.sh
set -euo pipefail

API_PROJECT=${API_PROJECT:-src/backend/PMPlatform.Api}
OUTPUT_DIR=${OUTPUT_DIR:-artifacts}

die() { printf 'seed dry-run: %s\n' "$*" >&2; exit 1; }

summary() {
  [ -n "${GITHUB_STEP_SUMMARY:-}" ] || return 0
  printf '%s\n' "$*" >>"$GITHUB_STEP_SUMMARY"
}

[ -n "${DATABASE_URL:-}" ] || die "DATABASE_URL is unset — the scratch database, for psql"
[ -n "${DB_CONNECTION_STRING:-}" ] || die "DB_CONNECTION_STRING is unset — the same scratch database, for the API"
command -v psql >/dev/null 2>&1 || die "psql is required and is not on PATH"

# The built API, run as the environment's job runs the image. Development only lifts the secret-store requirement
# (TASK-019): the scratch database's connection string is not a secret and there is no store to read it from.
api() {
  ASPNETCORE_ENVIRONMENT=Development dotnet run --project "$API_PROJECT" --configuration Release --no-build --no-launch-profile -- "$@"
}

fingerprint() {
  psql "$DATABASE_URL" -X -q -At -v ON_ERROR_STOP=1 -f docs/operations/database-fingerprint.sql >"$1"
}

mkdir -p "$OUTPUT_DIR"

api seed || die "the seed failed on a freshly migrated database"
fingerprint "$OUTPUT_DIR/seed-first-run.txt"

api seed || die "the seed failed when run a second time, so it is not idempotent"
fingerprint "$OUTPUT_DIR/seed-second-run.txt"

if ! diff -u "$OUTPUT_DIR/seed-first-run.txt" "$OUTPUT_DIR/seed-second-run.txt" >&2; then
  die "a second run of the seed changed the database (diff above: table|name|rows|checksum), so it is not idempotent"
fi

api validate-data-integrity || die "the data-integrity check found violations (listed above)"

# Tables with rows, outside `common` (which holds only the migration history).
seeded=$(grep '^table|' "$OUTPUT_DIR/seed-first-run.txt" | grep -v '^table|common\.' | grep -c '^table|[^|]*|[1-9]' || true)
printf 'seed dry-run passed: %s table(s) seeded, unchanged by a second run, no integrity violation\n' "$seeded"
summary "### Seed and data-integrity dry-run: passed"
summary ""
summary "The seed ran twice on the migrated scratch database; the second run left every table's row count and checksum unchanged ($seeded seeded tables). \`validate-data-integrity\` found no violation."
