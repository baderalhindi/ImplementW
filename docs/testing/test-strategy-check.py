#!/usr/bin/env python3
"""TASK-084: the test strategy, the CI wiring and the coverage configuration agree (repo-checks, every PR).

  TS-1  Every integration test namespace is run by at least one `--filter FullyQualifiedName~...` step of ci-quality-gates.yml,
        and every such filter runs at least one namespace. (Approval, MasterDataConfig and Notifications ran in no step until
        TASK-084, DocumentManagement until TASK-081.)
  TS-2  Every job that runs `dotnet test` uploads a `coverage-<job>` artifact; the `coverage` job needs each of them and runs
        coverage-check.py; the ruleset requires `coverage` and `frontend`.
  TS-3  Directory.Build.props points test projects at coverage.runsettings under CI=true; the run settings instrument exactly the
        assemblies coverage-thresholds.json gates; no source file of those assemblies is marked [ExcludeFromCodeCoverage].
  TS-4  The threshold register in test-strategy.md states the values coverage-thresholds.json and vite.config.ts enforce.
  TS-5  The journey table has UJ-001..UJ-015 once each, an owning e2e file under src/frontend/e2e/ named after the journey and used
        by no other journey, and a status of PENDING or AUTOMATED; an AUTOMATED journey's file exists and names its journey id.
        Every test the table cites as evidence exists.
  TS-6  The Step 18 domain table has the 21 WF/FG domains once each, its scenario counts add up to 4,079 (2,854 business-run,
        1,225 QA-assisted), and every integration namespace and frontend feature it names exists.
  TS-7  The journey suite stays wired as TASK-085 built it (docs/testing/e2e-suite.md): e2e-journeys.yml runs on a schedule and
        can be called; ci-cd-pipeline.yml's e2e-sit calls it against SIT and deploy-uat needs e2e-sit, so a failing journey blocks
        promotion to UAT; Playwright retries nothing; every uj-nnn spec under src/frontend/e2e/journeys owns a row of the table.

Run from the repository root: python3 docs/testing/test-strategy-check.py
"""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent.parent
STRATEGY = HERE / "test-strategy.md"
THRESHOLDS = HERE / "coverage-thresholds.json"
WORKFLOW = ROOT / ".github/workflows/ci-quality-gates.yml"
RULESET = ROOT / ".github/branch-protection/protected-branches.ruleset.json"
BUILD_PROPS = ROOT / "src/backend/Directory.Build.props"
RUNSETTINGS = ROOT / "src/backend/coverage.runsettings"
VITE_CONFIG = ROOT / "src/frontend/vite.config.ts"
BACKEND = ROOT / "src/backend"
INTEGRATION = BACKEND / "PMPlatform.Tests.Integration"
FEATURES = ROOT / "src/frontend/src/features"
E2E = "src/frontend/e2e/"
NAMESPACE_ROOT = "PMPlatform.Tests.Integration."
DOMAINS = [f"WF-{n:02d}" for n in range(1, 16)] + [f"FG-{n:02d}" for n in range(1, 7)]
JOURNEYS = [f"UJ-{n:03d}" for n in range(1, 16)]
SCENARIOS, BUSINESS, QA_ASSISTED = 4079, 2854, 1225
E2E_WORKFLOW = ROOT / ".github/workflows/e2e-journeys.yml"
PIPELINE = ROOT / ".github/workflows/ci-cd-pipeline.yml"
PLAYWRIGHT_CONFIG = ROOT / "src/frontend/e2e/playwright.config.ts"


def cells(line: str) -> list[str]:
    return [part.strip() for part in line.strip().strip("|").split("|")]


def table_rows(text: str, first_cell: str) -> list[list[str]]:
    return [cells(line) for line in text.splitlines() if re.match(rf"^\|\s*{first_cell}\s*\|", line)]


def ticked(cell: str) -> list[str]:
    return re.findall(r"`([^`]+)`", cell)


def jobs(workflow: str) -> dict[str, str]:
    body = workflow.split("\njobs:\n", 1)[1]
    parts = re.split(r"^  ([A-Za-z0-9_-]+):\s*$", body, flags=re.MULTILINE)
    return dict(zip(parts[1::2], parts[2::2]))


def namespaces_with_tests() -> set[str]:
    found = set()
    for source in INTEGRATION.rglob("*.cs"):
        if "/obj/" in source.as_posix() or "/bin/" in source.as_posix():
            continue
        text = source.read_text(encoding="utf-8")
        match = re.search(r"^namespace\s+([\w.]+)\s*;", text, flags=re.MULTILINE)
        if match and re.search(r"\[(Fact|Theory)\b", text):
            found.add(match.group(1))
    return found


def check_filters(workflow: str, failures: list[str]) -> None:
    filters = re.findall(r"--filter\s+FullyQualifiedName~(\S+)", workflow)
    namespaces = namespaces_with_tests()
    for namespace in sorted(namespaces):
        if not any(f in namespace + "." for f in filters):
            failures.append(f"TS-1 {namespace} runs in no CI step: add a `--filter FullyQualifiedName~{namespace}.` step")
    for f in filters:
        if not any(f in namespace + "." for namespace in namespaces):
            failures.append(f"TS-1 the filter FullyQualifiedName~{f} runs no integration namespace")


