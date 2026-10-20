#!/usr/bin/env python3
"""Static checks on health checks and uptime monitoring (TASK-091, CTL-31).

The API, the Cloud Run probes, the uptime check and the deploy action each name a health path or a
timeout, in four languages. `terraform validate` and the test suites each see one of them; none sees
that they still agree. A probe pointed at a path the API does not serve validates perfectly well and
restarts every instance on its first day. These checks run in the `repo-checks` CI job, standard
library only. Record: docs/architecture/health-checks-uptime-monitoring.md.

    python3 docs/architecture/uptime-monitoring-check.py

  H-1  The API maps /health, /health/live and /health/ready.
  H-2  Cloud Run's start-up probe reads /health/ready and its liveness probe /health/live; the
       start-up probe waits longer than the API's database probe.
  H-3  The uptime check reads /health/ready over TLS on 443 and waits longer than the database probe.
  H-4  An environment has uptime monitoring exactly when the manifest gives it ALERT_CHANNEL_WEBHOOK_URL.
  H-5  The configuration flags an outage within 180 s (period + timeout + duration), leaving at least
       two minutes of the five-minute alert threshold for Cloud Monitoring's own delay (D-7).
  H-6  The deploy action passes the on-call channel's name from route-alerts.sh --name to Terraform.
  H-7  No notification channel is declared or looked up in infra/terraform: either would copy
       ALERT_CHANNEL_WEBHOOK_URL into the state file.
"""
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ENDPOINTS = ROOT / "src/backend/PMPlatform.Api/HealthChecks/HealthEndpoints.cs"
DATABASE_PROBE = ROOT / "src/backend/PMPlatform.Infrastructure/Persistence/PostgreSqlHealthCheck.cs"
COMPUTE = ROOT / "infra/terraform/compute/main.tf"
MONITORING = ROOT / "infra/terraform/monitoring"
TERRAFORM = ROOT / "infra/terraform"
ENVIRONMENTS = ROOT / "infra/terraform/environments"
MANIFEST = ROOT / "infra/environments/environments.json"
DEPLOY_ACTION = ROOT / ".github/actions/deploy-environment/action.yml"

ALERT_THRESHOLD_SECONDS = 300
CLOUD_MONITORING_ALLOWANCE_SECONDS = 120

findings = []


def fail(check, message):
    findings.append(f"{check}: {message}")


def read(path):
    try:
        return path.read_text(encoding="utf-8")
    except OSError as exc:
        fail("H-0", f"cannot read {path.relative_to(ROOT)}: {exc}")
        return ""


def block(text, name):
    """The body of the first HCL block called `name` (braces balanced), or ''."""
    match = re.search(rf"\b{name}\s*\{{", text)
    if not match:
        return ""
    depth, start = 0, match.end() - 1
    for index in range(start, len(text)):
        depth += {"{": 1, "}": -1}.get(text[index], 0)
        if depth == 0:
            return text[start + 1 : index]
    return ""


def attribute(text, name):
    match = re.search(rf'^\s*{name}\s*=\s*"?([^"\n]+?)"?\s*$', text, re.M)
    return match.group(1) if match else None


def variable_default(variables, name):
    body = block(variables, f'variable "{name}"')
    return attribute(body, "default")


