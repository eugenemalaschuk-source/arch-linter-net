"""Transport contract checks; not hosted bootstrap or final acceptance evidence."""

from pathlib import Path
import re

import pytest


ROOT = Path(__file__).resolve().parents[3]
WORKFLOW = ROOT / ".github/workflows/architecture-health-badge-promotion.yml"
HANDOFF_ROOT = "${{ runner.temp }}/arch-linter-net-bootstrap-handoff"


def _assert_upload_contract(workflow: str) -> None:
    bootstrap = workflow.split("\n  promote:\n", 1)[0]
    upload = bootstrap.split("      - name: Upload private trusted bootstrap handoff\n", 1)[1]
    settings = dict(re.findall(r"^          ([\w-]+): (.+)$", upload, re.MULTILINE))
    assert settings["name"] == "architecture-health-bootstrap-handoff"
    assert settings["path"] == HANDOFF_ROOT, "Only validated handoff staging may be uploaded"
    assert settings.get("include-hidden-files") == "true", "Managed .github files must survive upload"
    assert settings["if-no-files-found"] == "error"
    assert settings["retention-days"] == "7"
    assert "contents: write" not in bootstrap


def test_private_handoff_preserves_hidden_managed_files() -> None:
    _assert_upload_contract(WORKFLOW.read_text(encoding="utf-8"))


@pytest.mark.parametrize("mutation", ("missing-hidden", "disabled-hidden", "broad-root"))
def test_handoff_transport_rejects_unsafe_regressions(mutation: str) -> None:
    workflow = WORKFLOW.read_text(encoding="utf-8")
    hidden_setting = "          include-hidden-files: true\n"
    if mutation == "missing-hidden":
        workflow = workflow.replace(hidden_setting, "")
    elif mutation == "disabled-hidden":
        workflow = workflow.replace(hidden_setting, "          include-hidden-files: false\n")
    else:
        workflow = workflow.replace("path: " + HANDOFF_ROOT, "path: ${{ runner.temp }}")
    with pytest.raises(AssertionError):
        _assert_upload_contract(workflow)
