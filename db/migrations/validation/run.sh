#!/usr/bin/env bash
# Migration up/down validation at representative volume (TASK-089). Record: db/migrations/rollback-validation.md.
#
#   db/migrations/validation/run.sh                 # full cycle: every migration down and up again (~40 min locally)
#   db/migrations/validation/run.sh --depth 2       # the two newest migrations only (what a pull request runs)
#
# Builds the release image from this tree (infra/docker/api.Dockerfile), starts PostgreSQL 17 and a psql client on a
# Docker network of its own, runs rollback_validation.py against that server and removes all three containers and the
# network on exit. DB_CONNECTION_STRING is set here to the server this script starts; any value in the caller's shell
# names a different server and is ignored. Results: artifacts/migration-validation/<UTC time>/results.json, or
# $MIGRATION_VALIDATION_OUTPUT.
#
# Requires: docker, python3 (3.11+). Every value below is a local default, valid only on this private network.
set -euo pipefail

here=$(cd "$(dirname "$0")" && pwd)
repository=$(cd "$here/../../.." && pwd)
output=${MIGRATION_VALIDATION_OUTPUT:-$repository/artifacts/migration-validation/$(date -u +%Y%m%dT%H%M%SZ)}
network=ahda-migration-validation
server=AHDA-migration-validation-postgres
client=AHDA-migration-validation-client
image=ahda-migration-validation:latest
postgres=mirror.gcr.io/library/postgres:17

cleanup() {
  docker rm -f "$server" "$client" >/dev/null 2>&1 || true
  docker network rm "$network" >/dev/null 2>&1 || true
}
trap cleanup EXIT
cleanup

echo "Building the release image from $repository"
docker build --quiet -f "$repository/infra/docker/api.Dockerfile" -t "$image" "$repository" >/dev/null

docker network create "$network" >/dev/null
# shm-size: the fingerprint's parallel aggregates need more than Docker's 64 MB default /dev/shm.
docker run -d --name "$server" --network "$network" --shm-size=1g \
  -e POSTGRES_USER=pmplatform -e POSTGRES_PASSWORD=pmplatform -e POSTGRES_DB=postgres "$postgres" >/dev/null
docker run -d --name "$client" --network "$network" -v "$repository:/repo:ro" --entrypoint sleep "$postgres" infinity >/dev/null

for _ in $(seq 1 60); do
  docker exec "$server" pg_isready -q -h localhost -U pmplatform -d postgres && break
  sleep 1
done
docker exec "$server" pg_isready -q -h localhost -U pmplatform -d postgres

export DB_CONNECTION_STRING="Host=$server;Port=5432;Database=postgres;Username=pmplatform;Password=pmplatform"
python3 "$here/rollback_validation.py" --network "$network" --client "$client" --image "$image" --output "$output" "$@"
