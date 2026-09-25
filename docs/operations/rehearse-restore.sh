#!/usr/bin/env sh
# Rehearses the database restore and its verification end to end, on one machine (TASK-023).
#
#   docs/operations/rehearse-restore.sh
#
# What it does. It starts a SOURCE PostgreSQL 17, applies the EF Core migrations with the API image's
# `migrate` command and loads the local seed (infra/docker/postgres/seed), adds a fixture
# (a hash-chained audit_activity.audit_event, a 200,000-row table with a sequence and an index), and
# fingerprints it. It then takes the two kinds of backup the runbook restores from and restores each
# into its own isolated server, timing both:
#
#   physical  pg_basebackup, started as a new server — the local counterpart of restoring a Cloud SQL
#             automated backup to a new instance (runbook §5.2)
#   export    plain SQL, gzipped, imported into an empty database — the same format the scheduled
#             Cloud SQL export writes to the db-backups bucket (runbook §5.3)
#
# Each restore is compared with verify-restore.sh against the fingerprint taken at the backup. Then
# nine faults are introduced, one at a time, into copies of the restored database, and each must be
# reported by the exit status the runbook relies on — judged against an unchanged copy, which must
# match.
#
# What it is not. Neither backup is a Cloud SQL backup and neither restore ran in GCP: no
# environment exists (ADR-001 R-1 to R-3, UGV-07). It proves the procedure's verification half — the
# half an operator cannot eyeball — and the drill in a non-PROD environment is still owed
# (restore-drill-evidence-log.md).
#
# Requires: docker, psql 17. Takes about a minute once the API image is cached. Leaves nothing running.
set -eu

here=$(cd "$(dirname "$0")" && pwd)
repository=$(cd "$here/../.." && pwd)
verify="$here/verify-restore.sh"
run_id=$$
work=$(mktemp -d)
network="restore-rehearsal-$run_id"
source_container="restore-rehearsal-source-$run_id"
physical_container="restore-rehearsal-physical-$run_id"
export_container="restore-rehearsal-export-$run_id"
image="pmplatform-api:task-023-rehearsal"
export PGPASSWORD=pmplatform # the TASK-014 local default; never a real credential

cleanup() {
  docker rm -f "$source_container" "$physical_container" "$export_container" >/dev/null 2>&1 || true
  docker network rm "$network" >/dev/null 2>&1 || true
  rm -rf "$work"
}
trap cleanup EXIT

now() { date -u +%Y-%m-%dT%H:%M:%SZ; }
seconds() { date +%s; }

# wait_ready <container> — until the server accepts a query over TCP, not just the socket.
wait_ready() {
  i=0
  until docker exec "$1" psql -h 127.0.0.1 -U pmplatform -d pmplatform -Atc 'SELECT 1' >/dev/null 2>&1; do
    i=$((i + 1))
    [ $i -lt 60 ] || { echo "$1 did not become ready" >&2; exit 1; }
    sleep 1
  done
}

connection() {
  port=$(docker port "$1" 5432/tcp | head -1 | sed 's/.*://')
  echo "host=127.0.0.1 port=$port user=pmplatform dbname=${2:-pmplatform} sslmode=disable"
}

sql() {
  docker exec -i "$1" psql -h 127.0.0.1 -U pmplatform -d "${3:-pmplatform}" -X -q -v ON_ERROR_STOP=1 -c "$2" >/dev/null
}

echo "== $(now) source: PostgreSQL 17, migrated schema and local seed, plus the drill fixture"
docker build --quiet -f "$repository/infra/docker/api.Dockerfile" -t "$image" "$repository" >/dev/null
docker network create "$network" >/dev/null
docker run -d --name "$source_container" --network "$network" -p 127.0.0.1::5432 \
  -e POSTGRES_DB=pmplatform -e POSTGRES_USER=pmplatform -e POSTGRES_PASSWORD=pmplatform \
  postgres:17 >/dev/null
wait_ready "$source_container"
docker run --rm --network "$network" -e ASPNETCORE_ENVIRONMENT=Development \
  -e DB_CONNECTION_STRING="Host=$source_container;Port=5432;Database=pmplatform;Username=pmplatform;Password=pmplatform" \
  "$image" migrate >/dev/null
