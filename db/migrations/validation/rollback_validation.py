#!/usr/bin/env python3
"""Migration up/down validation at representative volume (TASK-089). Record: db/migrations/rollback-validation.md.

    db/migrations/validation/run.sh [--depth N|all]          # the usual entry point: builds and tears down its own stack
    rollback_validation.py --network NET --client CONTAINER --image IMAGE --output DIR [--depth N|all] [--keep]

DB_CONNECTION_STRING (Npgsql format) names a PostgreSQL 17 server, as seen from NET, where the role can CREATE DATABASE.
As in the integration tests' ThrowawayDatabase, only its server and credentials are used: the run creates its own
database there and drops it afterwards; the database the string names is never touched. Migrations run through the
release image's `migrate [<target>]` command, the one each environment's migration job runs; every SQL statement runs
through psql in CLIENT, a postgres:17 container on NET with the repository mounted at /repo.

The run, in order:
  1. build a reference ladder: a second database, with no data, migrated up one migration at a time from the bottom of
     the walk to the newest. Each level's schema checksum is what Up builds from below;
  2. migrate an empty database to the newest migration; load the platform seed, the e2e data set and the load test's
     representative volume (docs/testing/load-test/volume), then validate-data-integrity.sql;
  3. roll back the most recent migration and re-apply it, as an operator would in a non-PROD environment, running the
     integrity validator after each;
  4. walk down DEPTH migrations one at a time with the data in place, then up again one at a time to the newest;
  5. after a full walk (DEPTH = all), load the same data again into the re-applied schema.

At every state it records the schema checksum (SHA-256 of schema-snapshot.sql's lines, sorted by byte value), the history
table and a data fingerprint: per table the row count and, per column, an order-independent hash of (primary key, value).
A state passes when its history holds exactly the migrations of its level, when its checksum equals the reference ladder's
for that level, and when every column present before and after the step with the same type holds identical values in the
same number of rows. The only data a step may lose is what its migration's Down drops: whole tables and whole columns, each
listed in the results. Exit status 0 when every check passes, 1 otherwise.
"""
from __future__ import annotations

import argparse
import datetime
import hashlib
import json
import os
import subprocess
import sys
import time
from dataclasses import dataclass, field
from pathlib import Path

REPOSITORY = Path(__file__).resolve().parents[3]
MIGRATIONS = REPOSITORY / "src/backend/PMPlatform.Infrastructure/Persistence/Migrations"
HISTORY_TABLE = 'common."__EFMigrationsHistory"'
HISTORY_KEY = "common.__EFMigrationsHistory"
# The slowest step at volume takes seconds (rollback-validation.md §4); ten minutes means the command will not exit.
MIGRATE_TIMEOUT_SECONDS = 600

# Paths inside the client container, where run.sh mounts the repository at /repo.
SNAPSHOT_SQL = "/repo/db/migrations/validation/schema-snapshot.sql"
SEED = "/repo/db/seed/seed-master-data.sql"
VALIDATOR = "/repo/db/seed/validate-data-integrity.sql"
PROVISION = REPOSITORY / "src/frontend/e2e/stack/provision"
VOLUME = REPOSITORY / "docs/testing/load-test/volume"

COLUMNS_SQL = r"""
SELECT n.nspname || '|' || c.relname || '|' || a.attname || '|' || format_type(a.atttypid, a.atttypmod) || '|'
       || EXISTS (SELECT 1 FROM pg_index i WHERE i.indrelid = c.oid AND i.indisprimary AND a.attnum = ANY (i.indkey))
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
JOIN pg_attribute a ON a.attrelid = c.oid AND a.attnum > 0 AND NOT a.attisdropped
WHERE c.relkind = 'r' AND n.nspname NOT IN ('pg_catalog', 'information_schema') AND n.nspname NOT LIKE 'pg\_%'
ORDER BY n.nspname COLLATE "C", c.relname COLLATE "C", a.attname COLLATE "C"
"""


class ValidationError(Exception):
    """A command the run depends on failed; the run cannot continue."""


def migration_ids() -> list[str]:
    """The release's migrations in application order, from the repository the image was built from."""
    ids = sorted(p.stem for p in MIGRATIONS.glob("*_TASK-*.cs") if not p.name.endswith(".Designer.cs"))
    if not ids:
        raise ValidationError(f"no migrations under {MIGRATIONS}")
    return ids


