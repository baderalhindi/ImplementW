#!/usr/bin/env bash
# The TASK-022 validation cell, executed. Record: docs/architecture/artifact-build-and-scanning.md §5
#
#   "Introduce a dependency with a known CRITICAL CVE in a test branch and confirm the pipeline
#    blocks it; remove it and confirm the pipeline goes green."
#
# Runs the pipeline's own gate commands — the same script, the same restore, the same order as the
# dependency-scan and package jobs and the promotion re-scan — three times, in a scratch worktree of
# the commit under test, and asserts the outcome of each:
#
#   baseline  the commit as it is                                  gate passes
#   planted   + log4net 2.0.9 (backend, CVE-2018-1285, CRITICAL, fixed in 2.0.10)
#             + lodash 4.17.11 (frontend, CVE-2019-10744, CRITICAL, fixed in 4.17.12)
#                                                                  gate blocks, naming both CVEs,
#                                                                  and the report is still written
#   removed   both taken out again                                 gate passes
#
#   .github/scripts/verify-dependency-gate.sh                 manifests only
#   .github/scripts/verify-dependency-gate.sh --with-image    also build the release image each time,
#                                                             gate it, and re-scan all three SBOMs
#                                                             together as a promotion stage does
#
# Needs git, dotnet, npm, trivy and jq; --with-image also needs docker. Writes nothing into this
# checkout: the worktree is created under a temporary directory and removed on exit.
set -euo pipefail

WITH_IMAGE=0
case "${1:-}" in
  --with-image) WITH_IMAGE=1 ;;
  "") ;;
  *) echo "usage: $0 [--with-image]" >&2; exit 2 ;;
esac

for tool in git dotnet npm trivy jq; do
  command -v "$tool" >/dev/null 2>&1 || { echo "error: $tool is required and is not on PATH" >&2; exit 2; }
done
[ "$WITH_IMAGE" = "0" ] || command -v docker >/dev/null 2>&1 ||
  { echo "error: --with-image needs docker" >&2; exit 2; }

REF=${DRILL_REF:-HEAD}
SCRATCH=$(mktemp -d)
WORKTREE="$SCRATCH/worktree"
REPO=$(git rev-parse --show-toplevel)

cleanup() {
  git -C "$REPO" worktree remove --force "$WORKTREE" >/dev/null 2>&1 || true
  rm -rf "$SCRATCH"
}
trap cleanup EXIT

git -C "$REPO" worktree add --quiet --detach "$WORKTREE" "$REF"
cd "$WORKTREE"
SCAN=.github/scripts/release-security-scan.sh

failures=0
check() {
  if [ "$1" = "$2" ]; then
    echo "  ok    $3"
  else
    echo "  FAIL  $3 (expected exit $2, got $1)"
    failures=$((failures + 1))
  fi
}

reports_name() {
  local cve=$1 dir=$2
  if jq -e -s --arg cve "$cve" '[.[].Results[]?.Vulnerabilities[]?.VulnerabilityID] | index($cve) != null' \
      "$dir"/gate-*.json >/dev/null 2>&1; then
    echo "  ok    the gate report names $cve"
  else
    echo "  FAIL  the gate report does not name $cve"
    failures=$((failures + 1))
  fi
}

# One pass of the pipeline's security gates over the worktree as it stands. Prints the exit code of
# each gate as "<manifests> <image> <promotion>"; "-" where the leg did not run.
run_gates() {
  local phase=$1 manifests image=- promotion=-
  rm -rf artifacts "$SCRATCH/evidence-$phase"
  mkdir -p "$SCRATCH/evidence-$phase"/{dependencies,image,promotion}

  # dependency-scan, step for step.
  dotnet restore src/backend -p:RestorePackagesWithLockFile=true -p:NuGetAudit=false --verbosity quiet >&2
  export EVIDENCE_DIR="$SCRATCH/evidence-$phase/dependencies"
  "$SCAN" sbom-manifests >&2
  "$SCAN" scan >&2
  manifests=0; "$SCAN" gate >&2 || manifests=$?

  if [ "$WITH_IMAGE" = "1" ]; then
    # package, step for step, from the drill's copy of the Dockerfile (below).
    docker build --quiet --file infra/docker/api.Dockerfile --tag "pmplatform-api:drill-$phase" . >&2
    export EVIDENCE_DIR="$SCRATCH/evidence-$phase/image"
    "$SCAN" sbom-image "pmplatform-api:drill-$phase" >&2
    "$SCAN" scan >&2
    image=0; "$SCAN" gate >&2 || image=$?

    # The promotion re-scan: both artifacts merged, as download-artifact merge-multiple does.
    cp "$SCRATCH/evidence-$phase"/dependencies/sbom-*.cdx.json "$SCRATCH/evidence-$phase"/image/sbom-*.cdx.json \
      "$SCRATCH/evidence-$phase/promotion/"
    export EVIDENCE_DIR="$SCRATCH/evidence-$phase/promotion"
    "$SCAN" scan >&2
    promotion=0; "$SCAN" gate >&2 || promotion=$?
  fi
  unset EVIDENCE_DIR
  echo "$manifests $image $promotion"
}

