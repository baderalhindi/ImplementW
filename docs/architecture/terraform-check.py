#!/usr/bin/env python3
"""Static checks on the infrastructure code (TASK-017).

`terraform validate` proves the configuration is well formed and `terraform plan` proves it agrees
with the provider. Neither proves what this task's acceptance criteria actually turn on: that the
IaC and infra/environments/environments.json (TASK-016) still describe the same four environments,
that no hosting parameter has been written into the Terraform where the manifest already holds it,
and that no credential is on its way into the repository.

Those are the mistakes a reviewer misses and a plan does not catch, because a hardcoded region or a
duplicated project id plans perfectly well. They are caught here instead, in the `repo-checks` CI
job, with no Terraform binary and no cloud credentials — standard library only.

    python3 docs/architecture/terraform-check.py
"""
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
TERRAFORM = ROOT / "infra/terraform"
MANIFEST = ROOT / "infra/environments/environments.json"
WORKFLOW = ROOT / ".github/workflows/ci-quality-gates.yml"
VERSION_FILE = TERRAFORM / ".terraform-version"

# The five deliverables of TASK-017, plus the composition the four roots share.
MODULES = ["network", "compute", "database", "storage", "load-balancer"]
COMPOSITION = "platform"

# Google's region in Saudi Arabia is me-central2 (Dammam). Any other region literal in the
# Terraform is either a mistake or a second place the region is written down, and ADR-001 C-1
# admits neither. me-central1 is Doha, Qatar.
REGION_LITERAL = re.compile(r'"((?:asia|australia|europe|northamerica|southamerica|us|me|africa)-[a-z]+\d+)"')

# Functions whose value changes between two runs. Any one of them in the configuration means a
# `terraform plan` on an unchanged state reports a change, which is acceptance criterion 2 failing
# by construction rather than by drift. uuidv5() is deterministic and is not in this list.
NONDETERMINISTIC = re.compile(r"\b(timestamp|plantimestamp|uuid|bcrypt)\s*\(")

# Anything that looks like a credential on its way into the repository (CTL-18, CTL-48).
CREDENTIAL_PATTERNS = [
    (re.compile(r"^\s*credentials\s*=", re.M), "a provider credentials argument"),
    (re.compile(r"-----BEGIN [A-Z ]*PRIVATE KEY-----"), "a private key"),
    (re.compile(r'"private_key(_id)?"\s*:'), "a service-account key file"),
    (re.compile(r'^\s*password\s*=\s*"[^"]+"', re.M), "a literal password"),
]

findings = []


def fail(check, message):
    findings.append("{}: {}".format(check, message))


def load_manifest():
    try:
        return json.loads(MANIFEST.read_text())
    except (OSError, json.JSONDecodeError) as exc:
        print("cannot read {}: {}".format(MANIFEST.relative_to(ROOT), exc), file=sys.stderr)
        sys.exit(1)


def terraform_files():
    return sorted(p for p in TERRAFORM.rglob("*.tf") if ".terraform" not in p.parts)


def rel(path):
    return str(path.relative_to(ROOT))


def check_layout():
    """T-1 — the five deliverable modules and the composition exist, each with the three files."""
    for name in MODULES + [COMPOSITION]:
        directory = TERRAFORM / name
        if not directory.is_dir():
            fail("T-1", "module '{}' is missing from infra/terraform".format(name))
            continue
        for filename in ("main.tf", "variables.tf", "outputs.tf", "versions.tf"):
            if not (directory / filename).is_file():
                fail("T-1", "{} has no {}".format(rel(directory), filename))


def check_roots(manifest):
    """T-2 — one root per environment, no more, each wired to its own state bucket and name."""
    declared = [e["name"] for e in manifest["environments"]]
    roots_dir = TERRAFORM / "environments"
    if not roots_dir.is_dir():
        fail("T-2", "infra/terraform/environments does not exist")
        return
    present = sorted(p.name for p in roots_dir.iterdir() if p.is_dir())
    if present != sorted(declared):
        fail("T-2", "environment roots are {}, but the manifest declares {}".format(present, sorted(declared)))

    for env in manifest["environments"]:
        root = roots_dir / env["name"]
        if not root.is_dir():
            continue

        backend = root / "backend.tf"
        if not backend.is_file():
            fail("T-2", "{} has no backend.tf; its state would be written to disk beside the code".format(rel(root)))
        else:
            text = backend.read_text()
            bucket = re.search(r'bucket\s*=\s*"([^"]+)"', text)
            if bucket is None:
                fail("T-2", "{} declares no state bucket".format(rel(backend)))
            elif bucket.group(1) != env["state_bucket"]:
                fail("T-2", "{} points at '{}'; the manifest gives {} the bucket '{}'".format(
                    rel(backend), bucket.group(1), env["name"], env["state_bucket"]))
            if 'backend "gcs"' not in text:
                fail("T-2", "{} does not use the gcs backend".format(rel(backend)))

        main = root / "main.tf"
        if not main.is_file():
            fail("T-2", "{} has no main.tf".format(rel(root)))
            continue
        text = main.read_text()
        named = re.search(r'environment\s*=\s*"([^"]+)"', text)
        if named is None:
            fail("T-2", "{} does not name its environment".format(rel(main)))
        elif named.group(1) != env["name"]:
            fail("T-2", "{} is in the '{}' directory but names environment '{}'".format(
                rel(main), env["name"], named.group(1)))


