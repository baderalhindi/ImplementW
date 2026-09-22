output "document_bucket_name" {
  description = "Private object storage backing WF-12 documents (DOCUMENT_STORAGE_CONNECTION_STRING)."
  value       = google_storage_bucket.all["documents"].name
}

output "bucket_names" {
  description = "Every bucket this module created, by role."
  value       = { for role, bucket in google_storage_bucket.all : role => bucket.name }
}
