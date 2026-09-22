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
