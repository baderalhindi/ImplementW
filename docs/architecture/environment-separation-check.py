#!/usr/bin/env python3
"""Separation check for the four environments (TASK-016).

infra/environments/environments.json is the single source of truth for what "isolated" means here:
four environments, each with its own project, database instance, secret-store namespace, state
bucket and service accounts, none of them shared, all of them in the named in-Kingdom region. This
script asserts those invariants statically, so a manifest edit that quietly shares an identifier
between PROD and a non-PROD environment fails the pull request instead of the drill (ADR-001 C-1,
C-5, C-7; cybersecurity-control-matrix CTL-01, CTL-02, CTL-23, CTL-47).

It checks the manifest against itself, against the Environment and Secrets sheet snapshot, against
the GitHub deployment environments, and against the promotion table in the record. It cannot check
what only a provisioned environment can answer — that is infra/environments/verify-separation.sh.

    python3 docs/architecture/environment-separation-check.py
"""
import csv
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
MANIFEST = ROOT / "infra/environments/environments.json"
GITHUB = ROOT / "infra/environments/github/environments.json"
SHEET = ROOT / "docs/architecture/environment-and-secrets.csv"
RECORD = ROOT / "docs/architecture/environment-separation.md"

ORDER = ["dev", "sit", "uat", "prod"]
VARIABLES = {
    "DB_CONNECTION_STRING": ("secret", "Secret"),
    "JWT_SIGNING_KEY": ("secret", "Secret"),
    "APP_BASE_URL": ("config", "Public"),
}
# Google's region in Saudi Arabia is me-central2 (Dammam). me-central1 is Doha, Qatar: out of
# Kingdom, and the one neighbouring identifier a typo produces (ADR-001 §7 C-1).
IN_KINGDOM = {"me-central2"}
REGION_PATTERN = re.compile(r"\b[a-z]+-[a-z]+\d(?:-[a-z])?\b")

findings = []


def fail(check, message):
    findings.append(f"{check}: {message}")


def load(path):
    try:
        return json.loads(path.read_text())
    except (OSError, json.JSONDecodeError) as exc:
        print(f"cannot read {path.relative_to(ROOT)}: {exc}", file=sys.stderr)
        sys.exit(1)


def identifiers(env):
    """Every value that must belong to exactly one environment."""
    yield "project_id", env["project_id"]
    yield "network", env["network"]
    yield "state_bucket", env["state_bucket"]
    yield "database.instance", env["database"]["instance"]
    yield "database.user", env["database"]["user"]
    yield "secret_store.namespace", env["secret_store"]["namespace"]
    yield "secret_store.prefix", env["secret_store"]["prefix"]
    for role, account in env["service_accounts"].items():
        yield f"service_accounts.{role}", account
    for name, spec in env["variables"].items():
        if spec.get("kind") == "secret":
            yield f"variables.{name}.secret_id", spec["secret_id"]


def check_shape(manifest):
    names = [e["name"] for e in manifest["environments"]]
    if names != ORDER:
        fail("S-1", f"environments are {names}, expected exactly {ORDER} in that order")
        return False
    for env, expected_next in zip(manifest["environments"], ORDER[1:] + [None]):
        if env.get("promotes_to") != expected_next:
            fail("S-1", f"{env['name']}.promotes_to is {env.get('promotes_to')!r}, expected {expected_next!r}")
    return True


def check_uniqueness(manifest):
    seen = {}
    for env in manifest["environments"]:
        for field, value in identifiers(env):
            # The namespace is the project by design (S-3 asserts it), so it is not a second value.
            if field == "secret_store.namespace":
                continue
            if not value:
                fail("S-2", f"{env['name']}.{field} is empty; it names an isolation boundary")
            elif value in seen:
                fail("S-2", f"{env['name']}.{field} = {value!r} is also {seen[value]} — the boundary is shared")
            else:
                seen[value] = f"{env['name']}.{field}"


def check_ownership(manifest):
    for env in manifest["environments"]:
        project = env["project_id"]
        for role, account in env["service_accounts"].items():
            if not account.endswith(f"@{project}.iam.gserviceaccount.com"):
                fail("S-3", f"{env['name']} {role} service account {account} is not hosted by {project}")
        prefix = env["secret_store"]["prefix"]
        for name, spec in env["variables"].items():
            if spec.get("kind") == "secret" and not spec["secret_id"].startswith(prefix):
                fail("S-3", f"{env['name']} secret {spec['secret_id']} does not carry the namespace prefix {prefix!r}")
        if env["secret_store"]["namespace"] != project:
            fail("S-3", f"{env['name']} secret namespace {env['secret_store']['namespace']} is not its project {project}")


def check_prod_boundary(manifest):
    envs = {e["name"]: e for e in manifest["environments"]}
    prod = envs["prod"]
    prod_values = {v for _, v in identifiers(prod)}
    for name, env in envs.items():
        if name == "prod":
            continue
        shared = prod_values & {v for _, v in identifiers(env)}
        if shared:
            fail("S-4", f"{name} shares {sorted(shared)} with prod")
        if env["folder"] == prod["folder"]:
            fail("S-4", f"{name} sits in the PROD folder {prod['folder']}")
    folders = {f["name"]: f for f in manifest["platform"]["folders"]}
    if prod["folder"] not in folders:
        fail("S-4", f"prod folder {prod['folder']} is not declared in platform.folders")
    elif folders[prod["folder"]]["holds"] != ["prod"]:
        fail("S-4", f"folder {prod['folder']} holds {folders[prod['folder']]['holds']}, expected ['prod'] only")


