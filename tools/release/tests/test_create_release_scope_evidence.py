from __future__ import annotations

import hashlib
import json
import subprocess
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import create_release_scope_evidence as generator  # noqa: E402
from create_release_scope_evidence import (  # noqa: E402
    _issue_number,
    _read_declaration,
    _repository,
    _safe_path,
    _source_commit,
    build_evidence,
)

_COMMIT = "b" * 40
_REPOSITORY = "owner/arch-linter-net"


def _declaration(tmp_path: Path, required: list[dict] | None = None) -> Path:
    path = tmp_path / "release-scope.json"
    path.write_text(json.dumps({
        "schema": "checkpoint-b-release-scope-declaration/v1",
        "release_target": "0.6.1",
        "story": 434,
        "required_items": required if required is not None else [
            {"issue": 435, "finding": "F1", "summary": "First"},
            {"issue": 466, "finding": "gate", "summary": "Gate"},
        ],
        "excluded_items": [{"issue": 450, "reason": "Post-release refactoring."}],
    }))
    return path


def _manifest(tmp_path: Path, commit: str = _COMMIT) -> Path:
    path = tmp_path / "package-manifest.json"
    path.write_text(json.dumps({"version": "0.6.1", "source_commit": commit}))
    return path


def _stub_gh(monkeypatch, states: dict[int, str], returncode: int = 0, stderr: str = "") -> list[list[str]]:
    invocations: list[list[str]] = []

    def fake_run(argv, **kwargs):
        invocations.append(argv)
        number = int(argv[3])
        return subprocess.CompletedProcess(
            argv, returncode,
            stdout=json.dumps({"number": number, "state": states.get(number, "CLOSED"), "title": f"Item {number}"}),
            stderr=stderr)

    monkeypatch.setattr(generator.subprocess, "run", fake_run)
    return invocations


def test_build_evidence_binds_state_to_the_candidate(tmp_path: Path, monkeypatch) -> None:
    monkeypatch.chdir(tmp_path)
    invocations = _stub_gh(monkeypatch, {435: "CLOSED", 466: "OPEN"})
    manifest_path = _manifest(tmp_path)

    evidence = build_evidence(_declaration(tmp_path), manifest_path, _COMMIT, _REPOSITORY)

    assert evidence["schema"] == "checkpoint-b-release-scope/v1"
    assert evidence["source_commit"] == _COMMIT
    assert evidence["repository"] == _REPOSITORY
    assert evidence["candidate_manifest_sha256"] == hashlib.sha256(manifest_path.read_bytes()).hexdigest()
    assert [(item["issue"], item["state"]) for item in evidence["required_items"]] == [(435, "closed"), (466, "open")]
    assert evidence["excluded_items"] == [{"issue": 450, "reason": "Post-release refactoring."}]
    assert all(argv[0] == "gh" and "--repo" in argv for argv in invocations)


def test_build_evidence_rejects_a_manifest_from_another_commit(tmp_path: Path, monkeypatch) -> None:
    monkeypatch.chdir(tmp_path)
    _stub_gh(monkeypatch, {})
    declaration = _declaration(tmp_path)
    manifest = _manifest(tmp_path, "c" * 40)

    with pytest.raises(ValueError, match="source commit does not match"):
        build_evidence(declaration, manifest, _COMMIT, _REPOSITORY)


def test_unresolvable_issue_is_fatal(tmp_path: Path, monkeypatch) -> None:
    monkeypatch.chdir(tmp_path)
    _stub_gh(monkeypatch, {}, returncode=1, stderr="not found")
    declaration = _declaration(tmp_path)
    manifest = _manifest(tmp_path)

    with pytest.raises(ValueError, match="Cannot resolve issue #435"):
        build_evidence(declaration, manifest, _COMMIT, _REPOSITORY)


@pytest.mark.parametrize("value", ["owner", "owner/name/extra", "owner/na me", "own;er/name", "", "-"])
def test_invalid_repository_is_rejected(value: str) -> None:
    with pytest.raises(ValueError, match="not a valid GitHub owner/name repository"):
        _repository(value)


@pytest.mark.parametrize("value", ["owner/name", "Owner/arch-linter-net", "o.w_n-er/a.b_c-d"])
def test_valid_repository_is_accepted(value: str) -> None:
    assert _repository(value) == value


