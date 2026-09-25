#!/usr/bin/env python3
"""Static checks on the network security configuration (TASK-021).

`terraform validate` proves the modules are well formed. It does not look at what TASK-021's
acceptance criteria turn on: that 443 is the only port the public address listens on, that TLS
terminates at a regional load balancer in the named region at 1.2 or above, that the WAF is
attached and enforcing, and that only the API tier can reach the database port.

Every one of those can be undone by a change that plans perfectly well — a second forwarding
rule, a global load balancer, a relaxed TLS profile, the WAF left in preview, an ingress allow, a
dropped deny. They are caught here, in the `repo-checks` CI job, with no Terraform binary and no
cloud credentials.

    python3 docs/architecture/network-security-check.py
"""
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
TERRAFORM = ROOT / "infra/terraform"
NETWORK = TERRAFORM / "network"
LOAD_BALANCER = TERRAFORM / "load-balancer"
COMPUTE = TERRAFORM / "compute"
DATABASE = TERRAFORM / "database"
PLATFORM = TERRAFORM / "platform"
ROOTS = TERRAFORM / "environments"
ORG_POLICY = ROOT / "infra/environments/apply-org-policy.sh"
VERIFY = NETWORK / "verify-network-security.sh"
RECORD = ROOT / "docs/architecture/network-security.md"

# A global load balancer terminates TLS at the Google edge nearest the client, which need not be
# in the Kingdom (ADR-001 C-2). These are the global forms of what load-balancer/ creates.
GLOBAL_RESOURCES = (
    "google_compute_global_forwarding_rule", "google_compute_global_address",
    "google_compute_target_https_proxy", "google_compute_target_http_proxy",
    "google_compute_url_map", "google_compute_backend_service",
    "google_compute_security_policy", "google_compute_ssl_policy",
    "google_compute_managed_ssl_certificate",
)

# Constraints apply-org-policy.sh binds so that a public path cannot be created at all.
FOLDER_CONSTRAINTS = (
    "compute.skipDefaultNetworkCreation", "compute.vmExternalIpAccess", "sql.restrictPublicIp",
)

CHECK_ID = re.compile(r"^#\s+(N-\d)\s+\S", re.M)

findings = []


def fail(check, message):
    findings.append("{}: {}".format(check, message))


def read(path):
    return path.read_text() if path.is_file() else ""


def rel(path):
    return str(path.relative_to(ROOT))


def blocks(text, kind, type_):
    """Every `<kind> "<type_>" "<name>" { ... }` block in text, as {name: body}.

    Terraform blocks at the top level of a file start in column 0 and end at a closing brace in
    column 0, which is how `terraform fmt` lays them out and every file here is formatted.
    """
    pattern = r'^{} "{}" "([^"]+)" \{{\n(.*?)^\}}'.format(kind, re.escape(type_))
    return {m.group(1): m.group(2) for m in re.finditer(pattern, text, re.M | re.S)}


def variable_default(text, name):
    body = re.search(r'^variable "{}" \{{\n(.*?)^\}}'.format(re.escape(name)), text, re.M | re.S)
    if body is None:
        return None
    default = re.search(r"^\s*default\s*=\s*(.+)$", body.group(1), re.M)
    return default.group(1).strip() if default else None


def check_deliverables():
    """NS-1 — the modules, the verification script and the record with its diagram exist."""
    for directory in (NETWORK, LOAD_BALANCER):
        for name in ("main.tf", "variables.tf", "outputs.tf", "versions.tf"):
            if not (directory / name).is_file():
                fail("NS-1", "{}/{} is missing".format(rel(directory), name))
    if not VERIFY.is_file():
        fail("NS-1", "{} is missing; the validation cell is a scan and a probe against a running "
                     "environment, and nothing else performs them".format(rel(VERIFY)))
    elif VERIFY.stat().st_mode & 0o111 == 0:
        fail("NS-1", "{} is not executable".format(rel(VERIFY)))
    if "```mermaid" not in read(RECORD):
        fail("NS-1", "{} has no network diagram. The workbook names one as a deliverable.".format(
            rel(RECORD)))


