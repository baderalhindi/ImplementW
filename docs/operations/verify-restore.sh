#!/usr/bin/env sh
# Verifies that a restore reproduced what was backed up (TASK-023, CTL-36). Read-only on both sides.
#
#   docs/operations/verify-restore.sh fingerprint <connection>              > source.fingerprint
#   docs/operations/verify-restore.sh compare     source.fingerprint <connection-to-restored>
#   docs/operations/verify-restore.sh documents   gs://<source-bucket> gs://<restored-bucket>
#   docs/operations/verify-restore.sh report      source.fingerprint restored.fingerprint
#
# fingerprint  prints database-fingerprint.sql's output for one database: a row count and an md5 per
#              table, every sequence, per-schema object counts, and the audit chain's linkage.
# compare      fingerprints the restored database, diffs it against a saved fingerprint, and checks
#              the restored audit chain is intact. The saved side is a file rather than a live
#              connection because the source keeps changing after the backup is taken: what a restore
#              must equal is the source *as it was at the backup*, captured then (runbook §6.2).
# documents    lists both buckets as name, size and CRC32C and diffs the two listings.
# report       prints two saved fingerprints side by side, table by table, as a Markdown table for the
#              evidence log. It reports; compare decides.
#
# <connection> is a libpq connection string or URI. Put no password in it: in GCP connect through the
# Cloud SQL Auth Proxy with --auto-iam-authn, locally use PGPASSWORD or a .pgpass file.
#
# Exit status: 0 the two sides match; 1 they differ, or the audit chain is broken; 2 nothing was
# proved — a connection failed or a tool is missing. 2 is never reported as a pass.
#
# Requires: psql (17, matching the server) for the database commands; gcloud for documents.
# Runbook: docs/operations/backup-restore-dr-runbook.md
set -eu

here=$(cd "$(dirname "$0")" && pwd)
fingerprint_sql="$here/database-fingerprint.sql"

usage() {
  sed -n '4,7p' "$0" | sed 's/^# //' >&2
  exit 2
}

not_proved() {
  echo "NOT PROVED: $*" >&2
  exit 2
}

require_tool() {
  command -v "$1" >/dev/null 2>&1 || not_proved "$1 is not on PATH"
}

# fingerprint <connection> — to stdout. A failed connection is exit 2, never an empty fingerprint:
# an empty file compared with an empty file would otherwise be a match.
fingerprint() {
  require_tool psql
  output=$(psql "$1" -X -q -At -v ON_ERROR_STOP=1 -f "$fingerprint_sql" 2>&1) \
    || not_proved "could not fingerprint the database: $output"
  [ -n "$output" ] || not_proved "the database returned an empty fingerprint"
  printf '%s\n' "$output"
}

# audit_chain_intact <fingerprint-file> — linkage only; recomputing hashes is TASK-073's.
audit_chain_intact() {
  line=$(grep '^audit-chain|' "$1") || return 1
  case "$line" in
    'audit-chain|absent' | 'audit-chain|events=0|'*) return 0 ;;
    *'|genesis=1|broken=0|forks=0') return 0 ;;
    *) return 1 ;;
  esac
}

compare_databases() {
  expected=$1
  [ -s "$expected" ] || not_proved "$expected is missing or empty"
  actual=$(mktemp)
  trap 'rm -f "$actual"' EXIT
  fingerprint "$2" >"$actual"

  status=0
  if diff -u "$expected" "$actual"; then
    echo "MATCH: $(grep -c '^table|' "$actual") tables," \
      "$(awk -F'|' '/^table\|/ { n += $3 } END { print n + 0 }' "$actual") rows," \
      "$(grep -c '^sequence|' "$actual") sequences, identical counts and checksums"
  else
    echo "MISMATCH: the lines above marked - are the source at the backup, + the restored database" >&2
    status=1
  fi

  if audit_chain_intact "$actual"; then
    echo "AUDIT CHAIN: $(grep '^audit-chain|' "$actual" | cut -d'|' -f2-)"
  else
    echo "AUDIT CHAIN BROKEN: $(grep '^audit-chain|' "$actual" | cut -d'|' -f2-)" >&2
    status=1
  fi
  return $status
}

# bucket_listing <gs://bucket> — every live object as name,size,crc32c, sorted.
bucket_listing() {
  output=$(gcloud storage objects list "$1/**" --format='csv[no-heading](name,size,crc32c_hash)' 2>&1) \
    || not_proved "could not list $1: $output"
  printf '%s\n' "$output" | sed '/^$/d' | LC_ALL=C sort
}

compare_documents() {
  require_tool gcloud
  source_listing=$(mktemp)
  restored_listing=$(mktemp)
  trap 'rm -f "$source_listing" "$restored_listing"' EXIT
  bucket_listing "$1" >"$source_listing"
  bucket_listing "$2" >"$restored_listing"
  [ -s "$source_listing" ] || not_proved "$1 lists no objects; there is nothing to compare"

  if diff -u "$source_listing" "$restored_listing"; then
    echo "MATCH: $(wc -l <"$source_listing" | tr -d ' ') objects, identical names, sizes and CRC32C"
  else
    echo "MISMATCH: - is $1, + is $2" >&2
    return 1
  fi
}

# report <source-fingerprint> <restored-fingerprint> — one row per table present on either side.
report() {
  [ -s "$1" ] && [ -s "$2" ] || not_proved "both fingerprints must exist and be non-empty"
  tables_a=$(mktemp)
  tables_b=$(mktemp)
  trap 'rm -f "$tables_a" "$tables_b"' EXIT
  grep '^table|' "$1" | cut -d'|' -f2- | LC_ALL=C sort >"$tables_a"
  grep '^table|' "$2" | cut -d'|' -f2- | LC_ALL=C sort >"$tables_b"
  echo "| Table | Source rows | Restored rows | Source checksum (md5) | Restored checksum (md5) | |"
  echo "| --- | ---: | ---: | --- | --- | --- |"
  LC_ALL=C join -t'|' -a1 -a2 -e missing -o 0,1.2,2.2,1.3,2.3 "$tables_a" "$tables_b" \
    | awk -F'|' '{ verdict = ($2 == $3 && $4 == $5) ? "match" : "**DIFFERS**"
                   printf "| `%s` | %s | %s | `%s` | `%s` | %s |\n", $1, $2, $3, $4, $5, verdict }'
}

[ $# -ge 2 ] || usage
command=$1
shift
case "$command" in
  fingerprint) [ $# -eq 1 ] || usage; fingerprint "$1" ;;
  compare) [ $# -eq 2 ] || usage; compare_databases "$1" "$2" ;;
  documents) [ $# -eq 2 ] || usage; compare_documents "$1" "$2" ;;
  report) [ $# -eq 2 ] || usage; report "$1" "$2" ;;
  *) usage ;;
esac
