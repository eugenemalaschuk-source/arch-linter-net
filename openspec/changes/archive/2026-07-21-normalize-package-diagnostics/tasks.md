## 1. Human/JSON parity for `PackageDependencyDiagnostic`

- [x] 1.1 Add a `PackageDependencyDiagnostic d => d.SourceType` case to `ArchitectureDiagnosticFormatter.SourceTypeOf`.
- [x] 1.2 Add a `PackageDependencyDiagnostic d => d.ForbiddenNamespace` case to `ForbiddenNamespaceOf`.
- [x] 1.3 Add a `PackageDependencyDiagnostic d => d.ForbiddenReferences` case to `ForbiddenReferencesOf`.
- [x] 1.4 Add a `PackageDependencyDiagnostic` case to `ApplyDiagnosticSpecificCiFields` that sets
      `obj["forbidden_package_group"] = package.ForbiddenPackageGroup`.

## 2. Typed `PackageAllowOnlyDiagnostic`

- [x] 2.1 Add `ArchitectureDiagnosticKind.PackageAllowOnly` (appended after the last existing value).
- [x] 2.2 Add `Model/PackageAllowOnlyDiagnostic.cs` (mirrors `ContextAllowOnlyDiagnostic` shape; see design.md).
- [x] 2.3 Add `Model/PackageAllowOnlyPayload.cs` implementing `IArchitectureDiagnosticPayload`.
- [x] 2.4 Wire `CheckPackageAllowOnlyContract` (`Execution/ArchitectureAnalysisSession.PackageDependency.cs`) to set
      `Payload = new PackageAllowOnlyPayload(contract.Allowed.ToArray())` (or equivalent) on the violation it builds.
- [x] 2.5 Add `PackageAllowOnlyDiagnostic` to the same three Human/JSON switches from task 1.
- [x] 2.6 Add a `PackageAllowOnlyDiagnostic` case to `ApplyDiagnosticSpecificCiFields` setting
      `obj["allowed_package_groups"]`.

## 3. SARIF parity

- [x] 3.1 Add `PackageAllowOnlyDiagnostic` to `ArchitectureSarifFormatter.ExtractFields`.
- [x] 3.2 Add `PackageAllowOnlyDiagnostic => "package"` to `ArchitectureSarifFormatter.LogicalLocationKindFor`.

## 4. Tests

- [x] 4.1 Add/extend `ArchitectureDiagnosticFormatter` tests proving `FormatViolationsForHumans` and the CI JSON object
      for a `package_dependency` violation carry non-empty source/forbidden-namespace/forbidden-references matching the
      underlying violation (regression test for the exact reported bug).
- [x] 4.2 Add the same non-empty/parity assertions for a `package_allow_only` violation, plus asserting its `Kind ==
      PackageAllowOnly` and JSON `allowed_package_groups`.
- [x] 4.3 Add/extend `ArchitectureSarifFormatterTests` asserting `package_allow_only` results use logical-location kind
      `"package"`.
- [x] 4.4 Add a parity test that runs the same violation through Human, JSON and SARIF formatters and asserts the
      source project, forbidden package group/allowed groups, and matched package references are equivalent across
      all three (per issue acceptance criterion).
- [x] 4.5 Run `rtk dotnet test tests/ArchLinterNet.Core.Tests --no-restore` and confirm the full package-family suite
      (`PackageDependencyContractTests`, `PackageAllowOnlyContractTests`, formatter/SARIF tests) passes.

## 5. Docs

- [x] 5.1 Grep `docs/ai/*.md` and `docs/guides/*.md` for existing package-diagnostic JSON field documentation; update
      if any existing doc enumerates unified-JSON fields for package contracts, to include `forbidden_package_group`/
      `allowed_package_groups`.

## 6. Spec sync and archive

- [x] 6.1 Verify implementation matches the delta spec scenarios below.
- [x] 6.2 Run `openspec validate --all`.
- [x] 6.3 Run `openspec archive normalize-package-diagnostics`.
