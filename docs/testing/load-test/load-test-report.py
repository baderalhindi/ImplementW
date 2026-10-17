#!/usr/bin/env python3
"""TASK-086: summarise a load-test run, judge it against the latency budget, and hold every breach to a backlog row.

  python3 docs/testing/load-test/load-test-report.py <results dir>
      Reads a run of run.sh (requests.csv.gz, statements.csv, resources.csv), writes <results dir>/stats.json — the per-endpoint
      figures a recorded run keeps in docs/testing/load-test/results/ — and prints the report's tables in Markdown.
  python3 docs/testing/load-test/load-test-report.py --judge <stats.json>
      Prints the tables of a recorded run again, against budget.json as it is now (e.g. once AHDA confirms PTBC-043).
  python3 docs/testing/load-test/load-test-report.py --check
      repo-checks, every PR:
        LT-1  every recorded run in results/ judges cleanly against budget.json: each endpoint within its group's p95 and
              the error-rate ceiling, or named in the optimisation backlog of docs/testing/load-test.md (§8) with a filed
              GitHub issue (#n) and a status;
        LT-2  every backlog row names an endpoint some recorded run measured (no stale rows);
        LT-3  every register endpoint the baseline map names has a row in docs/architecture/register-query-baseline.csv.

Standard library only. Exit 1 on any finding, or, for a run, on any breach with no backlog row.
"""

from __future__ import annotations

import csv
import gzip
import json
import math
import re
import sys
from collections import defaultdict
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent.parent.parent
BUDGET = HERE / "budget.json"
RESULTS = HERE / "results"
RECORD = HERE.parent / "load-test.md"
BASELINE = ROOT / "docs/architecture/register-query-baseline.csv"

JUDGED_PHASES = ("normal", "peak")

# The TASK-026 baseline queries behind each request the SPA sends, by the caller's scope ("*": any scope): the request's SQL
# floor is its page plus its count (R-29), measured by RegisterQueryPlanTests at the same volume (indexing-strategy.md §5).
# The cross-project registers send one request per project (load-test.md F-1), so their floor is the project-tab query.
# Not mapped, because no baseline query is the one the API runs: the five-state `GET /projects?status` the cross-project
# registers start with, the per-project concern read, documents below ALL scope, and every dashboard and report.
BASELINE_MAP: dict[tuple[str, str], tuple[str, ...]] = {
    ("GET /projects", "ALL"): ("SCR-025 ALL page", "SCR-025 ALL count"),
    ("GET /projects", "DEPT"): ("SCR-025 DEPT page", "SCR-025 DEPT count"),
    ("GET /projects", "ENTITY"): ("SCR-025 ENTITY page", "SCR-025 ENTITY count"),
    ("GET /projects?projectManagerUserId", "OWN"): ("SCR-026 OWN page", "SCR-026 OWN count"),
    ("GET /risks?projectId", "*"): ("SCR-080 project tab page",),
    ("GET /change-requests?projectId", "*"): ("SCR-105 project page",),
    ("GET /approval-tasks", "*"): ("SCR-100 personal page", "SCR-100 personal count", "SCR-100 role queue page"),
    ("GET /notifications", "*"): ("SCR-150 first page",),
    ("GET /notifications/unread-count", "*"): ("SCR-150 unread badge",),
    ("GET /documents", "ALL"): ("SCR-120 ALL page", "SCR-120 ALL count"),
}


def baseline_names(endpoint: str, scope: str) -> tuple[str, ...] | None:
    return BASELINE_MAP.get((endpoint, scope)) or BASELINE_MAP.get((endpoint, "*"))


def percentile(sorted_values: list[float], p: float) -> float:
    """Nearest-rank percentile, as k6 and most load tools report it."""
    if not sorted_values:
        return math.nan
    rank = max(1, math.ceil(p / 100 * len(sorted_values)))
    return sorted_values[rank - 1]


def parse_tags(extra: str) -> dict[str, str]:
    return dict(pair.split("=", 1) for pair in extra.split("&") if "=" in pair)


