#!/usr/bin/env sh
# Verifies one provisioned environment's network against the controls TASK-021 owns.
#
#   infra/terraform/network/verify-network-security.sh sit
#   infra/terraform/network/verify-network-security.sh sit --dry-run   # prints the calls it would make
#   infra/terraform/network/verify-network-security.sh sit --scan      # also scans from where it runs
#   infra/terraform/network/verify-network-security.sh sit --probe     # also probes from inside the VPC
#
# The validation cell is two experiments — an external port scan on which only 443 is reachable,
# and a connection to the database port from outside the API tier that is blocked — and a static
# check on the Terraform can only prove the code asks for the result. N-1 to N-5 read the running
# configuration and are read-only; N-6 to N-8 are the scan (--scan); N-9 is the probe (--probe).
#   N-1  the database has no public endpoint, and takes its private IP from the one range the
#        firewall rules are written against (CTL-03)
#   N-2  the firewall admits no inbound traffic, opens the database port to the API tier's tag
#        only, and denies the database range to every other source in the VPC (CTL-03)
#   N-3  the project holds one network, one public address and one public listener, on TCP 443
#   N-4  TLS 1.2+ with the RESTRICTED profile at the load balancer, and the WAF attached and
#        enforcing (CTL-04, G-7)
#   N-5  the API is reachable only through the load balancer, and carries the tag N-2 is scoped to
#   N-6  an external TCP scan of every port on the public address finds 443 open and nothing else
#   N-7  the load balancer refuses TLS 1.0, TLS 1.1 and a CBC suite, and accepts TLS 1.2 and 1.3
#   N-8  the WAF blocks an SQL-injection probe, while an ordinary request is not blocked
#   N-9  from inside the VPC, a host without the API tier's tag cannot reach the database port;
#        a host with it can, which is what shows the first result is the firewall and not a fault
#
# Requires: gcloud (viewer rights on the environment's project), jq.
# --scan additionally requires nmap, openssl and curl, and must run from OUTSIDE the network — a
#   laptop or CI runner on the internet — because that is where the validation cell scans from.
# --probe additionally requires rights to create and delete Compute Engine instances. It creates
#   two e2-micro VMs with no public address and no service account in the application subnet,
#   reads one line each from their serial consoles, and deletes them on exit, success or not.
# Record: docs/architecture/network-security.md
set -eu

here=$(cd "$(dirname "$0")" && pwd)
MANIFEST=${MANIFEST:-"$here/../../environments/environments.json"}
export MANIFEST
# shellcheck source=../../environments/lib/common.sh
. "$here/../../environments/lib/common.sh"

environment=${1:-}
parse_dry_run "$@"
SCAN=0
PROBE=0
for arg in "$@"; do
  case "$arg" in
    --scan) SCAN=1 ;;
    --probe) PROBE=1 ;;
  esac
done
require_tools jq
[ "$DRY_RUN" = "1" ] || require_tools gcloud
[ "$SCAN" = "0" ] || [ "$DRY_RUN" = "1" ] || require_tools nmap openssl curl
require_known_environment "$environment"
region=$(require_region)

# Every name below is derived exactly as infra/terraform/platform/main.tf derives it, from the same
# manifest, so this script and the Terraform cannot disagree about what they are looking at.
project=$(env_get "$environment" '.project_id')
network=$(env_get "$environment" '.network')
instance=$(env_get "$environment" '.database.instance')
prefix="$(manifest_get '.platform.resource_prefix')-$environment"
app_tag="$prefix-app"
psa_range="$network-private-services"
database_port=5432
app_base_url=$(env_get "$environment" '.variables.APP_BASE_URL.value')
host=$(echo "$app_base_url" | sed -n 's#^https://\([^/]*\).*#\1#p')

failures=0

fail() {
  echo "  FAIL $*"
  failures=$((failures + 1))
}

note() {
  echo "  ---- $*"
}

# gcloud_json <args...> — a read-only call. Under --dry-run it prints the call and returns an empty
# document, which makes every check below report "not checked" rather than a false pass.
gcloud_json() {
  if [ "$DRY_RUN" = "1" ]; then
    echo_command gcloud "$@" --project="$project" --format=json >&2
    echo '{}'
  else
    gcloud "$@" --project="$project" --format=json
  fi
}

