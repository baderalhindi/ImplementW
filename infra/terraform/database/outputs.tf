output "instance_name" {
  description = "Instance name, for the runbooks and the restore drill (TASK-023)."
  value       = google_sql_database_instance.main.name
}

output "connection_name" {
  description = "project:region:instance — the form the Cloud SQL connectors take."
  value       = google_sql_database_instance.main.connection_name
}

output "private_ip_address" {
  description = "The instance's only address. There is no public one."
  value       = google_sql_database_instance.main.private_ip_address
}

output "database_name" {
  description = "Application database on the instance."
  value       = google_sql_database.app.name
}

output "service_account_email" {
  description = "The instance's own service agent. It is the principal that writes an export and reads it back, so a restore into another bucket is one more grant to this address (TASK-023)."
  value       = google_sql_database_instance.main.service_account_email_address
}

output "backup_export_uri" {
  description = "The object scheduled exports are written to, as gs://bucket/instance/database.sql.gz. Null where the environment has no backup bucket. This is what DB_BACKUP_STORAGE_CONNECTION_STRING addresses (TASK-019 writes the value; nothing here reads or writes a secret)."
  value       = local.backup_export_uri
}

output "backup_configuration" {
  description = "The documented backup schedule, as applied. The restore drill (TASK-023) and the backup evidence CTL-35 asks for are read against these values rather than against a document that can drift from them."
  value = {
    region                         = var.region
    start_time                     = var.backup_start_time
    point_in_time_recovery         = true
    transaction_log_retention_days = var.transaction_log_retention_days
    retained_backups               = var.retained_backups
    export_schedule                = var.backup_export_schedule
    encryption                     = var.encryption_key_name == null ? "google-managed" : var.encryption_key_name
  }
}
