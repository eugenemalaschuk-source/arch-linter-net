"""Consumer setup data must cross the API boundary, never the workspace boundary."""
from __future__ import annotations

import base64
import hashlib
import json
from pathlib import Path
import sys

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))
from badge_promotion import cli

REPOSITORY = "synthetic-owner/synthetic-repo"
COMMIT = "a" * 40
PATH = ".github/badge-promotion/registry.json"
CONTENT_URL = f"/repos/{REPOSITORY}/contents/{PATH}?ref={COMMIT}"
REPO_URL = f"/repos/{REPOSITORY}"
COMMIT_URL = REPO_URL + f"/git/commits/{COMMIT}"
TREES = ("b" * 40, "c" * 40, "d" * 40)
TREE_URLS = [REPO_URL + f"/git/trees/{sha}" for sha in TREES]
FILE_CALLS = [COMMIT_URL, *TREE_URLS, CONTENT_URL]


def config_document() -> dict:
    value = json.loads((Path(__file__).parent / "fixtures/approved-config.json").read_text())
    value["repository_visibility"] = "private"
    return value


def envelope(config: dict | None = None) -> dict:
    return {"schema": "architecture-health-badge-promotion/registry/v1", "configurations": {"setup_generated": config or config_document()}}


def contents(data: bytes | None = None) -> dict:
    data = json.dumps(envelope()).encode() if data is None else data
    return {"type": "file", "path": PATH, "encoding": "base64", "size": len(data),
            "sha": hashlib.sha1(f"blob {len(data)}\0".encode() + data, usedforsecurity=False).hexdigest(),
            "content": base64.encodebytes(data).decode()}


class Api:
    def __init__(self, document: object | None = None) -> None:
        self.document = contents() if document is None else document
        self.repository = {"full_name": REPOSITORY, "private": True}
        self.calls: list[str] = []
        self.overrides: dict[str, object] = {}

    def request(self, path: str) -> object:
        self.calls.append(path)
        if path in self.overrides:
            return self.overrides[path]
        if path == COMMIT_URL:
            return {"sha": COMMIT, "tree": {"sha": TREES[0]}}
        if path in TREE_URLS:
            i = TREE_URLS.index(path)
            return {"sha": TREES[i], "truncated": False, "tree": [
                {"path": PATH.split("/")[i], "mode": "100644" if i == 2 else "040000",
                 "type": "blob" if i == 2 else "tree",
                 "sha": self.document.get("sha", "e" * 40) if i == 2 else TREES[i + 1]}
            ]}
        if path == CONTENT_URL:
            return self.document
        if path == REPO_URL:
            return self.repository
        raise AssertionError("Unexpected API request: " + path)


@pytest.fixture
def api(monkeypatch: pytest.MonkeyPatch) -> Api:
    api = Api()
    monkeypatch.setattr(cli, "GitHubApi", lambda: api)
    for key, value in {"GITHUB_REPOSITORY": REPOSITORY, "GITHUB_SHA": COMMIT,
                       "GITHUB_REF": "refs/heads/main", "GITHUB_EVENT_NAME": "push"}.items():
        monkeypatch.setenv(key, value)
    return api


@pytest.mark.parametrize("event", ["push", "schedule"])
def test_generated_registry_loads_at_exact_consumer_commit(api: Api, monkeypatch: pytest.MonkeyPatch, event: str) -> None:
    monkeypatch.setenv("GITHUB_EVENT_NAME", event)
    loaded = cli._load_config("setup_generated")
    assert loaded.repository == REPOSITORY
    assert loaded.repository_visibility == "private"
    assert loaded.base_ref == "main"
    assert api.calls == [*FILE_CALLS, REPO_URL]


def test_workspace_shadow_registry_and_code_are_not_used(api: Api, monkeypatch: pytest.MonkeyPatch, tmp_path: Path) -> None:
    path = tmp_path / PATH
    path.parent.mkdir(parents=True)
    path.write_text('{"not":"approved"}')
    (tmp_path / "config.py").write_text('raise AssertionError("consumer code executed")')
    monkeypatch.chdir(tmp_path)
    monkeypatch.setenv("GITHUB_WORKSPACE", str(tmp_path))
    assert cli._load_config("setup_generated").repository == REPOSITORY
    assert path.read_text() == '{"not":"approved"}'


