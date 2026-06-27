## Context

Today, every checker that needs to report a problem populates an `ArchitectureViolation` record (`src/ArchLinterNet.Core/Model/ArchitectureViolation.cs`), which carries 4 base fields plus 8 optional fields (`SourceLayer`, `TargetLayer`, `AllowedImporters`, `ForbiddenExternalGroup`, `TemplateName`, `ContainerNamespace`, `DependencyPaths`, `MatchedNamespacePrefixes`) that are only meaningful for specific checker kinds. Cycles are reported as raw `IReadOnlyCollection<string>` path lists with no dedicated type. `ArchitectureUnmatchedIgnoredViolation` is already a properly scoped separate record. `ArchitectureDiagnosticFormatter` (`src/ArchLinterNet.Core/Reporting/`) formats all of these for human-readable text and CI JSON, and currently has to null-check optional fields to infer what kind of violation it's rendering.

`ValidationOutcome` (`src/ArchLinterNet.Core/Validation/ValidationOutcome.cs`) aggregates `Violations` / `Cycles` / `UnmatchedIgnoredViolations` and is the boundary between checkers and formatters.

## Goals / Non-Goals

**Goals:**
- Represent each diagnostic kind (dependency violation, cycle, unmatched ignore, configuration, external dependency) as its own typed shape, carrying only relevant fields.
- Decouple `ArchitectureDiagnosticFormatter` from checker-specific optional fields — it should pattern-match on diagnostic kind, not null-check arbitrary fields.
- Preserve human-readable and JSON output byte-for-byte for all current scenarios (verified by `UnifiedJsonOutputTests.cs` and existing CLI tests).

**Non-Goals:**
- Changing checker output types (`ArchitectureViolation`, `ArchitectureUnmatchedIgnoredViolation`, cycle detection) — they keep producing what they produce today.
- New output formats (e.g. SARIF, XML).
- Adding source locations to diagnostics.
- New contract/checker families.

## Decisions

**Decision: sealed record hierarchy with a `Kind` discriminator, not a single record with a `Kind` enum field.**
An abstract base `ArchitectureDiagnostic` with an abstract `Kind` discriminator (enum `ArchitectureDiagnosticKind`) and sealed per-kind records (`DependencyDiagnostic`, `CycleDiagnostic`, `UnmatchedIgnoreDiagnostic`, `ConfigurationDiagnostic`, `ExternalDependencyDiagnostic`) deriving from it. Each subtype declares only the fields relevant to its kind.
- *Alternative considered*: keep one record, add a `Kind` enum field, leave the rest of the fields optional (status quo with a discriminator bolted on). Rejected — doesn't solve the actual problem (formatters still have to know which optional fields apply per kind); it only adds a redundant flag.
- *Alternative considered*: visitor pattern over diagnostic kinds. Rejected as over-engineering for ~5 kinds and a formatter with two output modes (human/JSON) — a C# `switch` expression on sealed records gives exhaustiveness checking without the extra indirection.

**Decision: adapter is a static mapper, not a constructor/factory on `ArchitectureDiagnostic` itself.**
`ArchitectureDiagnosticMapper` (`src/ArchLinterNet.Core/Reporting/ArchitectureDiagnosticMapper.cs`) exposes `FromViolation(ArchitectureViolation)`, `FromCycle(IReadOnlyCollection<string> path, string contractName, string? contractId)`, and `FromUnmatchedIgnore(ArchitectureUnmatchedIgnoredViolation)`. This keeps the new model free of any dependency on legacy types — only the mapper knows about both sides.
- *Alternative considered*: implicit/explicit conversion operators on the new types. Rejected — conversion operators hide non-trivial branching logic (deciding `ConfigurationDiagnostic` vs `ExternalDependencyDiagnostic` vs `DependencyDiagnostic` from which optional fields are set) and are harder to test directly than a plain static method.

**Decision: `ArchitectureDiagnosticFormatter` takes `ArchitectureDiagnostic` collections internally, but its public surface (methods called from `Program.cs`) is unchanged.**
The formatter's existing public methods (`FormatViolationsForHumans`, `FormatCyclesForHumans`, etc.) keep their current signatures and call the mapper internally before formatting. This means zero changes to `src/ArchLinterNet.Cli/Program.cs` call sites and zero risk to output compatibility, while still moving the "what kind of thing is this" logic out of the formatter body and into the mapper + pattern match.
- *Alternative considered*: change `Program.cs` to map to `ArchitectureDiagnostic` before calling the formatter. Rejected for this change — it would touch CLI code unnecessarily; the issue scope is the model/formatter boundary, not the CLI entry point.

**Decision: dependency-violation kinds are split by which optional fields are populated, determined once in the mapper.**
The mapper inspects `ArchitectureViolation`'s optional fields to decide which subtype to produce: `ContainerNamespace`/`TemplateName`/`DependencyPaths` set → `ConfigurationDiagnostic`; `ForbiddenExternalGroup` set → `ExternalDependencyDiagnostic`; otherwise → `DependencyDiagnostic` (covers layer/allow-only/method-body/asmdef/independence/protected-surface checks, which use the base + `SourceLayer`/`TargetLayer`/`AllowedImporters`). This mirrors the field groupings observed in `ArchitectureContractRunner.Checking.cs` without requiring checkers to declare their kind explicitly — a non-goal would be retrofitting checkers to emit a kind tag.

`MatchedNamespacePrefixes` is **not** kind-specific: investigation found a real call site (protected-layer checks in `ArchitectureContractRunner.Checking.cs`) that sets both `SourceLayer`/`TargetLayer`/`AllowedImporters` (the dependency group) and `MatchedNamespacePrefixes` on the same violation, for display purposes (annotating which namespace prefix matched). A strict mutually-exclusive-kind split would lose this field at that call site. Instead, `MatchedNamespacePrefixes` is hoisted onto the shared base `ArchitectureDiagnostic` as an optional field available to every subtype, since it is purely a display annotation about namespace matching and not data specific to any one diagnostic kind.

## Risks / Trade-offs

- **[Risk]** Misclassifying which legacy field combination maps to which new kind could produce wrong diagnostics → **Mitigation**: cover every checker family (layer, allow-only, method-body, asmdef, independence, protected-surface, external-dependency, configuration) with a conversion test asserting the mapper picks the expected subtype.
- **[Risk]** Formatter output drift during the refactor → **Mitigation**: run `UnifiedJsonOutputTests.cs` and add new human/JSON regression tests before and after the refactor; no test changes should be needed for existing tests to keep passing.
- **[Risk]** Field-presence heuristic in the mapper is implicit and could silently misclassify a future checker that reuses an existing optional field for a new purpose → accepted trade-off for this change since no checker output types change; documented in mapper code comments and covered by the conversion test suite per checker family.

## Migration Plan

No data migration. This is an internal-only model addition with an adapter; CLI behavior and output format are unaffected. Rollback is a plain revert (no external state to undo).
