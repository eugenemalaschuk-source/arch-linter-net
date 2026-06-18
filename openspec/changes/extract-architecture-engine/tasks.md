## 1. Project Setup

- [ ] 1.1 Update `src/ArchLinterNet.Core/ArchLinterNet.Core.csproj`: add `YamlDotNet.Serialization` and `Microsoft.CodeAnalysis.CSharp` NuGet packages, remove `Unity.Container`
- [ ] 1.2 Delete `src/ArchLinterNet.Core/ArchitectureCompositionRoot.cs` (Unity DI stub)
- [ ] 1.3 Delete `src/ArchLinterNet.Core/ServiceLocator.cs` (Unity DI stub)
- [ ] 1.4 Delete `src/ArchLinterNet.Core/IArchitectureValidator.cs` (no longer needed)
- [ ] 1.5 Create subdirectory structure under `src/ArchLinterNet.Core/`: `Contracts/`, `Execution/`, `Model/`, `Reporting/`, `Resolution/`, `Scanning/`

## 2. Contracts Layer

- [ ] 2.1 Copy `ArchitectureContractModels.cs` from First Ice into `src/ArchLinterNet.Core/Contracts/`, rename namespace to `ArchLinterNet.Core.Contracts`
- [ ] 2.2 Copy `ArchitectureContractLoader.cs` from First Ice into `src/ArchLinterNet.Core/Contracts/`, rename namespace to `ArchLinterNet.Core.Contracts`, update internal `using` to `ArchLinterNet.Core.Resolution`
- [ ] 2.3 Update `ArchLinterNet.Core.Tests/ArchLinterNet.Core.Tests.csproj` to add `YamlDotNet.Serialization` package reference

## 3. Model Layer

- [ ] 3.1 Copy `ArchitectureViolation.cs` from First Ice into `src/ArchLinterNet.Core/Model/`, rename namespace to `ArchLinterNet.Core.Model`

## 4. Resolution Layer

- [ ] 4.1 Copy `ArchitectureRepositoryRootLocator.cs` into `src/ArchLinterNet.Core/Resolution/`, rename namespace to `ArchLinterNet.Core.Resolution`
- [ ] 4.2 Copy `ArchitectureLayerResolver.cs` into `src/ArchLinterNet.Core/Resolution/`, rename namespace to `ArchLinterNet.Core.Resolution`, update `using` to `ArchLinterNet.Core.Contracts`
- [ ] 4.3 Copy `ArchitectureIgnoreMatcher.cs` into `src/ArchLinterNet.Core/Resolution/`, rename namespace to `ArchLinterNet.Core.Resolution`, update `using` to `ArchLinterNet.Core.Contracts`

## 5. Scanning Layer

- [ ] 5.1 Copy `ArchitectureTypeNames.cs` into `src/ArchLinterNet.Core/Scanning/`, rename namespace to `ArchLinterNet.Core.Scanning`
- [ ] 5.2 Copy `ArchitectureTypeScanner.cs` into `src/ArchLinterNet.Core/Scanning/`, rename namespace to `ArchLinterNet.Core.Scanning`, update `using` references
- [ ] 5.3 Copy `ArchitectureReferenceScanner.cs` into `src/ArchLinterNet.Core/Scanning/`, rename namespace to `ArchLinterNet.Core.Scanning`
- [ ] 5.4 Copy `ArchitectureCycleDetector.cs` into `src/ArchLinterNet.Core/Scanning/`, rename namespace to `ArchLinterNet.Core.Scanning`
- [ ] 5.5 Copy `ArchitectureForbiddenCallMatcher.cs` into `src/ArchLinterNet.Core/Scanning/`, rename namespace to `ArchLinterNet.Core.Scanning`, update `using` to `Microsoft.CodeAnalysis`
- [ ] 5.6 Copy `ArchitectureIlMethodBodyScanner.cs` into `src/ArchLinterNet.Core/Scanning/`, rename namespace to `ArchLinterNet.Core.Scanning`, update `using` references
- [ ] 5.7 Copy `ArchitectureAsmdefScanner.cs` into `src/ArchLinterNet.Core/Scanning/`, rename namespace to `ArchLinterNet.Core.Scanning`, generalize hardcoded `Assets/FirstIce` root to configurable parameter
- [ ] 5.8 Copy `ArchitectureSourceScanner.cs` into `src/ArchLinterNet.Core/Scanning/`, rename namespace to `ArchLinterNet.Core.Scanning`, generalize hardcoded source roots to configurable parameter