@pytest.mark.parametrize("value", ["", "zzzzzzz", "12345", "b" * 65, "abc123; rm -rf /"])
def test_invalid_source_commit_is_rejected(value: str) -> None:
    with pytest.raises(ValueError, match="not a valid commit SHA"):
        _source_commit(value)


@pytest.mark.parametrize("value", [0, -1, "435", None, True, 4.0])
def test_invalid_issue_number_is_rejected(value) -> None:
    with pytest.raises(ValueError, match="not a valid issue number"):
        _issue_number(value)


def test_path_outside_the_workspace_is_rejected(tmp_path: Path, monkeypatch) -> None:
    workspace = tmp_path / "workspace"
    workspace.mkdir()
    monkeypatch.chdir(workspace)

    with pytest.raises(ValueError, match="resolves outside the release workspace"):
        _safe_path(Path("/etc/passwd"), "candidate manifest")


def test_traversal_out_of_the_workspace_is_rejected(tmp_path: Path, monkeypatch) -> None:
    workspace = tmp_path / "workspace"
    workspace.mkdir()
    monkeypatch.chdir(workspace)

    with pytest.raises(ValueError, match="resolves outside the release workspace"):
        _safe_path(Path("../../escaped.json"), "output")


def test_incomparable_root_is_treated_as_not_containing(tmp_path: Path, monkeypatch) -> None:
    """os.path.commonpath raises for paths on different Windows drives; that is a rejection, not a
    crash, and must not leak a confusing message."""
    monkeypatch.setattr(generator.os.path, "commonpath",
                        lambda paths: (_ for _ in ()).throw(ValueError("different drives")))

    with pytest.raises(ValueError, match="resolves outside the release workspace"):
        _safe_path(tmp_path / "file.json", "output")


def test_path_inside_the_workspace_is_accepted(tmp_path: Path, monkeypatch) -> None:
    monkeypatch.chdir(tmp_path)

    assert _safe_path(Path("nested/file.json"), "output") == (tmp_path / "nested" / "file.json").resolve()


@pytest.mark.parametrize(
    ("declaration", "message"),
    [
        ({"schema": "other/v1"}, "not a release-scope declaration"),
        ({"schema": "checkpoint-b-release-scope-declaration/v1", "required_items": []}, "no required items"),
        ({"schema": "checkpoint-b-release-scope-declaration/v1",
          "required_items": [{"issue": 435}, {"issue": 435}]}, "duplicate required item"),
        ({"schema": "checkpoint-b-release-scope-declaration/v1",
          "required_items": [{"issue": "435"}]}, "not a valid issue number"),
    ],
)
def test_malformed_declaration_is_rejected(tmp_path: Path, declaration: dict, message: str) -> None:
    path = tmp_path / "release-scope.json"
    path.write_text(json.dumps(declaration))

    with pytest.raises(ValueError, match=message):
        _read_declaration(path)


def test_main_writes_the_bound_inventory(tmp_path: Path, monkeypatch) -> None:
    monkeypatch.chdir(tmp_path)
    _stub_gh(monkeypatch, {435: "CLOSED", 466: "CLOSED"})
    output = tmp_path / "evidence" / "release-scope.json"
    monkeypatch.setattr(sys, "argv", [
        "create_release_scope_evidence.py",
        "--declaration", str(_declaration(tmp_path)),
        "--candidate-manifest", str(_manifest(tmp_path)),
        "--source-commit", _COMMIT,
        "--repository", _REPOSITORY,
        "--output", str(output),
    ])

    assert generator.main() == 0
    written = json.loads(output.read_text())
    assert [item["state"] for item in written["required_items"]] == ["closed", "closed"]


def test_the_shipped_declaration_is_valid() -> None:
    """The declaration this release actually ships must satisfy its own contract."""
    declaration = _read_declaration(Path(generator.__file__).with_name("release-scope.json"))

    assert declaration["story"] == 434
    assert declaration["release_target"] == "0.6.1"
    required = {item["issue"] for item in declaration["required_items"]}
    excluded = {item["issue"] for item in declaration["excluded_items"]}
    assert required.isdisjoint(excluded)
    assert all(item.get("reason") for item in declaration["excluded_items"])
