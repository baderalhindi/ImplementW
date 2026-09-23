#!/usr/bin/env python3
"""The secret inventory (TASK-019): which secret each environment holds, and under what id.

The Environment and Secrets sheet is the source of truth for *which* variables exist and where each
one is stored; `infra/environments/environments.json` (TASK-016) is the source of truth for the
per-environment namespace those ids are built in. This module is the one place that joins them, so
the id `pmplatform-sit-jwt-signing-key` is derived rather than typed, and no second list of secrets
exists anywhere to drift from the sheet.

    python3 infra/secrets/inventory.py            # print the inventory
    python3 infra/secrets/inventory.py --check    # exit 1 if the manifest disagrees with the sheet
    python3 infra/secrets/inventory.py --write    # rewrite the manifest's secret entries from the sheet

Routing. A row's Storage Location column decides which store holds it, and the four routes are not
interchangeable:

    secret-store  the approved secret-management platform — the inventory below
    cicd          the CI/CD platform's native secret store (CTL-48) — GitHub environment secrets,
                  never this inventory, because the pipeline needs them before any environment exists
    bootstrap     held outside the repository and outside the store: SECRET_STORE_ENDPOINT is plain
                  configuration and SECRET_STORE_AUTH_TOKEN is the credential that opens the store,
                  so neither can live inside it
    config        Public rows — the environment's configuration store, not a secret

A Secret row that matches no route is an error, not a silent omission: the point of deriving the
inventory from the sheet is that a row added to the sheet cannot be forgotten here.
"""
import argparse
import csv
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SHEET = ROOT / "docs/architecture/environment-and-secrets.csv"
MANIFEST = ROOT / "infra/environments/environments.json"

ENVIRONMENTS = ["dev", "sit", "uat", "prod"]

# Storage Location phrases, in the order they are tested. The first match wins, so the two routes
# that take a variable *out* of the store are tested before the phrase that puts one in it.
ROUTES = [
    ("bootstrap", ("bootstrap configuration", "injected via deployment platform")),
    ("cicd", ("ci/cd platform's native secret store",)),
    ("secret-store", ("approved secret-management platform",)),
    ("config", ("application configuration store",)),
]

# Scope strings that name no standing environment. Each is an explicit decision, not a fallback:
# a scope this module does not recognise fails the check.
NO_STANDING_ENVIRONMENT = {
    "all (ci/cd)": "the pipeline, not an environment",
    "cutover window only": "created for the cutover window and revoked when it closes (TASK-102)",
}


class InventoryError(Exception):
    """The sheet and the manifest cannot be reconciled without a human decision."""


def secret_id(prefix, variable):
    """The id of `variable` in the namespace `prefix`. One rule, applied everywhere.

    PMPlatform.Infrastructure.Secrets.SecretStore applies the same rule at runtime by appending the
    lower-kebab name to SECRET_STORE_ENDPOINT, and SecretStoreTests asserts the two agree on every
    entry of the manifest.
    """
    return prefix + variable.lower().replace("_", "-")


def read_sheet(path=SHEET):
    """The sheet's rows as dicts, keyed by variable name, in sheet order."""
    with path.open(newline="", encoding="utf-8") as handle:
        rows = list(csv.DictReader(handle))
    return {row["Variable Name"]: row for row in rows}


def route_of(row):
    storage = row["Storage Location"].lower()
    for route, phrases in ROUTES:
        if any(phrase in storage for phrase in phrases):
            return route
    raise InventoryError(
        f"{row['Variable Name']}: Storage Location {row['Storage Location']!r} names no known store. "
        "Add the route to inventory.py ROUTES deliberately; do not let the row fall through."
    )


def scope_of(row):
    """The environments a row is in scope for, as a list in ENVIRONMENTS order."""
    scope = row["Environment Scope"]
    found = [env for env in ENVIRONMENTS if re.search(rf"\b{env}\b", scope, re.IGNORECASE)]
    if found:
        return found
    if scope.strip().lower() in NO_STANDING_ENVIRONMENT:
        return []
    raise InventoryError(
        f"{row['Variable Name']}: Environment Scope {scope!r} names none of {ENVIRONMENTS} and is not "
        f"one of the recognised non-standing scopes {sorted(NO_STANDING_ENVIRONMENT)}."
    )


def classify(sheet=None):
    """Every sheet row as (route, environments, classification), with the Secret rows validated.

    A Secret row routed to the configuration store is a contradiction in the sheet itself and is
    raised here rather than resolved by guessing which column is right.
    """
    sheet = sheet if sheet is not None else read_sheet()
    classified = {}
    for name, row in sheet.items():
        route = route_of(row)
        secret = row["Classification"].strip().lower().startswith("secret")
        if secret and route == "config":
            raise InventoryError(
                f"{name} is classified Secret but its Storage Location is the configuration store. "
                "The sheet contradicts itself; resolve it in the sheet."
            )
        if not secret and route == "secret-store":
            raise InventoryError(
                f"{name} is classified {row['Classification']!r} but is stored in the secret store. "
                "The sheet contradicts itself; resolve it in the sheet."
            )
        classified[name] = (route, scope_of(row), row["Classification"].strip())
    return classified