# field <json> <jq-filter> — empty string when absent. Not jq's `//`, which reads `false` as absent:
# ipv4Enabled being false is the answer N-1 most wants to read.
field() {
  echo "$1" | jq -r "$2 | if . == null then \"\" else . end"
}

echo "== $environment — project $project, network $network, region $region =="

sql=$(gcloud_json sql instances describe "$instance")
networks=$(gcloud_json compute networks list)
rules=$(gcloud_json compute firewall-rules list)
range=$(gcloud_json compute addresses describe "$psa_range" --global)
addresses=$(gcloud_json compute addresses list)
forwarding=$(gcloud_json compute forwarding-rules list)
instances=$(gcloud_json compute instances list)
proxy=$(gcloud_json compute target-https-proxies describe "$prefix-https-proxy" --region="$region")
backend=$(gcloud_json compute backend-services describe "$prefix-api" --region="$region")
waf=$(gcloud_json compute security-policies describe "$prefix-waf" --region="$region")
service=$(gcloud_json run services describe "$prefix-api" --region="$region")

psa_cidr=""
lb_ip=""
database_ip=""

if [ "$DRY_RUN" != "1" ]; then
  # N-1. No public endpoint is two facts: the instance has no public address, and it holds its
  # private one in the range the firewall rules name — otherwise N-2 is guarding a different range.
  [ "$(field "$sql" '.settings.ipConfiguration.ipv4Enabled')" = "false" ] ||
    fail "N-1 the database has a public address (ipv4Enabled is not false). CTL-03: the database tier has no public endpoint."
  [ -z "$(field "$sql" '[.ipAddresses[]? | select(.type != "PRIVATE")] | first | .ipAddress')" ] ||
    fail "N-1 the database holds an address that is not private: $(field "$sql" '[.ipAddresses[]? | select(.type != "PRIVATE") | .ipAddress] | join(", ")')."
  [ -z "$(field "$sql" '.settings.ipConfiguration.authorizedNetworks[]?.value')" ] ||
    fail "N-1 the database has authorized networks, which only exist to admit public clients."
  allocated=$(field "$sql" '.settings.ipConfiguration.allocatedIpRange')
  [ "$allocated" = "$psa_range" ] ||
    fail "N-1 the database takes its address from '${allocated:-any range}', not $psa_range, so the firewall rules in N-2 do not address it."
  database_ip=$(field "$sql" '[.ipAddresses[]? | select(.type == "PRIVATE") | .ipAddress] | first')
  psa_cidr="$(field "$range" '.address')/$(field "$range" '.prefixLength')"
  note "N-1 database $instance at $database_ip, private only, in $psa_range ($psa_cidr)"

  # N-2. The rules on this network, enabled ones only: a disabled rule enforces nothing either way.
  live=$(echo "$rules" | jq --arg n "/networks/$network" '[.[] | select((.network | endswith($n)) and (.disabled != true))]')

  inbound=$(echo "$live" | jq -r '[.[] | select(.direction == "INGRESS" and ((.allowed // []) | length) > 0) | .name] | join(", ")')
  [ -z "$inbound" ] ||
    fail "N-2 ingress is allowed by: $inbound. Nothing in this architecture receives traffic on the VPC path; the load balancer reaches the API through a serverless endpoint group."

  allow=$(echo "$live" | jq --arg c "$psa_cidr" '[.[] | select(.direction == "EGRESS" and ((.allowed // []) | length) > 0 and ((.destinationRanges // []) | index($c)))]')
  if [ "$(echo "$allow" | jq 'length')" != "1" ]; then
    fail "N-2 expected exactly one rule allowing traffic to $psa_cidr, found $(echo "$allow" | jq 'length')."
  else
    [ "$(echo "$allow" | jq -c '.[0].targetTags // []')" = "[\"$app_tag\"]" ] ||
      fail "N-2 the rule allowing $psa_cidr is scoped to $(echo "$allow" | jq -c '.[0].targetTags // "every instance"'), not [\"$app_tag\"]."
    [ "$(echo "$allow" | jq -c '.[0].allowed')" = "[{\"IPProtocol\":\"tcp\",\"ports\":[\"$database_port\"]}]" ] ||
      fail "N-2 the rule allowing $psa_cidr opens $(echo "$allow" | jq -c '.[0].allowed'), not tcp:$database_port alone."
  fi
  allow_priority=$(echo "$allow" | jq '.[0].priority // 0')

  deny=$(echo "$live" | jq --arg c "$psa_cidr" '[.[] | select(.direction == "EGRESS" and ((.denied // []) | length) > 0 and ((.destinationRanges // []) | index($c)) and ((.targetTags // []) | length) == 0 and ((.targetServiceAccounts // []) | length) == 0)] | sort_by(.priority)')
  if [ "$(echo "$deny" | jq 'length')" = "0" ]; then
    fail "N-2 no rule denies $psa_cidr to every source. Cloud SQL lives in Google's producer network where no ingress rule can be written, so without it any other workload in the VPC reaches the database."
  else
    deny_priority=$(echo "$deny" | jq '.[0].priority')
    [ "$deny_priority" -gt "$allow_priority" ] ||
      fail "N-2 the database deny (priority $deny_priority) outranks the API tier's allow ($allow_priority), so the API tier cannot reach its own database."
    [ "$(echo "$deny" | jq '.[0].logConfig.enable // false')" = "true" ] ||
      fail "N-2 the database deny is not logged, so a blocked attempt leaves no evidence."
    # Any other egress allow that is evaluated before the deny could let a source through it.
    ahead=$(echo "$live" | jq -r --argjson p "$deny_priority" --arg c "$psa_cidr" '[.[] | select(.direction == "EGRESS" and ((.allowed // []) | length) > 0 and .priority <= $p and (((.destinationRanges // []) | index($c)) | not)) | .name] | join(", ")')
    [ -z "$ahead" ] ||
      fail "N-2 egress allowed by $ahead is evaluated before the database deny. Review whether its destination covers $psa_cidr."
  fi
  note "N-2 $(echo "$live" | jq 'length') enabled rule(s) on $network"

  # N-3. What an outside scanner could possibly find: networks, public addresses, public listeners.
  names=$(echo "$networks" | jq -r '[.[].name] | join(", ")')
  [ "$names" = "$network" ] ||
    fail "N-3 the project holds networks '$names', expected only $network. A 'default' network ships with SSH and RDP open to 0.0.0.0/0 (apply-org-policy.sh binds compute.skipDefaultNetworkCreation)."
  external=$(echo "$addresses" | jq '[.[] | select(.addressType == "EXTERNAL")]')
  [ "$(echo "$external" | jq -r '[.[].name] | join(", ")')" = "$prefix-lb" ] ||
    fail "N-3 public addresses are '$(echo "$external" | jq -r '[.[].name] | join(", ")')', expected only $prefix-lb."
  lb_ip=$(echo "$external" | jq -r --arg n "$prefix-lb" '[.[] | select(.name == $n) | .address] | first // ""')
  listeners=$(echo "$forwarding" | jq -c '[.[] | select((.loadBalancingScheme // "") | startswith("EXTERNAL")) | {name, IPProtocol, portRange, IPAddress}]')
  [ "$(echo "$listeners" | jq -c '[.[] | {IPProtocol, portRange, IPAddress}]')" = "[{\"IPProtocol\":\"TCP\",\"portRange\":\"443-443\",\"IPAddress\":\"$lb_ip\"}]" ] ||
    fail "N-3 public listeners are $listeners, expected exactly one: TCP 443 on $lb_ip."
  public_vms=$(echo "$instances" | jq -r '[.[] | select([.networkInterfaces[]?.accessConfigs[]?.natIP] | length > 0) | .name] | join(", ")')
  [ -z "$public_vms" ] ||
    fail "N-3 VM(s) with a public address: $public_vms (apply-org-policy.sh binds compute.vmExternalIpAccess)."
  note "N-3 one public address, $lb_ip, listening on TCP 443"

  # N-4. TLS policy on the proxy, and the WAF on the backend.
  ssl_policy_name=$(field "$proxy" '.sslPolicy' | sed 's#.*/##')
  if [ -z "$ssl_policy_name" ]; then
    fail "N-4 the HTTPS proxy has no SSL policy, so it negotiates the provider's defaults, which include TLS 1.0."
  else
    ssl=$(gcloud_json compute ssl-policies describe "$ssl_policy_name" --region="$region")
    case "$(field "$ssl" '.minTlsVersion')" in
      TLS_1_2 | TLS_1_3) ;;
      *) fail "N-4 minimum TLS version is '$(field "$ssl" '.minTlsVersion')'. CTL-04 requires 1.2 or above." ;;
    esac
    [ "$(field "$ssl" '.profile')" = "RESTRICTED" ] ||
      fail "N-4 TLS profile is '$(field "$ssl" '.profile')', expected RESTRICTED; the others admit CBC suites with SHA-1 MACs."
    note "N-4 TLS: $ssl_policy_name, minimum $(field "$ssl" '.minTlsVersion'), profile $(field "$ssl" '.profile')"
  fi
  attached=$(field "$backend" '.securityPolicy' | sed 's#.*/##')
  [ "$attached" = "$prefix-waf" ] ||
    fail "N-4 the backend's security policy is '${attached:-none}', expected $prefix-waf. Traffic reaches the API unfiltered."
  waf_rules=$(echo "$waf" | jq '[.rules[]? | select(.priority < 2147483647)]')
  [ "$(echo "$waf_rules" | jq 'length')" -gt 0 ] ||
    fail "N-4 the WAF policy has no rules beyond its default allow."
  previewing=$(echo "$waf_rules" | jq -r '[.[] | select(.preview == true) | .priority] | join(", ")')
  [ -z "$previewing" ] ||
    fail "N-4 WAF rule(s) at priority $previewing are in preview: they log and do not block (G-7 baseline: enforcing)."
  note "N-4 WAF $prefix-waf attached, $(echo "$waf_rules" | jq 'length') rule(s)"

  # N-5. The API's own URL must answer nothing from outside, or every control above is bypassable.
  ingress=$(field "$service" '.metadata.annotations["run.googleapis.com/ingress"]')
  [ "$ingress" = "internal-and-cloud-load-balancing" ] ||
    fail "N-5 the API's ingress is '${ingress:-unset}', expected internal-and-cloud-load-balancing. Its run.app URL bypasses TLS policy and WAF."
  interfaces=$(field "$service" '.spec.template.metadata.annotations["run.googleapis.com/network-interfaces"]')
  echo "$interfaces" | jq -e --arg t "$app_tag" '[.[]?.tags[]?] | index($t)' >/dev/null 2>&1 ||
    fail "N-5 the API's VPC interface does not carry $app_tag, so the allow rule in N-2 does not apply to it and it cannot reach the database."
