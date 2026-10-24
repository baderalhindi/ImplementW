#!/usr/bin/env python3
"""Checks on the operational handover package (TASK-097). Record: docs/handover/README.md

    python3 docs/handover/handover-package-check.py           # repo-checks: the package is whole and current
    python3 docs/handover/handover-package-check.py --gate    # Release Checklist "Operational Handover Package Accepted"

HP-1  the index cross-references every document the workbook's Documentation-category tasks produced: each task in
      DOCUMENTATION_TASKS has index rows, and each of its deliverables is the record of a row naming that task.
HP-2  every relative link in the package resolves to a file or folder in the repository; index IDs are unique and
      every index row links its record.
HP-3  the index is current: where a record states its own status (`| Status |` or `| Decision status |` in its
      header), the index row quotes that status's first sentence, in bold, at the start of its status cell.
HP-4  acceptance is not claimed without its evidence: the package status is COMPILED or ACCEPTED; ACCEPTED needs the
      acknowledgment marked SIGNED and every field of its receipt record filled, with a 40-character commit; while
      COMPILED, no receipt field is filled. --gate fails until ACCEPTED.
HP-5  the knowledge-transfer record covers Client Scope §12: KT-1 to KT-5 each name at least one index row, and every
      session logged names a known area and a date.

Standard library only, like the other repo-checks.
"""

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PACKAGE = ROOT / "docs/handover"
INDEX = PACKAGE / "README.md"
TRANSFER = PACKAGE / "knowledge-transfer-record.md"
ACKNOWLEDGMENT = PACKAGE / "knowledge-transfer-acknowledgment.md"

# The workbook's Documentation category (Implementation Plan, column Category), read 2026-10-24 from both the local
# AHDA_RPMO_Platform_Implementation_Plan_v2.xlsx and the Google Sheet: TASK-093 to TASK-097, no others. The paths are
# what each task's record names as its deliverables. A new Documentation-category task is added here and to the index.
DOCUMENTATION_TASKS = {
    "TASK-093": ["docs/operations/on-call-runbook.md", "docs/operations/incident-severity-classification.md"],
    "TASK-094": ["docs/api/README.md", "docs/api/openapi.v1.json", "docs/api/spec-audit.py",
                 "docs/api/spec-audit-sample.json", "docs/api/api-documentation-check.py"],
    "TASK-095": ["docs/onboarding/developer-onboarding-guide.md"],
    "TASK-096": ["docs/architecture/user-guide-faq.md", "src/frontend/src/content/help/help.ts"]
                + [f"src/frontend/src/content/help/{language}/{name}.json"
                   for language in ("en", "ar") for name in ("guide", "faq", "knowledge-base")],
    "TASK-097": ["docs/handover/README.md", "docs/handover/knowledge-transfer-record.md",
                 "docs/handover/knowledge-transfer-acknowledgment.md", "docs/handover/handover-package-check.py"],
}
TRANSFER_AREAS = [f"KT-{n}" for n in range(1, 6)]
RECEIPT_FIELDS = ["Signed original held at", "Signed on", "Package commit reviewed", "Signed for AHDA by (role)",
                  "Signed for the delivery team by (role)"]

LINK = re.compile(r"\[[^\]]*\]\(([^)\s]+)\)")
STATUS_ROW = re.compile(r"^\| (?:Status|Decision status) \|(.*?)\|\s*$", re.M)

findings = []


def fail(check, message):
    findings.append(f"{check}: {message}")


def cells(line):
    return [cell.strip() for cell in line.strip().strip("|").split("|")]


def rows(text, pattern):
    """The table rows of a document whose first cell matches pattern, as lists of cells."""
    return [cells(line) for line in text.splitlines()
            if line.startswith("|") and re.fullmatch(pattern, cells(line)[0])]


def target(document, link):
    """The repository path a relative link in document points at, or None for an external link."""
    if re.match(r"^[a-z]+:", link) or link.startswith("#"):
        return None
    return (document.parent / link.split("#", 1)[0]).resolve()


def lead(document):
    """The first sentence of the status a record states in its header, or None if it states none."""
    match = STATUS_ROW.search(document.read_text(encoding="utf-8"))
    if not match:
        return None
    bold = re.search(r"\*\*(.+?)\*\*", match.group(1))
    status = (bold.group(1) if bold else match.group(1)).strip()
    sentence = re.match(r"(.+?\.)(?=\s|$)", status)
    return sentence.group(1) if sentence else status


def index_rows():
    text = INDEX.read_text(encoding="utf-8")
    entries = rows(text, r"H-\d{2}")
    seen = set()
    for entry in entries:
        if entry[0] in seen:
            fail("HP-2", f"index ID {entry[0]} is used twice")
        seen.add(entry[0])
    return entries


