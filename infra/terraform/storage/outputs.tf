output "document_bucket_name" {
  description = "Private object storage backing WF-12 documents (DOCUMENT_STORAGE_CONNECTION_STRING)."
  value       = google_storage_bucket.all["documents"].name
}

output "bucket_names" {
  description = "Every bucket this module created, by role."
  value       = { for role, bucket in google_storage_bucket.all : role => bucket.name }
}

output "document_backup_job_name" {
  description = "The hourly documents -> document-backups transfer job (TASK-023). Null where the environment has no backup bucket. Its run history is the document-store half of the backup evidence the runbook reads."
  value       = one(google_storage_transfer_job.document_backup[*].name)
}
