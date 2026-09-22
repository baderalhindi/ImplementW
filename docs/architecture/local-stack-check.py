#!/usr/bin/env python3
"""Drift check for the local-stack bridge schema (TASK-014).

infra/docker/postgres/init/01-schema.sql transcribes nine tables of docs/architecture/erd.dbml so the local
PostgreSQL can hold seeded users before TASK-023/TASK-024 create the schema through EF Core migrations. Until
that file is deleted, every column it declares must match the ERD: same tables, same columns in the same
order, same type, same nullability, same default. Exit code 1 on any finding.

    python3 docs/architecture/local-stack-check.py
"""
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DBML = ROOT / "docs/architecture/erd.dbml"
SQL = ROOT / "infra/docker/postgres/init/01-schema.sql"

findings = []


def parse_dbml():
    tables = {}
    current = None
    in_indexes = False
    for raw in DBML.read_text().splitlines():
        line = raw.strip()
        m = re.match(r"^Table (\w+)\.(\w+) \{$", line)
        if m:
            current = f"{m.group(1)}.{m.group(2)}"
            tables[current] = []
            continue
        if current is None:
            continue
        if line == "}":
            if in_indexes:
                in_indexes = False
            else:
                current = None
            continue
        if line.startswith("indexes {"):
            in_indexes = True
            continue
        if in_indexes or line.startswith("Note:"):
            continue
        m = re.match(r"^(\w+) (\S+)(?: \[(.*)\])?$", line)
        if not m:
            continue
        attrs = [a.strip() for a in (m.group(3) or "").split(",")]
        default = next((a[len("default: "):] for a in attrs if a.startswith("default: ")), None)
        nullable = "pk" not in attrs and "not null" not in attrs
        tables[current].append((m.group(1), m.group(2), nullable, default))
    return tables


def parse_sql():
    tables = {}
    for m in re.finditer(r'CREATE TABLE (\w+)\."?(\w+)"? \((.*?)\n\);', SQL.read_text(), re.S):
        cols = []
        for raw in m.group(3).splitlines():
            line = raw.strip().rstrip(",")
            if not line or line.startswith("UNIQUE ("):
                continue
            cm = re.match(r'^"?(\w+)"? (\S+)(.*)$', line)
            rest = cm.group(3)
            dm = re.search(r"DEFAULT (\S+)", rest)
            nullable = "PRIMARY KEY" not in rest and "NOT NULL" not in rest
            cols.append((cm.group(1), cm.group(2), nullable, dm.group(1) if dm else None))
        tables[f"{m.group(1)}.{m.group(2)}"] = cols
    return tables


dbml = parse_dbml()
sql = parse_sql()

if not sql:
    findings.append(f"no CREATE TABLE found in {SQL.relative_to(ROOT)}")

for table, sql_cols in sql.items():
    if table not in dbml:
        findings.append(f"{table}: bridged in 01-schema.sql but not in erd.dbml")
        continue
    if sql_cols != dbml[table]:
        sql_by_name = {c[0]: c for c in sql_cols}
        dbml_by_name = {c[0]: c for c in dbml[table]}
        for name in dbml_by_name.keys() - sql_by_name.keys():
            findings.append(f"{table}.{name}: in erd.dbml, missing from 01-schema.sql")
        for name in sql_by_name.keys() - dbml_by_name.keys():
            findings.append(f"{table}.{name}: in 01-schema.sql, not in erd.dbml")
        for name in sql_by_name.keys() & dbml_by_name.keys():
            if sql_by_name[name] != dbml_by_name[name]:
                findings.append(f"{table}.{name}: 01-schema.sql {sql_by_name[name][1:]} != erd.dbml {dbml_by_name[name][1:]}")
        if [c[0] for c in sql_cols] != [c[0] for c in dbml[table]] and not (dbml_by_name.keys() ^ sql_by_name.keys()):
            findings.append(f"{table}: column order differs from erd.dbml")

if findings:
    for f in findings:
        print(f"FINDING: {f}")
    sys.exit(1)

print(f"OK: {len(sql)} bridged tables, {sum(len(c) for c in sql.values())} columns match erd.dbml.")