def parse_connection_string(value: str) -> dict[str, str]:
    """The Npgsql keys the run needs, from a 'Key=Value;…' string."""
    aliases = {"host": "host", "server": "host", "port": "port", "database": "database", "username": "username",
               "user id": "username", "user": "username", "userid": "username", "password": "password"}
    parts: dict[str, str] = {}
    for pair in value.split(";"):
        if "=" in pair:
            key, _, item = pair.partition("=")
            if key.strip().lower() in aliases:
                parts[aliases[key.strip().lower()]] = item.strip()
    missing = {"host", "username", "password"} - parts.keys()
    if missing:
        raise ValidationError(f"DB_CONNECTION_STRING lacks {', '.join(sorted(missing))}")
    parts.setdefault("port", "5432")
    parts.setdefault("database", "postgres")
    return parts


def quote_identifier(name: str) -> str:
    return '"' + name.replace('"', '""') + '"'


def quote_literal(value: str) -> str:
    return "'" + value.replace("'", "''") + "'"


@dataclass
class Server:
    """The PostgreSQL server, the psql client container and the release image, all on one Docker network."""

    network: str
    client: str
    image: str
    connection: dict[str, str]

    def psql(self, database: str, sql: str | None = None, files: list[str] | None = None,
             single_transaction: bool = False) -> list[str]:
        command = ["docker", "exec", "-i", "-e", f"PGPASSWORD={self.connection['password']}", self.client,
                   "psql", "-X", "-q", "-At", "-v", "ON_ERROR_STOP=1",
                   "-h", self.connection["host"], "-p", self.connection["port"], "-U", self.connection["username"],
                   "-d", database]
        if single_transaction:
            command.append("--single-transaction")
        command += [f"--file={f}" for f in files or []]
        if sql is not None:
            command.append("--file=-")
        result = subprocess.run(command, input=sql, capture_output=True, text=True)
        if result.returncode != 0:
            raise ValidationError(f"psql on {database} failed ({result.returncode}): {result.stderr.strip()[-4000:]}")
        return [line for line in result.stdout.splitlines() if line]

    def migrate(self, database: str, target: str | None) -> float:
        """Runs the release image's `migrate [<target>]` against DATABASE; returns the wall-clock seconds."""
        connection = (f"Host={self.connection['host']};Port={self.connection['port']};Database={database};"
                      f"Username={self.connection['username']};Password={self.connection['password']}")
        # No --init: the job in an environment runs the image as it is, so a command that cannot exit is a finding here too.
        name = f"migration-validation-migrate-{os.getpid()}"
        command = ["docker", "run", "--rm", "--name", name, "--network", self.network,
                   "-e", "ASPNETCORE_ENVIRONMENT=Development", "-e", f"DB_CONNECTION_STRING={connection}",
                   self.image, "migrate"] + ([target] if target else [])
        started = time.monotonic()
        try:
            result = subprocess.run(command, capture_output=True, text=True, timeout=MIGRATE_TIMEOUT_SECONDS)
        except subprocess.TimeoutExpired as timeout:
            subprocess.run(["docker", "rm", "-f", name], capture_output=True)
            output = "".join(part.decode(errors="replace") if isinstance(part, bytes) else part
                             for part in (timeout.stdout or "", timeout.stderr or "")).strip()
            raise ValidationError(f"migrate {target or '(latest)'} did not exit within {MIGRATE_TIMEOUT_SECONDS} s: "
                                  f"{output[-4000:]}") from None
        elapsed = time.monotonic() - started
        if result.returncode != 0:
            lines = (result.stdout + result.stderr).splitlines()
            reason = [line.strip() for line in lines if " failed: " in line or "MessageText:" in line] or lines[-20:]
            raise ValidationError(f"migrate {target or '(latest)'} failed ({result.returncode}): {' | '.join(reason)[:4000]}")
        return round(elapsed, 1)


@dataclass
class State:
    """What a database holds at one point of the run."""

    history: list[str]
    schema_lines: list[str]
    tables: dict[str, dict] = field(default_factory=dict)

    @property
    def checksum(self) -> str:
        return hashlib.sha256("\n".join(self.schema_lines).encode()).hexdigest()


def take_state(server: Server, database: str, with_data: bool = True) -> State:
    history = server.psql(database, f"SELECT migration_id FROM {HISTORY_TABLE} ORDER BY migration_id")
    schema = sorted(server.psql(database, files=[SNAPSHOT_SQL]), key=lambda line: line.encode())
    state = State(history, schema)
    if with_data:
        state.tables = fingerprint(server, database)
    return state


