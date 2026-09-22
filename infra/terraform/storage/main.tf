# Object storage (TASK-017 authors the module; TASK-037 consumes the document bucket, TASK-023 the
# backup targets).
#
# Three buckets, one per row the Environment and Secrets sheet names: WF-12 documents, the database
# backup target and the document-store backup target. Each is single-region in the named region,
# private, versioned and closed to public access — ADR-001 C-4 and CTL-20.
#
# Cloud SQL's automated backups and PITR (see ../database) are managed by the service and do not
# land in a bucket. The database backup bucket is the target for exports and long-retention copies,
# which is what TASK-023's restore drill and DB_BACKUP_STORAGE_CONNECTION_STRING refer to.

locals {
  # The sheet scopes both backup rows to SIT, UAT and PROD. DEV gets documents only.
  roles = concat(["documents"], var.create_backup_buckets ? ["db-backups", "document-backups"] : [])
}

resource "google_storage_bucket" "all" {
  for_each = toset(local.roles)

  project  = var.project_id
  name     = "${var.project_id}-${each.key}"
  location = var.region

  # A bucket holding evidence for an audited workflow is not a bucket to empty by accident.
  force_destroy = false

  storage_class               = "STANDARD"
  uniform_bucket_level_access = true
  public_access_prevention    = "enforced"

  versioning {
    enabled = true
  }

  soft_delete_policy {
    retention_duration_seconds = var.soft_delete_retention_days * 24 * 60 * 60
  }

  # No rule at all while the retention period is open (OQ-003), so nothing is deleted on a period
  # nobody approved. DocumentVersions are immutable and unlink never deletes (CTL-20), so the only
  # thing this would ever remove is a superseded object version.
  dynamic "lifecycle_rule" {
    for_each = var.noncurrent_version_retention_days == null ? [] : [var.noncurrent_version_retention_days]
    content {
      condition {
        with_state                 = "ARCHIVED"
        days_since_noncurrent_time = lifecycle_rule.value
      }
      action {
        type = "Delete"
      }
    }
  }

  labels = merge(var.labels, { role = each.key })
}

# The runtime reads and writes documents. It has no grant on either backup bucket: a backup the
# application can overwrite is not a backup.
resource "google_storage_bucket_iam_member" "runtime_documents" {
  bucket = google_storage_bucket.all["documents"].name
  role   = "roles/storage.objectUser"
  member = "serviceAccount:${var.runtime_service_account}"
}
