output "network_id" {
  description = "VPC id, for resources that attach to the network."
  value       = google_compute_network.vpc.id
}

output "network_self_link" {
  description = "VPC self link, for the Cloud SQL private network and the forwarding rules."
  value       = google_compute_network.vpc.self_link
}

output "app_subnet_id" {
  description = "Application subnet id, for the Cloud Run Direct VPC egress interface."
  value       = google_compute_subnetwork.app.id
}

output "private_services_connection_id" {
  description = "Service networking connection. The database instance depends on it: a private-IP instance cannot be created before the peering exists."
  value       = google_service_networking_connection.private_services.id
}