def fingerprint(server: Server, database: str) -> dict[str, dict]:
    """Per table: row count, and per column its type and the sum of a 64-bit hash of (primary key, value) over every row."""
    columns: dict[str, list[tuple[str, str, bool]]] = {}
    for line in server.psql(database, COLUMNS_SQL):
        schema, table, column, type_, primary = line.split("|")
        columns.setdefault(f"{schema}.{table}", []).append((column, type_, primary == "t"))
    columns.pop(HISTORY_KEY, None)  # compared row by row through State.history instead

    queries = []
    for table, cols in columns.items():
        schema, name = table.split(".", 1)
        key_columns = [quote_identifier(c) for c, _, primary in cols if primary]
        key = f"concat_ws(',', {', '.join(f't.{c}::text' for c in key_columns)})" if key_columns else "''"
        hashes = [f"coalesce(sum(hashtextextended({key} || '#' || CASE WHEN t.{quote_identifier(c)} IS NULL THEN 'N' "
                  f"ELSE 'V' || t.{quote_identifier(c)}::text END, 0)::numeric), 0)" for c, _, _ in cols]
        queries.append(f"SELECT concat_ws('|', {quote_literal(table)}, count(*), {', '.join(hashes)}) "
                       f"FROM {quote_identifier(schema)}.{quote_identifier(name)} AS t;")

    tables: dict[str, dict] = {}
    for line in server.psql(database, "SET statement_timeout = 0;\n" + "\n".join(queries)):
        table, rows, *hashes = line.split("|")
        cols = columns[table]
        tables[table] = {"rows": int(rows), "columns": {c: [t, h] for (c, t, _), h in zip(cols, hashes, strict=True)}}
    return tables


@dataclass
class Comparison:
    """How the data changed between two states, and whether that change is allowed."""

    dropped_tables: dict[str, int] = field(default_factory=dict)
    created_tables: list[str] = field(default_factory=list)
    dropped_columns: list[str] = field(default_factory=list)
    added_columns: list[str] = field(default_factory=list)
    retyped_columns: list[str] = field(default_factory=list)
    verified_tables: int = 0
    verified_rows: int = 0
    violations: list[str] = field(default_factory=list)

    def as_dict(self) -> dict:
        return {
            "droppedTables": self.dropped_tables,
            "rowsInDroppedTables": sum(self.dropped_tables.values()),
            "createdTables": self.created_tables,
            "droppedColumns": self.dropped_columns,
            "addedColumns": self.added_columns,
            "retypedColumns": self.retyped_columns,
            "verifiedTables": self.verified_tables,
            "verifiedRows": self.verified_rows,
            "violations": self.violations,
        }


def compare(before: State, after: State, emptied_tables: frozenset[str] = frozenset(),
            reset_columns: frozenset[str] = frozenset()) -> Comparison:
    """
    Every table in both states keeps its row count, and every column in both with the same type keeps its values.
    EMPTIED_TABLES (dropped by a Down, then recreated by an Up) may come back empty; RESET_COLUMNS ('table.column', dropped
    by a Down, then re-added) may come back with other values. Anything else that differs is a violation.
    """
    result = Comparison()
    result.dropped_tables = {t: before.tables[t]["rows"] for t in sorted(before.tables.keys() - after.tables.keys())}
    result.created_tables = sorted(after.tables.keys() - before.tables.keys())
    for table in sorted(before.tables.keys() & after.tables.keys()):
        old, new = before.tables[table], after.tables[table]
        if table in emptied_tables:
            if new["rows"] != 0:
                result.violations.append(f"{table}: recreated with {new['rows']} rows, expected empty")
            continue
        result.dropped_columns += [f"{table}.{c}" for c in sorted(old["columns"].keys() - new["columns"].keys())]
        result.added_columns += [f"{table}.{c}" for c in sorted(new["columns"].keys() - old["columns"].keys())]
        if old["rows"] != new["rows"]:
            result.violations.append(f"{table}: {old['rows']} rows before, {new['rows']} after")
            continue
        changed = False
        for column in sorted(old["columns"].keys() & new["columns"].keys()):
            (old_type, old_hash), (new_type, new_hash) = old["columns"][column], new["columns"][column]
            if old_type != new_type:
                result.retyped_columns.append(f"{table}.{column}: {old_type} -> {new_type}")
            elif old_hash != new_hash and f"{table}.{column}" not in reset_columns:
                result.violations.append(f"{table}.{column}: values changed")
                changed = True
        if not changed:
            result.verified_tables += 1
            result.verified_rows += new["rows"]
    return result


