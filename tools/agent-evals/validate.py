#!/usr/bin/env python3
"""Validate Aqua's skill structure, routing fixtures, result schema, and claims."""

from __future__ import annotations

import hashlib
import json
import re
import sys
from pathlib import Path
from typing import Any
from urllib.parse import unquote


ROOT = Path(__file__).resolve().parents[2]
SKILLS_ROOT = ROOT / ".agents" / "skills"
CASES_PATH = ROOT / "docs" / "agent-evals" / "cases" / "skill-routing.json"
RESULT_SCHEMA_PATH = ROOT / "docs" / "agent-evals" / "result.schema.json"
AUTHORITY_PATH = (
    ROOT / "docs" / "aqua-system" / "07-verification-decision-and-risk-register.md"
)
EXPECTED_SKILLS = {
    "resolve-authority",
    "diagnose-bug",
    "review-change",
    "verify-stateful-change",
    "validate-evidence",
    "verify-workflow",
}
NAME_RE = re.compile(r"^[a-z0-9]+(?:-[a-z0-9]+)*$")
SHA40_RE = re.compile(r"^[0-9a-f]{40}$")
SHA64_RE = re.compile(r"^[0-9a-f]{64}$")
LINK_RE = re.compile(r"(?<!!)\[[^\]]+\]\(([^)]+)\)")
MUTABLE_FIXTURE_RE = re.compile(
    r"AQG-V2-D\d+|\bD(?:03B|08|09|10)\b|5/25/125|provider finality",
    re.IGNORECASE,
)
CASE_KEYS = {
    "id",
    "trigger_expectation",
    "skills_under_test",
    "expected_skills",
    "forbidden_skills",
    "authority_mode",
    "prompt",
    "expected_assertions",
}
ASSERTION_KEYS = {"id", "text"}
SUPPORTED_SCHEMA_KEYS = {
    "$schema",
    "title",
    "type",
    "additionalProperties",
    "required",
    "properties",
    "enum",
    "pattern",
    "minLength",
    "minimum",
    "minItems",
    "uniqueItems",
    "items",
}
ROOT_REQUIRED_PHRASES = (
    "Backend authorization is authoritative",
    "UI visibility is not authorization",
    "direct API or legacy paths must not bypass",
    "Changes to shared durable state must preserve atomicity",
    "database constraints",
    "idempotency",
    "retry safety",
    "concurrency correctness",
    "Migrations and historical operations must not fabricate or destroy",
    "SQLite or mock evidence alone cannot establish migration safety",
    "Never make tests or validation green",
)
AUTHORITY_REQUIRED_PHRASES = (
    "`UNRESOLVED`",
    "`PROPOSED`",
    "`CONFIRMED`",
    "`SUPERSEDED`",
    "decision ID",
    "authorizing owner identity or role",
    "confirmation date",
    "durable source or evidence locator",
    "effective boundary",
    "superseded decision ID",
)


class ValidationError(Exception):
    pass


def canonical_json(value: Any) -> str:
    return json.dumps(value, ensure_ascii=False, separators=(",", ":"), sort_keys=True)


def case_hash(case: dict[str, Any]) -> str:
    return hashlib.sha256(canonical_json(case).encode("utf-8")).hexdigest()


def parse_frontmatter(path: Path) -> dict[str, str]:
    text = path.read_text(encoding="utf-8")
    lines = text.splitlines()
    if not lines or lines[0] != "---":
        raise ValidationError(f"{path.relative_to(ROOT)}: missing opening frontmatter")
    try:
        end = lines.index("---", 1)
    except ValueError as exc:
        raise ValidationError(
            f"{path.relative_to(ROOT)}: missing closing frontmatter"
        ) from exc

    metadata: dict[str, str] = {}
    for line in lines[1:end]:
        if not line.strip():
            continue
        if ":" not in line:
            raise ValidationError(
                f"{path.relative_to(ROOT)}: unsupported frontmatter line {line!r}"
            )
        key, value = line.split(":", 1)
        key = key.strip()
        value = value.strip().strip('"').strip("'")
        if key in metadata:
            raise ValidationError(f"{path.relative_to(ROOT)}: duplicate {key!r}")
        metadata[key] = value

    if set(metadata) != {"name", "description"}:
        raise ValidationError(
            f"{path.relative_to(ROOT)}: format-portable frontmatter must contain "
            "only name and description"
        )
    return metadata


