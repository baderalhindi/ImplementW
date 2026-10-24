#!/usr/bin/env python3
"""The published-spec audit (TASK-094): does the published OpenAPI document describe the live API?

For every endpoint in the sample (spec-audit-sample.json) it checks three things against a running environment:

  D  definition — the live API's own document (GET /openapi/v1.json) defines the operation, and every schema the
     operation reaches, exactly as the published document does: the environment runs the contract that was published;
  Q  request    — the request the audit sends conforms to the published request schema, and the API accepts it;
  R  response   — the status the API answers is one the published document lists for the operation, and the body
     conforms to the schema published for that status: types, nullability, enums, required properties, patterns and
     formats, and no property the schema does not document.

An endpoint MATCHES when all three hold. The run passes when at least five endpoints were audited and every one matches.

    AUDIT_USERNAME=… AUDIT_PASSWORD=… python3 docs/api/spec-audit.py --base-url https://<DEV host> \\
        --spec <the openapi-v1 artifact's openapi.v1.json> --out artifacts/api-audit/<date>.json

The sample's first entry signs in with AUDIT_USERNAME/AUDIT_PASSWORD; its access token authenticates the rest. The audit
writes nothing an environment keeps but a sign-in, its audit event and one client error report. Standard library only.
"""

import argparse
import copy
import datetime
import json
import os
import re
import sys
import urllib.error
import urllib.parse
import urllib.request
import uuid
from pathlib import Path

HERE = Path(__file__).resolve().parent
MINIMUM_SAMPLE = 5
DATE_TIME = re.compile(r"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+-]\d{2}:\d{2})$")
DATE = re.compile(r"^\d{4}-\d{2}-\d{2}$")
VARIABLE = re.compile(r"\$\{([A-Z_]+)\}")


class Document:
    """An OpenAPI 3.1 document: its operations, and its schemas with each $ref followed."""

    def __init__(self, root):
        self.root = root
        self.schemas = root.get("components", {}).get("schemas", {})

    def operation(self, name):
        method, path = name.split(" ", 1)
        return self.root.get("paths", {}).get(path, {}).get(method.lower())

    def reached(self, node):
        """The names of every component schema node reaches, through $refs to any depth."""
        names, pending = set(), [node]
        while pending:
            current = pending.pop()
            if isinstance(current, dict):
                ref = current.get("$ref")
                if isinstance(ref, str):
                    name = ref.rsplit("/", 1)[-1]
                    if name not in names:
                        names.add(name)
                        pending.append(self.schemas.get(name))
                pending.extend(v for k, v in current.items() if k != "$ref")
            elif isinstance(current, list):
                pending.extend(current)
        return names

    def resolve(self, node):
        """(schema, types, nullable) with $refs followed and a nullable oneOf/anyOf reduced to its one schema."""
        nullable = False
        for _ in range(32):
            if not isinstance(node, dict):
                return None
            if "$ref" in node:
                node = self.schemas.get(node["$ref"].rsplit("/", 1)[-1])
                continue
            union = node.get("oneOf") or node.get("anyOf")
            if isinstance(union, list) and len(union) == 2 and sum(1 for s in union if s == {"type": "null"}) == 1:
                nullable = True
                node = next(s for s in union if s != {"type": "null"})
                continue
            types = node.get("type", [])
            types = set(types if isinstance(types, list) else [types])
            nullable |= "null" in types or None in node.get("enum", [0])
            return node, types - {"null"}, nullable
        return None

    def violations(self, value, schema_node, at="$"):
        """Where value departs from the schema: the shape check of R and Q."""
        resolved = self.resolve(schema_node)
        if resolved is None:
            return [f"{at}: no schema"]
        schema, types, nullable = resolved
        if value is None:
            return [] if nullable else [f"{at}: null, documented as not nullable"]

        kind = ("boolean" if isinstance(value, bool) else "object" if isinstance(value, dict)
                else "array" if isinstance(value, list) else "string" if isinstance(value, str)
                else "integer" if isinstance(value, int) or (isinstance(value, float) and value.is_integer() and "integer" in types)
                else "number")
        if types and kind not in types and not (kind == "integer" and "number" in types):
            return [f"{at}: {kind}, documented as {'|'.join(sorted(types))}"]

        found = []
        if "enum" in schema and value not in schema["enum"]:
            found.append(f"{at}: {json.dumps(value)} is not one of the documented values")
        if isinstance(value, str):
            if "pattern" in schema and not re.search(schema["pattern"], value):
                found.append(f"{at}: {json.dumps(value)} does not match {schema['pattern']}")
            found.extend(self._format(value, schema.get("format"), at))
        if isinstance(value, dict):
            properties = schema.get("properties", {})
            found.extend(f"{at}.{name}: required, absent" for name in schema.get("required", []) if name not in value)
            for name, item in value.items():
                found.extend(self.violations(item, properties[name], f"{at}.{name}") if name in properties
                             else [f"{at}.{name}: not documented"])
        if isinstance(value, list):
            for index, item in enumerate(value):
                found.extend(self.violations(item, schema.get("items"), f"{at}[{index}]"))
        return found

    @staticmethod
    def _format(value, fmt, at):
        if fmt == "uuid":
            try:
                uuid.UUID(value)
            except ValueError:
                return [f"{at}: {json.dumps(value)} is not a uuid"]
        if fmt == "date-time" and not DATE_TIME.match(value):
            return [f"{at}: {json.dumps(value)} is not an RFC 3339 date-time"]
        if fmt == "date" and not DATE.match(value):
            return [f"{at}: {json.dumps(value)} is not a date"]
        return []


