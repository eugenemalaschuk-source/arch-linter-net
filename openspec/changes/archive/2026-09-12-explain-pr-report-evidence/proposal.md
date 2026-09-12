## Why

The architecture PR comment can report a passing Gate with `DEBT` or `DEGRADING` Health without
showing the reason that determined that non-healthy result. Its bounded navigation list can also
hide the canonical report/artifact location behind ordinary finding-detail truncation. This makes
the post-v0.8 stabilization evidence difficult to audit even though the canonical Health document
already carries the authority facts.

## What Changes

- Add a deterministic Core-owned explanation summary for every non-healthy Health dimension,
  preserving the distinction between blocking and advisory debt.
- Render complete waiver lifecycle totals and a bounded, informative non-blocking detail section
  without treating `metadata_incomplete` advisory debt as a Gate blocker.
- Carry a safe, explicit report-artifact navigation reference from the CI producer to the CLI
  report projection so report/bundle navigation is not constrained by `--max-details`.
- Add public-safe regression fixtures and Core/CLI/transport tests for the #799 Health shape,
  detail boundaries, hostile content, and stale or missing report navigation context.
- Update the PR-report documentation to explain evidence availability, bounded comments, and the
  immutable full-report drill-down.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `architecture-pr-reporting`: the reviewer Markdown must explain every non-healthy canonical
  dimension and preserve non-blocking lifecycle evidence and complete navigation under bounds.
- `architecture-pr-report-publication`: producer-bound report navigation must make the exact
  immutable artifact bundle discoverable without granting semantic authority to the publisher.

## Impact

- Core PR-report models, reader/projector, Health report-evidence formatter, and their tests.
- CLI PR Markdown formatter/renderer, CLI tests, and report command integration fixtures.
- The read-only report-producer portion of `.github/workflows/ci.yml`; the privileged publisher
  remains a byte-only transport boundary.
- PR report specs and reviewer-facing output documentation.
