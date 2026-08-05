## 1. Interruptible finding mapping

- [x] 1.1 Add a `cancellationToken` parameter to `ArchitectureFindingMapper.FromViolations`, checked per
      violation, and thread it through `ArchitectureDiagnosticFormatter`'s and `ArchitectureSarifFormatter`'s
      existing calls.
- [x] 1.2 Add a cancellation-aware `FormatCoverageForHumans` overload (`ICliRuntime` DIM +
      `ArchitectureDiagnosticFormatter` concrete overloads), overridden in `CliRuntime`, and thread
      `ReportCoordinator`'s token into it.

## 2. Type discovery and Roslyn source scanning

- [x] 2.1 Add a `cancellationToken` parameter to `ArchitectureTypeScanner.FindTypesInLayer`/
      `FindTypesInNamespace`/`FindTypes`, checked per target assembly, and thread it through
      `ArchitectureIlMethodBodyScanner`'s two call sites.
- [x] 2.2 Add a `cancellationToken` parameter to `ArchitectureSourceScanner.FindMethodBodyViolations`,
      checked before the Roslyn compilation is built and per syntax tree while analyzing it, and to its
      `FindMatchingSourceFiles`/`FindSourceFilesForNamespace` helpers, checked per file; thread
      `Context.CancellationToken` into `ArchitectureAnalysisSession.Checking`'s call site.

## 3. Spec reconciliation

- [x] 3.1 Update the canonical spec's deep-scanning scenario to name `ArchitectureTypeScanner` and
      `ArchitectureSourceScanner.FindMethodBodyViolations` as interruptible boundaries.
- [x] 3.2 Update the canonical spec's mid-render scenario to name `ArchitectureFindingMapper.FromViolations`
      mapping and `FormatCoverageForHumans` as interruptible boundaries, not only per-item serialization.

## 4. Validation

- [x] 4.1 Add deterministic regression tests proving genuine mid-enumeration cancellation for each fixed
      boundary (custom side-effecting `IEnumerable<Assembly>`/`IArchitectureFileSystem` wrappers, not just
      pre-cancelled-token tests).
- [x] 4.2 Run `make test` (CEL.Tests, Core.Tests, Cli.Tests) and `openspec validate --all`.
