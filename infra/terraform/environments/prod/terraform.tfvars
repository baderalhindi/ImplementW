# PROD sizing and posture. Nothing structural is here; structure is ../../platform.

# Address plan. The four VPCs are not peered and carry no route to one another, so these ranges
# need not differ — they do so that a future connection to an AHDA network needs no renumbering.
app_subnet_cidr                = "10.40.0.0/20"
proxy_subnet_cidr              = "10.40.16.0/24"
private_services_cidr          = "10.40.128.0/20"
private_services_prefix_length = 20

database_tier                     = "db-custom-4-15360"
database_availability_type        = "REGIONAL"
database_disk_autoresize_limit_gb = 500
database_disk_size_gb             = 100

# retained_backups and noncurrent_version_retention_days are left unset on purpose. The backup
# retention period is OQ-003 / PTBC-048, open with AHDA Cybersecurity and Records; TASK-020's own
# gate cell says to build the mechanism and leave the period configurable. PROD cannot be applied
# until the approved value is set (platform/guards.tf).

# database_encryption_key_name is unset, so the disk, the automated backups and the exports are
# encrypted with Google-managed keys. Whether the key has to be AHDA's own follows the data
# classification in ADR-001 R-4, which is unanswered; nothing here invents a key ring or a rotation
# period (infrastructure-as-code.md F-6).

# The off-instance copy (TASK-020). Automated backups and the point-in-time recovery log live with
# the instance and are deleted with it; this export is what outlives it, and TASK-023's drill
# restores from it. 03:00 UTC clears both the 22:00 backup window and the Saturday 01:00
# maintenance window. Each run lands as a new version of one object, so how many copies are kept
# is noncurrent_version_retention_days above — the same OQ-003 period, configured in one place.
backup_export_schedule = "0 3 * * *"

compute_cpu           = "2"
compute_memory        = "4Gi"
compute_min_instances = 2
compute_max_instances = 10

# The WAF enforces here as in every environment: the sensitivity-1 OWASP baseline blocks from DEV
# onward, so a false positive is found in testing and not on PROD's first day (control matrix G-7,
# docs/architecture/network-security.md §3.4).
waf_preview = false

deletion_protection = true
