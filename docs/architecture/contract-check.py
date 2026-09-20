#!/usr/bin/env python3
"""Contract lint for the platform API and event conventions (TASK-009).

Checks an OpenAPI 3.x JSON document against api-conventions.md §4 (rules C-1 to C-14 in §8) and
event JSON samples against event-envelope.schema.json (event-conventions.md §5). Exit code 1 on
any finding. Standard library only, so it runs in the TASK-015 CI gate with no extra toolchain.

    python3 docs/architecture/contract-check.py                       # sample spec + event samples
    python3 docs/architecture/contract-check.py path/to/openapi.json  # the generated spec
    python3 docs/architecture/contract-check.py --self-test           # prove every rule fires
"""
import copy
import json
import re
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
SAMPLE_SPEC = HERE / "api-conventions-samples.openapi.json"
ENVELOPE_SCHEMA = HERE / "event-envelope.schema.json"
EVENT_SAMPLES = HERE / "event-samples"

MODULES = {
    "Project", "Progress", "Schedule", "ProjectTask", "Milestone", "Risk", "ManagementConcern",
    "ChangeRequest", "Suspension", "Closure", "Approval", "DocumentManagement",
    "ExternalParticipation", "FinancialKpi", "Notifications", "Dashboards", "Reports",
    "IdentityAccess", "MasterDataConfig", "IntegrationMonitoring", "AuditActivity",
}
BASE = "/api/v1/"
KEBAB = re.compile(r"^[a-z][a-z0-9]*(-[a-z0-9]+)*$")
CAMEL = re.compile(r"^[a-z][A-Za-z0-9]*$")
PASCAL = re.compile(r"^[A-Z][A-Za-z0-9]*$")
OPERATION_ID = re.compile(r"^[A-Z][A-Za-z0-9]*_[A-Z][A-Za-z0-9]*$")
PATH_PARAM = re.compile(r"^\{[a-z][A-Za-z0-9]*\}$")
PROBLEM_REF = "#/components/schemas/ProblemDetails"
PROBLEM_REQUIRED = {"type", "title", "status", "instance", "code", "correlationId", "idempotencyKey", "timestamp"}
MONEY_PATTERN = r"^-?[0-9]{1,16}\.[0-9]{2}$"
WRITE_METHODS = {"post", "put"}
METHODS = {"get", "post", "put", "delete", "patch", "head", "options"}
UUID = re.compile(r"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$")

findings = []


def find(rule, msg):
    findings.append(f"{rule}: {msg}")


# ------------------------------------------------------------------ helpers
def resolve(spec, node):
    """Follow a local $ref chain; returns the referenced object (or node itself)."""
    seen = 0
    while isinstance(node, dict) and "$ref" in node and seen < 10:
        ref = node["$ref"]
        if not ref.startswith("#/"):
            return node
        cur = spec
        for part in ref[2:].split("/"):
            cur = cur.get(part, {}) if isinstance(cur, dict) else {}
        node = cur
        seen += 1
    return node


def params_of(spec, path_item, op):
    out = []
    for p in list(path_item.get("parameters", [])) + list(op.get("parameters", [])):
        out.append(resolve(spec, p))
    return out


def header_param(params, name, required=None):
    for p in params:
        if p.get("in") == "header" and p.get("name", "").lower() == name.lower():
            return required is None or bool(p.get("required")) == required
    return False


def query_param(params, name):
    return any(p.get("in") == "query" and p.get("name") == name for p in params)


def is_success(code):
    return code.isdigit() and code.startswith("2")


def response_is_problem(spec, resp):
    resp = resolve(spec, resp)
    content = resp.get("content", {})
    if set(content) != {"application/problem+json"}:
        return False
    schema = content["application/problem+json"].get("schema", {})
    return schema.get("$ref") == PROBLEM_REF


def path_segments(path):
    return [s for s in path[len(BASE):].split("/") if s]


def is_collection_path(path):
    segs = path_segments(path)
    return bool(segs) and not PATH_PARAM.match(segs[-1])


def is_command_path(path, path_item):
    segs = path_segments(path)
    return (
        len(segs) >= 3
        and PATH_PARAM.match(segs[-2]) is not None
        and not PATH_PARAM.match(segs[-1])
        and set(m for m in path_item if m in METHODS) == {"post"}
    )


