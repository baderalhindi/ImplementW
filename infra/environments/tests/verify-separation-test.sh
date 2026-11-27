#!/usr/bin/env sh
# Runs verify-separation.sh against the stand-in gcloud and psql in stub/ and checks what it
# reports. Nothing reaches a provider. Run in the repo-checks CI job (QA PHASE-E-01).
#
#   sh infra/environments/tests/verify-separation-test.sh
#
# Requires: jq.
set -eu

here=$(cd "$(dirname "$0")" && pwd)
script="$here/../verify-separation.sh"
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

# A manifest with the organisation the committed one leaves empty (UGV-07), so D-3b runs.
jq '.platform.organization_id = "123456789012"' "$here/../environments.json" >"$work/environments.json"

failed=0
# scenario <name> <expected exit: 0|nonzero> <VAR=value for the stubs, or -> \
#          <grep pattern that must match>... -- <pattern that must not>...
scenario() {
  name=$1 expected=$2 setting=$3
  shift 3
  [ "$setting" = - ] && setting=STUB_NONE=1
  status=0
  env PATH="$here/stub:$PATH" MANIFEST="$work/environments.json" \
    SIT_DB_HOST=sit-db DEV_DB_PASSWORD=dev-password "$setting" \
    sh "$script" >"$work/out" 2>&1 || status=$?
  problems=""
  case "$expected" in
    0) [ "$status" -eq 0 ] || problems="$problems exit $status, expected 0;" ;;
    nonzero) [ "$status" -ne 0 ] || problems="$problems exit 0, expected non-zero;" ;;
  esac
  mode=must
  for pattern in "$@"; do
    if [ "$pattern" = "--" ]; then mode=mustnot; continue; fi
    if grep -Eq "$pattern" "$work/out"; then
      [ "$mode" = mustnot ] && problems="$problems printed /$pattern/;"
    else
      [ "$mode" = must ] && problems="$problems missing /$pattern/;"
    fi
  done
  if [ -n "$problems" ]; then
    echo "FAIL $name:$problems"
    sed 's/^/    | /' "$work/out" | tail -n 15
    failed=$((failed + 1))
  else
    echo "ok   $name"
  fi
}

# A separated platform: every check passes and the run exits 0.
scenario "separated platform passes" 0 - \
  '^PASS  D-1 ' '^PASS  D-2 ' '^PASS  D-3 ' '^PASS  D-3b ' '^PASS  D-3c ' '^PASS  D-4 ' '^OK: every separation drill passed' \
  -- '^(FAIL|SKIP|ERROR) '

# QA PHASE-E-01: no credentials. Nothing may pass, every check is reported, the summary is printed.
scenario "offline: no PASS, ERROR per check, summary" nonzero STUB_OFFLINE=1 \
  '^ERROR D-2 +cannot act as' '^ERROR D-3 ' '^ERROR D-3b ' '^ERROR D-3c ' '^ERROR D-4 ' 'check\(s\) did not pass' \
  -- '^PASS  D-[234]' 'ABORTED'
# The impersonation itself fails: the reads are not attempted and not counted as refused.
scenario "impersonation refused is not a secret refusal" nonzero STUB_IMPERSONATION=fail \
  '^ERROR D-2 +cannot act as deploy-dev@' 'check\(s\) did not pass' -- '^PASS  D-2 '
# A failure that is not Secret Manager's refusal (API disabled) proves nothing.
scenario "API error is not a refusal" nonzero STUB_SECRET_ERROR=1 '^ERROR D-2 .*SERVICE_DISABLED' -- '^PASS  D-2 '
# A readable PROD secret is still caught.
scenario "readable PROD secret fails" nonzero STUB_LEAK=pmplatform-prod-jwt-signing-key \
  '^FAIL  D-2 .*READ the PROD secret pmplatform-prod-jwt-signing-key'
# An unreadable folder policy used to read as "no service account" (PASS).
scenario "unreadable folder policy is ERROR" nonzero STUB_FOLDER_POLICY=fail '^ERROR D-3b ' -- '^PASS  D-3b '
# D-1: a database that never answered has not rejected the credential.
scenario "unreachable SIT database is ERROR" nonzero STUB_PSQL=unreachable '^ERROR D-1 .*timeout' -- '^PASS  D-1 '
scenario "DEV credential accepted by SIT fails" nonzero STUB_PSQL=connected '^FAIL  D-1 '
# A run that stops early still says so and exits non-zero.
jq '.platform.region = ""' "$work/environments.json" >"$work/no-region.json"
scenario "early stop prints ABORTED" nonzero MANIFEST="$work/no-region.json" '^ABORTED: the run stopped'

echo
if [ "$failed" -eq 0 ]; then
  echo "OK: verify-separation.sh reports every scenario correctly."
else
  echo "$failed scenario(s) failed."
  exit 1
fi
