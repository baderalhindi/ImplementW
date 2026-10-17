#!/usr/bin/env bash
# Re-applies the e2e data set (provision/*.sql, in file order, one transaction) to the e2e stack already running, after a
# provisioning file changed. Every file is idempotent; the configuration files skip themselves once applied, so a changed
# configuration needs a new file (or `run.sh` without E2E_REUSE_STACK, which starts from a fresh database).
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
args=()
for file in provision/*.sql; do args+=("--file=/provision/$(basename "$file")"); done
docker compose -f compose.yml run --rm --no-deps --entrypoint psql e2e-seed \
  --no-psqlrc --quiet --set=ON_ERROR_STOP=1 --single-transaction "${args[@]}"