# ------------------------------------------------------------ OpenAPI rules
def check_openapi(spec):
    if not str(spec.get("openapi", "")).startswith("3."):
        find("C-1", "document is not OpenAPI 3.x")
    schemas = spec.get("components", {}).get("schemas", {})

    # C-6 ProblemDetails
    problem = schemas.get("ProblemDetails")
    if not problem:
        find("C-6", "components.schemas.ProblemDetails is missing")
    else:
        missing = PROBLEM_REQUIRED - set(problem.get("required", []))
        if missing:
            find("C-6", f"ProblemDetails.required lacks {sorted(missing)}")
        missing = PROBLEM_REQUIRED - set(problem.get("properties", {}))
        if missing:
            find("C-6", f"ProblemDetails.properties lacks {sorted(missing)}")
        if "errors" not in problem.get("properties", {}):
            find("C-6", "ProblemDetails has no errors[] property")

    # C-11 / C-13 schema names and properties
    for name, schema in schemas.items():
        if not PASCAL.match(name):
            find("C-11", f"schema name {name!r} is not PascalCase")
        for prop, pschema in schema.get("properties", {}).items():
            if not CAMEL.match(prop):
                find("C-11", f"{name}.{prop} is not camelCase")
            if prop == "currency" or prop.endswith("Currency"):
                find("C-13", f"{name}.{prop}: no currency property is permitted (ADR-008, R-16)")
            if prop.endswith("Sar"):
                target = resolve(spec, pschema)
                alts = [resolve(spec, a) for a in target.get("oneOf", [])] if "oneOf" in target else [target]
                money = [a for a in alts if a.get("type") == "string"]
                if not money or any(a.get("pattern") != MONEY_PATTERN for a in money):
                    find("C-13", f"{name}.{prop} must be a MoneySar decimal string (R-16)")

    operation_ids = set()
    for path, path_item in spec.get("paths", {}).items():
        # C-1 base path; C-2 segments
        if not path.startswith(BASE):
            find("C-1", f"{path} does not start with {BASE}")
            continue
        segs = path_segments(path)
        if len(segs) > 4:
            find("C-2", f"{path} nests deeper than collection/{{id}}/child/{{id}} (R-3)")
        for i, seg in enumerate(segs):
            if PATH_PARAM.match(seg):
                if i == 0:
                    find("C-2", f"{path} starts with a parameter")
                continue
            if not KEBAB.match(seg):
                find("C-2", f"{path}: segment {seg!r} is not kebab-case")
        command = is_command_path(path, path_item)
        if not command and len(segs) >= 2 and not PATH_PARAM.match(segs[-1]) and PATH_PARAM.match(segs[-2]):
            find("C-2", f"{path}: a segment after {{id}} must be a POST-only command or a child collection (R-4)")

        for method, op in path_item.items():
            if method not in METHODS:
                continue
            where = f"{method.upper()} {path}"
            if method == "patch":
                find("C-3", f"{where}: PATCH is not used (R-5)")
            # C-4 operationId and tag
            oid = op.get("operationId", "")
            if not OPERATION_ID.match(oid):
                find("C-4", f"{where}: operationId {oid!r} is not <Module>_<Action> (R-50)")
            elif oid in operation_ids:
                find("C-4", f"{where}: duplicate operationId {oid}")
            operation_ids.add(oid)
            tags = op.get("tags", [])
            if len(tags) != 1 or tags[0] not in MODULES:
                find("C-4", f"{where}: exactly one tag from the 21 modules is required (R-51), got {tags}")
            if op.get("x-module") != (tags[0] if tags else None):
                find("C-4", f"{where}: x-module must equal the module tag")

            params = params_of(spec, path_item, op)
            responses = op.get("responses", {})

            # C-5 error responses
            if "default" not in responses:
                find("C-5", f"{where}: responses.default (ProblemDetails) is required (R-53)")
            for code, resp in responses.items():
                if is_success(code):
                    continue
                if not response_is_problem(spec, resp):
                    find("C-5", f"{where}: response {code} is not application/problem+json ProblemDetails (R-23)")

            # C-7 write class and idempotency; C-8 If-Match
            if method in WRITE_METHODS:
                wc = op.get("x-write-class")
                if wc not in ("sensitive", "non-sensitive"):
                    find("C-7", f"{where}: x-write-class must be 'sensitive' or 'non-sensitive' (R-35)")
                elif wc == "sensitive":
                    if not header_param(params, "Idempotency-Key", required=True):
                        find("C-7", f"{where}: sensitive write without a required Idempotency-Key header (R-35)")
                elif not str(op.get("x-write-class-reason", "")).strip():
                    find("C-7", f"{where}: non-sensitive write needs x-write-class-reason (R-35)")
                if method == "put" and not header_param(params, "If-Match", required=True):
                    find("C-8", f"{where}: PUT without a required If-Match header (R-21)")

            # C-9 paging on collection GETs
            if method == "get" and is_collection_path(path) and not op.get("x-unpaged"):
                offset = query_param(params, "page") and query_param(params, "pageSize")
                cursor = query_param(params, "cursor") and query_param(params, "pageSize")
                if not (offset or cursor):
                    find("C-9", f"{where}: collection GET without page/pageSize or cursor/pageSize (R-28)")
                ok = resolve(spec, responses.get("200", {}))
                schema = resolve(spec, ok.get("content", {}).get("application/json", {}).get("schema", {}))
                props = set(schema.get("properties", {}))
                if "items" not in props or not ({"totalCount", "nextCursor"} & props):
                    find("C-9", f"{where}: 200 schema is not a page envelope (items + totalCount|nextCursor) (R-29/R-30)")
            # C-10 sort declares x-sortable
            if query_param(params, "sort") and not op.get("x-sortable"):
                find("C-10", f"{where}: sort parameter without x-sortable (R-32)")

            # C-11 no inline response schemas; C-12 correlation/location headers
            for code, resp in responses.items():
                if not is_success(code):
                    continue
                resp = resolve(spec, resp)
                for ctype, media in resp.get("content", {}).items():
                    if ctype == "application/json" and "$ref" not in media.get("schema", {}):
                        find("C-11", f"{where} {code}: inline response schema; name it (R-22, R-54)")
                    if ctype not in ("application/json", "application/octet-stream") and not op.get("x-binary"):
                        find("C-10", f"{where} {code}: content type {ctype} needs x-binary (R-8, R-13)")
                headers = {h.lower() for h in resp.get("headers", {})}
                if "x-correlation-id" not in headers:
                    find("C-12", f"{where} {code}: X-Correlation-Id response header not declared (R-41)")
                if code == "201" and "location" not in headers:
                    find("C-12", f"{where} 201: Location header not declared (R-5)")
            if command and method == "post" and op.get("x-write-class") != "sensitive":
                find("C-7", f"{where}: a command endpoint is always a sensitive write (R-4, R-35)")


