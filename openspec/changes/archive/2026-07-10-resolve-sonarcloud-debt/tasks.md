## 1. CA1861 — Static readonly array fields (186 issues)

- [x] 1.1 Fix CA1861 in test files: `ArchitectureSarifFormatterTests.cs` (17), `ConditionSetConfigTests.cs` (16), `ArchitectureDiagnosticMapperTests.cs` (16)
- [x] 1.2 Fix CA1861 in test files: `ArchitectureProjectDiscoveryTests.cs` (15), `PolicyConsistencyCheckTests.cs` (11)
- [x] 1.3 Fix CA1861 in test files: `ForbiddenCallMatcherTests.cs` (9), `ArchitectureBaselineApplicationServiceFakeCompositionTests.cs` (8), `UnifiedJsonOutputTests.cs` (8)
- [x] 1.4 Fix CA1861 in remaining test files: `ArchitectureRunnerSetupServiceDiscoveryTests.cs`, `ArchitectureValidatorTests.cs`, `ProjectAssemblyCoverageContractTests.cs`, `LayerResolverTests.cs`, `ProtectedContractTests.EdgeCases.cs`, and 6 others (single-use arrays — cannot extract)
- [x] 1.5 Fix CA1861 in source files (Core, CLI, Unity — none found, all issues are in tests)
- [x] 1.6 Verify: `dotnet build --no-restore` passes (CA1861 not detectable locally — must verify via SonarCloud re-analysis)

## 2. CA1822 — Mark methods static (44 issues)

- [x] 2.1 Fix CA1822 in test fixtures: `CompositionContractTestFixtures.cs` (11), `PublicApiSurfaceContractTestFixtures.cs` (8)
- [x] 2.2 Fix CA1822 in remaining test files (~15 files, ~18 issues)
- [x] 2.3 Fix CA1822 in source files: `ArchitectureValidator.cs` (2), `ArchitectureBaselineLoadingService.cs` (2), `CliCompositionRoot.cs`, `AsmdefValidator.cs`, `PublicApiSurfaceChecker.cs`, `InheritanceChecker.cs`, `AssemblyIndependenceChecker.cs`
- [x] 2.4 Verify: `dotnet build --no-restore` passes

## 3. S8677 — PowerShell script issues (26 issues)

- [x] 3.1 Audit impacted scripts in `.github/workflows/` and `tools/scripts/`
- [x] 3.2 Fix S8677 findings in CI workflow files
- [x] 3.3 Fix S8677 findings in tooling scripts

## 4. S3267 — Simplify loops to LINQ (13 issues)

- [x] 4.1 Fix S3267 in `CoverageValidator.cs` and related validator files
- [x] 4.2 Fix S3267 in remaining files (CLI and Core)

## 5. CA1859 — Narrow return types (9 issues)

- [x] 5.1 Review and fix CA1859 findings across Core and CLI
- [x] 5.2 Verify no behavioral changes from type narrowing

## 6. S3011 — Reflection usage (8 issues)

- [x] 6.1 Audit each S3011: suppress intentional reflection with `// NOSONAR` + comment
- [x] 6.2 Refactor any accidental non-public access

## 7. S1192 — Repeated string literals (7 issues)

- [x] 7.1 Extract repeated literals into constants across Core and CLI

## 8. S107 — Too many parameters (6 issues)

- [x] 8.1 Fix `ArchitectureValidationResult` constructor (11 params): introduce parameter object
- [x] 8.2 Fix `ICliRuntime` method (8 params) and `ValidateCommandDefinition` (13 params): introduce parameter objects or suppress borderline cases

## 9. S1172 — Unused method parameters (6 issues)

- [x] 9.1 Remove unused params across Core and CLI files

## 10. S108 — Empty code blocks (6 issues)

- [x] 10.1 Fill or remove all empty code blocks (catch clauses, method bodies, etc.)

## 11. SYSLIB1045 — GeneratedRegex (5 issues)

- [x] 11.1 Replace `new Regex(...)` with `[GeneratedRegex]` on static Regex fields

## 12. S2325 — Make private/nested methods static (6 issues)

- [x] 12.1 Mark eligible private/nested methods as static across Core and CLI

## 13. Remaining small rules (15 issues)

- [x] 13.1 CA1806 (3): Use return values of method calls
- [x] 13.2 CA1865/CA1860/CA1866 (7): Use explicit string comparison overloads
- [x] 13.3 S127 (3): Fix loop variable mutation in loop body
- [x] 13.4 S3358 (2): Simplify nested ternary expressions
- [x] 13.5 S3220 (2): Fix ambiguous `params` overload calls
- [x] 13.6 S1144 (2): Remove unused private methods

## 14. Validation

- [x] 14.1 Run `rtk make acceptance` — full lint + test suite
- [x] 14.2 Run `dotnet build` — verify `TreatWarningsAsErrors` is clean
- [ ] 14.3 Trigger SonarCloud analysis on the merged PR — confirm zero new-code issues