def check_coverage_jobs(workflow: str, failures: list[str]) -> None:
    all_jobs = jobs(workflow)
    testing = [name for name, body in all_jobs.items() if "dotnet test" in body]
    for name in testing:
        body = all_jobs[name]
        if "actions/upload-artifact" not in body or f"name: coverage-{name}\n" not in body:
            failures.append(f"TS-2 job {name} runs dotnet test but uploads no coverage-{name} artifact")
    gate = all_jobs.get("coverage")
    if gate is None:
        failures.append("TS-2 ci-quality-gates.yml has no `coverage` job")
    else:
        needs = re.search(r"needs:\s*\[([^\]]*)\]", gate)
        needed = {n.strip() for n in needs.group(1).split(",")} if needs else set()
        for name in testing:
            if name not in needed:
                failures.append(f"TS-2 the coverage job does not need {name}")
        if "docs/testing/coverage-check.py" not in gate:
            failures.append("TS-2 the coverage job does not run docs/testing/coverage-check.py")
    ruleset = json.loads(RULESET.read_text(encoding="utf-8"))
    required = {c["context"] for r in ruleset["rules"] if r["type"] == "required_status_checks" for c in r["parameters"]["required_status_checks"]}
    for context in ("coverage", "frontend"):
        if context not in required:
            failures.append(f"TS-2 the ruleset does not require the `{context}` check")


def check_collection(policy: dict, failures: list[str]) -> None:
    props = BUILD_PROPS.read_text(encoding="utf-8")
    if not re.search(r"Condition=\"[^\"]*'\$\(CI\)' == 'true'[^\"]*\">\s*<RunSettingsFilePath>\$\(MSBuildThisFileDirectory\)coverage\.runsettings</RunSettingsFilePath>", props):
        failures.append("TS-3 Directory.Build.props does not set RunSettingsFilePath to coverage.runsettings under CI=true")
    included = set(re.findall(r"<ModulePath>\.\*\[\\\\/\]([\w\\.]+)\\\.dll\$</ModulePath>", RUNSETTINGS.read_text(encoding="utf-8")))
    included = {name.replace("\\.", ".") for name in included}
    if included != set(policy["assemblies"]):
        failures.append(f"TS-3 coverage.runsettings instruments {sorted(included)}, coverage-thresholds.json gates {sorted(policy['assemblies'])}")
    for assembly in policy["assemblies"]:
        for source in (BACKEND / assembly).rglob("*.cs"):
            if "/obj/" not in source.as_posix() and "ExcludeFromCodeCoverage" in source.read_text(encoding="utf-8"):
                failures.append(f"TS-3 {source.relative_to(ROOT)} opts out of coverage with ExcludeFromCodeCoverage")


def check_register(strategy: str, policy: dict, failures: list[str]) -> None:
    vite = VITE_CONFIG.read_text(encoding="utf-8")
    thresholds = re.search(r"thresholds:\s*\{\s*lines:\s*(\d+),\s*branches:\s*(\d+)\s*\}", vite)
    enforced = {
        "backend.minimumPerModule": str(policy["minimumPerModule"]),
        "backend.minimumTotal": str(policy["minimumTotal"]),
        "frontend.lines": thresholds.group(1) if thresholds else None,
        "frontend.branches": thresholds.group(2) if thresholds else None,
    }
    stated = {ticked(row[0])[0]: row[1].rstrip("%") for row in table_rows(strategy, r"`(backend|frontend)\.\w+`")}
    for key, value in enforced.items():
        if value is None:
            failures.append(f"TS-4 vite.config.ts states no coverage threshold for {key}")
        elif stated.get(key) != value:
            failures.append(f"TS-4 test-strategy.md states {key} = {stated.get(key)}, the configuration enforces {value}")


def evidence_exists(reference: str) -> bool:
    if reference.endswith(".test.tsx") or reference.endswith(".test.ts"):
        return (FEATURES / reference).is_file()
    folder, _, cls = reference.partition("/")
    return (INTEGRATION / folder / f"{cls}.cs").is_file()


def check_journeys(strategy: str, failures: list[str]) -> None:
    rows = table_rows(strategy, r"UJ-\d{3}")
    ids = [row[0] for row in rows]
    for journey in JOURNEYS:
        if ids.count(journey) != 1:
            failures.append(f"TS-5 {journey} appears {ids.count(journey)} times in the journey table")
    owners: dict[str, str] = {}
    for row in rows:
        journey, owner, status, evidence = row[0], ticked(row[3]), row[4], row[5]
        if journey not in JOURNEYS:
            failures.append(f"TS-5 {journey} is not one of Step 18's UJ-001..UJ-015")
        if len(owner) != 1 or not owner[0].startswith(E2E) or not owner[0].endswith(".spec.ts") \
                or not Path(owner[0]).name.startswith(journey.lower() + "-"):
            failures.append(f"TS-5 {journey} has no owning e2e file `{E2E}...{journey.lower()}-<name>.spec.ts`")
            continue
        if owner[0] in owners:
            failures.append(f"TS-5 {journey} and {owners[owner[0]]} share the owning file {owner[0]}")
        owners[owner[0]] = journey
        state = status.split()[0] if status else ""
        if state not in ("PENDING", "AUTOMATED"):
            failures.append(f"TS-5 {journey} status `{status}` is neither PENDING nor AUTOMATED")
        if state == "AUTOMATED":
            spec = ROOT / owner[0]
            if not spec.is_file() or journey not in spec.read_text(encoding="utf-8"):
                failures.append(f"TS-5 {journey} is AUTOMATED but {owner[0]} does not exist or does not name {journey}")
        for reference in ticked(evidence):
            if not evidence_exists(reference):
                failures.append(f"TS-5 {journey} cites `{reference}`, which does not exist")