def check_no_hosting_literals(manifest):
    """T-3 — no region, project, network, instance, secret or service account written twice.

    The manifest is the source of truth (TASK-016), and its CI check already proves no identifier
    is shared between two environments. A copy in the Terraform is a second truth that drifts.
    """
    allowlist = set(manifest["platform"].get("region_allowlist", []))
    owned = {}
    for env in manifest["environments"]:
        for label, value in (
            ("project_id", env["project_id"]),
            ("network", env["network"]),
            ("database.instance", env["database"]["instance"]),
            ("database.user", env["database"]["user"]),
            ("service_accounts.deploy", env["service_accounts"]["deploy"]),
            ("service_accounts.runtime", env["service_accounts"]["runtime"]),
        ):
            owned[value] = "{}.{}".format(env["name"], label)
        for name, spec in env["variables"].items():
            if spec.get("kind") == "secret":
                owned[spec["secret_id"]] = "{}.variables.{}.secret_id".format(env["name"], name)

    for path in terraform_files():
        text = path.read_text()
        for match in REGION_LITERAL.finditer(text):
            region = match.group(1)
            where = "not on platform.region_allowlist" if region not in allowlist else "already in the manifest"
            fail("T-3", "{} writes the region '{}' into the configuration ({}). The region is UGV-07 "
                        "and is read from platform.region; a second copy is a second truth.".format(
                            rel(path), region, where))
        for value, source in owned.items():
            if '"{}"'.format(value) in text:
                fail("T-3", "{} hardcodes '{}', which the manifest already holds as {}".format(
                    rel(path), value, source))


def check_state_buckets_not_shared(manifest):
    """T-4 — no two roots write to one state bucket.

    The backend block takes no variables, so these four names are the one place a manifest value is
    restated. Sharing one bucket would put PROD state inside a non-PROD boundary (CTL-02).
    """
    seen = {}
    for env in manifest["environments"]:
        backend = TERRAFORM / "environments" / env["name"] / "backend.tf"
        if not backend.is_file():
            continue
        found = re.search(r'bucket\s*=\s*"([^"]+)"', backend.read_text())
        if found is None:
            continue
        bucket = found.group(1)
        if bucket in seen:
            fail("T-4", "{} and {} both write state to '{}'".format(seen[bucket], env["name"], bucket))
        seen[bucket] = env["name"]


def check_no_credentials():
    """T-5 — no credential, key or password reaches the repository (CTL-18, CTL-48)."""
    for path in list(terraform_files()) + sorted(TERRAFORM.rglob("*.tfvars")):
        if ".terraform" in path.parts:
            continue
        text = path.read_text()
        for pattern, what in CREDENTIAL_PATTERNS:
            if pattern.search(text):
                fail("T-5", "{} contains {}. Deploy credentials live in the CI/CD secret store and "
                            "are never committed (Environment and Secrets: DEPLOY_SERVICE_ACCOUNT_KEY).".format(
                                rel(path), what))


def check_toolchain_pinned():
    """T-6 — one Terraform version and one provider version, pinned everywhere and in CI."""
    if not VERSION_FILE.is_file():
        fail("T-6", "infra/terraform/.terraform-version is missing; the toolchain is unpinned")
        pinned = None
    else:
        pinned = VERSION_FILE.read_text().strip()
        if not re.fullmatch(r"\d+\.\d+\.\d+", pinned):
            fail("T-6", "infra/terraform/.terraform-version holds '{}', which is not an exact version".format(pinned))

    if pinned and WORKFLOW.is_file() and pinned not in WORKFLOW.read_text():
        fail("T-6", "{} does not pin Terraform {}; CI would run a different version from the one "
                    "the lock files were produced with".format(rel(WORKFLOW), pinned))

    provider_pins = set()
    for path in terraform_files():
        text = path.read_text()
        if "required_providers" not in text:
            continue
        found = re.search(r'source\s*=\s*"hashicorp/google"\s*\n\s*version\s*=\s*"([^"]+)"', text)
        if found is None:
            fail("T-6", "{} declares required_providers without a pinned google provider version".format(rel(path)))
        else:
            provider_pins.add(found.group(1))
    if len(provider_pins) > 1:
        fail("T-6", "the google provider is pinned to {} in different places; one configuration, "
                    "one provider version".format(sorted(provider_pins)))


