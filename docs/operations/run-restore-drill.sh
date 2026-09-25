#!/usr/bin/env sh
# Executes the TASK-023 validation cell, locally, following the runbook's drill (§8) step for step:
#
#   "Execute a full restore-from-backup drill into an isolated environment; compare row counts and
#    checksums of key tables against the source; record actual time-to-restore against the RTO target."
#
#   docs/operations/run-restore-drill.sh <evidence-directory>
#
# Two environments, each a Docker network with its own PostgreSQL 17 and its own PMPlatform.Api built
# from this tree:
#
#   source    the TASK-014 schema and seed plus drill-fixture.sql, with the API serving from it
#   isolated  an --internal network: no route to the source, no route out. Nothing on the host can
#             reach it either; every check runs from an operations host inside it, as the runbook's
#             drill host runs inside the VPC (BR-1)
#
# The steps, by runbook number:
#   DS-2  fingerprint the source                         -> before.fingerprint
#         back it up: pg_basebackup (the local counterpart of an automated backup) and a SQL export
#   DS-3  fingerprint again; must equal DS-2             -> source.fingerprint
#         then write to the source, so a restore that read the live source rather than the backup fails
#   DS-4  START THE CLOCK. Create the isolated environment and restore the backup into it
#   DS-5  compare the restored database with source.fingerprint; audit chain
#   DS-8  bring the API up on the restored database; /health 200 and one active user per role.
#         STOP THE CLOCK
#   DS-6  import the export into a second database and compare (after the clock: it is a second
#         restore path, not part of restoring the service)
#   DS-10 tear both environments down
#
# Not executed locally, and why: DS-7 documents (no object store; the comparison is evidence log E-2)
# and DS-9 RPO (Cloud SQL's latest recovery time has no local counterpart).
#
# Requires: docker. Writes the fingerprints, the table report and a timing summary to the evidence
# directory. Leaves nothing running.
set -eu

