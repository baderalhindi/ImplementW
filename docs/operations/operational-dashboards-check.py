#!/usr/bin/env python3
"""Static checks on the operational dashboards and alert thresholds (TASK-092, CTL-32).

The thresholds are named in a register, read by the API under one prefix, set per environment in
Terraform, and announced by a log line a Cloud Monitoring metric parses; the dashboards are a JSON
definition and an application screen. Each lives in a different language, and no test sees them all:
a threshold renamed in C# still validates in Terraform and silently stops applying, and a log line
reworded in C# still compiles and silently stops paging anyone. These checks run in the
`repo-checks` CI job, standard library only. Record: docs/operations/operational-dashboards.md.

    python3 docs/operations/operational-dashboards-check.py

  O-1  The register names exactly the alert types FG-05 raises (OperationalAlertType), each once
       or, for a type with two bounds, once per bound.
  O-2  Every threshold's setting is a property of IntegrationMonitoringPolicy (and its bound one of
       QueueBacklogThreshold's); Terraform's key pattern admits exactly the register's settings.
  O-3  Every environment's integration_alert_thresholds key is a registered threshold for one
       integration code; PROD sets a value only for a CONFIRMED threshold; an OPEN threshold cites
       its open question.
  O-4  The configured route fits the routing window: FG-05's pass, the outbox and two WF-15 passes
       by their default poll intervals, and the on-call policy's alignment period.
  O-5  The line OperationalAlerting logs is the one the log-based metric matches, and its extractors
       read the type, severity and integration back for every alert type.
  O-6  Every alert raised publishes the intent the register's route names, under its family.
  O-7  The platform dashboard's panels are the ones the record defines (PD-n), and the queue state
       the record's OD-1 shows is in the API's view and on ADM-047.
"""
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
REGISTER = ROOT / "docs/operations/alert-thresholds.json"
RECORD = ROOT / "docs/operations/operational-dashboards.md"
DASHBOARD = ROOT / "docs/operations/dashboards/platform-service.json.tftpl"
BACKEND = ROOT / "src/backend"
ALERT_TYPE = BACKEND / "PMPlatform.Domain/IntegrationMonitoring/OperationalAlertType.cs"
POLICY = BACKEND / "PMPlatform.Application/Features/IntegrationMonitoring/IntegrationMonitoringPolicy.cs"
ALERTING = BACKEND / "PMPlatform.Application/Features/IntegrationMonitoring/OperationalAlerting.cs"
INTENT = BACKEND / "PMPlatform.Application/Features/IntegrationMonitoring/Contracts/Events/OperationalAlertRaised.cs"
VIEWS = BACKEND / "PMPlatform.Application/Features/IntegrationMonitoring/Contracts/IntegrationViews.cs"
QUEUE_TABLE = ROOT / "src/frontend/src/features/integration-admin/components/QueueTable.tsx"
OPERATIONS_TF = ROOT / "infra/terraform/monitoring/operations.tf"
PLATFORM_VARIABLES = ROOT / "infra/terraform/platform/variables.tf"
ENVIRONMENTS = ROOT / "infra/terraform/environments"

QUEUE_FIELDS = ["depth", "inFlight", "awaitingRetry", "oldestQueuedAt", "deadLetters"]

findings = []


def fail(check, message):
    findings.append(f"{check}: {message}")


def read(path):
    try:
        return path.read_text(encoding="utf-8")
    except OSError as exc:
        fail("O-0", f"cannot read {path.relative_to(ROOT)}: {exc}")
        return ""


def snake_upper(name):
    return re.sub(r"(?<!^)(?=[A-Z])", "_", name).upper()


def alert_types():
    """OperationalAlertType's members, as the API writes them (SNAKE_CASE_UPPER)."""
    return [snake_upper(m) for m in re.findall(r"^\s*([A-Z]\w*)\s*=\s*\d+\s*,?\s*$", read(ALERT_TYPE), re.M)]


def check_alert_types(register):
    """O-1"""
    rows = register["thresholds"]
    built = alert_types()
    if not built:
        fail("O-1", f"no member found in {ALERT_TYPE.relative_to(ROOT)}")
    registered = {row["alertType"] for row in rows}
    for missing in sorted(set(built) - registered):
        fail("O-1", f"FG-05 raises {missing}, which alert-thresholds.json does not register")
    for extra in sorted(registered - set(built)):
        fail("O-1", f"alert-thresholds.json registers {extra}, which OperationalAlertType does not have")
    seen = set()
    for row in rows:
        identity = (row["alertType"], row.get("setting"), row.get("property"))
        if identity in seen:
            fail("O-1", f"{row['id']} registers {identity} twice")
        seen.add(identity)


