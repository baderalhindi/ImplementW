#!/usr/bin/env bash
# Release security scan (TASK-022, CTL-41). Record: docs/architecture/artifact-build-and-scanning.md
#
# Every scan is a scan of an SBOM. The release's software bill of materials is generated once, when
# it is built, and the vulnerability report and the gate both read that SBOM rather than re-reading
# the image or the source tree. Two things follow. The report describes exactly the components the
# SBOM lists, so the two cannot disagree. And a later promotion can re-scan the same SBOM against
# the vulnerability database as it stands on the day of that promotion, which is what makes a CVE
# published after the build block SIT, UAT or PROD without rebuilding anything.
#
#   release-security-scan.sh sbom-image <image-ref>   SBOM of the release image: base-image OS packages and the published .NET closure
#   release-security-scan.sh sbom-manifests           SBOMs of the backend and frontend package manifests
#   release-security-scan.sh scan                     report every finding in every SBOM; never fails on a finding
#   release-security-scan.sh gate                     fail on any fixable HIGH or CRITICAL finding not excepted
#
# Works on $EVIDENCE_DIR (default artifacts/security): `scan` and `gate` read every sbom-*.cdx.json
# found there, so the same two commands serve the build and every promotion. Needs trivy and jq.
set -euo pipefail

EVIDENCE_DIR=${EVIDENCE_DIR:-artifacts/security}
EXCEPTIONS=${EXCEPTIONS:-.github/security/vulnerability-exceptions.yaml}

# The gate's line. TASK-022 requires CRITICAL; HIGH is TASK-018's threshold for the release image,
# kept rather than loosened, and applied to the manifests too so the release has one policy.
# "With an available fix" is --ignore-unfixed: a finding nobody can act on by upgrading does not
# stop a release, and it stays in the report.
BLOCKING_SEVERITIES=HIGH,CRITICAL

usage() {
  echo "usage: $0 {sbom-image <image-ref> | sbom-manifests | scan | gate}" >&2
  exit 2
}

need() {
  command -v "$1" >/dev/null 2>&1 || { echo "error: $1 is required and is not on PATH" >&2; exit 2; }
}

