## Why

A further round of PR #416 review found that the prior `fix-cooperative-cancellation-gaps` change left
several boundaries checked only coarsely, or not at all: a bounded child-process kill wait whose timeout
result was discarded, `ReportCoordinator`'s own Format* methods rendering large findings sets with no token,
`LoadDocument`'s recursive policy import graph and the deep `ArchitectureTypeIndex`/`ArchitectureRoleIndex`
scans running with no cancellation check, `BuildStateCanonicalHasher` accepting no token at all, the CLI's
real Ctrl+C path routing its own cancellation notice through an already-cancelled token (so the notice itself
never published), and every baseline/public-API command handler folding `OperationCanceledException` into a
generic `<command> error` with no re-check between Core returning and the handler's own write.

The canonical spec also self-contradicted: it named baseline/public-API publication safety as a requirement
while a separate paragraph claimed those surfaces "do NOT currently accept a CancellationToken" — true when
first written, no longer true once this change's fixes land — and profile-generation/artifact-cleanup was
listed as in-scope even though that capability does not exist yet (it depends on unimplemented issue #374).

## What Changes

- Bound `BuildStatePreparationService`'s post-kill wait with a typed cleanup-timeout exception instead of
  discarding the wait result.
- Thread `CancellationToken` through `ReportCoordinator`'s own rendering methods, checked between each human
  report section and between each mode of a combined report.
- Thread `CancellationToken` through `IArchitecturePolicyDocumentLoader`/`ArchitecturePolicyImportGraphResolver`
  so a deep import graph is checked per document, not only before/after the whole load.
- Thread `CancellationToken` through `ArchitectureTypeIndex`/`ArchitectureRoleIndex` and
  `BuildStateCanonicalHasher`, checked per assembly/type/file.
- Fix `ValidateCommandHandler.WriteCancellation` to route its cancellation notice with a fresh token so an
  already-cancelled handler token cannot block delivery of the notice itself.
- Add a dedicated `OperationCanceledException` branch and a pre-publish token re-check to every baseline
  (`update`/`generate`/`prune`/`migrate`/`diff`/`verify`) and public-API (`update`/`capture`/`migrate`/`diff`)
  command handler.
- Reconcile the canonical spec: baseline/public-API are now accurately described as covered; profile
  generation/artifact-cleanup is described as not-yet-implemented (tracked by issue #418), not as an
  unfulfilled promise.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `cooperative-cancellation`: Complete the remaining publication-safety and iteration-boundary gaps found
  in PR #416 review, and correct the spec's self-contradictory scope statement.

## Impact

`ReportCoordinator`, `BuildStatePreparationService`, `BuildStateCanonicalHasher`,
`ArchitecturePolicyDocumentLoader`/`ArchitecturePolicyImportGraphResolver`, `ArchitectureTypeIndex`,
`ArchitectureRoleIndex`, `ValidateCommandHandler`, every baseline and public-API CLI command handler, NUnit
regression tests, and the `cooperative-cancellation` specification.
