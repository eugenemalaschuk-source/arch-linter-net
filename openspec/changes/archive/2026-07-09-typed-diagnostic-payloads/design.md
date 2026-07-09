## Context

`ArchitectureViolation` (`src/ArchLinterNet.Core/Model/ArchitectureViolation.cs`) is the shared output type produced by every checker/finder/scanner in `src/ArchLinterNet.Core/Execution/` and `src/ArchLinterNet.Core/Scanning/`. It has 5 required fields common to all violations (`ContractName`, `ContractId`, `SourceType`, `ForbiddenNamespace`, `ForbiddenReferences`) plus ~35 optional `init` properties, each belonging to exactly one of ten families: Configuration (template/container/dependency-path), ExternalDependency, PackageDependency, TypePlacement, PublicApiSurface, AttributeUsage, Inheritance, InterfaceImplementation, Composition, ProjectMetadata, and Dependency's own `SourceLayer`/`TargetLayer`/`AllowedImporters`.

`ArchitectureDiagnosticMapper.FromViolation` (`src/ArchLinterNet.Core/Reporting/ArchitectureDiagnosticMapper.cs`) converts an `ArchitectureViolation` into the corresponding sealed `ArchitectureDiagnostic` subtype (`ConfigurationDiagnostic`, `ExternalDependencyDiagnostic`, ..., falling back to `DependencyDiagnostic`) by checking, in order, which family's fields are non-null. This if-chain is the only place that knows the mapping from "which fields are populated" to "which family this is" — every new family has required both a new field cluster on `ArchitectureViolation` and a new branch here.

A newer family, policy consistency, does not go through this path at all: `ArchitectureAnalysisSession.PolicyConsistency.cs` constructs `PolicyConsistencyDiagnostic` (`src/ArchLinterNet.Core/Model/PolicyConsistencyDiagnostic.cs`) directly, as its own sealed record with its own fields, and callers consume `List<PolicyConsistencyDiagnostic>` directly — no shared bag, no mapper branch. This design generalizes that pattern to the other ten families without disturbing policy consistency (already correct) or the output-side `ArchitectureDiagnostic` subtypes, `ArchitectureDiagnosticFormatter`, or `ArchitectureSarifFormatter` (all already consume `ArchitectureDiagnostic`, not the violation bag, and are unaffected).

## Goals / Non-Goals

**Goals:**
- Eliminate the ~35 family-specific nullable fields from `ArchitectureViolation`, replacing them with a single typed payload slot.
- Eliminate the if-chain in `ArchitectureDiagnosticMapper.FromViolation`, replacing it with a null check and a virtual dispatch call.
- Make adding a new diagnostic family require touching only: one new payload record, its construction at the relevant checker/finder call site. No edits to `ArchitectureViolation` or `ArchitectureDiagnosticMapper`.
- Preserve existing JSON, SARIF, and human-readable output exactly (byte-identical) for all existing families.

**Non-Goals:**
- Changing violation semantics or which violations are produced.
- Redesigning `ArchitectureDiagnostic`, `ArchitectureDiagnosticFormatter`, or `ArchitectureSarifFormatter` — they already consume the diagnostic-side model and need no changes.
- Removing or renaming existing JSON/SARIF output fields.
- Migrating `PolicyConsistencyDiagnostic` (already follows the target pattern) or any checker that only ever produces plain dependency violations (e.g. `AssemblyIndependenceChecker`, which sets no family-specific fields today and keeps using the bare `ArchitectureViolation` constructor).
- Touching `ArchitectureContractRunner`, `ArchitectureValidator`, CLI commands, or the Testing API — all of these pass `List<ArchitectureViolation>` opaquely through to the formatter/mapper and never read the family-specific fields directly, so their signatures are unaffected.

## Decisions

### 1. `IArchitectureDiagnosticPayload` as a single-method dispatch interface

```csharp
namespace ArchLinterNet.Core.Model;

public interface IArchitectureDiagnosticPayload
{
    ArchitectureDiagnostic ToDiagnostic(ArchitectureViolation violation);
}
```

Each family gets a sealed payload record implementing this, e.g.:

```csharp
public sealed record ExternalDependencyPayload(string ForbiddenExternalGroup) : IArchitectureDiagnosticPayload
{
    public ArchitectureDiagnostic ToDiagnostic(ArchitectureViolation violation) =>
        new ExternalDependencyDiagnostic(
            violation.ContractName, violation.ContractId, violation.SourceType,
            violation.ForbiddenNamespace, violation.ForbiddenReferences, ForbiddenExternalGroup)
        {
            MatchedNamespacePrefixes = violation.MatchedNamespacePrefixes
        };
}
```

