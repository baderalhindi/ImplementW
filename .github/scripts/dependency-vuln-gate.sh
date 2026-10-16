#!/usr/bin/env bash
# Dependency vulnerability gate (TASK-080, CTL-42). Record: docs/architecture/secret-and-dependency-scanning.md
#
#   dependency-vuln-gate.sh gate <base> <head>   block on a fixable CRITICAL that <head> has and <base> does not
#   dependency-vuln-gate.sh report               list every fixable HIGH or CRITICAL in the checked-out tree
#
# Exit 0: nothing blocks (gate) or nothing found (report). Exit 1: it does. Exit 2: the scan could not run.
#
# `gate` is the pull-request check. "New" is decided per vulnerability and package: a finding <base>
# already carries is not this change's, and the weekly `report` and the release gate (TASK-022) are
# what make the owner fix it. A change that touches no package manifest cannot add a dependency, so
# it passes without a scan. Both trees are scanned with <base>'s exceptions file, so a change cannot
# except its own finding; an exception is a reviewed change of its own (CODEOWNERS: Security Lead).
#
# What is scanned: the backend's full NuGet graph (each project's packages.lock.json, written by a
# restore here, as release-security-scan.sh does) and the frontend's package-lock.json including
# dev dependencies, which do not ship but do run on the runner with the repository's token
# (artifact-build-and-scanning.md F-5).
#
# Needs git, dotnet, trivy and jq. Worktrees go under $WORK_DIR (default $RUNNER_TEMP, else
# artifacts/); the reports and a Markdown summary go to $EVIDENCE_DIR.
set -euo pipefail

GATE_SEVERITIES=CRITICAL
REPORT_SEVERITIES=HIGH,CRITICAL
EXCEPTIONS=.github/security/vulnerability-exceptions.yaml
EVIDENCE_DIR=${EVIDENCE_DIR:-artifacts/security/dependency-vuln-gate}
WORK_DIR=${WORK_DIR:-${RUNNER_TEMP:-artifacts}/dependency-vuln-gate}
SUMMARY="$EVIDENCE_DIR/summary.md"
NO_COMMIT=0000000000000000000000000000000000000000

# The files a dependency can enter through. Anything else leaves both graphs as they are.
MANIFESTS=(
  ':(glob)**/Directory.Packages.props'
  ':(glob)**/Directory.Build.props'
  ':(glob)**/Directory.Build.targets'
  ':(glob)**/*.csproj'
  ':(glob)**/[Nn]u[Gg]et.[Cc]onfig'
  ':(glob)**/packages.lock.json'
  ':(glob)**/package.json'
  ':(glob)**/package-lock.json'
)

usage() {
  echo "usage: $0 {gate <base> <head> | report}" >&2
  exit 2
}

need() {
  command -v "$1" >/dev/null 2>&1 || { echo "error: $1 is required and is not on PATH" >&2; exit 2; }
}

is_commit() {
  [ -n "$1" ] && [ "$1" != "$NO_COMMIT" ] && git cat-file -e "$1^{commit}" 2>/dev/null
}

publish_summary() {
  cat "$SUMMARY"
  if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then cat "$SUMMARY" >>"$GITHUB_STEP_SUMMARY"; fi
}

# findings <tree> <exceptions> <severities> <out>: every finding of those severities with a fixed
# version, not excepted, as one JSON array over both manifests.
findings() {
  local tree=$1 exceptions=$2 severities=$3 out=$4 name
  name=$(basename "$out" .json)
  dotnet restore "$tree/src/backend" -p:RestorePackagesWithLockFile=true -p:NuGetAudit=false --verbosity quiet >&2
  compgen -G "$tree/src/backend/*/packages.lock.json" >/dev/null ||
    { echo "error: the restore of $tree wrote no packages.lock.json" >&2; exit 2; }

  local common=(--quiet --scanners vuln --severity "$severities" --ignore-unfixed
    --ignorefile "$exceptions" --exit-code 0 --format json)
  trivy fs "${common[@]}" --output "$EVIDENCE_DIR/$name-backend.trivy.json" "$tree/src/backend"
  trivy fs "${common[@]}" --include-dev-deps --output "$EVIDENCE_DIR/$name-frontend.trivy.json" \
    "$tree/src/frontend/package-lock.json"

  jq -s '[.[] | (.ArtifactName | if test("package-lock") then "frontend" else "backend" end) as $component
          | .Results[]? | .Target as $target | .Vulnerabilities[]?
          | {component: $component, target: $target, id: .VulnerabilityID, severity: .Severity,
             package: .PkgName, installed: .InstalledVersion, fixed: .FixedVersion}]' \
    "$EVIDENCE_DIR/$name-backend.trivy.json" "$EVIDENCE_DIR/$name-frontend.trivy.json" >"$out"
}

# A worktree of <commit> at $WORK_DIR/<name>, replacing any left by an earlier run.
worktree() {
  local commit=$1 dir="$WORK_DIR/$2"
  git worktree remove --force "$dir" >/dev/null 2>&1 || rm -rf "$dir"
  git worktree add --quiet --detach "$dir" "$commit" >&2
  echo "$dir"
}

cleanup_worktrees() {
  local name
  for name in base head; do git worktree remove --force "$WORK_DIR/$name" >/dev/null 2>&1 || :; done
}