def summarise(run: Path) -> dict:
    durations: dict[tuple[str, str, str, str], list[float]] = defaultdict(list)
    errors: dict[tuple[str, str, str, str], int] = defaultdict(int)
    denied: dict[tuple[str, str, str, str], int] = defaultdict(int)
    statuses: dict[tuple[str, str, str, str], dict[str, int]] = defaultdict(lambda: defaultdict(int))
    jobs: dict[str, list[float]] = defaultdict(list)
    first = last = None
    with gzip.open(run / "requests.csv.gz", "rt", encoding="utf-8", newline="") as stream:
        for row in csv.DictReader(stream):
            tags = parse_tags(row.get("extra_tags") or "")
            phase = tags.get("phase", "")
            if row["metric_name"] == "report_job_completion":
                jobs[phase].append(float(row["metric_value"]))
                continue
            if row["metric_name"] not in ("http_req_duration", "screen_duration") or "endpoint" not in tags:
                continue
            stamp = int(row["timestamp"])
            first = stamp if first is None else min(first, stamp)
            last = stamp if last is None else max(last, stamp)
            key = (phase, tags["endpoint"], tags.get("budget", ""), tags.get("scope", ""))
            status = row["status"]
            statuses[key][status or "screen"] += 1
            # Latency is judged on answered requests only: a refusal is fast and would flatter p95. A 403 is a denial by the
            # permission matrix (load-test.md F-4), counted apart; anything else that is not 2xx is an error.
            if row["metric_name"] == "screen_duration" or status.startswith("2"):
                durations[key].append(float(row["metric_value"]))
            elif status == "403":
                denied[key] += 1
            else:
                errors[key] += 1
    endpoints = []
    for key in sorted(statuses):
        phase, endpoint, budget, scope = key
        values = sorted(durations[key])
        if not values:
            values = [math.nan]
        endpoints.append(
            {
                "phase": phase,
                "endpoint": endpoint,
                "budget": budget,
                "scope": scope,
                "count": sum(statuses[key].values()),
                "errors": errors[key],
                "denied": denied[key],
                "statuses": dict(sorted(statuses[key].items())),
                "p50": round(percentile(values, 50), 1),
                "p95": round(percentile(values, 95), 1),
                "p99": round(percentile(values, 99), 1),
                "max": round(values[-1], 1),
            }
        )
    for e in endpoints:
        for k in ("p50", "p95", "p99", "max"):
            if math.isnan(e[k]):
                e[k] = None
    report_jobs = {}
    for phase, values in jobs.items():
        values.sort()
        report_jobs[phase] = {"count": len(values), "p50": round(percentile(values, 50), 1), "p95": round(percentile(values, 95), 1),
                              "max": round(values[-1], 1)}
    return {
        "run": run.name,
        "seconds": None if first is None else last - first,
        "endpoints": endpoints,
        "reportJobs": report_jobs,
        "statements": top_statements(run / "statements.csv"),
        "resources": resources(run / "resources.csv"),
    }


def top_statements(path: Path, limit: int = 15) -> list[dict]:
    if not path.exists():
        return []
    with path.open(encoding="utf-8", newline="") as stream:
        rows = list(csv.DictReader(stream))
    return [
        {"calls": int(r["calls"]), "totalMs": float(r["total_ms"]), "meanMs": float(r["mean_ms"]), "maxMs": float(r["max_ms"]),
         "query": r["query"][:400]}
        for r in rows[:limit]
    ]


def resources(path: Path) -> dict:
    """Peak and median CPU per container. docker stats reports CPU as a percentage of one core."""
    if not path.exists():
        return {}
    cpu: dict[str, list[float]] = defaultdict(list)
    with path.open(encoding="utf-8", newline="") as stream:
        for row in csv.DictReader(stream):
            try:
                cpu[row["container"]].append(float(row["cpu_percent"].rstrip("%")))
            except (KeyError, ValueError):
                continue
    return {name: {"cpuMedian": round(percentile(sorted(v), 50), 1), "cpuMax": round(max(v), 1)} for name, v in sorted(cpu.items())}