def history_violation(state: State, expected: list[str]) -> list[str]:
    if state.history == expected:
        return []
    return [f"history holds {len(state.history)} migrations ending {state.history[-1:] or ['none']}, "
            f"expected {len(expected)} ending {expected[-1:] or ['none']}"]


def schema_diff(expected: State, actual: State) -> list[str]:
    missing = sorted(set(expected.schema_lines) - set(actual.schema_lines))
    extra = sorted(set(actual.schema_lines) - set(expected.schema_lines))
    return [f"schema differs: missing {line}" for line in missing[:20]] + [f"schema differs: extra {line}" for line in extra[:20]]


def load_data(server: Server, database: str) -> float:
    """The load stack's chain (docs/testing/load-test/compose.yml): seed, e2e data set, volume, integrity check, one transaction."""
    files = [SEED] + [f"/repo/{p.relative_to(REPOSITORY)}" for p in sorted(PROVISION.glob("*.sql"))]
    files += [f"/repo/{p.relative_to(REPOSITORY)}" for p in sorted(VOLUME.glob("*.sql"))] + [VALIDATOR]
    started = time.monotonic()
    server.psql(database, files=files, single_transaction=True)
    return round(time.monotonic() - started, 1)


def integrity_violations(server: Server, database: str) -> list[str]:
    try:
        server.psql(database, files=[VALIDATOR])
        return []
    except ValidationError as error:
        return [f"validate-data-integrity.sql: {error}"]


def summarise(state: State) -> dict:
    return {"tables": len(state.tables), "rows": sum(t["rows"] for t in state.tables.values())}


