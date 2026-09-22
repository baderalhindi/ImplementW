variable "project_id" {
  description = "The environment's own GCP project."
  type        = string
}

variable "region" {
  description = "The named in-Kingdom region. The instance, its automated backups and its transaction logs are all here (ADR-001 C-3)."
  type        = string
}

variable "instance_name" {
  description = "Instance name, taken from infra/environments/environments.json. Product-neutral by design (ADR-001 R-7)."
  type        = string
}

variable "database_name" {
  description = "Application database on the instance."
  type        = string
}

variable "database_user" {
  description = "Manifest name of the application database user. Recorded on the instance's labels so the manifest name stays traceable; the user itself is the runtime service account (see main.tf)."
  type        = string
}

variable "database_version" {
  description = "PostgreSQL major version. ADR-002 §4.2.3 fixes PostgreSQL 17, fallback 16, identical in all four environments and in the local Docker image."
  type        = string
  default     = "POSTGRES_17"

  validation {
    condition     = contains(["POSTGRES_17", "POSTGRES_16"], var.database_version)
    error_message = "ADR-002 §4.2.3 admits PostgreSQL 17, with 16 as the only fallback."
  }
}

variable "network_id" {
  description = "VPC the instance takes its private IP in."
  type        = string
}

variable "runtime_service_account" {
  description = "The environment's runtime service account (TASK-016). It is the database user: IAM authentication, so there is no password for this module to generate, hold in state or hand to anyone."
  type        = string
}

variable "tier" {
  description = "Machine type of the instance."
  type        = string
}

variable "availability_type" {
  description = "ZONAL or REGIONAL. An RTO of 4 to 6 hours does not by itself require regional HA (ADR-001 C-11); set per environment and re-test the cost against that target."
  type        = string

  validation {
    condition     = contains(["ZONAL", "REGIONAL"], var.availability_type)
    error_message = "availability_type is ZONAL or REGIONAL."
  }
}

variable "disk_size_gb" {
  description = "Initial data disk size. Autoresize is on, so this is a floor, not a ceiling."
  type        = number
}

variable "disk_autoresize_limit_gb" {
  description = "Ceiling on automatic disk growth. Zero means unlimited, which on a shared billing account is a cost incident waiting for a runaway query."
  type        = number
}

variable "backup_start_time" {
  description = "Start of the daily backup window, HH:MM UTC."
  type        = string
  default     = "22:00"
}

variable "transaction_log_retention_days" {
  description = "Length of the point-in-time recovery window. RPO 1 hour (PTBC-048) requires PITR to be on; the window length is an operational choice TASK-023's drill exercises."
  type        = number
  default     = 7
}

variable "retained_backups" {
  description = "Number of automated backups kept. OQ-003 / PTBC-048: the retention period is an open AHDA decision, so there is no default here and none is invented. Null leaves the provider default in place; PROD must set it (see platform/guards.tf)."
  type        = number
  default     = null
}

variable "deletion_protection" {
  description = "Whether terraform destroy and the API may delete the instance."
  type        = bool
}

variable "maintenance_window" {
  description = "Weekly maintenance window: day 1-7 (Monday-Sunday), hour 0-23 UTC."
  type = object({
    day  = number
    hour = number
  })
}

variable "labels" {
  description = "Labels applied to the instance."
  type        = map(string)
}
