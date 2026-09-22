# Network (TASK-017 authors the module; TASK-021 owns the security configuration inside it).
#
# One VPC per environment, in the environment's own project, with no route to any other
# environment: separation is structural, not a rule (TASK-016 §3.1, CTL-02). Three ranges:
# the application subnet, the proxy-only subnet the regional load balancer runs in, and the
# private services access range Cloud SQL takes its private IP from.
#
# Custom mode, because auto mode would create a subnet in every GCP region and ADR-001 C-1
# admits exactly one.

resource "google_compute_network" "vpc" {
  project                 = var.project_id
  name                    = var.network_name
  auto_create_subnetworks = false
  routing_mode            = "REGIONAL"
  description             = "PMPlatform ${var.labels.environment} — TASK-017"
}

resource "google_compute_subnetwork" "app" {
  project       = var.project_id
  name          = "${var.network_name}-app"
  region        = var.region
  network       = google_compute_network.vpc.id
  ip_cidr_range = var.app_subnet_cidr

  # Reaches Google APIs (Secret Manager, Cloud Logging) without a public address.
  private_ip_google_access = true

  # Flow logs are the evidence behind TASK-021's port scan and CTL-03's segmentation claim.
  log_config {
    aggregation_interval = "INTERVAL_5_SEC"
    flow_sampling        = 0.5
    metadata             = "INCLUDE_ALL_METADATA"
  }
}

# The regional external Application Load Balancer runs its Envoy proxies in this subnet, in this
# region. That is what makes TLS terminate inside the Kingdom (ADR-001 C-2, CTL-04); a global load
# balancer would terminate at the Google edge nearest the client. No resource is placed here.
resource "google_compute_subnetwork" "proxy" {
  project       = var.project_id
  name          = "${var.network_name}-proxy"
  region        = var.region
  network       = google_compute_network.vpc.id
  ip_cidr_range = var.proxy_subnet_cidr
  purpose       = "REGIONAL_MANAGED_PROXY"
  role          = "ACTIVE"
}

# Private services access: the range Google's service producer network allocates the Cloud SQL
# instance's private IP from. Pinned rather than auto-allocated so the address plan is reviewable
# and stable across a rebuild.
resource "google_compute_global_address" "private_services" {
  project       = var.project_id
  name          = "${var.network_name}-private-services"
  purpose       = "VPC_PEERING"
  address_type  = "INTERNAL"
  address       = cidrhost(var.private_services_cidr, 0)
  prefix_length = var.private_services_prefix_length
  network       = google_compute_network.vpc.id
}

resource "google_service_networking_connection" "private_services" {
  network                 = google_compute_network.vpc.id
  service                 = "servicenetworking.googleapis.com"
  reserved_peering_ranges = [google_compute_global_address.private_services.name]
}

# Firewall. The database has no public endpoint and the Cloud Run service accepts traffic only
# from the load balancer, so these rules are the tier-to-tier half of CTL-03: the application tier
# may open the database port and nothing else on the VPC path.
#
# Egress to Google APIs and to the internet does not traverse the VPC — the Cloud Run interface
# runs with PRIVATE_RANGES_ONLY egress — so the deny below does not cut Secret Manager or logging.

resource "google_compute_firewall" "allow_app_egress_to_database" {
  project     = var.project_id
  name        = "${var.network_name}-allow-app-egress-to-database"
  network     = google_compute_network.vpc.id
  description = "Application tier to database tier, PostgreSQL only (CTL-03)."
  direction   = "EGRESS"
  priority    = 1000

  target_tags        = [var.app_network_tag]
  destination_ranges = [var.private_services_cidr]

  allow {
    protocol = "tcp"
    ports    = [tostring(var.database_port)]
  }
}

resource "google_compute_firewall" "deny_app_egress" {
  project     = var.project_id
  name        = "${var.network_name}-deny-app-egress"
  network     = google_compute_network.vpc.id
  description = "Everything the rule above does not allow. Logged, so TASK-021 tunes from evidence."
  direction   = "EGRESS"
  priority    = 65534

  target_tags        = [var.app_network_tag]
  destination_ranges = ["0.0.0.0/0"]

  deny {
    protocol = "all"
  }

  log_config {
    metadata = "INCLUDE_ALL_METADATA"
  }
}

# GCP already denies unmatched ingress. This rule adds nothing but the log record, which is what
# turns "no unintended open ports" from an assertion into evidence for TASK-021's port scan.
resource "google_compute_firewall" "deny_all_ingress" {
  project     = var.project_id
  name        = "${var.network_name}-deny-all-ingress"
  network     = google_compute_network.vpc.id
  description = "Explicit, logged form of the implied deny (CTL-03)."
  direction   = "INGRESS"
  priority    = 65535

  source_ranges = ["0.0.0.0/0"]

  deny {
    protocol = "all"
  }

  log_config {
    metadata = "INCLUDE_ALL_METADATA"
  }
}