def run(server: Server, depth_argument: str, output: Path, keep: bool) -> int:
    migrations = migration_ids()
    count = len(migrations)
    depth = count if depth_argument == "all" else int(depth_argument)
    if not 1 <= depth <= count:
        raise ValidationError(f"--depth must be between 1 and {count} (or all), got {depth_argument}")
    bottom = count - depth

    def level_target(level: int) -> str:
        """The `migrate` target that leaves the first LEVEL migrations applied."""
        return migrations[level - 1] if level > 0 else "0"

    stamp = f"{datetime.datetime.now(datetime.UTC):%Y%m%d%H%M%S}"
    database, reference_database = f"migration_validation_{stamp}", f"migration_reference_{stamp}"
    maintenance = server.connection["database"]
    results: dict = {
        "task": "TASK-089",
        "startedAt": datetime.datetime.now(datetime.UTC).isoformat(timespec="seconds"),
        "commit": subprocess.run(["git", "-C", str(REPOSITORY), "rev-parse", "HEAD"], capture_output=True, text=True).stdout.strip(),
        "image": server.image,
        "migrations": count,
        "depth": depth,
        "failures": [],
    }
    failures: list[str] = results["failures"]
    output.mkdir(parents=True, exist_ok=True)

    def record(failure_list: list[str], where: str) -> None:
        failures.extend(f"{where}: {f}" for f in failure_list)

    def schema_check(state: State, level: int) -> list[str]:
        """A state at LEVEL must have the schema the reference ladder built for LEVEL from below, with no data."""
        if state.checksum == reference[level].checksum:
            return []
        return [f"checksum {state.checksum[:12]} is not the reference {reference[level].checksum[:12]} for "
                f"{level_target(level)}"] + schema_diff(reference[level], state)

    try:
        for name in (database, reference_database):
            server.psql(maintenance, f"CREATE DATABASE {quote_identifier(name)}")
        results["server"] = server.psql(database, "SHOW server_version")[0]

        # 1. The reference ladder: a database with no data, migrated up one migration at a time from the bottom of the
        #    walk. Each level's schema is what Up builds; every state below is judged against it.
        reference: dict[int, State] = {}
        for level in range(bottom, count + 1):
            server.migrate(reference_database, level_target(level))
            reference[level] = take_state(server, reference_database, with_data=False)
            record(history_violation(reference[level], migrations[:level]), f"reference {level_target(level)}")
        print(f"reference ladder: levels {bottom} to {count}, newest {reference[count].checksum[:12]}", flush=True)

        # 2. Newest migration, then the representative volume.
        results["migrateUpSeconds"] = server.migrate(database, None)
        top = take_state(server, database, with_data=False)
        record(history_violation(top, migrations) + schema_check(top, count), "migrate up")
        load_seconds = load_data(server, database)
        top = take_state(server, database)
        record(schema_check(top, count), "volume load")
        size = server.psql(database, "SELECT pg_database_size(current_database())")[0]
        results["volume"] = {"loadSeconds": load_seconds, "databaseBytes": int(size), **summarise(top)}
        print(f"loaded {results['volume']['rows']:,} rows in {results['volume']['tables']} tables "
              f"({int(size) / 2**20:,.0f} MiB) in {load_seconds}s; schema {top.checksum[:12]}", flush=True)

        # 3. The most recent migration: roll back, check, re-apply, check (the operator's rehearsal).
        rolled_back_seconds = server.migrate(database, level_target(count - 1))
        rolled_back = take_state(server, database)
        down = compare(top, rolled_back)
        rehearsal_failures = history_violation(rolled_back, migrations[:-1]) + schema_check(rolled_back, count - 1)
        rehearsal_failures += down.violations + integrity_violations(server, database)
        reapplied_seconds = server.migrate(database, None)
        reapplied = take_state(server, database)
        up = compare(top, reapplied, emptied_tables=frozenset(down.dropped_tables),
                     reset_columns=frozenset(down.dropped_columns + [c.split(":")[0] for c in down.retyped_columns]))
        rehearsal_failures += history_violation(reapplied, migrations) + schema_check(reapplied, count)
        rehearsal_failures += up.violations + integrity_violations(server, database)
        record(rehearsal_failures, f"most recent ({migrations[-1]})")
        results["mostRecent"] = {
            "migration": migrations[-1],
            "rollbackSeconds": rolled_back_seconds,
            "reapplySeconds": reapplied_seconds,
            "checksumBefore": top.checksum,
            "checksumRolledBack": rolled_back.checksum,
            "checksumReapplied": reapplied.checksum,
            "rollback": down.as_dict(),
            "reapply": up.as_dict(),
            "passed": not rehearsal_failures,
        }
        print(f"most recent {migrations[-1]}: rolled back in {rolled_back_seconds}s, "
              f"{down.verified_tables} tables / {down.verified_rows:,} rows unchanged, "
              f"{len(down.dropped_tables)} tables dropped; re-applied in {reapplied_seconds}s; "
              f"{'pass' if not rehearsal_failures else 'FAIL'}", flush=True)

        # 4. Down, one migration at a time, with the data in place; then up again, one at a time.
        previous = reapplied
        steps = []
        walk = [("down", level - 1, migrations[level - 1]) for level in range(count, bottom, -1)]
        walk += [("up", level, migrations[level - 1]) for level in range(bottom + 1, count + 1)]
        for direction, level, migration in walk:
            seconds = server.migrate(database, level_target(level))
            state = take_state(server, database)
            change = compare(previous, state)
            step_failures = history_violation(state, migrations[:level]) + schema_check(state, level) + change.violations
            record(step_failures, f"{direction} {migration}")
            steps.append({"direction": direction, "migration": migration, "seconds": seconds,
                          "checksumAfter": state.checksum, **change.as_dict(), "passed": not step_failures})
            print(f"{direction} {migration}: {seconds}s, {change.verified_tables} tables / {change.verified_rows:,} rows "
                  f"unchanged, {len(change.dropped_tables)} dropped ({sum(change.dropped_tables.values()):,} rows)"
                  f"{'' if not step_failures else ' FAIL'}", flush=True)
            previous = state
        results["steps"] = steps
        results["cycle"] = {
            "checksumUp": top.checksum,
            "checksumReapplied": previous.checksum,
            "identical": top.checksum == previous.checksum,
            "levelsCompared": 2 * depth,
            "levelsIdentical": sum(s["checksumAfter"] == reference[level].checksum
                                   for s, (_, level, _) in zip(steps, walk, strict=True)),
        }
        # The checksum registry: the schema each migration leaves, as the reference ladder built it.
        results["checksums"] = [{"migration": level_target(level), "checksum": reference[level].checksum}
                                for level in range(bottom, count + 1)]

        # 5. After a full walk, the re-applied schema takes the same data again.
        if depth == count:
            reload_seconds = load_data(server, database)
            reloaded = take_state(server, database)
            differing = [f"{t}: {top.tables[t]['rows']} rows first, {reloaded.tables.get(t, {}).get('rows')} on reload"
                         for t in top.tables if reloaded.tables.get(t, {}).get("rows") != top.tables[t]["rows"]]
            record(differing, "reload")
            results["reload"] = {"loadSeconds": reload_seconds, **summarise(reloaded), "passed": not differing}
            print(f"reload: {summarise(reloaded)['rows']:,} rows in {reload_seconds}s; "
                  f"{'same row count in every table' if not differing else 'DIFFERS'}", flush=True)
    except ValidationError as error:
        failures.append(f"aborted: {error}")
    finally:
        results["finishedAt"] = datetime.datetime.now(datetime.UTC).isoformat(timespec="seconds")
        results["passed"] = not failures
        (output / "results.json").write_text(json.dumps(results, indent=2) + "\n")
        if not keep:
            for name in (database, reference_database):
                server.psql(maintenance, f"DROP DATABASE IF EXISTS {quote_identifier(name)} WITH (FORCE)")

    print(f"{'PASS' if not failures else 'FAIL'}: results in {output / 'results.json'}")
    for failure in failures:
        print(f"  {failure}", file=sys.stderr)
    return 0 if not failures else 1


