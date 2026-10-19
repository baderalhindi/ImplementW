#!/usr/bin/env python3
"""TASK-088: the UAT scenario register, execution log, defect tracker and sign-off record, held together.

  python3 docs/testing/uat/uat-catalogue.py --register <Project Files.zip> <implementation plan .xlsx>
      Rebuilds scenario-register.csv from the Step 18 UAT Catalogue (UAT_Scenarios, UAT_Journeys) and checks the two
      constants below against their sources: JOURNEY_PACKAGES against Step 18's UAT_Journeys sheet, P0_TASKS against the
      workbook's Implementation Plan (each task Priority P0, its name or phase naming the domain, and no P0 task of a
      WF/FG phase left unmapped). Neither source is in the repository (both are AHDA's controlled files).
  python3 docs/testing/uat/uat-catalogue.py --check
      repo-checks, every PR:
        UR-1  the register has every Step 18 scenario once (4,079), each with the journeys of its package and the P0 tasks
              of its domain as the constants say, and cycle 1 exactly when it is tied to a journey;
        UR-2  every ADR-013 case is complete and names only real domains and personas;
        UL-1  every execution-log row is complete: a known run type and result, a case in scope, an ISO date, a tester and a
              build; a FAIL or BLOCKED names a defect that exists; a UAT run is by a named AHDA tester;
        UD-1  every defect is complete; its "expected to block" selector parses and matches at least one case; a Closed
              defect names its fix (PR #n) and a later UAT run that passed its retest; a Deferred one is Medium or Low and
              names AHDA's acceptance; Critical and High are never Deferred;
        US-1  the sign-off record names no acceptance while the gate is red (no signature ahead of the evidence);
        UT-1  the status table in uat-execution.md is this script's --status.
  python3 docs/testing/uat/uat-catalogue.py --gate
      The Release Checklist's "UAT Catalogue Executed & Signed Off": --check, and
        G-1   every case in scope has a latest UAT run with a recorded outcome (PASS, FAIL or NOT APPLICABLE; not BLOCKED);
        G-2   a latest FAIL is only on Medium/Low defects AHDA deferred; a NOT APPLICABLE names its acceptance reviewer;
        G-3   no Critical or High defect is anything but Closed (fixed and retested) or Rejected;
        G-4   the sign-off record holds AHDA's acceptance from every required signatory, dated, with the signed copy's reference.
  python3 docs/testing/uat/uat-catalogue.py --status
      Prints the status table (per domain: in scope, executed, results, at risk) and the open defects by severity.

Standard library only. Exit 1 on any finding.
"""

from __future__ import annotations

import csv
import io
import re
import sys
import zipfile
import xml.etree.ElementTree as ET
from collections import Counter
from dataclasses import dataclass
from pathlib import Path

HERE = Path(__file__).resolve().parent
REGISTER = HERE / "scenario-register.csv"
ADR013_CASES = HERE / "adr-013-cases.csv"
LOG = HERE / "execution-log.csv"
TRACKER = HERE / "defect-tracker.csv"
SIGN_OFF = HERE / "uat-sign-off.md"
RECORD = HERE / "uat-execution.md"
STATUS_BEGIN, STATUS_END = "<!-- uat-status:begin -->", "<!-- uat-status:end -->"

SCENARIOS = 4079
DOMAINS = [f"WF-{n:02d}" for n in range(1, 16)] + [f"FG-{n:02d}" for n in range(1, 7)]

# Step 18 UAT_Journeys: the packages each cross-domain journey exercises. A scenario is tied to a journey when its package is
# one of the journey's (Step 18 assigns every scenario one package).
JOURNEY_PACKAGES = {
    "UJ-001": ["BPK-014"],
    "UJ-002": ["BPK-022"],
    "UJ-003": ["BPK-015", "BPK-017"],
    "UJ-004": ["BPK-011", "BPK-020"],
    "UJ-005": ["BPK-023"],
    "UJ-006": ["BPK-010"],
    "UJ-007": ["BPK-012"],
    "UJ-008": ["BPK-026", "BPK-027"],
    "UJ-009": ["BPK-026", "BPK-027"],
    "UJ-010": ["BPK-026", "BPK-027"],
    "UJ-011": ["BPK-006"],
    "UJ-012": ["BPK-007"],
    "UJ-013": ["BPK-009"],
    "UJ-014": ["BPK-008", "BPK-028"],
    "UJ-015": ["BPK-027"],
}