def main():
    endpoints = read(ENDPOINTS)
    paths = dict(re.findall(r'public const string (\w+) = "([^"]+)";', endpoints))
    for name, expected in (("Health", "/health"), ("Live", "/health/live"), ("Ready", "/health/ready")):
        if paths.get(name) != expected:
            fail("H-1", f"{ENDPOINTS.relative_to(ROOT)} does not declare {name} = \"{expected}\"")
    for name in ("Health", "Live", "Ready"):
        if f"MapHealthChecks({name}" not in endpoints:
            fail("H-1", f"{ENDPOINTS.relative_to(ROOT)} does not map {name}")

    probe = re.search(r"public const int TimeoutSeconds = (\d+);", read(DATABASE_PROBE))
    probe_seconds = int(probe.group(1)) if probe else None
    if probe_seconds is None:
        fail("H-2", f"{DATABASE_PROBE.relative_to(ROOT)} declares no TimeoutSeconds")

    compute = read(COMPUTE)
    startup, liveness = block(compute, "startup_probe"), block(compute, "liveness_probe")
    if attribute(startup, "path") != paths.get("Ready"):
        fail("H-2", f"the start-up probe reads {attribute(startup, 'path')}, not {paths.get('Ready')}")
    if attribute(liveness, "path") != paths.get("Live"):
        fail("H-2", f"the liveness probe reads {attribute(liveness, 'path')}, not {paths.get('Live')}: a liveness "
                    "probe that reads the database restarts every instance during a database outage")
    startup_timeout = attribute(startup, "timeout_seconds")
    if probe_seconds is not None and not (startup_timeout and int(startup_timeout) > probe_seconds):
        fail("H-2", f"the start-up probe's timeout_seconds ({startup_timeout or 'the default 1'}) does not exceed the "
                    f"API's {probe_seconds}-second database probe, so a slow database fails start-up as a timeout")

    monitoring_main, variables = read(MONITORING / "main.tf"), read(MONITORING / "variables.tf")
    http_check = block(monitoring_main, "http_check")
    if variable_default(variables, "path") != paths.get("Ready") or "var.path" not in http_check:
        fail("H-3", f"the uptime check does not read {paths.get('Ready')}")
    if attribute(http_check, "port") != "443" or attribute(http_check, "use_ssl") != "true":
        fail("H-3", "the uptime check does not use TLS on 443, the only listener the load balancer has")
    check_timeout = variable_default(variables, "check_timeout_seconds")
    if probe_seconds is not None and not (check_timeout and int(check_timeout) > probe_seconds):
        fail("H-3", f"the uptime check's timeout ({check_timeout}) does not exceed the API's {probe_seconds}-second database probe")

    manifest = json.loads(read(MANIFEST) or "{}")
    for environment in manifest.get("environments", []):
        name = environment["name"]
        expected = "ALERT_CHANNEL_WEBHOOK_URL" in environment.get("variables", {})
        tfvars = read(ENVIRONMENTS / name / "terraform.tfvars")
        value = attribute(tfvars, "uptime_monitoring")
        if value != str(expected).lower():
            fail("H-4", f"{name}: uptime_monitoring is {value}, but the manifest {'gives' if expected else 'does not give'} "
                        "it ALERT_CHANNEL_WEBHOOK_URL")

    try:
        detection = sum(int(variable_default(variables, name) or "x")
                        for name in ("check_period_seconds", "check_timeout_seconds", "failure_duration_seconds"))
    except ValueError:
        fail("H-5", "check_period_seconds, check_timeout_seconds or failure_duration_seconds has no numeric default")
    else:
        if detection > ALERT_THRESHOLD_SECONDS - CLOUD_MONITORING_ALLOWANCE_SECONDS:
            fail("H-5", f"the configuration takes up to {detection} s to flag an outage, leaving less than "
                        f"{CLOUD_MONITORING_ALLOWANCE_SECONDS} s of the {ALERT_THRESHOLD_SECONDS}-second threshold")

    action = read(DEPLOY_ACTION)
    if "route-alerts.sh" not in action or "--name" not in action:
        fail("H-6", f"{DEPLOY_ACTION.relative_to(ROOT)} does not read the on-call channel with route-alerts.sh --name")
    if not re.search(r"TF_VAR_alert_notification_channel:\s*\$\{\{\s*steps\.alert-channel\.outputs\.name\s*\}\}", action):
        fail("H-6", f"{DEPLOY_ACTION.relative_to(ROOT)} does not pass TF_VAR_alert_notification_channel to terraform apply")

    for path in sorted(TERRAFORM.rglob("*.tf")):
        if ".terraform" in path.parts:
            continue
        if re.search(r'(resource|data)\s+"google_monitoring_notification_channel"', read(path)):
            fail("H-7", f"{path.relative_to(ROOT)} declares a notification channel; its URL would be written to the state "
                        "(route-alerts.sh keeps the channel; Terraform takes only its name)")

    if findings:
        print("\n".join(findings))
        print(f"\n{len(findings)} finding(s)")
        return 1
    print(f"OK: /health/live and /health/ready served, probed by Cloud Run and checked from outside; uptime monitoring in "
          f"{sum(1 for e in manifest['environments'] if 'ALERT_CHANNEL_WEBHOOK_URL' in e['variables'])} environments, "
          f"an outage flagged within {detection} s of configuration, the on-call channel outside the state")
    return 0


if __name__ == "__main__":
    sys.exit(main())
