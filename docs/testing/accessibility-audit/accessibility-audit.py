#!/usr/bin/env python3
"""TASK-087: record an accessibility audit run, and hold the audit's results and its remediation tracker together.

  python3 docs/testing/accessibility-audit/accessibility-audit.py --record <run name> [--note TEXT] [records dir]
      Reads the records src/frontend/e2e/accessibility/run.sh wrote (default src/frontend/test-results/accessibility: one JSON
      per screen and language) and writes results/<run name>.json — the run's figures the report cites — then prints the
      report's per-screen table in Markdown.
  python3 docs/testing/accessibility-audit/accessibility-audit.py --check
      repo-checks, every PR:
        AA-1  the latest recorded run covers every screen of src/frontend/e2e/accessibility/screens.ts in both locales, and at
              least 10 of them are top-traffic screens;
        AA-2  the latest run is clean on what the audit gates: no Critical/Serious WCAG 2.1 A/AA violation outside an
              AHDA-accepted deferral, no undecided contrast measured below 1.4.3, Tab reaches every control and comes back
              round on every top-traffic screen, no trap, no focus without a visible indicator, no focus escaping a modal or
              not returning on close, no content past 320 px outside its own scroll container;
        AA-3  every tracker row is well formed: a Closed row names the recorded run that verified it, a Deferred row names
              AHDA's acceptance, and accepted-deferrals.json lists exactly the Deferred rows' axe rules;
        AA-4  every run a tracker row names is recorded in results/, and the report names the latest run.
  python3 docs/testing/accessibility-audit/accessibility-audit.py --gate
      The UAT check (Release Checklist "Accessibility Audit Findings Closed"): --check, and no finding left Open.

Standard library only. Exit 1 on any finding.
"""

from __future__ import annotations

import csv
import json
import re
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent.parent.parent
RESULTS = HERE / "results"
TRACKER = HERE / "remediation-tracker.csv"
REPORT = HERE.parent / "accessibility-audit.md"
SCREENS = ROOT / "src/frontend/e2e/accessibility/screens.ts"
DEFERRALS = ROOT / "src/frontend/e2e/accessibility/accepted-deferrals.json"
RECORDS = ROOT / "src/frontend/test-results/accessibility"

LANGUAGES = ("ar", "en")
BLOCKING = ("critical", "serious")
TOP_TEN_REQUIRED = 10
TRACKER_COLUMNS = [
    "id", "kind", "title", "criterion", "level", "severity", "screens", "locales", "source",
    "found_in_run", "status", "resolution", "verified_in_run", "owner", "ahda_acceptance",
]
KINDS = ("Finding", "Advisory")
STATUSES = ("Open", "Closed", "Deferred")
SEVERITIES = ("Critical", "Serious", "Moderate", "Minor")


def screen_ids() -> list[str]:
    return re.findall(r"id: '(A11Y-S\d+)'", SCREENS.read_text(encoding="utf-8"))


def condense(record: dict) -> dict:
    """The figures a recorded run keeps of one screen in one language: counts and named defects, not the raw axe output."""
    axe, keyboard = record["axe"], record["keyboard"]
    return {
        "screen": record["screen"]["id"],
        "language": record["language"],
        "url": re.sub(r"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", "{id}", record["url"]),
        "title": record["title"],
        "state": record.get("state"),
        "axe": {
            "version": axe["axeVersion"],
            "passes": axe["passes"],
            "violations": [
                {"rule": v["rule"], "impact": v["impact"], "baseline": v["baseline"], "criteria": v["criteria"],
                 "nodes": len(v["targets"])}
                for v in axe["violations"]
            ],
            "incomplete": [{"rule": v["rule"], "impact": v["impact"], "nodes": v["nodes"]} for v in axe["incomplete"]],
        },
        "contrastReviewed": len(record.get("contrast", [])),
        "contrastBelow": [c for c in record.get("contrast", []) if not c["passes"]],
        "contrastMinimum": min((c["ratio"] for c in record.get("contrast", [])), default=None),
        "keyboard": {
            "controls": keyboard["controls"],
            "presses": keyboard["presses"],
            "cycled": keyboard["cycled"],
            "unreached": keyboard["unreached"],
            "noVisibleFocus": keyboard["noVisibleFocus"],
            "trapped": keyboard["trapped"],
            "escaped": keyboard.get("escaped", []),
        },
        "focusReturned": record.get("focusReturned"),
        "reflow": record.get("reflow"),
        "languageParts": record.get("languageParts", []),
    }


def git(*args: str) -> str:
    return subprocess.run(["git", *args], cwd=ROOT, capture_output=True, text=True, check=False).stdout.strip()