def check_settings(register):
    """O-2"""
    policy = read(POLICY)
    properties = set(re.findall(r"public\s+[\w<>?, ]+\s+(\w+)\s*\{\s*get;", policy))
    bounds = set(re.findall(r"public\s+[\w?]+\s+(\w+)\s*\{\s*get;\s*set;\s*\}", policy.split("class QueueBacklogThreshold", 1)[-1]))
    settings = set()
    for row in register["thresholds"]:
        setting = row.get("setting")
        if setting is None:
            continue
        settings.add(setting)
        if setting not in properties:
            fail("O-2", f"{row['id']}'s setting {setting} is not a property of IntegrationMonitoringPolicy; the API would never read it")
        if row.get("property") and row["property"] not in bounds:
            fail("O-2", f"{row['id']}'s bound {row['property']} is not a property of QueueBacklogThreshold")
        expected = f"{setting}__<CODE>" + (f"__{row['property']}" if row.get("property") else "")
        if row.get("key") != expected:
            fail("O-2", f"{row['id']}'s key is {row.get('key')!r}; its setting gives {expected!r}")
    pattern = re.search(r'regex\(\s*"([^"]+)"', read(PLATFORM_VARIABLES))
    if pattern is None:
        fail("O-2", "platform/variables.tf has no key pattern on integration_alert_thresholds")
        return
    regex = re.compile(pattern.group(1).replace("\\\\", "\\"))
    for row in register["thresholds"]:
        if row.get("key") and not regex.match(row["key"].replace("<CODE>", "EXCHANGE_EMAIL")):
            fail("O-2", f"Terraform refuses {row['id']}'s key {row['key']}")
    for unregistered in ["Unknown__DIRECTORY", "QueueBacklogThresholds__SMS", "FreshnessWindows__directory"]:
        if regex.match(unregistered):
            fail("O-2", f"Terraform admits {unregistered}, which is no registered threshold")


def tfvars_thresholds(path):
    text = read(path)
    block = re.search(r"^integration_alert_thresholds\s*=\s*\{(.*?)^\}", text, re.M | re.S)
    inline = re.search(r"^integration_alert_thresholds\s*=\s*\{\s*\}", text, re.M)
    if block is None and inline is None:
        return None
    body = block.group(1) if block else ""
    return dict(re.findall(r'"([^"]+)"\s*=\s*"([^"]*)"', body))


def check_environments(register):
    """O-3"""
    rows = register["thresholds"]
    for row in rows:
        if row["status"] == "OPEN" and not any(r.startswith(("PTBC-", "TBC-")) for r in row.get("references", [])):
            fail("O-3", f"{row['id']} is OPEN without the open question it waits on")
        if row["status"] == "CONFIRMED" and not row.get("confirmation"):
            fail("O-3", f"{row['id']} is CONFIRMED without a confirmation reference")
    keyed = [row for row in rows if row.get("key")]
    for root in sorted(p for p in ENVIRONMENTS.iterdir() if p.is_dir()):
        values = tfvars_thresholds(root / "terraform.tfvars")
        if values is None:
            fail("O-3", f"{root.name}/terraform.tfvars does not set integration_alert_thresholds")
            continue
        if "integration_alert_thresholds" not in read(root / "main.tf"):
            fail("O-3", f"{root.name}/main.tf does not pass integration_alert_thresholds to the platform")
        for key in values:
            row = next((r for r in keyed if re.fullmatch(re.escape(r["key"]).replace("<CODE>", "[A-Z][A-Z0-9_]*"), key)), None)
            if row is None:
                fail("O-3", f"{root.name} sets {key}, which no registered threshold names")
            elif root.name == "prod" and row["status"] != "CONFIRMED":
                fail("O-3", f"prod sets {key}, but {row['id']} is {row['status']}: AHDA has not confirmed it (PTBC-037)")


def default_seconds(path, setting_name="PollInterval"):
    match = re.search(setting_name + r"\s*\{\s*get;\s*set;\s*\}\s*=\s*TimeSpan\.From(Seconds|Minutes)\((\d+)\)", read(ROOT / path))
    if match is None:
        return None
    return int(match.group(2)) * (60 if match.group(1) == "Minutes" else 1)


def check_routing_window(register):
    """O-4"""
    routing = register["routing"]
    window = routing["windowSeconds"]
    total = 0
    for stage in routing["stages"]:
        seconds = default_seconds(stage["source"])
        if seconds is None:
            fail("O-4", f"cannot read the default PollInterval of {stage['source']} ({stage['setting']})")
            continue
        total += seconds
    if total > window:
        fail("O-4", f"the configured route takes up to {total} s, beyond the {window} s routing window")
    alignment = re.search(r'resource "google_monitoring_alert_policy" "operational_alert".*?alignment_period\s*=\s*"(\d+)s"', read(OPERATIONS_TF), re.S)
    if alignment is None:
        fail("O-4", "operations.tf has no operational_alert policy with an alignment period")
    elif int(alignment.group(1)) + default_seconds(routing["stages"][0]["source"] if routing["stages"] else "") > window:
        fail("O-4", f"the on-call route takes up to {alignment.group(1)} s plus FG-05's pass, beyond the {window} s window")
    return total


