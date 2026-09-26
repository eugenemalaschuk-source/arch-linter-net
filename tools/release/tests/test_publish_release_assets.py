from __future__ import annotations

import base64
import json
import os
import sys
from argparse import Namespace
from pathlib import Path

import pytest


_RELEASE_DIRECTORY = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(_RELEASE_DIRECTORY))

import publish_release_assets  # noqa: E402


def _fake_gh(directory: Path, state_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    directory.mkdir()
    executable = directory / "gh"
    executable.write_text(
        "#!/usr/bin/env python3\n"
        "import base64, json, os, pathlib, sys\n"
        "state_path = pathlib.Path(os.environ['FAKE_GH_STATE'])\n"
        "state = json.loads(state_path.read_text(encoding='utf-8'))\n"
        "args = sys.argv[1:]\n"
        "def save(): state_path.write_text(json.dumps(state), encoding='utf-8')\n"
        "if args[0] == 'api':\n"
        "    ref = state.get('tag_sha')\n"
        "    print(json.dumps([] if ref is None else [{'ref': 'refs/tags/v0.9.0', 'object': {'sha': ref, 'type': 'commit'}}]))\n"
        "elif args[:2] == ['release', 'view']:\n"
        "    release = state.get('release')\n"
        "    if release is None:\n"
        "        print('release not found', file=sys.stderr); sys.exit(1)\n"
        "    print(json.dumps({'tagName': release['tag_name'], 'assets': [{'name': name} for name in release['assets']]}))\n"
        "elif args[:2] == ['release', 'create']:\n"
        "    tag = args[2]; commit = args[args.index('--target') + 1]\n"
        "    state['tag_sha'] = commit; state['release'] = {'tag_name': tag, 'assets': {}}; save()\n"
        "elif args[:2] == ['release', 'upload']:\n"
        "    release = state['release']; path = pathlib.Path(args[3]); release['assets'][path.name] = base64.b64encode(path.read_bytes()).decode('ascii'); save()\n"
        "elif args[:2] == ['release', 'download']:\n"
        "    name = args[args.index('--pattern') + 1]; directory = pathlib.Path(args[args.index('--dir') + 1])\n"
        "    content = state['release']['assets'].get(name)\n"
        "    if content is None: print('asset not found', file=sys.stderr); sys.exit(1)\n"
        "    (directory / name).write_bytes(base64.b64decode(content))\n"
        "else:\n"
        "    print('unsupported fake gh command: ' + repr(args), file=sys.stderr); sys.exit(2)\n",
        encoding="utf-8",
    )
    executable.chmod(0o755)
    previous_path = os.environ.get("PATH", "")
    monkeypatch.setenv("PATH", f"{directory}:{previous_path}")
    monkeypatch.setenv("FAKE_GH_STATE", str(state_path))


def _arguments(asset: Path, notes: Path, candidate_sha: str) -> Namespace:
    return Namespace(
        repository="example/project",
        tag="v0.9.0",
        candidate_sha=candidate_sha,
        title="v0.9.0",
        notes_file=str(notes),
        create_if_missing=True,
        asset=[str(asset)],
    )


def _state(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def test_publisher_creates_missing_release_uploads_without_clobber_and_reads_back(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    candidate_sha = "a" * 40
    state_path = tmp_path / "state.json"
    state_path.write_text(json.dumps({"tag_sha": None, "release": None}), encoding="utf-8")
    _fake_gh(tmp_path / "bin", state_path, monkeypatch)
    asset = tmp_path / "release-forensics.json"
    asset.write_bytes(b"candidate report bytes")
    notes = tmp_path / "notes.md"
    notes.write_text("Release notes\n", encoding="utf-8")

    publish_release_assets.publish(_arguments(asset, notes, candidate_sha))

    state = _state(state_path)
    assert state["tag_sha"] == candidate_sha
    assert state["release"]["tag_name"] == "v0.9.0"
    assert base64.b64decode(state["release"]["assets"][asset.name]) == asset.read_bytes()


def test_publisher_resumes_matching_release_asset_without_replacing_it(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    candidate_sha = "b" * 40
    content = b"exact existing report"
    state_path = tmp_path / "state.json"
    state_path.write_text(
        json.dumps(
            {
                "tag_sha": candidate_sha,
                "release": {
                    "tag_name": "v0.9.0",
                    "assets": {"release-forensics.json": base64.b64encode(content).decode("ascii")},
                },
            }
        ),
        encoding="utf-8",
    )
    _fake_gh(tmp_path / "bin", state_path, monkeypatch)
    asset = tmp_path / "release-forensics.json"
    asset.write_bytes(content)
    notes = tmp_path / "notes.md"
    notes.write_text("Release notes\n", encoding="utf-8")

    publish_release_assets.publish(_arguments(asset, notes, candidate_sha))

    assert base64.b64decode(_state(state_path)["release"]["assets"][asset.name]) == content


def test_publisher_resumes_release_by_uploading_a_missing_asset(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    candidate_sha = "d" * 40
    state_path = tmp_path / "state.json"
    state_path.write_text(
        json.dumps({"tag_sha": candidate_sha, "release": {"tag_name": "v0.9.0", "assets": {}}}),
        encoding="utf-8",
    )
    _fake_gh(tmp_path / "bin", state_path, monkeypatch)
    asset = tmp_path / "release-forensics.json"
    asset.write_bytes(b"recovered missing asset")
    notes = tmp_path / "notes.md"
    notes.write_text("Release notes\n", encoding="utf-8")

    publish_release_assets.publish(_arguments(asset, notes, candidate_sha))

    assert base64.b64decode(_state(state_path)["release"]["assets"][asset.name]) == asset.read_bytes()


def test_publisher_fails_closed_on_existing_asset_digest_mismatch(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    candidate_sha = "c" * 40
    state_path = tmp_path / "state.json"
    state_path.write_text(
        json.dumps(
            {
                "tag_sha": candidate_sha,
                "release": {
                    "tag_name": "v0.9.0",
                    "assets": {"release-forensics.json": base64.b64encode(b"other bytes").decode("ascii")},
                },
            }
        ),
        encoding="utf-8",
    )
    _fake_gh(tmp_path / "bin", state_path, monkeypatch)
    asset = tmp_path / "release-forensics.json"
    asset.write_bytes(b"candidate bytes")
    notes = tmp_path / "notes.md"
    notes.write_text("Release notes\n", encoding="utf-8")

    arguments = _arguments(asset, notes, candidate_sha)
    with pytest.raises(publish_release_assets.ReleaseAssetError, match="refusing to replace"):
        publish_release_assets.publish(arguments)

    assert base64.b64decode(_state(state_path)["release"]["assets"][asset.name]) == b"other bytes"


def test_publisher_refuses_a_target_tag_on_another_commit(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    candidate_sha = "e" * 40
    state_path = tmp_path / "state.json"
    state_path.write_text(
        json.dumps(
            {
                "tag_sha": "f" * 40,
                "release": {"tag_name": "v0.9.0", "assets": {}},
            }
        ),
        encoding="utf-8",
    )
    _fake_gh(tmp_path / "bin", state_path, monkeypatch)
    asset = tmp_path / "release-forensics.json"
    asset.write_bytes(b"candidate bytes")
    notes = tmp_path / "notes.md"
    notes.write_text("Release notes\n", encoding="utf-8")

    arguments = _arguments(asset, notes, candidate_sha)
    with pytest.raises(publish_release_assets.ReleaseAssetError, match="exact candidate commit"):
        publish_release_assets.publish(arguments)

    assert _state(state_path)["tag_sha"] == "f" * 40
    assert _state(state_path)["release"]["assets"] == {}


def test_publisher_main_reads_release_identity_from_the_workflow_environment(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    candidate_sha = "a" * 40
    state_path = tmp_path / "state.json"
    state_path.write_text(json.dumps({"tag_sha": None, "release": None}), encoding="utf-8")
    _fake_gh(tmp_path / "bin", state_path, monkeypatch)
    asset = tmp_path / "release-forensics.json"
    asset.write_bytes(b"candidate bytes")
    notes = tmp_path / "notes.md"
    notes.write_text("Release notes\n", encoding="utf-8")
    monkeypatch.setenv("GH_REPO", "example/project")
    monkeypatch.setenv("TARGET_TAG", "v0.9.0")
    monkeypatch.setenv("RELEASE_COMMIT", candidate_sha)
    monkeypatch.setattr(
        sys,
        "argv",
        ["publish_release_assets.py", "--notes-file", str(notes), "--create-if-missing", "--asset", str(asset)],
    )

    assert publish_release_assets.main() == 0
    assert _state(state_path)["tag_sha"] == candidate_sha


@pytest.mark.parametrize(
    ("repository", "tag", "candidate_sha", "message"),
    [
        ("--repo/project", "v0.9.0", "a" * 40, "Repository identity"),
        ("example/project", "--help", "a" * 40, "Release tag"),
        ("example/project", "v0.9.0", "--help", "Candidate commit"),
    ],
)
def test_publisher_rejects_untrusted_release_identity(
    repository: str, tag: str, candidate_sha: str, message: str
) -> None:
    arguments = Namespace(repository=repository, tag=tag, candidate_sha=candidate_sha)

    with pytest.raises(publish_release_assets.ReleaseAssetError, match=message):
        publish_release_assets._validate_identity(arguments)  # noqa: SLF001
