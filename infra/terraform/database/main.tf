# Managed database (TASK-017 authored the module; TASK-020 owns the encryption, backup and
# recovery configuration in it).
#
# Cloud SQL for PostgreSQL 17 — ADR-002 §4.2.3, which closed ADR-001 R-7. The application is
# written to standard PostgreSQL only, so if AHDA IT requires AlloyDB the change is this module's
# resource type and nothing above it.
#
# Four properties are not configuration but the point of the module:
#   - no public endpoint and no non-TLS connection (CTL-03, CTL-17)
#   - encrypted at rest, Google-managed by default and customer-managed the moment a key is named
#     (CTL-17, ADR-001 R-4)
#   - automated backups and point-in-time recovery in the named region, sized to RPO 1 hour
#     (PTBC-048, CTL-35, ADR-001 C-3); off-instance copies in backup.tf
#   - no password anywhere: the application authenticates as its runtime service account, so this
#     module creates, holds and hands over no secret material (CTL-18)
#
# On RPO 1 hour. Cloud SQL's automated backup runs once a day, so backups alone are an RPO of 24
# hours and cannot meet PTBC-048. Point-in-time recovery is what meets it: the write-ahead log is
# archived continuously and a restore can name any second inside the log window, which is an RPO of
# seconds. PITR is therefore not an enhancement here, it is the mechanism, and it is asserted rather
# than assumed — the preconditions below fail the plan if the daily backup chain stops covering the
# log window, which is the one way a PITR window silently becomes unusable.

# The project number, which is what every Google-managed service agent is named after: the Cloud
# SQL agent that uses the encryption key here, and the Cloud Scheduler agent in backup.tf.
data "google_project" "this" {
  project_id = var.project_id
}

# Encryption at rest is on either way — Google-managed keys are not optional and cannot be turned
# off. This grant exists only for the case where AHDA's data classification (ADR-001 R-4) requires
# the key to be AHDA's: without it the instance cannot decrypt its own disk and the apply fails.
resource "google_kms_crypto_key_iam_member" "sql" {
  count = var.encryption_key_name == null ? 0 : 1

  crypto_key_id = var.encryption_key_name
  role          = "roles/cloudkms.cryptoKeyEncrypterDecrypter"
  member        = "serviceAccount:service-${data.google_project.this.number}@gcp-sa-cloud-sql.iam.gserviceaccount.com"
}

resource "google_sql_database_instance" "main" {
  project             = var.project_id
  name                = var.instance_name
  region              = var.region
  database_version    = var.database_version
  deletion_protection = var.deletion_protection

  # Null leaves Google-managed encryption in place; a key name replaces it with that key. No key
  # ring, key or rotation period is created here: those need a key administrator and a rotation
  # period, and both follow ADR-001 R-4 (infrastructure-as-code.md F-6).
  encryption_key_name = var.encryption_key_name

  settings {
    tier                  = var.tier
    availability_type     = var.availability_type
    disk_type             = "PD_SSD"
    disk_size             = var.disk_size_gb
    disk_autoresize       = true
    disk_autoresize_limit = var.disk_autoresize_limit_gb
    user_labels           = merge(var.labels, { database_user = var.database_user })

    # The Terraform-level deletion_protection above stops `terraform destroy`; this stops the API
    # and the console. An instance deleted by either takes its automated backups with it, which is
    # why backup.tf puts a copy outside the instance as well.
    deletion_protection_enabled = var.deletion_protection

    ip_configuration {
      # No public IP exists to be firewalled, so the database tier cannot be reached from public
      # ingress by configuration error (CTL-03). ENCRYPTED_ONLY is the rejection TASK-020's
      # validation check attempts: a non-TLS connection is refused by the instance (CTL-17).
      ipv4_enabled    = false
      private_network = var.network_id
      ssl_mode        = "ENCRYPTED_ONLY"

      # Pinned to the network module's private services range rather than left to whichever range
      # the peering offers, so the firewall rules written against that range address this
      # instance and nothing else (TASK-021, CTL-03).
      allocated_ip_range = var.allocated_ip_range
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

  # The key has to be usable before the disk it encrypts exists.
  depends_on = [google_kms_crypto_key_iam_member.sql]

  lifecycle {
    # A PITR window is only as long as the oldest automated backup that can seed it: recovery
    # replays the log forward from a backup, so a log day with no backup behind it cannot be
    # recovered to. Backups are daily, so the chain holds while retained_backups covers the log
    # window. Left unset, the provider's own default applies and the guard in ../platform holds
    # PROD until OQ-003 names a value.
    precondition {
      condition     = var.retained_backups == null || var.retained_backups >= var.transaction_log_retention_days
      error_message = "retained_backups (${coalesce(var.retained_backups, 0)}) is fewer than transaction_log_retention_days (${var.transaction_log_retention_days}). Automated backups are daily, and point-in-time recovery replays the log forward from one of them, so the oldest ${var.transaction_log_retention_days - coalesce(var.retained_backups, 0)} day(s) of log would have no backup to start from. Raise the retained backup count or shorten the log window (PTBC-048, CTL-35)."
    }

    # ADR-001 C-1 and C-3: the key that encrypts in-Kingdom data is itself in the Kingdom. A key
    # in another region would put the material that makes the data readable outside it.
    precondition {
      condition     = var.encryption_key_name == null || can(regex("/locations/${var.region}/", coalesce(var.encryption_key_name, "unset")))
      error_message = "encryption_key_name '${coalesce(var.encryption_key_name, "unset")}' is not a key in ${var.region}. ADR-001 C-1: no resource outside the named in-Kingdom region, and a key held elsewhere is the one that makes the data readable."
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
