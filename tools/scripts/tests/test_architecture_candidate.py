from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import architecture_candidate as candidate  # noqa: E402


SOURCE_SHA = "a" * 40
TREE_SHA = "b" * 40


@pytest.fixture
def candidate_inputs(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> dict[str, Path | str]:
    root = tmp_path / "repository"
    root.mkdir()
    policy = root / "architecture" / "dependencies.arch.yml"
    cli = root / "src" / "ArchLinterNet.Cli" / "bin" / "ArchLinterNet.Cli.dll"
    testing = root / "src" / "ArchLinterNet.Testing" / "bin" / "ArchLinterNet.Testing.dll"
    policy.parent.mkdir(parents=True)
    cli.parent.mkdir(parents=True)
    testing.parent.mkdir(parents=True)
    policy.write_bytes(b"policy\n")
    cli.write_bytes(b"cli assembly\n")
    testing.write_bytes(b"testing assembly\n")
    monkeypatch.setattr(candidate, "_ensure_clean", lambda _root: None)
    monkeypatch.setattr(candidate, "_git_identity", lambda _root: (SOURCE_SHA, TREE_SHA))
    return {
        "root": root,
        "policy": policy,
        "cli": cli,
        "testing": testing,
        "manifest": tmp_path / "candidate.json",
    }


def test_create_and_verify_bind_one_deterministic_candidate(candidate_inputs: dict[str, Path | str]) -> None:
    manifest = candidate.create_manifest(
        candidate_inputs["root"],
        policy_path=candidate_inputs["policy"],
        cli_assembly_path=candidate_inputs["cli"],
        testing_assembly_path=candidate_inputs["testing"],
        source_sha=SOURCE_SHA,
        tool_identity="cli=ArchLinterNet.Cli;testing=ArchLinterNet.Testing",
    )
    candidate_inputs["manifest"].write_text(candidate.canonical_json(manifest), encoding="utf-8")

    verified = candidate.verify_manifest(
        candidate_inputs["root"],
        candidate_inputs["manifest"],
        expected_source_sha=SOURCE_SHA,
        expected_tool_identity="cli=ArchLinterNet.Cli;testing=ArchLinterNet.Testing",
    )

    assert verified == manifest
    assert candidate.canonical_json(manifest) == candidate_inputs["manifest"].read_text(encoding="utf-8")
    assert manifest["policy_sha256"] == hashlib.sha256(candidate_inputs["policy"].read_bytes()).hexdigest()


def test_cli_commands_emit_the_canonical_manifest(
    candidate_inputs: dict[str, Path | str], capsys: pytest.CaptureFixture[str]
) -> None:
    identity = "cli=ArchLinterNet.Cli;testing=ArchLinterNet.Testing"
    create_args = [
        "create",
        "--repository-root",
        str(candidate_inputs["root"]),
        "--manifest",
        str(candidate_inputs["manifest"]),
        "--source-sha",
        SOURCE_SHA,
        "--policy",
        str(candidate_inputs["policy"]),
        "--cli-assembly",
        str(candidate_inputs["cli"]),
        "--testing-assembly",
        str(candidate_inputs["testing"]),
        "--tool-identity",
        identity,
    ]

    assert candidate.main(create_args) == 0
    created_output = capsys.readouterr().out
    assert created_output == candidate_inputs["manifest"].read_text(encoding="utf-8")

    assert candidate.main(
        [
            "verify",
            "--repository-root",
            str(candidate_inputs["root"]),
            "--manifest",
            str(candidate_inputs["manifest"]),
            "--source-sha",
            SOURCE_SHA,
            "--tool-identity",
            identity,
        ]
    ) == 0
    assert capsys.readouterr().out == created_output


def test_verify_rejects_missing_manifest_input(candidate_inputs: dict[str, Path | str]) -> None:
    manifest = candidate.create_manifest(
        candidate_inputs["root"],
        policy_path=candidate_inputs["policy"],
        cli_assembly_path=candidate_inputs["cli"],
        testing_assembly_path=candidate_inputs["testing"],
    )
    candidate_inputs["manifest"].write_text(candidate.canonical_json(manifest), encoding="utf-8")
    candidate_inputs["testing"].unlink()

    with pytest.raises(candidate.CandidateIdentityError, match="Missing candidate Testing assembly"):
        candidate.verify_manifest(candidate_inputs["root"], candidate_inputs["manifest"])


@pytest.mark.parametrize(
    ("field", "message"),
    [
        ("source_sha", "source SHA"),
        ("tree_sha", "tree SHA"),
        ("tool_identity", "tool identity"),
    ],
)
def test_verify_rejects_mismatched_identity(
    candidate_inputs: dict[str, Path | str], field: str, message: str
) -> None:
    manifest = candidate.create_manifest(
        candidate_inputs["root"],
        policy_path=candidate_inputs["policy"],
        cli_assembly_path=candidate_inputs["cli"],
        testing_assembly_path=candidate_inputs["testing"],
    )
    manifest[field] = "c" * 40 if field != "tool_identity" else "different-tool"
    candidate_inputs["manifest"].write_text(candidate.canonical_json(manifest), encoding="utf-8")

    with pytest.raises(candidate.CandidateIdentityError, match=message):
        candidate.verify_manifest(candidate_inputs["root"], candidate_inputs["manifest"])


def test_verify_rejects_mismatched_policy_digest(candidate_inputs: dict[str, Path | str]) -> None:
    manifest = candidate.create_manifest(
        candidate_inputs["root"],
        policy_path=candidate_inputs["policy"],
        cli_assembly_path=candidate_inputs["cli"],
        testing_assembly_path=candidate_inputs["testing"],
    )
    manifest["policy_sha256"] = "0" * 64
    candidate_inputs["manifest"].write_text(candidate.canonical_json(manifest), encoding="utf-8")

    with pytest.raises(candidate.CandidateIdentityError, match="policy SHA-256"):
        candidate.verify_manifest(candidate_inputs["root"], candidate_inputs["manifest"])


def test_verify_rejects_malformed_manifest(candidate_inputs: dict[str, Path | str]) -> None:
    candidate_inputs["manifest"].write_text(json.dumps({"schema": candidate.SCHEMA}), encoding="utf-8")

    with pytest.raises(candidate.CandidateIdentityError, match="fields are missing"):
        candidate.verify_manifest(candidate_inputs["root"], candidate_inputs["manifest"])


def test_create_rejects_missing_tool_identity(candidate_inputs: dict[str, Path | str]) -> None:
    with pytest.raises(candidate.CandidateIdentityError, match="tool identity"):
        candidate.create_manifest(
            candidate_inputs["root"],
            policy_path=candidate_inputs["policy"],
            cli_assembly_path=candidate_inputs["cli"],
            testing_assembly_path=candidate_inputs["testing"],
            tool_identity="",
        )
