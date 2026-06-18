## 1. Project Setup

- [x] 1.1 Update `src/ArchLinterNet.Core/ArchLinterNet.Core.csproj`: add `YamlDotNet.Serialization` and `Microsoft.CodeAnalysis.CSharp` NuGet packages, remove `Unity.Container`
- [x] 1.2 Delete `src/ArchLinterNet.Core/ArchitectureCompositionRoot.cs` (Unity DI stub)
- [x] 1.3 Delete `src/ArchLinterNet.Core/ServiceLocator.cs` (Unity DI stub)
- [x] 1.4 Delete `src/ArchLinterNet.Core/IArchitectureValidator.cs` (no longer needed)
- [x] 1.5 Create subdirectory structure under `src/ArchLinterNet.Core/`: `Contracts/`, `Execution/`, `Model/`, `Reporting/`, `Resolution/`, `Scanning/`

## 2. Contracts Layer

- [x] 2.1 Copy `ArchitectureContractModels.cs` from First Ice into `src/ArchLinterNet.Core/Contracts/`, rename namespace to `ArchLinterNet.Core.Contracts`
- [x] 2.2 Copy `ArchitectureContractLoader.cs` from First Ice into `src/ArchLinterNet.Core/Contracts/`, rename namespace to `ArchLinterNet.Core.Contracts`, update internal `using` to `ArchLinterNet.Core.Resolution`
- [x] 2.3 Update `ArchLinterNet.Core.Tests/ArchLinterNet.Core.Tests.csproj` to add `YamlDotNet.Serialization` package reference

## 3. Model Layer

- [x] 3.1 Copy `ArchitectureViolation.cs` from First Ice into `src/ArchLinterNet.Core/Model/`, rename namespace to `ArchLinterNet.Core.Model`

## 4. Resolution Layer

- [x] 4.1 Copy `ArchitectureRepositoryRootLocator.cs` into `src/ArchLinterNet.Core/Resolution/`, rename namespace to `ArchLinterNet.Core.Resolution`
- [x] 4.2 Copy `ArchitectureLayerResolver.cs` into `src/ArchLinterNet.Core/Resolution/`, rename namespace to `ArchLinterNet.Core.Resolution`, update `using` to `ArchLinterNet.Core.Contracts`
- [x] 4.3 Copy `ArchitectureIgnoreMatcher.cs` into `src/ArchLinterNet.Core/Resolution/`, rename namespace to `ArchLinterNet.Core.Resolution`, update `using` to `ArchLinterNet.Core.Contracts`

## 5. Scanning Layer

- [x] 5.1 Copy `ArchitectureTypeNames.cs` into `src/ArchLinterNet.Core/Scanning/`, rename namespace to `ArchLinterNet.Core.Scanning`
- [x] 5.2 Copy `ArchitectureTypeScanner.cs` into `src/ArchLinterNet.Core/Scanning/`, rename namespace to `ArchLinterNet.Core.Scanning`, update `using` references
- [x] 5.3 Copy `ArchitectureReferenceScanner.cs` into `src/ArchLinterNet.Core/Scanning/`, rename namespace to `ArchLinterNet.Core.Scanning`
- [x] 5.4 Copy `ArchitectureCycleDetector.cs` into `src/ArchLinterNet.Core/Scanning/`, rename namespace to `ArchLinterNet.Core.Scanning`
- [x] 5.5 Copy `ArchitectureForbiddenCallMatcher.cs` into `src/ArchLinterNet.Core/Scanning/`, rename namespace to `ArchLinterNet.Core.Scanning`, update `using` to `Microsoft.CodeAnalysis`
- [x] 5.6 Copy `ArchitectureIlMethodBodyScanner.cs` into `src/ArchLinterNet.Core/Scanning/`, rename namespace to `ArchLinterNet.Core.Scanning`, update `using` references
- [x] 5.7 Copy `ArchitectureAsmdefScanner.cs` into `src/ArchLinterNet.Core/Scanning/`, rename namespace to `ArchLinterNet.Core.Scanning`, generalize hardcoded `Assets/FirstIce` root to configurable parameter
- [x] 5.8 Copy `ArchitectureSourceScanner.cs` into `src/ArchLinterNet.Core/Scanning/`, rename namespace to `ArchLinterNet.Core.Scanning`, generalize hardcoded source roots to configurable parameter