def check_only_443():
    """NS-2 — the public address has one listener, and it is 443."""
    text = read(LOAD_BALANCER / "main.tf")
    rules = blocks(text, "resource", "google_compute_forwarding_rule")
    if len(rules) != 1:
        fail("NS-2", "infra/terraform/load-balancer/main.tf declares {} forwarding rule(s): {}. The "
                     "validation cell requires an external scan on which only 443 is reachable, so "
                     "the address has exactly one listener.".format(len(rules), ", ".join(rules) or "none"))
    for name, body in rules.items():
        if not re.search(r'port_range\s*=\s*"443"', body):
            fail("NS-2", "forwarding rule {} does not listen on 443 alone".format(name))
    for proxy in ("google_compute_region_target_http_proxy", "google_compute_target_http_proxy"):
        if 'resource "{}"'.format(proxy) in text:
            fail("NS-2", "infra/terraform/load-balancer/main.tf declares a plaintext HTTP proxy. A "
                         "port that only redirects is still an open port on the scan.")


def check_regional_edge():
    """NS-3 — every ingress resource is regional, so TLS terminates in the named region."""
    text = "".join(read(p) for p in sorted(LOAD_BALANCER.glob("*.tf")))
    for type_ in GLOBAL_RESOURCES:
        if 'resource "{}"'.format(type_) in text:
            fail("NS-3", "infra/terraform/load-balancer declares {}, a global resource. A global load "
                         "balancer terminates TLS at the Google edge nearest the client, which need "
                         "not be in the Kingdom (ADR-001 C-2, CTL-04).".format(type_))
    for name, body in blocks(text, "resource", "google_certificate_manager_certificate").items():
        if not re.search(r"location\s*=\s*var\.region", body):
            fail("NS-3", "certificate {} is not in var.region; a global certificate cannot be "
                         "attached to a regional proxy".format(name))


def check_tls():
    """NS-4 — TLS 1.2 or above, AEAD suites only."""
    text = read(LOAD_BALANCER / "main.tf")
    policies = blocks(text, "resource", "google_compute_region_ssl_policy")
    if not policies:
        fail("NS-4", "infra/terraform/load-balancer/main.tf has no SSL policy; the proxy would "
                     "negotiate the provider's defaults, which include TLS 1.0 (CTL-04).")
    for name, body in policies.items():
        if not re.search(r'profile\s*=\s*"RESTRICTED"', body):
            fail("NS-4", "SSL policy {} is not RESTRICTED. MODERN and COMPATIBLE still offer AES-CBC "
                         "suites with SHA-1 MACs at TLS 1.2.".format(name))
        if not re.search(r"min_tls_version\s*=\s*var\.min_tls_version", body):
            fail("NS-4", "SSL policy {} does not take its minimum from var.min_tls_version, so the "
                         "validation on that variable does not protect it.".format(name))
    for name, body in blocks(text, "resource", "google_compute_region_target_https_proxy").items():
        if "ssl_policy" not in body:
            fail("NS-4", "HTTPS proxy {} has no ssl_policy".format(name))

    variables = read(LOAD_BALANCER / "variables.tf")
    if variable_default(variables, "min_tls_version") not in ('"TLS_1_2"', '"TLS_1_3"'):
        fail("NS-4", "min_tls_version does not default to TLS_1_2 or TLS_1_3 (CTL-04)")
    body = re.search(r'^variable "min_tls_version" \{\n(.*?)^\}', variables, re.M | re.S)
    if body is None or not re.search(r'contains\(\["TLS_1_2", "TLS_1_3"\], var\.min_tls_version\)', body.group(1)):
        fail("NS-4", "min_tls_version has no validation confining it to TLS_1_2 or TLS_1_3, so a "
                     "root could set TLS_1_0 and plan cleanly.")


