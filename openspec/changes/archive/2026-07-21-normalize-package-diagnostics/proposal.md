## Why

Issue #358 (P1 regression, tracked under the #354 0.5.1 stabilization story) reports that package-contract
violations lose their evidence outside the checker/baseline path: `ArchitectureAnalysisSession` and the
baseline generator/comparer already carry the correct source project, forbidden package group and matched
`PackageReference` evidence, but `ArchitectureDiagnosticFormatter` (the shared Human/JSON adapter) has three
parallel `diagnostic switch` expressions — `SourceTypeOf`, `ForbiddenNamespaceOf`, `ForbiddenReferencesOf` —
that never learned about `PackageDependencyDiagnostic`. Every `strict_package_dependency`/`audit_package_dependency`
finding therefore renders `source ""`, `forbidden_namespace ""` and `forbidden_references []` in both human
text and unified JSON, while the SARIF formatter (which has its own, separately-maintained switch) already
renders it correctly. A single baseline entry proves the checker is right and only two of three public
adapters are wrong.

Separately, `CheckPackageAllowOnlyContract` never sets `ArchitectureViolation.Payload` at all, so
`package_allow_only` findings silently fall back to the generic `DependencyDiagnostic` mapping. That
happens to render non-empty text today (because `DependencyDiagnostic` is a handled case everywhere), but
it means package-allow-only findings have no distinct typed identity: SARIF's `LogicalLocationKindFor`
reports `"namespace"` instead of `"package"`, and nothing distinguishes an allow-only package finding from
an ordinary namespace/layer dependency finding in the diagnostic model itself. That is the same class of
silent-payload gap that just caused the `package_dependency` regression, in the sibling contract family.

Per the issue's priority clarification, this is scoped as an isolated, independently releasable P1 fix: it
makes the existing normalized package payload reach Human, JSON and SARIF identically, without adopting the
full discriminated diagnostic-detail architecture (typed condition/TFM/central-package-management/item-kind
evidence per declaration) that issue #373 owns separately as a P2 follow-on across every finding family.

## What Changes

- Add `PackageDependencyDiagnostic` to the three shared switch expressions in
  `ArchitectureDiagnosticFormatter` (`SourceTypeOf`, `ForbiddenNamespaceOf`, `ForbiddenReferencesOf`) so
  Human output and unified JSON render the same source project, forbidden-namespace display and forbidden
  package references that SARIF and the baseline path already carry.
- Add a `forbidden_package_group` field to the CI/unified JSON object for `PackageDependencyDiagnostic`
  (`ApplyDiagnosticSpecificCiFields`), so JSON consumers get the structured package-group identity without
  parsing the display-shaped `forbidden_namespace` string (`"package group '<name>'"`).
- Introduce a new `PackageAllowOnlyDiagnostic` record and matching `PackageAllowOnlyPayload`
  (`IArchitectureDiagnosticPayload`), mirroring the existing `ContextAllowOnlyDiagnostic`/`ContextAllowOnlyPayload`
  pattern, with its own `ArchitectureDiagnosticKind.PackageAllowOnly` value. Wire
  `CheckPackageAllowOnlyContract` to set this payload instead of leaving `Payload` unset.
- Extend all locations that dispatch on concrete diagnostic subtype to recognize
  `PackageAllowOnlyDiagnostic` alongside `PackageDependencyDiagnostic`: the three Human/JSON switches above,
  `ApplyDiagnosticSpecificCiFields` (new `allowed_package_groups` field), and the SARIF formatter's
  `ExtractFields`/`LogicalLocationKindFor` (so allow-only findings report `"package"` logical-location kind,
  not `"namespace"`).
- No change to violation identity, baseline matching, or JSON/SARIF schema versioning: the new JSON fields
  are additive, and `ArchitectureViolationIdentity.ResolveKind` already maps both `package_dependency` and
  `package_allow_only` contract families to the `"package"` identity kind independently of `Diagnostic.Kind`.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `package-dependency-contracts`: Human and JSON output now render the same source/forbidden-group/matched-package
  evidence as SARIF and baseline for `package_dependency` violations; JSON additionally exposes a structured
  `forbidden_package_group` field.
- `package-allow-only-contracts`: `package_allow_only` violations now map to their own typed
  `PackageAllowOnlyDiagnostic`/`Kind.PackageAllowOnly` instead of the generic dependency diagnostic, and SARIF
  reports their logical-location kind as `"package"`; JSON exposes a structured `allowed_package_groups` field.
- `diagnostics-model`: documents that every package-family diagnostic subtype (`PackageDependencyDiagnostic`,
  `PackageAllowOnlyDiagnostic`) must be present in every formatter dispatch point that switches on concrete
  `ArchitectureDiagnostic` subtypes, so the regression class (a new payload/subtype introduced without every
  adapter's switch being updated) is a specification-level requirement, not just an implementation detail.

## Impact

- `src/ArchLinterNet.Core/Reporting/ArchitectureDiagnosticFormatter.cs` (Human + unified JSON adapter).
- `src/ArchLinterNet.Core/Reporting/ArchitectureSarifFormatter.cs` (`ExtractFields`, `LogicalLocationKindFor`).
- `src/ArchLinterNet.Core/Model/PackageAllowOnlyDiagnostic.cs`, `Model/PackageAllowOnlyPayload.cs` (new),
  `Model/ArchitectureDiagnosticKind.cs`.
- `src/ArchLinterNet.Core/Execution/ArchitectureAnalysisSession.PackageDependency.cs`
  (`CheckPackageAllowOnlyContract`).
- `docs/ai/*.md` guidance mentioning package diagnostic JSON fields, if any.
- Test suites under `tests/ArchLinterNet.Core.Tests/` covering package dependency/allow-only formatting
  (Human, JSON, SARIF parity).

## Non-Goals

- Typed per-declaration evidence for item kind (direct/CPM/transitive/SDK-implicit), `Condition`, target
  framework/configuration context, or central-package-management provenance on the diagnostic payload — this
  requires new discovery-layer parsing (`ArchitectureDiscoveredPackageReference` today carries only
  `PackageId`/`Version`) and is explicitly owned by issue #373's broader discriminated diagnostic-detail
  model, not this isolated P1 fix.
- Any change to baseline identity, baseline schema version, or the public diagnostic JSON schema version —
  the new fields are additive and do not change existing field shapes or violation identity.
