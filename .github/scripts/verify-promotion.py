#!/usr/bin/env python3
"""TASK-018's validation checks, executed as far as they can be without a cloud.

The workbook asks for two things: "trigger a pipeline run end to end against a non-PROD environment
and confirm all stages execute in order" and "confirm a PROD deploy attempt halts and waits for
approval". Neither can be done against a real environment — the region, the tenancy, the DNS zone and
the artifact registry project are outstanding, so nothing is provisioned — and both are about the
*pipeline's* behaviour rather than the cloud's.

So this runs the pipeline. It is not a second copy of it: it reads ci-cd-pipeline.yml and the deploy
action with the same YAML reader `docs/architecture/cicd-pipeline-check.py` uses, orders the jobs by
their own `needs`, evaluates their own `if`, and runs their own `run:` steps. What it stands in for
is everything outside the repository — gcloud, terraform, the registry, the approvals API and the
health endpoint are stubs, and the manifest carries placeholder hosting values.

What it therefore does **not** prove: that GitHub halts a job on a protected environment (that is
GitHub's behaviour, and it needs the four environments to exist), that the image builds and scans
(those are third-party actions, not run here), or that any cloud call succeeds. What it does prove
is that the stages run in the declared order, that each stage's own steps run in order, that an
unapproved gated environment is refused before anything is applied, and that one digest reaches all
four environments unchanged.

    .github/scripts/verify-promotion.py                        # PROD unapproved: the run must halt
    .github/scripts/verify-promotion.py --approve sit,uat,prod # the full promotion, approvals recorded

Nothing in the repository is modified: the placeholder manifest, the stubs and the deployment
records are written to a temporary directory that is removed on exit.
"""
import argparse
import importlib.util
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CHECK = ROOT / "docs/architecture/cicd-pipeline-check.py"

spec = importlib.util.spec_from_file_location("cicd_pipeline_check", CHECK)
check = importlib.util.module_from_spec(spec)
spec.loader.exec_module(check)

EXPRESSION = re.compile(r"\$\{\{(.+?)\}\}")

# Stand-ins for the reviewers the promotion path names (environment-separation.md §4). The logins are
# obviously not real; what is being exercised is that an identity and a timestamp reach the record.
APPROVERS = {
    "sit": ("sit-reviewer", "Migration dry-run clean; artifact scan clean."),
    "uat": ("uat-reviewer", "The 15 cross-domain journeys pass against SIT; no open High finding."),
    "prod": ("prod-reviewer", "UAT acceptance signed; rollback rehearsed within 30 days."),
}

# Placeholder values for the five outstanding items, so the pipeline can be exercised past preflight.
# me-central2 is the one candidate on the manifest's own allowlist; the rest are obviously not real.
PLACEHOLDERS = {
    "region": "me-central2",
    "organization_id": "000000000000",
    "billing_account_id": "000000-000000-000000",
    "base_domain": "example.invalid",
    "artifact_registry_project": "ahda-pmplatform-artifacts",
}

STUBS = {
    "gcloud": 'echo "[stub gcloud] $*"',
    "terraform": 'echo "[stub terraform] $*"\ncase "$1" in\n  init) echo "Terraform has been successfully initialized!";;\n  apply) echo "Apply complete!";;\nesac',
    "curl": "printf '200'",
    "gh": 'cat "${APPROVALS_FILE:?APPROVALS_FILE unset}"',
    "dotnet": 'echo "[stub dotnet] $*"',
    "docker": 'case "$1" in\n  inspect) echo "$IMAGE_REPOSITORY@sha256:0e7d1c9a5b3f4e2d8a6c0b9f1e3d5a7c2b4e6f8a0c1d3e5f7a9b1c3d5e7f9a1b";;\n  *) echo "[stub docker] $*";;\nesac',
}


class Halt(Exception):
    def __init__(self, job, step, code):
        super().__init__("{} halted at '{}' (exit {})".format(job, step, code))
        self.job, self.step, self.code = job, step, code


