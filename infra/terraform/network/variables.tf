variable "project_id" {
  description = "The environment's own GCP project. One project per environment (TASK-016)."
  type        = string
}

variable "region" {
  description = "The named in-Kingdom region. Every resource here is regional (ADR-001 C-1)."
  type        = string
}

variable "network_name" {
  description = "VPC name, taken from infra/environments/environments.json."
  type        = string
}

variable "app_subnet_cidr" {
  description = "Primary range of the application subnet. Cloud Run attaches its Direct VPC egress interface here."
  type        = string
}

variable "proxy_subnet_cidr" {
  description = "Range of the REGIONAL_MANAGED_PROXY subnet the regional external Application Load Balancer runs in. Minimum /26."
  type        = string
}

variable "private_services_cidr" {
  description = "Range reserved for private services access, from which Cloud SQL takes its private IP."
  type        = string
}

variable "private_services_prefix_length" {
  description = "Prefix length of the private services access range. Must match private_services_cidr."
  type        = number
}

variable "database_port" {
  description = "Port the application tier may reach on the database tier. PostgreSQL."
  type        = number
  default     = 5432
}

variable "app_network_tag" {
  description = "Network tag carried by the application tier's VPC interface. The egress rules below are scoped to it."
  type        = string
}

variable "labels" {
  description = "Labels applied to every labelled resource in this module."
  type        = map(string)
}
