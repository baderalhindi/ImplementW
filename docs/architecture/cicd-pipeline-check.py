#!/usr/bin/env python3
"""Static checks on the promotion pipeline (TASK-018).

A workflow file is only correct in the way that matters if its *shape* is correct: which job needs
which, which job names which deployment environment, and which values travel from the build into
each deployment. Those are the three things CTL-40 turns on, and all three are edits a reviewer
waves through — deleting one entry from a `needs:` list looks like tidying and silently removes a
gate; changing `environment: prod` to `environment: uat` in the PROD stage removes the approval
requirement while leaving the job named `deploy-prod`.

None of it can be caught by running the pipeline, because the pipeline cannot run until the hosting
parameters arrive. It is caught here instead, in the `repo-checks` CI job, with no runner, no cloud
and no third-party module.

    python3 docs/architecture/cicd-pipeline-check.py
"""
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PIPELINE = ROOT / ".github/workflows/ci-cd-pipeline.yml"
QUALITY_GATES = ROOT / ".github/workflows/ci-quality-gates.yml"
DEPLOY_ACTION = ROOT / ".github/actions/deploy-environment/action.yml"
MANIFEST = ROOT / "infra/environments/environments.json"
GATES = ROOT / "infra/environments/github/environments.json"

SCRIPTS = [
    ROOT / ".github/scripts/release-metadata.sh",
    ROOT / ".github/scripts/deploy-preflight.sh",
    ROOT / ".github/scripts/migration-dry-run.sh",
    ROOT / ".github/scripts/migrate-environment.sh",
    ROOT / ".github/scripts/deployment-record.sh",
]

# The gates CTL-40 names. Each must be a need of the first deploy stage, so a red one skips all four.
REQUIRED_GATES = ["quality-gates", "package", "migration-dry-run"]

# Same rule as terraform-check.py T-3: a hosting value written anywhere but the manifest is a second
# truth, and it drifts. me-central1 is Doha, Qatar.
REGION_LITERAL = re.compile(r'"?((?:asia|australia|europe|northamerica|southamerica|us|me|africa)-[a-z]+\d+)"?')

CREDENTIAL_PATTERNS = [
    (re.compile(r"-----BEGIN [A-Z ]*PRIVATE KEY-----"), "a private key"),
    (re.compile(r'"private_key(_id)?"\s*:'), "a service-account key file"),
    (re.compile(r"\bAIza[0-9A-Za-z_-]{35}\b"), "a Google API key"),
    (re.compile(r"\bghp_[0-9A-Za-z]{36}\b"), "a GitHub token"),
]

findings = []


def fail(check, message):
    findings.append("{}: {}".format(check, message))


# --- A YAML reader for the subset these files use -------------------------------------------------
# Not a YAML parser. It reads block mappings, block sequences, block scalars and plain or quoted
# scalars, which is everything the three files in scope contain, and raises on anything else rather
# than guessing. The alternative is PyYAML, which is not in the standard library and so is not
# available to a `repo-checks` job that deliberately installs nothing.

class YamlError(Exception):
    pass


def _strip_comment(line):
    out, quote = [], None
    for i, char in enumerate(line):
        if quote:
            out.append(char)
            if char == quote:
                quote = None
        elif char in "'\"":
            quote = char
            out.append(char)
        elif char == "#" and (i == 0 or line[i - 1] in " \t"):
            break
        else:
            out.append(char)
    return "".join(out).rstrip()


def _scalar(text):
    text = text.strip()
    if len(text) >= 2 and text[0] == text[-1] and text[0] in "'\"":
        return text[1:-1]
    if text.startswith("[") and text.endswith("]"):
        inner = text[1:-1].strip()
        return [_scalar(part) for part in inner.split(",")] if inner else []
    return text


def _indent(line):
    return len(line) - len(line.lstrip(" "))


# A mapping entry is `key:` followed by a space or the end of the line. Without that rule a
# sequence item like `- 5432:5432` reads as a mapping, which is how a port becomes a port range.
KEY = re.compile(r"^([^:\s][^:]*):(?:\s+(.*))?$")


def _split_key(text):
    match = KEY.match(text.rstrip())
    if not match:
        return None, None
    return _scalar(match.group(1)), (match.group(2) or "").strip()


