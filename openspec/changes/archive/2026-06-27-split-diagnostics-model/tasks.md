## 1. Diagnostic model

- [x] 1.1 Add `ArchitectureDiagnosticKind` enum (Dependency, Cycle, UnmatchedIgnore, Configuration, ExternalDependency) in `src/ArchLinterNet.Core/Model/`.
- [x] 1.2 Add abstract `ArchitectureDiagnostic` base record with `Kind` discriminator and shared identifying fields (`ContractName`, `ContractId`).
- [x] 1.3 Add sealed `DependencyDiagnostic` record (`SourceType`, `ForbiddenNamespace`, `ForbiddenReferences`, `SourceLayer`, `TargetLayer`, `AllowedImporters`).
- [x] 1.4 Add sealed `CycleDiagnostic` record (cycle path).
- [x] 1.5 Add sealed `UnmatchedIgnoreDiagnostic` record (`IgnoreIndex`, `SourceType`, `ForbiddenReference`, `Reason`).
- [x] 1.6 Add sealed `ConfigurationDiagnostic` record (`TemplateName`, `ContainerNamespace`, `DependencyPaths`, plus base violation fields). `MatchedNamespacePrefixes` moved to the shared base instead (see design.md decisions).
- [x] 1.7 Add sealed `ExternalDependencyDiagnostic` record (`ForbiddenExternalGroup`, plus base violation fields).

## 2. Adapter layer

- [x] 2.1 Add `ArchitectureDiagnosticMapper` static class in `src/ArchLinterNet.Core/Reporting/`.
- [x] 2.2 Implement `FromViolation(ArchitectureViolation)` with field-presence classification (configuration fields → `ConfigurationDiagnostic`; `ForbiddenExternalGroup` → `ExternalDependencyDiagnostic`; otherwise → `DependencyDiagnostic`).
- [x] 2.3 Implement `FromCycle(IReadOnlyCollection<string> path, string contractName, string? contractId)` returning `CycleDiagnostic`.
- [x] 2.4 Implement `FromUnmatchedIgnore(ArchitectureUnmatchedIgnoredViolation)` returning `UnmatchedIgnoreDiagnostic`.

## 3. Formatter migration

- [x] 3.1 Update `ArchitectureDiagnosticFormatter` internals to map legacy results to `ArchitectureDiagnostic` via the mapper before formatting.
- [x] 3.2 Replace optional-field null-checks in formatter bodies with pattern matching (`switch`) over `ArchitectureDiagnostic` subtypes.
- [x] 3.3 Verify all public formatter method signatures called from `src/ArchLinterNet.Cli/Program.cs` are unchanged.

## 4. Tests

- [x] 4.1 Add `ArchitectureDiagnosticMapperTests.cs` covering one conversion case per checker family (layer, allow-only, method-body, asmdef, independence, protected-surface, external-dependency, configuration, cycle, unmatched ignore).
- [x] 4.2 Add `ArchitectureDiagnosticFormatterTests.cs` with human-readable and JSON regression cases per diagnostic kind.
- [x] 4.3 Confirm `tests/ArchLinterNet.Core.Tests/UnifiedJsonOutputTests.cs` passes unmodified.
- [x] 4.4 Confirm CLI-level tests in `tests/ArchLinterNet.Cli.Tests/` pass unmodified.

## 5. Validation

- [x] 5.1 Run `make fmt`.
- [x] 5.2 Run `task acceptance:fresh` (or `make acceptance` if `task` target unavailable) and resolve any failures. Note: this repo has no Taskfile (`task --init` required, none present) — ran `make acceptance` instead, which passed (lint + all 424 tests).
- [x] 5.3 Run `openspec validate --all` after archiving the change.
