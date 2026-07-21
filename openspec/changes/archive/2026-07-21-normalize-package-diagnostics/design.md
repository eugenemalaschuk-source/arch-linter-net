## Context

`ArchitectureAnalysisSession.CheckPackageDependencyContract` (`Execution/ArchitectureAnalysisSession.PackageDependency.cs:51-59`)
builds each violation with `Payload = new PackageDependencyPayload(packageGroupName)`. `ArchitectureDiagnosticMapper.FromViolation`
dispatches through that payload to produce a `PackageDependencyDiagnostic` (`Model/PackageDependencyDiagnostic.cs`) carrying the
real `SourceType`, `ForbiddenNamespace` (`"package group '<name>'"`) and `ForbiddenReferences` (`PackageId@Version` strings).

Three independent adapters then need to read those fields back off the concrete diagnostic subtype, because
`ArchitectureDiagnostic` itself declares no shared `SourceType`/`ForbiddenNamespace`/`ForbiddenReferences` members —
each subtype declares its own, and every adapter pattern-matches on the concrete type:

- `ArchitectureSarifFormatter.ExtractFields` (line ~355) — **has** a `PackageDependencyDiagnostic` case.
- `ArchitectureSarifFormatter.LogicalLocationKindFor` (line ~340) — **has** a `PackageDependencyDiagnostic => "package"` case.
- `ArchitectureDiagnosticFormatter.SourceTypeOf` / `ForbiddenNamespaceOf` / `ForbiddenReferencesOf` (lines 246-301) — **missing**
  the `PackageDependencyDiagnostic` case in all three, falling through to `_ => string.Empty` / `Array.Empty<string>()`.

Both `FormatForHumans` and `ToCiJsonObject` in the same file consume those three helpers, so the same bug reaches both Human
text and unified JSON simultaneously — matching exactly the regression described in the issue.

`CheckPackageAllowOnlyContract` (`Execution/ArchitectureAnalysisSession.PackageDependency.cs:102-107`) never sets `Payload`,
so `ArchitectureDiagnosticMapper.FromViolation` falls through to its `else` branch and produces a plain `DependencyDiagnostic`.
That happens to render non-empty output today (`DependencyDiagnostic` is a handled case in every switch), but it is an
identity gap of the same shape: nothing distinguishes an allow-only package finding from an ordinary layer/dependency finding
at the diagnostic-model level, and SARIF's `LogicalLocationKindFor` reports `"namespace"` for it instead of `"package"`.

## Goals / Non-Goals

**Goals:**
- Make `PackageDependencyDiagnostic` a recognized case in every formatter dispatch point that pattern-matches on concrete
  `ArchitectureDiagnostic` subtypes (the three Human/JSON switches; the CI-specific-fields dispatcher already reachable
  via the `DependencyDiagnostic`/`ExternalDependencyDiagnostic` precedent).
- Give `package_allow_only` violations their own typed `PackageAllowOnlyDiagnostic`/`PackageAllowOnlyPayload`, matching the
  existing `ContextDependencyDiagnostic`/`ContextAllowOnlyDiagnostic` pairing convention already used for context contracts,
  and wire it into the same dispatch points (including SARIF).
- Prove parity: the same violation's Human line, JSON object and SARIF result carry equivalent source/forbidden-group/matched-package
  evidence, with no field falling back to empty/generic because of an adapter-specific gap.

**Non-Goals:**
- Item-kind (direct/CPM/transitive/SDK-implicit), `Condition`, TFM/configuration-context, or central-package-management
  provenance as typed fields — `ArchitectureDiscoveredPackageReference` (`Discovery/ProjectDiscoveryModels.cs:3`) carries only
  `PackageId`/`Version` today; adding those requires new project-file-parsing capability in
  `ArchitectureProjectFileParser`, which is out of scope for this isolated P1 regression fix and is explicitly owned by
  issue #373's broader discriminated diagnostic-detail model across every finding family.
- Malformed/unresolved project metadata handling — already covered by the existing `CheckConfiguration` requirement
  ("Package dependency/allow-only contracts require discoverable package metadata for their source" in
  `package-dependency-contracts/spec.md`), unchanged by this proposal.
- Any baseline identity, baseline schema, or public JSON/SARIF schema version change. New JSON fields are additive.

## Decisions

### 1. Extend the three shared Human/JSON switches, don't introduce a shared interface

`ArchitectureDiagnosticFormatter`'s `SourceTypeOf`/`ForbiddenNamespaceOf`/`ForbiddenReferencesOf` already enumerate 14
concrete subtypes rather than declaring a shared interface/base member for these three fields. Adding a `PackageDependencyDiagnostic
d => d.SourceType` (etc.) line to each switch follows the existing pattern exactly and is the minimal diff; introducing a
shared interface here would be an unrequested architecture change and duplicate the typed-detail work #373 already owns.

### 2. `PackageAllowOnlyDiagnostic` mirrors `ContextAllowOnlyDiagnostic`, not `PackageDependencyDiagnostic`

`ContextDependencyDiagnostic`/`ContextAllowOnlyDiagnostic` already establish the project's convention for a
dependency-contract-family with a deny-list and allow-list sibling: two distinct diagnostic subtypes, two distinct
`ArchitectureDiagnosticKind` values, both exposing the same `SourceType`/`ForbiddenNamespace`/`ForbiddenReferences` triple.
`PackageAllowOnlyDiagnostic` follows the same shape:

```csharp
public sealed record PackageAllowOnlyDiagnostic(
    string ContractName,
    string? ContractId,
    string SourceType,
    string ForbiddenNamespace,
    IReadOnlyCollection<string> ForbiddenReferences,
    IReadOnlyCollection<string> AllowedPackageGroups)
    : ArchitectureDiagnostic(ContractName, ContractId)
{
    public override ArchitectureDiagnosticKind Kind => ArchitectureDiagnosticKind.PackageAllowOnly;
}
```

`AllowedPackageGroups` carries `contract.Allowed` (the configured allow-list group names) — the allow-only counterpart to
`PackageDependencyDiagnostic.ForbiddenPackageGroup` — and is surfaced only as an additive CI JSON field
(`allowed_package_groups`), not folded into `ForbiddenNamespace`/`ForbiddenReferences` display text.

### 3. New `ArchitectureDiagnosticKind.PackageAllowOnly` value

Enum values are append-only in this codebase (`ContextAllowOnly`, `PortBoundary`, `LayoutConvention` were all added at the
end); `PackageAllowOnly` is added after the existing last value to avoid renumbering any serialized/compared kind.

### 4. JSON field names

`forbidden_package_group` (singular, matches `PackageDependencyDiagnostic.ForbiddenPackageGroup`) and
`allowed_package_groups` (plural, matches `contract.Allowed`/`PackageAllowOnlyDiagnostic.AllowedPackageGroups`) — named
after their source fields, consistent with the existing `forbidden_external_group` field for
`ExternalDependencyDiagnostic`.