@pytest.mark.parametrize("identifier", ["reference-none", "reference-public-raw"])
def test_reference_registry_remains_package_owned(api: Api, identifier: str) -> None:
    assert cli._load_config(identifier).repository == "eugenemalaschuk-source/arch-linter-net"
    assert api.calls == []


@pytest.mark.parametrize("key,value", [
    ("GITHUB_EVENT_NAME", "pull_request"), ("GITHUB_EVENT_NAME", "pull_request_target"),
    ("GITHUB_EVENT_NAME", "workflow_dispatch"), ("GITHUB_EVENT_NAME", ""),
    ("GITHUB_REF", "refs/tags/main"), ("GITHUB_REF", "refs/pull/1/merge"),
    ("GITHUB_REF", "refs/heads/../main"), ("GITHUB_REF", "refs/heads/"),
    ("GITHUB_SHA", "main"), ("GITHUB_SHA", "a" * 39), ("GITHUB_SHA", "a" * 40 + "\n"),
    ("GITHUB_REPOSITORY", "../repo"), ("GITHUB_REPOSITORY", "owner/repo/extra"),
    ("GITHUB_REPOSITORY", "https://example.invalid/owner/repo"),
])
def test_unsupported_context_fails_before_api(api: Api, monkeypatch: pytest.MonkeyPatch, key: str, value: str) -> None:
    monkeypatch.setenv(key, value)
    with pytest.raises(cli.ProviderFailure, match="^approved_configuration_invalid$"):
        cli._load_config("setup_generated")
    assert api.calls == []


@pytest.mark.parametrize("change", [
    {"type": "dir"}, {"type": "symlink"}, {"path": "other.json"}, {"encoding": "none"},
    {"size": True}, {"size": -1}, {"size": 65537}, {"size": 1},
    {"sha": "b" * 40}, {"content": "!"}, {"content": "a" * 131073},
    {"content": "\u2603"}, {"target": "elsewhere"}, {"submodule_git_url": "https://example.invalid"},
])
def test_untrusted_contents_metadata_or_bytes_are_rejected(api: Api, change: dict) -> None:
    api.document = {**contents(), **change}
    with pytest.raises(cli.ProviderFailure, match="^approved_configuration_invalid$"):
        cli._load_config("setup_generated")
    assert api.calls == FILE_CALLS


@pytest.mark.parametrize("data", [b"", b"[]", b"null", b"{}", b"\xff", b"{bad}",
    b'{"schema":"x","schema":"y","configurations":{}}',
    b'{"schema":"x","configurations":NaN}', b"[" * 2000 + b"]" * 2000])
def test_invalid_registry_json_is_rejected(api: Api, data: bytes) -> None:
    api.document = contents(data)
    with pytest.raises(cli.ProviderFailure, match="^approved_configuration_invalid$"):
        cli._load_config("setup_generated")
    assert api.calls == FILE_CALLS


@pytest.mark.parametrize("field,value", [("repository", "another/repo"), ("base_ref", "develop"),
    ("disclosure_profile", "unknown"), ("destination", {"adapter": "arbitrary"})])
def test_registry_cannot_override_context_or_closed_contract(api: Api, field: str, value: object) -> None:
    config = config_document()
    config[field] = value
    api.document = contents(json.dumps(envelope(config)).encode())
    with pytest.raises(cli.ProviderFailure, match="^approved_configuration_invalid$"):
        cli._load_config("setup_generated")


@pytest.mark.parametrize("metadata", [{}, {"full_name": "another/repo", "private": True},
    {"full_name": REPOSITORY, "private": False}, {"full_name": REPOSITORY, "private": 1}, []])
def test_actual_repository_identity_and_visibility_must_match(api: Api, metadata: object) -> None:
    api.repository = metadata
    with pytest.raises(cli.ProviderFailure, match="^approved_configuration_invalid$"):
        cli._load_config("setup_generated")
    assert api.calls == [*FILE_CALLS, REPO_URL]