assert_phase() {
  local phase=$1 expected=$2 result manifests image promotion
  echo "== $phase" >&2
  result=$(run_gates "$phase")
  read -r manifests image promotion <<<"$result"
  echo "$phase:"
  check "$manifests" "$expected" "dependency-scan gate"
  if [ "$WITH_IMAGE" = "1" ]; then
    check "$image" "$expected" "package (image) gate"
    check "$promotion" "$expected" "promotion re-scan of all three SBOMs"
  fi
}

# In the pipeline an image carrying a vulnerable package is never built: NuGet's audit fails the
# Dockerfile's restore under warnings-as-errors, and dependency-scan fails first anyway. To observe
# the image gate's own verdict, the drill's copy of the Dockerfile restores with the audit off. The
# copy is the worktree's, never this checkout's.
if [ "$WITH_IMAGE" = "1" ]; then
  sed -i.bak 's#^RUN dotnet restore \(.*\)$#RUN dotnet restore \1 -p:NuGetAudit=false#' infra/docker/api.Dockerfile
  rm -f infra/docker/api.Dockerfile.bak
  grep -q 'NuGetAudit=false' infra/docker/api.Dockerfile ||
    { echo "error: could not turn the audit off in the drill's Dockerfile" >&2; exit 2; }
fi

assert_phase baseline 0

# The test branch: one vulnerable dependency in each manifest, each with a fixed version available.
sed -i.bak 's#^\(    <PackageVersion Include="Npgsql" .*\)$#\1\n    <PackageVersion Include="log4net" Version="2.0.9" />#' \
  src/backend/Directory.Packages.props
sed -i.bak 's#^</Project>$#  <ItemGroup>\n    <PackageReference Include="log4net" />\n  </ItemGroup>\n</Project>#' \
  src/backend/PMPlatform.Infrastructure/PMPlatform.Infrastructure.csproj
rm -f src/backend/*.bak src/backend/*/*.bak
(cd src/frontend && npm install lodash@4.17.11 --save-exact --package-lock-only --ignore-scripts --no-audit --no-fund >&2)
git --no-pager diff --stat -- src >&2

assert_phase planted 1
evidence="$SCRATCH/evidence-planted"
reports_name CVE-2018-1285 "$evidence/dependencies"
reports_name CVE-2019-10744 "$evidence/dependencies"
for report in vulnerabilities-backend.json vulnerabilities-frontend.json; do
  if [ -s "$evidence/dependencies/$report" ]; then
    echo "  ok    $report was written although the gate failed"
  else
    echo "  FAIL  $report is missing"
    failures=$((failures + 1))
  fi
done
if [ "$WITH_IMAGE" = "1" ]; then
  # The frontend is not in the image (cicd-pipeline.md F-3), so the image names the backend CVE only.
  reports_name CVE-2018-1285 "$evidence/image"
  reports_name CVE-2018-1285 "$evidence/promotion"
  reports_name CVE-2019-10744 "$evidence/promotion"
fi

# Removed again.
git checkout --quiet -- src
git clean --quiet -fdx -- src

assert_phase removed 0

if [ "$failures" -gt 0 ]; then
  echo "drill: $failures assertion(s) failed" >&2
  exit 1
fi
echo "drill: the gate passed the clean commit, blocked both CRITICAL CVEs, and passed again once they were removed"