class Context:
    """The expression contexts the pipeline uses, and nothing else: an unrecognised expression stops
    the drill rather than resolving to an empty string that would make a step pass for the wrong
    reason."""

    def __init__(self, commit_sha):
        self.needs, self.steps, self.inputs = {}, {}, {}
        self.github = {"sha": commit_sha, "token": "drill-token", "run_id": "0",
                       "repository": "baderalhindi/ImplementW", "server_url": "https://github.com"}
        self.secrets = {"DEPLOY_SERVICE_ACCOUNT_KEY": '{"type":"service_account"}',
                        "CONTAINER_REGISTRY_TOKEN": "drill-registry-token"}
        self.vars = {"ADR001_CONFIRMATION_REF": "DRILL — not a real authorisation", "MIGRATION_JOB": ""}

    def lookup(self, path):
        parts = path.split(".")
        table = {"needs": lambda: self.needs.get(parts[1], {}).get(parts[3], ""),
                 "steps": lambda: self.steps.get(parts[1], {}).get(parts[3], ""),
                 "inputs": lambda: self.inputs.get(parts[1], ""),
                 "secrets": lambda: self.secrets.get(parts[1], ""),
                 "vars": lambda: self.vars.get(parts[1], ""),
                 "github": lambda: self.github.get(parts[1], "")}
        if parts[0] not in table:
            raise SystemExit("the drill cannot resolve the context {!r}".format(path))
        return table[parts[0]]()

    def expand(self, text):
        if not isinstance(text, str):
            return text

        def one(match):
            body = match.group(1).strip()
            if "||" in body:
                for side in (part.strip() for part in body.split("||")):
                    value = self.lookup(side)
                    if value:
                        return str(value)
                return ""
            return str(self.lookup(body))

        return EXPRESSION.sub(one, text)

    def condition(self, text):
        if not text:
            return True
        body = text.strip()
        match = EXPRESSION.fullmatch(body)
        body = match.group(1).strip() if match else body
        form = re.fullmatch(r"(?:success\(\)\s*&&\s*)?(\S+)\s*==\s*'([^']*)'", body)
        if not form:
            raise SystemExit("the drill cannot evaluate the condition {!r}".format(text))
        return self.lookup(form.group(1)) == form.group(2)


def prepare(workspace, approved):
    """A placeholder manifest, the stubs, and the approvals the API would return for this run."""
    manifest = json.loads((ROOT / "infra/environments/environments.json").read_text())
    platform = manifest["platform"]
    platform["region"] = PLACEHOLDERS["region"]
    platform["organization_id"] = PLACEHOLDERS["organization_id"]
    platform["billing_account_id"] = PLACEHOLDERS["billing_account_id"]
    platform["base_domain"] = PLACEHOLDERS["base_domain"]
    platform["artifact_registry"]["project_id"] = PLACEHOLDERS["artifact_registry_project"]
    for environment in manifest["environments"]:
        environment["variables"]["APP_BASE_URL"]["value"] = "https://{}.{}".format(
            environment["name"], PLACEHOLDERS["base_domain"])
    manifest_path = workspace / "environments.json"
    manifest_path.write_text(json.dumps(manifest, indent=2))

    stubs = workspace / "stubs"
    stubs.mkdir()
    for name, body in STUBS.items():
        path = stubs / name
        path.write_text("#!/usr/bin/env bash\n{}\n".format(body))
        path.chmod(0o755)

    approvals = [{
        "state": "approved",
        "comment": APPROVERS[name][1],
        "user": {"login": APPROVERS[name][0], "type": "User"},
        "environments": [{"id": 400 + index, "name": name,
                          "created_at": "2026-01-01T00:00:00Z",
                          "updated_at": "2026-01-01T0{}:00:00Z".format(index)}],
    } for index, name in enumerate(sorted(approved)) if name in APPROVERS]
    approvals_path = workspace / "approvals.json"
    approvals_path.write_text(json.dumps(approvals))

    return manifest_path, stubs, approvals_path


def run_step(step, ctx, shared, job_name):
    uses = step.get("uses")
    if uses and not uses.startswith("./"):
        print("      · {:<44} [external action, not run here]".format(uses))
        return uses
    if uses:
        return uses

    name = step.get("name") or "run"
    env = dict(os.environ)
    env["PATH"] = "{}:{}".format(shared["stubs"], env["PATH"])
    env.update({key: str(ctx.expand(value)) for key, value in (step.get("env") or {}).items()})
    env.update(shared["env"])
    outputs = shared["workspace"] / "step-output"
    summary = shared["workspace"] / "step-summary"
    outputs.write_text("")
    summary.write_text("")
    env["GITHUB_OUTPUT"] = str(outputs)
    env["GITHUB_STEP_SUMMARY"] = str(summary)
    env["OUTPUT"] = str(shared["records"] / "deployment-record-{}.json".format(
        ctx.inputs.get("environment", job_name)))

    directory = ROOT / ctx.expand(step["working-directory"]) if step.get("working-directory") else ROOT
    result = subprocess.run(["bash", "-e", "-c", ctx.expand(step["run"])],
                            cwd=str(directory), env=env, capture_output=True, text=True)
    print("      · {:<44} {}".format(
        name[:44], "ok" if result.returncode == 0 else "FAILED (exit {})".format(result.returncode)))
    for line in [l for l in (result.stdout + result.stderr).splitlines() if l.strip()][-2:]:
        print("          {}".format(line[:140]))

    produced = {}
    for line in outputs.read_text().splitlines():
        if "=" in line:
            key, _, value = line.partition("=")
            produced[key] = value
    if step.get("id"):
        ctx.steps.setdefault(step["id"], {}).update(produced)
    if result.returncode != 0:
        raise Halt(job_name, name, result.returncode)
    return produced


