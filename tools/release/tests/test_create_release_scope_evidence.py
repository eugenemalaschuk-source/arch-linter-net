from __future__ import annotations

import hashlib
import json
import subprocess
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import _release_workspace  # noqa: E402
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


def _declaration(
    directory: Path,
    target: str = "0.6.4",
    *,
    name: str | None = None,
    declaration_id: str | None = None,
    story: int = 527,
    required: list[dict] | None = None,
    excluded: list[dict] | None = None,
    delivered: list[dict] | None = None,
) -> Path:
    directory.mkdir(parents=True, exist_ok=True)
    path = directory / (name or f"scope-for-{target}.json")
    path.write_text(
        json.dumps(
            {
                "schema": "checkpoint-b-release-scope-declaration/v2",
                "declaration_id": declaration_id or f"v{target}-authority",
                "release_target": target,
                "story": story,
                "required_items": required
                if required is not None
                else [
                    {"issue": 525, "finding": "selector", "summary": "Select API surface"},
                    {"issue": 526, "finding": "gate", "summary": "Validate consumer exit"},
                ],
                "excluded_items": excluded
                if excluded is not None
                else [{"issue": 450, "reason": "Separate post-release refactoring."}],
                "delivered_items": delivered if delivered is not None else [],
            }
        ),
        encoding="utf-8",
    )
    return path


def _manifest(tmp_path: Path, version: str = "0.6.4", commit: str = _COMMIT) -> Path:
    path = tmp_path / "package-manifest.json"
    path.write_text(json.dumps({"version": version, "source_commit": commit}), encoding="utf-8")
    return path


def _stub_gh(
    monkeypatch,
    states: dict[int, str],
    returncode: int = 0,
    stderr: str = "",
) -> list[list[str]]:
    invocations: list[list[str]] = []

    def fake_run(argv, **kwargs):
        invocations.append(argv)
        number = int(argv[3])
        return subprocess.CompletedProcess(
            argv,
            returncode,
            stdout=json.dumps(
                {"number": number, "state": states.get(number, "CLOSED"), "title": f"Item {number}"}
            ),
            stderr=stderr,
        )

    monkeypatch.setattr(generator.subprocess, "run", fake_run)
    return invocations


def test_coexisting_release_targets_select_their_own_reviewed_authority(tmp_path: Path, monkeypatch) -> None:
    monkeypatch.chdir(tmp_path)
    scopes = tmp_path / "scopes"
    v064 = _declaration(scopes, declaration_id="v0.6.4-public-api-consumer-exit")
    v070 = _declaration(
        scopes,
        "0.7.0",
        declaration_id="v0.7.0-release-architecture-forensics",
        story=613,
        required=[{"issue": 614, "finding": "scope", "summary": "Versioned release authority"}],
        excluded=[{"issue": 287, "reason": "Manual trust work is non-blocking."}],
        delivered=[{"issue": 222, "reason": "Delivered release context."}],
    )
    _stub_gh(monkeypatch, {})

    maintenance = build_evidence(scopes, _manifest(tmp_path), _COMMIT, _REPOSITORY)
    current = build_evidence(scopes, _manifest(tmp_path, "0.7.0"), _COMMIT, _REPOSITORY)

    assert maintenance["declaration_id"] == "v0.6.4-public-api-consumer-exit"
    assert maintenance["story"] == 527
    assert maintenance["declaration_sha256"] == hashlib.sha256(v064.read_bytes()).hexdigest()
    assert current["declaration_id"] == "v0.7.0-release-architecture-forensics"
    assert current["story"] == 613
    assert current["declaration_sha256"] == hashlib.sha256(v070.read_bytes()).hexdigest()
    assert current["excluded_items"] == [{"issue": 287, "reason": "Manual trust work is non-blocking."}]
    assert current["delivered_items"] == [{"issue": 222, "reason": "Delivered release context."}]


def test_evidence_binds_required_states_to_the_candidate_not_exclusions(tmp_path: Path, monkeypatch) -> None:
    monkeypatch.chdir(tmp_path)
    scopes = tmp_path / "scopes"
    _declaration(scopes)
    manifest_path = _manifest(tmp_path)
    invocations = _stub_gh(monkeypatch, {525: "CLOSED", 526: "OPEN", 450: "OPEN"})

    evidence = build_evidence(scopes, manifest_path, _COMMIT, _REPOSITORY)

    assert evidence["schema"] == "checkpoint-b-release-scope/v2"
    assert evidence["candidate_version"] == "0.6.4"
    assert evidence["release_target"] == "0.6.4"
    assert evidence["source_commit"] == _COMMIT
    assert evidence["repository"] == _REPOSITORY
    assert evidence["candidate_manifest_sha256"] == hashlib.sha256(manifest_path.read_bytes()).hexdigest()
    assert [(item["issue"], item["state"]) for item in evidence["required_items"]] == [
        (525, "closed"),
        (526, "open"),
    ]
    assert evidence["excluded_items"] == [{"issue": 450, "reason": "Separate post-release refactoring."}]
    assert [int(argv[3]) for argv in invocations] == [525, 526]