def _read_block_scalar(lines, index, parent_indent, style):
    """Literal (|) keeps the line breaks; folded (>) joins the lines with spaces. Both keep the
    indentation *relative* to the block, which is what makes an indented shell body survive."""
    body = []
    base = None
    while index < len(lines):
        raw = lines[index]
        if raw.strip() and _indent(raw) <= parent_indent:
            break
        if raw.strip() and base is None:
            base = _indent(raw)
        body.append(raw[base:] if base is not None and len(raw) >= base else raw.strip())
        index += 1
    joined = " ".join(line.strip() for line in body) if style.startswith(">") else "\n".join(body)
    return joined, index


def _parse_block(lines, index, indent):
    if index >= len(lines):
        return None, index
    if lines[index].lstrip().startswith("- "):
        return _parse_sequence(lines, index, indent)
    return _parse_mapping(lines, index, indent)


def _parse_sequence(lines, index, indent):
    items = []
    while index < len(lines):
        raw = lines[index]
        if _indent(raw) != indent or not raw.lstrip().startswith("- "):
            break
        rest = raw.lstrip()[2:]
        item_indent = indent + 2
        key, _ = _split_key(rest)
        if key is not None:
            # A sequence item that is itself a mapping; re-read it as one, with its first key
            # placed at the item's own indent.
            synthetic = [" " * item_indent + rest] + lines[index + 1:]
            value, consumed = _parse_mapping(synthetic, 0, item_indent)
            items.append(value)
            index += consumed
        else:
            items.append(_scalar(rest))
            index += 1
    return items, index


def _parse_mapping(lines, index, indent):
    mapping = {}
    while index < len(lines):
        raw = lines[index]
        current = _indent(raw)
        if current < indent:
            break
        if current > indent:
            raise YamlError("unexpected indent at line: {}".format(raw))
        if raw.lstrip().startswith("- "):
            break
        key, rest = _split_key(raw.strip())
        if key is None:
            raise YamlError("not a mapping entry: {}".format(raw))
        index += 1
        if rest in ("|", "|-", ">", ">-", "|+", ">+"):
            mapping[key], index = _read_block_scalar(lines, index, indent, rest)
        elif rest:
            mapping[key] = _scalar(rest)
        else:
            nested_indent = None
            for look in range(index, len(lines)):
                if lines[look].strip():
                    nested_indent = _indent(lines[look])
                    break
            if nested_indent is None or nested_indent <= indent:
                mapping[key] = None
            else:
                mapping[key], index = _parse_block(lines, index, nested_indent)
    return mapping, index


def load_yaml(path):
    lines = []
    for raw in path.read_text().splitlines():
        stripped = _strip_comment(raw)
        if stripped.strip():
            lines.append(stripped)
    value, _ = _parse_block(lines, 0, 0)
    return value or {}


def load_json(path):
    return json.loads(path.read_text())


def as_list(value):
    if value is None:
        return []
    return value if isinstance(value, list) else [value]


# --- The checks -----------------------------------------------------------------------------------

def check_deliverables():
    """P-1 — the files the pipeline is made of exist, and the scripts are executable."""
    for path in [PIPELINE, QUALITY_GATES, DEPLOY_ACTION, MANIFEST, GATES] + SCRIPTS:
        if not path.exists():
            fail("P-1", "{} does not exist".format(path.relative_to(ROOT)))
    for script in SCRIPTS:
        if script.exists() and not script.stat().st_mode & 0o111:
            fail("P-1", "{} is not executable".format(script.relative_to(ROOT)))


def check_promotion_order(jobs, manifest):
    """P-2 — the stages are the manifest's environments, chained in the manifest's own order."""
    order = []
    current = manifest["environments"][0]["name"]
    by_name = {env["name"]: env for env in manifest["environments"]}
    while current:
        order.append(current)
        if current not in by_name:
            fail("P-2", "environments.json promotes to '{}', which it does not declare".format(current))
            return []
        current = by_name[current].get("promotes_to")

    for environment in order:
        if "deploy-{}".format(environment) not in jobs:
            fail("P-2", "no deploy-{} job; the manifest's promotion path is {}".format(
                environment, " -> ".join(order)))

    for previous, environment in zip(order, order[1:]):
        job = jobs.get("deploy-{}".format(environment))
        if job is None:
            continue
        needs = as_list(job.get("needs"))
        if "deploy-{}".format(previous) not in needs:
            fail("P-2", "deploy-{} does not need deploy-{}, so {} can be deployed to without {}".format(
                environment, previous, environment.upper(), previous.upper()))
    return order


