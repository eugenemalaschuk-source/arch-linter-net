## Why

The architecture validation engine (`Architecture.Library`) is coupled to the First Ice monorepo — hardcoded Unity paths, First Ice namespace prefixes, and embedded inside a Unity-specific project file. Extracting it into the public `arch-linter-net` repository makes it reusable across any .NET codebase (ASP.NET, workers, services) without First Ice internals. The extracted repo already has a skeleton project structure with stub implementations; this change fills them with the real engine code.

## What Changes

- Copy 18 source files from `firstice/tools/Architecture.Library/` into `arch-linter-net/src/ArchLinterNet.Core/`, organized into `Contracts/`, `Execution/`, `Model/`, `Reporting/`, `Resolution/`, `Scanning/` subdirectories.
- Rename all namespaces from `FirstIce.Architecture.Library.*` to `ArchLinterNet.Core.*`.
- **BREAKING**: Remove `Unity.Container` dependency from Core — replace with a lightweight static composition root. Consumers handle their own DI.
- Add `YamlDotNet.Serialization` and `Microsoft.CodeAnalysis.CSharp` NuGet dependencies to Core.
- Generalize `ArchitectureSourceScanner` source-root discovery (remove hardcoded `Assets/FirstIce/Scripts`).
- Generalize `ArchitectureAsmdefScanner` root path (make configurable).
- Wire `ArchitectureValidator` to delegate to the real `ArchitectureContractRunner` instead of returning `true`.
- Wire CLI to real contract loading, execution, and formatted output.
- Add unit tests for YAML loading, layer resolution, ignore matching, and cycle detection.
- Copy documentation from First Ice into `docs/`.
- Add a sample `architecture/dependencies.arch.yml` to `samples/BasicCleanArchitecture/`.

## Capabilities

### New Capabilities

- `yaml-contract-loading`: Load and deserialize `architecture/dependencies.arch.yml` into strongly-typed C# models via YamlDotNet.
- `assembly-resolution`: Resolve target assemblies from YAML configuration with a multi-probe-path strategy (env var, YAML paths, AppContext.BaseDirectory, repo root, artifacts/bin).
- `dependency-contracts`: Evaluate strict/audit dependency contracts — verify source layer types do not reference forbidden layer namespaces.
- `layer-contracts`: Evaluate inward-only layer ordering constraints across a sequenced layer list.
- `allow-only-contracts`: Evaluate whitelist-based dependency constraints — source may reference only explicitly allowed layers.
- `cycle-contracts`: Detect directed cycles among layer dependency graphs using DFS.
- `method-body-contracts`: Detect forbidden API calls in executable method bodies via Roslyn semantic analysis with IL token fallback scanning.
- `asmdef-contracts`: Validate Unity `.asmdef` assembly definition dependency boundaries (editor-only detection, forbidden prefix references).
- `independence-contracts`: Evaluate mutual separation across a set of layers — no cross-references in either direction.
- `violation-reporting`: Format violations and cycles for human-readable console output and machine-parseable CI JSON artifacts.
- `ignore-matching`: Match violation suppression rules using glob-like patterns (exact, wildcard `*`, double-star `**`, single-char `?`).

### Modified Capabilities

_No existing specs — this is a fresh extraction._

## Impact

- **Source files**: 18 `.cs` files added to `src/ArchLinterNet.Core/` across 6 subdirectories.
- **Dependencies added**: `YamlDotNet.Serialization`, `Microsoft.CodeAnalysis.CSharp` to Core project.
- **Dependencies removed**: `Unity.Container` removed from Core.
- **Deleted files**: `ArchitectureCompositionRoot.cs`, `ServiceLocator.cs`, `IArchitectureValidator.cs` (Unity DI stubs removed).
- **Test files**: ~6 new test files in `tests/ArchLinterNet.Core.Tests/`.
- **Docs**: New `docs/README.md` from First Ice extraction.
- **Samples**: New `samples/BasicCleanArchitecture/architecture/dependencies.arch.yml`.
- **CLI**: Rewired to use real engine instead of stub `IArchitectureValidator`.
- **Testing project**: Rewired to expose real contract execution helpers.
- **Unity project**: Rewired to real `ArchitectureAsmdefScanner`.
- **First Ice repo**: Untouched — no deletion, no modifications.