## 6. Execution Layer

- [x] 6.1 Copy `ArchitectureAnalysisContext.cs` into `src/ArchLinterNet.Core/Execution/`, rename namespace to `ArchLinterNet.Core.Execution`
- [x] 6.2 Copy `ArchitectureAssemblyResolver.cs` into `src/ArchLinterNet.Core/Execution/`, rename namespace to `ArchLinterNet.Core.Execution`, update `using` to `ArchLinterNet.Core.Contracts`
- [x] 6.3 Copy `ArchitectureContractRunner.cs` into `src/ArchLinterNet.Core/Execution/`, rename namespace to `ArchLinterNet.Core.Execution`, update all `using` references

## 7. Reporting Layer

- [x] 7.1 Copy `ArchitectureDiagnosticFormatter.cs` into `src/ArchLinterNet.Core/Reporting/`, rename namespace to `ArchLinterNet.Core.Reporting`, update `using` to `ArchLinterNet.Core.Model`

## 8. Wire Up Stubs

- [x] 8.1 Rewrite `src/ArchLinterNet.Core/ArchitectureValidator.cs`: replace stub with real delegation to `ArchitectureContractLoader`, `ArchitectureAssemblyResolver`, and `ArchitectureContractRunner` — return `violations.Count == 0`
- [x] 8.2 Rewrite `src/ArchLinterNet.Cli/Program.cs`: add `--policy`, `--mode strict|audit`, `--format human|json` argument parsing, wire to real engine, print formatted output
- [x] 8.3 Rewrite `src/ArchLinterNet.Testing/Class1.cs`: wire `ArchitectureValidationBuilder.ValidateStrict()` to real engine execution, add `ArchitectureTestRuntime` helper class
- [x] 8.4 Rewrite `src/ArchLinterNet.Unity/Class1.cs`: wire `AsmdefValidator` to real `ArchitectureAsmdefScanner`

## 9. Tests

- [x] 9.1 Create `tests/ArchLinterNet.Core.Tests/ContractLoaderTests.cs`: test YAML loading from path, missing file error, minimal schema parsing
- [x] 9.2 Create `tests/ArchLinterNet.Core.Tests/LayerResolverTests.cs`: test layer resolution, namespace matching, suffix matching, describe output, unknown layer error
- [x] 9.3 Create `tests/ArchLinterNet.Core.Tests/IgnoreMatcherTests.cs`: test exact match, wildcard, double-star, single-char, no-match scenarios
- [x] 9.4 Create `tests/ArchLinterNet.Core.Tests/CycleDetectorTests.cs`: test no cycles, simple cycle, complex graph, disconnected graph
- [x] 9.5 Create `tests/ArchLinterNet.Core.Tests/ReferenceScannerTests.cs`: test type reference extraction, safe handling of missing assemblies
- [x] 9.6 Create `tests/ArchLinterNet.Core.Tests/ForbiddenCallMatcherTests.cs`: test pattern normalization, exact/namespace/member matching
- [x] 9.7 Delete `tests/ArchLinterNet.Core.Tests/UnitTest1.cs` (placeholder)
- [x] 9.8 Update `tests/ArchLinterNet.Core.Tests/ArchLinterNet.Core.Tests.csproj` with necessary package references

## 10. Documentation & Samples

- [x] 10.1 Copy and adapt First Ice `tools/docs/architecture_library/README.md` into `docs/README.md`, update namespace references and usage examples to `ArchLinterNet.*`
- [x] 10.2 Create `samples/BasicCleanArchitecture/architecture/dependencies.arch.yml` with a minimal 2-layer example

## 11. Validation

- [x] 11.1 Run `rtk make restore` to verify all NuGet packages resolve
- [x] 11.2 Run `rtk dotnet build ArchLinterNet.slnx` to verify the solution compiles with no errors (TreatWarningsAsErrors is on)
- [x] 11.3 Run `rtk make test` to verify all tests pass
- [x] 11.4 Run `rtk make lint` to verify code quality checks pass
