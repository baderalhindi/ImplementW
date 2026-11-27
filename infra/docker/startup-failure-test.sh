#!/usr/bin/env bash
# The API image must exit non-zero, without `docker run --init`, when the service fails at start-up
# (rollback-validation.md F-4, QA PHASE-E-02). Without an init, dotnet is PID 1, its abort on the unhandled
# exception is ignored by the kernel, and the container spins at full CPU instead of exiting.
#
#   infra/docker/startup-failure-test.sh <image>
#
# Two failures, one per start-up phase:
#   building the host   Production with no SECRET_STORE_ENDPOINT (SecretStoreException, Program.cs AddSecretStore)
#   starting the host   APM_DSN without APM_ENVIRONMENT_TAG (OptionsValidationException from ValidateOnStart)
# The DSN points at a closed local port; nothing leaves the container.
set -euo pipefail

image=${1:?usage: startup-failure-test.sh <image>}
limit_seconds=${STARTUP_FAILURE_LIMIT_SECONDS:-60}
name="pmplatform-startup-failure-$$"
failures=0

cleanup() { docker rm -f "$name" >/dev/null 2>&1 || true; }
trap cleanup EXIT

# expect_failed_start <case> <log pattern> <docker run -e arguments...>
expect_failed_start() {
  local case=$1 pattern=$2
  shift 2
  cleanup
  docker run -d --name "$name" --label com.docker.compose.project=ahda "$@" "$image" >/dev/null
  local started elapsed exit_code
  started=$(date +%s)
  while [ "$(docker inspect --format '{{.State.Running}}' "$name")" = true ]; do
    if (( $(date +%s) - started >= limit_seconds )); then
      echo "FAIL $case: still running ${limit_seconds}s after the start-up failure (PID 1 abort spin)"
      docker logs "$name" 2>&1 | grep -m1 'Unhandled exception' || true
      docker top "$name" -o pid,pcpu,args || true
      failures=$((failures + 1))
      return
    fi
    sleep 1
  done
  elapsed=$(( $(date +%s) - started ))
  exit_code=$(docker inspect --format '{{.State.ExitCode}}' "$name")
  # Read the whole log before matching: `docker logs | grep -q` fails under pipefail when grep stops reading first.
  local log
  log=$(docker logs "$name" 2>&1)
  if ! grep -q "$pattern" <<<"$log"; then
    echo "FAIL $case: exited $exit_code, but the log does not show $pattern:"
    docker logs "$name" 2>&1 | tail -n 10
    failures=$((failures + 1))
  elif [ "$exit_code" -eq 0 ]; then
    echo "FAIL $case: exited 0; the platform would read the failed start as a clean stop"
    failures=$((failures + 1))
  else
    echo "PASS $case: exited $exit_code after ${elapsed}s, no --init"
  fi
}

expect_failed_start "building the host (no secret store)" 'SecretStoreException' \
  -e ASPNETCORE_ENVIRONMENT=Production
expect_failed_start "starting the host (APM_DSN without APM_ENVIRONMENT_TAG)" 'OptionsValidationException' \
  -e ASPNETCORE_ENVIRONMENT=Development -e APM_DSN=http://public@127.0.0.1:9/1

if [ "$failures" -ne 0 ]; then
  echo "$failures start-up failure(s) did not end the container."
  exit 1
fi
echo "OK: every start-up failure ends the container with a non-zero exit."