def check_waf():
    """NS-5 — the WAF is attached to the backend and enforces in every environment."""
    text = read(LOAD_BALANCER / "main.tf")
    for name, body in blocks(text, "resource", "google_compute_region_backend_service").items():
        if not re.search(r"security_policy\s*=\s*google_compute_region_security_policy\.", body):
            fail("NS-5", "backend service {} has no Cloud Armor policy attached".format(name))
    waf_rules = blocks(text, "resource", "google_compute_region_security_policy_rule")
    if not any(re.search(r"preview\s*=\s*var\.waf_preview", b) for b in waf_rules.values()):
        fail("NS-5", "no WAF rule takes its preview flag from var.waf_preview")

    for path, name in ((LOAD_BALANCER / "variables.tf", "waf_preview"),
                       (PLATFORM / "variables.tf", "waf_preview")):
        if variable_default(read(path), name) != "false":
            fail("NS-5", "{}: waf_preview does not default to false. A WAF in preview logs and "
                         "blocks nothing (G-7 baseline: enforcing).".format(rel(path)))
    for tfvars in sorted(ROOTS.glob("*/terraform.tfvars")):
        if re.search(r"^\s*waf_preview\s*=\s*true", tfvars.read_text(), re.M):
            fail("NS-5", "{} puts the WAF in preview. The G-7 baseline enforces in every environment, "
                         "so a false positive surfaces in testing rather than in PROD "
                         "(network-security.md §3.4).".format(rel(tfvars)))

    rule_sets = re.search(r'^variable "waf_rule_sets" \{\n(.*?)^\}', read(LOAD_BALANCER / "variables.tf"),
                          re.M | re.S)
    if rule_sets is None or "sqli-" not in rule_sets.group(1) or "xss-" not in rule_sets.group(1):
        fail("NS-5", "waf_rule_sets does not default to a baseline including SQL injection and XSS")
    elif re.search(r"^\s*methodenforcement-", rule_sets.group(1), re.M):
        fail("NS-5", "waf_rule_sets enables methodenforcement. At sensitivity 1 it admits only GET, "
                     "HEAD, POST and OPTIONS, and the API uses PUT, PATCH and DELETE.")


def check_segmentation():
    """NS-6 — no inbound allow; the database port open to the API tier's tag and nobody else."""
    rules = blocks(read(NETWORK / "main.tf"), "resource", "google_compute_firewall")
    for name, body in rules.items():
        if re.search(r'direction\s*=\s*"INGRESS"', body) and re.search(r"^\s*allow\s*\{", body, re.M):
            fail("NS-6", "firewall rule {} allows ingress. Nothing in this architecture receives "
                         "traffic on the VPC path (CTL-03).".format(name))

    def to_database(body):
        return re.search(r"destination_ranges\s*=\s*\[var\.private_services_cidr\]", body)

    allows = {n: b for n, b in rules.items() if to_database(b) and re.search(r"^\s*allow\s*\{", b, re.M)}
    denies = {n: b for n, b in rules.items() if to_database(b) and re.search(r"^\s*deny\s*\{", b, re.M)}

    if len(allows) != 1:
        fail("NS-6", "expected exactly one rule allowing the database range, found {}".format(len(allows)))
    for name, body in allows.items():
        if not re.search(r"target_tags\s*=\s*\[var\.app_network_tag\]", body):
            fail("NS-6", "{} is not scoped to the application tier's tag, so every instance in the VPC "
                         "may open the database port".format(name))
        if not re.search(r'protocol\s*=\s*"tcp"\s*\n\s*ports\s*=\s*\[tostring\(var\.database_port\)\]', body):
            fail("NS-6", "{} opens more than tcp:database_port".format(name))

    open_to_all = {n: b for n, b in denies.items()
                   if "target_tags" not in b and "target_service_accounts" not in b}
    if not open_to_all:
        fail("NS-6", "no rule denies the database range to every source. Cloud SQL sits in Google's "
                     "producer network, where no ingress rule can be written, so without one any other "
                     "workload in the VPC reaches the database port.")

    def priority(body):
        found = re.search(r"priority\s*=\s*(\d+)", body)
        return int(found.group(1)) if found else 1000

    for allow_name, allow_body in allows.items():
        for deny_name, deny_body in open_to_all.items():
            if priority(deny_body) <= priority(allow_body):
                fail("NS-6", "{} (priority {}) is evaluated before {} ({}), so the API tier cannot reach "
                             "the database".format(deny_name, priority(deny_body), allow_name,
                                                   priority(allow_body)))
            if "log_config" not in deny_body:
                fail("NS-6", "{} is not logged, so a blocked attempt leaves no evidence".format(deny_name))


