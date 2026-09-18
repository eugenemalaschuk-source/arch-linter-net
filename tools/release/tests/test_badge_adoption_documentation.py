"""Documentation drift checks, not packed or live Relay acceptance."""

import json
import re
from pathlib import Path
from urllib.parse import unquote, urlsplit

import pytest

_ROOT = Path(__file__).resolve().parents[3]
_DOCS = _ROOT / "docs"
_PUBLIC = (
    _DOCS / "guides/badge-adoption.md",
    _DOCS / "guides/badge-setup.md",
    _DOCS / "guides/badge-lifecycle-operations.md",
)
_HANDOFF = _DOCS / "internal/badge-delivery-handoff.md"
_REFERENCE = _DOCS / "reference/badge-distribution.md"


def _read(path: Path) -> str:
    return path.read_text(encoding="utf-8")


@pytest.mark.parametrize(
    "path", (*_PUBLIC, _REFERENCE, _HANDOFF), ids=lambda p: p.stem
)
def test_badge_documentation_relative_links_resolve(path: Path) -> None:
    links = re.findall(r"\[[^\]]+\]\(([^\s)]+)\)", _read(path))
    assert links, f"Expected documentation cross-links in {path}"
    for link in links:
        parsed = urlsplit(link)
        if parsed.scheme or parsed.netloc or not parsed.path:
            continue
        target = (path.parent / unquote(parsed.path)).resolve()
        assert target.is_relative_to(_DOCS), link
        assert target.is_file(), f"{path}: missing link target {link}"
        if path in (*_PUBLIC, _REFERENCE):
            assert not target.is_relative_to(_DOCS / "internal"), link


def test_adoption_precedes_executable_guides_in_navigation() -> None:
    config = _read(_ROOT / "mkdocs.yml")
    nav = config.split("\nnav:\n", 1)[1].split("\nexclude_docs:", 1)[0]
    targets = [path.relative_to(_DOCS).as_posix() for path in _PUBLIC]
    assert all(nav.count(target) == 1 for target in targets)
    assert [nav.index(target) for target in targets] == sorted(
        nav.index(target) for target in targets
    )
    assert "internal/" not in nav
    assert nav.count(_REFERENCE.relative_to(_DOCS).as_posix()) == 1
    assert re.search(r"(?m)^exclude_docs: \|\n {2}internal/\s*$", config)


def test_public_guides_expose_candidate_boundary_and_single_entrypoint() -> None:
    for path in _PUBLIC:
        intro = _read(path).split("\n## ", 1)[0]
        assert "Prepublication candidate guidance" in intro
        assert "stable-release availability" in intro
    for path in _PUBLIC[1:]:
        assert "[badge adoption](badge-adoption.md)" in _read(path)
    # Executable examples stay with their existing setup/lifecycle owners.
    assert not re.search(r"(?m)^arch-linter-net ", _read(_PUBLIC[0]))


@pytest.mark.parametrize("mode", ("none", "github-raw", "relay"))
def test_adoption_and_setup_cover_the_same_transports(mode: str) -> None:
    for path in _PUBLIC[:2]:
        assert f"| `{mode}` |" in _read(path)


@pytest.mark.parametrize("profile", ("headline-only/v1", "headline-plus-freshness/v1"))
def test_adoption_and_setup_cover_both_disclosure_profiles(profile: str) -> None:
    for path in _PUBLIC[:2]:
        assert f"`{profile}`" in _read(path)


def test_handoff_inventory_tracks_machine_readable_authority() -> None:
    inventory = json.loads(
        _read(_ROOT / ".github/badge-promotion/release-inventory.json")
    )
    handoff = _read(_HANDOFF)
    compatibility = inventory["compatibility"]
    values = [
        inventory["schema"],
        inventory["release_authority"],
        *inventory["package_ids"],
        *inventory["excluded"],
        *inventory["relay"].values(),
        *(
            compatibility[key]
            for key in (
                "bundle", "config", "plan", "promotion", "publication", "storage",
                "publisher_commit", "action_commit", "workflow_path", "action_path",
                "workflow_source_sha", "action_source_sha",
            )
        ),
    ]
    for value in values:
        assert value in handoff, f"Refresh the source-audited handoff for {value}"


@pytest.mark.parametrize(
    ("event", "operation"),
    (
        ("Temporary suspension / expiry", "invalidate"),
        ("Disclosure withdrawal", "revoke"),
        ("Remove/uninstall", "remove"),
    ),
)
def test_lifecycle_matrix_distinguishes_temporary_and_terminal_operations(
    event: str, operation: str
) -> None:
    rows = [
        line.split("|")[1:-1]
        for line in _read(_PUBLIC[2]).splitlines()
        if line.startswith("|") and line.split("|")[1].strip() == event
    ]
    assert len(rows) == 1, f"Expected one lifecycle row for {event}"
    assert rows[0][1].strip() == f"`--operation {operation}`"


def test_adoption_separates_pause_withdrawal_and_uninstall() -> None:
    text = " ".join(_read(_PUBLIC[0]).split())
    assert "`invalidate` for a temporary suspension" in text
    assert "`revoke` to permanently withdraw disclosure consent" in text
    assert "`remove` to uninstall" in text
    assert "Invalidation is not a substitute for consent withdrawal" in text


def test_terminal_alias_requires_new_consent_before_fresh_setup() -> None:
    text = " ".join(_read(_PUBLIC[2]).split())
    assert "A tombstoned alias cannot be recovered" in text
    assert "new alias and explicit consent" in text


def test_synthetic_acceptance_uses_fresh_aliases_for_terminal_scenarios() -> None:
    section = _read(_PUBLIC[2]).split("## Synthetic acceptance checks", 1)[1]
    text = " ".join(section.split())
    assert "fresh disposable alias for each independent scenario" in text
    assert "Do not execute every matrix row sequentially against one alias" in text
    assert "separate non-tombstoned alias" in text


def test_distribution_reference_tracks_compatible_package_and_protocol_identities() -> None:
    inventory = json.loads(
        _read(_ROOT / ".github/badge-promotion/release-inventory.json")
    )
    reference = _read(_REFERENCE)
    identities = [
        *inventory["package_ids"],
        *(inventory["compatibility"][key] for key in (
            "bundle", "config", "plan", "promotion", "publication", "storage",
        )),
    ]
    for identity in identities:
        assert f"`{identity}`" in reference, f"Refresh distribution reference for {identity}"
    assert "not a released capability" in reference
    assert "../reference/badge-distribution.md" in _read(_PUBLIC[0])
