#!/usr/bin/env python3
"""TASK-081: the threat models, the findings log and the risk-acceptance register agree, and (with --gate) the UAT gate.

Without arguments (repo-checks, every PR):
  TM-1  Every threat row is well formed: STRIDE letters, L and I in 1..3, the inherent rating equals L x I, a known status.
  TM-2  A MITIGATED threat has a residual rating of Medium or lower; only a NOT BUILT threat has no residual rating.
  TM-3  Every threat rated High or Critical inherent, and every threat not MITIGATED, names a finding.
  FL-1  Every finding exists for the threats that name it, and names them back; threat ids are unique across the models.
  FL-2  A finding's rating is the highest inherent rating among its threats.
  FL-3  A Mitigated finding cites a PR, and none of its threats is GAP or NOT BUILT or has a residual rating above Medium;
        a Risk accepted finding cites an RA record that exists and covers it.
  RA-1  A Signed acceptance names who accepted it, when, and where the signed copy is.

--gate (the Release Checklist's "Security Review & Threat Modeling Signed Off"):
  G-1   Every High or Critical finding is Mitigated, or Risk accepted under a Signed RA record. Exit 1 lists the rest.
--verify-prs (with --gate): every PR a Mitigated finding cites is MERGED on GitHub (needs an authenticated `gh`).

Run from the repository root: python3 docs/security/findings-log-check.py [--gate [--verify-prs]]
"""

from __future__ import annotations

import json
import re
import subprocess
import sys
from dataclasses import dataclass, field
from pathlib import Path

ROOT = Path(__file__).resolve().parent
RATINGS = ["Low", "Medium", "High", "Critical"]
STATUSES = {"MITIGATED", "PARTIAL", "GAP", "DEPENDS", "NOT BUILT"}
DISPOSITIONS = {"Mitigated", "Risk accepted", "Open"}
NONE = {"—", "-", ""}


def rating_of(score: int) -> str:
    return "Low" if score <= 2 else "Medium" if score <= 4 else "High" if score <= 6 else "Critical"


def rank(rating: str) -> int:
    return RATINGS.index(rating)


def cells(line: str) -> list[str]:
    # A table row; "\|" inside a cell is an escaped pipe.
    parts = re.split(r"(?<!\\)\|", line.strip())[1:-1]
    return [p.strip() for p in parts]


def ids(text: str, prefix: str) -> list[str]:
    return re.findall(rf"\b{prefix}-\d+\b", text)


@dataclass
class Threat:
    id: str
    source: str
    inherent: str
    residual: str
    status: str
    findings: list[str]


@dataclass
class Finding:
    id: str
    threats: list[str]
    rating: str
    disposition: str
    evidence: str
    prs: list[int] = field(default_factory=list)
    acceptances: list[str] = field(default_factory=list)


@dataclass
class Acceptance:
    id: str
    status: str
    covers: list[str]
    fields: dict[str, str]


def read_threats(errors: list[str]) -> dict[str, Threat]:
    threats: dict[str, Threat] = {}
    models = sorted(ROOT.glob("threat-model-*.md"))
    if not models:
        errors.append("TM-1: no threat-model-*.md in docs/security")
    for model in models:
        for number, line in enumerate(model.read_text(encoding="utf-8").splitlines(), 1):
            if not re.match(r"^\|\s*[A-Z]+-T\d+\s*\|", line):
                continue
            where = f"{model.name}:{number}"
            row = cells(line)
            if len(row) != 10:
                errors.append(f"TM-1: {where}: a threat row has 10 columns, this one has {len(row)}")
                continue
            tid, stride, _, likelihood, impact, inherent, _, residual, status, finding = row
            if tid in threats:
                errors.append(f"FL-1: {where}: {tid} is also defined in {threats[tid].source}")
            if not re.fullmatch(r"[STRIDE](/[STRIDE])*", stride):
                errors.append(f"TM-1: {where}: {tid}: STRIDE is '{stride}'")
            if likelihood not in {"1", "2", "3"} or impact not in {"1", "2", "3"}:
                errors.append(f"TM-1: {where}: {tid}: L and I must be 1, 2 or 3 (got {likelihood}, {impact})")
                continue
            expected = rating_of(int(likelihood) * int(impact))
            if inherent != expected:
                errors.append(f"TM-1: {where}: {tid}: inherent is '{inherent}', L x I gives '{expected}'")
            if status not in STATUSES:
                errors.append(f"TM-1: {where}: {tid}: status '{status}' is not one of {sorted(STATUSES)}")
            if residual in NONE:
                if status != "NOT BUILT":
                    errors.append(f"TM-2: {where}: {tid}: only a NOT BUILT threat may have no residual rating")
            elif residual not in RATINGS:
                errors.append(f"TM-2: {where}: {tid}: residual '{residual}' is not a rating")
            elif rank(residual) > rank(expected):
                errors.append(f"TM-2: {where}: {tid}: residual '{residual}' is above inherent '{expected}'")
            if status == "MITIGATED" and residual in RATINGS and rank(residual) > rank("Medium"):
                errors.append(f"TM-2: {where}: {tid}: MITIGATED with residual '{residual}'")
            linked = ids(finding, "SR")
            if (rank(expected) >= rank("High") or status != "MITIGATED") and not linked:
                errors.append(f"TM-3: {where}: {tid} ({expected}, {status}) names no finding")
            threats[tid] = Threat(tid, where, expected, residual, status, linked)
    return threats


