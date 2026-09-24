output "lb_ip_address" {
  description = "The environment's public address. AHDA points the A record for the APP_BASE_URL host here."
  value       = module.platform.lb_ip_address
}

output "dns_authorization_record" {
  description = "The CNAME AHDA publishes so the managed certificate can be issued (F-1)."
  value       = module.platform.dns_authorization_record
}

output "database_connection_name" {
  description = "project:region:instance, for the runbooks and the restore drill (TASK-023)."
  value       = module.platform.database_connection_name
}

output "bucket_names" {
  description = "Object storage created for this environment, by role."
  value       = module.platform.bucket_names
}

output "database_backup_configuration" {
  description = "The documented backup schedule as applied — window, PITR, retention, export and encryption mode (CTL-35, TASK-023)."
  value       = module.platform.database_backup_configuration
}

output "database_backup_export_uri" {
  description = "The object scheduled exports are written to; the target DB_BACKUP_STORAGE_CONNECTION_STRING addresses."
  value       = module.platform.database_backup_export_uri
}