# --------------------------------------------------------- event samples
def validate(schema, value, path, root):
    """Minimal JSON Schema 2020-12 subset: type, required, properties, additionalProperties,
    enum, const, pattern, format uuid/date-time, items, $ref (local), minimum."""
    schema = resolve(root, schema)
    types = schema.get("type")
    if types:
        types = types if isinstance(types, list) else [types]
        actual = {type(None): "null", bool: "boolean", int: "integer", float: "number",
                  str: "string", list: "array", dict: "object"}[type(value)]
        if actual == "integer" and "number" in types:
            actual = "number"
        if actual not in types:
            find("EV", f"{path}: expected {types}, got {actual}")
            return
    if value is None:
        return
    if "const" in schema and value != schema["const"]:
        find("EV", f"{path}: must equal {schema['const']!r}")
    if "enum" in schema and value not in schema["enum"]:
        find("EV", f"{path}: {value!r} not in {schema['enum']}")
    if isinstance(value, str):
        if "pattern" in schema and not re.search(schema["pattern"], value):
            find("EV", f"{path}: {value!r} does not match {schema['pattern']}")
        if schema.get("format") == "uuid" and not UUID.match(value):
            find("EV", f"{path}: {value!r} is not a lowercase uuid")
        if schema.get("format") == "date-time" and not re.match(r"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?Z$", value):
            find("EV", f"{path}: {value!r} is not an RFC 3339 UTC date-time ending in Z")
    if isinstance(value, (int, float)) and not isinstance(value, bool) and "minimum" in schema and value < schema["minimum"]:
        find("EV", f"{path}: {value} < minimum {schema['minimum']}")
    if isinstance(value, dict):
        props = schema.get("properties", {})
        for req in schema.get("required", []):
            if req not in value:
                find("EV", f"{path}: missing required {req!r}")
        for k, v in value.items():
            if k in props:
                validate(props[k], v, f"{path}.{k}", root)
            elif schema.get("additionalProperties") is False:
                find("EV", f"{path}.{k}: not allowed")
            elif isinstance(schema.get("additionalProperties"), dict):
                validate(schema["additionalProperties"], v, f"{path}.{k}", root)
    if isinstance(value, list) and "items" in schema:
        for i, item in enumerate(value):
            validate(schema["items"], item, f"{path}[{i}]", root)


