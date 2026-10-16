#!/usr/bin/env bash
# The TASK-080 validation cell, executed locally. Record: docs/architecture/secret-and-dependency-scanning.md §5
#
#   "Introduce a fake credential string in a test PR and confirm the secret scanner blocks it;
#    remove it and confirm the PR is unblocked."
#
# Builds each case as commits on a scratch worktree of <ref> (default HEAD) and runs the gate commands
# the pull-request jobs run, on the same base..head range, asserting each exit code:
#
#   secret-scan
#     clean change                                       passes
#     + a fake API key                                   blocks
#     + the same key marked `gitleaks:allow`             blocks (inline allows are ignored)
#     + the key and an allowlist for it in one change    blocks (the base's configuration is read)
#     + the key, then a commit deleting it               blocks (the value is in the change's history)
#     the key removed by rewriting the change            passes
#   dependency-vuln-gate
#     + log4net 2.0.9 (CVE-2018-1285) and lodash 4.17.11 as a dev dependency (CVE-2019-10744),
#       both CRITICAL with a fixed version               blocks, naming both
#     + both and an exception for each in one change     blocks (the base's exceptions are read)
#     both already in the base, another manifest edit    passes (not this change's finding)
#     both removed by rewriting the change               passes
#
#   .github/scripts/verify-scanning-gates.sh [ref]
#
# Needs git, gitleaks, dotnet, trivy, jq, and npm or docker (for the frontend lock file). Writes only
# under $DRILL_DIR (default artifacts/verify-scanning-gates), which is git-ignored and removed on exit.
set -euo pipefail

REF=${1:-HEAD}
REPO=$(git rev-parse --show-toplevel)
DRILL_DIR=$(mkdir -p "${DRILL_DIR:-$REPO/artifacts/verify-scanning-gates}" && cd "${DRILL_DIR:-$REPO/artifacts/verify-scanning-gates}" && pwd)
TREE="$DRILL_DIR/tree"
export EVIDENCE_DIR="$DRILL_DIR/evidence" WORK_DIR="$DRILL_DIR/work"
export GIT_AUTHOR_NAME=drill GIT_AUTHOR_EMAIL=drill@example.invalid GIT_COMMITTER_NAME=drill GIT_COMMITTER_EMAIL=drill@example.invalid

cleanup() {
  for dir in "$WORK_DIR/base" "$WORK_DIR/head" "$TREE"; do git -C "$REPO" worktree remove --force "$dir" >/dev/null 2>&1 || :; done
  rm -rf "$DRILL_DIR"
}
trap cleanup EXIT
cleanup
mkdir -p "$DRILL_DIR"
git -C "$REPO" worktree add --quiet --detach "$TREE" "$REF"
cd "$TREE"
BASE=$(git rev-parse HEAD)

failures=0
expect() {
  local expected=$1 label=$2 status=0
  shift 2
  rm -rf "$EVIDENCE_DIR"
  "$@" >"$DRILL_DIR/last.log" 2>&1 || status=$?
  if [ "$status" = "$expected" ]; then
    echo "  ok    $label (exit $status)"
  else
    echo "  FAIL  $label: expected exit $expected, got $status"
    sed 's/^/        /' "$DRILL_DIR/last.log" | tail -20
    failures=$((failures + 1))
  fi
}
names() {
  if jq -e --arg id "$1" 'map(.id) | index($id) != null' "$EVIDENCE_DIR/new.json" >/dev/null 2>&1; then
    echo "  ok    the gate names $1"
  else
    echo "  FAIL  the gate does not name $1"
    failures=$((failures + 1))
  fi
}
reset_to() { git reset --quiet --hard "$1" && git clean --quiet -fdx -e artifacts; }
commit() { git add -A && git commit --quiet --no-verify -m "$1"; }
secret_gate() { .github/scripts/secret-scan.sh range "$BASE" "$(git rev-parse HEAD)"; }
dependency_gate() { .github/scripts/dependency-vuln-gate.sh gate "$1" "$(git rev-parse HEAD)"; }

# Random on every run, so no scanner can have seen it, and labelled so no reader takes it for real.
FAKE_KEY=$(head -c 600 /dev/urandom | LC_ALL=C tr -dc 'A-Za-z0-9' | cut -c1-40)
CREDENTIAL_FILE=src/backend/PMPlatform.Infrastructure/DrillCredential.cs
plant_credential() {
  printf 'namespace PMPlatform.Infrastructure;\n\n// TASK-080 drill: a fake credential that must block the merge.\ninternal static class DrillCredential\n{\n    internal const string ReportingApiKey = "%s";%s\n}\n' \
    "$FAKE_KEY" "${1:-}" >"$CREDENTIAL_FILE"
}