# The workbook's P0 tasks that build each domain. A scenario is tied to the P0 tasks of its domain.
P0_TASKS = {
    "WF-01": ["TASK-041", "TASK-042"],
    "WF-02": ["TASK-044", "TASK-045"],
    "WF-03": ["TASK-045", "TASK-046"],
    "WF-04": ["TASK-048"],
    "WF-05": ["TASK-050"],
    "WF-06": ["TASK-055"],
    "WF-07": ["TASK-057"],
    "WF-08": ["TASK-060"],
    "WF-09": ["TASK-062"],
    "WF-10": ["TASK-063"],
    "WF-11": ["TASK-035", "TASK-036"],
    "WF-12": ["TASK-037", "TASK-038"],
    "WF-13": ["TASK-066"],
    "WF-14": ["TASK-052"],
    "WF-15": ["TASK-039"],
    "FG-01": ["TASK-069", "TASK-070"],
    "FG-02": ["TASK-071"],
    "FG-03": ["TASK-028", "TASK-029", "TASK-030", "TASK-033"],
    "FG-04": ["TASK-034"],
    "FG-05": ["TASK-075"],
    "FG-06": ["TASK-073"],
}

# The personas of the UAT data set (uat-execution.md §5): the e2e people on the rehearsal stack, AHDA's named testers on UAT.
PERSONAS = {
    "administrator", "portfolioManager", "departmentManager", "otherDepartmentManager", "projectManager", "executive",
    "viewer", "entityProjectManager", "entityContributor", "otherEntityProjectManager",
}

REGISTER_FIELDS = [
    "Scenario ID", "Story ID", "RTM ID", "Domain", "Package", "Wave", "Source actor", "Execution mode",
    "Business capability", "Evidence class", "Journeys", "P0 tasks", "Cycle",
]
CASE_FIELDS = [
    "Case ID", "Title", "Domains", "Personas", "Procedure", "Expected outcome", "Negative variant", "Automated evidence",
]
LOG_FIELDS = [
    "Run ID", "Run type", "Scenario / Journey ID", "Case revision", "Build / environment", "Tester", "Execution date",
    "Fixture IDs", "Actual outcome", "Result", "Defect / retest reference", "Evidence link", "Acceptance reviewer",
]
TRACKER_FIELDS = [
    "Defect ID", "Title", "Severity", "Domain", "Found in", "Source finding", "Expected to block", "Status", "Fix",
    "Retest run", "Owner", "Acceptance reference", "Notes",
]
RUN_TYPES = {"UAT", "REHEARSAL"}
RESULTS = {"PASS", "FAIL", "BLOCKED", "NOT APPLICABLE"}
SEVERITIES = ["Low", "Medium", "High", "Critical"]
STATUSES = {"Open", "Fixed - awaiting retest", "Closed", "Deferred - accepted", "Rejected"}
DECISIONS = {"Pending", "Accepted", "Accepted with deferrals", "Rejected"}
REQUIRED_SIGNATORIES = ["AHDA Business Sponsor", "QA Lead"]
NONE = {"", "—", "-"}
DATE = re.compile(r"\d{4}-\d{2}-\d{2}$")


@dataclass(frozen=True)
class Case:
    """A case in scope: a Step 18 scenario, or an ADR-013 case. What a defect selector can match on."""

    id: str
    domains: tuple[str, ...]
    package: str
    actor: str
    mode: str
    capability: str
    cycle: str


def read_csv(path: Path, fields: list[str], errors: list[str], rule: str) -> list[dict[str, str]]:
    with path.open(encoding="utf-8", newline="") as handle:
        reader = csv.DictReader(handle)
        if reader.fieldnames != fields:
            errors.append(f"{rule}: {path.name} has columns {reader.fieldnames}, expected {fields}")
            return []
        return list(reader)


def joined(values: list[str]) -> str:
    return "; ".join(values)


# ── Register ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

