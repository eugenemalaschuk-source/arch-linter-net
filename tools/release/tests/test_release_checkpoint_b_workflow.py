from __future__ import annotations

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from merge_checkpoint_b_platform_evidence import _REQUIRED_SHARDS  # noqa: E402

_REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
_RELEASE_WORKFLOW = _REPOSITORY_ROOT / ".github" / "workflows" / "release-nuget.yml"
_SHARD_ENTRY = re.compile(
    r"^          - id: (?P<id>[a-z0-9-]+)\n"
    r"            name: .+\n"
    r"            target: (?P<target>[a-z0-9-]+)$",
    re.MULTILINE,
)


def _release_checkpoint_b_shards() -> list[tuple[str, str]]:
    workflow = _RELEASE_WORKFLOW.read_text(encoding="utf-8")
    matrix_start = workflow.index("        shard:\n")
    matrix_end = workflow.index("    env:\n", matrix_start)
    matrix = workflow[matrix_start:matrix_end]
    return [(match.group("id"), match.group("target")) for match in _SHARD_ENTRY.finditer(matrix)]


def test_release_checkpoint_b_matrix_matches_required_shard_inventory() -> None:
    entries = _release_checkpoint_b_shards()
    shard_ids = [shard_id for shard_id, _ in entries]

    assert len(shard_ids) == len(set(shard_ids))
    assert set(shard_ids) == _REQUIRED_SHARDS
    assert dict(entries) == {
        shard_id: f"test-packed-artifact-{shard_id}"
        for shard_id in _REQUIRED_SHARDS
    }
