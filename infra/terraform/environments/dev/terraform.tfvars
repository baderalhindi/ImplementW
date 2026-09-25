# DEV sizing and posture. Nothing structural is here; structure is ../../platform.

# Address plan. The four VPCs are not peered and carry no route to one another, so these ranges
# need not differ — they do so that a future connection to an AHDA network needs no renumbering.
app_subnet_cidr                = "10.10.0.0/20"
proxy_subnet_cidr              = "10.10.16.0/24"
private_services_cidr          = "10.10.128.0/20"
private_services_prefix_length = 20

database_tier                     = "db-custom-1-3840"
database_availability_type        = "ZONAL"
database_disk_autoresize_limit_gb = 100
database_disk_size_gb             = 20

# retained_backups and noncurrent_version_retention_days are left unset on purpose. The backup
# retention period is OQ-003 / PTBC-048, open with AHDA Cybersecurity and Records; TASK-020's own
# gate cell says to build the mechanism and leave the period configurable. PROD cannot be applied
# until the approved value is set (platform/guards.tf).

# database_encryption_key_name is unset, so the disk, the automated backups and the exports are
# encrypted with Google-managed keys. Whether the key has to be AHDA's own follows the data
# classification in ADR-001 R-4, which is unanswered; nothing here invents a key ring or a rotation
# period (infrastructure-as-code.md F-6).

# No backup_export_schedule. The Environment and Secrets sheet scopes
# DB_BACKUP_STORAGE_CONNECTION_STRING to SIT, UAT and PROD, so DEV has no backup bucket to export
# to and the module refuses a schedule without one.

compute_cpu           = "1"
compute_memory        = "1Gi"
compute_min_instances = 0
compute_max_instances = 2

# The WAF enforces here as in every environment: the sensitivity-1 OWASP baseline blocks from DEV
# onward, so a false positive is found in testing and not on PROD's first day (control matrix G-7,
# docs/architecture/network-security.md §3.4).
waf_preview = false

deletion_protection = false