echo "secret-scan:"
echo "// TASK-080 drill: a harmless change." >>README.md
commit "drill: harmless change"
expect 0 "a clean change passes" secret_gate
CLEAN=$(git rev-parse HEAD)

plant_credential
commit "drill: plant a fake credential"
expect 1 "a fake API key blocks" secret_gate
PLANTED=$(git rev-parse HEAD)

reset_to "$CLEAN"
plant_credential " // gitleaks:allow"
commit "drill: fake credential marked gitleaks:allow"
expect 1 "an inline gitleaks:allow does not clear it" secret_gate

reset_to "$CLEAN"
plant_credential
printf '\n[[allowlists]]\ndescription = "drill"\ntargetRules = ["generic-api-key"]\ncondition = "AND"\npaths = [%s]\nregexes = [%s]\n' \
  "'''DrillCredential\\.cs'''" "'''.*'''" >>.gitleaks.toml
commit "drill: fake credential and an allowlist for it"
expect 1 "an allowlist in the same change does not clear it" secret_gate

reset_to "$PLANTED"
git rm --quiet "$CREDENTIAL_FILE"
commit "drill: delete the fake credential in a later commit"
expect 1 "deleting it in a later commit does not clear it" secret_gate

reset_to "$CLEAN"
expect 0 "the change rewritten without it passes" secret_gate

# The vulnerable versions are the ones TASK-022's drill uses: each CRITICAL with a fixed version.
plant_dependencies() {
  # perl, not sed: BSD sed does not expand \n in a replacement.
  perl -pi -e 's#^(    <PackageVersion Include="Npgsql" .*)$#$1\n    <PackageVersion Include="log4net" Version="2.0.9" />#' \
    src/backend/Directory.Packages.props
  perl -pi -e 's#^</Project>$#  <ItemGroup>\n    <PackageReference Include="log4net" />\n  </ItemGroup>\n</Project>#' \
    src/backend/PMPlatform.Infrastructure/PMPlatform.Infrastructure.csproj
  grep -q 'Include="log4net" Version="2.0.9"' src/backend/Directory.Packages.props || { echo "error: log4net was not planted" >&2; exit 2; }
  local npm_args=(install lodash@4.17.11 --save-dev --save-exact --package-lock-only --ignore-scripts --no-audit --no-fund)
  if command -v npm >/dev/null 2>&1; then
    (cd src/frontend && npm "${npm_args[@]}" >/dev/null)
  else
    docker run --rm -v "$PWD/src/frontend:/w" -w /w mirror.gcr.io/library/node:24 npm "${npm_args[@]}" >/dev/null
  fi
  grep -q '"node_modules/lodash"' src/frontend/package-lock.json || { echo "error: lodash was not planted" >&2; exit 2; }
}

echo "dependency-vuln-gate:"
reset_to "$BASE"
plant_dependencies
commit "drill: plant two CRITICAL dependencies"
expect 1 "two CRITICAL dependencies block" dependency_gate "$BASE"
names CVE-2018-1285
names CVE-2019-10744
VULNERABLE=$(git rev-parse HEAD)

cat >>.github/security/vulnerability-exceptions.yaml <<'EOF'
  - id: CVE-2018-1285
    purls: [pkg:nuget/log4net@2.0.9]
    expired_at: 2026-12-31
    statement: drill
  - id: CVE-2019-10744
    purls: [pkg:npm/lodash@4.17.11]
    expired_at: 2026-12-31
    statement: drill
EOF
perl -pi -e 's/^vulnerabilities: \[\]$/vulnerabilities:/' .github/security/vulnerability-exceptions.yaml
commit "drill: the dependencies and an exception for each"
expect 1 "an exception in the same change does not clear it" dependency_gate "$BASE"

reset_to "$VULNERABLE"
perl -pi -e 's#^</Project>$#  <!-- TASK-080 drill: an unrelated manifest edit. -->\n</Project>#' src/backend/Directory.Packages.props
commit "drill: an unrelated manifest edit on a base that carries both"
expect 0 "findings the base already carries are not the change's" dependency_gate "$VULNERABLE"

reset_to "$BASE"
expect 0 "the change rewritten without them passes" dependency_gate "$BASE"

if [ "$failures" -gt 0 ]; then
  echo "drill: $failures assertion(s) failed" >&2
  exit 1
fi
echo "drill: both gates blocked every planted case, refused every way around them, and passed once the change was rewritten"
