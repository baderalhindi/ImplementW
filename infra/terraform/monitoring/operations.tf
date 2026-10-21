# Operational dashboard and the on-call route of FG-05's operational alerts (TASK-092, CTL-32).
#
# Two dashboards serve operations (docs/operations/operational-dashboards.md). OD-1 is in the
# application: ADM-047 Integration Monitoring shows every registered integration's availability,
# freshness, queue depth and dead letters from FG-05's own records, which only the application can
# read. OD-2 is this one, in the environment's Cloud Monitoring: the API's traffic, latency and error
# rate from Cloud Run's built-in metrics, readiness from the TASK-091 uptime check, the database, and
# the operational alerts the API raised. Its definition is docs/operations/dashboards, so the
# document and the dashboard cannot drift apart.
#
# An operational alert reaches its configured recipients through WF-15 (in-app and e-mail, FG-05
# §10.3). The on-call channel is told as well: the API logs one fixed line for each alert raised
# ("Operational alert raised: <TYPE> <SEVERITY> on <INTEGRATION> (alert <id>)."), which the
# log-based metric below counts, and the policy below sends to the channel that holds
# ALERT_CHANNEL_WEBHOOK_URL. FG-05 alerts are deduplicated per integration and type, so this is one
# notification per condition, not one per failed call.

resource "google_logging_metric" "operational_alerts" {
  project     = var.project_id
  name        = "${var.name_prefix}-operational-alerts-raised"
  description = "FG-05 operational alerts raised by the API (TASK-092): one per alert, labelled with its type, severity and integration."
  filter      = "resource.type=\"cloud_run_revision\" AND resource.labels.service_name=\"${var.service_name}\" AND textPayload:\"Operational alert raised: \""

  metric_descriptor {
    metric_kind = "DELTA"
    value_type  = "INT64"
    unit        = "1"

    labels {
      key         = "alert_type"
      value_type  = "STRING"
      description = "AVAILABILITY, FRESHNESS, RECONCILIATION_MISMATCH, DEAD_LETTER, DELIVERY_RATE or QUEUE_BACKLOG."
    }

    labels {
      key         = "severity"
      value_type  = "STRING"
      description = "CRITICAL or WARNING (FG-05's fixed severities until TBC-INT-17)."
    }

    labels {
      key         = "integration"
      value_type  = "STRING"
      description = "The integration definition's code, e.g. DIRECTORY or EXCHANGE_EMAIL."
    }
  }

  label_extractors = {
    alert_type  = "REGEXP_EXTRACT(textPayload, \"Operational alert raised: ([A-Z_]+) \")"
    severity    = "REGEXP_EXTRACT(textPayload, \"Operational alert raised: [A-Z_]+ ([A-Z]+) \")"
    integration = "REGEXP_EXTRACT(textPayload, \" on ([A-Z0-9_]+) \")"
  }
}

# Fires for each integration and alert type that raised an alert in the last minute. The FG-05 pass
# runs every 30 s and a raised alert is logged at once, so configuration adds at most 60 s (the
# alignment period) to the route; the in-app and e-mail route is WF-15's (alert-thresholds.json,
# routingWindow).
resource "google_monitoring_alert_policy" "operational_alert" {
  project      = var.project_id
  display_name = "${var.name_prefix} operational alert raised"
  combiner     = "OR"
  severity     = "WARNING"
  enabled      = true

  conditions {
    display_name = "An FG-05 operational alert was raised"

    condition_threshold {
      filter          = "metric.type=\"logging.googleapis.com/user/${google_logging_metric.operational_alerts.name}\" AND resource.type=\"cloud_run_revision\""
      comparison      = "COMPARISON_GT"
      threshold_value = 0
      duration        = "0s"

      aggregations {
        alignment_period     = "60s"
        per_series_aligner   = "ALIGN_SUM"
        cross_series_reducer = "REDUCE_SUM"
        group_by_fields      = ["metric.label.alert_type", "metric.label.severity", "metric.label.integration"]
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
    subject   = "PMPlatform ${var.environment}: $${metric.label.severity} $${metric.label.alert_type} on $${metric.label.integration}"
    content   = <<-EOT
      PMPlatform **${var.environment}** raised an FG-05 operational alert: **$${metric.label.alert_type}** ($${metric.label.severity}) on integration **$${metric.label.integration}**.

      The alert, its detail and its handling (acknowledge, resolve with a note) are on ADM-047 Integration Monitoring, `/admin/integration-monitoring`; its configured recipients have it in-app and by e-mail from WF-15. Raising an alert changed no business record (TASK-075 D-10).

      Runbook: docs/operations/operational-dashboards.md §7.
    EOT
  }

  notification_channels = [var.notification_channel]

  user_labels = var.labels
}

resource "google_monitoring_dashboard" "platform_service" {
  project = var.project_id
  dashboard_json = templatefile("${path.module}/../../../docs/operations/dashboards/platform-service.json.tftpl", {
    environment              = var.environment
    project_id               = var.project_id
    service_name             = var.service_name
    database_instance        = var.database_instance
    uptime_check_id          = google_monitoring_uptime_check_config.readiness.uptime_check_id
    operational_alert_metric = google_logging_metric.operational_alerts.name
  })
}