def register_row(scenario: dict[str, str]) -> dict[str, str]:
    journeys = [j for j, packages in JOURNEY_PACKAGES.items() if scenario["Package"] in packages]
    return {
        "Scenario ID": scenario["Scenario ID"],
        "Story ID": scenario["Story ID"],
        "RTM ID": scenario["RTM ID"],
        "Domain": scenario["Domain"],
        "Package": scenario["Package"],
        "Wave": scenario["Wave"],
        "Source actor": scenario["Source actor"].split("\n")[0].strip(),
        "Execution mode": scenario["Execution mode"],
        "Business capability": " ".join(scenario["Business capability"].split()),
        "Evidence class": scenario["Evidence class"],
        "Journeys": joined(journeys),
        "P0 tasks": joined(P0_TASKS[scenario["Domain"]]),
        "Cycle": "1" if journeys else "2",
    }


def check_register(errors: list[str]) -> list[Case]:
    rows = read_csv(REGISTER, REGISTER_FIELDS, errors, "UR-1")
    ids = [r["Scenario ID"] for r in rows]
    if len(rows) != SCENARIOS or len(set(ids)) != SCENARIOS:
        errors.append(f"UR-1: the register has {len(rows)} rows, {len(set(ids))} distinct; Step 18 has {SCENARIOS}")
    cases = []
    for row in rows:
        where = f"UR-1: {row['Scenario ID']}"
        if row["Domain"] not in P0_TASKS:
            errors.append(f"{where}: unknown domain {row['Domain']}")
            continue
        expected = register_row(row)
        for column in ("Journeys", "P0 tasks", "Cycle"):
            if row[column] != expected[column]:
                errors.append(f"{where}: {column} is '{row[column]}', the constants say '{expected[column]}'")
        cases.append(Case(row["Scenario ID"], (row["Domain"],), row["Package"], row["Source actor"],
                          row["Execution mode"], row["Business capability"], row["Cycle"]))
    return cases


def check_adr013(errors: list[str]) -> list[Case]:
    rows = read_csv(ADR013_CASES, CASE_FIELDS, errors, "UR-2")
    cases, seen = [], set()
    for row in rows:
        where = f"UR-2: {row['Case ID'] or '(no id)'}"
        if not re.fullmatch(r"UAT-A13-\d{2}", row["Case ID"]) or row["Case ID"] in seen:
            errors.append(f"{where}: the id is malformed or repeated")
        seen.add(row["Case ID"])
        for column in CASE_FIELDS:
            if row[column].strip() in NONE:
                errors.append(f"{where}: {column} is empty")
        domains = tuple(d.strip() for d in row["Domains"].split(";"))
        personas = [p.strip() for p in row["Personas"].split(";")]
        errors.extend(f"{where}: unknown domain {d}" for d in domains if d not in P0_TASKS)
        errors.extend(f"{where}: unknown persona {p}" for p in personas if p not in PERSONAS)
        cases.append(Case(row["Case ID"], domains, "", row["Personas"], "ADR-013", row["Title"], "1"))
    if not cases:
        errors.append("UR-2: no ADR-013 case; the participation amendment requires the entity Project Manager journey")
    return cases


# ── Defect selectors ─────────────────────────────────────────────────────────────────────────────────────────────────────
# "Expected to block": clauses separated by ';' (any may match), each a conjunction of terms joined by '&':
#   domain=WF-02   package=BPK-022   id=UAT-00012   cycle=1   mode~QA-ASSISTED   actor~regex   capability~regex
# '~' terms are case-insensitive regular expressions. 'all' alone marks an entry blocker: it stops every case, so the status
# lists it apart rather than counting the whole catalogue at risk. A selector records QA's expectation, not a result.

def term_matches(term: str, case: Case) -> bool:
    if term == "all":
        return True
    exact = re.fullmatch(r"(domain|package|id|cycle)=(\S+)", term)
    if exact:
        key, value = exact.groups()
        return value in case.domains if key == "domain" else getattr(case, key) == value
    pattern = re.fullmatch(r"(mode|actor|capability)~(.+)", term)
    if pattern:
        return re.search(pattern.group(2), getattr(case, pattern.group(1)), re.I) is not None
    raise ValueError(f"unknown selector term '{term}'")


