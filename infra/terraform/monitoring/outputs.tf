output "uptime_check_id" {
  description = "The uptime check, as the check_passed metric's check_id label names it."
  value       = google_monitoring_uptime_check_config.readiness.uptime_check_id
}

output "alert_policy_name" {
  description = "projects/<project>/alertPolicies/<id>."
  value       = google_monitoring_alert_policy.readiness.name
}

output "configured_detection_seconds" {
  description = "The longest the configuration lets an outage go unflagged: up to one period before the next check, its timeout, then the failure duration. Cloud Monitoring's own ingestion and notification delay comes on top (D-7)."
  value       = var.check_period_seconds + var.check_timeout_seconds + var.failure_duration_seconds
}
