#!/usr/bin/env python3
"""TASK-082: the penetration test report register, the remediation tracker and the retest evidence agree,
and (with --gate) the Release Checklist's "External Penetration Test Passed".

Without arguments (repo-checks, every PR):
  PR-1  Every report block (PTR-n) has a known status; a Final one names its provider, independence, environment,
        tested commit (40 hex), image digest, test window, report reference, SHA-256 (64 hex), date received,
        and covers areas A, B, C and D (penetration-test.md section 3.2).
  PR-2  Every finding as issued (PT-nn) is unique, belongs to a registered Draft or Final report, has a severity
        and a title.
  TR-1  The tracker has exactly one row per finding in the register, with the same severity.
  TR-2  Remediated cites a 'PR #n'; Risk accepted cites an RA record that exists and names the finding, and is
        never used for a Critical; Withdrawn is the finding's latest retest result.
  TR-3  The tracker's Retest column is the finding's latest retest round and result, or '—' before any.
  RT-1  Every retest block (RT-n) names a Final report, the provider, a date, the tested commit (40 hex), its
        reference and SHA-256 (64 hex), and lists only registered findings with a known result.
  RA-1  As in findings-log-check.py: a Signed acceptance names who accepted it, when, and where the signed copy is.

--gate (the Release Checklist's "External Penetration Test Passed"):
  G-1   A Final report exists, and every finding belongs to a Final report.
  G-2   Every Critical is Remediated and its latest retest is Closed.
  G-3   Every High is Remediated with a Closed latest retest, or Risk accepted under Signed RA records.
  G-4   Every Remediated finding, of any severity, has a Closed latest retest.
--verify-prs (with --gate): every PR a Remediated finding cites is MERGED on GitHub (needs an authenticated `gh`).

Run from the repository root: python3 docs/security/penetration-test-check.py [--gate [--verify-prs]]
"""

from __future__ import annotations

import importlib.util
import re
import sys
from dataclasses import dataclass, field
from pathlib import Path

ROOT = Path(__file__).resolve().parent

# The table, id and risk-acceptance readers are findings-log-check.py's; both checks read risk-acceptances.md.
sys.dont_write_bytecode = True  # no __pycache__ in docs/security
_spec = importlib.util.spec_from_file_location("findings_log_check", ROOT / "findings-log-check.py")
findings_log = sys.modules[_spec.name] = importlib.util.module_from_spec(_spec)  # dataclasses look it up there
_spec.loader.exec_module(findings_log)
cells, ids, NONE = findings_log.cells, findings_log.ids, findings_log.NONE

SEVERITIES = ["Informational", "Low", "Medium", "High", "Critical"]
REPORT_STATUSES = {"Not received", "Draft", "Final"}
DISPOSITIONS = {"Open", "Remediated", "Risk accepted", "Withdrawn"}
RESULTS = {"Closed", "Not closed", "Withdrawn"}
REQUIRED_AREAS = {"A", "B", "C", "D"}
REPORT_FIELDS = (
    "Provider", "Independence", "Environment", "Tested commit", "Image digest", "Test window",
    "Areas covered", "Report reference", "Report SHA-256", "Received",
)
RETEST_FIELDS = ("Report", "Provider", "Retest date", "Tested commit", "Retest reference", "Retest SHA-256")
SHA1 = re.compile(r"[0-9a-f]{40}")
SHA256 = re.compile(r"[0-9a-f]{64}")


@dataclass
class Report:
    id: str
    fields: dict[str, str] = field(default_factory=dict)

    @property
    def status(self) -> str:
        return self.fields.get("Status", "")


@dataclass
class Issued:
    id: str
    where: str
    report: str
    severity: str


@dataclass
class Tracked:
    id: str
    where: str
    severity: str
    disposition: str
    fix: str
    retest: str


@dataclass
class Retest:
    id: str
    number: int
    fields: dict[str, str] = field(default_factory=dict)
    results: dict[str, str] = field(default_factory=dict)