def substitute(node):
    """${NAME} in the sample is the environment variable NAME — credentials never sit in the sample file."""
    if isinstance(node, str):
        return VARIABLE.sub(lambda m: os.environ.get(m.group(1)) or sys.exit(f"spec-audit: {m.group(1)} is not set"), node)
    if isinstance(node, dict):
        return {k: substitute(v) for k, v in node.items()}
    if isinstance(node, list):
        return [substitute(v) for v in node]
    return node


def send(base_url, method, path, query=None, body=None, token=None):
    url = base_url.rstrip("/") + path + ("?" + urllib.parse.urlencode(query) if query else "")
    headers = {"Accept": "application/json"}
    data = None
    if body is not None:
        data = json.dumps(body).encode()
        headers["Content-Type"] = "application/json"
    if token:
        headers["Authorization"] = f"Bearer {token}"
    request = urllib.request.Request(url, data=data, method=method, headers=headers)
    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            return response.status, response.headers, response.read()
    except urllib.error.HTTPError as error:
        return error.code, error.headers, error.read()


def definition_differences(published, live, name):
    """D: the operation and every schema it reaches, published against live."""
    mine, theirs = published.operation(name), live.operation(name)
    if mine is None:
        return ["not in the published document"]
    if theirs is None:
        return ["not in the live document"]
    found = [] if mine == theirs else ["operation differs"]
    for schema in sorted(published.reached(mine) | live.reached(theirs)):
        if published.schemas.get(schema) != live.schemas.get(schema):
            found.append(f"schema {schema} differs")
    return found


