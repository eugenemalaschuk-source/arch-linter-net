## Why

Issue #159 is the direct follow-up to #154/#158: the static-class inventory in `docs/internal/static-class-inventory.md` still lists 12 behavior-owning production `static class` declarations (2 with hidden `static Lazy<T>` global state) as "follow-up candidates." Until they convert, #154's architecture target — "no behavior-owning Core service remains static unless explicitly reviewed as a compatibility facade or pure helper" — is unmet, and #142's guardrail work has no enforceable baseline to police against.

## What Changes

- Convert all 12 remaining production-service static classes in section (e) of the inventory into instance classes behind focused interfaces, registered `AddSingleton` in `AddArchLinterNetCore()`:
  - `ArchitectureContractLoader`, `ArchitectureRepositoryRootLocator` (removes `static Lazy<T>` hidden global state)
  - `ArchitectureProjectDiscovery`, `ArchitectureAssemblyResolver`, `ArchitectureSolutionParser`, `ArchitectureProjectFileParser`
  - `ArchitectureBaselineLoader`, `ArchitectureBaselineMerger`, `ArchitectureBaselineGenerator`
  - `ArchitectureDiagnosticFormatter`
  - `ArchitectureAsmdefScanner`, `ArchitectureSourceScanner`, `ArchitectureExternalDependencyIlScanner`, `ArchitectureIlMethodBodyScanner`
- **BREAKING (internal API only)**: where a shadow wrapper already exists (`ArchitecturePolicyDocumentLoader` → `ArchitectureContractLoader`, `ArchitectureRepositoryRootResolver` → `ArchitectureRepositoryRootLocator`, `ArchitectureProjectDiscoveryService` → `ArchitectureProjectDiscovery`, `ArchitectureAssemblyResolutionService` → `ArchitectureAssemblyResolver`), the wrapper absorbs the real logic and the static class is deleted rather than kept as a redundant forwarding layer. Static classes with no existing wrapper get a new interface + DI registration.
- Per-run domain objects that are not themselves DI-composed (`ArchitectureAnalysisSession`, its scanner call sites) keep their existing public constructors; they consume the converted scanners/parsers by direct instantiation of the stateless instance class rather than by DI injection, preserving the `core-composition-root` spec rule that only `ArchLinterNet.Core.Composition` types touch container APIs.
- Update `docs/internal/static-class-inventory.md` to reflect the converted/removed classes.

## Capabilities

### New Capabilities

(none — this extends existing capabilities, no new user-facing behavior)

### Modified Capabilities

- `static-production-service-inventory`: extends the "production orchestrators are instance-based" requirement to the remaining 12 classes, and adds a requirement that hidden `static Lazy<T>` singleton state is removed from production code.
- `project-discovery`: `ArchitectureProjectDiscovery`'s static entry point is replaced by `IArchitectureProjectDiscoveryService`, which now owns the resolution logic directly instead of forwarding to a static class.
- `assembly-resolution`: `ArchitectureAssemblyResolver`'s static entry point is replaced by `IArchitectureAssemblyResolutionService`, which now owns the resolution logic directly.
- `yaml-contract-loading`: `ArchitectureContractLoader`'s static entry point (including its cached-document singleton behavior) is replaced by `IArchitecturePolicyDocumentLoader` owning load/parse/validate logic directly.

## Impact

- `src/ArchLinterNet.Core/Contracts/`, `Discovery/`, `Execution/`, `Reporting/`, `Resolution/`, `Scanning/`: 12 classes converted, 4 shadow-wrapper classes absorb the converted logic and delete their forwarded static class.
- `src/ArchLinterNet.Core/Composition/ServiceCollectionExtensions.cs`: new `AddSingleton` registrations for classes without an existing wrapper.
- `src/ArchLinterNet.Core/Execution/ArchitectureAnalysisSession*.cs`: scanner call sites switch from static method calls to instance calls on a locally-constructed instance; no constructor/public-API signature change.
- `src/ArchLinterNet.Unity/**` (`AsmdefValidator.cs`): call sites for `ArchitectureContractLoader`/`ArchitectureRepositoryRootLocator`/`ArchitectureAsmdefScanner` switch to the DI-composed or directly-instantiated instance equivalents.
- `tests/ArchLinterNet.Core.Tests/**`: ~120 test call sites across the 12 classes update from static calls to instance calls (via direct `new` or DI-composed fakes, matching the existing `ArchitectureContractExecutor` test pattern).
- `docs/internal/static-class-inventory.md`: updated to move all 12 from "follow-up candidate" to "converted."