def lines_of(name: str) -> list[tuple[int, str]]:
    # Blank out HTML comments (the tracker's retest template) but keep line numbers.
    text = (ROOT / name).read_text(encoding="utf-8")
    text = re.sub(r"<!--.*?-->", lambda m: "\n" * m.group(0).count("\n"), text, flags=re.S)
    return list(enumerate(text.splitlines(), 1))


def filled(value: str | None) -> bool:
    return (value or "").strip() not in NONE


def read_register(errors: list[str]) -> tuple[dict[str, Report], dict[str, Issued]]:
    reports: dict[str, Report] = {}
    issued: dict[str, Issued] = {}
    current: Report | None = None
    for number, line in lines_of("penetration-test-report.md"):
        where = f"penetration-test-report.md:{number}"
        if line.startswith("## "):
            current = None
        heading = re.match(r"^###\s+(PTR-\d+)\b", line)
        if heading:
            if heading.group(1) in reports:
                errors.append(f"PR-1: {where}: {heading.group(1)} appears twice")
            current = Report(heading.group(1))
            reports[current.id] = current
            continue
        if not line.startswith("|"):
            continue
        row = cells(line)
        if current is not None and len(row) == 2:
            current.fields[row[0]] = row[1]
            continue
        if not re.match(r"^\|\s*PT-\d+\s*\|", line):
            continue
        if len(row) != 7:
            errors.append(f"PR-2: {where}: a finding row has 7 columns, this one has {len(row)}")
            continue
        pid, report, _, severity, _, _, title = row
        if pid in issued:
            errors.append(f"PR-2: {where}: {pid} appears twice")
        if severity not in SEVERITIES:
            errors.append(f"PR-2: {where}: {pid}: severity '{severity}' is not one of {SEVERITIES}")
        if not filled(title):
            errors.append(f"PR-2: {where}: {pid} has no title")
        issued[pid] = Issued(pid, where, report, severity)
    for report in reports.values():
        if report.status not in REPORT_STATUSES:
            errors.append(f"PR-1: {report.id}: status '{report.status}' is not one of {sorted(REPORT_STATUSES)}")
        if report.status != "Final":
            continue
        for key in REPORT_FIELDS:
            if not filled(report.fields.get(key)):
                errors.append(f"PR-1: {report.id} is Final but '{key}' is empty")
        if filled(report.fields.get("Tested commit")) and not SHA1.fullmatch(report.fields["Tested commit"].strip("`")):
            errors.append(f"PR-1: {report.id}: 'Tested commit' is not a 40-character commit id")
        if filled(report.fields.get("Report SHA-256")) and not SHA256.fullmatch(report.fields["Report SHA-256"].strip("`")):
            errors.append(f"PR-1: {report.id}: 'Report SHA-256' is not 64 hex characters")
        areas = set(re.findall(r"\b[A-F]\b", report.fields.get("Areas covered", "")))
        if missing := sorted(REQUIRED_AREAS - areas):
            errors.append(f"PR-1: {report.id} is Final but does not cover area(s) {', '.join(missing)}")
    for finding in issued.values():
        report = reports.get(finding.report)
        if report is None:
            errors.append(f"PR-2: {finding.where}: {finding.id} names report '{finding.report}', which is not registered")
        elif report.status not in {"Draft", "Final"}:
            errors.append(f"PR-2: {finding.where}: {finding.id} belongs to {report.id}, which is '{report.status}'")
    return reports, issued


