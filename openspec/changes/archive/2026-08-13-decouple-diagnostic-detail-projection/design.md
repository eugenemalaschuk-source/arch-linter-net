## Context

`ArchitectureDiagnosticFormatter` builds two structurally similar JSON dictionaries per finding — the top-level normalized-finding fields (`ToCiJsonObject`) and the nested `details` object (`BuildDetailsJsonObject`) — both of which call `ApplyDiagnosticSpecificCiFields(diagnostic, obj)` to add family-specific fields on top of the shared fields (`source`, `forbidden_namespace`, `forbidden_references`, etc.). That method is a single `switch (diagnostic) { case XDiagnostic x: ApplyXCiFields(x, obj); break; ... }` over all 24 sealed subtypes of `ArchitectureDiagnostic` (see `ArchitectureDiagnosticKind` for the closed enum). All 24 subtypes derive directly from `ArchitectureDiagnostic` — the hierarchy is flat, so exact-type dispatch is sufficient; no subtype needs to match by base-type or interface.

Of the 24 cases, 8 already delegate to a family-owned `Apply<Family>CiFields` method living in that family's own formatter partial file (`.Context.cs`, `.FrameworkReference.cs`, `.LayoutConventions.cs`, `.PublicApiSurface.cs`) or directly in `ArchitectureDiagnosticFormatter.cs`. The remaining cases inline their field-setting logic directly in the switch body.

This repo already has precedent for exactly this shape of refactor: `ArchitectureContractFamilyRegistry.All` (`src/ArchLinterNet.Core/Execution/ArchitectureContractFamilyRegistry.cs`) replaced a hand-edited per-family switch/handler-class pattern with an ordered static list of descriptors, each carrying a `Checker` delegate that may itself live in another file (e.g. `ArchitectureAnalysisSession.CheckXContract`). `ArchitectureContractHandlerRegistry` wraps that list in a `Dictionary<string, ArchitectureContractChecker>` for O(1) dispatch and throws `InvalidOperationException` for an unregistered family. This design mirrors that idiom.

## Goals / Non-Goals

**Goals:**
- Eliminate the central switch as the only place a new diagnostic family's structured CI/JSON output can be added.
- Preserve byte-identical wire output (field names, presence/omission, ordering, values) for every existing diagnostic kind.
- Keep each family's projection logic where it already naturally lives (its own partial file) rather than centralizing all 24 method bodies into the registry file.
- Provide a test that fails if a diagnostic kind exists without a registered projector.

**Non-Goals:**
- Changing human-readable output (`BuildHumanContext`) — out of scope per the issue.
- Changing the shared-field switches (`SourceTypeOf`, `ForbiddenNamespaceOf`, `ForbiddenReferencesOf`) or any switch in `ArchitectureFindingMapper.cs` — separate, unrelated dispatch points not mentioned in the issue's scope.
- Introducing a runtime-discovered/reflection-driven plugin model — the registry stays a static, compiled, ordered list; reflection is used only in the completeness test to enumerate subtypes, never at runtime.
- Changing the normalized-finding schema, SARIF formatter architecture, or the Testing adapter.

## Decisions

**Registry shape: `Dictionary<Type, Action<ArchitectureDiagnostic, Dictionary<string, object?>>>` keyed by exact CLR type, built from an ordered list.**
Because the diagnostic hierarchy is flat and every subtype is sealed, `diagnostic.GetType()` is a safe, unambiguous dispatch key — no risk of a subtype matching more than one entry, unlike the contract registry's `Type[]` `OwnedContractTypes` (which exists for a different purpose: catalog metadata, not dispatch). A single ordered `List<DiagnosticDetailProjection>` (a record pairing a `Type` and a delegate) is exposed as `DiagnosticDetailProjectionRegistry.All`, and a `Dictionary<Type, ...>` built from it is used for the actual lookup — mirroring `ArchitectureContractFamilyRegistry.All` feeding `ArchitectureContractHandlerRegistry`'s dictionary exactly. The list form (rather than a bare dictionary literal) keeps the completeness test able to assert both "no duplicate keys" and "exactly N entries," matching `ArchitectureContractFamilyRegistryTests`'s existing idiom.