def check_domains(strategy: str, failures: list[str]) -> None:
    rows = table_rows(strategy, r"(WF|FG)-\d{2}")
    ids = [row[0] for row in rows]
    for domain in DOMAINS:
        if ids.count(domain) != 1:
            failures.append(f"TS-6 {domain} appears {ids.count(domain)} times in the Step 18 domain table")
    totals = [0, 0, 0]
    for row in rows:
        try:
            counts = [int(row[i].replace(",", "")) for i in (2, 3, 4)]
        except (IndexError, ValueError):
            failures.append(f"TS-6 {row[0]} has no scenario counts")
            continue
        if counts[0] != counts[1] + counts[2]:
            failures.append(f"TS-6 {row[0]}: {counts[0]} scenarios is not {counts[1]} business-run + {counts[2]} QA-assisted")
        totals = [t + c for t, c in zip(totals, counts)]
        for namespace in ticked(row[5]):
            if not (INTEGRATION / namespace).is_dir():
                failures.append(f"TS-6 {row[0]} names the integration namespace {namespace}, which does not exist")
        for feature in ticked(row[6]):
            if not (FEATURES / feature).is_dir():
                failures.append(f"TS-6 {row[0]} names the frontend feature {feature}, which does not exist")
    if totals != [SCENARIOS, BUSINESS, QA_ASSISTED]:
        failures.append(f"TS-6 the domain table adds up to {totals}, Step 18 has {[SCENARIOS, BUSINESS, QA_ASSISTED]}")


def check_e2e_wiring(strategy: str, failures: list[str]) -> None:
    suite = E2E_WORKFLOW.read_text(encoding="utf-8") if E2E_WORKFLOW.is_file() else ""
    trigger = suite.split("\njobs:\n", 1)[0]
    if not re.search(r"^  schedule:\s*\n\s+- cron:", trigger, flags=re.MULTILINE):
        failures.append("TS-7 e2e-journeys.yml has no schedule: the journeys no longer run nightly")
    if not re.search(r"^  workflow_call:", trigger, flags=re.MULTILINE):
        failures.append("TS-7 e2e-journeys.yml cannot be called, so the pipeline cannot gate on it")
    pipeline = jobs(PIPELINE.read_text(encoding="utf-8"))
    gate = pipeline.get("e2e-sit", "")
    if "uses: ./.github/workflows/e2e-journeys.yml" not in gate or not re.search(r"target:\s*sit\b", gate):
        failures.append("TS-7 ci-cd-pipeline.yml has no e2e-sit job calling e2e-journeys.yml against SIT")
    needs = re.search(r"needs:\s*\[([^\]]*)\]", pipeline.get("deploy-uat", ""))
    if needs is None or "e2e-sit" not in [need.strip() for need in needs.group(1).split(",")]:
        failures.append("TS-7 deploy-uat does not need e2e-sit: a failing journey would not block promotion to UAT")
    config = PLAYWRIGHT_CONFIG.read_text(encoding="utf-8") if PLAYWRIGHT_CONFIG.is_file() else ""
    if not re.search(r"^\s*retries:\s*0,", config, flags=re.MULTILINE):
        failures.append("TS-7 playwright.config.ts must set `retries: 0` (test-strategy.md §10)")
    owned = {owner for row in table_rows(strategy, r"UJ-\d{3}") for owner in ticked(row[3])}
    for spec in sorted((ROOT / E2E / "journeys").glob("uj-*.spec.ts")):
        if spec.relative_to(ROOT).as_posix() not in owned:
            failures.append(f"TS-7 {spec.relative_to(ROOT)} owns no row of the journey table")


def main() -> int:
    strategy = STRATEGY.read_text(encoding="utf-8")
    workflow = WORKFLOW.read_text(encoding="utf-8")
    policy = json.loads(THRESHOLDS.read_text(encoding="utf-8"))
    failures: list[str] = []
    check_filters(workflow, failures)
    check_coverage_jobs(workflow, failures)
    check_collection(policy, failures)
    check_register(strategy, policy, failures)
    check_journeys(strategy, failures)
    check_domains(strategy, failures)
    check_e2e_wiring(strategy, failures)
    for failure in failures:
        print(failure)
    print(f"test strategy: {len(failures)} finding(s)")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