def summary(results: dict) -> str:
    """A results file as Markdown, for a job summary or the record."""
    def verdict(passed: bool | None) -> str:
        return "pass" if passed else "**FAIL**"

    lines = [f"### Migration up/down validation — {verdict(results.get('passed'))}", "",
             f"Commit `{results.get('commit', '')[:12]}`, PostgreSQL {results.get('server', '?')}, "
             f"{results.get('migrations')} migrations, depth {results.get('depth')}.", ""]
    volume = results.get("volume")
    if volume:
        lines += [f"Volume: {volume['rows']:,} rows in {volume['tables']} tables, "
                  f"{volume['databaseBytes'] / 2**20:,.0f} MiB, loaded in {volume['loadSeconds']} s.", ""]
    recent = results.get("mostRecent")
    if recent:
        down, up = recent["rollback"], recent["reapply"]
        lines += ["| Most recent migration | Rollback s | Tables / rows unchanged | Dropped tables (rows) | Re-apply s | Result |",
                  "| --- | ---: | ---: | ---: | ---: | --- |",
                  f"| `{recent['migration']}` | {recent['rollbackSeconds']} | {down['verifiedTables']} / {down['verifiedRows']:,} | "
                  f"{len(down['droppedTables'])} ({down['rowsInDroppedTables']:,}) | {recent['reapplySeconds']} | {verdict(recent['passed'])} |", ""]
    cycle = results.get("cycle")
    if cycle:
        steps = results["steps"]
        downs = [s for s in steps if s["direction"] == "down"]
        lines += [f"Cycle: up `{cycle['checksumUp'][:12]}`, after down and up again `{cycle['checksumReapplied'][:12]}` — "
                  f"{'identical' if cycle['identical'] else '**different**'}; {cycle['levelsIdentical']} of "
                  f"{cycle['levelsCompared']} states of the walk match the reference ladder.",
                  f"Down at volume: {len(downs)} steps, {sum(s['seconds'] for s in downs):,.0f} s, "
                  f"{sum(s['rowsInDroppedTables'] for s in downs):,} rows in dropped tables, "
                  f"{sum(len(s['violations']) for s in steps)} data violations.", ""]
    reload = results.get("reload")
    if reload:
        lines += [f"Reload into the re-applied schema: {reload['rows']:,} rows in {reload['loadSeconds']} s — {verdict(reload['passed'])}.", ""]
    lines += [f"- {f}" for f in results.get("failures", [])]
    return "\n".join(lines).rstrip() + "\n"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("--summary", type=Path, help="print a results.json as Markdown and exit")
    parser.add_argument("--network", help="Docker network the server, client and migrations share")
    parser.add_argument("--client", help="running postgres:17 container with the repository at /repo")
    parser.add_argument("--image", help="the release image (infra/docker/api.Dockerfile) built from this tree")
    parser.add_argument("--output", type=Path, help="directory for results.json")
    parser.add_argument("--depth", default="all", help="migrations to walk down and up again: a number, or all (default)")
    parser.add_argument("--keep", action="store_true", help="keep the database afterwards, for inspection")
    args = parser.parse_args()
    if args.summary:
        print(summary(json.loads(args.summary.read_text())), end="")
        return 0
    if not (args.network and args.client and args.image and args.output):
        parser.error("--network, --client, --image and --output are required to run")
    connection = os.environ.get("DB_CONNECTION_STRING")
    if not connection:
        print("DB_CONNECTION_STRING is unset: name a PostgreSQL 17 server reachable on the network", file=sys.stderr)
        return 2
    server = Server(args.network, args.client, args.image, parse_connection_string(connection))
    try:
        return run(server, args.depth, args.output, args.keep)
    except ValidationError as error:
        print(f"FAIL: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
