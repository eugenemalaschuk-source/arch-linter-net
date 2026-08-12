from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from verify_core_unit_shards import evaluate_shard_membership, extract_tokens  # noqa: E402

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
