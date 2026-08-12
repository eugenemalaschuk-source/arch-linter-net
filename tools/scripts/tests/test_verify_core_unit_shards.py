from __future__ import annotations

import subprocess
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import verify_core_unit_shards as vcus  # noqa: E402
from verify_core_unit_shards import (  # noqa: E402
    discover_fully_qualified_tests,
    evaluate_shard_membership,
    extract_tokens,
    main,
    validate_dll_path,
)

E2E_TOKENS = ["SomeE2eTests"]
PACKED_TOKENS = ["CheckpointBReleaseGateTests"]


def test_extract_tokens_parses_pipe_separated_fixtures() -> None:
    text = (
        "TEST_E2E_FIXTURES := FullyQualifiedName~FooTests|FullyQualifiedName~BarTests\n"
        "TEST_UNIT_FILTER := FullyQualifiedName!~FooTests&FullyQualifiedName!~BarTests\n"
    )

    tokens = extract_tokens(text, "TEST_E2E_FIXTURES")

    assert tokens == ["FooTests", "BarTests"]


def test_extract_tokens_missing_variable_raises() -> None:
    try:
        extract_tokens("SOME_OTHER_VAR := 1\n", "TEST_E2E_FIXTURES")
    except ValueError as exc:
        assert "TEST_E2E_FIXTURES" in str(exc)
    else:
        raise AssertionError("expected ValueError for a missing variable")


def test_happy_path_partitions_every_discovered_test_exactly_once() -> None:
    all_fqns = [
        "ArchLinterNet.Core.Tests.SomeE2eTests.DoesThing",
        "ArchLinterNet.Core.Tests.CheckpointBReleaseGateTests.InstallsPackedTool",
        "ArchLinterNet.Core.Tests.HeavyFixtureTests.SlowCase",
        "ArchLinterNet.Core.Tests.LightFixtureTests.FastCase",
    ]
    shard1_tokens = ["HeavyFixtureTests"]

    errors, summary = evaluate_shard_membership(all_fqns, E2E_TOKENS, PACKED_TOKENS, shard1_tokens)

    assert errors == []
    assert summary == {
        "discovered": 4,
        "e2e": 1,
        "packed_artifact": 1,
        "unit": 2,
        "shard1": 1,
        "shard2": 1,
    }


def test_new_unit_test_with_no_matching_token_lands_in_shard2_remainder() -> None:
    """A newly added fixture that nobody explicitly assigns to shard 1 must still be covered -
    the fail-closed remainder design the shard partition depends on."""
    all_fqns = [
        "ArchLinterNet.Core.Tests.HeavyFixtureTests.SlowCase",
        "ArchLinterNet.Core.Tests.BrandNewFixtureTests.NewCase",
    ]

    errors, summary = evaluate_shard_membership(all_fqns, E2E_TOKENS, PACKED_TOKENS, ["HeavyFixtureTests"])

    assert errors == []
    assert summary["unit"] == 2
    assert summary["shard1"] == 1
    assert summary["shard2"] == 1


def test_dead_shard_token_is_reported() -> None:
    all_fqns = ["ArchLinterNet.Core.Tests.LightFixtureTests.FastCase"]

    errors, _ = evaluate_shard_membership(all_fqns, E2E_TOKENS, PACKED_TOKENS, ["RenamedFixtureTests"])

    assert len(errors) == 1
    assert "RenamedFixtureTests" in errors[0]
    assert "dead" in errors[0] or "zero discovered" in errors[0]


def test_shard1_token_colliding_with_e2e_fixture_is_reported_as_a_leak() -> None:
    all_fqns = ["ArchLinterNet.Core.Tests.SomeE2eTests.DoesThing"]

    errors, _ = evaluate_shard_membership(all_fqns, E2E_TOKENS, PACKED_TOKENS, ["SomeE2eTests"])

    assert len(errors) == 1
    assert "SomeE2eTests" in errors[0]
    assert "E2E" in errors[0]


def test_shard1_token_colliding_with_packed_artifact_fixture_is_reported_as_a_leak() -> None:
    all_fqns = ["ArchLinterNet.Core.Tests.CheckpointBReleaseGateTests.InstallsPackedTool"]

    errors, _ = evaluate_shard_membership(all_fqns, E2E_TOKENS, PACKED_TOKENS, ["CheckpointBReleaseGateTests"])

    assert len(errors) == 1
    assert "packed-artifact" in errors[0]


def test_validate_dll_path_accepts_a_plain_dll_path(tmp_path: Path) -> None:
    dll = tmp_path / "ArchLinterNet.Core.Tests.dll"

    resolved = validate_dll_path(dll)

    assert resolved == dll.resolve()


@pytest.mark.parametrize(
    "suspicious",
    [
        "evil;rm -rf.dll",
        "evil`whoami`.dll",
        "evil$(whoami).dll",
        "not-a-dll.exe",
    ],
)
def test_validate_dll_path_rejects_shell_metacharacters_and_wrong_extension(
    tmp_path: Path, suspicious: str
) -> None:
    with pytest.raises(ValueError, match="Refusing to run dotnet vstest"):
        validate_dll_path(tmp_path / suspicious)


def test_discover_fully_qualified_tests_raises_when_dll_missing(tmp_path: Path) -> None:
    missing = tmp_path / "ArchLinterNet.Core.Tests.dll"

    with pytest.raises(FileNotFoundError, match="Build ArchLinterNet.Core.Tests"):
        discover_fully_qualified_tests(missing)


