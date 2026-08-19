## Why

Validation tells a reviewer whether one revision satisfies an architecture policy, but it does
not identify how the architecture changed from the base revision. PR and branch workflows need a
deterministic, compact delta over complete analysis results so new drift can be reviewed without
mixing it with established baseline debt.

## What Changes

- Add an `architecture change report` Core model and comparison service for two persisted,
  complete validation-result snapshots.
- Add a `change report` CLI command that accepts a base JSON result and a current JSON result,
  writes deterministic human or JSON output, and never performs partial analysis.
- Report additions and removals for namespaces, projects, assemblies, semantic roles and
  contexts, dependency edges, coverage blind spots, and normalized findings.
- Separate new violations and new coverage debt from findings already present in the base result.
- Add user-facing CLI and workflow documentation plus focused Core and CLI tests.

## Capabilities

### New Capabilities

- `architecture-change-report`: deterministic comparison and CLI reporting of complete
  architecture validation snapshots for branch and PR workflows.

### Modified Capabilities

None.

## Impact

- Core reporting, validation-result JSON reading, and public API snapshots.
- A new modular CLI command and focused command integration tests.
- User-facing MkDocs CLI/workflow documentation.
- No policy-schema, validation, baseline-writing, or changed-project/file execution behavior.