def validate_skills() -> None:
    skill_files = sorted(SKILLS_ROOT.glob("*/SKILL.md"))
    discovered: dict[str, Path] = {}
    for path in skill_files:
        metadata = parse_frontmatter(path)
        name = metadata["name"]
        description = metadata["description"]
        if not NAME_RE.fullmatch(name) or not 1 <= len(name) <= 64:
            raise ValidationError(f"{path.relative_to(ROOT)}: invalid skill name {name!r}")
        if path.parent.name != name:
            raise ValidationError(
                f"{path.relative_to(ROOT)}: name does not match directory"
            )
        if not 1 <= len(description) <= 1024:
            raise ValidationError(
                f"{path.relative_to(ROOT)}: description must be 1-1024 characters"
            )
        if name in discovered:
            raise ValidationError(
                f"duplicate skill ID {name!r}: {discovered[name]} and {path}"
            )
        discovered[name] = path

    if set(discovered) != EXPECTED_SKILLS:
        missing = sorted(EXPECTED_SKILLS - set(discovered))
        extra = sorted(set(discovered) - EXPECTED_SKILLS)
        raise ValidationError(f"skill set mismatch; missing={missing}, extra={extra}")

    root_agents = (ROOT / "AGENTS.md").read_text(encoding="utf-8")
    for phrase in ROOT_REQUIRED_PHRASES:
        if phrase not in root_agents:
            raise ValidationError(f"AGENTS.md: missing no-skill safeguard {phrase!r}")
    for name in EXPECTED_SKILLS:
        if f"`{name}`" not in root_agents:
            raise ValidationError(f"AGENTS.md: dangling or absent route for {name}")

    searchable = root_agents + "\n" + "\n".join(
        path.read_text(encoding="utf-8") for path in skill_files
    )
    retired_skill = "semantic-" + "integrity-review"
    if retired_skill in searchable:
        raise ValidationError(f"retired standalone {retired_skill} reference found")


def require_string_list(case_id: str, field: str, value: Any) -> list[str]:
    if not isinstance(value, list) or not all(isinstance(item, str) for item in value):
        raise ValidationError(f"{case_id}: {field} must be a string array")
    if len(value) != len(set(value)):
        raise ValidationError(f"{case_id}: {field} contains duplicates")
    return value