def load_budget() -> dict:
    return json.loads(BUDGET.read_text(encoding="utf-8"))


def load_baseline() -> dict[str, float]:
    with BASELINE.open(encoding="utf-8", newline="") as stream:
        return {row["query"]: float(row["baseline_ms"]) for row in csv.DictReader(stream)}


def load_backlog() -> dict[str, tuple[str, str]]:
    """Endpoint → (issue, status) from the optimisation backlog table of load-test.md (§8)."""
    text = RECORD.read_text(encoding="utf-8") if RECORD.exists() else ""
    section = re.search(r"^## 8\. Optimisation backlog\n(.*?)(?=^## )", text, re.S | re.M)
    backlog = {}
    for line in (section.group(1) if section else "").splitlines():
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        if len(cells) >= 5 and re.fullmatch(r"P-\d+", cells[0]):
            endpoint = cells[1].strip("`")
            issue = re.search(r"#\d+", cells[3])
            backlog[endpoint] = (issue.group(0) if issue else "", cells[4])
    return backlog


def breaches(stats: dict, budget: dict) -> list[tuple[dict, str]]:
    found = []
    for e in stats["endpoints"]:
        if e["phase"] not in JUDGED_PHASES:
            continue
        group = budget["groups"].get(e["budget"])
        if group and e["p95"] is not None and e["p95"] > group["p95Ms"]:
            found.append((e, f"p95 {e['p95']:.0f} ms > {group['p95Ms']} ms"))
        if e["count"] and e["errors"] / e["count"] > budget["maxErrorRate"]:
            found.append((e, f"errors {e['errors']}/{e['count']} > {budget['maxErrorRate']:.0%}"))
    for phase, job in stats.get("reportJobs", {}).items():
        group = budget["groups"].get("report-job")
        if phase in JUDGED_PHASES and group and job["p95"] > group["p95Ms"]:
            found.append(({"phase": phase, "endpoint": "report job (queued → file)", "scope": ""}, f"p95 {job['p95']:.0f} ms > {group['p95Ms']} ms"))
    return found


def ms(value: float | None) -> str:
    if value is None:
        return "—"
    return f"{value:,.0f}" if value >= 10 else f"{value:.1f}"


def render(stats: dict, budget: dict) -> str:
    baseline = load_baseline()
    backlog = load_backlog()
    breached = {(e["phase"], e["endpoint"], e.get("scope", "")): why for e, why in breaches(stats, budget)}
    out = [f"### Run `{stats['run']}` — {stats['seconds'] or 0:,} s of requests; budget {budget['status']}", ""]
    for phase in JUDGED_PHASES:
        rows = [e for e in stats["endpoints"] if e["phase"] == phase]
        if not rows:
            continue
        total = sum(e["count"] for e in rows)
        out += [f"#### Phase `{phase}` — {total:,} requests", "",
                "| Endpoint | Scope | Group | Requests | Denied | Errors | p50 ms | p95 ms | p99 ms | max ms | Budget p95 | Verdict |",
                "| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |"]
        for e in rows:
            group = budget["groups"].get(e["budget"], {})
            why = breached.get((phase, e["endpoint"], e["scope"]))
            verdict = "within"
            if why:
                verdict = f"**BREACH** ({why})" + (f", backlog {backlog[e['endpoint']][0]}" if e["endpoint"] in backlog else ", **no backlog row**")
            out.append(f"| `{e['endpoint']}` | {e['scope']} | {e['budget']} | {e['count']:,} | {e.get('denied', 0)} | {e['errors']} | {ms(e['p50'])} | {ms(e['p95'])} | "
                       f"{ms(e['p99'])} | {ms(e['max'])} | {group.get('p95Ms', '—')} | {verdict} |")
        out.append("")
    if stats.get("reportJobs"):
        out += ["#### Report jobs, queued to file", "", "| Phase | Jobs | p50 ms | p95 ms | max ms | Budget p95 |", "| --- | ---: | ---: | ---: | ---: | ---: |"]
        for phase, job in sorted(stats["reportJobs"].items()):
            out.append(f"| {phase} | {job['count']} | {ms(job['p50'])} | {ms(job['p95'])} | {ms(job['max'])} | {budget['groups']['report-job']['p95Ms']} |")
        out.append("")
    out += ["#### Against the TASK-026 baseline (peak)", "",
            "| Endpoint | Scope | Baseline queries | SQL floor ms | API p50 ms | API p95 ms | p95 / floor |", "| --- | --- | --- | ---: | ---: | ---: | ---: |"]
    for e in stats["endpoints"]:
        names = baseline_names(e["endpoint"], e["scope"])
        if e["phase"] != "peak" or not names or e["p95"] is None:
            continue
        floor = sum(baseline[n] for n in names)
        out.append(f"| `{e['endpoint']}` | {e['scope']} | {'; '.join(names)} | {floor:.3f} | {ms(e['p50'])} | {ms(e['p95'])} | {e['p95'] / floor:,.0f}× |")
    out.append("")
    if stats.get("statements"):
        out += ["#### Costliest statements during the run (pg_stat_statements)", "", "| Calls | Total ms | Mean ms | Max ms | Statement |", "| ---: | ---: | ---: | ---: | --- |"]
        for s in stats["statements"][:10]:
            query = s["query"].replace("|", "\\|")
            out.append(f"| {s['calls']:,} | {s['totalMs']:,.0f} | {s['meanMs']:.2f} | {s['maxMs']:.1f} | `{query[:160]}…` |")
        out.append("")
    if stats.get("resources"):
        out += ["#### Containers (CPU, % of one core)", "", "| Container | median | max |", "| --- | ---: | ---: |"]
        for name, r in stats["resources"].items():
            out.append(f"| {name} | {r['cpuMedian']} | {r['cpuMax']} |")
        out.append("")
    return "\n".join(out)