def read_findings(errors: list[str]) -> dict[str, Finding]:
    log = ROOT / "findings-log.md"
    findings: dict[str, Finding] = {}
    for number, line in enumerate(log.read_text(encoding="utf-8").splitlines(), 1):
        if not re.match(r"^\|\s*SR-\d+\s*\|", line):
            continue
        where = f"findings-log.md:{number}"
        row = cells(line)
        if len(row) != 7:
            errors.append(f"FL-1: {where}: a finding row has 7 columns, this one has {len(row)}")
            continue
        fid, threat_cell, rating, _, _, disposition, evidence = row
        if fid in findings:
            errors.append(f"FL-1: {where}: {fid} appears twice")
        if disposition not in DISPOSITIONS:
            errors.append(f"FL-3: {where}: {fid}: disposition '{disposition}' is not one of {sorted(DISPOSITIONS)}")
        findings[fid] = Finding(
            fid,
            re.findall(r"\b[A-Z]+-T\d+\b", threat_cell),
            rating,
            disposition,
            evidence,
            [int(n) for n in re.findall(r"PR #(\d+)", evidence)],
            ids(evidence, "RA"),
        )
    return findings


def read_acceptances(errors: list[str]) -> dict[str, Acceptance]:
    register = ROOT / "risk-acceptances.md"
    acceptances: dict[str, Acceptance] = {}
    current: Acceptance | None = None
    for line in register.read_text(encoding="utf-8").splitlines():
        heading = re.match(r"^###\s+(RA-\d+)\b", line)
        if heading:
            current = Acceptance(heading.group(1), "", [], {})
            acceptances[current.id] = current
            continue
        if current is None or not line.startswith("|"):
            continue
        row = cells(line)
        if len(row) == 2:
            key, value = row
            current.fields[key] = value
            if key == "Status":
                current.status = value.strip("*")
            if key == "Findings":
                current.covers = ids(value, "SR")
    for acceptance in acceptances.values():
        if acceptance.status not in {"Pending AHDA signature", "Signed"}:
            errors.append(f"RA-1: {acceptance.id}: status '{acceptance.status}' is neither 'Pending AHDA signature' nor 'Signed'")
        if acceptance.status == "Signed":
            for key in ("Accepted by", "Date signed", "Signed copy"):
                if acceptance.fields.get(key, "").strip() in NONE:
                    errors.append(f"RA-1: {acceptance.id} is Signed but '{key}' is empty")
    return acceptances


