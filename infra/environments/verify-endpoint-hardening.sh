#!/usr/bin/env sh
# Runs TASK-078's validation checks against a deployed environment and reports one line per check:
#   "Script a login-attempt burst against SIT and confirm throttling kicks in at the configured
#    threshold; run a header-scan tool against the deployed frontend and API and confirm all
#    required headers are present."
#
#   infra/environments/verify-endpoint-hardening.sh sit
#   BASE_URL=https://pmplatform.ahda.test CA_BUNDLE=edge-root.crt infra/environments/verify-endpoint-hardening.sh sit
#
# The address is the environment's APP_BASE_URL (environments.json): the SPA at /, the API at /api
# (TASK-016 D-1). BASE_URL overrides it, for a rehearsal against another stack.
#
# Checks (record: docs/architecture/endpoint-hardening.md §6):
#   E-1  a burst of SIGN_IN_LIMIT + 1 sign-ins from this machine: the first SIGN_IN_LIMIT are not 429,
#        the next is 429 RATE_LIMITED with Retry-After                          (workbook, criterion 1)
#   E-2  a preflight and a request from an Origin not on CORS_ALLOWED_ORIGINS are refused with
#        403 ORIGIN_NOT_ALLOWED and no Access-Control-Allow-Origin                 (criterion 2)
#   E-3  the SPA's / and the API's 401 carry the five required headers and the four further ones
#                                                                                  (workbook, criterion 3)
#   E-4  the MDN HTTP Observatory scanner reports no failed test (needs Node 24+)     (criterion 3)
#
# The burst signs in as a username no directory holds (PROBE_USERNAME), so it cannot lock out a real
# account. It must start a fresh window: run it no sooner than a minute after any other sign-in from
# this machine's address, and from an address no one else shares, or E-1 counts their requests too.
# A check that cannot be executed is reported SKIP and the run exits non-zero. Exit 0 only when
# every check is PASS.
#
# Requires: curl, jq; npx from Node 24+ for E-4 (the scanner refuses older Node).
set -eu

here=$(cd "$(dirname "$0")" && pwd)
. "$here/lib/common.sh"

require_tools curl jq
require_known_environment "${1:-}"
environment=$1

base=${BASE_URL:-$(env_get "$environment" '.variables.APP_BASE_URL.value')}
[ -n "$base" ] || die "APP_BASE_URL for $environment is empty in $MANIFEST (environment-separation.md F-1); set BASE_URL to rehearse elsewhere."
base=${base%/}
host=$(printf '%s' "$base" | sed -E 's#^https?://([^/:]+).*#\1#')

