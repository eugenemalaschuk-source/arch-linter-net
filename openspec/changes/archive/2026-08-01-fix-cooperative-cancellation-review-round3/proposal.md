## Why

A third round of PR #416 review, against commit `5828521`, confirmed 2 of the round-2 fixes but found 8
remaining gaps: single-format rendering (`FormatViolationsForHumans`/`FormatResultForCiArtifacts`/
`FormatResultAsSarif`) still rendered a large findings set as one uninterruptible call; the CLI's generic
`catch (OperationCanceledException)` discarded the process ID/deadline evidence carried by
`BuildStateProcessCleanupTimedOutException`; the cancellation notice still routed through file sinks
(`allowFileSinks: true`), risking overwriting an existing legitimate report from an earlier run;
`ArchitecturePolicyImportGraphResolver` only checked the token inside `Visit()`, so a sibling import already
got resolved/read/parsed before that check ever ran; `ArchitectureIlMethodBodyScanner` and
`ArchitectureExternalDependencyIlScanner` still walked types/methods/IL instructions with no token at all;
`BaselineWriteGate` and the public-API handlers' own `WriteAllTextToTemp` → `RenameTempToTarget` step had no
check or cleanup between staging and commit; and the canonical spec, once fixed for the profile-scope
contradiction, still claimed guarantees (full import traversal, full IL scanning, no publication races) that
the implementation did not yet meet.

## What Changes

- Add cancellation-aware overloads of `ICliRuntime.FormatViolationsForHumans`/`FormatResultForCiArtifacts`/
  `FormatResultAsSarif` (default-interface-method delegates, so every existing test fake keeps compiling),
  overridden in `CliRuntime` to call new per-finding cancellation-aware overloads of
  `ArchitectureDiagnosticFormatter`/`ArchitectureSarifFormatter`, checked per violation — the dominant
  contributor to a large report's size — not just before/after the whole call.
- Add a dedicated `catch (BuildStateProcessCleanupTimedOutException)` branch in `ValidateCommandHandler`,
  ahead of the general `OperationCanceledException` catch, surfacing the process ID and timeout in every
  output format instead of discarding it behind a generic "cancelled" message.
- Change `WriteCancellation` to `allowFileSinks: false`, routing the cancellation notice through the safe
  stream fallback instead of a configured `--report` file sink, so an existing legitimate report is never
  overwritten.
- Check cancellation in `ArchitecturePolicyImportGraphResolver`'s import loop before each sibling
  `VisitImport` call, not only inside `Visit()` once a document is entered.
- Thread `CancellationToken` through `ArchitectureIlMethodBodyScanner`/`ArchitectureExternalDependencyIlScanner`,
  checked per source type, and through `Context.CancellationToken` from their `ArchitectureAnalysisSession`
  call sites.
- Thread `CancellationToken` through `BaselineWriteGate.TryApply`/`TryCopySource` and a new shared
  `PublicApiTwoPhaseWriter.WriteAndCommit`, checked immediately before the rename that commits a write, with
  the staged temp file deleted on cancellation.
- Reconcile the canonical spec's scenarios for import traversal, deep scanning, rendering, publication races,
  and cleanup-timeout evidence to match the implementation above.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `cooperative-cancellation`: Close the remaining publication-safety, iteration-boundary, and evidence-loss
  gaps found in PR #416's third review round, and make the spec's scenarios match the implementation.

## Impact

`ICliRuntime`, `CliRuntime`, `ArchitectureDiagnosticFormatter`, `ArchitectureSarifFormatter`,
`ValidateCommandHandler`, `ArchitecturePolicyImportGraphResolver`, `ArchitectureIlMethodBodyScanner`,
`ArchitectureExternalDependencyIlScanner`, `ArchitectureAnalysisSession.Checking`, `BaselineWriteGate`, every
public-API command handler, NUnit regression tests, and the `cooperative-cancellation` specification.