def selected(selector: str, cases: list[Case]) -> list[Case]:
    clauses = [[t.strip() for t in clause.split("&")] for clause in selector.split(";") if clause.strip()]
    return [c for c in cases if any(all(term_matches(t, c) for t in clause) for clause in clauses)]


# ── Log, tracker, sign-off ───────────────────────────────────────────────────────────────────────────────────────────────

def defect_refs(text: str) -> list[str]:
    return re.findall(r"UAT-D-\d{3}", text)


def check_log(errors: list[str], scope: set[str], defects: set[str]) -> list[dict[str, str]]:
    rows = read_csv(LOG, LOG_FIELDS, errors, "UL-1")
    seen = set()
    for row in rows:
        where = f"UL-1: {row['Run ID'] or '(no id)'}"
        if not re.fullmatch(r"(UAT|RH)-\d{4}", row["Run ID"]) or row["Run ID"] in seen:
            errors.append(f"{where}: the run id is malformed or repeated")
        seen.add(row["Run ID"])
        if row["Run type"] not in RUN_TYPES or row["Run ID"][:2] != {"UAT": "UA", "REHEARSAL": "RH"}.get(row["Run type"]):
            errors.append(f"{where}: run type '{row['Run type']}' is unknown or does not match the id (UAT-nnnn, RH-nnnn)")
        target = row["Scenario / Journey ID"]
        if target not in scope and not (row["Run type"] == "REHEARSAL" and target in JOURNEY_PACKAGES):
            errors.append(f"{where}: '{target}' is no case in scope (a rehearsal may also name a journey)")
        if row["Result"] not in RESULTS:
            errors.append(f"{where}: result '{row['Result']}' is not one of {sorted(RESULTS)}")
        if not DATE.match(row["Execution date"]):
            errors.append(f"{where}: execution date '{row['Execution date']}' is not YYYY-MM-DD")
        for column in ("Case revision", "Build / environment", "Tester", "Actual outcome", "Evidence link"):
            if row[column].strip() in NONE:
                errors.append(f"{where}: {column} is empty")
        if row["Run type"] == "UAT" and re.search(r"delivery|claude|qa rehearsal", row["Tester"], re.I):
            errors.append(f"{where}: a UAT run is executed by an AHDA business tester, not '{row['Tester']}'")
        refs = defect_refs(row["Defect / retest reference"])
        if row["Result"] in {"FAIL", "BLOCKED"} and not refs:
            errors.append(f"{where}: a {row['Result']} names the defect it raised or met")
        errors.extend(f"{where}: names {ref}, which the tracker does not hold" for ref in refs if ref not in defects)
        if row["Result"] == "NOT APPLICABLE" and row["Acceptance reviewer"].strip() in NONE:
            errors.append(f"{where}: NOT APPLICABLE needs the acceptance reviewer who agreed it")
    return rows


def check_tracker(errors: list[str], cases: list[Case]) -> list[dict[str, str]]:
    rows = read_csv(TRACKER, TRACKER_FIELDS, errors, "UD-1")
    seen = set()
    for row in rows:
        where = f"UD-1: {row['Defect ID'] or '(no id)'}"
        if not re.fullmatch(r"UAT-D-\d{3}", row["Defect ID"]) or row["Defect ID"] in seen:
            errors.append(f"{where}: the id is malformed or repeated")
        seen.add(row["Defect ID"])
        for column in ("Title", "Domain", "Found in", "Owner"):
            if row[column].strip() in NONE:
                errors.append(f"{where}: {column} is empty")
        if row["Severity"] not in SEVERITIES:
            errors.append(f"{where}: severity '{row['Severity']}' is not one of {SEVERITIES}")
        if row["Status"] not in STATUSES:
            errors.append(f"{where}: status '{row['Status']}' is not one of {sorted(STATUSES)}")
        if row["Expected to block"].strip() not in NONE:
            try:
                if not selected(row["Expected to block"], cases):
                    errors.append(f"{where}: its selector matches no case in scope")
            except (ValueError, re.error) as error:
                errors.append(f"{where}: {error}")
        if row["Status"] == "Deferred - accepted":
            if row["Severity"] in {"High", "Critical"}:
                errors.append(f"{where}: a {row['Severity']} defect is never deferred")
            if row["Acceptance reference"].strip() in NONE:
                errors.append(f"{where}: a deferral names AHDA's acceptance")
        if row["Status"] == "Rejected" and row["Notes"].strip() in NONE:
            errors.append(f"{where}: a rejection says why in Notes")
        if row["Status"] in {"Fixed - awaiting retest", "Closed"} and not re.search(r"PR #\d+", row["Fix"]):
            errors.append(f"{where}: {row['Status']} names its fix as 'PR #n'")
    return rows