limit=${SIGN_IN_LIMIT:-30}             # RateLimiting:Policies:SignIn:PermitLimit as deployed
probe=${PROBE_USERNAME:-ratelimit.probe.no-such-user}
foreign=${FOREIGN_ORIGIN:-https://evil.example}

failures=0
report() { # report <PASS|FAIL|SKIP> <id> <text>
  printf '%-4s %-4s %s\n' "$1" "$2" "$3"
  [ "$1" = "PASS" ] || failures=$((failures + 1))
}

work=$(mktemp -d "${TMPDIR:-$here}/verify-endpoint-hardening.XXXXXX")
trap 'rm -rf "$work"' EXIT

# http <method> <path> [curl args...] — headers to $work/h, body to $work/b; prints the status.
http() {
  method=$1 path=$2
  shift 2
  curl --silent --show-error --max-time 30 ${CA_BUNDLE:+--cacert "$CA_BUNDLE"} \
    -X "$method" -D "$work/h" -o "$work/b" -w '%{http_code}' "$@" "$base$path"
}
header() { # header <name> — the value of a response header in $work/h, empty if absent
  grep -i "^$1:" "$work/h" | head -n 1 | sed -E 's/^[^:]+:[[:space:]]*//' | tr -d '\r'
}
code() { jq -r '.code // ""' "$work/b" 2>/dev/null || true; }

# E-1 — the burst.
statuses=""
attempt=0
while [ "$attempt" -le "$limit" ]; do
  attempt=$((attempt + 1))
  status=$(http POST /api/v1/sessions -H 'Content-Type: application/json' \
    --data "{\"username\":\"$probe\",\"password\":\"wrong-$attempt\"}")
  statuses="$statuses $status"
done
last_status=$status last_code=$(code) retry_after=$(header Retry-After)
throttled_early=$(printf '%s\n' $statuses | head -n "$limit" | grep -c '^429$' || true)
if [ "$throttled_early" -eq 0 ] && [ "$last_status" = 429 ] && [ "$last_code" = RATE_LIMITED ] && [ -n "$retry_after" ]; then
  report PASS E-1 "attempts 1-$limit answered$(printf '%s\n' $statuses | head -n "$limit" | sort | uniq -c | awk '{printf " %s x %s", $1, $2}'); attempt $((limit + 1)) answered 429 RATE_LIMITED, Retry-After $retry_after"
else
  report FAIL E-1 "expected $limit non-429 then 429 RATE_LIMITED with Retry-After; got:$statuses (last code '$last_code', Retry-After '$retry_after')"
fi

# E-2 — a foreign Origin, preflight and simple request.
status=$(http OPTIONS /api/v1/sessions -H "Origin: $foreign" -H 'Access-Control-Request-Method: POST' -H 'Access-Control-Request-Headers: content-type')
if [ "$status" = 403 ] && [ "$(code)" = ORIGIN_NOT_ALLOWED ] && [ -z "$(header Access-Control-Allow-Origin)" ]; then
  report PASS E-2 "preflight from $foreign refused: 403 ORIGIN_NOT_ALLOWED, no Access-Control-Allow-Origin"
else
  report FAIL E-2 "preflight from $foreign answered $status '$(code)', Access-Control-Allow-Origin '$(header Access-Control-Allow-Origin)'"
fi
status=$(http POST /api/v1/sessions/current/refresh -H "Origin: $foreign" -H 'Content-Type: application/json' --data '{"refreshToken":"x"}')
if [ "$status" = 403 ] && [ "$(code)" = ORIGIN_NOT_ALLOWED ]; then
  report PASS E-2 "request from $foreign not executed: 403 ORIGIN_NOT_ALLOWED"
else
  report FAIL E-2 "request from $foreign answered $status '$(code)'"
fi

# E-3 — the headers on the frontend and on the API.
required="Content-Security-Policy X-Content-Type-Options X-Frame-Options Strict-Transport-Security Referrer-Policy"
further="Permissions-Policy Cross-Origin-Opener-Policy Cross-Origin-Resource-Policy Cross-Origin-Embedder-Policy"
for target in "frontend /" "API /api/v1/sessions/current"; do
  name=${target%% *} path=${target#* }
  status=$(http GET "$path")
  missing=""
  for h in $required $further; do [ -n "$(header "$h")" ] || missing="$missing $h"; done
  if [ -z "$missing" ]; then
    report PASS E-3 "$name $path ($status): all nine headers present; CSP '$(header Content-Security-Policy)'; HSTS '$(header Strict-Transport-Security)'"
  else
    report FAIL E-3 "$name $path ($status): missing$missing"
  fi
done

# E-4 — the MDN HTTP Observatory scanner (the engine behind developer.mozilla.org/observatory).
if command -v npx >/dev/null 2>&1; then
  if npx --yes --package=@mdn/mdn-http-observatory@1.7.1 mdn-http-observatory-scan "$host" >"$work/scan.json" 2>"$work/scan.err" && jq -e '.scan' "$work/scan.json" >/dev/null 2>&1; then
    failed=$(jq -r '[.tests | to_entries[] | select(.value.pass | not) | "\(.key)=\(.value.result)"] | join(" ")' "$work/scan.json")
    summary=$(jq -r '"grade \(.scan.grade), score \(.scan.score), \(.scan.testsPassed)/\(.scan.testsQuantity) tests passed"' "$work/scan.json")
    if [ -z "$failed" ]; then
      report PASS E-4 "Observatory on $host: $summary"
    else
      report FAIL E-4 "Observatory on $host: $summary; failed: $failed"
    fi
  else
    report FAIL E-4 "Observatory scan of $host did not complete: $(head -c 300 "$work/scan.err" "$work/scan.json" 2>/dev/null | tr '\n' ' ')"
  fi
else
  report SKIP E-4 "npx not found; install Node 24+ or scan $host at https://developer.mozilla.org/observatory"
fi

[ "$failures" -eq 0 ] || exit 1
