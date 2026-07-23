## Context

`.NET` projects can pull in a broad shared-framework surface (e.g. `Microsoft.AspNetCore.App`, `Microsoft.WindowsDesktop.App`) via the MSBuild `FrameworkReference` item, independent of `PackageReference`. Existing package contracts (`package_dependency`/`package_allow_only`) only see `PackageReference` items, so there is no way today to forbid or restrict framework-reference declarations. Issue #359 asks for strict/audit governance parallel to the package contract-family architecture; the issue owner's priority clarification restricts this change to the exact single-source rule (P1) and explicitly defers reusable multi-source/glob authoring to #369 (P2).

The codebase already has a mature, repeated pattern for this shape of contract: `packages` groups + `package_dependency`/`package_allow_only` contract families, evaluated through `ArchitectureContractFamilyRegistry`, rendered through the shared diagnostic/formatter/SARIF/Testing API pipeline, and given exact-identity baseline support. This design mirrors that pattern as closely as possible rather than inventing a new shape.

## Goals / Non-Goals

**Goals:**
- Parse `FrameworkReference` MSBuild items (`Include`, optional `Condition`) during project discovery.
- Let policies declare named `framework_references` groups (exact names + dot-segment prefixes), mirroring `packages`.
- Support `strict_framework_dependency`/`audit_framework_dependency` (forbid) and `strict_framework_allow_only`/`audit_framework_allow_only` (allow-list) contracts, each targeting exactly one `source` project.
- Produce typed, human/JSON/SARIF/Testing-API-equivalent diagnostics distinct from package diagnostics.
- Preserve condition/TFM occurrence identity so the same framework reference in two different projects, or under two different conditions in the same project, is a distinct baseline entry.
- Update schema, capability manifest, and docs so the new contract families are fully authorable and documented.

**Non-Goals:**
- Reusable multi-source/glob contract expansion across many projects from one contract (#369, P2).
- Detecting actual framework API usage in compiled code (Roslyn/semantic analysis) — this is a pure declaration-level check, same boundary as package contracts.
- Governing implicit SDK-provided framework availability (only explicit `FrameworkReference` items are in scope).
- Version/central-package-management handling — `FrameworkReference` has no `Version` attribute.

## Decisions

### 1. Named framework groups, not raw framework names in contracts
Mirror `packages`: add a top-level `framework_references` map of named groups, each with `framework_names` (exact, case-insensitive) and `framework_name_prefixes` (dot-segment prefix, case-insensitive). Contracts reference group names in `forbidden`/`allowed`, exactly like package contracts reference `packages` group names. Alternative considered: let `forbidden`/`allowed` list raw framework names directly (no indirection), since the shared-framework namespace is small and closed. Rejected because the issue text explicitly asks for "reusable framework-reference groups" and prefix matching "where justified," and reusing the exact `packages` mental model keeps authoring and validator logic consistent (unknown/unusable-group configuration checks already exist for this shape).

### 2. No `dependency_depth` field on framework contracts
Package contracts have `dependency_depth` locked to `direct` (transitive is explicitly rejected). `FrameworkReference` has no transitive concept at all, so the framework contract schema omits `dependency_depth` entirely rather than carrying a vestigial field that only ever accepts one value. Alternative considered: include `dependency_depth: direct` for schema symmetry. Rejected as dead weight — omitting the field is simpler and avoids a rejection-path validator no one needs.

### 3. Condition/TFM identity
`FrameworkReference` items may carry a `Condition` attribute (commonly used for TFM-specific declarations in multi-targeted projects). `ArchitectureDiscoveredFrameworkReference` captures `Include` plus the raw `Condition` string (nullable). Violation/baseline identity threads the condition value through the same `SourceType`/`Configuration`-style identity fields `ArchitectureViolationIdentity` already exposes, so two conditional declarations of the same framework name in one project remain distinct baseline entries, and the same framework name in two different projects is naturally distinct via `SourceAssembly`.

### 4. New diagnostic kinds, not reuse of package diagnostics
Add `FrameworkReferenceDiagnostic`/`FrameworkReferenceAllowOnlyDiagnostic` with their own `ArchitectureDiagnosticKind` members and payload types, following the exact `PackageDependencyDiagnostic`/`PackageAllowOnlyDiagnostic` shape (source, forbidden/allowed group name, matched reference). This keeps package and framework violations separately identifiable in all output formats, matching the existing separation between package and external-dependency diagnostics.

### 5. Contract-family registry integration
Add two new `ArchitectureContractFamilyDescriptor` entries (`framework_dependency`, `framework_allow_only`) to `ArchitectureContractFamilyRegistry`, each wired to `strict_framework_*`/`audit_framework_*` contract-group properties and a new `CheckFrameworkDependencyContract`/`CheckFrameworkAllowOnlyContract` pair on `ArchitectureAnalysisSession` (new partial file `ArchitectureAnalysisSession.FrameworkReference.cs`), matching the existing `ArchitectureAnalysisSession.PackageDependency.cs` structure including `ConfigurationContributor` wiring for unknown/unusable framework-group detection.

### 6. Matching logic
Reuse the same exact + dot-segment-prefix matching semantics as `ArchitecturePackageDependencyResolver`, in a new `ArchitectureFrameworkReferenceResolver` (or a shared generic resolver if a low-risk extraction is straightforward without touching existing package behavior — decided at implementation time by whichever keeps the diff smallest and avoids behavior change to package matching).

## Risks / Trade-offs

- **Schema growth**: adding a fourth pair of `strict_*`/`audit_*` contract-group arrays and a new top-level group section increases schema surface. Mitigated by mirroring existing `$defs` structure exactly, keeping review low-risk.
- **Cross-cutting test fixtures**: several "every contract family" tests (registry, catalog, YAML round-trip, bindings) must be updated in lockstep or they will fail by design — these are treated as a checklist in tasks.md, not optional follow-ups.
- **Condition parsing fidelity**: MSBuild `Condition` expressions can be complex; this change only captures the raw condition string for identity/display purposes, it does not evaluate the condition against a specific TFM. This is consistent with how the parser already treats other conditional items and is not a regression.

## Migration Plan

Purely additive: no existing schema field, contract family, or diagnostic changes. Policies without `framework_references`/`framework_*` contracts are unaffected; no baseline migration is required for existing policies. No rollback concerns beyond reverting the change.

## Open Questions

None blocking — matching semantics extraction (shared vs. duplicated resolver) is an implementation-time call with no externally visible effect either way.
