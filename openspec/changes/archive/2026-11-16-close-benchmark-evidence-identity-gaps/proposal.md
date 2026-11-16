# Proposal

## Why

The revised #1123 evidence still has two provenance/coverage gaps: every generated History ancestry query is positive, and the audit digest omits the compiled assemblies consumed by Core. The History summary also repeats a catalog value while describing it as retained work, although no algorithm-retained state is measured.

## What Changes

- Add both positive and negative ancestry queries with an explicit expected answer per query, and verify the answer independently from the parent graph.
- Report strict-ancestor closure cardinality from the generated graph, without presenting it as measured runtime retention.
- Preserve the materialized source/configuration digest and separately retain identities for the exact DLL bytes loaded by the analysis runner.
- Refresh the affected deterministic workload inputs and benchmark evidence.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `large-solution-benchmarking`: require ancestry workloads to cover both query outcomes and require audit evidence to identify loaded assembly bytes separately from source inputs.

## Impact

The changes affect the test-only History workload generator and independent checks, `benchmark-evidence/v1` optional evidence fields and schema, the explicit #1123 evidence matrix, and the large-solution-benchmarking spec. No product behavior or public API changes.