def inventory(manifest, sheet=None):
    """{environment: {VARIABLE: secret_id}} — what the approved store holds, per environment."""
    classified = classify(sheet)
    prefixes = {env["name"]: env["secret_store"]["prefix"] for env in manifest["environments"]}
    result = {env: {} for env in ENVIRONMENTS}
    for name, (route, environments, _) in classified.items():
        if route != "secret-store":
            continue
        for env in environments:
            result[env][name] = secret_id(prefixes[env], name)
    return result


def on_demand(sheet=None):
    """Secret-store rows with no standing environment: created when needed, then revoked."""
    sheet = sheet if sheet is not None else read_sheet()
    return {
        name: NO_STANDING_ENVIRONMENT[sheet[name]["Environment Scope"].strip().lower()]
        for name, (route, environments, _) in classify(sheet).items()
        if route == "secret-store" and not environments
    }


def load_manifest(path=MANIFEST):
    return json.loads(path.read_text(encoding="utf-8"))


def manifest_secrets(manifest):
    return {
        env["name"]: {
            name: spec["secret_id"]
            for name, spec in env["variables"].items()
            if spec.get("kind") == "secret"
        }
        for env in manifest["environments"]
    }


def differences(manifest, sheet=None):
    """Every disagreement between the manifest's secret entries and the sheet-derived inventory."""
    expected = inventory(manifest, sheet)
    actual = manifest_secrets(manifest)
    findings = []
    for env in ENVIRONMENTS:
        want, have = expected[env], actual.get(env, {})
        for name in sorted(set(want) - set(have)):
            findings.append(f"{env}: {name} is in the sheet for {env.upper()} but not in the manifest")
        for name in sorted(set(have) - set(want)):
            findings.append(f"{env}: {name} is in the manifest but the sheet does not put it in {env.upper()}")
        for name in sorted(set(want) & set(have)):
            if want[name] != have[name]:
                findings.append(f"{env}.{name}: id is {have[name]!r}, the namespace rule gives {want[name]!r}")
    return findings


def write(manifest_path=MANIFEST):
    """Rewrite each environment's `variables` block from the sheet, in the manifest's own style.

    The block is regenerated as text rather than re-serialised from the parsed document, because the
    manifest is hand-written and keeps one entry per line; a `json.dumps` round trip would reformat
    every unrelated line of it and bury this change in the diff.
    """
    source = manifest_path.read_text(encoding="utf-8")
    manifest = json.loads(source)
    expected = inventory(manifest)

    out, cursor = [], 0
    for env in manifest["environments"]:
        start = source.index('"variables": {', cursor)
        end = matching_brace(source, source.index("{", start))
        entries = [
            f'        "{name}": {{ "kind": "secret", "secret_id": "{identifier}" }}'
            for name, identifier in sorted(expected[env["name"]].items())
        ] + [
            f'        "{name}": {{ "kind": "{spec["kind"]}", "value": "{spec["value"]}" }}'
            for name, spec in env["variables"].items()
            if spec.get("kind") != "secret"
        ]
        out.append(source[cursor:start])
        out.append('"variables": {\n' + ",\n".join(entries) + "\n      }")
        cursor = end + 1
    out.append(source[cursor:])
    manifest_path.write_text("".join(out), encoding="utf-8")
    return expected


def matching_brace(text, opening):
    depth = 0
    for index in range(opening, len(text)):
        depth += {"{": 1, "}": -1}.get(text[index], 0)
        if depth == 0:
            return index
    raise InventoryError("unbalanced braces in the manifest")


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    group = parser.add_mutually_exclusive_group()
    group.add_argument("--check", action="store_true", help="exit 1 if the manifest disagrees with the sheet")
    group.add_argument("--write", action="store_true", help="rewrite the manifest's secret entries")
    arguments = parser.parse_args()

    try:
        manifest = load_manifest()
        if arguments.write:
            expected = write()
            print(f"wrote {sum(len(v) for v in expected.values())} secret entries to "
                  f"{MANIFEST.relative_to(ROOT)}")
            return 0
        findings = differences(manifest)
        if arguments.check:
            for finding in findings:
                print(finding)
            if findings:
                print(f"\n{len(findings)} finding(s). Run inventory.py --write, or fix the sheet.")
                return 1
            print(f"OK: manifest matches the sheet for all {len(ENVIRONMENTS)} environments.")
            return 0

        expected = inventory(manifest)
        for env in ENVIRONMENTS:
            print(f"\n{env.upper()} ({len(expected[env])} secrets)")
            for name, identifier in sorted(expected[env].items()):
                print(f"  {name:<44} {identifier}")
        elsewhere = {n: r for n, (r, _, _) in classify().items() if r in {"cicd", "bootstrap"}}
        print("\nHeld outside the store:")
        for name, route in sorted(elsewhere.items()):
            print(f"  {name:<44} {route}")
        for name, why in sorted(on_demand().items()):
            print(f"  {name:<44} on demand — {why}")
        return 0
    except (OSError, json.JSONDecodeError, InventoryError) as exc:
        print(f"{type(exc).__name__}: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
