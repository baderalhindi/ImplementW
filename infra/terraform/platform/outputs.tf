output "project_id" {
  description = "The environment's project, as the manifest names it."
  value       = local.environment.project_id
}

output "region" {
  description = "The region every resource above was created in."
  value       = local.platform.region
}

output "lb_ip_address" {
  description = "The environment's public address. AHDA points the A record for the APP_BASE_URL host here."
  value       = module.load_balancer.ip_address
}

output "dns_authorization_record" {
  description = "The CNAME AHDA publishes so the managed certificate can be issued."
  value       = module.load_balancer.dns_authorization_record
}

output "database_connection_name" {
  description = "project:region:instance, for the runbooks and the restore drill (TASK-023)."
  value       = module.database.connection_name
}

output "bucket_names" {
  description = "Object storage created for this environment, by role."
  value       = module.storage.bucket_names
}

output "database_backup_configuration" {
  description = "The documented backup schedule as applied — window, PITR, retention, export and encryption mode. CTL-35's evidence and TASK-023's drill are read against this rather than against a document that can drift from it."
  value       = module.database.backup_configuration
}

output "database_backup_export_uri" {
  description = "The object scheduled exports are written to. Null in DEV, which has no backup bucket. This is the target DB_BACKUP_STORAGE_CONNECTION_STRING addresses (TASK-019 writes the value)."
  value       = module.database.backup_export_uri
}

output "migration_job_name" {
  description = "Cloud Run job that applies the release's migrations; the value of the MIGRATION_JOB repository variable (TASK-024)."
  value       = module.compute.migration_job_name
}