for seed in "$repository"/infra/docker/postgres/seed/*.sql; do
  docker exec -i "$source_container" psql -h 127.0.0.1 -U pmplatform -d pmplatform -X -q -v ON_ERROR_STOP=1 <"$seed" >/dev/null
done

docker exec -i "$source_container" psql -h 127.0.0.1 -U pmplatform -d pmplatform -X -q -v ON_ERROR_STOP=1 \
  <"$here/drill-fixture.sql" >/dev/null

source=$(connection "$source_container")
"$verify" fingerprint "$source" >"$work/source.fingerprint"
echo "   fingerprinted: $(grep -c '^table|' "$work/source.fingerprint") tables," \
  "$(awk -F'|' '/^table\|/ { n += $3 } END { print n }' "$work/source.fingerprint") rows;" \
  "$(grep '^audit-chain|' "$work/source.fingerprint")"

echo "== $(now) backups"
docker exec "$source_container" pg_basebackup -h 127.0.0.1 -U pmplatform -D /tmp/base -Ft -z -X fetch -c fast
docker cp -q "$source_container:/tmp/base/base.tar.gz" "$work/base.tar.gz"
docker exec "$source_container" sh -c 'pg_dump -h 127.0.0.1 -U pmplatform -d pmplatform --no-owner --no-privileges | gzip' \
  >"$work/pmplatform.sql.gz"
echo "   physical $(du -h "$work/base.tar.gz" | cut -f1), export $(du -h "$work/pmplatform.sql.gz" | cut -f1)"
# From here on the source may change; the fingerprint above is what the restores must equal.
sql "$source_container" "INSERT INTO drill_fixture.load (payload, recorded_at) VALUES ('written after the backup', now())"

echo "== $(now) restore 1 of 2: physical backup onto a new server"
started=$(seconds)
docker run -d --name "$physical_container" --network "$network" -p 127.0.0.1::5432 \
  --entrypoint sleep postgres:17 infinity >/dev/null
docker cp -q "$work/base.tar.gz" "$physical_container:/tmp/base.tar.gz"
docker exec "$physical_container" sh -c '
  set -e
  install -d -o postgres -g postgres -m 700 /var/lib/postgresql/restored
  tar -xzf /tmp/base.tar.gz -C /var/lib/postgresql/restored
  chown -R postgres:postgres /var/lib/postgresql/restored
  chmod 700 /var/lib/postgresql/restored
  su postgres -c "pg_ctl -D /var/lib/postgresql/restored -o \"-c listen_addresses=*\" -l /tmp/postgres.log -w start" >/dev/null'
wait_ready "$physical_container"
physical_seconds=$(( $(seconds) - started ))
if "$verify" compare "$work/source.fingerprint" "$(connection "$physical_container")"; then
  physical_result=MATCH
else
  physical_result=FAIL
fi
echo "   restored in ${physical_seconds}s: $physical_result"

echo "== $(now) restore 2 of 2: SQL export into an empty database"
docker run -d --name "$export_container" --network "$network" -p 127.0.0.1::5432 \
  -e POSTGRES_DB=pmplatform -e POSTGRES_USER=pmplatform -e POSTGRES_PASSWORD=pmplatform \
  postgres:17 >/dev/null
wait_ready "$export_container"
started=$(seconds)
gunzip -c "$work/pmplatform.sql.gz" \
  | docker exec -i "$export_container" psql -h 127.0.0.1 -U pmplatform -d pmplatform -X -q -v ON_ERROR_STOP=1 >/dev/null
export_seconds=$(( $(seconds) - started ))
restored=$(connection "$export_container")
if "$verify" compare "$work/source.fingerprint" "$restored"; then
  export_result=MATCH
else
  export_result=FAIL
fi
echo "   restored in ${export_seconds}s: $export_result"

echo "== $(now) faults: each must be reported, with the exit status the runbook relies on"
results="$work/faults"
: >"$results"

# fault <id> <expected exit> <description> <SQL applied to a fresh copy of the restored database>
fault() {
  copy="fault_$(echo "$1" | tr '[:upper:]' '[:lower:]')"
  sql "$export_container" "CREATE DATABASE $copy TEMPLATE pmplatform"
  [ -z "$4" ] || sql "$export_container" "$4" "$copy"
  set +e
  "$verify" compare "$work/source.fingerprint" "$(connection "$export_container" "$copy")" >"$work/$1.out" 2>&1
  status=$?
  set -e
  record "$1" "$2" "$status" "$3"
}

record() {
  if [ "$3" = "$2" ]; then verdict=expected; else verdict=UNEXPECTED; fi
  printf '| %s | %s | %s | %s | %s |\n' "$1" "$4" "$2" "$3" "$verdict" | tee -a "$results"
}

echo "| | Fault | Expected exit | Actual exit | |"
echo "| --- | --- | --- | --- | --- |"
fault F0 0 "none — an unchanged copy, the control: every fault below is judged against it" ""
fault F1 1 "one row missing out of 200,000" \
  "DELETE FROM drill_fixture.load WHERE id = 123456"
fault F2 1 "one character changed in one row, row count unchanged" \
  "UPDATE identity_access.role SET name_ar = name_ar || '.' WHERE code = 'R04'"
fault F3 1 "a whole table missing" \
  "DROP TABLE drill_fixture.load"
fault F4 1 "a sequence behind the data it numbers" \
  "SELECT setval('drill_fixture.load_id_seq', 1)"
fault F5 1 "an index missing" \
  "DROP INDEX drill_fixture.load_recorded_at"
fault F6 1 "one audit event's link rewritten" \
  "UPDATE audit_activity.audit_event SET previous_event_hash = repeat('0', 64) WHERE recorded_at = timestamptz '2026-09-25 00:08:20+00'"

# F7: the import stopped part-way — the first 60% of the export, errors ignored, as a careless
# operator would run it.
sql "$export_container" "CREATE DATABASE fault_f7"
lines=$(gunzip -c "$work/pmplatform.sql.gz" | wc -l)
gunzip -c "$work/pmplatform.sql.gz" | head -n $((lines * 6 / 10)) \
  | docker exec -i "$export_container" psql -h 127.0.0.1 -U pmplatform -d fault_f7 -X -q >/dev/null 2>&1 || true
set +e
"$verify" compare "$work/source.fingerprint" "$(connection "$export_container" fault_f7)" >"$work/F7.out" 2>&1
status=$?
set -e
record F7 1 "$status" "the import stopped part-way"

# F8: the restored server is not there. Nothing was compared, so this must not be a pass.
set +e
"$verify" compare "$work/source.fingerprint" "host=127.0.0.1 port=1 user=pmplatform dbname=pmplatform connect_timeout=3" \
  >"$work/F8.out" 2>&1
status=$?
set -e
record F8 2 "$status" "nothing listening — the comparison never reached a server"

# F9: the chain was already broken at the source, so the restore is a faithful copy of a broken
# chain. The two fingerprints are identical and the comparison must still fail.
sql "$export_container" "CREATE DATABASE fault_f9 TEMPLATE pmplatform"
sql "$export_container" "UPDATE audit_activity.audit_event SET previous_event_hash = repeat('f', 64) WHERE recorded_at = timestamptz '2026-09-25 00:08:20+00'" fault_f9
"$verify" fingerprint "$(connection "$export_container" fault_f9)" >"$work/f9.fingerprint"
set +e
"$verify" compare "$work/f9.fingerprint" "$(connection "$export_container" fault_f9)" >"$work/F9.out" 2>&1
status=$?
set -e
record F9 1 "$status" "a broken chain restored faithfully — identical fingerprints"

echo
echo "== $(now) summary"
echo "   physical restore: $physical_result in ${physical_seconds}s"
echo "   export restore:   $export_result in ${export_seconds}s"
expected=$(grep -c '| expected |' "$results" || true)
echo "   control and faults: $expected of 10 reported as expected"
for f in F2 F4 F6 F9; do
  echo "   -- $f"
  grep -E '^[-+][^-+]|^(MISMATCH|AUDIT CHAIN BROKEN|NOT PROVED)' "$work/$f.out" | sed 's/^/      /'
done
[ "$physical_result" = MATCH ] && [ "$export_result" = MATCH ] && [ "$expected" = 10 ]
