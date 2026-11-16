# Tasks

## 1. Correct History ancestry inputs and summaries

- [x] 1.1 Update the History catalog and generator to encode expected answers for positive and negative ancestry queries, derive strict-ancestor closure cardinality, and remove the non-materialized retained-entry input dimension; verify the catalog is read deterministically and its generator version advances.
- [x] 1.2 Independently traverse each generated ancestry graph in NUnit to validate every expected answer, both query outcomes, and derived closure counts; verify with `dotnet test tests/ArchLinterNet.Core.Tests --no-restore --filter FullyQualifiedName~BenchmarkHistoryWorkloadCatalogTests`.
- [x] 1.3 Update the benchmark methodology documentation to define the graph-derived closure count and query outcomes; verify markdown formatting with `make fmt`.

## 2. Record exact loaded DLL identities in audit evidence

- [x] 2.1 Add optional per-sample loaded-DLL inventory and aggregate SHA-256 fields to the evidence model/schema, populated from exact loader-captured PE digests while retaining the source/configuration digest; verify model validation and schema round-trip tests.
- [x] 2.2 Assert loaded DLL inventories and digests are present and stable across paired audit samples, and test that path roots do not affect the inventory identity; verify with focused `BenchmarkAuditExecutionTests`, `BenchmarkAuditEvidenceTests`, and `BenchmarkMaterializedInputIdentityTests`.

## 3. Refresh evidence and synchronize the capability spec

- [x] 3.1 Regenerate the explicit #1123 repeated baseline matrix for source revision `4a5c95c8f750` and verify all 33 evidence documents pass the benchmark evidence schema and record loaded DLL identities.
- [x] 3.2 Run `make fmt`, the focused benchmark tests, and `openspec validate --all`; inspect the final diff and confirm the delta spec matches the implementation.

## Workflow follow-up

- Archive this change with `openspec archive close-benchmark-evidence-identity-gaps` after implementation and spec synchronization, then rerun `openspec validate --all`.
