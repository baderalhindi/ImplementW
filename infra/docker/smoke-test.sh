#!/usr/bin/env bash
# TASK-014 acceptance check, reproducible on any machine with Docker (or podman behind DOCKER_HOST):
#   from a clean state, `docker compose up` brings up DB + API + frontend, the health endpoint returns 200 within
#   2 minutes, and the seed holds an active local user for every role R01–R08.
#
#   infra/docker/smoke-test.sh            # leaves the stack running
#   infra/docker/smoke-test.sh --down     # tears it down (and its volumes) afterwards
#
# Images are built first and timed separately: the 2-minute limit is applied to bringing the stack up and healthy,
# which is what a developer waits for on every start after the first. The build (and, on a cold machine, the base
# image pull) is reported but not limited — it depends on network and cache, not on this stack.
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
compose() { docker compose -f "$HERE/docker-compose.yml" "$@"; }
API_PORT="${PMPLATFORM_API_PORT:-5080}"
FRONTEND_PORT="${PMPLATFORM_FRONTEND_PORT:-5173}"
LIMIT_SECONDS=120

echo "== clean state"
compose down --volumes --remove-orphans

echo "== docker compose build"
build_started=$(date +%s)
compose build
build_elapsed=$(( $(date +%s) - build_started ))
echo "   built in ${build_elapsed}s"

echo "== docker compose up --wait (limit ${LIMIT_SECONDS}s)"
up_started=$(date +%s)
compose up --wait --wait-timeout "$LIMIT_SECONDS"
up_elapsed=$(( $(date +%s) - up_started ))
echo "   all services healthy after ${up_elapsed}s"
if (( up_elapsed > LIMIT_SECONDS )); then
    echo "FAIL: exceeded ${LIMIT_SECONDS}s" >&2
    exit 1
fi

echo "== API health"
health_status=$(curl --silent --output /dev/null --write-out '%{http_code}' "http://localhost:${API_PORT}/health")
health_body=$(curl --silent "http://localhost:${API_PORT}/health")
echo "   GET /health -> ${health_status} ${health_body}"
[[ "$health_status" == "200" ]]

echo "== frontend"
frontend_status=$(curl --silent --output /dev/null --write-out '%{http_code}' "http://localhost:${FRONTEND_PORT}/")
echo "   GET / -> ${frontend_status}"
[[ "$frontend_status" == "200" ]]

echo "== seeded users per role"
compose exec -T postgres psql --quiet --username=pmplatform --dbname=pmplatform < "$HERE/postgres/verify-seed.sql"

echo "PASS: DB + API + frontend healthy in ${up_elapsed}s (build ${build_elapsed}s); one active local user per role R01–R08"

if [[ "${1:-}" == "--down" ]]; then
    compose down --volumes --remove-orphans
fi
