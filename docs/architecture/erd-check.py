#!/usr/bin/env python3
"""Consistency check for the canonical ERD (TASK-008).

Verifies erd.dbml, erd.md and erd-appendix-crosswalk.csv against each other and against the
conventions in erd.md §4 (D-1, D-2, D-4, D-5, D-7, D-8, D-14). Exit code 1 on any finding.

    python3 docs/architecture/erd-check.py
"""
import csv
import re
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
DBML = HERE / "erd.dbml"
MD = HERE / "erd.md"
CSV = HERE / "erd-appendix-crosswalk.csv"

AUDIT = ["created_at", "created_by", "updated_at", "updated_by"]
POLICIES = {"APPEND_ONLY", "RETAIN", "HARD_DRAFT", "HARD_OWNER", "HARD_WORKING", "CASCADE"}
MONEY = "numeric(18,2)"

findings = []


def find(msg):
    findings.append(msg)


def pascal(name):
    return "".join(p.capitalize() for p in name.split("_"))


# ---------------------------------------------------------------- parse DBML
tables = {}  # "schema.table" -> {"cols": [(name, type, attrs)], "note": str}
current = None
for raw in DBML.read_text().splitlines():
    line = raw.strip()
    m = re.match(r"^Table (\w+)\.(\w+) \{$", line)
    if m:
        current = f"{m.group(1)}.{m.group(2)}"
        tables[current] = {"cols": [], "note": "", "in_indexes": False}
        continue
    if current is None:
        continue
    if line == "}":
        if tables[current]["in_indexes"]:
            tables[current]["in_indexes"] = False
        else:
            current = None
        continue
    if line.startswith("indexes {"):
        tables[current]["in_indexes"] = True
        continue
    if tables[current]["in_indexes"]:
        continue
    if line.startswith("Note:"):
        tables[current]["note"] = line[5:].strip()
        continue
    m = re.match(r"^(\w+) (\S+)(?: \[(.*)\])?$", line)
    if m:
        tables[current]["cols"].append((m.group(1), m.group(2), m.group(3) or ""))

if not tables:
    find("no tables parsed from erd.dbml")

# ---------------------------------------------------------------- parse erd.md registers, diagrams, derived register
md = MD.read_text()
register = {}  # "schema.table" -> (entity, policy, referenceable)
for m in re.finditer(r"^\| \*\*(\w+)\*\* \| `(\w+)\.(\w+)` \| .* \| (\w+) \| (referenceable|—) \|$", md, re.M):
    register[f"{m.group(2)}.{m.group(3)}"] = (m.group(1), m.group(4), m.group(5) == "referenceable")

diagram_entities = set()
for block in re.findall(r"```mermaid\n(.*?)```", md, re.S):
    for m in re.finditer(r"^\s+(\w+) \{$", block, re.M):
        diagram_entities.add(m.group(1))

derived = re.findall(r"^\| \d+ \| `(\w+)\.(\w+)` \| ", md, re.M)

entities = {pascal(k.split(".")[1]): k for k in tables}

# ---------------------------------------------------------------- checks
for key, t in tables.items():
    schema, name = key.split(".")
    cols = t["cols"]
    names = [c[0] for c in cols]
    # D-1
    if not cols or cols[0][0] != "id" or cols[0][1] != "uuid" or "pk" not in cols[0][2]:
        find(f"{key}: D-1 first column must be `id uuid [pk]`")
    # D-2
    for a in AUDIT:
        col = next((c for c in cols if c[0] == a), None)
        if col is None:
            find(f"{key}: D-2 missing audit column {a}")
        elif "not null" not in col[2]:
            find(f"{key}: D-2 audit column {a} must be not null")
    # D-3 / registers
    if key not in register:
        find(f"{key}: not in an erd.md §5 register")
    else:
        entity, policy, _ = register[key]
        if policy not in POLICIES:
            find(f"{key}: unknown delete policy {policy}")
        if f"Delete policy: {policy}." not in t["note"]:
            find(f"{key}: DBML note policy differs from register ({policy})")
        if entity != pascal(name):
            find(f"{key}: D-4 entity {entity} != PascalCase(table)")
        if entity not in diagram_entities:
            find(f"{key}: entity {entity} missing from every mermaid diagram")
    for cname, ctype, attrs in cols:
        # D-5
        if cname.endswith("_sar") and ctype != MONEY:
            find(f"{key}.{cname}: D-5 _sar column must be {MONEY}")
        if ctype == MONEY and not cname.endswith("_sar"):
            find(f"{key}.{cname}: D-5 money column must end in _sar")
        if cname in ("currency", "currency_code"):
            find(f"{key}.{cname}: D-5 forbids a currency column")
        # D-7
        if cname.endswith("_lang"):
            if ctype != "char(2)":
                find(f"{key}.{cname}: D-7 language tag must be char(2)")
            if cname[:-5] not in names:
                find(f"{key}.{cname}: D-7 language tag without its narrative column")
        # D-3
        if cname in ("deleted_at", "deleted_by", "is_deleted"):
            find(f"{key}.{cname}: D-3 forbids soft-delete columns")
        # D-14
        m = re.search(r"ref: > (\w+)\.(\w+)\.(\w+)", attrs)
        if m:
            tk = f"{m.group(1)}.{m.group(2)}"
            if tk not in tables:
                find(f"{key}.{cname}: ref target table {tk} does not exist")
            elif m.group(3) not in [c[0] for c in tables[tk]["cols"]]:
                find(f"{key}.{cname}: ref target column {m.group(3)} does not exist on {tk}")
            elif m.group(1) != schema and tk in register and not register[tk][2]:
                find(f"{key}.{cname}: D-14 cross-schema ref to non-referenceable {tk}")

for key in register:
    if key not in tables:
        find(f"{key}: in erd.md register but not in erd.dbml")

for ent, col in derived:
    if ent not in entities:
        find(f"§7 derived register: entity {ent} unknown")
    elif col not in [c[0] for c in tables[entities[ent]]["cols"]]:
        find(f"§7 derived register: {ent}.{col} unknown")

# crosswalk
with CSV.open(newline="") as fh:
    rows = list(csv.DictReader(fh))
if not rows:
    find("crosswalk CSV is empty")
for r in rows:
    for ref in [x.strip() for x in r["ERD table / column"].split(";") if x.strip()]:
        ent, _, col = ref.partition(".")
        if ent not in entities:
            find(f"crosswalk {r['Ref']}: entity {ent} unknown")
        elif col and col not in [c[0] for c in tables[entities[ent]]["cols"]]:
            find(f"crosswalk {r['Ref']}: column {ref} unknown")

# ---------------------------------------------------------------- report
if findings:
    print("\n".join(findings))
    print(f"\n{len(findings)} finding(s)")
    sys.exit(1)
print(f"ok — {len(tables)} tables, {len(register)} register rows, {len(diagram_entities)} diagram entities, "
      f"{len(derived)} derived-register entries, {len(rows)} crosswalk rows")