def check() -> list[str]:
    findings = []
    budget = load_budget()
    backlog = load_backlog()
    measured = set()
    runs = sorted(RESULTS.glob("*.json"))
    if not runs:
        findings.append("LT-1 no recorded run in docs/testing/load-test/results/")
    for path in runs:
        stats = json.loads(path.read_text(encoding="utf-8"))
        measured |= {e["endpoint"] for e in stats["endpoints"]} | {"report job (queued → file)"}
        for e, why in breaches(stats, budget):
            issue, status = backlog.get(e["endpoint"], ("", ""))
            if not issue or not status:
                findings.append(f"LT-1 {path.name}: {e['endpoint']} ({e['phase']}, {e.get('scope') or 'any scope'}) {why}, and the backlog has no row with an issue and a status")
    for endpoint in backlog:
        if endpoint not in measured:
            findings.append(f"LT-2 backlog row for {endpoint}, which no recorded run measured")
    baseline = load_baseline()
    for key, names in BASELINE_MAP.items():
        for name in names:
            if name not in baseline:
                findings.append(f"LT-3 {key[0]} ({key[1]}) maps to baseline query '{name}', which register-query-baseline.csv lacks")
    return findings


def main(argv: list[str]) -> int:
    if argv == ["--check"]:
        findings = check()
        for f in findings:
            print(f)
        print(f"{len(findings)} finding(s)")
        return 1 if findings else 0
    if len(argv) == 2 and argv[0] == "--judge":
        stats = json.loads(Path(argv[1]).read_text(encoding="utf-8"))
    elif len(argv) == 1:
        run = Path(argv[0])
        stats = summarise(run)
        (run / "stats.json").write_text(json.dumps(stats, indent=1, ensure_ascii=False) + "\n", encoding="utf-8")
    else:
        print(__doc__)
        return 2
    budget = load_budget()
    print(render(stats, budget))
    backlog = load_backlog()
    unfiled = [(e, why) for e, why in breaches(stats, budget) if e["endpoint"] not in backlog]
    for e, why in unfiled:
        print(f"BREACH without a backlog row: {e['endpoint']} ({e['phase']}, {e.get('scope') or 'any scope'}) {why}", file=sys.stderr)
    return 1 if unfiled else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
