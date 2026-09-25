# Compute (TASK-017).
#
# ADR-001 R-6 — Cloud Run or GKE — is open, has no owner, and ADR-002 §4.3 declined to take it,
# recording as S-7 that "TASK-017 cannot codify compute". This module codifies compute on
# **Cloud Run**, as the delivery team's selection, provisionally, pending the answer to Q4 of the
# ADR-001 confirmation request. The reasoning and what a GKE answer would cost are in
# docs/architecture/infrastructure-as-code.md §4; the short form is that ADR-003 deploys one
# container for 150 named users, and nothing in the workbook asks for Kubernetes.
#
# What that decision touches is deliberately small. The interface this module presents — a named
# service in the project's own region, reachable only through the regional load balancer, running
# as the environment's runtime service account, which is also the identity it reads its secrets
# with (TASK-019) — is what ../load-balancer consumes. A GKE answer replaces this module's body and the backend group in
# ../load-balancer, and leaves network, database, storage and the application unchanged.

resource "google_cloud_run_v2_service" "api" {
  project  = var.project_id
  name     = var.service_name
  location = var.region

  # Only the regional load balancer may reach the service. Everything CTL-04 requires — TLS 1.2+,
  # WAF, in-Kingdom termination — is enforced there, so a path that bypasses it would bypass all of
  # it. The service's own run.app URL answers nothing from outside.
  ingress             = "INGRESS_TRAFFIC_INTERNAL_LOAD_BALANCER"
  deletion_protection = var.deletion_protection
  labels              = var.labels

  template {
    service_account = var.runtime_service_account

    scaling {
      min_instance_count = var.min_instances
      max_instance_count = var.max_instances
    }

    max_instance_request_concurrency = var.request_concurrency

    # Direct VPC egress: the instance holds an address in the application subnet, so the database
    # is reachable on its private IP and the firewall rules in ../network apply to it by tag.
    # PRIVATE_RANGES_ONLY keeps Google API traffic — Secret Manager, Cloud Logging — off the VPC
    # path, which is why the deny-all egress rule there does not cut it.
    vpc_access {
      egress = "PRIVATE_RANGES_ONLY"

      network_interfaces {
        network    = var.network_id
        subnetwork = var.subnet_id
        tags       = [var.network_tag]
      }
    }

    containers {
      image = var.container_image

      ports {
        container_port = var.container_port
      }

      resources {
        limits = {
          cpu    = var.cpu
          memory = var.memory
        }
        startup_cpu_boost = true
      }

      dynamic "env" {
        for_each = var.plain_environment
        content {
          name  = env.key
          value = env.value
        }
      }

      startup_probe {
        http_get {
          path = "/health"
          port = var.container_port
        }
        initial_delay_seconds = 10
        period_seconds        = 5
        failure_threshold     = 12
      }

      liveness_probe {
        http_get {
          path = "/health"
          port = var.container_port
        }
        period_seconds    = 30
        failure_threshold = 3
      }
    }
  }

  traffic {
    type    = "TRAFFIC_TARGET_ALLOCATION_TYPE_LATEST"
    percent = 100
  }
}

# The release's schema migrations (TASK-024; docs/architecture/database-migrations.md). The database
# has no public IP, so migrations cannot run from the pipeline's runner; they run here, inside the
# environment, before the service is moved to the image that needs them
# (.github/scripts/migrate-environment.sh). Same image, identity, network path and configuration as
# the service, so the job reads DB_CONNECTION_STRING from the secret store exactly as the service
# does; `migrate` makes the image apply its migrations and exit instead of serving.
resource "google_cloud_run_v2_job" "migrate" {
  project             = var.project_id
  name                = var.migration_job_name
  location            = var.region
  deletion_protection = var.deletion_protection
  labels              = var.labels

  template {
    task_count  = 1
    parallelism = 1

    template {
      service_account = var.runtime_service_account

      # A failed migration fails the deployment step and is investigated; it is not retried out of
      # sight. Each migration runs in its own transaction, so a failure leaves the last good one applied.
      max_retries = 0
      timeout     = "1800s"

      vpc_access {
        egress = "PRIVATE_RANGES_ONLY"

        network_interfaces {
          network    = var.network_id
          subnetwork = var.subnet_id
          tags       = [var.network_tag]
        }
      }

      containers {
        image = var.container_image
        args  = ["migrate"]

        resources {
          limits = {
            cpu    = var.cpu
            memory = var.memory
          }
        }

        dynamic "env" {
          for_each = var.plain_environment
          content {
            name  = env.key
            value = env.value
          }
        }
      }
    }
  }

  # migrate-environment.sh points this job at the release image with `gcloud run jobs update` just
  # before the deployment's `terraform apply`, which sets the same digest here — so the apply that
  # follows every migration leaves no drift (T-9).
}

# The load balancer calls the service unauthenticated; the ingress restriction above is what keeps
# anyone else from doing the same, because traffic that did not arrive through the load balancer is
# rejected before this binding is consulted.
#
# An organisation that enforces constraints/iam.allowedPolicyMemberDomains will refuse allUsers on
# any binding. If AHDA's does, this becomes an authenticated backend with the load balancer's
# service agent as the invoker — see docs/architecture/infrastructure-as-code.md F-4.
resource "google_cloud_run_v2_service_iam_member" "invoker" {
  project  = var.project_id
  location = var.region
  name     = google_cloud_run_v2_service.api.name
  role     = "roles/run.invoker"
  member   = "allUsers"
}
