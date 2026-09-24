#!/usr/bin/env python3
"""Static checks on the database provisioning module (TASK-020).

`terraform validate` proves the module is well formed and `terraform-check.py` proves no hosting
value has been written into the Terraform twice. Neither looks at what TASK-020's acceptance
criteria actually turn on: that the instance is configured to refuse a non-TLS connection, that
automated backups and point-in-time recovery are on and in the named region, that the backup
retention period is still nobody's invention, and that the environments the Environment and
Secrets sheet gives a backup target are exactly the ones with an export configured.

Those are the mistakes that plan perfectly well and are found at an audit. They are caught here
instead, in the `repo-checks` CI job, with no Terraform binary and no cloud credentials.

    python3 docs/architecture/database-check.py
"""
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
MODULE = ROOT / "infra/terraform/database"
PLATFORM = ROOT / "infra/terraform/platform"
ROOTS = ROOT / "infra/terraform/environments"
MANIFEST = ROOT / "infra/environments/environments.json"
VERIFY = MODULE / "verify-database-controls.sh"

# The row that decides which environments have somewhere to put an off-instance copy.
BACKUP_TARGET_VARIABLE = "DB_BACKUP_STORAGE_CONNECTION_STRING"

# Every control the verification script claims in its header has to exist in its body. A check
# listed and not implemented is worse than one that was never claimed.
CHECK_ID = re.compile(r"^#\s+(D-\d)\s+\S", re.M)

findings = []


def fail(check, message):
    findings.append("{}: {}".format(check, message))


def load_manifest():
    try:
        return json.loads(MANIFEST.read_text())
    except (OSError, json.JSONDecodeError) as exc:
        print("cannot read {}: {}".format(MANIFEST.relative_to(ROOT), exc), file=sys.stderr)
        sys.exit(1)


def read(path):
    return path.read_text() if path.is_file() else ""


def rel(path):
    return str(path.relative_to(ROOT))


def check_deliverables():
    """DB-1 — the module's own files, and the verification script, exist and are runnable."""
    for name in ("main.tf", "backup.tf", "variables.tf", "outputs.tf", "versions.tf"):
        if not (MODULE / name).is_file():
            fail("DB-1", "infra/terraform/database/{} is missing".format(name))
    if not VERIFY.is_file():
        fail("DB-1", "{} is missing; the acceptance criteria are assertions about a running "
                     "instance and nothing else makes them".format(rel(VERIFY)))
    elif VERIFY.stat().st_mode & 0o111 == 0:
        fail("DB-1", "{} is not executable".format(rel(VERIFY)))


def check_transport():
    """DB-2 — the instance refuses a connection that is not TLS, and has no public address."""
    text = read(MODULE / "main.tf")
    if 'ssl_mode        = "ENCRYPTED_ONLY"' not in text and 'ssl_mode = "ENCRYPTED_ONLY"' not in text:
        fail("DB-2", "infra/terraform/database/main.tf does not set ssl_mode = \"ENCRYPTED_ONLY\". "
                     "CTL-17 and the TASK-020 acceptance criteria require the instance to reject a "
                     "non-TLS connection; the retired require_ssl argument is not a substitute and "
                     "no longer exists in the provider.")
    if not re.search(r"ipv4_enabled\s*=\s*false", text):
        fail("DB-2", "infra/terraform/database/main.tf does not disable the public address "
                     "(ipv4_enabled = false, CTL-03).")


def check_backup_mechanism():
    """DB-3 — backups and PITR are on, in the named region, and sized by variable not by literal."""
    text = read(MODULE / "main.tf")
    for pattern, message in (
        (r"enabled\s*=\s*true", "automated backups are not enabled (CTL-35)"),
        (r"point_in_time_recovery_enabled\s*=\s*true",
         "point-in-time recovery is not enabled. Automated backups run once a day, so without it "
         "the RPO is 24 hours and PTBC-048 asks for 1 hour"),
        (r"location\s*=\s*var\.region",
         "the backup location is not var.region. ADR-001 C-3 keeps backups in the named "
         "in-Kingdom region, and a backup in another region is the one copy that leaves it"),
        (r"transaction_log_retention_days\s*=\s*var\.transaction_log_retention_days",
         "the point-in-time recovery window is not taken from a variable"),
    ):
        if not re.search(pattern, text):
            fail("DB-3", "infra/terraform/database/main.tf: {}".format(message))


def check_no_retention_invented():
    """DB-4 — the retention period is still OQ-003's to give, in the module and in every root."""
    variables = read(MODULE / "variables.tf")
    block = re.search(r'variable "retained_backups" \{(.*?)\n\}', variables, re.S)
    if block is None:
        fail("DB-4", "infra/terraform/database/variables.tf declares no retained_backups variable")
    elif not re.search(r"default\s*=\s*null", block.group(1)):
        fail("DB-4", "retained_backups has a default. The backup retention period is OQ-003 / "
                     "PTBC-048, owed by AHDA Cybersecurity and Records; TASK-020's gate cell says "
                     "to build the mechanism and leave the period configurable.")

    for tfvars in sorted(ROOTS.glob("*/terraform.tfvars")):
        text = tfvars.read_text()
        for name in ("retained_backups", "noncurrent_version_retention_days"):
            if re.search(r"^\s*{}\s*=".format(name), text, re.M):
                fail("DB-4", "{} sets {}. That period is OQ-003 / PTBC-048 and has no approved "
                             "value; it is supplied at apply time once AHDA gives one.".format(
                                 rel(tfvars), name))