def test_api_authority_failure_has_no_local_fallback(api: Api, monkeypatch: pytest.MonkeyPatch) -> None:
    def unavailable(path: str) -> object:
        raise cli.ProviderFailure("required_capability_unavailable")
    monkeypatch.setattr(api, "request", unavailable)
    with pytest.raises(cli.ProviderFailure, match="^required_capability_unavailable$"):
        cli._load_config("setup_generated")


@pytest.mark.parametrize("mode,kind", [("120000", "blob"), ("160000", "commit"), ("040000", "tree")])
def test_registry_git_mode_rejects_symlinks_and_submodules(api: Api, mode: str, kind: str) -> None:
    api.overrides[TREE_URLS[2]] = {"sha": TREES[2], "truncated": False, "tree": [
        {"path": "registry.json", "mode": mode, "type": kind, "sha": api.document["sha"]}
    ]}
    with pytest.raises(cli.ProviderFailure, match="^approved_configuration_invalid$"):
        cli._load_config("setup_generated")
    assert CONTENT_URL not in api.calls


@pytest.mark.parametrize("document", [None, {}, {"sha": TREES[0], "truncated": True, "tree": []},
    {"sha": TREES[0], "truncated": False, "tree": []},
    {"sha": "f" * 40, "truncated": False, "tree": []}])
def test_invalid_or_incomplete_git_tree_has_no_fallback(api: Api, document: object) -> None:
    api.overrides[TREE_URLS[0]] = document
    with pytest.raises(cli.ProviderFailure, match="^approved_configuration_invalid$"):
        cli._load_config("setup_generated")
    assert CONTENT_URL not in api.calls


def test_contents_cannot_substitute_a_different_blob(api: Api) -> None:
    api.overrides[TREE_URLS[2]] = {"sha": TREES[2], "truncated": False, "tree": [
        {"path": "registry.json", "mode": "100644", "type": "blob", "sha": "e" * 40}
    ]}
    with pytest.raises(cli.ProviderFailure, match="^approved_configuration_invalid$"):
        cli._load_config("setup_generated")


@pytest.mark.parametrize("document", [None, {}, {"sha": "e" * 40, "tree": {"sha": TREES[0]}},
    {"sha": COMMIT, "tree": None}, {"sha": COMMIT, "tree": {"sha": "main"}}])
def test_invalid_git_commit_is_rejected(api: Api, document: object) -> None:
    api.overrides[COMMIT_URL] = document
    with pytest.raises(cli.ProviderFailure, match="^approved_configuration_invalid$"):
        cli._load_config("setup_generated")
    assert api.calls == [COMMIT_URL]


@pytest.mark.parametrize("entries", [None, {}, "tree"])
def test_non_array_git_entries_are_rejected(api: Api, entries: object) -> None:
    api.overrides[TREE_URLS[0]] = {"sha": TREES[0], "truncated": False, "tree": entries}
    with pytest.raises(cli.ProviderFailure, match="^approved_configuration_invalid$"):
        cli._load_config("setup_generated")
    assert CONTENT_URL not in api.calls


@pytest.mark.parametrize("field,value", [("schema", "unexpected/v1"), ("configurations", []), ("configurations", {})])
def test_closed_registry_envelope_is_required(api: Api, field: str, value: object) -> None:
    registry = envelope()
    registry[field] = value
    api.document = contents(json.dumps(registry).encode())
    with pytest.raises(cli.ProviderFailure, match="^approved_configuration_invalid$"):
        cli._load_config("setup_generated")


@pytest.mark.parametrize("visibility,adapter", [("public", "github-raw"), ("private", "relay")])
def test_supported_transport_configuration_is_preserved(api: Api, visibility: str, adapter: str) -> None:
    config = config_document()
    config["repository_visibility"] = visibility
    config["destination"] = (
        {"adapter": "github-raw", "branch": "architecture-health-badge", "endpoint_path": "architecture-health.json"}
        if adapter == "github-raw" else
        {"adapter": "relay", "alias": "a1234567", "endpoint": "https://relay.example.invalid", "audience": "synthetic"}
    )
    api.document = contents(json.dumps(envelope(config)).encode())
    api.repository["private"] = visibility == "private"
    loaded = cli._load_config("setup_generated")
    assert loaded.destination.adapter.value == adapter
    assert loaded.repository_visibility == visibility
    assert api.calls == [*FILE_CALLS, REPO_URL]
