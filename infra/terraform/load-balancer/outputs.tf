output "ip_address" {
  description = "The environment's only public address. The A record for domain_name points here."
  value       = google_compute_address.lb.address
}

output "dns_authorization_record" {
  description = "The CNAME AHDA must publish in its own DNS zone before the managed certificate can be issued (environment-separation.md F-1)."
  value = {
    name   = one(google_certificate_manager_dns_authorization.app.dns_resource_record).name
    type   = one(google_certificate_manager_dns_authorization.app.dns_resource_record).type
    target = one(google_certificate_manager_dns_authorization.app.dns_resource_record).data
  }
}

output "security_policy_name" {
  description = "Cloud Armor policy attached to the backend. TASK-021 tunes its rules."
  value       = google_compute_region_security_policy.waf.name
}
