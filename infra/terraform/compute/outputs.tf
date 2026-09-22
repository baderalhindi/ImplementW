output "service_name" {
  description = "Cloud Run service name, for the serverless network endpoint group in ../load-balancer."
  value       = google_cloud_run_v2_service.api.name
}

output "service_uri" {
  description = "The service's own URL. Not the platform's address: ingress is restricted to the load balancer, so this URL answers nothing from outside."
  value       = google_cloud_run_v2_service.api.uri
}
