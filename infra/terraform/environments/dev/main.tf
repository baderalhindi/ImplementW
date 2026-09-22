# DEV (TASK-017).
#
# The root is deliberately thin: a state backend, a provider and one call. Everything structural
# lives in ../../platform and is identical in all four environments; everything that differs is in
# terraform.tfvars beside this file, where a reviewer can diff two environments in one screen.
#
#   terraform init
#   terraform plan  -var="container_image=$IMAGE"
#   terraform apply -var="container_image=$IMAGE"
#
# The image is not committed. One artifact is built once and promoted unchanged through the four
# environments (TASK-018), so its digest is a property of the release, not of the environment.

terraform {
  required_version = ">= 1.13"
  required_providers {
    google = {
      source  = "hashicorp/google"
      version = "~> 8.4"
    }
  }
}

# Every resource sets its own project and region, both read from the manifest, so there is no
# provider-level default that a module could silently inherit and no second place the region is
# written down.
provider "google" {}

module "platform" {
  source      = "../../platform"
  environment = "dev"

  container_image = var.container_image

  app_subnet_cidr                = var.app_subnet_cidr
  proxy_subnet_cidr              = var.proxy_subnet_cidr
  private_services_cidr          = var.private_services_cidr
  private_services_prefix_length = var.private_services_prefix_length

  database_tier                     = var.database_tier
  database_availability_type        = var.database_availability_type
  database_disk_size_gb             = var.database_disk_size_gb
  database_disk_autoresize_limit_gb = var.database_disk_autoresize_limit_gb
  retained_backups                  = var.retained_backups

  noncurrent_version_retention_days = var.noncurrent_version_retention_days

  compute_cpu           = var.compute_cpu
  compute_memory        = var.compute_memory
  compute_min_instances = var.compute_min_instances
  compute_max_instances = var.compute_max_instances

  waf_preview         = var.waf_preview
  deletion_protection = var.deletion_protection
}