def validate_cases() -> dict[str, dict[str, Any]]:
    data = json.loads(CASES_PATH.read_text(encoding="utf-8"))
    if set(data) != {"version", "authority_drift_policy", "cases"}:
        raise ValidationError(f"{CASES_PATH.relative_to(ROOT)}: invalid envelope fields")
    if data["version"] != 2 or not isinstance(data["cases"], list):
        raise ValidationError(f"{CASES_PATH.relative_to(ROOT)}: invalid envelope")
    if not isinstance(data["authority_drift_policy"], str) or not data[
        "authority_drift_policy"
    ].strip():
        raise ValidationError("routing cases: authority_drift_policy is required")

    cases: dict[str, dict[str, Any]] = {}
    coverage = {name: set() for name in EXPECTED_SKILLS}
    compositions = 0
    for case in data["cases"]:
        if not isinstance(case, dict) or set(case) != CASE_KEYS:
            raise ValidationError("routing case has missing or unknown fields")
        case_id = case["id"]
        if not isinstance(case_id, str) or not NAME_RE.fullmatch(case_id):
            raise ValidationError(f"invalid case ID {case_id!r}")
        if case_id in cases:
            raise ValidationError(f"duplicate case ID {case_id!r}")

        trigger = case["trigger_expectation"]
        if trigger not in {"positive", "negative", "composition"}:
            raise ValidationError(f"{case_id}: invalid trigger expectation")
        under_test = require_string_list(case_id, "skills_under_test", case["skills_under_test"])
        expected = require_string_list(case_id, "expected_skills", case["expected_skills"])
        forbidden = require_string_list(case_id, "forbidden_skills", case["forbidden_skills"])
        if not under_test:
            raise ValidationError(f"{case_id}: skills_under_test cannot be empty")
        for field, skills in (
            ("skills_under_test", under_test),
            ("expected_skills", expected),
            ("forbidden_skills", forbidden),
        ):
            unknown = set(skills) - EXPECTED_SKILLS
            if unknown:
                raise ValidationError(f"{case_id}: {field} has unknown skills {sorted(unknown)}")
        if set(expected) & set(forbidden):
            raise ValidationError(f"{case_id}: expected and forbidden skills overlap")

        if trigger == "positive":
            if not set(under_test).issubset(expected):
                raise ValidationError(f"{case_id}: positive target must be expected")
        elif trigger == "negative":
            if not set(under_test).issubset(forbidden):
                raise ValidationError(f"{case_id}: negative target must be forbidden")
        else:
            if len(expected) < 2 or not set(under_test).issubset(expected):
                raise ValidationError(f"{case_id}: composition must expect every target skill")
            compositions += 1

        authority_mode = case["authority_mode"]
        if authority_mode not in {"stable-synthetic", "current-authority"}:
            raise ValidationError(f"{case_id}: invalid authority_mode")
        prompt = case["prompt"]
        if not isinstance(prompt, str) or not prompt.strip():
            raise ValidationError(f"{case_id}: prompt is required")
        if authority_mode == "stable-synthetic" and MUTABLE_FIXTURE_RE.search(prompt):
            raise ValidationError(
                f"{case_id}: mutable authority term requires current-authority mode"
            )

        assertions = case["expected_assertions"]
        if not isinstance(assertions, list) or not assertions:
            raise ValidationError(f"{case_id}: expected assertions are required")
        assertion_ids: set[str] = set()
        for assertion in assertions:
            if not isinstance(assertion, dict) or set(assertion) != ASSERTION_KEYS:
                raise ValidationError(f"{case_id}: invalid assertion fields")
            assertion_id = assertion["id"]
            if not isinstance(assertion_id, str) or not NAME_RE.fullmatch(assertion_id):
                raise ValidationError(f"{case_id}: invalid assertion ID {assertion_id!r}")
            if assertion_id in assertion_ids:
                raise ValidationError(f"{case_id}: duplicate assertion ID {assertion_id}")
            assertion_ids.add(assertion_id)
            if not isinstance(assertion["text"], str) or not assertion["text"].strip():
                raise ValidationError(f"{case_id}: assertion text is required")
        if authority_mode == "current-authority" and "resolves-current-authority" not in assertion_ids:
            raise ValidationError(
                f"{case_id}: current-authority case must assert authority resolution"
            )

        for skill in under_test:
            coverage[skill].add(trigger)
        cases[case_id] = case

    for skill, triggers in coverage.items():
        if not {"positive", "negative"}.issubset(triggers):
            raise ValidationError(f"{skill}: missing positive or negative boundary case")
    if compositions < 3:
        raise ValidationError("at least three composition cases are required")
    return cases


def validate_supported_schema(schema: dict[str, Any], path: str = "$") -> None:
    unknown = set(schema) - SUPPORTED_SCHEMA_KEYS
    if unknown:
        raise ValidationError(f"result schema {path}: unsupported keywords {sorted(unknown)}")
    properties = schema.get("properties", {})
    if properties and not isinstance(properties, dict):
        raise ValidationError(f"result schema {path}: properties must be an object")
    for name, child in properties.items():
        if not isinstance(child, dict):
            raise ValidationError(f"result schema {path}.{name}: property must be a schema")
        validate_supported_schema(child, f"{path}.{name}")
    items = schema.get("items")
    if items is not None:
        if not isinstance(items, dict):
            raise ValidationError(f"result schema {path}: items must be a schema")
        validate_supported_schema(items, f"{path}[]")


def matches_json_type(value: Any, expected: str) -> bool:
    if expected == "null":
        return value is None
    if expected == "boolean":
        return isinstance(value, bool)
    if expected == "integer":
        return isinstance(value, int) and not isinstance(value, bool)
    if expected == "number":
        return isinstance(value, (int, float)) and not isinstance(value, bool)
    if expected == "string":
        return isinstance(value, str)
    if expected == "array":
        return isinstance(value, list)
    if expected == "object":
        return isinstance(value, dict)
    raise ValidationError(f"result schema uses unsupported type {expected!r}")


