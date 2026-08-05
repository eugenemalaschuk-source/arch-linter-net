## 1. Interruptible single-format rendering

- [x] 1.1 Add cancellation-aware `ICliRuntime.FormatViolationsForHumans`/`FormatResultForCiArtifacts`/
      `FormatResultAsSarif` overloads (DIM, ignored-by-default) and override them in `CliRuntime`.
- [x] 1.2 Add per-finding cancellation-aware overloads to `ArchitectureDiagnosticFormatter` (violations +
      coverage findings in the CI-artifacts JSON builder) and `ArchitectureSarifFormatter` (violation
      entries), and thread `ReportCoordinator`'s token into them.

## 2. CLI evidence and publication safety

- [x] 2.1 Add a dedicated `catch (BuildStateProcessCleanupTimedOutException)` branch in
      `ValidateCommandHandler`, surfacing process ID/timeout in every output format.
- [x] 2.2 Change `WriteCancellation` to `allowFileSinks: false` so it never overwrites a configured
      `--report` file sink.

## 3. Deep scanning and import traversal

- [x] 3.1 Check cancellation per sibling import in `ArchitecturePolicyImportGraphResolver`'s loop, not only
      inside `Visit()`.
- [x] 3.2 Thread `CancellationToken` through `ArchitectureIlMethodBodyScanner`/
      `ArchitectureExternalDependencyIlScanner`, checked per source type.

## 4. Baseline/public-API temp-write-to-rename race

- [x] 4.1 Thread `CancellationToken` through `BaselineWriteGate.TryApply`/`TryCopySource`, checked
      immediately before the rename, with staged-temp cleanup on cancellation.
- [x] 4.2 Add a shared `PublicApiTwoPhaseWriter.WriteAndCommit` used by capture/update/migrate with the same
      check-before-rename-and-cleanup semantics.

## 5. Spec reconciliation

- [x] 5.1 Update the canonical spec's import-traversal, deep-scanning, rendering, cleanup-timeout, and
      publication-race scenarios to match the implementation above.

## 6. Validation

- [x] 6.1 Add deterministic regression tests for each fixed boundary.
- [x] 6.2 Run `make test` (Core.Tests and Cli.Tests) and `openspec validate --all`.