def check_retests(errors: list[str], tracker: list[dict[str, str]], log: list[dict[str, str]]) -> None:
    runs = {r["Run ID"]: r for r in log}
    for row in tracker:
        where = f"UD-1: {row['Defect ID']}"
        found = row["Found in"]
        if found != "Readiness review" and found not in runs:
            errors.append(f"{where}: found in '{found}', which is neither 'Readiness review' nor a logged run")
        if row["Status"] != "Closed":
            continue
        retest = runs.get(row["Retest run"])
        if retest is None:
            errors.append(f"{where}: Closed names no logged retest run")
        elif (retest["Run type"], retest["Result"]) != ("UAT", "PASS") or row["Defect ID"] not in defect_refs(
                retest["Defect / retest reference"]):
            errors.append(f"{where}: retest {retest['Run ID']} is not a passed UAT run referencing the defect")
        elif found in runs and retest["Execution date"] < runs[found]["Execution date"]:
            errors.append(f"{where}: retest {retest['Run ID']} predates the run that found it")


def read_sign_off(errors: list[str]) -> list[dict[str, str]]:
    signatories, header = [], None
    for line in SIGN_OFF.read_text(encoding="utf-8").splitlines():
        cells = [c.strip() for c in line.strip().strip("|").split("|")] if line.startswith("|") else None
        if cells is None:
            header = None
        elif cells[0] == "Role":
            header = cells
        elif header and not set(cells[0]) <= {"-", " "}:
            signatories.append(dict(zip(header, cells)))
    for row in signatories:
        if row.get("Decision") not in DECISIONS:
            errors.append(f"US-1: {row.get('Role')}: decision '{row.get('Decision')}' is not one of {sorted(DECISIONS)}")
    missing = [r for r in REQUIRED_SIGNATORIES if r not in {s.get("Role") for s in signatories}]
    if missing:
        errors.append(f"US-1: the sign-off record has no row for {missing}")
    return signatories


# ── Status and gate ──────────────────────────────────────────────────────────────────────────────────────────────────────

def latest_uat(log: list[dict[str, str]]) -> dict[str, dict[str, str]]:
    latest: dict[str, dict[str, str]] = {}
    for row in sorted((r for r in log if r["Run type"] == "UAT"), key=lambda r: (r["Execution date"], r["Run ID"])):
        latest[row["Scenario / Journey ID"]] = row
    return latest


def open_defects(tracker: list[dict[str, str]]) -> list[dict[str, str]]:
    return [d for d in tracker if d["Status"] in {"Open", "Fixed - awaiting retest"}]


