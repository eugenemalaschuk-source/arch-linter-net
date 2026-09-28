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
        "state.setdefault('events', [])\n"
        "def save(): state_path.write_text(json.dumps(state), encoding='utf-8')\n"
        "if args[0] == 'api':\n"
        "    path = args[1]\n"
        "    if path.startswith('repos/example/project/git/matching-refs/tags/'):\n"
        "        ref = state.get('tag_sha')\n"
        "        print(json.dumps([] if ref is None else [{'ref': 'refs/tags/v0.9.0', 'object': {'sha': ref, 'type': 'commit'}}]))\n"
        "    elif path.startswith('repos/example/project/releases/tags/'):\n"
        "        release = state.get('release')\n"
        "        if release is None:\n"
        "            print('gh: Not Found (HTTP 404)', file=sys.stderr); sys.exit(1)\n"
        "        print(json.dumps({'tag_name': release['tag_name'], 'draft': release.get('draft', False), 'immutable': release.get('immutable', False), 'target_commitish': release.get('target_commitish', ''), 'assets': [{'name': name} for name in release['assets']]}))\n"
        "elif args[:2] == ['release', 'create']:\n"
        "    tag = args[2]; commit = args[args.index('--target') + 1]\n"
        "    state['release'] = {'tag_name': tag, 'draft': '--draft' in args, 'immutable': False, 'target_commitish': commit, 'assets': {}}\n"
        "    if '--draft' not in args: state['tag_sha'] = commit\n"
        "    state['events'].append('create-draft' if '--draft' in args else 'create-published'); save()\n"
        "elif args[:2] == ['release', 'edit']:\n"
        "    state['tag_sha'] = state['release'].get('target_commitish', state.get('tag_sha')); state['release']['draft'] = False; state['release']['immutable'] = True; state['events'].append('publish'); save()\n"
        "elif args[:2] == ['release', 'upload']:\n"
        "    release = state['release']; path = pathlib.Path(args[3]); release['assets'][path.name] = base64.b64encode(path.read_bytes()).decode('ascii'); state['events'].append('upload:' + path.name); save()\n"
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
    assert state["release"]["draft"] is False
    assert state["release"]["immutable"] is True
    assert state["events"] == ["create-draft", f"upload:{asset.name}", "publish"]
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


def test_publisher_resumes_matching_draft_and_publishes_only_after_asset_verification(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    candidate_sha = "9" * 40
    state_path = tmp_path / "state.json"
    state_path.write_text(
        json.dumps({"tag_sha": candidate_sha, "release": {"tag_name": "v0.9.0", "draft": True, "assets": {}}}),
        encoding="utf-8",
    )
    _fake_gh(tmp_path / "bin", state_path, monkeypatch)
    asset = tmp_path / "release-forensics.json"
    asset.write_bytes(b"draft candidate report")
    notes = tmp_path / "notes.md"
    notes.write_text("Release notes\n", encoding="utf-8")

    publish_release_assets.publish(_arguments(asset, notes, candidate_sha))

    state = _state(state_path)
    assert state["release"]["draft"] is False
    assert state["events"] == [f"upload:{asset.name}", "publish"]


def test_publisher_resumes_draft_without_tag_when_target_matches_candidate(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    candidate_sha = "6" * 40
    state_path = tmp_path / "state.json"
    state_path.write_text(
        json.dumps(
            {
                "tag_sha": None,
                "release": {
                    "tag_name": "v0.9.0",
                    "draft": True,
                    "immutable": False,
                    "target_commitish": candidate_sha,
                    "assets": {},
                },
            }
        ),
        encoding="utf-8",
    )
    _fake_gh(tmp_path / "bin", state_path, monkeypatch)
    asset = tmp_path / "release-forensics.json"
    asset.write_bytes(b"recovered untagged draft report")
    notes = tmp_path / "notes.md"
    notes.write_text("Release notes\n", encoding="utf-8")

    publish_release_assets.publish(_arguments(asset, notes, candidate_sha))

    state = _state(state_path)
    assert state["tag_sha"] == candidate_sha
    assert state["release"]["draft"] is False
    assert state["events"] == [f"upload:{asset.name}", "publish"]


def test_publisher_refuses_untagged_draft_with_a_different_target(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    candidate_sha = "5" * 40
    state_path = tmp_path / "state.json"
    state_path.write_text(
        json.dumps(
            {
                "tag_sha": None,
                "release": {
                    "tag_name": "v0.9.0",
                    "draft": True,
                    "immutable": False,
                    "target_commitish": "4" * 40,
                    "assets": {},
                },
            }
        ),
        encoding="utf-8",
    )
    _fake_gh(tmp_path / "bin", state_path, monkeypatch)
    asset = tmp_path / "release-forensics.json"
    asset.write_bytes(b"candidate report")
    notes = tmp_path / "notes.md"
    notes.write_text("Release notes\n", encoding="utf-8")

    with pytest.raises(publish_release_assets.ReleaseAssetError, match="matching draft target"):
        publish_release_assets.publish(_arguments(asset, notes, candidate_sha))

    assert _state(state_path)["release"]["assets"] == {}


def test_publisher_fails_before_upload_for_incomplete_immutable_release(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    candidate_sha = "8" * 40
    state_path = tmp_path / "state.json"
    state_path.write_text(
        json.dumps(
            {
                "tag_sha": candidate_sha,
                "release": {"tag_name": "v0.9.0", "draft": False, "immutable": True, "assets": {}},
            }
        ),
        encoding="utf-8",
    )
    _fake_gh(tmp_path / "bin", state_path, monkeypatch)
    asset = tmp_path / "release-forensics.json"
    asset.write_bytes(b"candidate report")
    notes = tmp_path / "notes.md"
    notes.write_text("Release notes\n", encoding="utf-8")

    with pytest.raises(publish_release_assets.ReleaseAssetError, match="new release version"):
        publish_release_assets.publish(_arguments(asset, notes, candidate_sha))

    state = _state(state_path)
    assert state["release"]["assets"] == {}
    assert not any(event.startswith("upload:") for event in state.get("events", []))


def test_publisher_verifies_complete_immutable_release_without_mutating_it(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    candidate_sha = "7" * 40
    content = b"complete immutable report"
    state_path = tmp_path / "state.json"
    state_path.write_text(
        json.dumps(
            {
                "tag_sha": candidate_sha,
                "release": {
                    "tag_name": "v0.9.0",
                    "draft": False,
                    "immutable": True,
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

    assert _state(state_path)["release"]["assets"][asset.name] == base64.b64encode(content).decode("ascii")
    assert not any(
        event.startswith("upload:") or event == "publish"
        for event in _state(state_path).get("events", [])
    )


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


@pytest.mark.parametrize("tag", ["v0.2.0-rc.1", "v0.2.0-alpha.1", "v0.2.0+build.123"])
def test_publisher_accepts_the_complete_nuget_semver_tag_surface(tag: str) -> None:
    candidate_sha = "c" * 40
    arguments = Namespace(repository="example/project", tag=tag, candidate_sha=candidate_sha)

    assert publish_release_assets._validate_identity(arguments) == (  # noqa: SLF001
        "example/project",
        tag,
        candidate_sha,
    )


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
