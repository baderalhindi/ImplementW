# Uptime monitoring and alerting (TASK-091, CTL-31).
#
# Each environment's address is checked from outside, through the same path a user takes — DNS, the
# certificate, the load balancer and its WAF, Cloud Run, and the database behind the API — by
# requesting /health/ready, which answers 503 the moment the database does not. Cloud Monitoring is
# in the environment's own project, so the check, its results and the alert are AHDA's, under the
# same tenancy and IAM as everything else here; no other processor is added (CTL-50). The checkers
# themselves run from Google's fixed checker regions, none of which is in the Kingdom: they send one
# anonymous GET and receive "Healthy" or "Unhealthy", and nothing else crosses
# (docs/architecture/health-checks-uptime-monitoring.md D-6).
#
# The alert goes to the environment's on-call channel, a webhook whose URL is a secret
# (ALERT_CHANNEL_WEBHOOK_URL). Nothing in infra/terraform reads a secret (secret-management.md §3), and
# a notification channel declared here, or looked up as a data source, would put the URL in the state
# file. So the channel is created and kept by the secret's owner with
# infra/environments/route-alerts.sh, which reads the URL from the environment's secret store, and this
# module is given only the channel's resource name — supplied at apply time by the pipeline, like the
# container image, and never committed.

resource "google_monitoring_uptime_check_config" "readiness" {
  project      = var.project_id
  display_name = "${var.name_prefix} readiness"
  period       = "${var.check_period_seconds}s"
  timeout      = "${var.check_timeout_seconds}s"

  selected_regions = var.checker_regions
  checker_type     = "STATIC_IP_CHECKERS"

  http_check {
    request_method = "GET"
    path           = var.path
    port           = 443
    use_ssl        = true
    validate_ssl   = true

    accepted_response_status_codes {
      status_value = 200
    }
  }

  monitored_resource {
    type = "uptime_url"
    labels = {
      project_id = var.project_id
      host       = var.host
    }
  }

  user_labels = var.labels
}

# Fires when the check fails from at least var.failing_regions of the checker regions and keeps
# failing for var.failure_duration_seconds. One region alone does not page anyone: a checker's own
# network fault looks exactly like an outage from that one place. ALIGN_NEXT_OLDER with
# REDUCE_COUNT_FALSE counts the regions whose latest result is a failure.
resource "google_monitoring_alert_policy" "readiness" {
  project      = var.project_id
  display_name = "${var.name_prefix} readiness failing"
  combiner     = "OR"
  severity     = "CRITICAL"
  enabled      = true

  conditions {
    display_name = "${var.host}${var.path} failing from ${var.failing_regions} or more regions"

    condition_threshold {
      filter          = "metric.type=\"monitoring.googleapis.com/uptime_check/check_passed\" AND metric.label.check_id=\"${google_monitoring_uptime_check_config.readiness.uptime_check_id}\" AND resource.type=\"uptime_url\""
      comparison      = "COMPARISON_GT"
      threshold_value = var.failing_regions - 1
      duration        = "${var.failure_duration_seconds}s"

      aggregations {
        alignment_period     = "1200s"
        per_series_aligner   = "ALIGN_NEXT_OLDER"
        cross_series_reducer = "REDUCE_COUNT_FALSE"
        group_by_fields      = ["resource.label.*"]
      }

      trigger {
        count = 1
      }
    }
  }

  alert_strategy {
    auto_close = "1800s"
  }

  documentation {
    mime_type = "text/markdown"
    subject   = "PMPlatform ${var.environment}: ${var.path} failing"
    content   = <<-EOT
      PMPlatform **${var.environment}** answers `${var.path}` with something other than 200 from at least ${var.failing_regions} of the uptime checker regions.

      `/health/ready` is 503 when the API cannot reach its database within 3 seconds; no answer at all means the load balancer, the certificate or the service itself. The API's log names the failing check (`Health check postgresql`).

      Runbook: docs/architecture/health-checks-uptime-monitoring.md §7.
    EOT
  }

  notification_channels = [var.notification_channel]

  user_labels = var.labels
}
