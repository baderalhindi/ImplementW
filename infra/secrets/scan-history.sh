#!/usr/bin/env sh
# Scans the repository for committed secrets (TASK-019 acceptance criterion): the full git history and
# then the working tree, which catches an uncommitted file a history scan cannot see.
#
#   infra/secrets/scan-history.sh                 # both scans, report to stdout
#   infra/secrets/scan-history.sh --report out/   # also write the JSON findings there
#
# This is the evidence command for TASK-019, not the gate. The gate — scanning per pull request, on a
# schedule, and as a required branch-protection check — is TASK-080 / CTL-42, which owns the scanner
# configuration and the baseline. Nothing here is wired into CI on purpose.
#
# Requires: gitleaks (https://github.com/gitleaks/gitleaks), git.
set -eu

report_dir=''
while [ $# -gt 0 ]; do
  case $1 in
    --report) report_dir=${2:-} && shift 2 || exit 1 ;;
    *) echo "usage: $(basename "$0") [--report <directory>]" >&2 && exit 1 ;;
  esac
done

repository=$(git rev-parse --show-toplevel)
command -v gitleaks >/dev/null 2>&1 || {
  echo "error: gitleaks is required and is not on PATH" >&2
  exit 1
}

status=0
run_scan() {
  label=$1
  shift
  echo "== $label =="
  if [ -n "$report_dir" ]; then
    mkdir -p "$report_dir"
    set -- "$@" --report-format json --report-path "$report_dir/$label.json"
  fi
  gitleaks "$@" || status=1
}

run_scan history git "$repository" --log-opts='--all --full-history'
run_scan worktree dir "$repository"

[ "$status" = "0" ] && echo "No secret found in the history or the working tree."
exit "$status"
