#!/usr/bin/env python3
"""Generate the tracked release-scope declaration from a release story issue.

The story's fenced TOML block is the authoring input. This tool only creates the
existing v2 declaration consumed by candidate authorization; it never publishes
packages or changes release workflow state.
"""

from __future__ import annotations

import argparse
import json
import os
import re
import subprocess
import sys
import tempfile
import tomllib
from pathlib import Path
from typing import Any

from _release_workspace import _repository_root, _safe_path

_INPUT_SCHEMA = "release-scope-input/v1"
_DECLARATION_SCHEMA = "checkpoint-b-release-scope-declaration/v2"
_ISSUE_REPOSITORY = "eugenemalaschuk-source/arch-linter-net"
_RELEASE_TARGET_PATTERN = re.compile(
    r"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)"
    r"(?:-preview\.(0|[1-9]\d*))?$"
)
_DECLARATION_ID_PATTERN = re.compile(r"^[a-z0-9][a-z0-9._-]*$")
_SCOPE_BLOCK_PATTERN = re.compile(
    r"^```toml release-scope/v1[ \t]*\r?\n(?P<content>.*?)^```[ \t]*\r?$",
    re.MULTILINE | re.DOTALL,
)
_TOP_LEVEL_FIELDS = {
    "schema",
    "release_target",
    "declaration_id",
    "comments",
    "required_items",
    "excluded_items",
    "delivered_items",
}


def _declarations_directory() -> Path:
    return Path(__file__).with_name("scopes")


def _issue_number(value: Any) -> int:
    if not isinstance(value, int) or isinstance(value, bool) or value <= 0:
        raise ValueError("The release story issue number must be a positive integer.")
    return value


def _release_target(value: Any) -> str:
    if not isinstance(value, str) or not _RELEASE_TARGET_PATTERN.fullmatch(value):
        raise ValueError("release_target must be an exact stable or X.Y.Z-preview.N version.")
    return value


def _declaration_id(value: Any) -> str:
    if not isinstance(value, str) or not _DECLARATION_ID_PATTERN.fullmatch(value):
        raise ValueError("declaration_id must contain lowercase letters, digits, dots, underscores, or hyphens.")
    return value


def _non_empty_string(value: Any, description: str) -> str:
    if not isinstance(value, str) or not value.strip():
        raise ValueError(f"{description} must be a non-empty string.")
    return value


def _comments(value: Any) -> list[str]:
    if not isinstance(value, list) or not value:
        raise ValueError("comments must be a non-empty TOML array of strings.")
    return [_non_empty_string(comment, "Each comment") for comment in value]


def _validate_items(value: Any, name: str, fields: set[str], *, required: bool) -> list[dict[str, Any]]:
    if not isinstance(value, list) or (required and not value):
        requirement = "a non-empty array" if required else "an array"
        raise ValueError(f"{name} must be {requirement}.")

    validated: list[dict[str, Any]] = []
    seen: set[int] = set()
    for index, item in enumerate(value):
        label = f"{name}[{index}]"
        if not isinstance(item, dict):
            raise ValueError(f"{label} must be a TOML table.")
        if set(item) != fields:
            raise ValueError(f"{label} must contain exactly: {', '.join(sorted(fields))}.")

        issue = _issue_number(item.get("issue"))
        if issue in seen:
            raise ValueError(f"{name} contains duplicate issue #{issue}.")
        seen.add(issue)

        normalized = {"issue": issue}
        for field in sorted(fields - {"issue"}):
            description = f"{label}.{field}"
            normalized[field] = _non_empty_string(item.get(field), description)
        validated.append(normalized)
    return validated


def _parse_scope_block(body: str, story_number: int) -> dict[str, Any]:
    matches = list(_SCOPE_BLOCK_PATTERN.finditer(body))
    if not matches:
        raise ValueError("The release story has no fenced TOML release-scope/v1 block.")
    if len(matches) != 1:
        raise ValueError("The release story must contain exactly one release-scope/v1 block.")

    try:
        source = tomllib.loads(matches[0].group("content"))
    except tomllib.TOMLDecodeError as error:
        raise ValueError(f"The release-scope TOML block is invalid: {error}") from error

    if not isinstance(source, dict):
        raise ValueError("The release-scope block must be a TOML table.")
    unknown_fields = set(source) - _TOP_LEVEL_FIELDS
    missing_fields = _TOP_LEVEL_FIELDS - set(source)
    if unknown_fields or missing_fields:
        details = []
        if unknown_fields:
            details.append(f"unknown fields: {', '.join(sorted(unknown_fields))}")
        if missing_fields:
            details.append(f"missing fields: {', '.join(sorted(missing_fields))}")
        raise ValueError("The release-scope block has " + "; ".join(details) + ".")
    if source["schema"] != _INPUT_SCHEMA:
        raise ValueError(f"The release-scope block schema must be '{_INPUT_SCHEMA}'.")

    target = _release_target(source["release_target"])
    declaration_id = _declaration_id(source["declaration_id"])
    if not declaration_id.startswith(f"v{target}-"):
        raise ValueError("declaration_id must start with 'v<release_target>-'.")
    comments = _comments(source["comments"])
    required_items = _validate_items(
        source["required_items"], "required_items", {"issue", "finding", "summary"}, required=True
    )
    excluded_items = _validate_items(
        source["excluded_items"], "excluded_items", {"issue", "reason"}, required=False
    )
    delivered_items = _validate_items(
        source["delivered_items"], "delivered_items", {"issue", "reason"}, required=False
    )

    inventories = [
        {item["issue"] for item in items}
        for items in (required_items, excluded_items, delivered_items)
    ]
    if any(inventories[left] & inventories[right] for left in range(3) for right in range(left + 1, 3)):
        raise ValueError("The release-scope block repeats an issue across inventories.")

    return {
        "schema": _DECLARATION_SCHEMA,
        "declaration_id": declaration_id,
        "release_target": target,
        "story": _issue_number(story_number),
        "_comment": comments,
        "required_items": required_items,
        "excluded_items": excluded_items,
        "delivered_items": delivered_items,
    }