def check_lock_files(manifest):
    """T-7 — every root carries a committed lock file, for the same version, for more than one platform.

    Without a lock file CI resolves providers itself, and a plan that was clean on a reviewer's
    machine can differ from the one that applies. A lock file produced by a bare `terraform init`
    on one machine records that machine's platform only, so CI on linux_amd64 re-resolves and the
    lock stops meaning anything: `terraform providers lock -platform=...` is what fills it.
    """
    versions = {}
    for env in manifest["environments"]:
        lock = TERRAFORM / "environments" / env["name"] / ".terraform.lock.hcl"
        if not lock.is_file():
            fail("T-7", "{} has no .terraform.lock.hcl".format(rel(lock.parent)))
            continue
        text = lock.read_text()
        found = re.search(r'version\s*=\s*"([^"]+)"', text)
        if found is None:
            fail("T-7", "{} records no provider version".format(rel(lock)))
        else:
            versions.setdefault(found.group(1), []).append(env["name"])
        platforms = len(re.findall(r'"h1:', text))
        if platforms < 2:
            fail("T-7", "{} is locked for {} platform(s). Regenerate it with `terraform providers "
                        "lock -platform=linux_amd64 -platform=darwin_arm64 -platform=darwin_amd64`, "
                        "or CI will resolve providers for itself.".format(rel(lock), platforms))
    if len(versions) > 1:
        fail("T-7", "the four roots are locked to different provider versions: {}. One artifact is "
                    "promoted through all four; one provider builds them.".format(
                        {v: sorted(e) for v, e in versions.items()}))


def check_address_plan(manifest):
    """T-8 — no two environments are given the same address range.

    The four VPCs are not peered, so an overlap breaks nothing today. It breaks the day one of them
    is connected to an AHDA network, and renumbering a live environment is a rebuild.
    """
    ranges = {}
    for env in manifest["environments"]:
        tfvars = TERRAFORM / "environments" / env["name"] / "terraform.tfvars"
        if not tfvars.is_file():
            fail("T-8", "{} has no terraform.tfvars".format(rel(tfvars.parent)))
            continue
        text = tfvars.read_text()
        for key in ("app_subnet_cidr", "proxy_subnet_cidr", "private_services_cidr"):
            found = re.search(r'{}\s*=\s*"([^"]+)"'.format(key), text)
            if found is None:
                fail("T-8", "{} does not set {}".format(rel(tfvars), key))
                continue
            cidr = found.group(1)
            if cidr in ranges:
                fail("T-8", "{} and {} are both given the range {}".format(ranges[cidr], env["name"], cidr))
            ranges[cidr] = env["name"]


def check_plan_determinism():
    """T-9 — nothing in the configuration makes a plan differ from the last one.

    Acceptance criterion 2 is that a plan on the current state produces no unexpected diff, and the
    validation cell is two consecutive plans with zero diff between them. Only an applied
    environment can prove that. What can be proved here are the two configuration-level ways to
    lose it: a function whose value changes between runs, and a resource whose drift is hidden
    rather than removed.
    """
    for path in terraform_files():
        text = path.read_text()
        for match in NONDETERMINISTIC.finditer(text):
            line = text[: match.start()].count("\n") + 1
            fail("T-9", "{}:{} calls {}(), whose value changes between runs. Every plan would then "
                        "report a change against an unchanged state.".format(
                            rel(path), line, match.group(1)))
        if re.search(r"ignore_changes\s*=", text):
            line = text[: re.search(r"ignore_changes\s*=", text).start()].count("\n") + 1
            fail("T-9", "{}:{} uses lifecycle.ignore_changes. It hides drift rather than removing "
                        "it, and acceptance criterion 1 is that 100% of the infrastructure is "
                        "created by terraform apply with zero manual console changes. If an "
                        "exception is genuinely needed, record it in "
                        "docs/architecture/infrastructure-as-code.md and amend this check.".format(
                            rel(path), line))


def main():
    if not TERRAFORM.is_dir():
        print("infra/terraform does not exist", file=sys.stderr)
        return 1

    manifest = load_manifest()
    check_layout()
    check_roots(manifest)
    check_no_hosting_literals(manifest)
    check_state_buckets_not_shared(manifest)
    check_no_credentials()
    check_toolchain_pinned()
    check_lock_files(manifest)
    check_address_plan(manifest)
    check_plan_determinism()

    if findings:
        for finding in findings:
            print(finding)
        print("\n{} finding(s)".format(len(findings)))
        return 1

    region = manifest["platform"]["region"] or "UNNAMED (UGV-07)"
    print("OK: {} modules, {} environment roots, region {}; no hosting value duplicated outside "
          "infra/environments/environments.json, no credential committed.".format(
              len(MODULES), len(manifest["environments"]), region))
    return 0


if __name__ == "__main__":
    sys.exit(main())