[ $# -eq 1 ] || { echo "usage: $0 <evidence-directory>" >&2; exit 2; }
here=$(cd "$(dirname "$0")" && pwd)
repository=$(cd "$here/../.." && pwd)
mkdir -p "$1"
evidence=$(cd "$1" && pwd)
run_id=$$
image="pmplatform-api:task-023-drill"
password=pmplatform # the TASK-014 local default; never a real credential

source_network="drill-source-$run_id"
isolated_network="drill-isolated-$run_id"
source_db="drill-source-db-$run_id"
source_api="drill-source-api-$run_id"
restored_db="drill-restored-db-$run_id"
restored_api="drill-restored-api-$run_id"
work=$(mktemp -d)

cleanup() {
  docker rm -f "$source_db" "$source_api" "$restored_db" "$restored_api" >/dev/null 2>&1 || true
  docker network rm "$source_network" "$isolated_network" >/dev/null 2>&1 || true
  rm -rf "$work"
}
trap cleanup EXIT

now() { date -u +%Y-%m-%dT%H:%M:%SZ; }
log() { echo "$(now) $*" | tee -a "$evidence/drill.log"; }
: >"$evidence/drill.log"

# ops <network> <command...> — run a command on an operations host inside <network>, with this
# directory's tools and the evidence directory mounted. The runbook's drill host.
ops() {
  network=$1
  shift
  docker run --rm --network "$network" -e PGPASSWORD="$password" \
    -v "$here:/ops:ro" -v "$evidence:/evidence" -v "$repository/infra/docker/postgres:/seed:ro" \
    postgres:17 "$@"
}

connection() { echo "host=$1 port=5432 user=pmplatform dbname=${2:-pmplatform} sslmode=disable connect_timeout=5"; }

wait_ready() {
  i=0
  until docker exec "$1" psql -h 127.0.0.1 -U pmplatform -d pmplatform -Atc 'SELECT 1' >/dev/null 2>&1; do
    i=$((i + 1))
    [ $i -lt 90 ] || { log "FAIL $1 did not become ready"; exit 1; }
    sleep 1
  done
}

# api_healthy <container> — /health from inside the API's own container: the isolated network
# publishes nothing, so there is no other way in.
api_healthy() {
  i=0
  until [ "$(docker exec "$1" curl --silent --output /dev/null --write-out '%{http_code}' http://localhost:8080/health 2>/dev/null)" = 200 ]; do
    i=$((i + 1))
    [ $i -lt 90 ] || { log "FAIL $1 /health did not return 200"; docker logs --tail 30 "$1" >&2; exit 1; }
    sleep 1
  done
}

start_api() {
  docker run -d --name "$1" --network "$2" \
    -e ASPNETCORE_ENVIRONMENT=Development \
    -e DB_CONNECTION_STRING="Host=$3;Port=5432;Database=pmplatform;Username=pmplatform;Password=$password" \
    -e JWT_SIGNING_KEY=local-development-only-signing-key-not-valid-outside-docker-compose \
    -e CORS_ALLOWED_ORIGINS=http://localhost:5173 -e APP_BASE_URL=http://localhost:5173 \
    -e LOG_LEVEL=Warning -e APM_ENVIRONMENT_TAG=drill \
    "$image" >/dev/null
}

log "== release artifact: building $image from this tree (before the incident, as a deployed digest would be)"
docker build --quiet -f "$repository/infra/docker/api.Dockerfile" -t "$image" "$repository" >"$work/image-id"
log "   $(cat "$work/image-id")"

log "== source environment"
docker network create "$source_network" >/dev/null
docker run -d --name "$source_db" --network "$source_network" \
  -e POSTGRES_DB=pmplatform -e POSTGRES_USER=pmplatform -e POSTGRES_PASSWORD="$password" \
  -v "$repository/infra/docker/postgres/init:/docker-entrypoint-initdb.d:ro" postgres:17 >/dev/null
wait_ready "$source_db"
docker exec -i "$source_db" psql -h 127.0.0.1 -U pmplatform -d pmplatform -X -q -v ON_ERROR_STOP=1 \
  <"$here/drill-fixture.sql" >/dev/null
start_api "$source_api" "$source_network" "$source_db"
api_healthy "$source_api"
log "   source database and API up; /health 200"

log "== DS-2 fingerprint the source, then back it up"
ops "$source_network" /ops/verify-restore.sh fingerprint "$(connection "$source_db")" >"$evidence/before.fingerprint"
docker exec "$source_db" pg_basebackup -h 127.0.0.1 -U pmplatform -D /tmp/base -Ft -z -X fetch -c fast
docker cp -q "$source_db:/tmp/base/base.tar.gz" "$work/base.tar.gz"
docker exec "$source_db" sh -c 'pg_dump -h 127.0.0.1 -U pmplatform -d pmplatform --no-owner --no-privileges | gzip' \
  >"$work/pmplatform.sql.gz"
backup_taken=$(now)
log "   backups taken at $backup_taken: physical $(du -h "$work/base.tar.gz" | cut -f1), export $(du -h "$work/pmplatform.sql.gz" | cut -f1)"

log "== DS-3 fingerprint again; the source must not have changed across the backup"
ops "$source_network" /ops/verify-restore.sh fingerprint "$(connection "$source_db")" >"$evidence/source.fingerprint"
if cmp -s "$evidence/before.fingerprint" "$evidence/source.fingerprint"; then
  log "   identical: source.fingerprint is the source as backed up"
else
  log "FAIL the source changed across the backup; the drill is not valid (runbook DS-3)"
  exit 1
fi
docker exec "$source_db" psql -h 127.0.0.1 -U pmplatform -d pmplatform -X -q -v ON_ERROR_STOP=1 \
  -c "INSERT INTO drill_fixture.load (payload, recorded_at) VALUES ('written after the backup', now())" >/dev/null
log "   one row written to the source after the backup"

log "== DS-4 incident declared: CLOCK STARTED"
clock_start=$(date +%s)
t0=$(now)
docker network create --internal "$isolated_network" >/dev/null
docker run -d --name "$restored_db" --network "$isolated_network" --entrypoint sleep postgres:17 infinity >/dev/null
docker cp -q "$work/base.tar.gz" "$restored_db:/tmp/base.tar.gz"
docker exec "$restored_db" sh -c '
  set -e
  install -d -o postgres -g postgres -m 700 /var/lib/postgresql/restored
  tar -xzf /tmp/base.tar.gz -C /var/lib/postgresql/restored
  chown -R postgres:postgres /var/lib/postgresql/restored
  chmod 700 /var/lib/postgresql/restored
  su postgres -c "pg_ctl -D /var/lib/postgresql/restored -o \"-c listen_addresses=*\" -l /tmp/postgres.log -w start" >/dev/null'
wait_ready "$restored_db"
t_restored=$(now)
log "   backup restored onto a new server in the isolated environment"

log "== isolation: the operations host in the isolated environment must reach nothing outside it"
source_ip=$(docker inspect -f "{{(index .NetworkSettings.Networks \"$source_network\").IPAddress}}" "$source_db")
# The control: the same probe, from the source's own network, must succeed — otherwise a probe that
# can never connect would report isolation that was never tested.
ops "$source_network" psql "$(connection "$source_ip")" -Atc 'SELECT 1' >/dev/null 2>&1 \
  || { log "FAIL the isolation probe cannot reach the source even from its own network; isolation not tested"; exit 1; }
if ops "$isolated_network" psql "$(connection "$source_ip")" -Atc 'SELECT 1' >/dev/null 2>&1; then
  log "FAIL the isolated environment can reach the source database at $source_ip"
  exit 1
fi
if ops "$isolated_network" bash -c 'exec 3<>/dev/tcp/1.1.1.1/443' >/dev/null 2>&1; then
  log "FAIL the isolated environment has a route out"
  exit 1
fi
log "   source database at $source_ip: reachable from the source network (control), unreachable from the isolated one. Internet: unreachable"

log "== DS-5 compare with the source as backed up"
ops "$isolated_network" /ops/verify-restore.sh fingerprint "$(connection "$restored_db")" >"$evidence/restored.fingerprint"
if ops "$isolated_network" /ops/verify-restore.sh compare /evidence/source.fingerprint "$(connection "$restored_db")" \
  >"$work/compare.out" 2>&1; then
  database_result=MATCH
else
  database_result=FAIL
fi
tee -a "$evidence/drill.log" <"$work/compare.out"
t_verified=$(now)

log "== DS-8 the service, on the restored database"
start_api "$restored_api" "$isolated_network" "$restored_db"
api_healthy "$restored_api"
ops "$isolated_network" psql "$(connection "$restored_db")" -X -q -v ON_ERROR_STOP=1 -f /seed/verify-seed.sql \
  >"$evidence/verify-seed.out"
clock_stop=$(date +%s)
t_service=$(now)
elapsed=$((clock_stop - clock_start))
log "   /health 200 from the restored API; one active local user for each of R01-R08"
log "== CLOCK STOPPED: time-to-restore ${elapsed}s"

log "== DS-6 the export, into a second database on the restored server"
docker exec "$restored_db" psql -h 127.0.0.1 -U pmplatform -d pmplatform -X -q -c 'CREATE DATABASE pmplatform_export' >/dev/null
gunzip -c "$work/pmplatform.sql.gz" \
  | docker exec -i "$restored_db" psql -h 127.0.0.1 -U pmplatform -d pmplatform_export -X -q -v ON_ERROR_STOP=1 >/dev/null
if ops "$isolated_network" /ops/verify-restore.sh compare /evidence/source.fingerprint "$(connection "$restored_db" pmplatform_export)" \
  >"$work/export.out" 2>&1; then
  export_result=MATCH
else
  export_result=FAIL
fi
tee -a "$evidence/drill.log" <"$work/export.out"

"$here/verify-restore.sh" report "$evidence/source.fingerprint" "$evidence/restored.fingerprint" >"$evidence/table-report.md"

if [ "$elapsed" -le 14400 ]; then rto_result="PASS (<= 4 h)"
elif [ "$elapsed" -le 21600 ]; then rto_result="PASS WITH FINDING (> 4 h, <= 6 h)"
else rto_result="FAIL (> 6 h)"; fi

cat >"$evidence/summary.md" <<EOF
| Field | Value |
| --- | --- |
| Release artifact | \`$image\` \`$(cat "$work/image-id")\` |
| Backup taken | $backup_taken |
| DS-4 clock start | $t0 |
| Backup restored, server accepting connections | $t_restored |
| DS-5 comparison finished | $t_verified |
| DS-8 service verified, clock stop | $t_service |
| **Time-to-restore** | **${elapsed} s** |
| P-5 against RTO 4–6 h | $rto_result |
| P-1 database, from the backup | $database_result |
| P-2 database, from the export | $export_result |
| P-3 audit chain | $(grep '^audit-chain|' "$evidence/restored.fingerprint" | cut -d'|' -f2- | sed 's/|/, /g') |
EOF
log "== summary"
tee -a "$evidence/drill.log" <"$evidence/summary.md"

[ "$database_result" = MATCH ] && [ "$export_result" = MATCH ] && [ "$elapsed" -le 21600 ]