def _fetch_story(story_number: int) -> dict[str, Any]:
    story_number = _issue_number(story_number)
    completed = subprocess.run(
        [
            "gh",
            "issue",
            "view",
            str(story_number),
            "--repo",
            _ISSUE_REPOSITORY,
            "--json",
            "number,body",
        ],
        cwd=_repository_root(),
        capture_output=True,
        text=True,
        check=False,
        shell=False,
    )
    if completed.returncode != 0:
        raise ValueError(f"Cannot read release story #{story_number} with gh: {completed.stderr.strip()}")
    try:
        story = json.loads(completed.stdout)
    except json.JSONDecodeError as error:
        raise ValueError("Cannot read release story: gh returned invalid JSON.") from error
    if (
        not isinstance(story, dict)
        or story.get("number") != story_number
        or not isinstance(story.get("body"), str)
    ):
        raise ValueError(f"Cannot read release story #{story_number}: gh returned an invalid issue identity or body.")
    return story


def _render_declaration(declaration: dict[str, Any]) -> bytes:
    return (json.dumps(declaration, ensure_ascii=False, indent=2) + "\n").encode("utf-8")


def _output_path(target: str) -> Path:
    target = _release_target(target)
    source_directory = _declarations_directory()
    if source_directory.is_symlink():
        raise ValueError("The release-scope declarations directory must not be a symlink.")
    directory = _safe_path(source_directory, "release-scope declarations")
    if not directory.is_dir():
        raise ValueError("The release-scope declarations directory is missing or invalid.")
    output = directory / f"{target}.json"
    if output.is_symlink():
        raise ValueError(f"The release-scope declaration '{output.name}' must not be a symlink.")
    return _safe_path(output, "release-scope declaration output")


def _existing_result(path: Path, content: bytes) -> bool:
    if path.is_symlink():
        raise ValueError(f"The existing declaration '{path.name}' is a symlink.")
    if not path.exists():
        return False
    if not path.is_file():
        raise ValueError(f"The existing declaration path '{path.name}' is not a regular file.")
    try:
        existing = path.read_bytes()
    except OSError as error:
        raise ValueError(f"Cannot read existing declaration '{path.name}': {error}") from error
    if existing != content:
        raise ValueError(
            f"Declaration '{path.name}' already exists with different bytes; refusing to replace reviewed authority."
        )
    return True


def _write_new(path: Path, content: bytes) -> bool:
    if _existing_result(path, content):
        return False

    temporary_path: Path | None = None
    try:
        descriptor, temporary_name = tempfile.mkstemp(
            prefix=f".{path.stem}.", suffix=".tmp", dir=path.parent
        )
        temporary_path = Path(temporary_name)
        with os.fdopen(descriptor, "wb") as destination:
            destination.write(content)
            destination.flush()
            os.fsync(destination.fileno())

        try:
            # A same-directory hard link atomically creates the new name without
            # replacing a declaration that may have appeared after the first check.
            os.link(temporary_path, path)
        except FileExistsError:
            if _existing_result(path, content):
                return False
            raise
        return True
    except OSError as error:
        raise ValueError(f"Cannot create declaration '{path.name}' atomically: {error}") from error
    finally:
        if temporary_path is not None:
            try:
                temporary_path.unlink(missing_ok=True)
            except OSError:
                pass


def _generate(story_number: int) -> tuple[Path, bytes]:
    story = _fetch_story(story_number)
    declaration = _parse_scope_block(story["body"], story["number"])
    path = _output_path(declaration["release_target"])
    return path, _render_declaration(declaration)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    for name in ("preview", "generate"):
        command = commands.add_parser(name)
        command.add_argument("--story", type=int, required=True, help="release story issue number")
    arguments = parser.parse_args(argv)

    try:
        path, content = _generate(arguments.story)
        if arguments.command == "preview":
            sys.stdout.buffer.write(content)
            return 0
        changed = _write_new(path, content)
    except (OSError, ValueError) as error:
        print(str(error), file=sys.stderr)
        return 2

    status = "created" if changed else "already matches"
    print(f"Release-scope declaration {status}: {path.relative_to(_repository_root()).as_posix()}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
