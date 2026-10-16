#!/usr/bin/env bash
# Secret scanning gate (TASK-080, CTL-42). Record: docs/architecture/secret-and-dependency-scanning.md
#
#   secret-scan.sh install               install the pinned gitleaks, checksum-verified, onto the job's PATH
#   secret-scan.sh range <base> <head>   scan every commit <head> adds over <base>: a pull request or a push
#   secret-scan.sh history               scan every commit on every ref: the scheduled full-history scan
#
# Exit 0: nothing found. Exit 1: a finding. Exit 2: the scan could not run, which is never a pass.
#
# Three rules hold for every scan, so a change cannot switch its own gate off:
#   - `range` reads .gitleaks.toml and .gitleaksignore from <base>, never from the change under scan.
#     An allowlist for a false positive is a reviewed change of its own (CODEOWNERS: Security Lead),
#     merged before the change that needs it.
#   - Inline `gitleaks:allow` comments are ignored. A false positive is answered in .gitleaks.toml,
#     where a reviewer reads it, not on the line that holds it.
#   - Every value is redacted, in the log, in the report and in the job summary.
#
# Writes the JSON report and a Markdown summary to $EVIDENCE_DIR (default artifacts/security/secret-scan).
set -euo pipefail

GITLEAKS_VERSION=8.30.1
# gitleaks_8.30.1_checksums.txt, from the release.
GITLEAKS_SHA256=551f6fc83ea457d62a0d98237cbad105af8d557003051f41f3e7ca7b3f2470eb

EVIDENCE_DIR=${EVIDENCE_DIR:-artifacts/security/secret-scan}
REPORT="$EVIDENCE_DIR/gitleaks.json"
SUMMARY="$EVIDENCE_DIR/summary.md"
NO_COMMIT=0000000000000000000000000000000000000000

usage() {
  echo "usage: $0 {install | range <base> <head> | history}" >&2
  exit 2
}

append_summary() {
  if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then cat "$SUMMARY" >>"$GITHUB_STEP_SUMMARY"; fi
}

install() {
  local dir=${GITLEAKS_DIR:-${RUNNER_TEMP:?RUNNER_TEMP or GITLEAKS_DIR must be set}/gitleaks}
  local archive="$dir/gitleaks.tar.gz"
  mkdir -p "$dir"
  curl -fsSL --retry 3 -o "$archive" \
    "https://github.com/gitleaks/gitleaks/releases/download/v$GITLEAKS_VERSION/gitleaks_${GITLEAKS_VERSION}_linux_x64.tar.gz"
  echo "$GITLEAKS_SHA256  $archive" | sha256sum --check --strict
  tar -xzf "$archive" -C "$dir" gitleaks
  [ -z "${GITHUB_PATH:-}" ] || echo "$dir" >>"$GITHUB_PATH"
  "$dir/gitleaks" version
}

# A result is only evidence if it came from the pinned version.
require_gitleaks() {
  command -v gitleaks >/dev/null 2>&1 || { echo "error: gitleaks is not on PATH; run '$0 install'" >&2; exit 2; }
  local version
  version=$(gitleaks version)
  [ "$version" = "$GITLEAKS_VERSION" ] || [ "$version" = "v$GITLEAKS_VERSION" ] ||
    { echo "error: gitleaks $version is on PATH; this gate is pinned to $GITLEAKS_VERSION" >&2; exit 2; }
}

is_commit() {
  [ -n "$1" ] && [ "$1" != "$NO_COMMIT" ] && git cat-file -e "$1^{commit}" 2>/dev/null
}

# scan <title> <scope> <config> <ignore-file> <log-opts>
scan() {
  local title=$1 scope=$2 config=$3 ignore=$4 log_opts=$5 status=0
  gitleaks git . --config "$config" --gitleaks-ignore-path "$ignore" --ignore-gitleaks-allow \
    --log-opts="$log_opts" --redact --no-banner --exit-code 1 \
    --report-format json --report-path "$REPORT" || status=$?
  [ "$status" -le 1 ] || { echo "error: gitleaks exited $status; the scan did not complete" >&2; exit 2; }
  [ -s "$REPORT" ] || { echo "error: gitleaks wrote no report" >&2; exit 2; }

  local count
  count=$(jq 'length' "$REPORT")
  {
    echo "### $title"
    echo
    echo "Scope: $scope. gitleaks $GITLEAKS_VERSION, values redacted."
    echo
    if [ "$count" -eq 0 ]; then
      echo "**No secret found.**"
    else
      echo "**$count finding(s). Blocked.**"
      echo
      echo "| Rule | File | Line | Commit |"
      echo "| --- | --- | --- | --- |"
      jq -r '.[] | "| \(.RuleID) | `\(.File)` | \(.StartLine) | \(.Commit[0:12]) |"' "$REPORT"
      echo
      echo "Treat each value as exposed: the commit is already on GitHub. Rotate it"
      echo "(\`infra/secrets/secret-rotation-runbook.md\`), then rewrite the branch so no commit holds it and"
      echo "force-push. A later commit that deletes the value does not clear this gate. A finding that is"
      echo "not a secret is answered by an allowlist in \`.gitleaks.toml\`, merged first in a change of its own."
    fi
  } >"$SUMMARY"
  cat "$SUMMARY"
  append_summary
  [ "$count" -eq 0 ]
}

range() {
  local base=${1-} head=${2:?usage: range <base> <head>}
  is_commit "$head" || { echo "error: $head is not a commit in this clone (fetch-depth: 0?)" >&2; exit 2; }

  local config="$EVIDENCE_DIR/gitleaks-base.toml" ignore="$EVIDENCE_DIR/gitleaksignore-base"
  : >"$ignore"
  if is_commit "$base"; then
    # The base's rules, not the change's. A base without a config gets the default rule set; never
    # the change's own file, which gitleaks would otherwise pick up from the working tree.
    git show "$base:.gitleaks.toml" >"$config" 2>/dev/null || printf '[extend]\nuseDefault = true\n' >"$config"
    git show "$base:.gitleaksignore" >"$ignore" 2>/dev/null || :
    scan "Secret scan" "the $(git rev-list --no-merges --count "$base..$head") commit(s) ${head:0:12} adds over ${base:0:12}" \
      "$config" "$ignore" "$base..$head"
  else
    # A new ref, or a force-push whose old head is gone: there is no base to diff against, so every
    # commit the head can reach is in scope, under the head's own reviewed configuration.
    git show "$head:.gitleaks.toml" >"$config" 2>/dev/null || printf '[extend]\nuseDefault = true\n' >"$config"
    scan "Secret scan" "every commit reachable from ${head:0:12} (no base commit to diff against)" \
      "$config" "$ignore" "$head"
  fi
}

history() {
  local config=.gitleaks.toml ignore="$EVIDENCE_DIR/gitleaksignore-head"
  [ -f "$config" ] || { echo "error: $config not found" >&2; exit 2; }
  if [ -f .gitleaksignore ]; then cp .gitleaksignore "$ignore"; else : >"$ignore"; fi
  scan "Full-history secret scan" "all $(git rev-list --all --no-merges --count) commits on every ref (merge commits add no content of their own)" "$config" "$ignore" "--all"
}

[ $# -ge 1 ] || usage
command=$1
shift
case "$command" in
  install) install ;;
  range | history)
    require_gitleaks
    command -v jq >/dev/null 2>&1 || { echo "error: jq is required and is not on PATH" >&2; exit 2; }
    mkdir -p "$EVIDENCE_DIR"
    "$command" "$@"
    ;;
  *) usage ;;
esac
