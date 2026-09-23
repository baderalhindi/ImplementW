#!/usr/bin/env python3
"""A stand-in for the approved secret store, for the rotation rehearsal (TASK-019).

Speaks the one method the application uses — Secret Manager's `versions/latest:access` — over plain
HTTP on localhost, so the rotation in the validation cell can be rehearsed end to end before any
environment exists (ADR-001 R-1 to R-3, UGV-07). It is **not** a secret store: it holds its values in
a plain file, it is bound to the loopback interface, and nothing but
`infra/secrets/rehearse-rotation.sh` starts it.

    python3 infra/secrets/stub-store.py --port 8099 --token <token> --state <file>

The state file is `{"<secret id>": "<value>"}` and is re-read on every request, so writing it is what
"a new version becomes the latest enabled version" looks like from the application's side.
"""
import argparse
import base64
import json
import re
import sys
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

ACCESS = re.compile(r"^/v1/projects/[^/]+/secrets/([^/]+)/versions/latest:access$")


def handler_for(state_path, token):
    class Handler(BaseHTTPRequestHandler):
        protocol_version = "HTTP/1.1"

        def do_GET(self):  # noqa: N802 — BaseHTTPRequestHandler's naming
            match = ACCESS.match(self.path)
            if not match:
                return self.answer(404, {"error": {"message": "not an access request"}})
            if self.headers.get("Authorization") != f"Bearer {token}":
                return self.answer(401, {"error": {"message": "missing or wrong bearer token"}})

            secret_id = match.group(1)
            values = json.loads(Path(state_path).read_text(encoding="utf-8"))
            if secret_id not in values:
                return self.answer(404, {"error": {"message": f"no enabled version of {secret_id}"}})

            payload = base64.b64encode(values[secret_id].encode("utf-8")).decode("ascii")
            return self.answer(200, {"name": secret_id, "payload": {"data": payload}})

        def answer(self, status, document):
            body = json.dumps(document).encode("utf-8")
            self.send_response(status)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)

        def log_message(self, format, *args):
            # The id and the status, never the value: this server is a rehearsal of a store that
            # must not log what it serves (CTL-18).
            match = ACCESS.match(self.path)
            print(f"stub-store: read {match.group(1) if match else self.path}", file=sys.stderr, flush=True)

    return Handler


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--port", type=int, required=True)
    parser.add_argument("--token", required=True)
    parser.add_argument("--state", required=True)
    arguments = parser.parse_args()

    server = ThreadingHTTPServer(("127.0.0.1", arguments.port), handler_for(arguments.state, arguments.token))
    print(f"stub-store: listening on 127.0.0.1:{arguments.port}", file=sys.stderr, flush=True)
    server.serve_forever()


if __name__ == "__main__":
    main()
