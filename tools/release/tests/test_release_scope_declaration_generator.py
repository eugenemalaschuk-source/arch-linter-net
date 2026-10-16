from __future__ import annotations

import json
import subprocess
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import generate_release_scope_declaration as generator  # noqa: E402
import _release_workspace as release_workspace  # noqa: E402

_STORY_NUMBER = 565
_FIXTURE_PATH = Path(__file__).parent / "fixtures" / "release-scope-story.md"
_FIXTURE_BODY = _FIXTURE_PATH.read_text(encoding="utf-8")


def _stub_story(monkeypatch, body: str, *, returned_number: int = _STORY_NUMBER) -> list[list[str]]:
    invocations: list[list[str]] = []

    def fake_run(argv, **kwargs):
        invocations.append(argv)
        assert kwargs["cwd"] == generator._repository_root()
        assert kwargs["shell"] is False
        return subprocess.CompletedProcess(
            argv,
            0,
            stdout=json.dumps({"number": returned_number, "body": body}),
            stderr="",
        )

    monkeypatch.setattr(generator.subprocess, "run", fake_run)
    return invocations


def _expected_declaration(story: int = _STORY_NUMBER) -> dict:
    return {
        "schema": "checkpoint-b-release-scope-declaration/v2",
        "declaration_id": "v0.10.0-preview.3-fixture-scope",
        "release_target": "0.10.0-preview.3",
        "story": story,
        "_comment": ["Test scope generation from a release story."],
        "required_items": [
            {"issue": 701, "finding": "fixture-required", "summary": "A required release item."}
        ],
        "excluded_items": [{"issue": 702, "reason": "An explicitly non-blocking item."}],
        "delivered_items": [],
    }


def test_story_block_generates_existing_declaration_schema() -> None:
    declaration = generator._parse_scope_block(_FIXTURE_BODY, _STORY_NUMBER)

    assert declaration == _expected_declaration()
    assert json.loads(generator._render_declaration(declaration)) == declaration


@pytest.mark.parametrize("newline", ["\n", "\r\n"], ids=["LF", "CRLF"])
def test_story_block_supports_lf_and_crlf(newline: str) -> None:
    body = _FIXTURE_BODY.replace("\n", newline)

    assert generator._parse_scope_block(body, _STORY_NUMBER) == _expected_declaration()


@pytest.mark.parametrize(
    ("body", "message"),
    [
        ("No structured scope is present.", "no fenced TOML"),
        (
            _FIXTURE_BODY + "\n" + _FIXTURE_BODY,
            "exactly one",
        ),
        (
            _FIXTURE_BODY.replace('schema = "release-scope-input/v1"', 'schema = "unknown/v1"'),
            "schema must be",
        ),
        (
            _FIXTURE_BODY.replace(
                'release_target = "0.10.0-preview.3"', 'release_target = "0.10.0-preview.4"'
            ),
            "declaration_id must start",
        ),
        (
            _FIXTURE_BODY.replace(
                'comments = ["Test scope generation from a release story."]',
                'comments = ["Test scope generation from a release story."]\nunknown = true',
            ),
            "unknown fields",
        ),
        (
            _FIXTURE_BODY.replace("issue = 702", "issue = 701"),
            "repeats an issue across inventories",
        ),
        (
            _FIXTURE_BODY.replace(
                "[[excluded_items]]",
                '[[required_items]]\nissue = 701\nfinding = "duplicate"\nsummary = "Duplicate issue."\n\n'
                "[[excluded_items]]",
            ),
            "required_items contains duplicate issue #701",
        ),
        (
            _FIXTURE_BODY.replace("summary = \"A required release item.\"", "summary = 17"),
            "summary must be a non-empty string",
        ),
        (
            _FIXTURE_BODY.replace("issue = 701", "issue = false"),
            "positive integer",
        ),
        (
            _FIXTURE_BODY.replace('comments = ["Test scope generation from a release story."]', "comments = []"),
            "comments must be a non-empty",
        ),
        (
            _FIXTURE_BODY.replace('declaration_id = "v0.10.0-preview.3-fixture-scope"', "declaration_id = \"bad id\""),
            "declaration_id must contain",
        ),
        (
            _FIXTURE_BODY.replace('summary = "A required release item."', 'summary = "A required release item.'),
            "TOML block is invalid",
        ),
    ],
)
def test_invalid_scope_block_is_rejected(body: str, message: str) -> None:
    with pytest.raises(ValueError, match=message):
        generator._parse_scope_block(body, _STORY_NUMBER)


def test_empty_optional_inventories_are_supported() -> None:
    body = _FIXTURE_BODY.replace(
        "delivered_items = []",
        "delivered_items = []\nexcluded_items = []",
    ).replace(
        '[[excluded_items]]\nissue = 702\nreason = "An explicitly non-blocking item."',
        "",
    )

    declaration = generator._parse_scope_block(body, _STORY_NUMBER)

    assert declaration["excluded_items"] == []
    assert declaration["delivered_items"] == []


