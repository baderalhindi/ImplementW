# Ingress: regional external Application Load Balancer, WAF and TLS (TASK-017 authors the module;
# TASK-021 owns the WAF baseline and the segmentation evidence inside it).
#
# Every resource here is **regional**. That is the whole point of the module and not a style
# choice: a global external load balancer terminates TLS at the Google edge nearest the client,
# which is not necessarily inside the Kingdom, and ADR-001 C-2 forbids that. The proxies run in the
# proxy-only subnet of the environment's own VPC, in the named region (CTL-04).
#
# Port 80 exists only to redirect to 443. There is no plaintext path to the application.

resource "google_compute_address" "lb" {
  project      = var.project_id
  name         = "${var.name_prefix}-lb"
  region       = var.region
  address_type = "EXTERNAL"
  network_tier = "PREMIUM"
}

# --- WAF -------------------------------------------------------------------------------------

resource "google_compute_region_security_policy" "waf" {
  project     = var.project_id
  name        = "${var.name_prefix}-waf"
  region      = var.region
  type        = "CLOUD_ARMOR"
  description = "PMPlatform ${var.labels.environment} ingress WAF. Rule baseline: G-7, TASK-021."
}

resource "google_compute_region_security_policy_rule" "waf" {
  for_each = var.waf_rule_sets

  project         = var.project_id
  region          = var.region
  security_policy = google_compute_region_security_policy.waf.name
  description     = "OWASP CRS ${each.key}, sensitivity ${each.value}"
  action          = "deny(403)"
  priority        = 1000 + index(keys(var.waf_rule_sets), each.key)
  preview         = var.waf_preview

  match {
    expr {
      expression = "evaluatePreconfiguredWaf('${each.key}', {'sensitivity': ${each.value}})"
    }
  }
}

# The policy's own default. Everything the rules above do not deny reaches the application, where
# authentication and authorisation decide it (CTL-08). A WAF is not the access control.
resource "google_compute_region_security_policy_rule" "default" {
  project         = var.project_id
  region          = var.region
  security_policy = google_compute_region_security_policy.waf.name
  description     = "Default rule."
  action          = "allow"
  priority        = 2147483647

  match {
    versioned_expr = "SRC_IPS_V1"
    config {
      src_ip_ranges = ["*"]
    }
  }
}

# --- Backend ---------------------------------------------------------------------------------

resource "google_compute_region_network_endpoint_group" "api" {
  project               = var.project_id
  name                  = "${var.name_prefix}-api-neg"
  region                = var.region
  network_endpoint_type = "SERVERLESS"

  cloud_run {
    service = var.cloud_run_service_name
  }
}

resource "google_compute_region_backend_service" "api" {
  project               = var.project_id
  name                  = "${var.name_prefix}-api"
  region                = var.region
  load_balancing_scheme = "EXTERNAL_MANAGED"
  security_policy       = google_compute_region_security_policy.waf.self_link

  backend {
    group = google_compute_region_network_endpoint_group.api.id
    # A serverless network endpoint group takes no balancing mode; the capacity scaler is the one
    # value an EXTERNAL_MANAGED backend service still requires.
    capacity_scaler = 1.0
  }

  # Every request is a log line with its WAF verdict on it. This is the evidence behind TASK-021's
  # port scan and the input to TASK-033's SIEM forwarding.
  log_config {
    enable      = true
    sample_rate = 1.0
  }
}

# --- TLS -------------------------------------------------------------------------------------

resource "google_compute_region_ssl_policy" "tls" {
  project         = var.project_id
  name            = "${var.name_prefix}-tls"
  region          = var.region
  profile         = "MODERN"
  min_tls_version = var.min_tls_version
}

# A Google-managed certificate for a domain AHDA owns. AHDA publishes the CNAME this authorisation
# asks for (see the dns_authorization_record output); Certificate Manager then issues and renews
# the certificate without anyone holding a private key.
resource "google_certificate_manager_dns_authorization" "app" {
  project  = var.project_id
  name     = "${var.name_prefix}-dns-auth"
  location = var.region
  domain   = var.domain_name
  labels   = var.labels
}

resource "google_certificate_manager_certificate" "app" {
  project  = var.project_id
  name     = "${var.name_prefix}-cert"
  location = var.region
  labels   = var.labels

  managed {
    domains            = [var.domain_name]
    dns_authorizations = [google_certificate_manager_dns_authorization.app.id]
  }
}

# --- Front end -------------------------------------------------------------------------------

resource "google_compute_region_url_map" "https" {
  project         = var.project_id
  name            = "${var.name_prefix}-https"
  region          = var.region
  default_service = google_compute_region_backend_service.api.id
}

resource "google_compute_region_target_https_proxy" "app" {
  project                          = var.project_id
  name                             = "${var.name_prefix}-https-proxy"
  region                           = var.region
  url_map                          = google_compute_region_url_map.https.id
  certificate_manager_certificates = [google_certificate_manager_certificate.app.id]
  ssl_policy                       = google_compute_region_ssl_policy.tls.id
}

resource "google_compute_forwarding_rule" "https" {
  project               = var.project_id
  name                  = "${var.name_prefix}-https"
  region                = var.region
  load_balancing_scheme = "EXTERNAL_MANAGED"
  network_tier          = "PREMIUM"
  network               = var.network_id
  ip_address            = google_compute_address.lb.id
  port_range            = "443"
  target                = google_compute_region_target_https_proxy.app.id
}

resource "google_compute_region_url_map" "http_redirect" {
  project = var.project_id
  name    = "${var.name_prefix}-http-redirect"
  region  = var.region

  default_url_redirect {
    https_redirect         = true
    redirect_response_code = "MOVED_PERMANENTLY_DEFAULT"
    strip_query            = false
  }
}

resource "google_compute_region_target_http_proxy" "redirect" {
  project = var.project_id
  name    = "${var.name_prefix}-http-proxy"
  region  = var.region
  url_map = google_compute_region_url_map.http_redirect.id
}

resource "google_compute_forwarding_rule" "http" {
  project               = var.project_id
  name                  = "${var.name_prefix}-http"
  region                = var.region
  load_balancing_scheme = "EXTERNAL_MANAGED"
  network_tier          = "PREMIUM"
  network               = var.network_id
  ip_address            = google_compute_address.lb.id
  port_range            = "80"
  target                = google_compute_region_target_http_proxy.redirect.id
}