def validate_json_schema(value: Any, schema: dict[str, Any], path: str = "$") -> None:
    if "enum" in schema and value not in schema["enum"]:
        raise ValidationError(f"{path}: value {value!r} is not in the allowed enum")

    expected_types = schema.get("type")
    if expected_types is not None:
        if isinstance(expected_types, str):
            expected_types = [expected_types]
        if not isinstance(expected_types, list) or not all(
            isinstance(item, str) for item in expected_types
        ):
            raise ValidationError(f"result schema {path}: invalid type declaration")
        if not any(matches_json_type(value, item) for item in expected_types):
            raise ValidationError(f"{path}: expected type {expected_types}, got {type(value).__name__}")

    if isinstance(value, dict):
        required = schema.get("required", [])
        missing = set(required) - set(value)
        if missing:
            raise ValidationError(f"{path}: missing required fields {sorted(missing)}")
        properties = schema.get("properties", {})
        if schema.get("additionalProperties") is False:
            unknown = set(value) - set(properties)
            if unknown:
                raise ValidationError(f"{path}: unknown fields {sorted(unknown)}")
        for name, child in properties.items():
            if name in value:
                validate_json_schema(value[name], child, f"{path}.{name}")

    if isinstance(value, list):
        if len(value) < schema.get("minItems", 0):
            raise ValidationError(f"{path}: too few items")
        if schema.get("uniqueItems"):
            serialized = [canonical_json(item) for item in value]
            if len(serialized) != len(set(serialized)):
                raise ValidationError(f"{path}: items must be unique")
        if "items" in schema:
            for index, item in enumerate(value):
                validate_json_schema(item, schema["items"], f"{path}[{index}]")

    if isinstance(value, str):
        if len(value) < schema.get("minLength", 0):
            raise ValidationError(f"{path}: string is too short")
        pattern = schema.get("pattern")
        if pattern is not None and re.fullmatch(pattern, value) is None:
            raise ValidationError(f"{path}: string does not match {pattern!r}")

    if isinstance(value, (int, float)) and not isinstance(value, bool):
        minimum = schema.get("minimum")
        if minimum is not None and value < minimum:
            raise ValidationError(f"{path}: value is below minimum {minimum}")


def validate_result_data(
    data: dict[str, Any],
    source: str,
    cases: dict[str, dict[str, Any]],
    schema: dict[str, Any],
) -> None:
    validate_json_schema(data, schema)
    case = cases.get(data["case_id"])
    if case is None:
        raise ValidationError(f"{source}: unknown case_id {data['case_id']!r}")
    if data["case_definition_sha256"] != case_hash(case):
        raise ValidationError(f"{source}: case definition hash does not match selected case")
    if data["trigger_expectation"] != case["trigger_expectation"]:
        raise ValidationError(f"{source}: trigger expectation does not match case")
    if data["skills_under_test"] != case["skills_under_test"]:
        raise ValidationError(f"{source}: skills_under_test does not match case")
    if data["expected_skills"] != case["expected_skills"]:
        raise ValidationError(f"{source}: expected_skills does not match case")
    unknown_loaded = set(data["skills_loaded"]) - EXPECTED_SKILLS
    if unknown_loaded:
        raise ValidationError(f"{source}: unknown loaded skills {sorted(unknown_loaded)}")

    commit = data["commit"]
    if not SHA40_RE.fullmatch(commit):
        raise ValidationError(f"{source}: commit must be a full lowercase SHA")
    clean_identity = f"git:{commit}"
    dirty_identity_re = re.compile(
        rf"^git:{re.escape(commit)}\+diff-sha256:[0-9a-f]{{64}}$"
    )
    if data["worktree_dirty"]:
        if dirty_identity_re.fullmatch(data["snapshot_identity"]) is None:
            raise ValidationError(f"{source}: dirty snapshot identity is invalid")
    elif data["snapshot_identity"] != clean_identity:
        raise ValidationError(f"{source}: clean snapshot identity must equal {clean_identity}")

    expected_assertion_ids = [item["id"] for item in case["expected_assertions"]]
    actual_assertion_ids = [item["id"] for item in data["assertions"]]
    if actual_assertion_ids != expected_assertion_ids:
        raise ValidationError(f"{source}: assertion IDs/order do not match selected case")

    assertion_outcomes = [item["outcome"] for item in data["assertions"]]
    if data["outcome"] == "PASS":
        if any(outcome != "PASS" for outcome in assertion_outcomes):
            raise ValidationError(f"{source}: overall PASS requires every assertion to pass")
        if data["skills_loaded"] != case["expected_skills"]:
            raise ValidationError(
                f"{source}: overall PASS requires exactly every expected skill to load"
            )
        if set(data["skills_loaded"]) & set(case["forbidden_skills"]):
            raise ValidationError(f"{source}: forbidden skill loaded in passing result")
    if "FAIL" in assertion_outcomes and data["outcome"] == "PASS":
        raise ValidationError(f"{source}: failed assertion cannot produce PASS")
    if data["outcome"] == "NOT_RUN":
        if data["skills_loaded"] or any(
            outcome != "NOT_CHECKED" for outcome in assertion_outcomes
        ):
            raise ValidationError(
                f"{source}: NOT_RUN requires no loaded skills and all assertions NOT_CHECKED"
            )


