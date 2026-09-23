#!/usr/bin/env python3
"""Secret-management check (TASK-019).

Asserts the invariants that make "every secret is read at runtime from the approved store, and no
secret is in the repository" true of the code rather than of a claim in a document (CTL-18,
Blueprint Section 22.1, F-3). What only a provisioned environment can answer — replication, IAM,
whether a container holds a version — is infra/secrets/verify-secret-integration.sh.

That the manifest's inventory still matches the Environment and Secrets sheet is asserted by
environment-separation-check.py S-6 and S-7, which reads it through the same module; this script
does not repeat it.

    python3 docs/architecture/secret-management-check.py
"""
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "infra/secrets"))
import inventory  # noqa: E402  — the single derivation of the secret inventory

MANIFEST = ROOT / "infra/environments/environments.json"
APPLICATION_SECRETS = ROOT / "src/backend/PMPlatform.Infrastructure/Secrets/ApplicationSecrets.cs"
PLATFORM = ROOT / "infra/terraform/platform/main.tf"
TERRAFORM = ROOT / "infra/terraform"
SCRIPTS = ROOT / "infra/secrets"
RUNBOOK = ROOT / "infra/secrets/secret-rotation-runbook.md"
RECORD = ROOT / "docs/architecture/secret-management.md"
SCANNER_CONFIG = ROOT / ".gitleaks.toml"
GITIGNORE = ROOT / ".gitignore"

findings = []


def fail(check, message):
    findings.append(f"{check}: {message}")


def application_keys():
    """The configuration keys ApplicationSecrets declares, as string literals."""
    text = APPLICATION_SECRETS.read_text(encoding="utf-8")
    listed = re.search(r"Keys\s*=\s*\[([^\]]*)\]", text)
    if not listed:
        fail("K-1", f"{APPLICATION_SECRETS.relative_to(ROOT)} declares no Keys list")
        return []
    constants = dict(re.findall(r'const string (\w+)\s*=\s*"([^"]+)"', text))
    entries = [entry.strip() for entry in listed.group(1).split(",") if entry.strip()]
    return [constants.get(entry, entry.strip('"')) for entry in entries]


def check_application_keys(manifest, classified):
    """K-1 — a key the application reads is a secret of every environment that runs it."""
    secrets = inventory.inventory(manifest)
    for key in application_keys():
        if key not in classified:
            fail("K-1", f"ApplicationSecrets declares {key}, which is not a row of the Environment and Secrets sheet")
            continue
        route, scope, classification = classified[key]
        if route != "secret-store":
            fail("K-1", f"ApplicationSecrets reads {key}, which the sheet stores in the {route} ({classification})")
        missing = [env for env in inventory.ENVIRONMENTS if key not in secrets[env]]
        if missing:
            fail("K-1", f"ApplicationSecrets reads {key}, which no container exists for in {missing}. "
                        f"The sheet scopes it to {[e.upper() for e in scope]}; a key the application requires "
                        "at start-up must exist in every environment it runs in")


def check_no_injected_secret():
    """K-2 — the deployment hands the runtime the store's address, never a secret value."""
    for path in sorted(TERRAFORM.rglob("*.tf")):
        text = path.read_text(encoding="utf-8")
        for marker in ("secret_key_ref", "value_source", "google_secret_manager_secret_version"):
            if marker in text:
                fail("K-2", f"{path.relative_to(ROOT)} uses {marker}: the application reads its secrets from "
                            "the store with its own identity, so no secret value is bound to a revision or "
                            "written by Terraform (secret-management.md §3)")

    platform = PLATFORM.read_text(encoding="utf-8")
    if "SECRET_STORE_ENDPOINT" not in platform:
        fail("K-2", f"{PLATFORM.relative_to(ROOT)} passes no SECRET_STORE_ENDPOINT; the runtime would have "
                    "no way to reach the store")
    endpoint = re.search(r"secret_store_endpoint\s*=\s*\"([^\"]+)\"", platform)
    if not endpoint:
        fail("K-2", f"{PLATFORM.relative_to(ROOT)} defines no secret_store_endpoint local")
    else:
        for required in ("${local.environment.project_id}", "${local.environment.secret_store.prefix}"):
            if required not in endpoint.group(1):
                fail("K-2", f"secret_store_endpoint does not interpolate {required}; the address must come "
                            "from the manifest, not be written out again")


def check_scripts_never_pass_a_value():
    """K-3 — a secret value never becomes a command argument, where any process can read it."""
    for path in sorted(SCRIPTS.glob("*.sh")):
        text = path.read_text(encoding="utf-8")
        for flag in re.findall(r"""--data-file=([^\s)'";]+)""", text):
            if flag != "-":
                fail("K-3", f"{path.relative_to(ROOT)} writes a secret from {flag}; the value is read from "
                            "stdin (--data-file=-) so it is never on disk")
        if "--secret-data" in text:
            fail("K-3", f"{path.relative_to(ROOT)} uses --secret-data, which puts the value in the process "
                        "arguments; use --data-file=- instead")


def check_runbook_and_record(classified):
    """K-4 — every secret the store holds has a rotation procedure, and the record exists."""
    if not RECORD.exists():
        fail("K-4", f"{RECORD.relative_to(ROOT)} is missing")
    if not RUNBOOK.exists():
        fail("K-4", f"{RUNBOOK.relative_to(ROOT)} is missing")
        return
    runbook = RUNBOOK.read_text(encoding="utf-8")
    for name, (route, _, _) in sorted(classified.items()):
        if route == "secret-store" and f"`{name}`" not in runbook:
            fail("K-4", f"{name} is held in the secret store and the rotation runbook does not name it")


def check_repository_posture(classified):
    """K-5 — the repository denies secrets by default and is scanned."""
    if not SCANNER_CONFIG.exists():
        fail("K-5", f"{SCANNER_CONFIG.name} is missing; infra/secrets/scan-history.sh has no configuration")
    # Line-exact: "infra/secrets/*" as a substring also matches the "!infra/secrets/*.py" exception
    # that re-admits this module's own code, which would make the check pass with the rule deleted.
    ignore = {line.strip() for line in GITIGNORE.read_text(encoding="utf-8").splitlines()}
    for required in ("infra/secrets/*", "appsettings.*.Local.json", ".env"):
        if required not in ignore:
            fail("K-5", f".gitignore does not exclude {required}")
    for name, (route, _, _) in sorted(classified.items()):
        if route == "secret-store" and not inventory.read_sheet()[name]["Owner"].strip():
            fail("K-5", f"{name} is held in the secret store and the sheet names no owner to rotate it")


def main():
    try:
        manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
        classified = inventory.classify()
    except (OSError, json.JSONDecodeError, inventory.InventoryError) as exc:
        print(f"{type(exc).__name__}: {exc}", file=sys.stderr)
        return 1

    check_application_keys(manifest, classified)
    check_no_injected_secret()
    check_scripts_never_pass_a_value()
    check_runbook_and_record(classified)
    check_repository_posture(classified)

    if findings:
        for finding in findings:
            print(finding)
        print(f"\n{len(findings)} finding(s).")
        return 1

    held = sum(1 for route, _, _ in classified.values() if route == "secret-store")
    containers = sum(len(v) for v in inventory.inventory(manifest).values())
    print(f"OK: {held} secrets in the approved store across {containers} containers; "
          f"{len(application_keys())} read by the application at runtime; none injected by the deployment, "
          "none passed as an argument, all with a rotation procedure.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
