## Why

A fourth round of PR #416 review, against commit `5671254`, confirmed 5 of the round-3 fixes but found 3
remaining gaps: `ArchitectureFindingMapper.FromViolations` — the mapping step every rendering path
(`FormatViolationsForHumans`, `FormatResultForCiArtifacts`, `FormatResultAsSarif`) runs before it ever
serializes a single item — built its entire `List<ArchitectureFinding>` with no cancellation check at all,
so "per-finding" cancellation only ever took effect after the full mapping pass had already finished; type
discovery (`ArchitectureTypeScanner.FindTypesInLayer`/`FindTypesInNamespace`, called from
`ArchitectureIlMethodBodyScanner`/`ArchitectureExternalDependencyIlScanner` before their own per-type loop
even starts) and Roslyn-based source scanning (`ArchitectureSourceScanner.FindMethodBodyViolations`, the
CLI's only other method-body scanning pipeline besides IL) had no token at all; and the canonical spec's
scanning and rendering scenarios did not mention either gap, so they read as already covering guarantees the
implementation did not yet meet.

## What Changes

- Thread `CancellationToken` through `ArchitectureFindingMapper.FromViolations`, checked per violation as
  each finding's diagnostic and identity are constructed — not only in the per-item loop callers run
  afterward over the already-fully-built result.
- Add a cancellation-aware `ArchitectureDiagnosticFormatter.FormatCoverageForHumans` overload (plus an
  `ICliRuntime`/`CliRuntime` DIM pair mirroring the existing `FormatViolationsForHumans` pattern), and thread
  the token through both of `ArchitectureDiagnosticFormatter`'s/`ArchitectureSarifFormatter`'s existing calls
  into `FromViolations`.
- Add `CancellationToken` parameters to `ArchitectureTypeScanner.FindTypesInLayer`/`FindTypesInNamespace`
  (and the shared private `FindTypes` helper), checked per target assembly, and thread
  `ArchitectureIlMethodBodyScanner`'s token into both calls.
- Add a `CancellationToken` parameter to `ArchitectureSourceScanner.FindMethodBodyViolations` and its
  `FindMatchingSourceFiles`/`FindSourceFilesForNamespace` helpers, checked before the Roslyn compilation is
  built, per syntax tree while analyzing it, and per file while discovering the source set — and thread
  `Context.CancellationToken` into its `ArchitectureAnalysisSession` call site.
- Reconcile the canonical spec's deep-scanning and mid-render scenarios to name
  `ArchitectureFindingMapper.FromViolations`, `FormatCoverageForHumans`, `ArchitectureTypeScanner`, and
  `ArchitectureSourceScanner.FindMethodBodyViolations` as interruptible boundaries.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `cooperative-cancellation`: Close the remaining mapping-level rendering gap and the type-discovery/source-
  scanning gap found in PR #416's fourth review round, and make the spec's scenarios match the
  implementation.

## Impact

`ArchitectureFindingMapper`, `ArchitectureDiagnosticFormatter`, `ArchitectureSarifFormatter`, `ICliRuntime`,
`CliRuntime`, `ArchitectureTypeScanner`, `ArchitectureIlMethodBodyScanner`, `ArchitectureSourceScanner`,
`ArchitectureAnalysisSession.Checking`, NUnit regression tests, and the `cooperative-cancellation`
specification.
