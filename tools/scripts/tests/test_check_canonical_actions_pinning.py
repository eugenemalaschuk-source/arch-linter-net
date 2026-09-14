from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import check_canonical_actions_pinning as pinning  # noqa: E402


def write_doc(root: Path, relative: str, content: str) -> None:
    path = root / relative
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8")


def test_rejects_mutable_third_party_tag_in_yaml_fence(tmp_path: Path) -> None:
    write_doc(
        tmp_path,
        "docs/guides/ci-integration.md",
        "```yaml\njobs:\n  build:\n    steps:\n      - uses: actions/checkout@v4\n```\n",
    )

    violations = pinning.find_violations(tmp_path)

    assert len(violations) == 1
    assert "actions/checkout@v4" in violations[0]


def test_accepts_full_commit_sha_with_version_comment(tmp_path: Path) -> None:
    write_doc(
        tmp_path,
        "docs/guides/ci-integration.md",
        "```yaml\njobs:\n  build:\n    steps:\n"
        "      - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1\n"
        "```\n",
    )

    assert pinning.find_violations(tmp_path) == []


def test_accepts_first_party_local_action(tmp_path: Path) -> None:
    write_doc(
        tmp_path,
        "docs/guides/ci-integration.md",
        "```yaml\njobs:\n  build:\n    steps:\n      - uses: ./.github/actions/my-local-action\n```\n",
    )

    assert pinning.find_violations(tmp_path) == []


def test_accepts_own_repository_reusable_workflow_placeholder(tmp_path: Path) -> None:
    write_doc(
        tmp_path,
        "docs/guides/ci-integration.md",
        "```yaml\njobs:\n  badge:\n"
        "    uses: eugenemalaschuk-source/arch-linter-net/.github/workflows/"
        "architecture-health-badge-promotion.yml@<reviewed-sha>\n"
        "```\n",
    )

    assert pinning.find_violations(tmp_path) == []


def test_ignores_descriptive_prose_outside_a_fence(tmp_path: Path) -> None:
    write_doc(
        tmp_path,
        "docs/guides/ci-integration.md",
        "Earlier examples used `actions/checkout@v4` before the repository "
        "adopted commit-SHA pinning.\n",
    )

    assert pinning.find_violations(tmp_path) == []


def test_ignores_non_yaml_fences(tmp_path: Path) -> None:
    write_doc(
        tmp_path,
        "docs/guides/ci-integration.md",
        "```bash\n# uses: actions/checkout@v4\necho not yaml\n```\n",
    )

    assert pinning.find_violations(tmp_path) == []


def test_ignores_internal_docs(tmp_path: Path) -> None:
    write_doc(
        tmp_path,
        "docs/internal/notes.md",
        "```yaml\nsteps:\n  - uses: actions/checkout@v4\n```\n",
    )

    assert pinning.find_violations(tmp_path) == []


def test_rejects_unpinned_third_party_reference_without_at_ref(tmp_path: Path) -> None:
    write_doc(
        tmp_path,
        "docs/guides/ci-integration.md",
        "```yaml\nsteps:\n  - uses: actions/checkout\n```\n",
    )

    violations = pinning.find_violations(tmp_path)

    assert len(violations) == 1
    assert "unpinned" in violations[0]


def test_rejects_indented_yaml_fence(tmp_path: Path) -> None:
    write_doc(
        tmp_path,
        "docs/guides/ci-integration.md",
        "1. Step one:\n\n"
        "   ```yaml\n"
        "   steps:\n"
        "     - uses: actions/checkout@v4\n"
        "   ```\n",
    )

    violations = pinning.find_violations(tmp_path)

    assert len(violations) == 1
    assert "actions/checkout@v4" in violations[0]


def test_rejects_yaml_fence_with_extra_info_string_content(tmp_path: Path) -> None:
    write_doc(
        tmp_path,
        "docs/guides/ci-integration.md",
        '```yaml title="ci.yml"\nsteps:\n  - uses: actions/checkout@v4\n```\n',
    )

    violations = pinning.find_violations(tmp_path)

    assert len(violations) == 1
    assert "actions/checkout@v4" in violations[0]


def test_rejects_tilde_yaml_fence(tmp_path: Path) -> None:
    write_doc(
        tmp_path,
        "docs/guides/ci-integration.md",
        "~~~yaml\nsteps:\n  - uses: actions/checkout@v4\n~~~\n",
    )

    violations = pinning.find_violations(tmp_path)

    assert len(violations) == 1
    assert "actions/checkout@v4" in violations[0]


def test_rejects_quoted_uses_key(tmp_path: Path) -> None:
    write_doc(
        tmp_path,
        "docs/guides/ci-integration.md",
        '```yaml\nsteps:\n  - "uses": actions/checkout@v4\n```\n',
    )

    violations = pinning.find_violations(tmp_path)

    assert len(violations) == 1
    assert "actions/checkout@v4" in violations[0]


def test_rejects_flow_style_uses_mapping(tmp_path: Path) -> None:
    write_doc(
        tmp_path,
        "docs/guides/ci-integration.md",
        "```yaml\nsteps:\n  - {uses: actions/checkout@v4, with: {fetch-depth: 0}}\n```\n",
    )

    violations = pinning.find_violations(tmp_path)

    assert len(violations) == 1
    assert "actions/checkout@v4" in violations[0]


def test_short_fence_marker_inside_open_fence_does_not_close_it(tmp_path: Path) -> None:
    write_doc(
        tmp_path,
        "docs/guides/ci-integration.md",
        "````yaml\nsteps:\n  - uses: actions/checkout@v4\n``\nmore: text\n````\n",
    )

    violations = pinning.find_violations(tmp_path)

    assert len(violations) == 1
    assert "actions/checkout@v4" in violations[0]
