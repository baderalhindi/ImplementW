#!/usr/bin/env sh
# The TASK-017 validation cell, executed rather than described.
#
#   infra/terraform/verify-idempotency.sh <dev|sit|uat|prod>
#
# Four checks, in the order the workbook states them:
#
#   V-1  terraform validate
#   V-2  a static security scan with zero HIGH/CRITICAL findings
#   V-3  terraform plan on the current state produces no diff        (acceptance criterion 2)
#   V-4  terraform plan twice in a row, with no manual change between, produces the same plan
#
# V-1 and V-2 run against the configuration and need nothing else; CI runs them on every pull
# request. V-3 and V-4 need a state file, which means an environment must have been applied. They
# **exit non-zero when they cannot run**: an unexecuted check is not a passed check, and the
# acceptance criterion is not met by a script that would have proved it.
#
# Requires: terraform (the version in .terraform-version), python3, and trivy for V-2.
# Record: docs/architecture/infrastructure-as-code.md
set -eu

here=$(cd "$(dirname "$0")" && pwd)
environment=${1:-}
root="$here/environments/$environment"

die() {
  echo "error: $*" >&2
  exit 1
}

[ -n "$environment" ] || die "usage: $(basename "$0") <dev|sit|uat|prod>"
[ -d "$root" ] || die "unknown environment '$environment'; $here/environments holds: $(ls "$here/environments" | tr '\n' ' ')"

for tool in terraform python3; do
  command -v "$tool" >/dev/null 2>&1 || die "$tool is required and is not on PATH"
done

pinned=$(cat "$here/.terraform-version")
running=$(terraform version -json | python3 -c 'import json,sys; print(json.load(sys.stdin)["terraform_version"])')
[ "$running" = "$pinned" ] || die "terraform $running is on PATH; this configuration is pinned to $pinned (.terraform-version) and the committed lock files were produced with it"

[ -n "${TF_VAR_container_image:-}" ] || die "TF_VAR_container_image is unset. The image is supplied at apply time by the promotion pipeline (TASK-018); a plan cannot be compared against a state that was applied with a different one."

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
failures=0

record() {
  # record <id> <PASS|FAIL> <message>
  printf '%-5s %-4s %s\n' "$1" "$2" "$3"
  [ "$2" = "PASS" ] || failures=$((failures + 1))
}

echo "== $environment =="
terraform -chdir="$root" init -input=false -lockfile=readonly >/dev/null

# V-1 ---------------------------------------------------------------------------------------
if terraform -chdir="$root" validate >/dev/null 2>&1; then
  record V-1 PASS "terraform validate"
else
  terraform -chdir="$root" validate || true
  record V-1 FAIL "terraform validate"
fi

# V-2 ---------------------------------------------------------------------------------------
# tfsec, which the workbook names as an example, was retired into Trivy and its rule for this
# check still looks for a provider argument that no longer exists (infrastructure-as-code.md F-11).
# Trivy carries the same rule set forward and is what CI runs.
if command -v trivy >/dev/null 2>&1; then
  if trivy config --severity HIGH,CRITICAL --exit-code 1 --tf-vars "$root/terraform.tfvars" "$root" >"$work/scan.txt" 2>&1; then
    record V-2 PASS "static security scan, zero HIGH/CRITICAL"
  else
    cat "$work/scan.txt"
    record V-2 FAIL "static security scan reported HIGH or CRITICAL findings"
  fi
else
  record V-2 FAIL "trivy is not on PATH, so the scan did not run"
fi

# V-3 and V-4 -------------------------------------------------------------------------------
if [ "$(terraform -chdir="$root" state list 2>/dev/null | wc -l | tr -d ' ')" = "0" ]; then
  record V-3 FAIL "no state: $environment has not been applied, so there is no current state to plan against"
  record V-4 FAIL "no state: see V-3"
else
  # -detailed-exitcode: 0 no changes, 1 error, 2 changes present. "No unexpected diff" is 0.
  set +e
  terraform -chdir="$root" plan -input=false -detailed-exitcode -out="$work/plan1.bin" >"$work/plan1.txt" 2>&1
  first=$?
  set -e
  case "$first" in
    0) record V-3 PASS "plan on the current state reports no changes" ;;
    2) sed -n '/Terraform will perform/,$p' "$work/plan1.txt" | head -40
       record V-3 FAIL "plan on the current state reports changes — either a console change was made, or the configuration has drifted from what was applied" ;;
    *) cat "$work/plan1.txt"; record V-3 FAIL "plan failed" ;;
  esac

  if [ "$first" = "0" ] || [ "$first" = "2" ]; then
    terraform -chdir="$root" plan -input=false -out="$work/plan2.bin" >"$work/plan2.txt" 2>&1
    terraform -chdir="$root" show -json "$work/plan1.bin" >"$work/plan1.json"
    terraform -chdir="$root" show -json "$work/plan2.bin" >"$work/plan2.json"
    if python3 "$here/compare-plans.py" "$work/plan1.json" "$work/plan2.json"; then
      record V-4 PASS "two consecutive plans describe the same changes"
    else
      record V-4 FAIL "two consecutive plans differ"
    fi
  fi
fi

echo
if [ "$failures" -gt 0 ]; then
  echo "$failures check(s) failed."
  exit 1
fi
echo "All four checks passed for $environment."