def check_gates(jobs, order):
    """P-3 — every gate CTL-40 names blocks the first stage, and each stage blocks the next."""
    if not order:
        return
    first = jobs.get("deploy-{}".format(order[0]))
    if first is None:
        return
    needs = as_list(first.get("needs"))
    for gate in REQUIRED_GATES:
        if gate not in needs:
            fail("P-3", "deploy-{} does not need '{}', so a failed {} would not block promotion".format(
                order[0], gate, gate))
        if gate not in jobs:
            fail("P-3", "the pipeline declares no '{}' job".format(gate))

    for environment in order:
        job = jobs.get("deploy-{}".format(environment))
        if job is None:
            continue
        condition = str(job.get("if", ""))
        if "success()" not in condition:
            fail("P-3", "deploy-{}'s `if` does not call success(), so it could run after a failed need"
                        .format(environment))
        if "needs.preflight.outputs.ready" not in condition:
            fail("P-3", "deploy-{} does not require preflight readiness".format(environment))


def check_approval(jobs, order, gates):
    """P-4 — each stage names its own deployment environment, and the gated ones keep their reviewers."""
    declared = {entry["name"]: entry for entry in gates["environments"]}
    for environment in order:
        job = jobs.get("deploy-{}".format(environment))
        if job is None:
            continue
        target = job.get("environment")
        name = target.get("name") if isinstance(target, dict) else target
        if name != environment:
            fail("P-4", "deploy-{} deploys to the '{}' environment; a stage that does not name its own "
                        "environment does not get its approval gate".format(environment, name))
        if environment not in declared:
            fail("P-4", "'{}' is not declared in {}".format(environment, GATES.relative_to(ROOT)))

    terminal = order[-1] if order else None
    if terminal and terminal in declared and not declared[terminal].get("reviewer_teams"):
        fail("P-4", "the '{}' environment names no reviewer team, so the deployment at the end of the "
                    "promotion path would not wait for an approval (CTL-40)".format(terminal))


def check_traceability(jobs, order, action):
    """P-5 — one artifact, by digest, and a Task ID on every deployment."""
    steps = as_list(action.get("runs", {}).get("steps"))
    if not any("deployment-record.sh" in str(step.get("run", "")) for step in steps):
        fail("P-5", "the deploy action does not run deployment-record.sh, so a deployment would leave "
                    "no record of its commit, task or approver")

    for environment in order:
        job = jobs.get("deploy-{}".format(environment))
        if job is None:
            continue
        step = next((s for s in as_list(job.get("steps")) if "deploy-environment" in str(s.get("uses", ""))), None)
        if step is None:
            fail("P-5", "deploy-{} does not use the shared deploy action".format(environment))
            continue
        inputs = step.get("with") or {}
        digest = str(inputs.get("image-digest", ""))
        if "needs.package.outputs.image_digest" not in digest:
            fail("P-5", "deploy-{} does not deploy the digest built by `package` ({!r}); one artifact is "
                        "built once and promoted unchanged".format(environment, digest))
        if "release-metadata.outputs.task_id" not in str(inputs.get("task-id", "")):
            fail("P-5", "deploy-{} does not carry the release's Task ID (CTL-40)".format(environment))
        if str(inputs.get("environment", "")) != environment:
            fail("P-5", "deploy-{} passes environment={!r} to the deploy action".format(
                environment, inputs.get("environment")))


def check_no_hosting_values(manifest):
    """P-6 — no hosting value or identifier is written into the pipeline; the manifest holds them."""
    identifiers = set()
    for environment in manifest["environments"]:
        identifiers.update([
            environment["project_id"], environment["network"], environment["state_bucket"],
            environment["database"]["instance"], environment["database"]["user"],
            environment["secret_store"]["namespace"],
            environment["service_accounts"]["deploy"], environment["service_accounts"]["runtime"],
        ])

    for path in [PIPELINE, QUALITY_GATES, DEPLOY_ACTION] + SCRIPTS:
        if not path.exists():
            continue
        text = path.read_text()
        name = path.relative_to(ROOT)
        for identifier in sorted(identifiers):
            if identifier and identifier in text:
                fail("P-6", "{} writes down '{}', which belongs to {} alone".format(
                    name, identifier, MANIFEST.relative_to(ROOT)))
        for match in REGION_LITERAL.finditer(text):
            fail("P-6", "{} names the region '{}'; the region is read from the manifest and written "
                        "nowhere else (ADR-001 C-1)".format(name, match.group(1)))