def test_build_evidence_rejects_a_manifest_from_another_commit(tmp_path: Path, monkeypatch) -> None:
    monkeypatch.chdir(tmp_path)
    scopes = tmp_path / "scopes"
    _declaration(scopes)
    _stub_gh(monkeypatch, {})

    with pytest.raises(ValueError, match="source commit does not match"):
        build_evidence(scopes, _manifest(tmp_path, commit="c" * 40), _COMMIT, _REPOSITORY)


@pytest.mark.parametrize("version", ["0.7.0-preview.1", "0.6.5", "0.7.1"])
def test_unsupported_preview_or_unmapped_candidate_target_fails_closed(
    tmp_path: Path, monkeypatch, version: str
) -> None:
    monkeypatch.chdir(tmp_path)
    scopes = tmp_path / "scopes"
    _declaration(scopes, name="not-semantic.json")
    _stub_gh(monkeypatch, {})

    with pytest.raises(ValueError, match="exact stable release target|No reviewed release-scope declaration"):
        build_evidence(scopes, _manifest(tmp_path, version), _COMMIT, _REPOSITORY)


def test_filename_cannot_redirect_target_selection(tmp_path: Path, monkeypatch) -> None:
    monkeypatch.chdir(tmp_path)
    scopes = tmp_path / "scopes"
    _declaration(scopes, "0.6.4", name="0.7.0.json")
    _stub_gh(monkeypatch, {})

    with pytest.raises(ValueError, match="No reviewed release-scope declaration matches candidate target 0.7.0"):
        build_evidence(scopes, _manifest(tmp_path, "0.7.0"), _COMMIT, _REPOSITORY)


def test_duplicate_declarations_for_one_target_fail_closed(tmp_path: Path, monkeypatch) -> None:
    monkeypatch.chdir(tmp_path)
    scopes = tmp_path / "scopes"
    _declaration(scopes, name="first.json")
    _declaration(scopes, name="second.json")
    _stub_gh(monkeypatch, {})

    with pytest.raises(ValueError, match="Multiple release-scope declarations"):
        build_evidence(scopes, _manifest(tmp_path), _COMMIT, _REPOSITORY)


def test_unresolvable_required_issue_is_fatal(tmp_path: Path, monkeypatch) -> None:
    monkeypatch.chdir(tmp_path)
    scopes = tmp_path / "scopes"
    _declaration(scopes)
    _stub_gh(monkeypatch, {}, returncode=1, stderr="not found")

    with pytest.raises(ValueError, match="Cannot resolve issue #525"):
        build_evidence(scopes, _manifest(tmp_path), _COMMIT, _REPOSITORY)


def test_malformed_required_issue_response_is_fatal(tmp_path: Path, monkeypatch) -> None:
    monkeypatch.chdir(tmp_path)
    scopes = tmp_path / "scopes"
    _declaration(scopes)

    def fake_run(argv, **kwargs):
        return subprocess.CompletedProcess(argv, 0, stdout="{}", stderr="")

    monkeypatch.setattr(generator.subprocess, "run", fake_run)

    with pytest.raises(ValueError, match="Cannot resolve issue #525: invalid GitHub response"):
        build_evidence(scopes, _manifest(tmp_path), _COMMIT, _REPOSITORY)


@pytest.mark.parametrize(
    ("declaration", "message"),
    [
        ({"schema": "other/v1"}, "not a release-scope declaration"),
        (
            {
                "schema": "checkpoint-b-release-scope-declaration/v2",
                "declaration_id": "v0.6.4-authority",
                "release_target": "0.6.4",
                "story": 527,
                "required_items": [],
                "excluded_items": [],
                "delivered_items": [],
            },
            "no required items",
        ),
        (
            {
                "schema": "checkpoint-b-release-scope-declaration/v2",
                "declaration_id": "v0.6.4-authority",
                "release_target": "0.6.4",
                "story": 527,
                "required_items": [{"issue": 525}, {"issue": 525}],
                "excluded_items": [],
                "delivered_items": [],
            },
            "duplicate required items",
        ),
        (
            {
                "schema": "checkpoint-b-release-scope-declaration/v2",
                "declaration_id": "v0.6.4-authority",
                "release_target": "0.6.4",
                "story": 527,
                "required_items": [{"issue": 525}],
                "excluded_items": [{"issue": 525, "reason": "Duplicated."}],
                "delivered_items": [],
            },
            "repeats an item",
        ),
        (
            {
                "schema": "checkpoint-b-release-scope-declaration/v2",
                "declaration_id": "invalid identity",
                "release_target": "0.6.4",
                "story": 527,
                "required_items": [{"issue": 525}],
                "excluded_items": [],
                "delivered_items": [],
            },
            "invalid declaration identity",
        ),
    ],
)
def test_malformed_declaration_is_rejected(tmp_path: Path, declaration: dict, message: str) -> None:
    path = tmp_path / "scope.json"
    path.write_text(json.dumps(declaration), encoding="utf-8")

    with pytest.raises(ValueError, match=message):
        _read_declaration(path)


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


