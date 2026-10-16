#!/usr/bin/env python3
"""Static checks on the secret-scanning and dependency-vulnerability gates (TASK-080, CTL-42).

A gate is only a gate while three things stay true: it runs on every pull request to a protected
branch, branch protection requires it, and it cannot be switched off from inside the change it
judges. Each of those is one quiet edit away from false — a `paths:` filter, a renamed job, a scan
that reads the change's own allowlist, a schedule that drifts to monthly — and none of them turns a
run red. They are caught here, in `repo-checks`, with the standard library only.

    python3 docs/architecture/secret-and-dependency-scanning-check.py
"""
import importlib.util
import re
import sys
import tomllib
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SECRET_WORKFLOW = ROOT / ".github/workflows/secret-scan.yml"
DEPENDENCY_WORKFLOW = ROOT / ".github/workflows/dependency-vuln-gate.yml"
PIPELINE = ROOT / ".github/workflows/ci-cd-pipeline.yml"
SECRET_SCRIPT = ROOT / ".github/scripts/secret-scan.sh"
DEPENDENCY_SCRIPT = ROOT / ".github/scripts/dependency-vuln-gate.sh"
REPORT_SCRIPT = ROOT / ".github/scripts/report-to-owner.sh"
DRILL_SCRIPT = ROOT / ".github/scripts/verify-scanning-gates.sh"
OWNER = ROOT / ".github/security/scan-owner.yaml"
EXCEPTIONS = ROOT / ".github/security/vulnerability-exceptions.yaml"
SCANNER_CONFIG = ROOT / ".gitleaks.toml"
RULESET = ROOT / ".github/branch-protection/protected-branches.ruleset.json"
CODEOWNERS = ROOT / ".github/CODEOWNERS"

# workflow -> (the required per-change job, the scheduled job that reports to the owner)
GATES = {
    SECRET_WORKFLOW: ("secret-scan", "secret-scan-history"),
    DEPENDENCY_WORKFLOW: ("dependency-vuln-gate", "dependency-vuln-report"),
}
SCRIPTS = [SECRET_SCRIPT, DEPENDENCY_SCRIPT, REPORT_SCRIPT, DRILL_SCRIPT]
# Jobs that read commits other than the one checked out: the base, every commit of a change, every ref.
FULL_CLONE_JOBS = {"secret-scan", "secret-scan-history", "dependency-vuln-gate"}

findings = []


def fail(check, message):
    findings.append(f"{check}: {message}")