def check_coverage(entries):
    for task, deliverables in DOCUMENTATION_TASKS.items():
        named = [entry for entry in entries if task in entry[2]]
        if not named:
            fail("HP-1", f"no index row names {task}, a Documentation-category task")
        linked = {target(INDEX, link) for entry in named for link in LINK.findall(entry[1])}
        for deliverable in deliverables:
            if (ROOT / deliverable).resolve() not in linked:
                fail("HP-1", f"{deliverable} ({task}) is not the record of an index row naming {task}")


def check_links(entries):
    for document in sorted(PACKAGE.glob("*.md")):
        for link in LINK.findall(document.read_text(encoding="utf-8")):
            path = target(document, link)
            if path is not None and not path.exists():
                fail("HP-2", f"{document.name} links {link}, which does not exist")
    for entry in entries:
        if not LINK.findall(entry[1]):
            fail("HP-2", f"index row {entry[0]} links no record")


def check_currency(entries):
    for entry in entries:
        links = LINK.findall(entry[1])
        record = target(INDEX, links[0]) if links else None
        if record is None or record.suffix != ".md" or not record.is_file():
            continue
        stated = lead(record)
        if stated is not None and not entry[-1].startswith(f"**{stated}**"):
            fail("HP-3", f"index row {entry[0]} does not open with {record.relative_to(ROOT)}'s status "
                         f"**{stated}**; the record has changed since the index was written")


def receipt():
    return {entry[0]: entry[1] for entry in rows(ACKNOWLEDGMENT.read_text(encoding="utf-8"), "|".join(map(re.escape, RECEIPT_FIELDS)))}


def check_acceptance(gate):
    status = lead(INDEX) or ""
    signed = (lead(ACKNOWLEDGMENT) or "").startswith("SIGNED")
    fields = receipt()
    for field in RECEIPT_FIELDS:
        if field not in fields:
            fail("HP-4", f"the acknowledgment's receipt record has no '{field}' row")
    filled = {field: value for field, value in fields.items() if value not in ("", "—")}
    if status.startswith("ACCEPTED"):
        if not signed:
            fail("HP-4", "the package is ACCEPTED but the acknowledgment is not marked SIGNED")
        for field in RECEIPT_FIELDS:
            if field not in filled:
                fail("HP-4", f"the package is ACCEPTED but the receipt record's '{field}' is empty")
        if not re.fullmatch(r"`?[0-9a-f]{40}`?", filled.get("Package commit reviewed", "")):
            fail("HP-4", "the receipt record's 'Package commit reviewed' is not a 40-character commit")
    elif status.startswith("COMPILED"):
        if signed or filled:
            fail("HP-4", "the package is not ACCEPTED, yet the acknowledgment is marked SIGNED or its receipt is filled")
        if gate:
            fail("HP-4", "gate: the package has not been accepted by AHDA's receiving operations team (README §9)")
    else:
        fail("HP-4", f"the package status '{status}' is neither COMPILED nor ACCEPTED")


def check_transfer(entries):
    text = TRANSFER.read_text(encoding="utf-8")
    known = {entry[0] for entry in entries}
    areas = {entry[0]: entry for entry in rows(text, r"KT-\d")}
    for area in TRANSFER_AREAS:
        if area not in areas:
            fail("HP-5", f"the knowledge-transfer record has no {area} (Client Scope §12)")
            continue
        named = set(re.findall(r"H-\d{2}", " ".join(areas[area])))
        if not named:
            fail("HP-5", f"{area} names no index row as its material")
        for unknown in sorted(named - known):
            fail("HP-5", f"{area} names {unknown}, which is not in the index")
    for session in rows(text, r"L-\d{2}"):
        if not re.fullmatch(r"\d{4}-\d{2}-\d{2}", session[1]):
            fail("HP-5", f"session {session[0]} has no date (YYYY-MM-DD)")
        if not set(re.findall(r"KT-\d", session[2])) & set(TRANSFER_AREAS):
            fail("HP-5", f"session {session[0]} names no knowledge-transfer area")


def main():
    gate = "--gate" in sys.argv[1:]
    entries = index_rows()
    check_coverage(entries)
    check_links(entries)
    check_currency(entries)
    check_acceptance(gate)
    check_transfer(entries)
    for finding in findings:
        print(finding)
    if findings:
        return 1
    print(f"OK: {len(entries)} index rows cover the {len(DOCUMENTATION_TASKS)} Documentation-category tasks; every link "
          f"resolves; every stated status is current; acceptance is {'claimed with' if gate else 'not claimed without'} "
          "its evidence; Client Scope §12's five areas are covered")
    return 0


if __name__ == "__main__":
    sys.exit(main())
