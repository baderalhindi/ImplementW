# Managed database (TASK-017 authors the module; TASK-020 owns encryption, backup and the drills
# inside it).
#
# Cloud SQL for PostgreSQL 17 — ADR-002 §4.2.3, which closed ADR-001 R-7. The application is
# written to standard PostgreSQL only, so if AHDA IT requires AlloyDB the change is this module's
# resource type and nothing above it.
#
# Three properties are not configuration but the point of the module:
#   - no public endpoint and no non-TLS connection (CTL-03, CTL-17)
#   - automated backups and point-in-time recovery in the named region, sized to RPO 1 hour
#     (PTBC-048, CTL-35, ADR-001 C-3)
#   - no password anywhere: the application authenticates as its runtime service account, so this
#     module creates, holds and hands over no secret material (CTL-18)

resource "google_sql_database_instance" "main" {
  project             = var.project_id
  name                = var.instance_name
  region              = var.region
  database_version    = var.database_version
  deletion_protection = var.deletion_protection

  settings {
    tier                  = var.tier
    availability_type     = var.availability_type
    disk_type             = "PD_SSD"
    disk_size             = var.disk_size_gb
    disk_autoresize       = true
    disk_autoresize_limit = var.disk_autoresize_limit_gb
    user_labels           = merge(var.labels, { database_user = var.database_user })

    ip_configuration {
      # No public IP exists to be firewalled, so the database tier cannot be reached from public
      # ingress by configuration error (CTL-03). ENCRYPTED_ONLY is the rejection TASK-020's
      # validation check attempts: a non-TLS connection is refused by the instance (CTL-17).
      ipv4_enabled    = false
      private_network = var.network_id
      ssl_mode        = "ENCRYPTED_ONLY"
    }

    backup_configuration {
      enabled                        = true
      start_time                     = var.backup_start_time
      location                       = var.region
      point_in_time_recovery_enabled = true
      transaction_log_retention_days = var.transaction_log_retention_days

      dynamic "backup_retention_settings" {
        for_each = var.retained_backups == null ? [] : [var.retained_backups]
        content {
          retention_unit   = "COUNT"
          retained_backups = backup_retention_settings.value
        }
      }
    }

    maintenance_window {
      day          = var.maintenance_window.day
      hour         = var.maintenance_window.hour
      update_track = "stable"
    }

    # Written out one block per flag rather than generated: a static security scanner reads the
    # literal blocks, and these six are exactly what it is looking for.

    # The application authenticates as a service account; no password is issued, so none can leak.
    database_flags {
      name  = "cloudsql.iam_authentication"
      value = "on"
    }

    # Connection-level audit logging, all five CIS GCP Foundation benchmark items. Who connected,
    # who disconnected and what waited on a lock is the database half of the evidence CTL-26
    # forwards to AHDA's SIEM and CTL-03's segmentation claim is read against.
    database_flags {
      name  = "log_connections"
      value = "on"
    }

    database_flags {
      name  = "log_disconnections"
      value = "on"
    }

    database_flags {
      name  = "log_checkpoints"
      value = "on"
    }

    database_flags {
      name  = "log_lock_waits"
      value = "on"
    }

    database_flags {
      name  = "log_temp_files"
      value = "0"
    }

    # Every connection, accepted or rejected, is a log line. TASK-021's segmentation evidence and
    # TASK-033's SIEM forwarding both read it.
    insights_config {
      query_insights_enabled = true
    }
  }
}

resource "google_sql_database" "app" {
  project   = var.project_id
  instance  = google_sql_database_instance.main.name
  name      = var.database_name
  charset   = "UTF8"
  collation = "en_US.UTF8"
}

# The application's database identity. A Cloud SQL IAM service-account user is named by the
# service account with the ".gserviceaccount.com" suffix removed — that is the API's own form.
resource "google_sql_user" "app" {
  project  = var.project_id
  instance = google_sql_database_instance.main.name
  name     = trimsuffix(var.runtime_service_account, ".gserviceaccount.com")
  type     = "CLOUD_IAM_SERVICE_ACCOUNT"
}

# roles/cloudsql.client opens the connection; roles/cloudsql.instanceUser lets the service account
# log in as itself. Neither grants read of any data the database does not give it.
resource "google_project_iam_member" "runtime_sql_client" {
  project = var.project_id
  role    = "roles/cloudsql.client"
  member  = "serviceAccount:${var.runtime_service_account}"
}

resource "google_project_iam_member" "runtime_sql_instance_user" {
  project = var.project_id
  role    = "roles/cloudsql.instanceUser"
  member  = "serviceAccount:${var.runtime_service_account}"
}