def read_tracker(errors: list[str]) -> tuple[dict[str, Tracked], dict[str, Retest]]:
    tracked: dict[str, Tracked] = {}
    retests: dict[str, Retest] = {}
    current: Retest | None = None
    for number, line in lines_of("penetration-test-tracker.md"):
        where = f"penetration-test-tracker.md:{number}"
        if line.startswith("## "):
            current = None
        heading = re.match(r"^###\s+(RT-(\d+))\b", line)
        if heading:
            if heading.group(1) in retests:
                errors.append(f"RT-1: {where}: {heading.group(1)} appears twice")
            current = Retest(heading.group(1), int(heading.group(2)))
            retests[current.id] = current
            continue
        if not line.startswith("|"):
            continue
        row = cells(line)
        if current is not None:
            if re.match(r"^\|\s*PT-\d+\s*\|", line):
                if len(row) != 3:
                    errors.append(f"RT-1: {where}: a result row has 3 columns, this one has {len(row)}")
                elif row[1] not in RESULTS:
                    errors.append(f"RT-1: {where}: {row[0]}: result '{row[1]}' is not one of {sorted(RESULTS)}")
                elif row[0] in current.results:
                    errors.append(f"RT-1: {where}: {row[0]} appears twice in {current.id}")
                else:
                    current.results[row[0]] = row[1]
            elif len(row) == 2:
                current.fields[row[0]] = row[1]
            continue
        if not re.match(r"^\|\s*PT-\d+\s*\|", line):
            continue
        if len(row) != 8:
            errors.append(f"TR-1: {where}: a tracker row has 8 columns, this one has {len(row)}")
            continue
        pid, severity, _, _, disposition, fix, retest, _ = row
        if pid in tracked:
            errors.append(f"TR-1: {where}: {pid} appears twice")
        if disposition not in DISPOSITIONS:
            errors.append(f"TR-2: {where}: {pid}: disposition '{disposition}' is not one of {sorted(DISPOSITIONS)}")
        tracked[pid] = Tracked(pid, where, severity, disposition, fix, retest)
    return tracked, retests


def latest(pid: str, retests: dict[str, Retest]) -> tuple[str, str] | None:
    rounds = sorted((r for r in retests.values() if pid in r.results), key=lambda r: r.number)
    return (rounds[-1].id, rounds[-1].results[pid]) if rounds else None


def cross_check(reports, issued, tracked, retests, acceptances, errors: list[str]) -> None:
    for pid, finding in issued.items():
        if pid not in tracked:
            errors.append(f"TR-1: {pid} ({finding.where}) has no row in the tracker")
        elif tracked[pid].severity != finding.severity:
            errors.append(
                f"TR-1: {tracked[pid].where}: {pid}: severity '{tracked[pid].severity}', "
                f"the report says '{finding.severity}'"
            )
    for row in tracked.values():
        if row.id not in issued:
            errors.append(f"TR-1: {row.where}: {row.id} is not in the report register")
        last = latest(row.id, retests)
        expected = f"{last[0]} {last[1]}" if last else "—"
        shown = row.retest if filled(row.retest) else "—"
        if shown != expected:
            errors.append(f"TR-3: {row.where}: {row.id}: Retest says '{shown}', the retest evidence says '{expected}'")
        if row.disposition == "Remediated" and not re.search(r"PR #\d+", row.fix):
            errors.append(f"TR-2: {row.where}: {row.id} is Remediated but cites no 'PR #n'")
        if row.disposition == "Risk accepted":
            if row.severity == "Critical":
                errors.append(f"TR-2: {row.where}: {row.id} is Critical; a Critical cannot be risk-accepted")
            cited = ids(row.fix, "RA")
            if not cited:
                errors.append(f"TR-2: {row.where}: {row.id} is Risk accepted but cites no RA record")
            for raid in cited:
                if raid not in acceptances:
                    errors.append(f"TR-2: {row.where}: {row.id} cites {raid}, which is not in risk-acceptances.md")
                elif row.id not in ids(acceptances[raid].fields.get("Findings", ""), "PT"):
                    errors.append(f"TR-2: {row.where}: {row.id} cites {raid}, whose Findings row does not name it")
        if row.disposition == "Withdrawn" and (last is None or last[1] != "Withdrawn"):
            errors.append(f"TR-2: {row.where}: {row.id} is Withdrawn but its latest retest result is not 'Withdrawn'")
    for retest in retests.values():
        for key in RETEST_FIELDS:
            if not filled(retest.fields.get(key)):
                errors.append(f"RT-1: {retest.id}: '{key}' is empty")
        report = reports.get(retest.fields.get("Report", ""))
        if filled(retest.fields.get("Report")) and (report is None or report.status != "Final"):
            errors.append(f"RT-1: {retest.id}: report '{retest.fields['Report']}' is not a registered Final report")
        if filled(retest.fields.get("Tested commit")) and not SHA1.fullmatch(retest.fields["Tested commit"].strip("`")):
            errors.append(f"RT-1: {retest.id}: 'Tested commit' is not a 40-character commit id")
        if filled(retest.fields.get("Retest SHA-256")) and not SHA256.fullmatch(retest.fields["Retest SHA-256"].strip("`")):
            errors.append(f"RT-1: {retest.id}: 'Retest SHA-256' is not 64 hex characters")
        if not retest.results:
            errors.append(f"RT-1: {retest.id} lists no finding")
        for pid in retest.results:
            if pid not in issued:
                errors.append(f"RT-1: {retest.id} lists {pid}, which is not in the report register")
    for acceptance in acceptances.values():
        for pid in ids(acceptance.fields.get("Findings", ""), "PT"):
            if pid not in issued:
                errors.append(f"RA-1: {acceptance.id} covers {pid}, which is not in the report register")


