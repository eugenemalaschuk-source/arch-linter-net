## 1. Process cleanup

- [x] 1.1 Bound `BuildStatePreparationService`'s post-kill wait with a typed
      `BuildStateProcessCleanupTimedOutException` instead of discarding the wait result.

## 2. Rendering and deep scan boundaries

- [x] 2.1 Thread `CancellationToken` through `ReportCoordinator`'s FormatSingle/CombinedHuman/Json/Sarif and
      `AppendHumanSection`, checked between sections and between modes.
- [x] 2.2 Thread `CancellationToken` through `IArchitecturePolicyDocumentLoader`/
      `ArchitecturePolicyImportGraphResolver`, checked per document/import.
- [x] 2.3 Thread `CancellationToken` through `ArchitectureTypeIndex`/`ArchitectureRoleIndex`, checked per
      assembly/type.
- [x] 2.4 Thread `CancellationToken` through `BuildStateCanonicalHasher`, checked per file.

## 3. CLI publication safety

- [x] 3.1 Fix `ValidateCommandHandler.WriteCancellation` to route with a fresh token so the cancellation
      notice itself is not blocked by the already-cancelled handler token.
- [x] 3.2 Add a dedicated `OperationCanceledException` branch and pre-publish token re-check to every
      baseline command handler (`update`/`generate`/`prune`/`migrate`/`diff`/`verify`).
- [x] 3.3 Add a dedicated `OperationCanceledException` branch and pre-publish token re-check to every
      public-API command handler (`update`/`capture`/`migrate`/`diff`).

## 4. Spec reconciliation

- [x] 4.1 File issue #418 tracking profile-generation/artifact-cleanup cancellation, blocked on #374.
- [x] 4.2 Rewrite the canonical spec's baseline/public-API/profile paragraph to one consistent statement and
      add scenarios for the fixes above.
- [x] 4.3 Correct `fix-cooperative-cancellation-gaps/tasks.md` task 2.3, which had claimed profile scope was
      extended.

## 5. Validation

- [x] 5.1 Add deterministic regression tests for each fixed boundary.
- [x] 5.2 Run `make test` (Core.Tests and Cli.Tests) and `openspec validate --all`.
