#!/usr/bin/env python3
"""Compare two `terraform show -json` plan files (TASK-017, validation cell V-4).

The workbook asks for two consecutive plans with no manual change between them and zero diff. Two
things in Terraform's own JSON differ between any two runs and describe no change at all:

  - `timestamp`, the wall-clock moment the plan was produced;
  - the order of `relevant_attributes`, and of every `child_modules` list in `planned_values` and
    `prior_state`, both of which Terraform emits in map-iteration order. The second was found by
    TASK-021: two SIT plans identical in content listed the five modules in different orders.

Comparing the raw files reports a difference every time and so proves nothing. This compares what a
plan actually asserts — the resource changes, the planned values, the resolved configuration, the
variables and the output changes — and reports the first section that differs.

    python3 compare-plans.py plan1.json plan2.json
"""
import json
import sys
from pathlib import Path

# Everything a plan asserts. `prior_state` is included: if two consecutive plans disagree about the
# state they read, the refresh is not idempotent either.
SECTIONS = [
    "resource_changes",
    "planned_values",
    "prior_state",
    "output_changes",
    "configuration",
    "variables",
    "checks",
    "errored",
]

# Terraform metadata, not a planned change.
IGNORED = {"timestamp", "relevant_attributes", "format_version", "terraform_version"}


def load(path):
    try:
        return json.loads(Path(path).read_text())
    except (OSError, json.JSONDecodeError) as exc:
        sys.exit("cannot read {}: {}".format(path, exc))


def relevant_attributes_key(entry):
    return (entry.get("resource", ""), tuple(str(part) for part in entry.get("attribute", [])))


def order_modules(node):
    """Sort every `child_modules` list by address, at any depth. A module's address is unique within
    its parent, so this orders the list without merging or dropping anything."""
    if isinstance(node, dict):
        ordered = {key: order_modules(value) for key, value in node.items()}
        if isinstance(ordered.get("child_modules"), list):
            ordered["child_modules"] = sorted(ordered["child_modules"], key=lambda m: m.get("address", ""))
        return ordered
    if isinstance(node, list):
        return [order_modules(item) for item in node]
    return node


def main(argv):
    if len(argv) != 3:
        sys.exit("usage: compare-plans.py <plan1.json> <plan2.json>")

    first, second = order_modules(load(argv[1])), order_modules(load(argv[2]))
    differences = []

    for section in SECTIONS:
        if first.get(section) != second.get(section):
            differences.append(section)

    # Order-insensitive, because Terraform's order here is not stable and means nothing.
    one = sorted(first.get("relevant_attributes", []), key=relevant_attributes_key)
    two = sorted(second.get("relevant_attributes", []), key=relevant_attributes_key)
    if one != two:
        differences.append("relevant_attributes (as a set)")

    unexpected = (set(first) | set(second)) - set(SECTIONS) - IGNORED
    for key in sorted(unexpected):
        if first.get(key) != second.get(key):
            differences.append(key)

    if differences:
        print("the two plans differ in: {}".format(", ".join(differences)))
        for section in ("resource_changes",):
            if section in differences:
                addresses_one = {c["address"] for c in first.get(section, [])}
                addresses_two = {c["address"] for c in second.get(section, [])}
                only_one = sorted(addresses_one - addresses_two)
                only_two = sorted(addresses_two - addresses_one)
                if only_one:
                    print("  only in the first plan: {}".format(", ".join(only_one)))
                if only_two:
                    print("  only in the second plan: {}".format(", ".join(only_two)))
        return 1

    changes = first.get("resource_changes", [])
    actions = sorted({tuple(c["change"]["actions"]) for c in changes})
    print("identical: {} resource change(s), actions {}".format(
        len(changes), [list(a) for a in actions] or "none"))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
