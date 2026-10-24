#!/usr/bin/env python3
"""Static checks on the API documentation's publication (TASK-094). Record: docs/api/README.md

    python3 docs/api/api-documentation-check.py

AD-1  the gate generates the document, compares it with the snapshot and publishes it: ci-quality-gates.yml has an
      `api-documentation` job that runs openapi-publish.sh and uploads `openapi-v1`; the ruleset requires the check; the
      promotion pipeline calls the gate, so every build of main publishes it.
AD-2  the docs UI is served where the acceptance criterion names it and nowhere else: DEV and SIT set
      api_documentation = true, UAT and PROD do not, the platform guard refuses it outside DEV and SIT, and the API's
      default is off.
AD-3  the audit sample still names real operations: at least five, each in the snapshot, the first a sign-in.

Standard library only, like the other repo-checks.
"""

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
GATES = ROOT / ".github/workflows/ci-quality-gates.yml"
PIPELINE = ROOT / ".github/workflows/ci-cd-pipeline.yml"
RULESET = ROOT / ".github/branch-protection/protected-branches.ruleset.json"
TERRAFORM = ROOT / "infra/terraform"
APPSETTINGS = ROOT / "src/backend/PMPlatform.Api/appsettings.json"
SNAPSHOT = ROOT / "docs/api/openapi.v1.json"
SAMPLE = ROOT / "docs/api/spec-audit-sample.json"
DOCUMENTED_ENVIRONMENTS = {"dev", "sit"}

findings = []


def fail(check, message):
    findings.append(f"{check}: {message}")


def job(workflow, name):
    """The text of one job of a workflow: from its id to the next job id at the same indent."""
    match = re.search(rf"^  {re.escape(name)}:\n(.*?)(?=^  [a-z][a-z0-9-]*:\n|\Z)", workflow, re.S | re.M)
    return match.group(1) if match else None


def check_publication():
    gates = GATES.read_text(encoding="utf-8")
    body = job(gates, "api-documentation")
    if body is None:
        fail("AD-1", "ci-quality-gates.yml has no api-documentation job")
    else:
        if ".github/scripts/openapi-publish.sh" not in body:
            fail("AD-1", "the api-documentation job does not run .github/scripts/openapi-publish.sh")
        if not re.search(r"uses: actions/upload-artifact@\S+\n\s+with:\n\s+name: openapi-v1\n", body):
            fail("AD-1", "the api-documentation job does not upload the openapi-v1 artifact")
    ruleset = json.loads(RULESET.read_text(encoding="utf-8"))
    required = {c["context"] for r in ruleset["rules"] if r["type"] == "required_status_checks"
                for c in r["parameters"]["required_status_checks"]}
    if "api-documentation" not in required:
        fail("AD-1", "the ruleset does not require the api-documentation check")
    if "uses: ./.github/workflows/ci-quality-gates.yml" not in PIPELINE.read_text(encoding="utf-8"):
        fail("AD-1", "ci-cd-pipeline.yml does not call ci-quality-gates.yml, so main's builds publish no document")


def check_environments():
    for environment in sorted(p.name for p in (TERRAFORM / "environments").iterdir() if p.is_dir()):
        tfvars = (TERRAFORM / "environments" / environment / "terraform.tfvars").read_text(encoding="utf-8")
        enabled = bool(re.search(r"^api_documentation\s*=\s*true\s*$", tfvars, re.M))
        if enabled != (environment in DOCUMENTED_ENVIRONMENTS):
            fail("AD-2", f"{environment} {'serves' if enabled else 'does not serve'} the API docs; DEV and SIT do, UAT and PROD do not")
        if "api_documentation = var.api_documentation" not in (TERRAFORM / "environments" / environment / "main.tf").read_text(encoding="utf-8"):
            fail("AD-2", f"environments/{environment}/main.tf does not pass api_documentation to the platform")
    guards = (TERRAFORM / "platform/guards.tf").read_text(encoding="utf-8")
    if '!var.api_documentation || contains(["dev", "sit"], var.environment)' not in guards:
        fail("AD-2", "platform/guards.tf does not refuse api_documentation outside DEV and SIT")
    if 'var.api_documentation ? { ApiDocumentation__Enabled = "true" } : {}' not in (TERRAFORM / "platform/main.tf").read_text(encoding="utf-8"):
        fail("AD-2", "platform/main.tf does not turn api_documentation into ApiDocumentation__Enabled")
    if json.loads(APPSETTINGS.read_text(encoding="utf-8")).get("ApiDocumentation", {}).get("Enabled") is not False:
        fail("AD-2", "appsettings.json does not default ApiDocumentation:Enabled to false")


def check_sample():
    paths = json.loads(SNAPSHOT.read_text(encoding="utf-8"))["paths"]
    samples = json.loads(SAMPLE.read_text(encoding="utf-8"))["samples"]
    if len(samples) < 5:
        fail("AD-3", f"the audit sample has {len(samples)} endpoints; the acceptance criterion asks for at least 5")
    if not samples or not samples[0].get("signIn"):
        fail("AD-3", "the audit sample's first endpoint is not the sign-in that authenticates the rest")
    for entry in samples:
        method, path = entry["operation"].split(" ", 1)
        if method.lower() not in paths.get(path, {}):
            fail("AD-3", f"{entry['operation']} is in the audit sample but not in {SNAPSHOT.name}")


def main():
    check_publication()
    check_environments()
    check_sample()
    for finding in findings:
        print(finding)
    if findings:
        return 1
    print("OK: the document is generated, compared with the snapshot and published by the api-documentation check on every "
          "build; the docs UI is served in DEV and SIT only; the audit sample names real operations")
    return 0


if __name__ == "__main__":
    sys.exit(main())