## 6. Execution Layer

- [ ] 6.1 Copy `ArchitectureAnalysisContext.cs` into `src/ArchLinterNet.Core/Execution/`, rename namespace to `ArchLinterNet.Core.Execution`
- [ ] 6.2 Copy `ArchitectureAssemblyResolver.cs` into `src/ArchLinterNet.Core/Execution/`, rename namespace to `ArchLinterNet.Core.Execution`, update `using` to `ArchLinterNet.Core.Contracts`
- [ ] 6.3 Copy `ArchitectureContractRunner.cs` into `src/ArchLinterNet.Core/Execution/`, rename namespace to `ArchLinterNet.Core.Execution`, update all `using` references

## 7. Reporting Layer

- [ ] 7.1 Copy `ArchitectureDiagnosticFormatter.cs` into `src/ArchLinterNet.Core/Reporting/`, rename namespace to `ArchLinterNet.Core.Reporting`, update `using` to `ArchLinterNet.Core.Model`

## 8. Wire Up Stubs

- [ ] 8.1 Rewrite `src/ArchLinterNet.Core/ArchitectureValidator.cs`: replace stub with real delegation to `ArchitectureContractLoader`, `ArchitectureAssemblyResolver`, and `ArchitectureContractRunner` — return `violations.Count == 0`
- [ ] 8.2 Rewrite `src/ArchLinterNet.Cli/Program.cs`: add `--policy`, `--mode strict|audit`, `--format human|json` argument parsing, wire to real engine, print formatted output
- [ ] 8.3 Rewrite `src/ArchLinterNet.Testing/Class1.cs`: wire `ArchitectureValidationBuilder.ValidateStrict()` to real engine execution, add `ArchitectureTestRuntime` helper class
- [ ] 8.4 Rewrite `src/ArchLinterNet.Unity/Class1.cs`: wire `AsmdefValidator` to real `ArchitectureAsmdefScanner`

## 9. Tests

- [ ] 9.1 Create `tests/ArchLinterNet.Core.Tests/ContractLoaderTests.cs`: test YAML loading from path, missing file error, minimal schema parsing
- [ ] 9.2 Create `tests/ArchLinterNet.Core.Tests/LayerResolverTests.cs`: test layer resolution, namespace matching, suffix matching, describe output, unknown layer error
- [ ] 9.3 Create `tests/ArchLinterNet.Core.Tests/IgnoreMatcherTests.cs`: test exact match, wildcard, double-star, single-char, no-match scenarios
- [ ] 9.4 Create `tests/ArchLinterNet.Core.Tests/CycleDetectorTests.cs`: test no cycles, simple cycle, complex graph, disconnected graph
- [ ] 9.5 Create `tests/ArchLinterNet.Core.Tests/ReferenceScannerTests.cs`: test type reference extraction, safe handling of missing assemblies
- [ ] 9.6 Create `tests/ArchLinterNet.Core.Tests/ForbiddenCallMatcherTests.cs`: test pattern normalization, exact/namespace/member matching
- [ ] 9.7 Delete `tests/ArchLinterNet.Core.Tests/UnitTest1.cs` (placeholder)
- [ ] 9.8 Update `tests/ArchLinterNet.Core.Tests/ArchLinterNet.Core.Tests.csproj` with necessary package references

## 10. Documentation & Samples

- [ ] 10.1 Copy and adapt First Ice `tools/docs/architecture_library/README.md` into `docs/README.md`, update namespace references and usage examples to `ArchLinterNet.*`
- [ ] 10.2 Create `samples/BasicCleanArchitecture/architecture/dependencies.arch.yml` with a minimal 2-layer example

## 11. Validation

- [ ] 11.1 Run `rtk make restore` to verify all NuGet packages resolve
- [ ] 11.2 Run `rtk dotnet build ArchLinterNet.slnx` to verify the solution compiles with no errors (TreatWarningsAsErrors is on)
- [ ] 11.3 Run `rtk make test` to verify all tests pass
- [ ] 11.4 Run `rtk make lint` to verify code quality checks pass