def status_table(cases: list[Case], log: list[dict[str, str]], tracker: list[dict[str, str]]) -> str:
    latest = latest_uat(log)
    blockers = [d for d in open_defects(tracker) if d["Expected to block"].strip() == "all"]
    at_risk = {c.id for d in open_defects(tracker) if d["Expected to block"].strip() not in NONE | {"all"}
               for c in selected(d["Expected to block"], cases)}
    groups = [(d, [c for c in cases if c.mode != "ADR-013" and c.domains[0] == d]) for d in DOMAINS]
    groups.append(("ADR-013", [c for c in cases if c.mode == "ADR-013"]))
    lines = [
        "| Domain | In scope | Cycle 1 | Executed | PASS | FAIL | BLOCKED | N/A | Not run | At risk |",
        "| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |",
    ]
    totals = Counter()
    for name, group in groups:
        results = Counter(latest[c.id]["Result"] for c in group if c.id in latest)
        row = Counter({
            "In scope": len(group), "Cycle 1": sum(c.cycle == "1" for c in group), "Executed": sum(results.values()),
            "PASS": results["PASS"], "FAIL": results["FAIL"], "BLOCKED": results["BLOCKED"],
            "N/A": results["NOT APPLICABLE"], "Not run": len(group) - sum(results.values()),
            "At risk": sum(c.id in at_risk for c in group),
        })
        totals.update(row)
        lines.append(f"| {name} | " + " | ".join(f"{row[k]:,}" for k in row) + " |")
    lines.append("| **Total** | " + " | ".join(f"**{totals[k]:,}**" for k in totals) + " |")
    severities = Counter(d["Severity"] for d in open_defects(tracker))
    lines += ["", "| Open defects | Critical | High | Medium | Low |", "| --- | --- | --- | --- | --- |",
              "| Open or awaiting retest | " + " | ".join(str(severities[s]) for s in reversed(SEVERITIES)) + " |", "",
              "Entry blockers (stop every case; not counted as at risk): "
              + (", ".join(d["Defect ID"] for d in blockers) or "none") + "."]
    return "\n".join(lines)


def gate(cases: list[Case], log: list[dict[str, str]], tracker: list[dict[str, str]],
         signatories: list[dict[str, str]]) -> list[str]:
    failures = []
    latest = latest_uat(log)
    defects = {d["Defect ID"]: d for d in tracker}
    not_executed = [c.id for c in cases if c.id not in latest or latest[c.id]["Result"] == "BLOCKED"]
    if not_executed:
        failures.append(f"G-1: {len(not_executed):,} of {len(cases):,} cases in scope have no recorded UAT outcome "
                        f"(first: {', '.join(not_executed[:5])})")
    for case_id, run in latest.items():
        if run["Result"] == "FAIL":
            refs = defect_refs(run["Defect / retest reference"])
            if not refs or any(defects[r]["Severity"] in {"High", "Critical"} or defects[r]["Status"] != "Deferred - accepted"
                               for r in refs):
                failures.append(f"G-2: {case_id} last failed ({run['Run ID']}) on a defect AHDA has not deferred")
    for defect in tracker:
        if defect["Severity"] in {"High", "Critical"} and defect["Status"] not in {"Closed", "Rejected"}:
            failures.append(f"G-3: {defect['Defect ID']} ({defect['Severity']}) is {defect['Status']}")
    by_role = {s.get("Role"): s for s in signatories}
    for role in REQUIRED_SIGNATORIES:
        signed = by_role.get(role, {})
        if not (signed.get("Decision", "").startswith("Accepted") and DATE.match(signed.get("Date", ""))
                and signed.get("Name", "").strip() not in NONE and signed.get("Signed copy", "").strip() not in NONE):
            failures.append(f"G-4: {role} has not signed AHDA's acceptance (name, decision, date, signed copy)")
    return failures


def check() -> tuple[list[str], list[Case], list[dict[str, str]], list[dict[str, str]], list[dict[str, str]]]:
    errors: list[str] = []
    cases = check_register(errors) + check_adr013(errors)
    tracker = check_tracker(errors, cases)
    log = check_log(errors, {c.id for c in cases}, {d["Defect ID"] for d in tracker})
    check_retests(errors, tracker, log)
    signatories = read_sign_off(errors)
    if any(s.get("Decision", "").startswith("Accepted") for s in signatories) and gate(cases, log, tracker, signatories):
        errors.append("US-1: the sign-off record records an acceptance while the gate is red")
    record = RECORD.read_text(encoding="utf-8")
    match = re.search(re.escape(STATUS_BEGIN) + r"\n(.*?)\n" + re.escape(STATUS_END), record, re.S)
    if match is None or match.group(1).strip() != status_table(cases, log, tracker):
        errors.append(f"UT-1: the status table in {RECORD.name} is not --status; paste its output between the markers")
    return errors, cases, log, tracker, signatories


# ── Building the register from the controlled sources ────────────────────────────────────────────────────────────────────

NS = {"m": "http://schemas.openxmlformats.org/spreadsheetml/2006/main",
      "r": "http://schemas.openxmlformats.org/officeDocument/2006/relationships"}


