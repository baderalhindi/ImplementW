#!/usr/bin/env sh
# The on-call channel the environment's uptime alert notifies (TASK-091, CTL-31).
#
#   infra/environments/route-alerts.sh sit             # the secret's owner: create or update the channel
#   infra/environments/route-alerts.sh sit --dry-run   # print the calls, read nothing
#   infra/environments/route-alerts.sh sit --name      # the pipeline: print the channel's resource name
#
# The channel is a Cloud Monitoring webhook whose URL is ALERT_CHANNEL_WEBHOOK_URL. It is kept here,
# not in Terraform, because nothing in infra/terraform reads a secret (secret-management.md §3): a
# channel resource or data source would copy the URL into the state file. Terraform's alert policy
# is given only the channel's name, which the deploy action reads with --name and passes as
# TF_VAR_alert_notification_channel.
#
# The owner (DevOps/Platform Lead) runs it once the secret has its first version, and again after
# rotating it: the channel keeps its name and takes the new URL, so nothing else changes. The URL is
# read from the store into a variable and reaches gcloud on stdin, never as an argument and never on
# disk (infra/secrets/lib.sh). --name reads no secret.
#
# Exit 0 with nothing printed for an environment the Environment and Secrets sheet gives no on-call
# channel (DEV).
#
# Requires: gcloud with the beta component, jq. Record: docs/architecture/health-checks-uptime-monitoring.md §7
set -eu

here=$(cd "$(dirname "$0")" && pwd)
. "$here/lib/common.sh"

require_tools gcloud jq
require_known_environment "${1:-}"
environment=$1
shift
parse_dry_run "$@"
name_only=0
for arg in "$@"; do
  case $arg in
    --name) name_only=1 ;;
    --dry-run) ;;
    *) die "unknown option '$arg'" ;;
  esac
done

secret=$(env_get "$environment" '.variables.ALERT_CHANNEL_WEBHOOK_URL.secret_id')
if [ -z "$secret" ]; then
  echo "$environment has no ALERT_CHANNEL_WEBHOOK_URL in $MANIFEST, so no on-call channel and no uptime alert." >&2
  exit 0
fi

project=$(env_get "$environment" '.project_id')
display_name="pmplatform-$environment-on-call"

# existing — the channel's resource name, or nothing if there is none. A failed listing stops the run:
# under set -e an assignment from a failing command substitution would otherwise exit without a word.
existing() {
  gcloud beta monitoring channels list --project="$project" \
    --filter="displayName=\"$display_name\" AND type=\"webhook_tokenauth\"" --format='value(name)' ||
    die "could not list the notification channels of $project with this identity"
}

if [ "$name_only" = "1" ]; then
  [ "$DRY_RUN" = "1" ] && { echo_command gcloud beta monitoring channels list --project="$project" --filter="displayName=\"$display_name\""; exit 0; }
  channel=$(existing) || exit 1
  [ -n "$channel" ] || die "no channel '$display_name' in $project. Its owner creates it from $secret with: infra/environments/route-alerts.sh $environment"
  [ "$(printf '%s\n' "$channel" | wc -l)" -eq 1 ] || die "more than one channel '$display_name' in $project; delete all but one"
  echo "$channel"
  exit 0
fi

require_authorisation >&2

if [ "$DRY_RUN" = "1" ]; then
  echo_command gcloud secrets versions access latest --secret="$secret" --project="$project"
  echo_command gcloud beta monitoring channels list --project="$project" --filter="displayName=\"$display_name\""
  echo_command gcloud beta monitoring channels create "|update <channel>" --project="$project" --channel-content-from-file=/dev/stdin
  echo "  (stdin: {\"type\":\"webhook_tokenauth\",\"displayName\":\"$display_name\",\"labels\":{\"url\":\"<$secret>\"}})"
  exit 0
fi

url=$(gcloud secrets versions access latest --secret="$secret" --project="$project") ||
  die "could not read $secret in $project; route-alerts.sh is run by the secret's owner"
case $url in
  https://*) ;;
  *) die "$secret does not hold an https:// URL; Cloud Monitoring sends the alert to whatever it names" ;;
esac

content() {
  printf '%s' "$url" | jq -Rs --arg name "$display_name" \
    '{type: "webhook_tokenauth", displayName: $name, description: "TASK-091: the on-call channel. URL from ALERT_CHANNEL_WEBHOOK_URL; kept by infra/environments/route-alerts.sh.", labels: {url: .}, enabled: true}'
}

channel=$(existing) || exit 1
if [ -z "$channel" ]; then
  content | gcloud beta monitoring channels create --project="$project" --channel-content-from-file=/dev/stdin --format='value(name)' >/dev/null
  echo "created $display_name" >&2
else
  content | gcloud beta monitoring channels update "$channel" --project="$project" --channel-content-from-file=/dev/stdin >/dev/null
  echo "updated $display_name with the current $secret" >&2
fi

existing
