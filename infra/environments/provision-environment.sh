#!/usr/bin/env sh
# Provisions one environment's isolation boundary (TASK-016): its own GCP project, its own
# Terraform state bucket, its own secret-store namespace holding every secret the Environment and
# Secrets sheet scopes to it, and its own deploy and runtime service accounts. Nothing it creates
# is shared with any other environment, and it grants no principal outside the project it creates.
#
#   infra/environments/apply-org-policy.sh --dry-run            # run this FIRST, once, per folder
#   infra/environments/provision-environment.sh dev --dry-run   # then once per environment
#
# What it deliberately does NOT create, because another task owns it:
#   network, subnets, firewall rules  TASK-021    database instance            TASK-020
#   compute/runtime services          TASK-017    secret VALUES (versions)     TASK-019, the owner named in the sheet
# The manifest names those resources so that "its own instance", "its own network" is auditable
# before they exist; the task that creates them uses the names from the same file.
#
# Requires: gcloud (authenticated with folder-level project-creation rights), jq.
# Record: docs/architecture/environment-separation.md
set -eu

here=$(cd "$(dirname "$0")" && pwd)
. "$here/lib/common.sh"

environment=${1:-}
parse_dry_run "$@"
require_tools jq
[ "$DRY_RUN" = "1" ] || require_tools gcloud  # --dry-run prints the calls, it does not make them
require_known_environment "$environment"
require_platform_values
region=$(require_region)
require_authorisation

org=$(manifest_get '.platform.organization_id')
billing=$(manifest_get '.platform.billing_account_id')
tier=$(env_get "$environment" '.tier')
folder=$(env_get "$environment" '.folder')
project=$(env_get "$environment" '.project_id')
bucket=$(env_get "$environment" '.state_bucket')
deploy_sa=$(env_get "$environment" '.service_accounts.deploy')
runtime_sa=$(env_get "$environment" '.service_accounts.runtime')

if [ "$DRY_RUN" = "1" ]; then
  folder_id="<folder-id:$folder>"
else
  folder_id=$(gcloud resource-manager folders list --organization="$org" \
    --filter="displayName=$folder" --format='value(name)')
  [ -n "$folder_id" ] || die "folder '$folder' does not exist under organisation $org; run apply-org-policy.sh first"
fi

echo "== $environment — project $project, folder $folder, region $region =="

# 1. The project. The label set is what the cost, audit and policy views group by; the log
#    bucket's location was fixed by apply-org-policy.sh before this point (ADR-001 C-6).
run gcloud projects create "$project" \
  --folder="$folder_id" \
  --labels="environment=$environment,tier=$tier,platform=pmplatform,task=task-016"
run gcloud billing projects link "$project" --billing-account="$billing"

# 2. Services. Only what this task's own resources need; every other task enables its own.
run gcloud services enable \
  cloudresourcemanager.googleapis.com \
  iam.googleapis.com \
  serviceusage.googleapis.com \
  secretmanager.googleapis.com \
  storage.googleapis.com \
  logging.googleapis.com \
  monitoring.googleapis.com \
  --project="$project"

# 3. Terraform state (TASK-017 consumes it). Regional, versioned, private: the state file holds
#    resource names and outputs, so PROD state is readable only inside the PROD project.
run gcloud storage buckets create "gs://$bucket" \
  --project="$project" \
  --location="$region" \
  --uniform-bucket-level-access \
  --public-access-prevention
run gcloud storage buckets update "gs://$bucket" --versioning

# 4. Service accounts. Two per environment, both inside the environment's own project, so a
#    credential names the environment it belongs to and can hold no grant in another.
run gcloud iam service-accounts create "deploy-$environment" \
  --project="$project" \
  --display-name="PMPlatform $environment deploy (CI/CD, TASK-018)"
run gcloud iam service-accounts create "app-$environment" \
  --project="$project" \
  --display-name="PMPlatform $environment runtime"

run gcloud projects add-iam-policy-binding "$project" \
  --member="serviceAccount:$deploy_sa" --role="roles/storage.admin" --condition=None
run gcloud projects add-iam-policy-binding "$project" \
  --member="serviceAccount:$deploy_sa" --role="roles/secretmanager.admin" --condition=None

# 5. The secret-store namespace. The project is the namespace — Secret Manager has no namespace
#    of its own — and the prefix makes a misrouted read visibly wrong in a log line. Replication
#    is user-managed and pinned to the one region (ADR-001 C-5); the default policy would put
#    secret material outside the Kingdom.
#    Containers only. No version is created here, so this script never handles a secret value.
for secret_id in $(env_get "$environment" '.variables | to_entries[] | select(.value.kind == "secret") | .value.secret_id'); do
  run gcloud secrets create "$secret_id" \
    --project="$project" \
    --replication-policy=user-managed \
    --locations="$region" \
    --labels="environment=$environment,task=task-016"
  run gcloud secrets add-iam-policy-binding "$secret_id" \
    --project="$project" \
    --member="serviceAccount:$runtime_sa" \
    --role="roles/secretmanager.secretAccessor" --condition=None
done

cat <<NOTE

Done: $project. Still owed before the environment can serve traffic:
  - secret VERSIONS for $(env_get "$environment" '.variables | to_entries[] | select(.value.kind == "secret") | .value.secret_id' | tr '\n' ' ')
    added by the owner named in the Environment and Secrets sheet, never by this script (TASK-019)
  - APP_BASE_URL for $environment, once the DNS zone is named (environment-separation.md F-1)
  - network (TASK-021), database instance $(env_get "$environment" '.database.instance') (TASK-020), runtime (TASK-017)
Then: infra/environments/verify-separation.sh
NOTE