def order_jobs(jobs):
    done, ordered = set(), []
    while len(ordered) < len(jobs):
        ready = [name for name, job in jobs.items()
                 if name not in done and all(need in done for need in check.as_list(job.get("needs")))]
        if not ready:
            raise SystemExit("the pipeline's needs do not form an order")
        for name in ready:
            ordered.append(name)
            done.add(name)
    return ordered


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--approve", default="", help="comma-separated environments to approve")
    arguments = parser.parse_args()
    approved = {name for name in arguments.approve.split(",") if name}

    pipeline = check.load_yaml(ROOT / ".github/workflows/ci-cd-pipeline.yml")
    action = check.load_yaml(ROOT / ".github/actions/deploy-environment/action.yml")
    gates = json.loads((ROOT / "infra/environments/github/environments.json").read_text())
    reviewers = {entry["name"]: entry["reviewer_teams"] for entry in gates["environments"]}

    workspace = Path(tempfile.mkdtemp(prefix="verify-promotion-"))
    try:
        manifest, stubs, approvals = prepare(workspace, approved)
        records = workspace / "records"
        records.mkdir()
        commit = subprocess.run(["git", "rev-parse", "HEAD"], cwd=str(ROOT),
                                capture_output=True, text=True).stdout.strip()
        shared = {"stubs": stubs, "workspace": workspace, "records": records,
                  "env": {"MANIFEST": str(manifest), "APPROVALS_FILE": str(approvals),
                          "GITHUB_SHA": commit, "GITHUB_RUN_ID": "0", "GITHUB_ACTOR": "drill",
                          "GITHUB_REPOSITORY": "baderalhindi/ImplementW",
                          "IMAGE_REPOSITORY": "{}-docker.pkg.dev/{}/pmplatform/api".format(
                              PLACEHOLDERS["region"], PLACEHOLDERS["artifact_registry_project"])}}

        ctx = Context(commit)
        jobs = pipeline["jobs"]
        order = order_jobs(jobs)
        print("Stage order, read from the pipeline's own `needs`:\n  {}\n".format(" -> ".join(order)))
        print("Placeholder hosting values stand in for the five outstanding items; approvals given: {}\n"
              .format(", ".join(sorted(approved)) or "none"))

        executed, skipped = [], []
        for name in order:
            job = jobs[name]
            environment = job.get("environment")
            env_name = environment.get("name") if isinstance(environment, dict) else environment

            if not ctx.condition(job.get("if")):
                print("  ✗ {:<18} skipped — its condition is false".format(name))
                skipped.append(name)
                continue
            if job.get("uses"):
                print("  · {:<18} calls {} [not re-run here]".format(name, job["uses"]))
                executed.append(name)
                continue

            gate = reviewers.get(env_name) if env_name else None
            if gate:
                state = "approved by {}".format(APPROVERS[env_name][0]) if env_name in approved else "NOT APPROVED"
                print("  ⏸ {:<18} gate on '{}' — reviewers {}: {}".format(
                    name, env_name, ", ".join(gate), state))
                if env_name not in approved:
                    print("       On GitHub the job would not start at all. The drill starts it, to show")
                    print("       what the second lock does when the first one is bypassed.")

            print("  ▶ {:<18}{}".format(name, " [environment: {}]".format(env_name) if env_name else ""))
            try:
                for step in check.as_list(job.get("steps")):
                    used = run_step(step, ctx, shared, name)
                    if used == "./.github/actions/deploy-environment":
                        inner = Context(commit)
                        inner.inputs = {k: ctx.expand(v) for k, v in (step.get("with") or {}).items()}
                        inner.needs, inner.vars, inner.secrets = ctx.needs, ctx.vars, ctx.secrets
                        print("        deploy-environment:")
                        for inner_step in check.as_list(action["runs"]["steps"]):
                            run_step(inner_step, inner, shared, name)
                executed.append(name)
            except Halt as halt:
                print("  ⛔ {:<18} {}".format(name, halt))
                print("\nStages executed: {}".format(" -> ".join(executed)))
                print("Not reached:     {}".format(
                    ", ".join(n for n in order if n not in executed and n not in skipped)))
                return 1
            ctx.needs[name] = {k: ctx.expand(v) for k, v in (job.get("outputs") or {}).items()}

        print("\nStages executed in order: {}".format(" -> ".join(executed)))
        if skipped:
            print("Skipped: {}".format(", ".join(skipped)))
        return report_records(records)
    finally:
        shutil.rmtree(workspace, ignore_errors=True)


def report_records(records):
    """One artifact, promoted unchanged, and an approval on every gated environment."""
    written = sorted(records.glob("deployment-record-*.json"))
    if not written:
        return 0
    print("\nDeployment records:")
    digests = set()
    for path in written:
        record = json.loads(path.read_text())
        digests.add(record["image_digest"])
        approval = record["approval"]
        evidence = ("{} at {}".format(approval["approver"], approval["approved_at"])
                    if approval["required"] else "no approval required")
        print("  {:<5} {} {:<12} {}".format(
            record["environment"], record["commit_sha"][:12], record["task_id"], evidence))
    if len(digests) == 1:
        print("\nOne artifact reached every environment unchanged: {}".format(digests.pop()))
        return 0
    print("\nThe environments did not receive the same artifact: {}".format(digests))
    return 1


if __name__ == "__main__":
    sys.exit(main())
