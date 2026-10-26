#!/usr/bin/env bash
# Generate and publish the OpenAPI document (TASK-094, api-conventions R-49). Record: docs/api/README.md
#
# Runs the built API's `openapi` command, which writes the document the API serves without starting it or touching a
# database, and then:
#   1. fails if the document differs from the committed contract snapshot docs/api/openapi.v1.json, so the snapshot
#      can never describe an API other than the one in the same commit (the contract tests compare it module by module;
#      this compares all of it, byte for byte);
#   2. lints it with TASK-009's contract-check.py and keeps the report (not a gate here: the open platform findings are
#      the contract tests' to judge, OpenApiDocument.OpenPlatformFindings);
#   3. writes publication.json — the commit, the time and the counts — beside it.
# The job that runs it uploads $OUTPUT_DIR as the `openapi-v1` artifact.
#
#   dotnet build src/backend/PMPlatform.Api --configuration Release && .github/scripts/openapi-publish.sh
set -euo pipefail

API_PROJECT=${API_PROJECT:-src/backend/PMPlatform.Api}
OUTPUT_DIR=${OUTPUT_DIR:-artifacts/api}
SNAPSHOT=docs/api/openapi.v1.json

die() { printf 'openapi publish: %s\n' "$*" >&2; exit 1; }

summary() {
  [ -n "${GITHUB_STEP_SUMMARY:-}" ] || return 0
  printf '%s\n' "$*" >>"$GITHUB_STEP_SUMMARY"
}

mkdir -p "$OUTPUT_DIR"
DOCUMENT="$(cd "$OUTPUT_DIR" && pwd)/openapi.v1.json"

# Development only lifts the secret-store requirement (TASK-019): the command reads no secret and opens no connection.
# The path is absolute because `dotnet run` runs the API from its project directory.
ASPNETCORE_ENVIRONMENT=Development dotnet run --project "$API_PROJECT" --configuration Release --no-build --no-launch-profile \
  -- openapi "$DOCUMENT" || die "the API did not write its OpenAPI document"
[ -s "$DOCUMENT" ] || die "the API exited without writing $DOCUMENT"

if ! cmp -s "$SNAPSHOT" "$DOCUMENT"; then
  diff -u "$SNAPSHOT" "$DOCUMENT" | head -200 >&2 || true
  die "$SNAPSHOT is not the API as built. Regenerate it with: ASPNETCORE_ENVIRONMENT=Development dotnet run --project $API_PROJECT -- openapi \"\$PWD/$SNAPSHOT\", and commit it"
fi

lint_status=0
python3 docs/architecture/contract-check.py "$DOCUMENT" >"$OUTPUT_DIR/contract-check.txt" || lint_status=$?
[ "$lint_status" -le 1 ] || die "contract-check.py did not run (exit $lint_status)"

python3 - "$DOCUMENT" "$OUTPUT_DIR/contract-check.txt" "${GITHUB_SHA:-$(git rev-parse HEAD)}" >"$OUTPUT_DIR/publication.json" <<'EOF'
import collections, datetime, json, sys
document = json.load(open(sys.argv[1], encoding="utf-8"))
methods = {"get", "put", "post", "delete", "patch", "head", "options"}
operations = [o for item in document["paths"].values() for m, o in item.items() if m in methods]
lint = open(sys.argv[2], encoding="utf-8").read().splitlines()
print(json.dumps({
    "commit": sys.argv[3],
    "generatedAt": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
    "openapi": document["openapi"],
    "infoVersion": document["info"]["version"],
    "paths": len(document["paths"]),
    "operations": len(operations),
    "schemas": len(document.get("components", {}).get("schemas", {})),
    "operationsByTag": dict(sorted(collections.Counter(t for o in operations for t in o.get("tags", [])).items())),
    "contractCheck": lint[-1] if lint else "",
}, indent=2))
EOF

summary "### OpenAPI document (TASK-094)"
summary ""
summary "Generated from the API as built and identical to \`$SNAPSHOT\`: $(python3 -c 'import json,sys; p=json.load(open(sys.argv[1])); print(p["operations"], "operations,", p["paths"], "paths,", p["schemas"], "schemas,", len(p["operationsByTag"]), "modules")' "$OUTPUT_DIR/publication.json"). Lint: $(tail -1 "$OUTPUT_DIR/contract-check.txt")."
printf 'openapi publish: %s matches %s; artifact in %s\n' "$DOCUMENT" "$SNAPSHOT" "$OUTPUT_DIR"
