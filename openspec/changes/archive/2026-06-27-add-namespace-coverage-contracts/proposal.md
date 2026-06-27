## Why

Issue #98 is the first executable coverage contract family on top of the shared architecture coverage model and inventory. Today the runtime still rejects every declared `strict_coverage` / `audit_coverage` contract even though the reviewed schema and inventory already exist. That leaves the most important blind-spot check unimplemented: first-party namespaces under configured roots can still appear outside any declared layer, glob layer, template expansion, or explicit exclusion without producing a signal.

## What Changes

- Implement `scope: namespace` coverage contracts for both `strict_coverage` and `audit_coverage`.
- Reuse the existing coverage inventory, layer matcher, namespace glob matcher, and expanded layer-template facts to classify first-party namespaces under configured `roots`.
- Report uncovered namespaces with deterministic ordering, representative type evidence, and contract name/ID context.
- Honor `analysis.coverage` severity (`error` / `warn` / `off`) without changing behavior for policies that declare no coverage contracts.
- Keep `scope: project`, `scope: assembly`, `scope: dependency_edge`, and `scope: rule_input` reserved and explicitly rejected for now so unsupported scopes are never silently accepted.

## Capabilities

### New Capabilities
- `namespace-coverage-contracts`: Enforce namespace-scope architecture coverage contracts against first-party namespaces discovered in the shared coverage inventory.

### Modified Capabilities
- `architecture-coverage-model`: The reviewed coverage shape is no longer design-only for `scope: namespace`; namespace coverage becomes executable while the other scopes remain reserved for follow-up issues.

## Impact

- `src/ArchLinterNet.Core/Contracts/` gains full binding for namespace coverage roots/exclusions.
- `src/ArchLinterNet.Core/Execution/` gains namespace coverage execution and coverage-family wiring.
- `src/ArchLinterNet.Core/Validation/` and CLI/reporting paths gain coverage severity handling so warnings can be surfaced without failing validation.
- `tests/ArchLinterNet.Core.Tests/` gains namespace coverage behavior tests and replaces the old blanket "coverage is reserved" assertions with scope-specific expectations.
