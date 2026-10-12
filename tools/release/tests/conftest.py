from __future__ import annotations

from pathlib import Path
import sys

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import _release_workspace  # noqa: E402


@pytest.fixture(autouse=True)
def _allow_isolated_test_workspace(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    """Let release-tool tests use isolated temporary directories as confined workspaces."""
    repository_root = _release_workspace._repository_root()
    monkeypatch.setattr(
        _release_workspace,
        "_allowed_roots",
        lambda: (tmp_path.resolve(), repository_root),
    )