def check_database_range():
    """NS-7 — the database takes its address from the range the firewall rules name."""
    if not re.search(r"allocated_ip_range\s*=\s*var\.allocated_ip_range", read(DATABASE / "main.tf")):
        fail("NS-7", "infra/terraform/database/main.tf does not pin allocated_ip_range, so the "
                     "instance may take an address outside the range the firewall rules address")
    if not re.search(r"allocated_ip_range\s*=\s*module\.network\.private_services_range_name",
                     read(PLATFORM / "main.tf")):
        fail("NS-7", "infra/terraform/platform/main.tf does not pass the network module's private "
                     "services range to the database")


def check_api_tier():
    """NS-8 — the API is reachable only through the load balancer and carries the firewall's tag."""
    compute = read(COMPUTE / "main.tf")
    if not re.search(r'ingress\s*=\s*"INGRESS_TRAFFIC_INTERNAL_LOAD_BALANCER"', compute):
        fail("NS-8", "the Cloud Run service accepts traffic that did not come through the load "
                     "balancer, so its run.app URL bypasses the TLS policy and the WAF")
    if not re.search(r"tags\s*=\s*\[var\.network_tag\]", compute):
        fail("NS-8", "the Cloud Run VPC interface carries no network tag, so no firewall rule "
                     "identifies it as the API tier")
    platform = read(PLATFORM / "main.tf")
    # Anchored: `network_tag = local.app_network_tag` is also a substring of the app_network_tag line.
    if not (re.search(r"^\s*app_network_tag\s*=\s*local\.app_network_tag\s*$", platform, re.M)
            and re.search(r"^\s*network_tag\s*=\s*local\.app_network_tag\s*$", platform, re.M)):
        fail("NS-8", "platform/main.tf does not give the network and compute modules the same tag "
                     "(local.app_network_tag); the allow rule and the API tier could disagree")


def check_org_policy():
    """NS-9 — the folders make a default network, a public VM address and a public database impossible."""
    text = read(ORG_POLICY)
    for constraint in FOLDER_CONSTRAINTS:
        if not re.search(r"set_folder_policy\s+\"\$folder_id\"\s+{}\b".format(re.escape(constraint)), text):
            fail("NS-9", "{} does not bind {} to the folders".format(rel(ORG_POLICY), constraint))


def check_verification_script():
    """NS-10 — every check the verification script's header claims is implemented in its body."""
    text = read(VERIFY)
    if not text:
        return
    header, _, body = text.partition("set -eu")
    claimed = CHECK_ID.findall(header)
    if not claimed:
        fail("NS-10", "{} claims no checks in its header".format(rel(VERIFY)))
    for check in claimed:
        if check not in body:
            fail("NS-10", "{} documents {} and does not implement it. An unexecuted check is not a "
                          "passed check.".format(rel(VERIFY), check))


def main():
    if not NETWORK.is_dir():
        print("infra/terraform/network does not exist", file=sys.stderr)
        return 1

    check_deliverables()
    check_only_443()
    check_regional_edge()
    check_tls()
    check_waf()
    check_segmentation()
    check_database_range()
    check_api_tier()
    check_org_policy()
    check_verification_script()

    if findings:
        for finding in findings:
            print(finding)
        print("\n{} finding(s)".format(len(findings)))
        return 1

    print("OK: one public listener on 443 at a regional load balancer; TLS 1.2+ RESTRICTED; WAF "
          "attached and enforcing in all four environments; no inbound allow; the database port open "
          "to the API tier's tag only and denied to every other source; folders refuse a default "
          "network, public VM addresses and a public database.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
