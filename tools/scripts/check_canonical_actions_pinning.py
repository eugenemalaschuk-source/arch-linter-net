#!/usr/bin/env python3
"""Reject mutable third-party GitHub Actions refs in canonical docs examples.

Production `.github/workflows/*.yml` already pin third-party actions to full commit
SHAs. This guard applies the same discipline to canonical, copy/paste-intended
workflow examples under `docs/`, so adopting a documented snippet does not
silently weaken the repository's own CI security model.

Only a real `uses:` mapping key inside a fenced `yaml`/`yml` code block is in
scope — prose mentioning an action tag descriptively (for example, inside a
`run:` string, a YAML comment, or historical/explanatory text) is not a
canonical workflow reference and is not flagged. First-party local actions
(`./...`) and reusable-workflow references owned by this repository are exempt,
since they are not third-party supply-chain surface. A third-party Docker
action (`docker://...`) is pinned by an immutable `sha256:` digest rather than
a Git commit SHA, and is validated against that separate immutable form.

Fence detection follows CommonMark/pymdownx.superfences grammar closely enough to
avoid an easy bypass: fences may be indented up to 3 spaces, may use backtick or
tilde fence characters, may carry extra info-string content after the language
token (for example ```` ```yaml title="ci.yml" ````), and a closing fence must
reuse the same character with at least the opening run length and no info string.
`uses:` detection is anchored to an actual mapping-key position — the start of a
(optionally list-item- or flow-brace-prefixed) line — rather than scanning for the
substring anywhere on the line, so it does not mistake a `run:`/`echo` string or a
`#` comment mentioning `uses:` for a real reference. A quoted key (`"uses":`) or a
flow-style mapping (`{uses: ...}`) is still recognized.
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

FENCE_LINE = re.compile(r"^ {0,3}(?P<fence>`{3,}+|~{3,}+)(?P<info>.*)$")
USES_KEY_PREFIX = r"""^\s*(?:-\s+)?(?:\{\s*)?["']?uses["']?\s*:\s*"""
USES_REF = r"""(?P<ref>"[^"]+"|'[^']+'|[^\s,}]+)"""
USES_KEY = re.compile(USES_KEY_PREFIX + USES_REF)
FULL_COMMIT_SHA = re.compile(r"^[0-9a-fA-F]{40}$")
DOCKER_SHA256_DIGEST = re.compile(r"^sha256:[0-9a-fA-F]{64}$")
YAML_LANGUAGES = {"yaml", "yml"}
OWN_REPO_PREFIX = "eugenemalaschuk-source/arch-linter-net/"
DOCKER_ACTION_PREFIX = "docker://"
IGNORED_DOCS_DIRS = {"internal"}


def repository_root() -> Path:
    return Path(__file__).resolve().parents[2]


def canonical_markdown_files(root: Path) -> list[Path]:
    docs_root = root / "docs"
    if not docs_root.exists():
        return []
    return sorted(
        path
        for path in docs_root.rglob("*.md")
        if not set(path.relative_to(docs_root).parts[:-1]) & IGNORED_DOCS_DIRS
    )


def fence_language(info: str) -> str:
    token = info.strip().split()[0] if info.strip() else ""
    return token.strip("{}.").lower()


def yaml_fence_lines(text: str) -> list[tuple[int, str]]:
    """Return (line_number, content) pairs for lines inside yaml/yml fences.

    Tracks one open fence at a time, matching CommonMark's rule that a closing
    fence must reuse the opening fence character, be at least as long as the
    opening run, and carry no info string of its own.
    """
    lines: list[tuple[int, str]] = []
    open_fence_char: str | None = None
    open_fence_len = 0
    in_yaml_fence = False

    for line_number, line in enumerate(text.splitlines(), start=1):
        match = FENCE_LINE.match(line)
        if match is not None:
            fence = match.group("fence")
            info = match.group("info")
            if open_fence_char is None:
                open_fence_char = fence[0]
                open_fence_len = len(fence)
                in_yaml_fence = fence_language(info) in YAML_LANGUAGES
                continue
            is_closing = (
                fence[0] == open_fence_char
                and len(fence) >= open_fence_len
                and info.strip() == ""
            )
            if is_closing:
                open_fence_char = None
                open_fence_len = 0
                in_yaml_fence = False
                continue
        if in_yaml_fence:
            lines.append((line_number, line))
    return lines


def is_exempt_ref(action_ref: str) -> bool:
    return action_ref.startswith("./") or action_ref.startswith(OWN_REPO_PREFIX)


def docker_pin_violation(relative_path: str, line_number: int, action_ref: str) -> str | None:
    if "@" not in action_ref:
        return f"{relative_path}:{line_number}: unpinned third-party Docker action reference '{action_ref}'"
    _, _, digest = action_ref.rpartition("@")
    if not DOCKER_SHA256_DIGEST.match(digest):
        return (
            f"{relative_path}:{line_number}: mutable third-party Docker action reference "
            f"'{action_ref}' must pin an immutable sha256 digest"
        )
    return None


def git_ref_pin_violation(relative_path: str, line_number: int, action_ref: str) -> str | None:
    if "@" not in action_ref:
        return f"{relative_path}:{line_number}: unpinned third-party action reference '{action_ref}'"
    _, _, pin = action_ref.rpartition("@")
    if not FULL_COMMIT_SHA.match(pin):
        return (
            f"{relative_path}:{line_number}: mutable third-party action reference '{action_ref}' "
            "must pin a full commit SHA"
        )
    return None


def ref_violations(relative_path: str, text: str) -> list[str]:
    violations: list[str] = []
    for line_number, line in yaml_fence_lines(text):
        match = USES_KEY.match(line)
        if match is None:
            continue
        action_ref = match.group("ref").strip("\"'")
        if is_exempt_ref(action_ref):
            continue
        if action_ref.startswith(DOCKER_ACTION_PREFIX):
            violation = docker_pin_violation(relative_path, line_number, action_ref)
        else:
            violation = git_ref_pin_violation(relative_path, line_number, action_ref)
        if violation is not None:
            violations.append(violation)
    return violations


def find_violations(root: Path) -> list[str]:
    violations: list[str] = []
    for path in canonical_markdown_files(root):
        relative_path = path.relative_to(root).as_posix()
        violations.extend(ref_violations(relative_path, path.read_text(encoding="utf-8")))
    return violations


def main() -> int:
    root = repository_root()
    violations = find_violations(root)
    if not violations:
        print("Canonical Actions pinning guard: OK")
        return 0

    print("Canonical Actions pinning guard failed:", file=sys.stderr)
    for violation in violations:
        print(f"- {violation}", file=sys.stderr)
    print(
        "Pin canonical third-party GitHub Actions examples to a full commit SHA, reusing the "
        "same reviewed pin already used for that action/version in .github/workflows/*.yml.",
        file=sys.stderr,
    )
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