def check_region(manifest):
    allowlist = manifest["platform"]["region_allowlist"]
    outside = [r for r in allowlist if r not in IN_KINGDOM]
    if outside:
        fail("S-5", f"region_allowlist holds {outside}, which is outside Saudi Arabia (ADR-001 C-1)")
    region = manifest["platform"]["region"]
    if region and region not in allowlist:
        fail("S-5", f"platform.region {region!r} is not on the allowlist {allowlist}")
    # No region identifier may appear anywhere else in the manifest: a hard-coded region in a
    # bucket or instance name is how a resource lands outside the Kingdom without anyone editing
    # platform.region.
    for env in manifest["environments"]:
        for field, value in identifiers(env):
            for candidate in REGION_PATTERN.findall(str(value)):
                if candidate not in allowlist and candidate.split("-")[0] in {"me", "us", "eu", "asia", "australia", "southamerica", "northamerica", "africa"}:
                    fail("S-5", f"{env['name']}.{field} = {value!r} names region {candidate!r}")
    return region


def check_variables(manifest):
    urls = {}
    for env in manifest["environments"]:
        declared = set(env["variables"])
        if declared != set(VARIABLES):
            fail("S-6", f"{env['name']} declares {sorted(declared)}, expected {sorted(VARIABLES)}")
        for name, spec in env["variables"].items():
            expected_kind = VARIABLES.get(name, (None, None))[0]
            if spec.get("kind") != expected_kind:
                fail("S-6", f"{env['name']}.{name}.kind is {spec.get('kind')!r}, expected {expected_kind!r}")
            elif expected_kind == "secret":
                if set(spec) != {"kind", "secret_id"}:
                    fail("S-6", f"{env['name']}.{name} carries {sorted(set(spec) - {'kind', 'secret_id'})}; "
                                "a secret entry names its secret-store entry and nothing else — never a value")
            elif expected_kind == "config":
                if set(spec) != {"kind", "value"}:
                    fail("S-6", f"{env['name']}.{name} carries {sorted(set(spec) - {'kind', 'value'})}, expected kind and value")
                else:
                    urls[env["name"]] = spec["value"]

    populated = {e: u for e, u in urls.items() if u}
    if populated and len(populated) != len(urls):
        fail("S-6", f"APP_BASE_URL is set for {sorted(populated)} and empty for the rest; "
                    "the sheet requires a distinct value per environment")
    if len(set(populated.values())) != len(populated):
        fail("S-6", f"APP_BASE_URL values are not distinct per environment: {populated}")
    for env, url in populated.items():
        if not url.startswith("https://"):
            fail("S-6", f"{env} APP_BASE_URL {url!r} is not https")


def check_sheet(manifest):
    rows = {}
    with SHEET.open(newline="") as handle:
        for fields in list(csv.reader(handle))[1:]:
            if len(fields) >= 4:
                rows[fields[0]] = (fields[2], fields[3])
    for name, (_, classification) in VARIABLES.items():
        if name not in rows:
            fail("S-7", f"{name} is not a row of the Environment and Secrets sheet snapshot")
            continue
        scope, sheet_class = rows[name]
        if not sheet_class.startswith(classification):
            fail("S-7", f"{name} is {sheet_class!r} in the sheet, this manifest treats it as {classification}")
        for env in ORDER:
            if env.upper() not in scope.upper():
                fail("S-7", f"{name} sheet scope {scope!r} does not cover {env.upper()}")


def check_github(manifest, github):
    names = [e["name"] for e in github["environments"]]
    if names != ORDER:
        fail("S-8", f"GitHub environments are {names}, expected {ORDER}")
        return
    by_name = {e["name"]: e for e in github["environments"]}
    if by_name["dev"]["reviewer_teams"]:
        fail("S-8", "dev carries an approval gate; the promotion path makes build -> DEV automatic")
    for name in ORDER[1:]:
        if not by_name[name]["reviewer_teams"]:
            fail("S-8", f"{name} has no reviewer team — the promotion path requires a named approval gate")
    if not by_name["prod"]["prevent_self_review"]:
        fail("S-8", "prod does not set prevent_self_review; the PROD approval must be someone other than the deployer")


def check_record():
    if not RECORD.exists():
        fail("S-9", f"{RECORD.relative_to(ROOT)} is missing")
        return
    gates = re.findall(r"^\| \*\*(G-\d)\*\* \|([^|]*)\|([^|]*)\|", RECORD.read_text(), re.M)
    expected = [("build", "DEV"), ("DEV", "SIT"), ("SIT", "UAT"), ("UAT", "PROD")]
    if len(gates) != 4:
        fail("S-9", f"the record's promotion table has {len(gates)} gate rows, expected 4")
        return
    for (gate, promotion, approver), (source, target) in zip(gates, expected):
        if f"{source}" not in promotion or f"{target}" not in promotion:
            fail("S-9", f"{gate} reads {promotion.strip()!r}, expected {source} to {target}")
        if gate != "G-1" and not approver.strip().strip("—").strip():
            fail("S-9", f"{gate} ({promotion.strip()}) names no approver")


def main():
    manifest = load(MANIFEST)
    github = load(GITHUB)
    region = None
    if check_shape(manifest):
        check_uniqueness(manifest)
        check_ownership(manifest)
        check_prod_boundary(manifest)
        check_variables(manifest)
    region = check_region(manifest)
    check_sheet(manifest)
    check_github(manifest, github)
    check_record()

    if findings:
        for finding in findings:
            print(finding)
        print(f"\n{len(findings)} finding(s).")
        return 1
    unresolved = [k for k, v in manifest["platform"].items() if v == "" and k != "region"]
    print(f"OK: {len(ORDER)} isolated environments; {len(list(identifiers(manifest['environments'][0])))} "
          f"identifiers per environment, none shared; region "
          f"{region or 'UNNAMED (UGV-07)'}; unresolved platform values: {', '.join(unresolved) or 'none'}.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