fi

# N-6 to N-8 — the validation cell's scan, run from wherever this script runs. It is only evidence
# when that is outside the network, which is the operator's to ensure and the record's to state.
if [ "$SCAN" = "1" ] && [ "$DRY_RUN" = "1" ]; then
  echo_command nmap -Pn -sT -p- -T4 --open -oG - "<lb-ip>"
  echo_command openssl s_client -connect "<lb-ip>:443" -servername "${host:-<host>}" "-tls1|-tls1_1|-tls1_2|-tls1_3"
  echo_command curl --resolve "${host:-<host>}:443:<lb-ip>" "https://${host:-<host>}/health"
  echo_command curl --resolve "${host:-<host>}:443:<lb-ip>" "https://${host:-<host>}/health?id=1%27%20OR%20%271%27%3D%271"
elif [ "$SCAN" = "1" ]; then
  if [ -z "$lb_ip" ] || [ -z "$host" ]; then
    fail "N-6 no public address or no APP_BASE_URL host, so there is nothing to scan."
  else
    # N-6. Every TCP port. A connect scan needs no privileges; -Pn because the address answers no ping.
    open=$(nmap -Pn -sT -p- -T4 --open -oG - "$lb_ip" |
      sed -n 's/.*Ports: //p' | tr ',' '\n' | sed -n 's#^ *\([0-9]*\)/open/.*#\1#p' | sort -n | tr '\n' ' ' | sed 's/ $//')
    if [ -z "$open" ]; then
      fail "N-6 no port on $lb_ip answered, not even 443, so the scan never reached the load balancer and proved nothing."
    elif [ "$open" = "443" ]; then
      note "N-6 external scan of $lb_ip, TCP 1-65535: only 443 open"
    else
      fail "N-6 external scan of $lb_ip found open: $open. Only 443 may be reachable."
    fi

    # N-7. Three outcomes per handshake, as in TASK-020's D-2: the server accepted it, the server
    # refused it, or the client never offered it. OpenSSL 3 will not offer TLS 1.0 or 1.1 at its
    # default security level, and a handshake the client refused to attempt is not a server's
    # refusal — so the legacy attempts drop to security level 0, and "no protocols available"
    # is reported as nothing proved rather than as a pass.
    handshake() {
      if out=$(openssl s_client -connect "$lb_ip:443" -servername "$host" "$@" </dev/null 2>&1); then :; fi
      if echo "$out" | grep -Eq 'Cipher is [A-Z0-9_-]+' && ! echo "$out" | grep -q 'Cipher is (NONE)'; then
        echo accepted
      elif echo "$out" | grep -Eiq 'no protocols available|no ciphers available|unknown option'; then
        echo untested
      elif echo "$out" | grep -Eiq 'alert|handshake failure|wrong version number|unsupported protocol'; then
        echo refused
      else
        echo untested
      fi
    }
    for case_ in "TLS 1.0|refused|-tls1 -cipher DEFAULT:@SECLEVEL=0" \
      "TLS 1.1|refused|-tls1_1 -cipher DEFAULT:@SECLEVEL=0" \
      "TLS 1.2 with a CBC suite|refused|-tls1_2 -cipher ECDHE-ECDSA-AES128-SHA:ECDHE-RSA-AES128-SHA:@SECLEVEL=0" \
      "TLS 1.2|accepted|-tls1_2" \
      "TLS 1.3|accepted|-tls1_3"; do
      label=${case_%%|*}
      rest=${case_#*|}
      expected=${rest%%|*}
      # shellcheck disable=SC2086 # the options are a word list on purpose
      got=$(handshake ${rest#*|})
      if [ "$got" = "$expected" ]; then
        note "N-7 $label: $got"
      elif [ "$got" = "untested" ]; then
        fail "N-7 $label: this client could not attempt it, so nothing was proved. Use an OpenSSL build that still offers it."
      else
        fail "N-7 $label: $got, expected $expected (CTL-04)."
      fi
    done

    # N-8. A blocked probe alone could be a WAF that blocks everything, or an application returning
    # 403 for its own reasons; an ordinary request that is not blocked is what makes it evidence.
    ordinary=$(curl -s -o /dev/null -w '%{http_code}' --max-time 15 --resolve "$host:443:$lb_ip" "https://$host/health" || true)
    blocked=$(curl -s -o /dev/null -w '%{http_code}' --max-time 15 --resolve "$host:443:$lb_ip" \
      "https://$host/health?id=1%27%20OR%20%271%27%3D%271" || true)
    if [ "$ordinary" = "000" ] || [ "$ordinary" = "403" ]; then
      fail "N-8 an ordinary request returned $ordinary, so a 403 on the probe would prove nothing about the WAF."
    elif [ "$blocked" = "403" ]; then
      note "N-8 SQL-injection probe blocked (403); ordinary request $ordinary"
    else
      fail "N-8 SQL-injection probe returned $blocked, expected 403 from the WAF (G-7)."
    fi
  fi
fi

# N-9 — the validation cell's "direct connection to the database port from outside the API tier's
# security group". From the internet there is no database address to attempt, which N-1 already
# proves; the attempt that means something starts inside the VPC, from a host the firewall does
# not recognise as the API tier. Two VMs: one untagged, which must be dropped, and one carrying the
# API tier's tag, which must connect. Without the second, a timeout could equally be a database
# that is down or an address that is wrong.
if [ "$PROBE" = "1" ]; then
  zone=$(if [ "$DRY_RUN" = "1" ]; then echo "<zone in $region>"; else
    gcloud compute zones list --project="$project" --filter="region:$region" --format='value(name)' | head -1; fi)
  untagged="$prefix-probe-untagged"
  tagged="$prefix-probe-tagged"
  # A file rather than --metadata=startup-script=…, which splits its value on commas. timeout exits
  # 124 when the connect never completes — a firewall drop — and the attempt fails fast with any
  # other status when something answers with a reset.
  script=$(mktemp)
  cat > "$script" <<PROBE
#!/bin/bash
if timeout 5 bash -c 'exec 3<>/dev/tcp/${database_ip:-<database-ip>}/$database_port' 2>/dev/null; then r=CONNECTED
elif [ \$? -eq 124 ]; then r=TIMEOUT; else r=REFUSED; fi
echo "PROBE-RESULT \$r" | tee /dev/ttyS0
PROBE

  cleanup() {
    rm -f "$script"
    if [ "$DRY_RUN" = "1" ]; then
      echo_command gcloud compute instances delete "$untagged" "$tagged" --zone="$zone" --project="$project" --quiet
    else
      gcloud compute instances delete "$untagged" "$tagged" --zone="$zone" --project="$project" --quiet >/dev/null 2>&1 || true
    fi
  }
  trap cleanup EXIT

  for vm in "$untagged" "$tagged"; do
    tags=""
    [ "$vm" = "$tagged" ] && tags="--tags=$app_tag"
    # shellcheck disable=SC2086 # $tags is empty or one flag
    run gcloud compute instances create "$vm" --project="$project" --zone="$zone" \
      --machine-type=e2-micro --subnet="$network-app" --no-address \
      --no-service-account --no-scopes --shielded-secure-boot \
      --image-family=debian-12 --image-project=debian-cloud \
      --labels=purpose=task-021-probe --metadata-from-file=startup-script="$script" --format=none $tags
  done

  if [ "$DRY_RUN" != "1" ]; then
    result() {
      attempt=0
      while [ "$attempt" -lt 30 ]; do
        line=$(gcloud compute instances get-serial-port-output "$1" --zone="$zone" --project="$project" 2>/dev/null |
          sed -n 's/.*PROBE-RESULT \([A-Z]*\).*/\1/p' | head -1)
        [ -n "$line" ] && { echo "$line"; return; }
        attempt=$((attempt + 1))
        sleep 10
      done
      echo NONE
    }
    from_untagged=$(result "$untagged")
    from_tagged=$(result "$tagged")
    case "$from_tagged" in
      CONNECTED) note "N-9 positive control: a host tagged $app_tag reached $database_ip:$database_port" ;;
      *) fail "N-9 the positive control, tagged $app_tag, got $from_tagged on $database_ip:$database_port. The API tier's own path is broken, so the untagged result proves nothing." ;;
    esac
    case "$from_untagged" in
      TIMEOUT) note "N-9 a host without $app_tag was dropped at $database_ip:$database_port (logged by the database deny rule)" ;;
      CONNECTED) fail "N-9 a host without $app_tag CONNECTED to $database_ip:$database_port. Only the API tier may reach the database (CTL-03)." ;;
      REFUSED) fail "N-9 a host without $app_tag was answered by $database_ip:$database_port with a reset, so its packets reached the database tier." ;;
      *) fail "N-9 the untagged probe reported nothing within five minutes, so nothing was proved." ;;
    esac
  fi
fi

if [ "$DRY_RUN" = "1" ]; then
  echo "(dry run: the calls above were printed, not made; nothing was checked)"
  exit 0
fi

if [ "$failures" -gt 0 ]; then
  echo "$failures finding(s)."
  exit 1
fi

ran="configuration (N-1 to N-5)"
[ "$SCAN" = "1" ] && ran="$ran, external scan (N-6 to N-8)"
[ "$PROBE" = "1" ] && ran="$ran, VPC probe (N-9)"
echo "OK: $ran."
[ "$SCAN" = "1" ] || echo "Not run: the external scan. Add --scan, from outside the network."
[ "$PROBE" = "1" ] || echo "Not run: the database probe. Add --probe."