def build_not_run_result(case: dict[str, Any]) -> dict[str, Any]:
    commit = "0" * 40
    return {
        "case_id": case["id"],
        "case_definition_sha256": case_hash(case),
        "trigger_expectation": case["trigger_expectation"],
        "harness": "codex",
        "model": "not-run",
        "reasoning_level": "not-run",
        "commit": commit,
        "snapshot_identity": f"git:{commit}",
        "worktree_dirty": False,
        "skills_under_test": case["skills_under_test"],
        "expected_skills": case["expected_skills"],
        "skills_loaded": [],
        "assertions": [
            {"id": item["id"], "outcome": "NOT_CHECKED", "evidence": ""}
            for item in case["expected_assertions"]
        ],
        "outcome": "NOT_RUN",
        "notes": "Deterministic validator self-test fixture.",
    }


def expect_invalid(callback: Any, label: str) -> None:
    try:
        callback()
    except ValidationError:
        return
    raise ValidationError(f"result validator self-test failed to reject {label}")


def validate_result_contract_self_test(
    cases: dict[str, dict[str, Any]], schema: dict[str, Any]
) -> None:
    first_case = next(iter(cases.values()))
    valid = build_not_run_result(first_case)
    validate_result_data(valid, "self-test-valid", cases, schema)

    unknown = json.loads(json.dumps(valid))
    unknown["unexpected"] = True
    expect_invalid(
        lambda: validate_result_data(unknown, "self-test-unknown", cases, schema),
        "unknown result field",
    )

    wrong_type = json.loads(json.dumps(valid))
    wrong_type["worktree_dirty"] = "false"
    expect_invalid(
        lambda: validate_result_data(wrong_type, "self-test-type", cases, schema),
        "wrong result field type",
    )

    unknown_case = json.loads(json.dumps(valid))
    unknown_case["case_id"] = "unknown-case"
    expect_invalid(
        lambda: validate_result_data(
            unknown_case, "self-test-unknown-case", cases, schema
        ),
        "unknown case ID",
    )

    unknown_skill = json.loads(json.dumps(valid))
    unknown_skill["skills_loaded"] = ["unknown-skill"]
    expect_invalid(
        lambda: validate_result_data(
            unknown_skill, "self-test-unknown-skill", cases, schema
        ),
        "unknown loaded skill ID",
    )

    wrong_classification = json.loads(json.dumps(valid))
    wrong_classification["trigger_expectation"] = (
        "negative"
        if first_case["trigger_expectation"] != "negative"
        else "positive"
    )
    expect_invalid(
        lambda: validate_result_data(
            wrong_classification, "self-test-classification", cases, schema
        ),
        "case/result classification mismatch",
    )

    wrong_snapshot = json.loads(json.dumps(valid))
    wrong_snapshot["snapshot_identity"] = "git:" + "1" * 40
    expect_invalid(
        lambda: validate_result_data(
            wrong_snapshot, "self-test-snapshot", cases, schema
        ),
        "commit/snapshot mismatch",
    )

    wrong_assertion = json.loads(json.dumps(valid))
    wrong_assertion["assertions"][0]["id"] = "wrong-assertion"
    expect_invalid(
        lambda: validate_result_data(
            wrong_assertion, "self-test-assertion", cases, schema
        ),
        "mismatched assertion ID",
    )

    passing = json.loads(json.dumps(valid))
    passing["outcome"] = "PASS"
    passing["skills_loaded"] = list(first_case["expected_skills"])
    passing["assertions"] = [
        {"id": item["id"], "outcome": "PASS", "evidence": "self-test"}
        for item in first_case["expected_assertions"]
    ]
    validate_result_data(passing, "self-test-pass", cases, schema)
    passing["assertions"][0]["outcome"] = "FAIL"
    expect_invalid(
        lambda: validate_result_data(passing, "self-test-failed-pass", cases, schema),
        "failed assertion with overall PASS",
    )

    composition = next(
        case
        for case in cases.values()
        if case["trigger_expectation"] == "composition"
    )
    missing_skill = build_not_run_result(composition)
    missing_skill["outcome"] = "PASS"
    missing_skill["skills_loaded"] = composition["expected_skills"][:-1]
    missing_skill["assertions"] = [
        {"id": item["id"], "outcome": "PASS", "evidence": "self-test"}
        for item in composition["expected_assertions"]
    ]
    expect_invalid(
        lambda: validate_result_data(
            missing_skill, "self-test-composition", cases, schema
        ),
        "composition PASS missing an expected skill",
    )