def _load_pipeline_check():
    """The YAML-subset reader of cicd-pipeline-check.py, reused rather than copied. No bytecode is
    written: the check runs from a clean checkout and must leave it clean."""
    sys.dont_write_bytecode = True
    spec = importlib.util.spec_from_file_location("cicd_pipeline_check", ROOT / "docs/architecture/cicd-pipeline-check.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


pipeline_check = _load_pipeline_check()
load_yaml, load_json, as_list = pipeline_check.load_yaml, pipeline_check.load_json, pipeline_check.as_list


def rel(path):
    return path.relative_to(ROOT)


def protected_branches(ruleset):
    return [ref.removeprefix("refs/heads/") for ref in ruleset["conditions"]["ref_name"]["include"]]


def required_checks(ruleset):
    for rule in ruleset["rules"]:
        if rule["type"] == "required_status_checks":
            return {check["context"] for check in rule["parameters"]["required_status_checks"]}
    return set()


def check_deliverables():
    """SD-1 — the files the gates are made of exist, and the scripts are executable."""
    for path in [*GATES, *SCRIPTS, OWNER, EXCEPTIONS, SCANNER_CONFIG, RULESET]:
        if not path.exists():
            fail("SD-1", f"{rel(path)} does not exist")
    for script in SCRIPTS:
        if script.exists() and not script.stat().st_mode & 0o111:
            fail("SD-1", f"{rel(script)} is not executable")


def check_triggers(name, workflow, branches):
    """SD-2 — every pull request to a protected branch is judged, and the full scan runs at least weekly."""
    triggers = workflow.get("on") or {}
    pull_request = triggers.get("pull_request")
    if not isinstance(pull_request, dict):
        fail("SD-2", f"{name} does not run on pull requests")
        return
    missing = sorted(set(branches) - set(as_list(pull_request.get("branches"))))
    if missing:
        fail("SD-2", f"{name} does not run on pull requests to {', '.join(missing)}")
    # A required check that a filter skips never reports, and the pull request waits on it forever;
    # one that a filter lets pass is a hole. Either way the gate must see every change.
    for event in ("pull_request", "push"):
        for key in ("paths", "paths-ignore", "branches-ignore"):
            if isinstance(triggers.get(event), dict) and key in triggers[event]:
                fail("SD-2", f"{name} filters its {event} trigger with '{key}'")

    crons = [entry.get("cron", "") for entry in as_list(triggers.get("schedule")) if isinstance(entry, dict)]
    # Five fields; any day of the week, any day of the month and any month means at least once a week.
    weekly = [cron for cron in crons if re.fullmatch(r"\S+ \S+ \* \* \S+", cron.strip())]
    if not weekly:
        fail("SD-2", f"{name} has no schedule that runs at least weekly (crons: {crons or 'none'})")
    if "workflow_dispatch" not in triggers:
        fail("SD-2", f"{name} cannot be run on demand (no workflow_dispatch)")


def check_required(name, workflow, gate_job, report_job, checks):
    """SD-3 — the per-change job is a required status check, and runs where it is required."""
    jobs = workflow.get("jobs") or {}
    job = jobs.get(gate_job)
    if not isinstance(job, dict):
        fail("SD-3", f"{name} has no '{gate_job}' job")
        return
    # The check's context is the job's name; renaming it leaves the ruleset requiring a check nobody reports.
    if job.get("name") != gate_job:
        fail("SD-3", f"{name}: job '{gate_job}' reports as {job.get('name')!r}, not '{gate_job}'")
    if gate_job not in checks:
        fail("SD-3", f"'{gate_job}' is not a required status check in {rel(RULESET)}")
    condition = str(job.get("if", ""))
    if "pull_request" not in condition:
        fail("SD-3", f"{name}: '{gate_job}' does not run on pull requests (if: {condition!r})")
    if report_job in checks:
        fail("SD-3", f"'{report_job}' is required, but it only runs on a schedule, so every pull request would wait on it")
    for job_name in FULL_CLONE_JOBS & {gate_job, report_job}:
        steps = as_list((jobs.get(job_name) or {}).get("steps"))
        checkout = next((step for step in steps if str(step.get("uses", "")).startswith("actions/checkout@")), None)
        if not checkout or str((checkout.get("with") or {}).get("fetch-depth")) != "0":
            fail("SD-3", f"{name}: '{job_name}' checks out a shallow clone; the base and every commit must be in it")


def check_secret_scan():
    """SD-4 — the secret scan is pinned, redacted, and cannot be allowlisted from inside a change."""
    text = SECRET_SCRIPT.read_text(encoding="utf-8")
    for needle, why in [
        ("--redact", "values would be printed in the log, the report and the job summary"),
        ("--ignore-gitleaks-allow", "an inline gitleaks:allow comment would silence a finding on its own line"),
        ('git show "$base:.gitleaks.toml"', "a pull request would be scanned with its own, possibly widened, configuration"),
        ('git show "$base:.gitleaksignore"', "a pull request could add its own finding to .gitleaksignore"),
        ("--gitleaks-ignore-path", "gitleaks would read .gitleaksignore from the change's working tree"),
        ("sha256sum --check", "the scanner binary is not verified against the release checksum"),
    ]:
        if needle not in text:
            fail("SD-4", f"secret-scan.sh lacks `{needle}`: {why}")
    if not re.search(r"^GITLEAKS_VERSION=\d+\.\d+\.\d+$", text, re.M):
        fail("SD-4", "secret-scan.sh does not pin an exact GITLEAKS_VERSION")
    if not re.search(r"^GITLEAKS_SHA256=[0-9a-f]{64}$", text, re.M):
        fail("SD-4", "secret-scan.sh does not pin GITLEAKS_SHA256")

    # The header of .gitleaks.toml promises that nothing is suppressed globally.
    config = tomllib.loads(SCANNER_CONFIG.read_text(encoding="utf-8"))
    if not (config.get("extend") or {}).get("useDefault"):
        fail("SD-4", ".gitleaks.toml no longer extends gitleaks' default rule set")
    for key in ("allowlist", "rules"):
        if key in config:
            fail("SD-4", f".gitleaks.toml has a top-level [{key}]; suppress a finding with a scoped [[allowlists]] entry")
    for index, allowlist in enumerate(config.get("allowlists", []), 1):
        label = allowlist.get("description") or f"allowlist {index}"
        for field in ("description", "targetRules", "paths", "regexes"):
            if not allowlist.get(field):
                fail("SD-4", f".gitleaks.toml {label!r} has no {field}: an allowlist names the rule, the paths and the value's shape")
        if allowlist.get("paths") and allowlist.get("regexes") and allowlist.get("condition") != "AND":
            fail("SD-4", f".gitleaks.toml {label!r} does not set condition = \"AND\", so either its paths or its regexes alone suppress")


def check_dependency_gate(pipeline):
    """SD-5 — the dependency gate blocks fixable CRITICAL findings, dev dependencies included, on the base's exceptions."""
    text = DEPENDENCY_SCRIPT.read_text(encoding="utf-8")
    severities = re.search(r"^GATE_SEVERITIES=(\S+)$", text, re.M)
    if not severities or "CRITICAL" not in severities.group(1).split(","):
        fail("SD-5", "dependency-vuln-gate.sh does not gate on CRITICAL (TASK-080 acceptance criterion)")
    for needle, why in [
        ("--ignore-unfixed", "the gate would block on findings no upgrade can fix"),
        ("--include-dev-deps", "frontend dev dependencies, which run on the runner, would not be scanned"),
        ('git show "$base:$EXCEPTIONS"', "a pull request could except its own finding"),
        ("RestorePackagesWithLockFile=true", "the backend's transitive graph would not be scanned"),
    ]:
        if needle not in text:
            fail("SD-5", f"dependency-vuln-gate.sh lacks `{needle}`: {why}")
    if not re.search(r"^EXCEPTIONS=\.github/security/vulnerability-exceptions\.yaml$", text, re.M):
        fail("SD-5", "dependency-vuln-gate.sh does not read the release gate's exceptions file")

    # One scanner version across the pull request and the release, so the two cannot disagree on a finding.
    def trivy_versions(workflow):
        return {
            str((step.get("with") or {}).get("version"))
            for job in (workflow.get("jobs") or {}).values() if isinstance(job, dict)
            for step in as_list(job.get("steps"))
            if str(step.get("uses", "")).startswith("aquasecurity/setup-trivy@")
        }
    release, gate = trivy_versions(pipeline), trivy_versions(load_yaml(DEPENDENCY_WORKFLOW))
    if not gate or gate != release:
        fail("SD-5", f"dependency-vuln-gate.yml installs trivy {sorted(gate)}; the release pipeline uses {sorted(release)}")


def check_owner_reporting(name, workflow, report_job):
    """SD-6 — every scheduled run reports to the named owner, found or not, and only that job may write."""
    owner = re.search(r"^github:\s*([A-Za-z0-9](?:[A-Za-z0-9-]{0,37}[A-Za-z0-9])?)\s*$", OWNER.read_text(encoding="utf-8"), re.M)
    if not owner:
        fail("SD-6", f"{rel(OWNER)} names no GitHub account")
    jobs = workflow.get("jobs") or {}
    job = jobs.get(report_job) or {}
    condition = str(job.get("if", ""))
    if "schedule" not in condition:
        fail("SD-6", f"{name}: '{report_job}' does not run on the schedule")
    report_steps = [step for step in as_list(job.get("steps")) if "report-to-owner.sh" in str(step.get("run", ""))]
    if not report_steps:
        fail("SD-6", f"{name}: '{report_job}' does not report to the owner")
    for step in report_steps:
        if "!cancelled()" not in str(step.get("if", "")):
            fail("SD-6", f"{name}: the owner is told only when the scan passes; the report step needs `if: ${{{{ !cancelled() }}}}`")
    if (job.get("permissions") or {}).get("issues") != "write":
        fail("SD-6", f"{name}: '{report_job}' cannot write the owner's issue (permissions: issues: write)")

    permissions = workflow.get("permissions")
    if permissions != {"contents": "read"}:
        fail("SD-6", f"{name}: the workflow's default permissions are {permissions!r}, not contents: read")
    for job_name, other in jobs.items():
        if job_name != report_job and isinstance(other, dict):
            granted = [key for key, value in (other.get("permissions") or {}).items() if value == "write"]
            if granted:
                fail("SD-6", f"{name}: '{job_name}' grants write on {', '.join(granted)}; it judges untrusted changes")


def check_codeowners():
    """SD-7 — a change that could weaken a gate is reviewed by the Security Lead."""
    owners = {}
    for line in CODEOWNERS.read_text(encoding="utf-8").splitlines():
        parts = line.split("#", 1)[0].split()
        if len(parts) >= 2:
            owners[parts[0]] = parts[1:]

    def owner_of(path):
        # The last matching pattern wins; only the literal and directory forms this file uses.
        match = None
        for pattern, teams in owners.items():
            if pattern == "*" or pattern == path or (pattern.endswith("/") and path.startswith(pattern)):
                match = teams
        return match or []

    for path in [SCANNER_CONFIG, *GATES, *SCRIPTS, OWNER, EXCEPTIONS, RULESET]:
        key = "/" + str(rel(path))
        if not any(team.endswith("/security") for team in owner_of(key)):
            fail("SD-7", f"{key} is not owned by the security team in CODEOWNERS")


def main():
    check_deliverables()
    if findings:
        report()
    try:
        workflows = {path: load_yaml(path) for path in GATES}
        pipeline = load_yaml(PIPELINE)
    except pipeline_check.YamlError as exc:
        print(f"cannot read the workflow files: {exc}", file=sys.stderr)
        return 1
    ruleset = load_json(RULESET)
    branches, checks = protected_branches(ruleset), required_checks(ruleset)

    for path, (gate_job, report_job) in GATES.items():
        name = path.name
        check_triggers(name, workflows[path], branches)
        check_required(name, workflows[path], gate_job, report_job, checks)
        check_owner_reporting(name, workflows[path], report_job)
    check_secret_scan()
    check_dependency_gate(pipeline)
    check_codeowners()
    report()


def report():
    if findings:
        print(f"{len(findings)} finding(s):", file=sys.stderr)
        for finding in findings:
            print("  " + finding, file=sys.stderr)
        sys.exit(1)
    print("OK: secret-scan and dependency-vuln-gate run on every pull request to the protected branches, are required "
          "checks, read their allowlists from the base, and report weekly full scans to the named owner.")
    sys.exit(0)


if __name__ == "__main__":
    main()
