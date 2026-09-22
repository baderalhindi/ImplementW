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

compute_cpu           = "1"
compute_memory        = "1Gi"
compute_min_instances = 0
compute_max_instances = 2

# The WAF logs rather than blocks until TASK-021 has tuned the rule sets against real traffic
# (control matrix G-7).
waf_preview = true

deletion_protection = false