def markdown_files() -> list[Path]:
    files = [
        ROOT / "AGENTS.md",
        ROOT / "AqualLifeStyle" / "9.4.2" / "aqua-frontend" / "AGENTS.md",
        ROOT / "docs" / "aqua-system" / "README.md",
        AUTHORITY_PATH,
        ROOT / "docs" / "agent-instruction-system.md",
    ]
    files.extend(SKILLS_ROOT.glob("**/*.md"))
    files.extend((ROOT / "docs" / "exec-plans").glob("**/*.md"))
    files.extend((ROOT / "docs" / "agent-evals").glob("**/*.md"))
    return sorted(set(files))


def validate_links() -> None:
    for path in markdown_files():
        text = path.read_text(encoding="utf-8")
        for raw_target in LINK_RE.findall(text):
            target = raw_target.strip().strip("<>").split("#", 1)[0]
            if not target or target.startswith(("http://", "https://", "mailto:")):
                continue
            target = unquote(target.split(" ", 1)[0])
            resolved = (path.parent / target).resolve()
            if not resolved.exists():
                raise ValidationError(
                    f"{path.relative_to(ROOT)}: broken local link {raw_target!r}"
                )

    frontend = ROOT / "AqualLifeStyle" / "9.4.2" / "aqua-frontend" / "AGENTS.md"
    bootstrap = (frontend.parent / "../../../AGENTS.md").resolve()
    if bootstrap != (ROOT / "AGENTS.md").resolve() or not bootstrap.exists():
        raise ValidationError("frontend AGENTS.md bootstrap does not resolve to root")


def validate_authority_contract() -> None:
    authority = AUTHORITY_PATH.read_text(encoding="utf-8")
    for phrase in AUTHORITY_REQUIRED_PHRASES:
        if phrase not in authority:
            raise ValidationError(f"authority convention missing {phrase!r}")
    index = (ROOT / "docs" / "aqua-system" / "README.md").read_text(encoding="utf-8")
    if "`UNRESOLVED` means no confirmed policy currently exists" not in index:
        raise ValidationError("authority index does not map UNRESOLVED lifecycle state")


def main() -> int:
    try:
        validate_skills()
        cases = validate_cases()
        schema = json.loads(RESULT_SCHEMA_PATH.read_text(encoding="utf-8"))
        validate_supported_schema(schema)
        validate_result_contract_self_test(cases, schema)
        validate_links()
        validate_authority_contract()
        for argument in sys.argv[1:]:
            path = Path(argument).resolve()
            data = json.loads(path.read_text(encoding="utf-8"))
            if not isinstance(data, dict):
                raise ValidationError(f"{path}: result root must be an object")
            validate_result_data(data, str(path), cases, schema)
    except (OSError, json.JSONDecodeError, ValidationError) as exc:
        print(f"FAIL: {exc}", file=sys.stderr)
        return 1

    suffix = f", {len(sys.argv) - 1} result file(s)" if len(sys.argv) > 1 else ""
    print(
        "PASS: "
        f"{len(EXPECTED_SKILLS)} skills, {len(cases)} routing cases, result schema "
        f"and coherence, authority contract, root safeguards, local links{suffix}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