def check_log_line():
    """O-5"""
    alerting = read(ALERTING)
    template = re.search(r'Message\s*=\s*"(Operational alert raised:[^"]*)"', alerting)
    if template is None:
        fail("O-5", "OperationalAlerting logs no 'Operational alert raised:' line")
        return
    tf = read(OPERATIONS_TF)
    metric_filter = re.search(r'textPayload:\\"([^\\]+)\\"', tf)
    if metric_filter is None or not template.group(1).startswith(metric_filter.group(1)):
        fail("O-5", f"the log-based metric does not match the line OperationalAlerting logs ({template.group(1)!r})")
    extractors = dict(re.findall(r'(\w+)\s*=\s*"REGEXP_EXTRACT\(textPayload, \\"(.+?)\\"\)"', tf))
    for label in ["alert_type", "severity", "integration"]:
        if label not in extractors:
            fail("O-5", f"the log-based metric has no extractor for {label}")
    for alert_type in alert_types():
        line = (template.group(1).replace("{AlertType}", alert_type).replace("{Severity}", "WARNING")
                .replace("{Integration}", "EXCHANGE_EMAIL").replace("{AlertId}", "0192f3a4-5b6c-7d8e-9f0a-2c3d4e5f6071"))
        expected = {"alert_type": alert_type, "severity": "WARNING", "integration": "EXCHANGE_EMAIL"}
        for label, pattern in extractors.items():
            found = re.search(pattern, "      " + line)
            if found is None or found.group(1) != expected.get(label):
                fail("O-5", f"the {label} extractor reads {found.group(1) if found else None!r} from {line!r}, not {expected.get(label)!r}")


def check_intent(register):
    """O-6"""
    intent = read(INTENT)
    family = re.search(r'EventFamilyCode\s*=\s*"([^"]+)"', intent)
    event_type = re.search(r'Type\s*=\s*"([^"]+)"', intent)
    if family is None or family.group(1) != register["routing"]["family"]:
        fail("O-6", f"OperationalAlertRaised's family is {family.group(1) if family else None!r}; the register routes {register['routing']['family']!r}")
    if event_type is None or event_type.group(1) != register["routing"]["eventType"]:
        fail("O-6", f"OperationalAlertRaised's type is {event_type.group(1) if event_type else None!r}; the register names {register['routing']['eventType']!r}")
    raise_body = read(ALERTING).split("public async Task<OperationalAlert?> RaiseAsync", 1)[-1].split("private static", 1)[0]
    if "outbox.Stage(Intent(" not in raise_body:
        fail("O-6", "OperationalAlerting.RaiseAsync does not stage the alert's NotificationIntent: alerts would reach no recipient")


def check_dashboards():
    """O-7"""
    template = read(DASHBOARD)
    record = read(RECORD)
    panels = re.findall(r'"title":\s*"(PD-\d+) ', template)
    documented = re.findall(r"^\|\s*(PD-\d+)\s*\|", record, re.M)
    if not panels:
        fail("O-7", "the platform dashboard has no PD-n panel")
    for panel in sorted(set(panels) - set(documented)):
        fail("O-7", f"{panel} is on the dashboard but not defined in operational-dashboards.md")
    for panel in sorted(set(documented) - set(panels)):
        fail("O-7", f"operational-dashboards.md defines {panel}, which the dashboard does not show")
    if len(panels) != len(set(panels)):
        fail("O-7", "a PD-n panel is on the dashboard twice")
    views = read(VIEWS)
    queue_table = read(QUEUE_TABLE)
    view = re.search(r"record IntegrationQueueState\(([^)]*)\)", views)
    names = {p.split()[-1][0].lower() + p.split()[-1][1:] for p in view.group(1).split(",")} if view else set()
    if "public int Depth =>" in views:
        names.add("depth")
    for field in QUEUE_FIELDS:
        if field not in names:
            fail("O-7", f"the API's IntegrationQueueState has no {field}")
        if f'data-field="{field}"' not in queue_table:
            fail("O-7", f"ADM-047's queue table shows no {field}")


def main():
    try:
        register = json.loads(read(REGISTER) or "{}")
    except json.JSONDecodeError as exc:
        print(f"cannot parse {REGISTER.relative_to(ROOT)}: {exc}", file=sys.stderr)
        return 1
    if "thresholds" not in register or "routing" not in register:
        print(f"{REGISTER.relative_to(ROOT)} has no thresholds or routing", file=sys.stderr)
        return 1
    check_alert_types(register)
    check_settings(register)
    check_environments(register)
    route = check_routing_window(register)
    check_log_line()
    check_intent(register)
    check_dashboards()
    if findings:
        print("\n".join(findings), file=sys.stderr)
        print(f"{len(findings)} finding(s)", file=sys.stderr)
        return 1
    open_count = sum(1 for r in register["thresholds"] if r["status"] == "OPEN")
    print(f"OK: {len(register['thresholds'])} thresholds over {len(set(r['alertType'] for r in register['thresholds']))} alert types, "
          f"{open_count} open (PTBC-037), none set in prod unconfirmed; each alert routed to WF-15 and the on-call channel, "
          f"configured route {route} s within the {register['routing']['windowSeconds']} s window; dashboard and ADM-047 match the record.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