def audit(base_url, published, live, sample):
    results, token = [], None
    for entry in sample["samples"]:
        name = entry["operation"]
        method, path = name.split(" ", 1)
        operation = published.operation(name) or {}
        body = substitute(entry.get("body"))
        result = {"operation": name, "operationId": operation.get("operationId"), "module": (operation.get("tags") or ["?"])[0]}

        result["definition"] = definition_differences(published, live, name)

        request_schema = operation.get("requestBody", {}).get("content", {}).get("application/json", {}).get("schema")
        if body is None:
            result["request"] = [] if not operation.get("requestBody", {}).get("required") else ["the operation requires a body"]
        else:
            result["request"] = (published.violations(body, request_schema, "request") if request_schema
                                 else ["the operation documents no JSON request body"])

        status, headers, raw = send(base_url, method, path.format(**entry.get("path", {})), entry.get("query"), body,
                                    None if entry.get("signIn") else token)
        result["status"] = status
        documented = operation.get("responses", {}).get(str(status))
        found = []
        if documented is None:
            found.append(f"{status} is not a documented response ({', '.join(operation.get('responses', {}))})")
        elif raw:
            content = documented.get("content", {})
            media = (headers.get("Content-Type") or "").split(";")[0].strip()
            schema = (content.get(media) or content.get("application/json") or {}).get("schema")
            if schema is None:
                found.append(f"a {media} body, documented without one")
            elif media.endswith("json"):
                found.extend(published.violations(json.loads(raw), schema, "response"))
        elif documented.get("content"):
            found.append("no body, documented with one")
        # The session authenticates the rest whatever the sign-in's verdict, so each later row is judged on its own endpoint.
        if entry.get("signIn") and raw and (headers.get("Content-Type") or "").startswith("application/json"):
            token = json.loads(raw).get(sample.get("tokenProperty", "accessToken"))
        if not 200 <= status < 300:
            found.append(f"answered {status}: {raw[:200].decode(errors='replace')}")
        result["response"] = found
        result["match"] = not (result["definition"] or result["request"] or found)
        results.append(result)
    return results


def table(results):
    lines = ["| # | Operation | Module | D definition | Q request | Status | R response | Verdict |",
             "| --- | --- | --- | --- | --- | --- | --- | --- |"]
    for index, r in enumerate(results, 1):
        cell = lambda found: "same" if not found else "; ".join(found)[:300]
        lines.append(f"| {index} | `{r['operation']}` | {r['module']} | {cell(r['definition'])} | {cell(r['request'])} | "
                     f"{r['status']} | {cell(r['response'])} | {'MATCH' if r['match'] else 'MISMATCH'} |")
    return "\n".join(lines)


def main():
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("--base-url", required=True, help="the environment, e.g. https://<DEV host>")
    parser.add_argument("--spec", default=str(HERE / "openapi.v1.json"), help="the published document (the openapi-v1 artifact)")
    parser.add_argument("--sample", default=str(HERE / "spec-audit-sample.json"))
    parser.add_argument("--out", help="write the results, with the run's metadata, as JSON")
    args = parser.parse_args()

    published = Document(json.loads(Path(args.spec).read_text(encoding="utf-8")))
    sample = json.loads(Path(args.sample).read_text(encoding="utf-8"))
    status, _, raw = send(args.base_url, "GET", "/openapi/v1.json")
    if status != 200:
        sys.exit(f"spec-audit: {args.base_url}/openapi/v1.json answered {status}; is ApiDocumentation:Enabled set there?")
    live_root = json.loads(raw)
    live = Document(copy.deepcopy(live_root))

    results = audit(args.base_url, published, live, sample)
    print(table(results))
    matched = sum(r["match"] for r in results)
    verdict = len(results) >= MINIMUM_SAMPLE and matched == len(results)
    print(f"\n{matched} of {len(results)} sampled endpoints match the published document; "
          f"{'PASS' if verdict else 'FAIL'} (at least {MINIMUM_SAMPLE}, all matching)")
    if args.out:
        Path(args.out).parent.mkdir(parents=True, exist_ok=True)
        Path(args.out).write_text(json.dumps({
            "auditedAt": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
            "baseUrl": args.base_url,
            "publishedSpec": args.spec,
            "liveDocumentEqualsPublished": {k: v for k, v in live_root.items() if k != "servers"} == published.root,
            "results": results,
            "passed": verdict,
        }, indent=2) + "\n", encoding="utf-8")
    return 0 if verdict else 1


if __name__ == "__main__":
    sys.exit(main())