def check_events(schema_path, samples_dir):
    schema = json.loads(schema_path.read_text())
    samples = sorted(samples_dir.glob("*.json"))
    if not samples:
        find("EV", f"no event samples under {samples_dir}")
    for sample in samples:
        event = json.loads(sample.read_text())
        validate(schema, event, sample.name, schema)
        expected_key = f"{event.get('eventType')}:{event.get('idempotencyKey')}"
        if event.get("messageKey") != expected_key:
            find("EV", f"{sample.name}: messageKey must be '<eventType>:<idempotencyKey>' (EV-4)")
        if event.get("idempotencyKey") == event.get("correlationId"):
            find("EV", f"{sample.name}: idempotencyKey must not equal correlationId (R-45)")
    return len(samples)


# ---------------------------------------------------------------- self-test
def self_test():
    """Mutate the sample spec and samples; every mutation must produce the named finding."""
    base = json.loads(SAMPLE_SPEC.read_text())
    envelope = json.loads(ENVELOPE_SCHEMA.read_text())
    sample_event = json.loads(next(EVENT_SAMPLES.glob("*.json")).read_text())
    create = "/api/v1/projects"
    submit = "/api/v1/change-requests/{changeRequestId}/submit"
    cases = []

    def case(rule, name, mutate):
        cases.append((rule, name, mutate))

    case("C-1", "path outside /api/v1", lambda s: s["paths"].update({"/v2/things": s["paths"].pop(create)}))
    case("C-2", "non-kebab segment", lambda s: s["paths"].update({"/api/v1/changeRequests": s["paths"].pop(create)}))
    case("C-3", "PATCH operation", lambda s: s["paths"][create].update({"patch": copy.deepcopy(s["paths"][create]["post"])}))
    case("C-4", "bad operationId", lambda s: s["paths"][create]["post"].update({"operationId": "createProject"}))
    case("C-4", "tag not a module", lambda s: s["paths"][create]["post"].update({"tags": ["Projects"]}))
    case("C-5", "missing default response", lambda s: s["paths"][create]["post"]["responses"].pop("default"))
    case("C-5", "4xx not ProblemDetails", lambda s: s["paths"][create]["post"]["responses"].update(
        {"400": {"description": "x", "content": {"application/json": {"schema": {"type": "object"}}}}}))
    case("C-6", "correlationId not required", lambda s: s["components"]["schemas"]["ProblemDetails"]["required"].remove("correlationId"))
    case("C-6", "idempotencyKey property missing", lambda s: s["components"]["schemas"]["ProblemDetails"]["properties"].pop("idempotencyKey"))
    case("C-7", "POST without x-write-class", lambda s: s["paths"][create]["post"].pop("x-write-class"))
    case("C-7", "sensitive POST without Idempotency-Key", lambda s: s["paths"][create]["post"].update(
        {"parameters": [p for p in s["paths"][create]["post"]["parameters"] if p.get("$ref") != "#/components/parameters/IdempotencyKey"]}))
    case("C-7", "non-sensitive without reason", lambda s: s["paths"][create]["post"].update({"x-write-class": "non-sensitive"}))
    case("C-7", "command marked non-sensitive", lambda s: s["paths"][submit]["post"].update({"x-write-class": "non-sensitive", "x-write-class-reason": "x"}))

    def put_without_if_match(s):
        put = copy.deepcopy(s["paths"][create]["post"])
        put["operationId"] = "Project_ReplaceProject"
        s["paths"]["/api/v1/projects/{projectId}"] = {"put": put}
    case("C-8", "PUT without If-Match", put_without_if_match)
    case("C-9", "collection GET unpaged", lambda s: s["paths"]["/api/v1/risks"]["get"].update(
        {"parameters": [p for p in s["paths"]["/api/v1/risks"]["get"]["parameters"] if p.get("$ref") != "#/components/parameters/Page"]}))
    case("C-9", "200 not a page envelope", lambda s: s["components"]["schemas"]["RiskPage"]["properties"].pop("totalCount"))
    case("C-10", "sort without x-sortable", lambda s: s["paths"]["/api/v1/risks"]["get"].pop("x-sortable"))
    case("C-11", "inline response schema", lambda s: s["paths"]["/api/v1/risks"]["get"]["responses"]["200"]["content"]["application/json"].update({"schema": {"type": "object"}}))
    case("C-11", "snake_case property", lambda s: s["components"]["schemas"]["RiskSummary"]["properties"].update({"next_review": {"type": "string"}}))
    case("C-12", "201 without Location", lambda s: s["paths"][create]["post"]["responses"]["201"]["headers"].pop("Location"))
    case("C-12", "200 without X-Correlation-Id", lambda s: s["paths"]["/api/v1/risks"]["get"]["responses"]["200"]["headers"].pop("X-Correlation-Id"))
    case("C-13", "currency property", lambda s: s["components"]["schemas"]["ProjectDetail"]["properties"].update({"currency": {"type": "string"}}))
    case("C-13", "money as number", lambda s: s["components"]["schemas"]["ProjectDetail"]["properties"].update({"declaredBudgetSar": {"type": "number"}}))

    failures = 0
    for rule, name, mutate in cases:
        spec = copy.deepcopy(base)
        mutate(spec)
        findings.clear()
        check_openapi(spec)
        hit = any(f.startswith(rule + ":") for f in findings)
        print(f"  {'ok  ' if hit else 'MISS'} {rule:5} {name}")
        failures += 0 if hit else 1

    event_cases = [
        ("missing subject", lambda e: e.pop("subject")),
        ("eventType not <Module>.<Name>", lambda e: e.update({"eventType": "projectIntakeRecorded"})),
        ("messageKey mismatch", lambda e: e.update({"messageKey": "x"})),
        ("unknown envelope field", lambda e: e.update({"payloadVersion": 2})),
        ("occurredAt not UTC", lambda e: e.update({"occurredAt": "2026-09-20T09:12:44+03:00"})),
        ("idempotencyKey equals correlationId", lambda e: e.update({"idempotencyKey": e["correlationId"], "messageKey": f"{e['eventType']}:{e['correlationId']}"})),
    ]
    for name, mutate in event_cases:
        event = copy.deepcopy(sample_event)
        mutate(event)
        findings.clear()
        validate(envelope, event, "event", envelope)
        if event.get("messageKey") != f"{event.get('eventType')}:{event.get('idempotencyKey')}":
            find("EV", "messageKey")
        if event.get("idempotencyKey") == event.get("correlationId"):
            find("EV", "idempotencyKey equals correlationId")
        hit = bool(findings)
        print(f"  {'ok  ' if hit else 'MISS'} EV    {name}")
        failures += 0 if hit else 1

    findings.clear()
    print(f"self-test: {len(cases) + len(event_cases)} mutations, {failures} missed")
    return failures


def main(argv):
    if "--self-test" in argv:
        return 1 if self_test() else 0
    spec_path = Path(argv[1]).resolve() if len(argv) > 1 else SAMPLE_SPEC
    spec = json.loads(spec_path.read_text())
    check_openapi(spec)
    ops = sum(1 for pi in spec.get("paths", {}).values() for m in pi if m in METHODS)
    events = check_events(ENVELOPE_SCHEMA, EVENT_SAMPLES) if spec_path == SAMPLE_SPEC else 0
    for f in findings:
        print(f)
    print(f"{spec_path.name}: {ops} operations, {events} event samples, {len(findings)} findings")
    return 1 if findings else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