def sheet_rows(book: zipfile.ZipFile, sheet: str) -> list[dict[str, str]]:
    shared = []
    if "xl/sharedStrings.xml" in book.namelist():
        for item in ET.fromstring(book.read("xl/sharedStrings.xml")).findall("m:si", NS):
            shared.append("".join(t.text or "" for t in item.iter(f"{{{NS['m']}}}t")))
    targets = {r.get("Id"): r.get("Target") for r in ET.fromstring(book.read("xl/_rels/workbook.xml.rels"))}
    paths = {s.get("name"): "xl/" + targets[s.get(f"{{{NS['r']}}}id")].lstrip("/").removeprefix("xl/")
             for s in ET.fromstring(book.read("xl/workbook.xml")).find("m:sheets", NS)}
    table = []
    for row in ET.fromstring(book.read(paths[sheet])).iter(f"{{{NS['m']}}}row"):
        cells = {}
        for cell in row.findall("m:c", NS):
            column = 0
            for letter in re.match(r"[A-Z]+", cell.get("r")).group():
                column = column * 26 + ord(letter) - 64
            value = cell.find("m:v", NS)
            if cell.get("t") == "s" and value is not None:
                cells[column] = shared[int(value.text)]
            elif cell.get("t") == "inlineStr":
                cells[column] = "".join(t.text or "" for t in cell.iter(f"{{{NS['m']}}}t"))
            else:
                cells[column] = value.text if value is not None else ""
        table.append(cells)
    header = table[0]
    return [{header[k]: v for k, v in r.items() if k in header} for r in table[1:] if r]


def build_register(sources: str, workbook: str) -> list[str]:
    errors = []
    with zipfile.ZipFile(sources) as outer:
        name = next(n for n in outer.namelist()
                    if n.endswith("Step_18_UAT_Catalogue_v1.0.xlsx") and "__MACOSX" not in n)
        step18 = zipfile.ZipFile(io.BytesIO(outer.read(name)))
    journeys = {j["Journey ID"]: [p.strip() for p in j["Packages"].split(";")] for j in sheet_rows(step18, "UAT_Journeys")}
    if journeys != JOURNEY_PACKAGES:
        errors.append(f"JOURNEY_PACKAGES differs from Step 18 UAT_Journeys: {journeys}")
    plan = [r for r in sheet_rows(zipfile.ZipFile(workbook), "Implementation Plan") if r.get("Task ID")]
    tasks = {r["Task ID"]: r for r in plan}
    for domain, ids in P0_TASKS.items():
        for task in ids:
            row = tasks.get(task, {})
            if row.get("Priority") != "P0" or domain not in f"{row.get('Task Name', '')} {row.get('Phase', '')}":
                errors.append(f"P0_TASKS: {task} is not a P0 task naming {domain} in its name or phase")
    mapped = {t for ids in P0_TASKS.values() for t in ids}
    for row in plan:
        builds = re.search(r"\((?:WF|FG)-\d\d", row.get("Phase", "")) and re.search(r"(?:WF|FG)-\d\d", row["Task Name"])
        if row.get("Priority") == "P0" and builds and row["Task ID"] not in mapped:
            errors.append(f"P0_TASKS: {row['Task ID']} ({row['Task Name']}) is a P0 domain task no domain lists")
    scenarios = sheet_rows(step18, "UAT_Scenarios")
    with REGISTER.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, REGISTER_FIELDS, lineterminator="\n")
        writer.writeheader()
        writer.writerows(register_row(s) for s in scenarios)
    print(f"{REGISTER.name}: {len(scenarios):,} scenarios written")
    return errors


def main(argv: list[str]) -> int:
    sys.dont_write_bytecode = True
    if argv[:1] == ["--register"] and len(argv) == 3:
        errors = build_register(argv[1], argv[2])
    elif argv in (["--check"], ["--gate"], ["--status"]):
        errors, cases, log, tracker, signatories = check()
        if argv == ["--status"]:
            print(status_table(cases, log, tracker))
        elif argv == ["--gate"] and not errors:
            errors = gate(cases, log, tracker, signatories)
    else:
        print(__doc__)
        return 2
    for error in errors:
        print(error)
    if argv != ["--status"]:
        print(f"{len(errors)} finding(s)")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
