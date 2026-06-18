## Context

The `arch-linter-net` repository was scaffolded with a solution structure (`ArchLinterNet.slnx`), project references, build scripts, and architecture contracts — but all implementations are stubs. The real engine lives in `firstice/tools/Architecture.Library/` as 18 source files organized across 6 directories (`Contracts/`, `Execution/`, `Model/`, `Reporting/`, `Resolution/`, `Scanning/`).

The engine evaluates YAML-driven architecture contracts against .NET assemblies using reflection, Roslyn semantic analysis, and IL-level scanning. It currently uses `Unity.Container` for DI and has hardcoded First Ice paths in two scanners.

Stakeholders: Any .NET team wanting architecture governance without ArchUnitNET/NetArchTest — a YAML-policy-driven, contract-first approach.

## Goals / Non-Goals

**Goals:**
- Extract the complete engine into `ArchLinterNet.Core` with renamed namespaces.
- Remove Unity dependency from Core — make it a pure library.
- Generalize hardcoded First Ice paths in source/asmdef scanners.
- Wire up all stub implementations (Validator, CLI, Testing, Unity).
- Add unit tests covering core behaviors.
- Preserve existing strict/audit contract behavior exactly.

**Non-Goals:**
- Redesigning the YAML schema.
- Replacing the model with ArchUnitNET/NetArchTest.
- Adding graph UI, dashboards, or enterprise features.
- Consuming ArchLinterNet from First Ice in this task.
- Deleting `tools/Architecture.Library` from First Ice.
- Adding CI/CD pipelines (future task).

## Decisions

### D1: Remove Unity.Container from Core

**Choice**: Replace with a simple static `ArchitectureEngine` facade class.

**Rationale**: Core should have zero DI framework dependencies. The engine is a stateless library — callers construct `ArchitectureContractRunner` directly. A thin static helper (`ArchitectureEngine.FromRepositoryRoot()`) provides convenience without DI.

**Alternatives considered**:
- Keep Unity → rejected (couples Core to a specific DI container).
- Use `Microsoft.Extensions.DependencyInjection` → rejected (adds a dependency for no benefit in a library).
- Pure static APIs only → chosen (simplest, no DI needed).

### D2: Generalize source scanner roots

**Choice**: Add `string[] sourceRoots` parameter to `FindMethodBodyViolations()`. Default fallback scans `src/` directories.

**Rationale**: The hardcoded `["Assets/FirstIce/Scripts", "tests", "bdd"]` is First Ice-specific. Making it a parameter lets consumers specify their own source roots while maintaining backward compatibility via defaults.

### D3: Generalize asmdef scanner root

**Choice**: Add `string asmdefRoot` parameter to `FindAsmdefViolations()`. Default fallback scans `Assets/` recursively.

**Rationale**: The hardcoded `"Assets/FirstIce"` is Unity-specific. Making it configurable allows any Unity project to use it.

### D4: Test strategy — own assemblies as targets

**Choice**: Use the test project's own compiled assemblies as target assemblies for contract tests.

**Rationale**: The test project compiles to `ArchLinterNet.Core.Tests.dll` which is a loadable .NET assembly. We can define test contracts against its own namespace (`ArchLinterNet.Core`) and verify the engine correctly detects violations. This avoids needing external test assemblies.

### D5: File organization within Core

**Choice**: Mirror the source directory structure: `Contracts/`, `Execution/`, `Model/`, `Reporting/`, `Resolution/`, `Scanning/` subdirectories under `src/ArchLinterNet.Core/`.

**Rationale**: Preserves the original logical grouping. Consumers can reference `ArchLinterNet.Core.Contracts`, `ArchLinterNet.Core.Execution`, etc. The alternative (flat structure) would create 18+ files in one directory.

### D6: Keep `ArchitectureValidator` as thin wrapper

**Choice**: `ArchitectureValidator` becomes a convenience facade over the real engine, not the engine itself.

**Rationale**: The engine API (`ArchitectureContractRunner`) is the primary public surface. `ArchitectureValidator` provides a simple `bool Validate(string policyPath)` entry point for consumers who want one-liner validation.

## Risks / Trade-offs

- **[Roslyn version compatibility]** → The engine depends on `Microsoft.CodeAnalysis.CSharp`. Pin to a version compatible with `net10.0`. If version conflicts arise, use `<PackageVersion>` in `Directory.Packages.props`.
- **[IL scanning on non-full-framework]** → IL token resolution may behave differently on .NET Core vs .NET Framework. Mitigation: the engine already targets `netstandard2.1` in First Ice; moving to `net10.0` is safe.
- **[Test assembly availability]** → Some tests need loadable assemblies. Mitigation: compile test fixtures that define types in known namespaces, then reference them as target assemblies.
- **[Source scanner path generalization]** → Changing the parameter signature is a breaking change for any code calling the old overload. Mitigation: add new overload with `sourceRoots` parameter; keep old overload with `[Obsolete]` attribute pointing to new one.