def test_discover_fully_qualified_tests_parses_the_target_file_on_success(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    dll = tmp_path / "ArchLinterNet.Core.Tests.dll"
    dll.write_text("fake assembly bytes")

    def fake_run(cmd, capture_output, text):  # noqa: ARG001 - signature must match subprocess.run
        target_arg = next(part for part in cmd if part.startswith("--ListTestsTargetPath:"))
        target_path = Path(target_arg.split(":", 1)[1])
        target_path.write_text("ArchLinterNet.Core.Tests.FooTests.Bar\nArchLinterNet.Core.Tests.FooTests.Baz\n")
        return subprocess.CompletedProcess(cmd, returncode=0, stdout="", stderr="")

    monkeypatch.setattr(vcus.subprocess, "run", fake_run)

    fqns = discover_fully_qualified_tests(dll)

    assert fqns == ["ArchLinterNet.Core.Tests.FooTests.Bar", "ArchLinterNet.Core.Tests.FooTests.Baz"]


def test_discover_fully_qualified_tests_raises_on_nonzero_exit(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    dll = tmp_path / "ArchLinterNet.Core.Tests.dll"
    dll.write_text("fake assembly bytes")

    def fake_run(cmd, capture_output, text):  # noqa: ARG001
        return subprocess.CompletedProcess(cmd, returncode=1, stdout="", stderr="boom")

    monkeypatch.setattr(vcus.subprocess, "run", fake_run)

    with pytest.raises(RuntimeError, match="dotnet vstest --ListFullyQualifiedTests failed"):
        discover_fully_qualified_tests(dll)


def _write_test_mk(path: Path, shard1_fixtures: str) -> None:
    path.write_text(
        "TEST_E2E_FIXTURES := FullyQualifiedName~SomeE2eTests\n"
        "TEST_PACKED_ARTIFACT_FILTER := FullyQualifiedName~CheckpointBReleaseGateTests\n"
        f"TEST_CORE_UNIT_SHARD_1_FIXTURES := {shard1_fixtures}\n"
    )


def test_main_reports_success_and_returns_zero(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]
) -> None:
    test_mk = tmp_path / "make" / "test.mk"
    test_mk.parent.mkdir()
    _write_test_mk(test_mk, "FullyQualifiedName~HeavyFixtureTests")
    monkeypatch.setattr(
        vcus,
        "discover_fully_qualified_tests",
        lambda dll: [  # noqa: ARG005
            "ArchLinterNet.Core.Tests.SomeE2eTests.DoesThing",
            "ArchLinterNet.Core.Tests.HeavyFixtureTests.SlowCase",
            "ArchLinterNet.Core.Tests.LightFixtureTests.FastCase",
        ],
    )
    monkeypatch.setattr(sys, "argv", ["verify_core_unit_shards.py", "--test-mk", str(test_mk)])

    exit_code = main()

    assert exit_code == 0
    assert "Core unit shard membership check passed." in capsys.readouterr().out


def test_main_reports_failure_and_returns_one_on_shard_errors(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]
) -> None:
    test_mk = tmp_path / "make" / "test.mk"
    test_mk.parent.mkdir()
    _write_test_mk(test_mk, "FullyQualifiedName~NeverDiscoveredTests")
    monkeypatch.setattr(
        vcus,
        "discover_fully_qualified_tests",
        lambda dll: ["ArchLinterNet.Core.Tests.LightFixtureTests.FastCase"],  # noqa: ARG005
    )
    monkeypatch.setattr(sys, "argv", ["verify_core_unit_shards.py", "--test-mk", str(test_mk)])

    exit_code = main()

    captured = capsys.readouterr().out
    assert exit_code == 1
    assert "Core unit shard membership check failed" in captured
    assert "NeverDiscoveredTests" in captured


def test_main_reports_missing_variable_and_returns_one(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]
) -> None:
    test_mk = tmp_path / "make" / "test.mk"
    test_mk.parent.mkdir()
    test_mk.write_text("SOME_OTHER_VAR := 1\n")
    monkeypatch.setattr(sys, "argv", ["verify_core_unit_shards.py", "--test-mk", str(test_mk)])

    exit_code = main()

    assert exit_code == 1
    assert "TEST_E2E_FIXTURES" in capsys.readouterr().out


def test_main_reports_discovery_failure_and_returns_one(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]
) -> None:
    test_mk = tmp_path / "make" / "test.mk"
    test_mk.parent.mkdir()
    _write_test_mk(test_mk, "FullyQualifiedName~HeavyFixtureTests")

    def raise_not_found(dll):  # noqa: ARG001
        raise FileNotFoundError("Core.Tests assembly not found")

    monkeypatch.setattr(vcus, "discover_fully_qualified_tests", raise_not_found)
    monkeypatch.setattr(sys, "argv", ["verify_core_unit_shards.py", "--test-mk", str(test_mk)])

    exit_code = main()

    assert exit_code == 1
    assert "Core.Tests assembly not found" in capsys.readouterr().out


def test_main_accepts_an_explicit_dll_override(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]
) -> None:
    test_mk = tmp_path / "make" / "test.mk"
    test_mk.parent.mkdir()
    _write_test_mk(test_mk, "FullyQualifiedName~HeavyFixtureTests")
    explicit_dll = tmp_path / "custom" / "ArchLinterNet.Core.Tests.dll"
    seen_dll_paths: list[Path] = []

    def fake_discover(dll: Path) -> list[str]:
        seen_dll_paths.append(dll)
        return ["ArchLinterNet.Core.Tests.HeavyFixtureTests.SlowCase"]

    monkeypatch.setattr(vcus, "discover_fully_qualified_tests", fake_discover)
    monkeypatch.setattr(
        sys, "argv", ["verify_core_unit_shards.py", "--test-mk", str(test_mk), "--dll", str(explicit_dll)]
    )

    exit_code = main()

    assert exit_code == 0
    assert seen_dll_paths == [explicit_dll]
