variable "project_id" {
  description = "The environment's own GCP project."
  type        = string
}

variable "region" {
  description = "The named in-Kingdom region."
  type        = string
}

variable "service_name" {
  description = "Cloud Run service name."
  type        = string
}

variable "container_image" {
  description = "The image to run, by digest. Supplied at apply time by the promotion pipeline (TASK-018) as TF_VAR_container_image, because one artifact is built once and promoted unchanged through all four environments."
  type        = string

  validation {
    condition     = can(regex("@sha256:[0-9a-f]{64}$", var.container_image)) || can(regex(":[A-Za-z0-9._-]+$", var.container_image))
    error_message = "container_image must carry a digest or a tag. A bare repository name would deploy whatever :latest happens to be."
  }
}

variable "runtime_service_account" {
  description = "The environment's runtime service account (TASK-016). It exists only in this project, so a credential names the environment it belongs to."
  type        = string
}

variable "network_id" {
  description = "VPC the Direct VPC egress interface attaches to."
  type        = string
}

variable "subnet_id" {
  description = "Application subnet the Direct VPC egress interface takes its addresses from."
  type        = string
}

variable "network_tag" {
  description = "Network tag on that interface. The firewall rules in ../network are scoped to it."
  type        = string
}

variable "cpu" {
  description = "CPU limit per instance."
  type        = string
}

variable "memory" {
  description = "Memory limit per instance."
  type        = string
}

variable "min_instances" {
  description = "Instances kept warm. Zero is right for DEV and wrong for PROD, where a cold start lands on a user."
  type        = number
}

variable "max_instances" {
  description = "Scaling ceiling. It is also the database connection ceiling: max_instances times the pool size must stay under the instance's connection limit."
  type        = number
}

variable "request_concurrency" {
  description = "Concurrent requests per instance."
  type        = number
  default     = 80
}

variable "container_port" {
  description = "Port the ASP.NET Core container listens on."
  type        = number
  default     = 8080
}

variable "secret_environment" {
  description = "Environment variables read from Secret Manager at start-up, as variable name to secret id. The containers are created empty by TASK-016 and the values are written under TASK-019; an empty secret fails the container at boot, which is the stated behaviour."
  type        = map(string)
}

variable "plain_environment" {
  description = "Environment variables that are configuration, not secrets."
  type        = map(string)
}

variable "deletion_protection" {
  description = "Whether terraform destroy and the API may delete the service."
  type        = bool
}

variable "labels" {
  description = "Labels applied to the service."
  type        = map(string)
}
