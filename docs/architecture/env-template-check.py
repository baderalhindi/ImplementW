#!/usr/bin/env python3
"""Environment template check (TASK-013).

Diffs the two environment templates against the Environment and Secrets sheet (snapshotted as
environment-and-secrets.csv) in both directions, and confirms that neither template carries a value.
Exit code 1 on any finding.

    python3 docs/architecture/env-template-check.py
"""
import csv
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SHEET = ROOT / "docs/architecture/environment-and-secrets.csv"
BACKEND = ROOT / "src/backend/PMPlatform.Api/appsettings.Template.json"
FRONTEND = ROOT / "src/frontend/.env.example"
VITE_PREFIX = "VITE_"

findings = []


def find(msg):
    findings.append(msg)


# ---------------------------------------------------------------- sheet
with SHEET.open(newline="") as f:
    sheet = {row["Variable Name"]: row for row in csv.DictReader(f)}
public = {name for name, row in sheet.items() if row["Classification"].startswith("Public")}

# ---------------------------------------------------------------- backend: JSON with // comments, flat keys
jsonc = BACKEND.read_text()
stripped = "\n".join(re.sub(r"^\s*//.*$|\s+//.*$", "", line) for line in jsonc.splitlines())
try:
    backend = json.loads(stripped)
except json.JSONDecodeError as exc:
    sys.exit(f"{BACKEND.name}: not parseable as JSON once // comments are removed: {exc}")

for name in sheet:
    if name not in backend:
        find(f"{BACKEND.name}: sheet variable {name} is missing")
for name, value in backend.items():
    if name not in sheet:
        find(f"{BACKEND.name}: {name} is not a sheet variable")
    if value != "":
        find(f"{BACKEND.name}: {name} carries a value; templates list names only")

# ---------------------------------------------------------------- frontend: dotenv, VITE_ prefix, Public only
frontend = {}
for lineno, line in enumerate(FRONTEND.read_text().splitlines(), 1):
    if not line.strip() or line.lstrip().startswith("#"):
        continue
    m = re.match(r"^([A-Z0-9_]+)=(.*?)\s*(?:#.*)?$", line)
    if not m:
        find(f"{FRONTEND.name}:{lineno}: not a NAME= line")
        continue
    frontend[m.group(1)] = m.group(2)

for name, value in frontend.items():
    if not name.startswith(VITE_PREFIX):
        find(f"{FRONTEND.name}: {name} lacks the {VITE_PREFIX} prefix and cannot reach the bundle")
        continue
    sheet_name = name[len(VITE_PREFIX):]
    if sheet_name not in sheet:
        find(f"{FRONTEND.name}: {name} is not sheet variable {sheet_name}")
    elif sheet_name not in public:
        find(f"{FRONTEND.name}: {name} is classified Secret in the sheet and may not be in a bundle")
    if value != "":
        find(f"{FRONTEND.name}: {name} carries a value; templates list names only")

# ---------------------------------------------------------------- report
if findings:
    print("\n".join(findings))
    sys.exit(1)
print(
    f"OK: {len(sheet)} sheet variables; {len(backend)} in {BACKEND.name}; "
    f"{len(frontend)} public variables in {FRONTEND.name}; no values in either template."
)