def check_no_credentials():
    """P-7 — no credential is on its way into the repository, and secrets arrive only as secrets."""
    for path in [PIPELINE, DEPLOY_ACTION] + SCRIPTS:
        if not path.exists():
            continue
        text = path.read_text()
        name = path.relative_to(ROOT)
        for pattern, description in CREDENTIAL_PATTERNS:
            if pattern.search(text):
                fail("P-7", "{} contains what looks like {} (CTL-18, CTL-48)".format(name, description))
        # The two secrets the workbook assigns this task may be *referenced*, and their values may
        # only ever come from the platform's own secret store. A line that assigns one of them
        # anything else is a credential arriving by another route (CTL-48).
        for secret in ("DEPLOY_SERVICE_ACCOUNT_KEY", "CONTAINER_REGISTRY_TOKEN"):
            assignment = re.compile(r"^\s*{}\s*[:=]".format(secret), re.M)
            for line in text.splitlines():
                if assignment.match(line) and "secrets.{}".format(secret) not in line:
                    fail("P-7", "{} assigns {} from something other than the secret store: {}".format(
                        name, secret, line.strip()))


def check_permissions(pipeline):
    """P-8 — the pipeline takes no more of the token than it reads with."""
    permissions = pipeline.get("permissions")
    if not isinstance(permissions, dict) or permissions.get("contents") != "read":
        fail("P-8", "the workflow does not declare `permissions: contents: read` at the top level")
    for key, value in (permissions or {}).items():
        if key != "contents" and value == "write":
            fail("P-8", "the workflow grants write on '{}'".format(key))


def check_quality_gate_wiring(pipeline, quality_gates):
    """P-9 — the gate is called, not copied, and not run twice on the same commit."""
    triggers = quality_gates.get("on") or {}
    if "workflow_call" not in triggers:
        fail("P-9", "ci-quality-gates.yml is not callable, so the pipeline cannot gate on it")
    push_branches = as_list((triggers.get("push") or {}).get("branches"))
    if "main" in push_branches:
        fail("P-9", "ci-quality-gates.yml still runs on pushes to main, which runs the same checks "
                    "twice on a release commit and gates the deployment on only one of them")

    job = (pipeline.get("jobs") or {}).get("quality-gates") or {}
    if job.get("uses") != "./.github/workflows/ci-quality-gates.yml":
        fail("P-9", "the pipeline's quality-gates job does not call ci-quality-gates.yml (it is {!r})"
                    .format(job.get("uses")))


def check_manifest_registry(manifest):
    """P-10 — the registry the four environments pull from is declared, in one place."""
    registry = (manifest.get("platform") or {}).get("artifact_registry")
    if not isinstance(registry, dict):
        fail("P-10", "environments.json declares no platform.artifact_registry")
        return
    for field in ("project_id", "repository", "image"):
        if field not in registry:
            fail("P-10", "platform.artifact_registry has no '{}'".format(field))
    unresolved = (manifest.get("platform") or {}).get("unresolved") or {}
    if not registry.get("project_id") and "artifact_registry.project_id" not in unresolved:
        fail("P-10", "platform.artifact_registry.project_id is empty and is not listed in "
                     "platform.unresolved, so nothing records who owes it")


def main():
    check_deliverables()
    if findings:
        report()

    try:
        pipeline = load_yaml(PIPELINE)
        quality_gates = load_yaml(QUALITY_GATES)
        action = load_yaml(DEPLOY_ACTION)
    except YamlError as exc:
        print("cannot read the workflow files: {}".format(exc), file=sys.stderr)
        return 1

    manifest = load_json(MANIFEST)
    gates = load_json(GATES)
    jobs = pipeline.get("jobs") or {}

    order = check_promotion_order(jobs, manifest)
    check_gates(jobs, order)
    check_approval(jobs, order, gates)
    check_traceability(jobs, order, action)
    check_no_hosting_values(manifest)
    check_no_credentials()
    check_permissions(pipeline)
    check_quality_gate_wiring(pipeline, quality_gates)
    check_manifest_registry(manifest)

    return report(order)


def report(order=None):
    if findings:
        print("{} finding(s):".format(len(findings)), file=sys.stderr)
        for finding in findings:
            print("  " + finding, file=sys.stderr)
        sys.exit(1)
    print("OK: promotion path {}; every gate blocks the first stage and every stage blocks the next; "
          "one artifact by digest, traceable to a commit and a Task ID.".format(
              " -> ".join(order or [])))
    sys.exit(0)


if __name__ == "__main__":
    main()
