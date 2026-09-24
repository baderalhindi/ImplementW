# Off-instance backup copies (TASK-020).
#
# Cloud SQL's automated backups and transaction logs live with the instance and are deleted with
# it. That is enough for the failure the RPO describes — a bad deployment, a dropped table, a zone
# going away — and not enough for the one the backup bucket exists for: the instance, or the
# project, being deleted. DB_BACKUP_STORAGE_CONNECTION_STRING in the Environment and Secrets sheet
# names a target for exactly that copy, and until something writes to it the row names nothing.
#
# The mechanism is a scheduled export of the application database to the environment's db-backups
# bucket (../storage, ADR-001 C-4: single-region, in the named region). The object name is fixed,
# so each run lands as a new version of one object in a versioned bucket, and the chain's length is
# the bucket's own non-current-version lifecycle rule — which is null while OQ-003 is open. That is
# what "build the mechanism, leave the period configurable" means here: the frequency is this
# schedule, the retention is that rule, and neither carries a value nobody approved.
#
# DEV has no backup bucket (the sheet scopes the row to SIT, UAT and PROD), so it has no export.

locals {
  backup_export_enabled = var.backup_export_schedule == null ? 0 : 1

  # One object, many versions. A timestamped name would need something that can compute a timestamp
  # at run time; a fixed name plus bucket versioning gives the same chain with nothing to run.
  backup_export_uri = var.backup_bucket_name == null ? null : "gs://${var.backup_bucket_name}/${var.instance_name}/${var.database_name}.sql.gz"
}

# The instance writes the export itself, as its own service agent. objectCreator can add an object
# and cannot delete or overwrite one: an export that could remove an earlier export would be a
# backup the backup mechanism can destroy. objectViewer is what a restore reads it back with.
resource "google_storage_bucket_iam_member" "instance_backup_writer" {
  count = var.backup_bucket_name == null ? 0 : 1

  bucket = var.backup_bucket_name
  role   = "roles/storage.objectCreator"
  member = "serviceAccount:${google_sql_database_instance.main.service_account_email_address}"
}

resource "google_storage_bucket_iam_member" "instance_backup_reader" {
  count = var.backup_bucket_name == null ? 0 : 1

  bucket = var.backup_bucket_name
  role   = "roles/storage.objectViewer"
  member = "serviceAccount:${google_sql_database_instance.main.service_account_email_address}"
}

# The scheduler's own identity, separate from the runtime service account on purpose: the
# application must not be able to export the database it serves (CTL-02, least privilege).
resource "google_service_account" "backup_export" {
  count = local.backup_export_enabled

  project      = var.project_id
  account_id   = "${var.instance_name}-backup"
  display_name = "PMPlatform ${var.labels.environment} database export (TASK-020)"
}

# roles/cloudsql.editor would do this and also restart, patch and restore the instance. Two
# permissions are what an export needs, so two permissions are what it gets.
resource "google_project_iam_custom_role" "backup_export" {
  count = local.backup_export_enabled

  project     = var.project_id
  role_id     = "pmplatformBackupExporter"
  title       = "PMPlatform backup exporter"
  description = "Export a Cloud SQL database to the environment's backup bucket, and nothing else (TASK-020)."
  permissions = ["cloudsql.instances.export", "cloudsql.instances.get"]
}

resource "google_project_iam_member" "backup_export" {
  count = local.backup_export_enabled

  project = var.project_id
  role    = google_project_iam_custom_role.backup_export[0].id
  member  = "serviceAccount:${google_service_account.backup_export[0].email}"
}

# Cloud Scheduler mints the OAuth token the call below carries, which it can only do if its own
# service agent may create tokens for that account.
resource "google_service_account_iam_member" "scheduler_token_creator" {
  count = local.backup_export_enabled

  service_account_id = google_service_account.backup_export[0].name
  role               = "roles/iam.serviceAccountTokenCreator"
  member             = "serviceAccount:service-${data.google_project.this.number}@gcp-sa-cloudscheduler.iam.gserviceaccount.com"
}

resource "google_cloud_scheduler_job" "backup_export" {
  count = local.backup_export_enabled

  project     = var.project_id
  region      = var.region
  name        = "${var.instance_name}-backup-export"
  description = "Export ${var.database_name} to ${local.backup_export_uri} (TASK-020, CTL-35)."
  schedule    = var.backup_export_schedule

  # Every other time value in this repository — the backup window, the maintenance window, the
  # schedule above — is UTC, so an operator reading two of them side by side is comparing like
  # with like and no one has to know which of them meant local time.
  time_zone = "Etc/UTC"

  # An export of a database this size is minutes, not hours; the call only starts the operation.
  attempt_deadline = "600s"

  retry_config {
    retry_count = 3
  }

  http_target {
    http_method = "POST"
    uri         = "https://sqladmin.googleapis.com/v1/projects/${var.project_id}/instances/${var.instance_name}/export"
    headers     = { "Content-Type" = "application/json" }

    # offload runs the export on a temporary instance rather than the primary, so a nightly export
    # cannot slow the service that is serving traffic.
    body = base64encode(jsonencode({
      exportContext = {
        fileType  = "SQL"
        uri       = local.backup_export_uri
        databases = [var.database_name]
        offload   = true
      }
    }))

    oauth_token {
      service_account_email = google_service_account.backup_export[0].email
      scope                 = "https://www.googleapis.com/auth/cloud-platform"
    }
  }

  depends_on = [
    google_sql_database.app,
    google_storage_bucket_iam_member.instance_backup_writer,
    google_service_account_iam_member.scheduler_token_creator,
  ]

  lifecycle {
    precondition {
      condition     = var.backup_bucket_name != null
      error_message = "backup_export_schedule is set for ${var.instance_name} but backup_bucket_name is null, so the export has nowhere to write. The Environment and Secrets sheet scopes DB_BACKUP_STORAGE_CONNECTION_STRING to SIT, UAT and PROD; DEV has no backup bucket and no export."
    }
  }
}