# Fails rather than returning nothing: a gate that found no SBOM to read has checked nothing, and
# must not report that as a pass.
load_sboms() {
  shopt -s nullglob
  SBOMS=("$EVIDENCE_DIR"/sbom-*.cdx.json)
  shopt -u nullglob
  [ ${#SBOMS[@]} -gt 0 ] || { echo "error: no sbom-*.cdx.json in $EVIDENCE_DIR" >&2; exit 2; }
}

# sbom-backend.cdx.json -> backend
component_of() {
  basename "$1" .cdx.json | sed 's/^sbom-//'
}

summary() {
  if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
    cat >>"$GITHUB_STEP_SUMMARY"
  else
    cat >/dev/null
  fi
}

sbom_image() {
  local image_ref=${1:?usage: sbom-image <image-ref>}
  trivy image --quiet --format cyclonedx --output "$EVIDENCE_DIR/sbom-api-image.cdx.json" "$image_ref"
  echo "sbom: api-image ($image_ref)"
}

# The backend graph is read from each project's packages.lock.json, written by a restore with
# RestorePackagesWithLockFile. Trivy does not read obj/project.assets.json, so without lock files it
# sees the ten direct references in Directory.Packages.props and none of their transitive
# dependencies. The frontend's is package-lock.json, production dependencies only: the dev
# toolchain builds the bundle and is not shipped in it.
sbom_manifests() {
  compgen -G "src/backend/*/packages.lock.json" >/dev/null || {
    echo "error: src/backend has no packages.lock.json; run" >&2
    echo "  dotnet restore src/backend -p:RestorePackagesWithLockFile=true" >&2
    exit 2
  }
  trivy fs --quiet --format cyclonedx --output "$EVIDENCE_DIR/sbom-backend.cdx.json" src/backend
  echo "sbom: backend (src/backend)"
  trivy fs --quiet --format cyclonedx --output "$EVIDENCE_DIR/sbom-frontend.cdx.json" src/frontend/package-lock.json
  echo "sbom: frontend (src/frontend/package-lock.json)"
}

scan() {
  {
    echo "### Dependency and image scan"
    echo
    echo "Every finding, fixable or not. The gate below decides on the fixable HIGH and CRITICAL ones."
    echo
    echo "| SBOM | Components | CRITICAL | HIGH | MEDIUM | LOW | UNKNOWN |"
    echo "| --- | --- | --- | --- | --- | --- | --- |"
  } | summary

  local sbom component report
  load_sboms
  for sbom in "${SBOMS[@]}"; do
    component=$(component_of "$sbom")
    report="$EVIDENCE_DIR/vulnerabilities-$component.json"
    trivy sbom --quiet --scanners vuln --format json --output "$report" "$sbom"
    trivy convert --format table --scanners vuln --output "$EVIDENCE_DIR/vulnerabilities-$component.txt" "$report"
    jq -r --arg component "$component" --argjson components "$(jq '.components | length' "$sbom")" '
      [.Results[]?.Vulnerabilities[]?.Severity] as $s
      | [$component, $components]
        + (["CRITICAL", "HIGH", "MEDIUM", "LOW", "UNKNOWN"] | map(. as $level | $s | map(select(. == $level)) | length))
      | "| " + (map(tostring) | join(" | ")) + " |"' "$report" | tee /dev/stderr | summary
  done
}

gate() {
  [ -f "$EXCEPTIONS" ] || { echo "error: $EXCEPTIONS not found" >&2; exit 2; }

  local sbom component report blocked=()
  load_sboms
  for sbom in "${SBOMS[@]}"; do
    component=$(component_of "$sbom")
    report="$EVIDENCE_DIR/gate-$component.json"
    # --exit-code 0 and the count below, rather than --exit-code 1: every SBOM is gated and reported
    # before the step fails, so one red component does not hide a second.
    trivy sbom --quiet --scanners vuln --severity "$BLOCKING_SEVERITIES" --ignore-unfixed \
      --ignorefile "$EXCEPTIONS" --exit-code 0 --format json --output "$report" "$sbom"
    if [ "$(jq '[.Results[]?.Vulnerabilities[]?] | length' "$report")" -gt 0 ]; then
      blocked+=("$component")
    fi
  done

  if [ ${#blocked[@]} -eq 0 ]; then
    echo "gate: no fixable $BLOCKING_SEVERITIES finding in ${#SBOMS[@]} SBOM(s)"
    printf '\n**Gate: passed.** No fixable %s finding.\n' "$BLOCKING_SEVERITIES" | summary
    return 0
  fi

  {
    echo
    echo "**Gate: blocked.** Each finding below has a fixed version; upgrade to it, or record a reviewed, dated exception in \`$EXCEPTIONS\`."
    echo
    echo "| SBOM | Severity | Vulnerability | Package | Installed | Fixed in |"
    echo "| --- | --- | --- | --- | --- | --- |"
  } | summary
  for component in "${blocked[@]}"; do
    jq -r --arg component "$component" '
      .Results[]?.Vulnerabilities[]?
      | "| \($component) | \(.Severity) | \(.VulnerabilityID) | \(.PkgName) | \(.InstalledVersion) | \(.FixedVersion) |"' \
      "$EVIDENCE_DIR/gate-$component.json" | tee /dev/stderr | summary
  done
  echo "gate: blocked — fixable $BLOCKING_SEVERITIES finding(s) in: ${blocked[*]}" >&2
  return 1
}

[ $# -ge 1 ] || usage
need trivy
need jq
mkdir -p "$EVIDENCE_DIR"

command=$1
shift
case "$command" in
  sbom-image) sbom_image "$@" ;;
  sbom-manifests) sbom_manifests ;;
  scan) scan ;;
  gate) gate ;;
  *) usage ;;
esac
