variable "environment" {
  description = "Which environment this root is. Must be one of the four the manifest declares."
  type        = string

  validation {
    condition     = contains(["dev", "sit", "uat", "prod"], var.environment)
    error_message = "environment is dev, sit, uat or prod (infra/environments/environments.json)."
  }
}

variable "manifest_path" {
  description = "The environment manifest TASK-016 owns. It is the single source of truth for project ids, network and instance names, secret ids and the region; nothing here restates a value it already holds."
  type        = string
  default     = "../../environments/environments.json"
}

variable "container_image" {
  description = "The image to deploy, by digest. Supplied at apply time by the promotion pipeline (TASK-018) as TF_VAR_container_image — never committed, because one artifact is promoted unchanged and its digest is not a property of the environment."
  type        = string
}

# --- Per-environment sizing and posture ---------------------------------------------------------
# Everything below differs between environments on purpose. Everything not below is identical in
# all four, which is what makes a diff between two environment roots worth reading.

variable "app_subnet_cidr" {
  description = "Primary range of the application subnet."
  type        = string
}

variable "proxy_subnet_cidr" {
  description = "Range of the load balancer's proxy-only subnet."
  type        = string
}

variable "private_services_cidr" {
  description = "Range private services access allocates the database's private IP from."
  type        = string
}

variable "private_services_prefix_length" {
  description = "Prefix length of private_services_cidr."
  type        = number
}

variable "database_tier" {
  description = "Cloud SQL machine type."
  type        = string
}

variable "database_availability_type" {
  description = "ZONAL or REGIONAL."
  type        = string
}

variable "database_disk_size_gb" {
  description = "Initial data disk size."
  type        = number
}

variable "database_disk_autoresize_limit_gb" {
  description = "Ceiling on automatic database disk growth."
  type        = number
}

variable "retained_backups" {
  description = "Automated backups kept. Null everywhere until OQ-003 closes; PROD may not be applied while it is null (see guards.tf)."
  type        = number
  default     = null
}

variable "database_encryption_key_name" {
  description = "Customer-managed encryption key for the database, as a full KMS resource name in the named region. Null everywhere: encryption at rest is on regardless, and whether the key must be AHDA's follows the data classification in ADR-001 R-4 (TASK-020)."
  type        = string
  default     = null
}

variable "backup_export_schedule" {
  description = "Cron schedule, UTC, for the export of the database to the environment's backup bucket — the copy that survives the instance being deleted. Null in DEV, which has no backup bucket (TASK-020)."
  type        = string
  default     = null
}

variable "noncurrent_version_retention_days" {
  description = "Days a superseded object version is kept. Null while OQ-003 is open."
  type        = number
  default     = null
}

variable "compute_cpu" {
  description = "CPU limit per Cloud Run instance."
  type        = string
}

variable "compute_memory" {
  description = "Memory limit per Cloud Run instance."
  type        = string
}

variable "compute_min_instances" {
  description = "Instances kept warm."
  type        = number
}

variable "compute_max_instances" {
  description = "Scaling ceiling."
  type        = number
}

variable "waf_preview" {
  description = "Whether the WAF logs rather than blocks. False in every environment: the G-7 baseline enforces from DEV onward (TASK-021)."
  type        = bool
  default     = false
}

variable "deletion_protection" {
  description = "Whether terraform destroy may remove the database instance and the Cloud Run service."
  type        = bool
}
