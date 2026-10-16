#!/usr/bin/env python3
"""TASK-084: the business-logic coverage gate (test-strategy.md §5).

Merges the Cobertura reports that every backend `dotnet test` step in CI writes (coverage.runsettings), line by line: a line is
covered when any test run executed it. Each source file of PMPlatform.Domain and PMPlatform.Application belongs to one module:

  PMPlatform.Domain/<Module>/...                  -> <Module>
  PMPlatform.Application/Features/<Module>/...    -> <Module>
  any other PMPlatform.Application file           -> Common

  CV-1  Every assembly coverage-thresholds.json names is in the reports.
  CV-2  Every module's line coverage is at least minimumPerModule.
  CV-3  The line coverage of all modules together is at least minimumTotal.

Exit 1 on any failure. With GITHUB_STEP_SUMMARY set, the table is also written to the job summary.

Run from the repository root: python3 docs/testing/coverage-check.py <report.cobertura.xml or directory> ...
"""

from __future__ import annotations

import json
import os
import re
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict
from pathlib import Path

THRESHOLDS = Path(__file__).resolve().parent / "coverage-thresholds.json"


def reports(arguments: list[str]) -> list[Path]:
    found: list[Path] = []
    for argument in arguments:
        path = Path(argument)
        found.extend(sorted(path.rglob("*.cobertura.xml")) if path.is_dir() else [path])
    return found


def module_of(assembly: str, filename: str) -> str | None:
    # Cobertura file names are absolute paths on the machine that ran the tests; only the part under the project matters.
    match = re.search(rf"[\\/]{re.escape(assembly)}[\\/](.+)$", filename)
    if match is None:
        return None
    parts = re.split(r"[\\/]", match.group(1))
    if assembly == "PMPlatform.Domain" and len(parts) > 1:
        return parts[0]
    if assembly == "PMPlatform.Application" and len(parts) > 2 and parts[0] == "Features":
        return parts[1]
    return "Common"


def merge(paths: list[Path], assemblies: set[str]) -> tuple[dict[tuple[str, str, int], bool], set[str]]:
    lines: dict[tuple[str, str, int], bool] = {}
    seen: set[str] = set()
    for path in paths:
        for package in ET.parse(path).getroot().iter("package"):
            assembly = package.get("name", "")
            if assembly not in assemblies:
                continue
            seen.add(assembly)
            for cls in package.iter("class"):
                module = module_of(assembly, cls.get("filename", ""))
                if module is None:
                    continue
                source = re.sub(r"^.*?[\\/]" + re.escape(assembly) + r"[\\/]", assembly + "/", cls.get("filename", ""))
                for line in cls.iter("line"):
                    key = (module, source, int(line.get("number", "0")))
                    lines[key] = lines.get(key, False) or int(line.get("hits", "0")) > 0
    return lines, seen


def percent(covered: int, valid: int) -> float:
    return 100.0 * covered / valid if valid else 100.0


def main(arguments: list[str]) -> int:
    if not arguments:
        print(__doc__)
        return 2
    policy = json.loads(THRESHOLDS.read_text(encoding="utf-8"))
    assemblies = set(policy["assemblies"])
    per_module, total_minimum = float(policy["minimumPerModule"]), float(policy["minimumTotal"])

    paths = reports(arguments)
    lines, seen = merge(paths, assemblies)
    failures = [f"CV-1 no coverage data for {name} in {len(paths)} report(s)" for name in sorted(assemblies - seen)]

    tally: dict[str, list[int]] = defaultdict(lambda: [0, 0])
    for (module, _, _), covered in lines.items():
        tally[module][0] += covered
        tally[module][1] += 1

    rows = ["| Module | Lines covered | Line coverage | Minimum | Result |", "| --- | --- | --- | --- | --- |"]
    for module in sorted(tally):
        covered, valid = tally[module]
        value = percent(covered, valid)
        passed = value >= per_module
        rows.append(f"| {module} | {covered} / {valid} | {value:.1f}% | {per_module:.0f}% | {'pass' if passed else 'FAIL'} |")
        if not passed:
            failures.append(f"CV-2 {module}: {value:.1f}% of {valid} lines, below {per_module:.0f}%")
    covered, valid = sum(c for c, _ in tally.values()), sum(v for _, v in tally.values())
    value = percent(covered, valid)
    rows.append(f"| **All modules** | {covered} / {valid} | {value:.1f}% | {total_minimum:.0f}% | {'pass' if value >= total_minimum else 'FAIL'} |")
    if value < total_minimum:
        failures.append(f"CV-3 all modules: {value:.1f}% of {valid} lines, below {total_minimum:.0f}%")

    table = "\n".join(rows)
    print(f"Business-logic line coverage from {len(paths)} report(s), {', '.join(sorted(assemblies))}:\n\n{table}\n")
    summary = os.environ.get("GITHUB_STEP_SUMMARY")
    if summary:
        with open(summary, "a", encoding="utf-8") as handle:
            handle.write(f"## Business-logic coverage (TASK-084)\n\n{len(paths)} report(s).\n\n{table}\n")
    for failure in failures:
        print(failure)
    print("coverage gate: " + ("FAIL" if failures else "pass"))
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