def record_run(name: str, note: str, records: Path) -> int:
    files = sorted(records.glob("A11Y-S*.json"))
    if not files:
        print(f"no records in {records}; run src/frontend/e2e/accessibility/run.sh first", file=sys.stderr)
        return 1
    screens = {}
    for path in files:
        data = json.loads(path.read_text(encoding="utf-8"))
        screens.setdefault(data["screen"]["id"], {"name": data["screen"]["name"], "scr": data["screen"]["scr"],
                                                  "basis": data["screen"]["basis"], "topTen": data["screen"]["topTen"],
                                                  "persona": data["screen"]["persona"], "languages": {}})
        screens[data["screen"]["id"]]["languages"][data["language"]] = condense(data)
    run = {
        "run": name,
        "note": note,
        "recordedAt": datetime.now(timezone.utc).isoformat(timespec="seconds"),
        "commit": git("rev-parse", "--short", "HEAD"),
        "workingTreeClean": git("status", "--porcelain") == "",
        "baseline": "WCAG 2.1 Level AA (working baseline, PTBC-040 / OQ-010)",
        "screens": screens,
    }
    RESULTS.mkdir(exist_ok=True)
    out = RESULTS / f"{name}.json"
    out.write_text(json.dumps(run, indent=1, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"wrote {out.relative_to(ROOT)}\n")
    print(table(run))
    return 0


def table(run: dict) -> str:
    lines = ["| Screen | Step 16 | Locale | axe passes | Violations (C/S/M/m) | Controls | Reached by Tab | Visible focus | Reflow 320 px |",
             "| --- | --- | --- | ---: | --- | ---: | --- | --- | --- |"]
    for sid, screen in sorted(run["screens"].items()):
        for lang in LANGUAGES:
            entry = screen["languages"].get(lang)
            if entry is None:
                continue
            impacts = [v["impact"] for v in entry["axe"]["violations"]]
            counts = "/".join(str(impacts.count(i)) for i in ("critical", "serious", "moderate", "minor"))
            kb = entry["keyboard"]
            reached = "all" if not kb["unreached"] else f"{kb['controls'] - len(kb['unreached'])} of {kb['controls']}"
            focus = "yes" if not kb["noVisibleFocus"] else f"{len(kb['noVisibleFocus'])} without"
            reflow = "yes" if entry["reflow"] and not entry["reflow"]["overflowing"] else "no"
            lines.append(f"| {sid} {screen['name']} | {screen['scr']} | {lang} | {entry['axe']['passes']} | {counts} | "
                         f"{kb['controls']} | {reached} | {focus} | {reflow} |")
    return "\n".join(lines)


def recorded_runs() -> dict[str, dict]:
    return {p.stem: json.loads(p.read_text(encoding="utf-8")) for p in sorted(RESULTS.glob("*.json"))}


def read_tracker() -> list[dict[str, str]]:
    with TRACKER.open(encoding="utf-8", newline="") as handle:
        reader = csv.DictReader(handle)
        if reader.fieldnames != TRACKER_COLUMNS:
            raise SystemExit(f"AA-3 {TRACKER.name} columns are {reader.fieldnames}, expected {TRACKER_COLUMNS}")
        return list(reader)


def check(gate: bool) -> int:
    failures: list[str] = []
    runs = recorded_runs()
    if not runs:
        print("AA-1 no recorded run in results/")
        return 1
    latest_name = max(runs)
    latest = runs[latest_name]
    deferrals = json.loads(DEFERRALS.read_text(encoding="utf-8"))
    deferred_rules = {(d["rule"], s) for d in deferrals for s in d["screens"]}

    # AA-1 coverage
    for sid in screen_ids():
        recorded = latest["screens"].get(sid, {}).get("languages", {})
        for lang in LANGUAGES:
            if lang not in recorded:
                failures.append(f"AA-1 {latest_name} has no record of {sid} in {lang}")
    top = [sid for sid, s in latest["screens"].items() if s["topTen"]]
    if len(top) < TOP_TEN_REQUIRED:
        failures.append(f"AA-1 {latest_name} audits {len(top)} top-traffic screens, the criterion needs {TOP_TEN_REQUIRED}")

    # AA-2 the latest run is clean on what the audit gates
    for sid, screen in sorted(latest["screens"].items()):
        for lang, entry in sorted(screen["languages"].items()):
            where = f"AA-2 {latest_name} {sid} [{lang}]"
            for v in entry["axe"]["violations"]:
                if v["baseline"] and v["impact"] in BLOCKING and (v["rule"], sid) not in deferred_rules:
                    failures.append(f"{where}: {v['impact']} {v['rule']} on {v['nodes']} node(s), not deferred by AHDA")
            for c in entry["contrastBelow"]:
                failures.append(f"{where}: contrast {c['ratio']}:1 below {c['required']}:1 at {c['selector']}")
            kb = entry["keyboard"]
            if screen["topTen"] and (kb["unreached"] or not kb["cycled"]):
                failures.append(f"{where}: Tab does not reach every control ({len(kb['unreached'])} unreached, cycled={kb['cycled']})")
            for key, what in (("trapped", "keyboard trap"), ("noVisibleFocus", "focus without a visible indicator"),
                              ("escaped", "focus escaping the modal")):
                if kb[key]:
                    failures.append(f"{where}: {what}: {', '.join(kb[key][:3])}")
            if entry["state"] and entry["state"].get("modal") and entry["focusReturned"] is not True:
                failures.append(f"{where}: focus does not return to the opener when the modal closes")
            if entry["reflow"] is None or entry["reflow"]["overflowing"]:
                failures.append(f"{where}: does not reflow at 320 px")

    # AA-3 tracker rows
    rows = read_tracker()
    ids = [r["id"] for r in rows]
    if len(ids) != len(set(ids)):
        failures.append("AA-3 tracker ids repeat")
    deferred_ids = set()
    for r in rows:
        where = f"AA-3 {r['id']}"
        if not re.fullmatch(r"A11Y-F-\d{3}", r["id"]):
            failures.append(f"{where}: id is not A11Y-F-nnn")
        if r["kind"] not in KINDS:
            failures.append(f"{where}: kind {r['kind']!r} is not one of {KINDS}")
        if r["status"] not in STATUSES:
            failures.append(f"{where}: status {r['status']!r} is not one of {STATUSES}")
        if r["severity"] not in SEVERITIES:
            failures.append(f"{where}: severity {r['severity']!r} is not one of {SEVERITIES}")
        for field in ("title", "criterion", "screens", "locales", "source", "found_in_run", "resolution", "owner"):
            if r[field].strip() == "":
                failures.append(f"{where}: {field} is empty")
        if r["status"] == "Closed" and r["verified_in_run"].strip() == "":
            failures.append(f"{where}: Closed without the run that verified it")
        if r["status"] == "Deferred":
            deferred_ids.add(r["id"])
            if r["ahda_acceptance"].strip() == "":
                failures.append(f"{where}: Deferred without AHDA's acceptance")
        if r["status"] != "Deferred" and r["ahda_acceptance"].strip() != "" and r["status"] != "Closed":
            failures.append(f"{where}: names an AHDA acceptance but is {r['status']}")
        # AA-4 named runs exist
        for field in ("found_in_run", "verified_in_run"):
            name = r[field].strip()
            if name not in ("", "manual review") and name not in runs:
                failures.append(f"AA-4 {r['id']}: {field} {name!r} is not a recorded run in results/")
        if gate and r["kind"] == "Finding" and r["status"] == "Open":
            failures.append(f"GATE {r['id']} is Open: {r['title']}")
    listed = {d["finding"] for d in deferrals}
    for finding in sorted(listed - deferred_ids):
        failures.append(f"AA-3 accepted-deferrals.json lists {finding}, which is not a Deferred tracker row")
    for finding in sorted(deferred_ids - listed):
        row = next(r for r in rows if r["id"] == finding)
        if row["source"].startswith("axe"):
            failures.append(f"AA-3 {finding} is a Deferred axe finding missing from accepted-deferrals.json")

    # AA-4 the report is current
    if latest_name not in REPORT.read_text(encoding="utf-8"):
        failures.append(f"AA-4 {REPORT.name} does not name the latest recorded run {latest_name}")

    for failure in failures:
        print(failure)
    if not failures:
        open_findings = sum(1 for r in rows if r["kind"] == "Finding" and r["status"] == "Open")
        print(f"accessibility audit: {latest_name}, {len(latest['screens'])} screens x {len(LANGUAGES)} locales clean; "
              f"tracker {len(rows)} rows, {open_findings} finding(s) open" + (" — gate passes" if gate else ""))
    return 1 if failures else 0


def main(argv: list[str]) -> int:
    if argv[:1] == ["--record"] and len(argv) >= 2:
        name, rest = argv[1], argv[2:]
        note = ""
        if rest[:1] == ["--note"]:
            note, rest = rest[1], rest[2:]
        return record_run(name, note, Path(rest[0]) if rest else RECORDS)
    if argv == ["--check"]:
        return check(gate=False)
    if argv == ["--gate"]:
        return check(gate=True)
    print(__doc__, file=sys.stderr)
    return 2


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
