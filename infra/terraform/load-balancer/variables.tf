variable "project_id" {
  description = "The environment's own GCP project."
  type        = string
}

variable "region" {
  description = "The named in-Kingdom region. Every resource in this module is regional, which is what keeps TLS termination inside the Kingdom (ADR-001 C-2, CTL-04)."
  type        = string
}

variable "name_prefix" {
  description = "Prefix for every resource this module creates, e.g. pmplatform-dev."
  type        = string
}

variable "network_id" {
  description = "VPC the forwarding rules belong to."
  type        = string
}

variable "cloud_run_service_name" {
  description = "Backend service behind the load balancer."
  type        = string
}

variable "domain_name" {
  description = "The environment's hostname, the host part of APP_BASE_URL. The certificate is issued for it. No AHDA-owned DNS zone is named anywhere in the workbook yet (environment-separation.md F-1, proposed UGV-12), so this is empty today and the preflight in ../platform refuses the apply."
  type        = string
}

variable "min_tls_version" {
  description = "Lowest TLS version the load balancer will negotiate. CTL-04 requires 1.2 or above."
  type        = string
  default     = "TLS_1_2"

  validation {
    condition     = contains(["TLS_1_2", "TLS_1_3"], var.min_tls_version)
    error_message = "CTL-04 requires TLS 1.2 or above; TLS_1_0 and TLS_1_1 are not admissible."
  }
}

variable "waf_rule_sets" {
  description = "Cloud Armor preconfigured WAF rule sets, as rule set name to sensitivity (1-4). This is the G-7 baseline TASK-021 records (network-security.md §3.4). methodenforcement is deliberately absent: at sensitivity 1 it admits only GET, HEAD, POST and OPTIONS, and the API uses PUT, PATCH and DELETE (api-conventions.md). php, java, nodejs and cve-canary target runtimes the platform does not run."
  type        = map(number)
  default = {
    sqli-v33-stable             = 1
    xss-v33-stable              = 1
    lfi-v33-stable              = 1
    rfi-v33-stable              = 1
    rce-v33-stable              = 1
    scannerdetection-v33-stable = 1
    protocolattack-v33-stable   = 1
    sessionfixation-v33-stable  = 1
  }
}

variable "waf_preview" {
  description = "Whether the WAF rules log rather than block. False — enforcing — in every environment (TASK-021, network-security.md §3.4): sensitivity-1 rules are the low-false-positive tier, and enforcing them from DEV onward is what makes a false positive surface in testing rather than on PROD's first day. Setting it true turns the WAF off in all but name; network-security-check.py fails the pull request if a root does."
  type        = bool
  default     = false
}

variable "labels" {
  description = "Labels applied to the labelled resources in this module."
  type        = map(string)
}
