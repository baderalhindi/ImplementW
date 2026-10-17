#!/usr/bin/env python3
"""Summarises a cross-domain journey run (TASK-085). Record: docs/testing/e2e-suite.md

    e2e-summary.py <results.json> <target> <summary.md>

Reads Playwright's JSON report and writes a Markdown summary: the target the suite ran against, the commit, and one row per
journey (UJ-001..UJ-015 and the ADR-013 participation specs) with its outcome and duration. A journey that should exist but
did not run is listed as NOT RUN, so a suite that silently lost a spec reads as incomplete rather than green. Exits 1 when
any journey failed or did not run, 0 otherwise; the summary is written either way.
"""
from __future__ import annotations

import json
import os
import sys
from pathlib import Path

EXPECTED = [f"UJ-{n:03d}" for n in range(1, 16)] + [
    "ADR-013 entity Project Manager journey",
    "ADR-013 denied cross-entity access",
]


def outcomes(report: dict) -> dict[str, tuple[str, float, str]]:
    """Title → (outcome, seconds, file) for every test in the report, suites flattened."""
    found: dict[str, tuple[str, float, str]] = {}

    def walk(suite: dict) -> None:
        for spec in suite.get("specs", []):
            for test in spec.get("tests", []):
                results = test.get("results", [])
                duration = sum(r.get("duration", 0) for r in results) / 1000
                status = test.get("status", "unknown")  # expected | unexpected | flaky | skipped
                outcome = {"expected": "PASS", "unexpected": "FAIL", "flaky": "FLAKY", "skipped": "SKIPPED"}.get(status, status.upper())
                # test.fail(): a known product gap. It counts as passing while it fails as expected; Playwright reports the
                # run red ("unexpected") the day it passes, so the mark is removed then.
                known = [a.get("description", "") for a in test.get("annotations", []) if a.get("type") == "fail"]
                if known and outcome == "PASS":
                    outcome = f"EXPECTED FAIL ({known[0]})"
                found[spec["title"]] = (outcome, duration, spec.get("file", ""))
        for child in suite.get("suites", []):
            walk(child)

    for suite in report.get("suites", []):
        walk(suite)
    return found


def main() -> int:
    if len(sys.argv) != 4:
        print(__doc__, file=sys.stderr)
        return 2
    results_path, target, summary_path = Path(sys.argv[1]), sys.argv[2], Path(sys.argv[3])
    report = json.loads(results_path.read_text(encoding="utf-8")) if results_path.is_file() else {}
    found = outcomes(report)

    rows, failed = [], 0
    for journey in EXPECTED:
        match = next(((title, result) for title, result in found.items() if title.startswith(journey)), None)
        if match is None:
            rows.append(f"| {journey} | — | **NOT RUN** | |")
            failed += 1
            continue
        title, (outcome, seconds, file) = match
        failed += not outcome.startswith(("PASS", "EXPECTED FAIL"))
        shown = outcome if outcome.startswith(("PASS", "EXPECTED FAIL")) else f"**{outcome}**"
        rows.append(f"| {title} | `{file}` | {shown} | {seconds:.0f} s |")
    extra = sorted(set(found) - {t for t in found for j in EXPECTED if t.startswith(j)})
    for title in extra:
        outcome, seconds, file = found[title]
        failed += not outcome.startswith(("PASS", "EXPECTED FAIL"))
        rows.append(f"| {title} | `{file}` | {outcome} | {seconds:.0f} s |")

    expected_failures = sum("EXPECTED FAIL" in row for row in rows)
    passed = len(rows) - failed - expected_failures
    commit = os.environ.get("GITHUB_SHA", "")[:12] or "local"
    if failed:
        verdict = f"**{failed} of {len(rows)} journeys did not pass**"
    elif expected_failures:
        verdict = f"no unexpected failure; {expected_failures} journey(s) fail as expected on a known product gap"
    else:
        verdict = "All journeys passed"
    summary = "\n".join(
        [
            f"### Cross-domain journeys — {verdict}",
            "",
            f"Target **{target}** · commit `{commit}` · {passed} passed, {expected_failures} expected to fail, {failed} not passed. "
            "A failing journey blocks promotion to UAT (docs/testing/e2e-suite.md §8).",
            "",
            "| Journey | Spec | Outcome | Duration |",
            "| --- | --- | --- | --- |",
            *rows,
            "",
        ]
    )
    summary_path.parent.mkdir(parents=True, exist_ok=True)
    summary_path.write_text(summary, encoding="utf-8")
    print(summary)
    return 0 if failed == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
