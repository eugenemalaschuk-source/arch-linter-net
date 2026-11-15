# Proposal

## Why

The existing benchmark catalog declares independent scale dimensions, but several generated workloads currently perform different work than their labels claim. This makes retained evidence misleading and weakens the benchmark foundation intended for downstream optimization work.

## What Changes

- Execute metric measurement for metric-rule workloads and retain the measured metric count and deterministic result identity in benchmark evidence.
- Keep ordinary contract counts independent from method-body rule counts while retaining a correct total inventory.
- Hold SARIF finding volume constant while report-rule count scales, with every declared rule referenced.
- Generate ancestry inputs with genuinely diverging and rejoining branches, real rename events, and graph-verified query answers.
- Verify that independent materializations and builds of the same workload produce the same canonical input identity while retaining staged analysis inputs.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `large-solution-benchmarking`: require scale dimensions and retained evidence to represent the work actually executed, including deterministic metric results, report output shape, History graph inputs, and reproducible materialized-input identity.

## Impact

Changes are limited to the internal benchmark harness, its evidence schema and retained #1123 evidence, deterministic NUnit tests, and the existing OpenSpec capability. No product API, runtime behavior, `analysis-profile/v1` schema, or external dependency changes.
