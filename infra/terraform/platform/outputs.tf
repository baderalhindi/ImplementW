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
