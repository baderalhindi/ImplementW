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
  description = "Cloud Armor preconfigured WAF rule sets, as rule set name to sensitivity (1-4). The baseline and its tuning are G-7 in the control matrix and belong to TASK-021; what this module owns is that the policy exists and is attached."
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
  description = "Whether the WAF rules log rather than block. True until TASK-021 has tuned them against real traffic: a rule set turned straight to enforcing is how a WAF takes a working application off the air on its first day."
  type        = bool
  default     = true
}

variable "labels" {
  description = "Labels applied to the labelled resources in this module."
  type        = map(string)
}