table() {
  echo "| Component | Severity | Vulnerability | Package | Installed | Fixed in | Found in |"
  echo "| --- | --- | --- | --- | --- | --- | --- |"
  jq -r '.[] | "| \(.component) | \(.severity) | \(.id) | \(.package) | \(.installed) | \(.fixed) | `\(.target)` |"' "$1"
}

gate() {
  local base=${1-} head=${2:?usage: gate <base> <head>}
  is_commit "$head" || { echo "error: $head is not a commit in this clone (fetch-depth: 0?)" >&2; exit 2; }

  if is_commit "$base" && [ -z "$(git diff --name-only "$base" "$head" -- "${MANIFESTS[@]}")" ]; then
    {
      echo "### Dependency vulnerability gate"
      echo
      echo "No package manifest changed between ${base:0:12} and ${head:0:12}, so this change adds no dependency."
      echo "**Passed** without a scan."
    } >"$SUMMARY"
    publish_summary
    return 0
  fi

  trap cleanup_worktrees EXIT
  local exceptions="$EVIDENCE_DIR/vulnerability-exceptions-base.yaml" base_label
  if is_commit "$base"; then
    git show "$base:$EXCEPTIONS" >"$exceptions" 2>/dev/null || echo 'vulnerabilities: []' >"$exceptions"
    findings "$(worktree "$base" base)" "$exceptions" "$GATE_SEVERITIES" "$EVIDENCE_DIR/base.json"
    base_label=${base:0:12}
  else
    # A new ref: nothing to diff against, so every finding is the head's own.
    git show "$head:$EXCEPTIONS" >"$exceptions" 2>/dev/null || echo 'vulnerabilities: []' >"$exceptions"
    echo '[]' >"$EVIDENCE_DIR/base.json"
    base_label="no base commit"
  fi
  findings "$(worktree "$head" head)" "$exceptions" "$GATE_SEVERITIES" "$EVIDENCE_DIR/head.json"

  jq --slurpfile base "$EVIDENCE_DIR/base.json" \
    '[.[] | select([.id, .package] as $key | [$base[0][] | [.id, .package]] | index([$key]) | not)]
     | unique_by([.component, .id, .package, .installed])' \
    "$EVIDENCE_DIR/head.json" >"$EVIDENCE_DIR/new.json"

  local new carried
  new=$(jq length "$EVIDENCE_DIR/new.json")
  carried=$(jq --slurpfile new "$EVIDENCE_DIR/new.json" '[.[] | select(. as $f | $new[0] | index($f) | not)] | length' \
    "$EVIDENCE_DIR/head.json")
  {
    echo "### Dependency vulnerability gate"
    echo
    echo "${head:0:12} against $base_label: fixable $GATE_SEVERITIES findings in the backend NuGet graph and the frontend"
    echo "npm graph (dev dependencies included), exceptions as of the base."
    echo
    if [ "$new" -eq 0 ]; then
      echo "**Passed.** This change adds no fixable $GATE_SEVERITIES vulnerability."
    else
      echo "**Blocked.** This change adds $new fixable $GATE_SEVERITIES finding(s). Upgrade to the fixed version, or"
      echo "record a reviewed, dated exception in \`$EXCEPTIONS\` in a change of its own."
      echo
      table "$EVIDENCE_DIR/new.json"
    fi
    if [ "$carried" -gt 0 ]; then
      echo
      echo "The base already carries $carried fixable $GATE_SEVERITIES finding(s); not this change's, and reported weekly to the owner."
    fi
  } >"$SUMMARY"
  publish_summary
  [ "$new" -eq 0 ]
}

report() {
  [ -f "$EXCEPTIONS" ] || { echo "error: $EXCEPTIONS not found" >&2; exit 2; }
  findings . "$EXCEPTIONS" "$REPORT_SEVERITIES" "$EVIDENCE_DIR/current.json"
  jq 'unique_by([.component, .id, .package, .installed])' "$EVIDENCE_DIR/current.json" >"$EVIDENCE_DIR/report.json"

  local count
  count=$(jq length "$EVIDENCE_DIR/report.json")
  {
    echo "### Dependency vulnerability report"
    echo
    echo "$(git rev-parse --short=12 HEAD) on ${GITHUB_REF_NAME:-$(git branch --show-current)}: fixable $REPORT_SEVERITIES findings,"
    echo "the release gate's line (artifact-build-and-scanning.md D-3), with the exceptions in \`$EXCEPTIONS\`."
    echo
    if [ "$count" -eq 0 ]; then
      echo "**No fixable $REPORT_SEVERITIES finding.**"
    else
      echo "**$count finding(s).** Each blocks the next release until it is upgraded or excepted."
      echo
      table "$EVIDENCE_DIR/report.json"
    fi
  } >"$SUMMARY"
  publish_summary
  [ "$count" -eq 0 ]
}

[ $# -ge 1 ] || usage
command=$1
shift
case "$command" in
  gate | report)
    for tool in git dotnet trivy jq; do need "$tool"; done
    mkdir -p "$EVIDENCE_DIR" "$WORK_DIR"
    "$command" "$@"
    ;;
  *) usage ;;
esac