@pytest.mark.parametrize("value", [0, -1, "525", None, True, 4.0])
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


def test_path_inside_the_workspace_is_accepted(tmp_path: Path, monkeypatch) -> None:
    monkeypatch.chdir(tmp_path)

    assert _safe_path(Path("nested/file.json"), "output") == (tmp_path / "nested" / "file.json").resolve()


def test_incomparable_root_is_treated_as_not_containing(tmp_path: Path, monkeypatch) -> None:
    monkeypatch.setattr(
        _release_workspace.os.path,
        "commonpath",
        lambda paths: (_ for _ in ()).throw(ValueError("different drives")),
    )

    with pytest.raises(ValueError, match="resolves outside the release workspace"):
        _safe_path(tmp_path / "file.json", "output")


def test_main_writes_target_selected_inventory_to_the_fixed_location(tmp_path: Path, monkeypatch) -> None:
    monkeypatch.chdir(tmp_path)
    scopes = tmp_path / "scopes"
    _declaration(scopes)
    _stub_gh(monkeypatch, {525: "CLOSED", 526: "CLOSED"})
    output = tmp_path / "artifacts" / "checkpoint-b" / "release-scope.json"
    monkeypatch.setattr(generator, "_declarations_directory", lambda: scopes)
    monkeypatch.setattr(generator, "_candidate_manifest_path", lambda: _manifest(tmp_path))
    monkeypatch.setattr(generator, "_output_path", lambda: output)
    monkeypatch.setattr(
        sys,
        "argv",
        [
            "create_release_scope_evidence.py",
            "--source-commit",
            _COMMIT,
            "--repository",
            _REPOSITORY,
        ],
    )

    assert generator.main() == 0
    written = json.loads(output.read_text(encoding="utf-8"))
    assert written["candidate_version"] == "0.6.4"
    assert [item["state"] for item in written["required_items"]] == ["closed", "closed"]


def test_main_rejects_caller_controlled_scope_paths(monkeypatch) -> None:
    monkeypatch.setattr(
        sys,
        "argv",
        [
            "create_release_scope_evidence.py",
            "--source-commit",
            _COMMIT,
            "--repository",
            _REPOSITORY,
            "--scope-dir",
            "/tmp/elsewhere",
        ],
    )

    with pytest.raises(SystemExit):
        generator.main()


def test_fixed_locations_stay_inside_the_release_workspace() -> None:
    root = generator._repository_root()

    assert generator._declarations_directory().is_relative_to(root)
    assert generator._candidate_manifest_path().is_relative_to(root)
    assert generator._output_path().is_relative_to(root)


def test_shipped_declarations_preserve_both_reviewed_release_authorities() -> None:
    declarations = [
        _read_declaration(path)
        for path in sorted(generator._declarations_directory().glob("*.json"))
    ]

    by_target = {declaration["release_target"]: declaration for declaration in declarations}
    assert len(declarations) == len(by_target) == 2
    assert set(by_target) == {"0.6.4", "0.7.0"}
    assert by_target["0.6.4"]["story"] == 527
    assert {item["issue"] for item in by_target["0.6.4"]["required_items"]} == {525, 526}
    assert by_target["0.7.0"]["story"] == 613
    assert {item["issue"] for item in by_target["0.7.0"]["required_items"]} == {116, 234, 267, 269, 614}
    assert by_target["0.7.0"]["excluded_items"] == [
        {
            "issue": 287,
            "reason": "OpenSSF Metal-series passing self-assessment is manual/external trust housekeeping and is non-blocking by #613 design.",
        }
    ]
    assert by_target["0.7.0"]["delivered_items"] == [
        {
            "issue": 222,
            "reason": "Architecture policy badge is completed release context and is not an open v0.7 publication blocker.",
        }
    ]
