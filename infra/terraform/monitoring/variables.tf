variable "project_id" {
  description = "The environment's own GCP project. The check, its results and the alert policy live here."
  type        = string
}

variable "environment" {
  description = "dev, sit, uat or prod — for the alert's subject line."
  type        = string
}

variable "name_prefix" {
  description = "Resource name prefix, ahda-pmplatform-<env>."
  type        = string
}

variable "host" {
  description = "The environment's hostname, from APP_BASE_URL. The check goes through the load balancer, as a user does."
  type        = string

  validation {
    condition     = var.host != ""
    error_message = "host is empty: APP_BASE_URL has no value for this environment (environment-separation.md F-1)."
  }
}

variable "notification_channel" {
  description = "The on-call channel's resource name, projects/<project>/notificationChannels/<id>. route-alerts.sh creates the channel from ALERT_CHANNEL_WEBHOOK_URL and prints this name; the pipeline supplies it as TF_VAR_alert_notification_channel."
  type        = string

  validation {
    condition     = can(regex("^projects/[^/]+/notificationChannels/[0-9]+$", var.notification_channel))
    error_message = "notification_channel is not a channel's resource name. Run infra/environments/route-alerts.sh <env> as the secret's owner first; it prints the name."
  }
}

variable "path" {
  description = "The readiness endpoint (PMPlatform.Api/HealthChecks/HealthEndpoints.cs): 503 when the database does not answer."
  type        = string
  default     = "/health/ready"
}

variable "check_period_seconds" {
  description = "How often each checker region requests the path. 60 is Cloud Monitoring's shortest."
  type        = number
  default     = 60

  validation {
    condition     = contains([60, 300, 600, 900], var.check_period_seconds)
    error_message = "check_period_seconds is one of Cloud Monitoring's periods: 60, 300, 600 or 900."
  }
}

variable "check_timeout_seconds" {
  description = "How long a checker waits for the answer. Above the API's 3-second database probe, so a slow database is reported as 503 rather than as a timeout."
  type        = number
  default     = 10

  validation {
    condition     = var.check_timeout_seconds > 3 && var.check_timeout_seconds <= 60
    error_message = "check_timeout_seconds must exceed the API's 3-second database probe and be at most 60."
  }
}

variable "checker_regions" {
  description = "Cloud Monitoring checker regions; at least three. None is in the Kingdom (D-6). Europe and Asia-Pacific are the nearest."
  type        = list(string)
  default     = ["EUROPE", "ASIA_PACIFIC", "USA_VIRGINIA"]

  validation {
    condition     = length(var.checker_regions) >= 3
    error_message = "Cloud Monitoring requires at least three checker regions."
  }
}

variable "failing_regions" {
  description = "How many checker regions must see the failure before the alert fires."
  type        = number
  default     = 2
}

variable "failure_duration_seconds" {
  description = "How long the failure must last before the alert fires: one more check period, so a single lost request does not page anyone."
  type        = number
  default     = 60
}

variable "labels" {
  description = "Labels every resource carries."
  type        = map(string)
}
