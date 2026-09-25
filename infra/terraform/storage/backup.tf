# Off-bucket copy of WF-12 documents (TASK-023).
#
# The documents bucket is versioned with a soft-delete window, so an overwritten or deleted object
# is recoverable inside the bucket itself with no copy at all. What that does not answer is the
# bucket, or its objects wholesale, being deleted. The Environment and Secrets sheet names
# DOCUMENT_STORAGE_BACKUP_CONNECTION_STRING for the copy that does, and main.tf creates the
# document-backups bucket it addresses; until this file nothing wrote to it.
#
# The mechanism is a Storage Transfer Service job, hourly, documents -> document-backups. Hourly
# because RPO 1 hour (PTBC-048) is the platform's objective and 3600s is the shortest interval the
# service schedules. It copies new objects and never deletes or overwrites in the sink, so a
# document deleted at the source is still in the backup. The backup bucket is versioned too, and
# its retention is the same null lifecycle rule as every other bucket while OQ-003 is open.
#
# DEV has no backup bucket (the sheet scopes the row to SIT, UAT and PROD), so it has no job.

# The Storage Transfer service agent is named after the project number.
data "google_project" "this" {
  project_id = var.project_id
}

locals {
  document_backup_enabled = var.create_backup_buckets ? 1 : 0
  transfer_service_agent  = "serviceAccount:project-${data.google_project.this.number}@storage-transfer-service.iam.gserviceaccount.com"
}

# Source: read and list. Nothing more — the job must not be able to change a document.
resource "google_storage_bucket_iam_member" "transfer_documents_reader" {
  for_each = local.document_backup_enabled == 1 ? toset(["roles/storage.objectViewer", "roles/storage.legacyBucketReader"]) : toset([])

  bucket = google_storage_bucket.all["documents"].name
  role   = each.value
  member = local.transfer_service_agent
}

# Sink: list and create, and not delete. The job never overwrites a name already in the sink, so it
# needs no delete permission: DocumentVersions are immutable (CTL-20), a name is written once, and
# the first copy of it is the one kept. Nothing the source does can remove or replace a backup.
resource "google_storage_bucket_iam_member" "transfer_backup_writer" {
  for_each = local.document_backup_enabled == 1 ? toset(["roles/storage.objectCreator", "roles/storage.legacyBucketReader"]) : toset([])

  bucket = google_storage_bucket.all["document-backups"].name
  role   = each.value
  member = local.transfer_service_agent
}

resource "google_storage_transfer_job" "document_backup" {
  count = local.document_backup_enabled

  project     = var.project_id
  description = "Hourly copy of ${google_storage_bucket.all["documents"].name} to ${google_storage_bucket.all["document-backups"].name} (TASK-023, CTL-36)."

  transfer_spec {
    gcs_data_source {
      bucket_name = google_storage_bucket.all["documents"].name
    }
    gcs_data_sink {
      bucket_name = google_storage_bucket.all["document-backups"].name
    }
    transfer_options {
      delete_objects_unique_in_sink              = false
      delete_objects_from_source_after_transfer  = false
      overwrite_objects_already_existing_in_sink = false
    }
  }

  # A fixed start date rather than a computed one, so the plan does not change from run to run
  # (terraform-check.py T-9). A date in the past starts the job at its next interval.
  schedule {
    schedule_start_date {
      year  = 2026
      month = 9
      day   = 25
    }
    start_time_of_day {
      hours   = 0
      minutes = 15
      seconds = 0
      nanos   = 0
    }
    repeat_interval = "3600s"
  }

  depends_on = [
    google_storage_bucket_iam_member.transfer_documents_reader,
    google_storage_bucket_iam_member.transfer_backup_writer,
  ]
}