def test_fetch_story_uses_fixed_repository_despite_redirected_gh_default(monkeypatch) -> None:
    monkeypatch.setenv("GH_REPO", "attacker/other-repo")
    invocations = _stub_story(monkeypatch, _FIXTURE_BODY)

    story = generator._fetch_story(_STORY_NUMBER)

    assert story == {"number": _STORY_NUMBER, "body": _FIXTURE_BODY}
    assert invocations == [
        [
            "gh",
            "issue",
            "view",
            str(_STORY_NUMBER),
            "--repo",
            "eugenemalaschuk-source/arch-linter-net",
            "--json",
            "number,body",
        ]
    ]


def test_fetch_story_rejects_a_different_issue_identity(monkeypatch) -> None:
    _stub_story(monkeypatch, _FIXTURE_BODY, returned_number=_STORY_NUMBER + 1)

    with pytest.raises(ValueError, match="invalid issue identity or body"):
        generator._fetch_story(_STORY_NUMBER)


def test_fixed_output_path_stays_under_release_scopes() -> None:
    output = generator._output_path("0.10.0-preview.3")

    assert output == generator._declarations_directory() / "0.10.0-preview.3.json"
    assert output.is_relative_to(generator._repository_root())


def test_output_path_rejects_a_directory_outside_the_repository(monkeypatch, tmp_path: Path) -> None:
    outside_directory = tmp_path / "scopes"
    outside_directory.mkdir()
    monkeypatch.setattr(
        release_workspace,
        "_allowed_roots",
        lambda: (generator._repository_root(),),
    )
    monkeypatch.setattr(generator, "_declarations_directory", lambda: outside_directory)

    with pytest.raises(ValueError, match="outside the release workspace"):
        generator._output_path("0.10.0-preview.3")


def test_preview_prints_exact_declaration_without_writing(tmp_path: Path, monkeypatch, capsys) -> None:
    declarations = tmp_path / "scopes"
    declarations.mkdir()
    monkeypatch.setattr(generator, "_repository_root", lambda: tmp_path)
    monkeypatch.setattr(generator, "_fetch_story", lambda _: {"number": _STORY_NUMBER, "body": _FIXTURE_BODY})
    monkeypatch.setattr(generator, "_output_path", lambda target: declarations / f"{target}.json")

    result = generator.main(["preview", "--story", str(_STORY_NUMBER)])

    output = capsys.readouterr().out.encode("utf-8")
    assert result == 0
    assert output == generator._render_declaration(_expected_declaration())
    assert list(declarations.iterdir()) == []


def test_generate_is_atomic_and_idempotent(tmp_path: Path, monkeypatch, capsys) -> None:
    root = tmp_path / "repo"
    declarations = root / "tools" / "release" / "scopes"
    declarations.mkdir(parents=True)
    monkeypatch.setattr(generator, "_repository_root", lambda: root)
    monkeypatch.setattr(generator, "_fetch_story", lambda _: {"number": _STORY_NUMBER, "body": _FIXTURE_BODY})
    monkeypatch.setattr(generator, "_output_path", lambda target: declarations / f"{target}.json")

    assert generator.main(["generate", "--story", str(_STORY_NUMBER)]) == 0
    first_output = capsys.readouterr().out
    declaration_path = declarations / "0.10.0-preview.3.json"
    expected_bytes = generator._render_declaration(_expected_declaration())
    assert "created" in first_output
    assert declaration_path.read_bytes() == expected_bytes
    assert sorted(path.name for path in declarations.iterdir()) == [declaration_path.name]

    assert generator.main(["generate", "--story", str(_STORY_NUMBER)]) == 0
    second_output = capsys.readouterr().out
    assert "already matches" in second_output
    assert declaration_path.read_bytes() == expected_bytes


def test_generate_preserves_conflicting_existing_declaration(tmp_path: Path, monkeypatch, capsys) -> None:
    root = tmp_path / "repo"
    declarations = root / "tools" / "release" / "scopes"
    declarations.mkdir(parents=True)
    declaration_path = declarations / "0.10.0-preview.3.json"
    original = b"reviewed bytes\n"
    declaration_path.write_bytes(original)
    monkeypatch.setattr(generator, "_repository_root", lambda: root)
    monkeypatch.setattr(generator, "_fetch_story", lambda _: {"number": _STORY_NUMBER, "body": _FIXTURE_BODY})
    monkeypatch.setattr(generator, "_output_path", lambda target: declarations / f"{target}.json")

    result = generator.main(["generate", "--story", str(_STORY_NUMBER)])

    assert result == 2
    assert "refusing to replace reviewed authority" in capsys.readouterr().err
    assert declaration_path.read_bytes() == original
    assert [path.name for path in declarations.iterdir()] == [declaration_path.name]