**Projector bodies stay in their natural home; the registry only wires them up.**
`DiagnosticDetailProjectionRegistry` lives in a new file, `src/ArchLinterNet.Core/Reporting/ArchitectureDiagnosticFormatter.DetailProjectionRegistry.cs`, as a `private static` member of the `ArchitectureDiagnosticFormatter` partial class (not a standalone public type — nothing outside the formatter needs to see it, and keeping it `private`/`internal` avoids expanding the class's public surface for a pure implementation detail). Each registry entry's lambda calls the existing or newly-extracted `Apply<Kind>CiFields` method, which continues to live wherever it already lives (family partial file) or, for newly-extracted cases with no existing dedicated file, in `.NormalizedDetails.cs` itself (the closest existing home for cases without a family-specific partial).

**Cases requiring extraction (no existing dedicated method) and their target file:**
| Diagnostic type | New method | Target file |
|---|---|---|
| `ExternalDependencyDiagnostic` | `ApplyExternalDependencyCiFields` | `.NormalizedDetails.cs` |
| `PackageDependencyDiagnostic` | `ApplyPackageDependencyCiFields` | `.NormalizedDetails.cs` |
| `PackageAllowOnlyDiagnostic` | `ApplyPackageAllowOnlyCiFields` | `.NormalizedDetails.cs` |
| `FrameworkReferenceDiagnostic` | `ApplyFrameworkReferenceCiFields` (sets `forbidden_framework_group`, then delegates to existing `ApplyFrameworkReferenceEvidenceCiFields`) | `.FrameworkReference.cs` (joins the family's existing evidence helper) |
| `FrameworkReferenceAllowOnlyDiagnostic` | `ApplyFrameworkReferenceAllowOnlyCiFields` (sets `allowed_framework_groups`, then delegates to `ApplyFrameworkReferenceEvidenceCiFields`) | `.FrameworkReference.cs` |
| `CycleDiagnostic` | `ApplyCycleCiFields` | `.NormalizedDetails.cs` |
| `UnmatchedIgnoreDiagnostic` | `ApplyUnmatchedIgnoreCiFields` | `.NormalizedDetails.cs` |
| `PolicyConsistencyDiagnostic` | `ApplyPolicyConsistencyCiFields` | `.NormalizedDetails.cs` |
| `BaselineLifecycleDiagnostic` | `ApplyBaselineLifecycleCiFields` | `.NormalizedDetails.cs` |
| `ArchitecturePolicyErrorDiagnostic` | `ApplyArchitecturePolicyErrorCiFields` | `.NormalizedDetails.cs` |

Every extracted method's body is a verbatim copy of its former switch-case body (same dictionary keys, same order, same null checks) — this is a pure Extract Method refactor with no logic change, which is why existing snapshot/schema tests are expected to pass unmodified.

**`ApplyBuildStatePreflightCiFields` relocates to `.BuildStatePreflight.cs`.**
It already exists as a named method (not inline in the switch), but currently sits in `.NormalizedDetails.cs` even though `.BuildStatePreflight.cs` is that family's dedicated partial (owning `FormatBuildStatePreflightForHumans`, `StateToken`, `BuildStatePreflightJson`). Moving it there is a small, low-risk consistency fix directly serving the issue's "family owns its own projection" requirement — no behavior change, same file just relocates a method.

**Unregistered-type behavior: throw `InvalidOperationException`.**
`ApplyDiagnosticSpecificCiFields` becomes a thin registry lookup:
```csharp
private static void ApplyDiagnosticSpecificCiFields(ArchitectureDiagnostic diagnostic, Dictionary<string, object?> obj)
{
    if (!DetailProjectionRegistry.TryGetValue(diagnostic.GetType(), out var projector))
    {
        throw new InvalidOperationException(
            $"No diagnostic detail projector registered for diagnostic type '{diagnostic.GetType().Name}'.");
    }
    projector(diagnostic, obj);
}
```
This mirrors `ArchitectureContractHandlerRegistry.Execute`'s behavior for an unregistered family and gives a clear failure mode in production, not just in tests, if a new diagnostic type is ever introduced without updating the registry.

**Completeness test: reflection over the Core assembly.**
A new test enumerates every sealed, non-abstract type in `typeof(ArchitectureDiagnostic).Assembly` that derives from `ArchitectureDiagnostic`, and asserts `DiagnosticDetailProjectionRegistry.All.Select(e => e.DiagnosticType)` is exactly that set (no missing, no stale/extra entries) — mirroring `ArchitectureContractFamilyRegistryTests.All_ContainsExactlyTheHistoricalFamilyCount` and `All_HasNoDuplicateFamilyIds`. This directly satisfies the issue's acceptance criterion that a newly-added, unregistered diagnostic kind fails a test.

## Risks / Trade-offs

- **[Risk]** A hand-copied extraction could silently reorder or drop a dictionary key, changing JSON field order (Dictionary iteration order is insertion order in practice, and existing tests appear to depend on it). → **Mitigation**: copy each case body verbatim, character-for-character where possible, and run the full existing `ArchitectureDiagnosticFormatterTests` suite (plus SARIF/coverage suites) unmodified before and after — any drift fails immediately.
- **[Risk]** Throwing at runtime for an unregistered type is a behavior change from today's switch, which silently applies zero extra fields (falls through with no `default` case) for an unmatched type. → **Mitigation**: this is unreachable in practice today (the 24 cases are exhaustive over the closed set of subtypes), and matches the intentionally stricter posture the issue asks for ("cannot silently omit structured output"). Since the switch was already exhaustive, no existing input can hit the new throw path.
- **[Trade-off]** Placing several small `Apply<Kind>CiFields` methods in `.NormalizedDetails.cs` for kinds with no natural dedicated partial file keeps that file as a partial "misc" home rather than fully distributing every family into its own file. Accepted: creating 8 new near-empty partial files for one-or-two-line projectors would add more indirection than it removes, and the issue only requires that adding a family not require editing a central *switch* — a registry entry (one line, one method) is not that.

## Migration Plan

Pure internal refactor, no data migration, no external API change. Land as a single PR:
1. Add the registry and extracted methods.
2. Point `ApplyDiagnosticSpecificCiFields` at the registry.
3. Move `ApplyBuildStatePreflightCiFields`.
4. Add the completeness test.
5. Run the full existing formatter/coverage/SARIF/schema test suites to confirm zero output drift.

No rollback concerns beyond a normal `git revert` — nothing depends on the registry's existence outside this one method.

## Open Questions

None — the design directly mirrors an established, already-reviewed pattern in this codebase (`ArchitectureContractFamilyRegistry`/`ArchitectureContractHandlerRegistry`), and the extraction is behavior-preserving by construction.