def check_guards():
    """DB-5 — PROD cannot be applied without a retention period or an off-instance copy."""
    text = read(PLATFORM / "guards.tf")
    for pattern, message in (
        (r'var\.environment\s*!=\s*"prod"\s*\|\|\s*var\.retained_backups\s*!=\s*null',
         "no guard holds PROD while the retention period is unset (OQ-003 / PTBC-048)"),
        (r'var\.environment\s*!=\s*"prod"\s*\|\|\s*var\.backup_export_schedule\s*!=\s*null',
         "no guard holds PROD without a scheduled export. Cloud SQL's automated backups are "
         "deleted with the instance they belong to, so an environment whose only copy is on the "
         "instance has no answer to the instance being deleted"),
    ):
        if not re.search(pattern, text):
            fail("DB-5", "infra/terraform/platform/guards.tf: {}".format(message))


def check_encryption_key_configurable():
    """DB-6 — CMEK is a hook, not a decision, and no key is committed anywhere."""
    variables = read(MODULE / "variables.tf")
    block = re.search(r'variable "encryption_key_name" \{(.*?)\n\}', variables, re.S)
    if block is None:
        fail("DB-6", "infra/terraform/database/variables.tf declares no encryption_key_name. "
                     "Whether AHDA's own key is required follows the data classification in "
                     "ADR-001 R-4; the hook exists so that answer costs a variable, not a rebuild.")
    elif not re.search(r"default\s*=\s*null", block.group(1)):
        fail("DB-6", "encryption_key_name has a default. Google-managed encryption is always on; "
                     "a customer-managed key is AHDA's decision to take (ADR-001 R-4).")

    if not re.search(r"encryption_key_name\s*=\s*var\.database_encryption_key_name",
                     read(PLATFORM / "main.tf")):
        fail("DB-6", "infra/terraform/platform/main.tf does not pass the encryption key through to "
                     "the database module, so no root could supply one.")

    for tfvars in sorted(ROOTS.glob("*/terraform.tfvars")):
        if "cryptoKeys/" in tfvars.read_text():
            fail("DB-6", "{} names a KMS key. The key belongs to the environment it protects and "
                         "is supplied at apply time, not committed.".format(rel(tfvars)))


def check_export_scope(manifest):
    """DB-7 — exactly the environments the sheet gives a backup target have an export.

    The Environment and Secrets sheet scopes DB_BACKUP_STORAGE_CONNECTION_STRING to SIT, UAT and
    PROD, and TASK-016's manifest carries that scoping. Reading it from the manifest rather than
    listing the three environments here means the sheet stays the source of truth: add the row to
    DEV and this check asks for DEV's export rather than contradicting it.
    """
    for env in manifest["environments"]:
        name = env["name"]
        tfvars = ROOTS / name / "terraform.tfvars"
        if not tfvars.is_file():
            continue
        scheduled = re.search(r"^\s*backup_export_schedule\s*=", tfvars.read_text(), re.M)
        has_target = BACKUP_TARGET_VARIABLE in env["variables"]
        if has_target and not scheduled:
            fail("DB-7", "{} is scoped {} but sets no backup_export_schedule, so nothing is ever "
                         "written to the target that row names.".format(name, BACKUP_TARGET_VARIABLE))
        if scheduled and not has_target:
            fail("DB-7", "{} sets a backup_export_schedule but the sheet gives it no {}, so the "
                         "export has no bucket to write to and the plan refuses.".format(
                             name, BACKUP_TARGET_VARIABLE))


def check_verification_script():
    """DB-8 — every control the verification script's header claims is implemented in its body."""
    text = read(VERIFY)
    if not text:
        return
    header, _, body = text.partition("set -eu")
    claimed = CHECK_ID.findall(header)
    if not claimed:
        fail("DB-8", "{} claims no checks in its header".format(rel(VERIFY)))
    for check in claimed:
        if check not in body:
            fail("DB-8", "{} documents {} and does not implement it. An unexecuted check is not a "
                         "passed check.".format(rel(VERIFY), check))


def main():
    if not MODULE.is_dir():
        print("infra/terraform/database does not exist", file=sys.stderr)
        return 1

    manifest = load_manifest()
    check_deliverables()
    check_transport()
    check_backup_mechanism()
    check_no_retention_invented()
    check_guards()
    check_encryption_key_configurable()
    check_export_scope(manifest)
    check_verification_script()

    if findings:
        for finding in findings:
            print(finding)
        print("\n{} finding(s)".format(len(findings)))
        return 1

    exporting = sorted(e["name"] for e in manifest["environments"]
                       if BACKUP_TARGET_VARIABLE in e["variables"])
    print("OK: TLS-only, no public address, automated backups and point-in-time recovery in the "
          "named region; retention period unset (OQ-003) and guarded for PROD; off-instance export "
          "in {}.".format(", ".join(exporting)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
