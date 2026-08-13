## 1. Fixture

- [x] 1.1 Add `tests/ArchLinterNet.Core.Tests/AdoptionAcceptance/Fixtures/api-surface-selector`
      (csproj, base `dependencies.arch.yml`, marker/role attribute types, a large incidental exported
      surface, an intentional `has_attribute`-selected surface, and an intentional
      `namespace`-selected surface).
- [x] 1.2 Author a `PublicApiContractAttribute` orthogonal marker that is never mapped in
      `classification:`, and a `ValueObjectRoleAttribute` that is mapped to role `ValueObject`.
- [x] 1.3 Author a selected type whose member references an unselected first-party exported type,
      for the fail-closed escape scenario.

## 2. Gate scenarios

- [x] 2.1 Add `CheckpointBReleaseGateTests.PublicApiSurfaceSelector.cs` with scenarios covering
      snapshot reduction, role continuity, the exact delta lifecycle, membership review-visibility,
      the fail-closed escape, a green full-policy strict run, and CLI/Testing parity.
- [x] 2.2 Wire the new scenarios into the main packed-artifact entrypoint alongside the existing
      consumer-cleanup matrix.

## 3. Release evidence

- [x] 3.1 Register the new scenario IDs in
      `tools/release/aggregate_checkpoint_b_evidence.py`'s `_CONSUMER_CLEANUP_SCENARIOS`.
- [x] 3.2 Advance `tools/release/release-scope.json` to the 0.6.4/#527 scope.
- [x] 3.3 Update `docs/internal/consumer-cleanup-gate.md` with the new scenario inventory rows.

## 4. Validation

- [x] 4.1 Run the focused `CheckpointBReleaseGateTests` packed-artifact suite.
- [x] 4.2 Run `make fmt` and inspect the diff.
- [x] 4.3 Run `openspec validate --all`.
- [x] 4.4 Run `make test-release-evidence` (aggregator pytest regressions).
