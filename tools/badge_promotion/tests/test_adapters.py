from __future__ import annotations

import pytest

from badge_promotion.adapters import AdapterError, NoneAdapter, require_supported_adapter
from badge_promotion.model import AdapterKind


def test_only_three_promotion_adapters_are_supported() -> None:
    assert [require_supported_adapter(value) for value in ("github-raw", "relay", "none")] == [AdapterKind.GITHUB_RAW, AdapterKind.RELAY, AdapterKind.NONE]
    with pytest.raises(AdapterError, match="unsupported"):
        require_supported_adapter("https")


def test_none_adapter_never_publishes_ready_data() -> None:
    adapter = NoneAdapter()
    with pytest.raises(AdapterError, match="cannot_publish"):
        adapter.commit(type("Decision", (), {"status": type("Status", (), {"value": "ready"})()})())
