# The root's inputs. Each one is described once, in ../../platform/variables.tf, and set once, in
# terraform.tfvars beside this file; repeating the descriptions here would be a second copy to keep
# in step. The root declares them only so the tfvars file has somewhere to land.

variable "container_image" {
  description = "The image to deploy, by digest. Supplied by the promotion pipeline as TF_VAR_container_image; never committed."
  type        = string
}

variable "app_subnet_cidr" {
  type = string
}

variable "proxy_subnet_cidr" {
  type = string
}

variable "private_services_cidr" {
  type = string
}

variable "private_services_prefix_length" {
  type = number
}

variable "database_tier" {
  type = string
}

variable "database_availability_type" {
  type = string
}

variable "database_disk_size_gb" {
  type = number
}

variable "database_disk_autoresize_limit_gb" {
  type = number
}

variable "retained_backups" {
  type    = number
  default = null
}

variable "noncurrent_version_retention_days" {
  type    = number
  default = null
}

variable "compute_cpu" {
  type = string
}

variable "compute_memory" {
  type = string
}

variable "compute_min_instances" {
  type = number
}

variable "compute_max_instances" {
  type = number
}

variable "waf_preview" {
  type    = bool
  default = true
}

variable "deletion_protection" {
  type = bool
}