def cross_check(threats, findings, acceptances, errors: list[str]) -> None:
    for threat in threats.values():
        for fid in threat.findings:
            if fid not in findings:
                errors.append(f"FL-1: {threat.source}: {threat.id} names {fid}, which is not in the findings log")
            elif threat.id not in findings[fid].threats:
                errors.append(f"FL-1: {fid} does not name {threat.id}, which names it ({threat.source})")
    for finding in findings.values():
        if not finding.threats:
            errors.append(f"FL-1: {finding.id} names no threat")
            continue
        missing = [t for t in finding.threats if t not in threats]
        for tid in missing:
            errors.append(f"FL-1: {finding.id} names {tid}, which no threat model defines")
        known = [threats[t] for t in finding.threats if t in threats]
        for threat in known:
            if finding.id not in threat.findings:
                errors.append(f"FL-1: {finding.id} names {threat.id}, which does not name it back ({threat.source})")
        if known:
            highest = max((t.inherent for t in known), key=rank)
            if finding.rating != highest:
                errors.append(f"FL-2: {finding.id}: rating '{finding.rating}', its threats' highest inherent is '{highest}'")
        if finding.disposition == "Mitigated":
            if not finding.prs:
                errors.append(f"FL-3: {finding.id} is Mitigated but cites no 'PR #n'")
            for threat in known:
                if threat.residual not in RATINGS or rank(threat.residual) > rank("Medium"):
                    errors.append(f"FL-3: {finding.id} is Mitigated but {threat.id}'s residual is '{threat.residual}'")
                if threat.status in {"GAP", "NOT BUILT"}:
                    errors.append(f"FL-3: {finding.id} is Mitigated but {threat.id} is {threat.status}")
        if finding.disposition == "Risk accepted":
            if not finding.acceptances:
                errors.append(f"FL-3: {finding.id} is Risk accepted but cites no RA record")
            for raid in finding.acceptances:
                if raid not in acceptances:
                    errors.append(f"FL-3: {finding.id} cites {raid}, which is not in risk-acceptances.md")
                elif finding.id not in acceptances[raid].covers:
                    errors.append(f"FL-3: {finding.id} cites {raid}, whose Findings row does not name it")
    for acceptance in acceptances.values():
        for fid in acceptance.covers:
            if fid not in findings:
                errors.append(f"RA-1: {acceptance.id} covers {fid}, which is not in the findings log")


def pr_state(number: int) -> str:
    result = subprocess.run(
        ["gh", "pr", "view", str(number), "--json", "state"], capture_output=True, text=True, check=False
    )
    if result.returncode != 0:
        return f"unknown ({result.stderr.strip() or 'gh failed'})"
    return json.loads(result.stdout)["state"]


def gate(findings, acceptances, verify_prs: bool) -> list[str]:
    blocking: list[str] = []
    for finding in findings.values():
        if finding.rating not in {"High", "Critical"}:
            continue
        if finding.disposition == "Mitigated" and finding.prs:
            if verify_prs:
                unmerged = [f"PR #{n} is {state}" for n in finding.prs if (state := pr_state(n)) != "MERGED"]
                if unmerged:
                    blocking.append(f"{finding.id} ({finding.rating}): {', '.join(unmerged)}")
            continue
        if finding.disposition == "Risk accepted" and finding.acceptances and all(
            acceptances.get(raid) and acceptances[raid].status == "Signed" for raid in finding.acceptances
        ):
            continue
        reason = (
            f"{', '.join(finding.acceptances)} not signed"
            if finding.disposition == "Risk accepted"
            else "Open"
        )
        blocking.append(f"{finding.id} ({finding.rating}): {reason}")
    return blocking


def main(argv: list[str]) -> int:
    unknown = [a for a in argv if a not in {"--gate", "--verify-prs"}]
    if unknown or ("--verify-prs" in argv and "--gate" not in argv):
        print(__doc__)
        return 2
    errors: list[str] = []
    threats = read_threats(errors)
    findings = read_findings(errors)
    acceptances = read_acceptances(errors)
    cross_check(threats, findings, acceptances, errors)
    for error in errors:
        print(f"FAIL {error}")
    if errors:
        print(f"{len(errors)} consistency error(s) in docs/security")
        return 1
    high = sum(1 for f in findings.values() if f.rating in {"High", "Critical"})
    print(
        f"OK: {len(threats)} threats in {len(set(t.source.split(':')[0] for t in threats.values()))} models, "
        f"{len(findings)} findings ({high} High/Critical), {len(acceptances)} risk-acceptance records"
    )
    if "--gate" not in argv:
        return 0
    blocking = gate(findings, acceptances, "--verify-prs" in argv)
    for line in blocking:
        print(f"GATE {line}")
    if blocking:
        print(f"UAT gate NOT green: {len(blocking)} High/Critical finding(s) without a merged mitigation or a signed acceptance")
        return 1
    print("UAT gate green: every High/Critical finding has a merged mitigation or a signed AHDA risk acceptance")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
