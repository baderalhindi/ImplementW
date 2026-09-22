variable "project_id" {
  description = "The environment's own GCP project. Bucket names are prefixed with it, which also makes them globally unique."
  type        = string
}

variable "region" {
  description = "The named in-Kingdom region. Every bucket is single-region here — not multi-region, not dual-region (ADR-001 C-4)."
  type        = string
}

variable "runtime_service_account" {
  description = "The environment's runtime service account. It is granted object access to the document bucket and to nothing else."
  type        = string
}

variable "create_backup_buckets" {
  description = "Whether to create the database and document backup targets. The Environment and Secrets sheet scopes DB_BACKUP_STORAGE_CONNECTION_STRING and DOCUMENT_STORAGE_BACKUP_CONNECTION_STRING to SIT, UAT and PROD, so DEV has neither."
  type        = bool
}

variable "noncurrent_version_retention_days" {
  description = "Days a non-current object version is kept before deletion. OQ-003 / PTBC-048: the retention period is an open AHDA decision. Null creates no lifecycle rule, so nothing is deleted on a period nobody approved."
  type        = number
  default     = null
}

variable "soft_delete_retention_days" {
  description = "Soft-delete window, within which a deleted object can be restored. This is a recovery mechanism, not a records-retention period."
  type        = number
  default     = 7
}

variable "labels" {
  description = "Labels applied to every bucket."
  type        = map(string)
}