**Alternative considered**: a registry/dictionary keyed by payload `Type` mapping to a delegate, resolved via reflection or a static registration call. Rejected — it reintroduces a central, growing registration list (even if not an if-chain) and adds indirection with no benefit over letting each payload type own its own conversion via an interface method, which the C# type system already dispatches correctly with zero central bookkeeping.

**Alternative considered**: keep the 10 branches but switch on `violation.GetType()` or a new required `Kind` field. Rejected — still a shared file every family must edit; doesn't meet the acceptance criterion that new families need no mapper edits.

### 2. Collapse `ArchitectureViolation` to common fields + `MatchedNamespacePrefixes` + `Payload`

```csharp
public sealed record ArchitectureViolation(
    string ContractName,
    string? ContractId,
    string SourceType,
    string ForbiddenNamespace,
    IReadOnlyCollection<string> ForbiddenReferences)
{
    public IReadOnlyCollection<string>? MatchedNamespacePrefixes { get; init; }
    public IArchitectureDiagnosticPayload? Payload { get; init; }
}
```

`MatchedNamespacePrefixes` stays on `ArchitectureViolation` (not folded into a payload) because it is already documented as cross-family shared metadata on the `ArchitectureDiagnostic` base record itself (`openspec/specs/diagnostics-model/spec.md`, "Matched namespace prefixes are available on any diagnostic kind") and is set by multiple unrelated families (plain dependency, configuration, external dependency). Moving it into per-family payloads would duplicate a field that is genuinely common, not family-specific — the opposite of this change's intent.

### 3. Mapper becomes a null check plus dispatch, with a bare fallback for payload-less violations

```csharp
public static ArchitectureDiagnostic FromViolation(ArchitectureViolation violation) =>
    violation.Payload?.ToDiagnostic(violation)
    ?? new DependencyDiagnostic(
        violation.ContractName, violation.ContractId, violation.SourceType,
        violation.ForbiddenNamespace, violation.ForbiddenReferences)
    {
        MatchedNamespacePrefixes = violation.MatchedNamespacePrefixes
    };
```

**Why keep a bare (payload-less) fallback instead of requiring every call site to construct a `DependencyPayload`**: most `ArchitectureViolation` construction sites (layer, allow-only, method-body, asmdef, independence, protected-surface contracts — the majority of the 24 files that construct `ArchitectureViolation`) never set any family-specific field today; they rely on the implicit "nothing matched" branch. Forcing all of them to construct an explicit `DependencyPayload` would inflate this change's diff for no output difference. `DependencyPayload` (see below) is still introduced for the sites that *do* set `SourceLayer`/`TargetLayer`/`AllowedImporters` today, for symmetry with every other family; sites that never set those keep using the bare constructor and fall through the null-payload branch, exactly as they hit the final if-chain branch today.

### 4. One payload type per family; only call sites that set family fields today change

Confirmed via `grep` across `src/ArchLinterNet.Core` for family-specific field assignments, these are the only production files that construct family-specific `ArchitectureViolation` fields and therefore the only ones needing changes:

| Family | Payload type | Call site(s) |
|---|---|---|
| Configuration | `ConfigurationPayload` (`TemplateName`, `ContainerNamespace`, `DependencyPaths`) | `ArchitectureAnalysisSession.Checking.cs`, `LayerTemplateExpander.cs` |
| ExternalDependency | `ExternalDependencyPayload` (`ForbiddenExternalGroup`) | `ArchitectureExternalDependencyViolationFinder.cs`, `ArchitectureExternalDependencyIlScanner.cs` |
| PackageDependency | `PackageDependencyPayload` (`ForbiddenPackageGroup`) | `ArchitectureAnalysisSession.PackageDependency.cs` |
| TypePlacement | `TypePlacementPayload` (`ExpectedTypeLocation`, `ActualTypeLocation`, `ExpectedTypeName`, `ActualTypeName`) | `ArchitectureAnalysisSession.TypePlacement.cs` |
| PublicApiSurface | `PublicApiSurfacePayload` (`UndeclaredApiSignature`, `ForbiddenPublicConstant`, `ApiAssemblyName`, `ApiVisibility`) | `Execution/Checkers/PublicApiSurfaceChecker.cs` |
| AttributeUsage | `AttributeUsagePayload` (`MatchedAttribute`, `AttributeUsageKind`, `ExpectedAttributeLocation`, `ActualAttributeLocation`) | `ArchitectureAnalysisSession.AttributeUsage.cs` |
| Inheritance | `InheritancePayload` (`ForbiddenBaseType`, `InheritanceSourceSurface`) | `Execution/Checkers/InheritanceChecker.cs` |
| InterfaceImplementation | `InterfaceImplementationPayload` (`MatchedInterface`, `ImplementationKind`, `ExpectedImplementationLocation`, `ActualImplementationLocation`) | `ArchitectureAnalysisSession.InterfaceImplementation.cs` |
| Composition | `CompositionPayload` (`SourceMember`, `MatchedForbiddenApi`, `ExpectedCompositionBoundary`) | `ArchitectureAnalysisSession.Composition.cs` |
| ProjectMetadata | `ProjectMetadataPayload` (`ProjectMetadataKind`, `ProjectMetadataKey`, `ProjectMetadataExpectedValue`, `ProjectMetadataActualValue`, `ProjectMetadataSourcePath`) | `ArchitectureAnalysisSession.ProjectMetadata.cs` |
| Dependency (layer fields) | `DependencyPayload` (`SourceLayer`, `TargetLayer`, `AllowedImporters`) | `ArchitectureAnalysisSession.cs`, `ArchitectureNamespaceViolationFinder.cs` |

Each payload record lives in `src/ArchLinterNet.Core/Model/`, next to its corresponding `*Diagnostic` record, and its `ToDiagnostic` mirrors exactly what the corresponding if-branch in the old mapper did — this is a mechanical, behavior-preserving move, not a redesign.

## Risks / Trade-offs

- **[Risk]** Missing a call site that sets a family field, silently downgrading a diagnostic to plain `DependencyDiagnostic` and changing output → **Mitigation**: the grep-derived call-site list above is exhaustive over `src/ArchLinterNet.Core`; after the refactor, re-run the same grep for the old field names — zero remaining hits (outside the deleted properties themselves and historical `openspec/changes/archive/` docs) confirms nothing was missed. Output-parity regression tests (see tasks.md) catch any behavioral drift directly.
- **[Risk]** Test files reference the removed bag fields directly (`ArchitectureDiagnosticMapperTests.cs`, `ArchitectureSarifFormatterTests.cs`, `ProtectedContractTests.EdgeCases.cs`, `ArchitectureValidationApplicationServiceFakeCompositionTests.cs`) → **Mitigation**: update each to construct the new payload type; this is required for the build to compile under `TreatWarningsAsErrors`, so it cannot be silently skipped.
- **[Trade-off]** Introducing 11 small payload record files instead of 1 field-heavy record is more files but each is single-purpose and mirrors the existing `*Diagnostic` sealed-subtype file layout already established in `src/ArchLinterNet.Core/Model/` — consistent with the codebase's existing one-record-per-file convention for this hierarchy.
- **[Risk]** `record` value equality changes: `ArchitectureViolation` equality now compares `Payload` by the payload record's own value equality (records get this for free) rather than comparing ~35 individual fields — behaviorally equivalent for any test using `Assert.That(violation, Is.EqualTo(...))`, but worth confirming no test relies on comparing two violations with different payload runtime types.

## Migration Plan

1. Add `IArchitectureDiagnosticPayload` and the 11 payload records (additive, does not break existing code).
2. Add `Payload` property to `ArchitectureViolation` alongside the existing fields (temporarily both exist).
3. Update `ArchitectureDiagnosticMapper.FromViolation` to check `Payload` first, falling back to the existing if-chain for any violation that still uses old fields (transitional safety net during the same commit/PR, not a permanent state).
4. Migrate each of the ~14 call sites one family at a time, verifying tests after each.
5. Once all call sites are migrated, delete the ~35 old fields from `ArchitectureViolation` and the transitional if-chain fallback from the mapper.
6. Run full test suite and `make acceptance` to confirm output parity.

No runtime/deployment migration is needed — this is a compile-time internal refactor with no persisted state or external contract change.

## Open Questions

None — the design mirrors an existing, already-shipped pattern (`PolicyConsistencyDiagnostic`) in the same codebase, so there is no unresolved technical uncertainty.
