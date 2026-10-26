#!/usr/bin/env bash
# Migration execution against one environment (TASK-018). Record: docs/architecture/cicd-pipeline.md
#
# The dry-run (.github/scripts/migration-dry-run.sh) proved the release's migrations apply cleanly
# and are reversible-safe. This applies them to a real environment, immediately before the image
# that depends on them is deployed.
#
# It cannot do so from the runner. Every environment's database has no public IP (infra/terraform/
# database), so the only path to it is from inside that environment's VPC, and ADR-001 C-8 has not
# settled whether a GitHub-hosted runner outside the Kingdom may hold a path to platform data at all
# (CTL-49). So the migration runs *in* the environment, as a Cloud Run job on the release image, and
# this script starts it and waits. The job is defined by TASK-024 in infra/terraform/compute
# (`<resource_prefix>-migrate`): the release image run as `dotnet PMPlatform.Api.dll migrate`, with the
# service's identity, network path and secret store. Terraform creates it on the environment's first
# apply; this script only points it at the release image and runs it, so it cannot create a job that
# lacks that identity or network path.
#
# The post-migration data step (TASK-027) runs as two more executions of the same job, with the
# container's arguments overridden: `seed` loads db/seed/seed-master-data.sql (idempotent; it adds only
# what is missing and never rewrites AHDA's wording), then `validate-data-integrity` runs
# db/seed/validate-data-integrity.sql, which exits non-zero on any orphaned foreign key or constraint
# violation. Either failure fails this step, so the image is never deployed. Overriding arguments needs
# run.jobs.runWithOverrides on the deploy account (docs/architecture/seed-data-and-integrity.md F-4).
#
#   ENVIRONMENT=dev PROJECT_ID=… REGION=… IMAGE=…@sha256:… .github/scripts/migrate-environment.sh
set -euo pipefail

: "${ENVIRONMENT:?ENVIRONMENT is unset}"
MIGRATIONS_DIR=${MIGRATIONS_DIR:-src/backend/PMPlatform.Infrastructure/Persistence/Migrations}
MIGRATION_JOB=${MIGRATION_JOB:-}

migration_count=$(find "$MIGRATIONS_DIR" -maxdepth 1 -name '*.cs' 2>/dev/null | wc -l | tr -d ' ')

if [ "$migration_count" = "0" ]; then
  echo "migrate ($ENVIRONMENT): no migrations in this release; nothing to apply."
  exit 0
fi

if [ -z "$MIGRATION_JOB" ]; then
  cat >&2 <<MSG
error: this release carries $migration_count migration(s) and MIGRATION_JOB is unset, so they cannot
be applied to '$ENVIRONMENT'.

The database has no public IP, so a migration runs inside the environment's VPC rather than from the
runner (ADR-001 C-8 / CTL-49). MIGRATION_JOB names the Cloud Run job that applies them. Terraform
creates it in every environment as <resource_prefix>-migrate (infra/terraform/compute, TASK-024); set
the repository variable MIGRATION_JOB to that name. See docs/architecture/database-migrations.md.

The deployment stops here rather than shipping an image against a schema that was never migrated.
MSG
  exit 1
fi

: "${PROJECT_ID:?PROJECT_ID is unset}"
: "${REGION:?REGION is unset}"
: "${IMAGE:?IMAGE is unset}"

echo "migrate ($ENVIRONMENT): running $MIGRATION_JOB on $IMAGE"

# --wait makes the deployment wait on the migration's exit status; a failed migration fails this
# step, and the deploy step that follows never runs.
# `update`, not `deploy`: deploy would create a missing job with default identity and no VPC path.
gcloud run jobs update "$MIGRATION_JOB" \
  --project "$PROJECT_ID" \
  --region "$REGION" \
  --image "$IMAGE" \
  --quiet
gcloud run jobs execute "$MIGRATION_JOB" \
  --project "$PROJECT_ID" \
  --region "$REGION" \
  --wait \
  --quiet

echo "migrate ($ENVIRONMENT): applied"

for command in seed validate-data-integrity; do
  echo "migrate ($ENVIRONMENT): running $command on $MIGRATION_JOB"
  gcloud run jobs execute "$MIGRATION_JOB" \
    --project "$PROJECT_ID" \
    --region "$REGION" \
    --args "$command" \
    --wait \
    --quiet
done

echo "migrate ($ENVIRONMENT): seeded; data integrity validated"
