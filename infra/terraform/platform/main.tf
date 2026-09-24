# The platform, composed (TASK-017).
#
# One of these per environment, called by infra/terraform/environments/<env>. The four roots differ
# only in their state backend and their sizing: everything structural is here, once, so that
# "DEV is SIT is UAT is PROD, smaller" is a property of the code rather than a claim in a document.
#
# Names are not invented here. infra/environments/environments.json (TASK-016) is the source of
# truth for the project, the network, the database instance, the secret ids and the service
# accounts, and its CI check already proves no identifier is shared between two environments
# (docs/architecture/environment-separation-check.py). This module reads that file.

locals {
  manifest = jsondecode(file("${path.module}/${var.manifest_path}"))
  platform = local.manifest.platform

  environment = one([for e in local.manifest.environments : e if e.name == var.environment])

  # APP_BASE_URL is the whole address of an environment: TASK-016 D-1 puts the SPA and the API on
  # one origin, so this hostname is both. Empty until F-1 closes.
  app_base_url = local.environment.variables.APP_BASE_URL.value
  domain_name  = local.app_base_url == "" ? "" : regex("^https://([^/]+)", local.app_base_url)[0]

  name_prefix = "${local.platform.resource_prefix}-${var.environment}"

  # The tag the firewall rules in ../network are written against and the Cloud Run interface in
  # ../compute carries. One string, two modules, no chance of them disagreeing.
  app_network_tag = "${local.name_prefix}-app"

  labels = {
    environment = var.environment
    tier        = local.environment.tier
    platform    = "pmplatform"
    task        = "task-017"
    managed_by  = "terraform"
  }

  # The address of this environment's secret namespace (TASK-019). It ends in the id prefix, and the
  # application appends a variable's lower-kebab name to it, so this one value carries the store, the
  # project and the namespace without naming a variable the Environment and Secrets sheet does not.
  # Nothing here reads a secret: the values are written by the owners the sheet names, and the
  # application reads them at runtime with its own identity (docs/architecture/secret-management.md §3).
  secret_store_endpoint = "https://secretmanager.googleapis.com/v1/projects/${local.environment.project_id}/secrets/${local.environment.secret_store.prefix}"
}

module "network" {
  source = "../network"

  project_id                     = local.environment.project_id
  region                         = local.platform.region
  network_name                   = local.environment.network
  app_subnet_cidr                = var.app_subnet_cidr
  proxy_subnet_cidr              = var.proxy_subnet_cidr
  private_services_cidr          = var.private_services_cidr
  private_services_prefix_length = var.private_services_prefix_length
  app_network_tag                = local.app_network_tag
  labels                         = local.labels

  depends_on = [terraform_data.preflight]
}

module "database" {
  source = "../database"

  project_id               = local.environment.project_id
  region                   = local.platform.region
  instance_name            = local.environment.database.instance
  database_name            = local.environment.database.database
  database_user            = local.environment.database.user
  network_id               = module.network.network_id
  runtime_service_account  = local.environment.service_accounts.runtime
  tier                     = var.database_tier
  availability_type        = var.database_availability_type
  disk_size_gb             = var.database_disk_size_gb
  disk_autoresize_limit_gb = var.database_disk_autoresize_limit_gb
  retained_backups         = var.retained_backups
  deletion_protection      = var.deletion_protection
  labels                   = local.labels

  # TASK-020. The key is null until AHDA's data classification calls for one (ADR-001 R-4); the
  # export writes to the bucket ../storage already creates for this environment, and DEV has none,
  # so the lookup returns null there rather than a name this file would have had to invent.
  encryption_key_name    = var.database_encryption_key_name
  backup_bucket_name     = lookup(module.storage.bucket_names, "db-backups", null)
  backup_export_schedule = var.backup_export_schedule

  maintenance_window = {
    day  = 6 # Saturday
    hour = 1
  }

  # The instance takes its private IP from the peering, which must exist first. The network_id
  # reference above orders it after the VPC but not after the service networking connection.
  depends_on = [module.network]
}

module "storage" {
  source = "../storage"

  project_id                        = local.environment.project_id
  region                            = local.platform.region
  runtime_service_account           = local.environment.service_accounts.runtime
  create_backup_buckets             = var.environment != "dev"
  noncurrent_version_retention_days = var.noncurrent_version_retention_days
  labels                            = local.labels

  depends_on = [terraform_data.preflight]
}

module "compute" {
  source = "../compute"

  project_id              = local.environment.project_id
  region                  = local.platform.region
  service_name            = "${local.name_prefix}-api"
  container_image         = var.container_image
  runtime_service_account = local.environment.service_accounts.runtime
  network_id              = module.network.network_id
  subnet_id               = module.network.app_subnet_id
  network_tag             = local.app_network_tag
  cpu                     = var.compute_cpu
  memory                  = var.compute_memory
  min_instances           = var.compute_min_instances
  max_instances           = var.compute_max_instances
  deletion_protection     = var.deletion_protection
  labels                  = local.labels

  # Only variables the Environment and Secrets sheet names. The sheet has no row for the document
  # bucket or the Cloud SQL connection name; both are carried inside the connection strings
  # TASK-019 writes, so nothing is invented here (infrastructure-as-code.md F-5).
  plain_environment = {
    APP_BASE_URL          = local.app_base_url
    SECRET_STORE_ENDPOINT = local.secret_store_endpoint
  }
}

module "load_balancer" {
  source = "../load-balancer"

  project_id             = local.environment.project_id
  region                 = local.platform.region
  name_prefix            = local.name_prefix
  network_id             = module.network.network_id
  cloud_run_service_name = module.compute.service_name
  domain_name            = local.domain_name
  waf_preview            = var.waf_preview
  labels                 = local.labels

  # The proxies run in the proxy-only subnet; a forwarding rule created before it exists fails.
  depends_on = [module.network]
}