def gate(reports, issued, tracked, retests, acceptances, verify_prs: bool) -> list[str]:
    blocking: list[str] = []
    if not any(r.status == "Final" for r in reports.values()):
        blocking.append("G-1: no Final penetration test report is registered")
    for finding in issued.values():
        if reports[finding.report].status != "Final":
            blocking.append(f"G-1: {finding.id} belongs to {finding.report}, which is not Final")
    for row in tracked.values():
        last = latest(row.id, retests)
        closed = last is not None and last[1] == "Closed"
        # Withdrawn needs the provider's Withdrawn as the latest result (TR-2), so it is resolved like a closed fix.
        resolved = (row.disposition == "Remediated" and closed) or row.disposition == "Withdrawn"
        if row.disposition == "Remediated" and not closed:
            blocking.append(f"G-4: {row.id} ({row.severity}) is Remediated with no Closed retest")
        elif row.severity == "Critical" and not resolved:
            blocking.append(f"G-2: {row.id} (Critical) is {row.disposition}")
        elif row.severity == "High" and not resolved:
            accepted = row.disposition == "Risk accepted" and all(
                acceptances.get(raid) and acceptances[raid].status == "Signed" for raid in ids(row.fix, "RA")
            )
            if not accepted:
                state = f"{', '.join(ids(row.fix, 'RA'))} not signed" if row.disposition == "Risk accepted" else row.disposition
                blocking.append(f"G-3: {row.id} (High) is {state}")
        if verify_prs and row.disposition == "Remediated":
            for n in (int(n) for n in re.findall(r"PR #(\d+)", row.fix)):
                if (state := findings_log.pr_state(n)) != "MERGED":
                    blocking.append(f"G-4: {row.id}: PR #{n} is {state}")
    return blocking


def main(argv: list[str]) -> int:
    unknown = [a for a in argv if a not in {"--gate", "--verify-prs"}]
    if unknown or ("--verify-prs" in argv and "--gate" not in argv):
        print(__doc__)
        return 2
    errors: list[str] = []
    reports, issued = read_register(errors)
    tracked, retests = read_tracker(errors)
    acceptances = findings_log.read_acceptances(errors)
    cross_check(reports, issued, tracked, retests, acceptances, errors)
    for error in errors:
        print(f"FAIL {error}")
    if errors:
        print(f"{len(errors)} consistency error(s) in the penetration test records")
        return 1
    by_severity = ", ".join(
        f"{sum(1 for f in issued.values() if f.severity == s)} {s}" for s in reversed(SEVERITIES)
    )
    print(
        f"OK: {len(reports)} report(s) ({sum(1 for r in reports.values() if r.status == 'Final')} Final), "
        f"{len(issued)} finding(s) ({by_severity}), {len(retests)} retest round(s)"
    )
    if "--gate" not in argv:
        return 0
    blocking = gate(reports, issued, tracked, retests, acceptances, "--verify-prs" in argv)
    for line in blocking:
        print(f"GATE {line}")
    if blocking:
        print(f"External Penetration Test NOT passed: {len(blocking)} blocking item(s)")
        return 1
    print(
        "External Penetration Test passed: a Final report, no unresolved Critical, every High closed by retest "
        "or a signed AHDA acceptance, every remediated finding closed by retest"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
