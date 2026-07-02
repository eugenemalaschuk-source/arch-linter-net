## Context

`ArchitectureContractLoader`, `ArchitectureBaselineLoader`, `ArchitectureProjectDiscovery`, `ArchitectureSourceScanner`, `ArchitectureAsmdefScanner`, `ArchitectureAssemblyResolver`, and `ArchitectureRepositoryRootLocator` are all `static` classes that call `File`/`Directory`/`Environment`/`AppContext`/`CSharpCompilation`/`Assembly` directly. #136 wrapped several of them in DI-registered instance services (`ArchitecturePolicyDocumentLoader`, `ArchitectureBaselineLoadingService`, `ArchitectureProjectDiscoveryService`, `ArchitectureAssemblyResolutionService`, `ArchitectureRepositoryRootResolver`), but each wrapper is a thin pass-through to the static class — the interface controls *whether the step runs*, not the IO it performs underneath. `ArchitectureRunnerSetupServiceFakeDependencyTests` demonstrates this: it fakes the whole `IArchitectureProjectDiscoveryService`, never exercising the real discovery logic at all.

~30 existing test files call these static classes' public methods directly by path or `ArchitectureContractDocument` (e.g. `ArchitectureContractLoader.LoadFromPath(path)`, `ArchitectureProjectDiscovery.ResolveFromDocument(document, root)`), and the issue's non-goals rule out rewriting them.

## Goals / Non-Goals

**Goals:**
- Give file system, environment, Roslyn compilation, and assembly loading/probing each a narrow interface + real default implementation, registered in the composition root.
- Make the DI-registered wrapper services (`ArchitecturePolicyDocumentLoader`, etc.) depend on these seams through their constructors and pass them down explicitly, so container-composed code can have its IO faked without faking the whole service.
- Preserve every existing public static method signature and behavior on the static classes so the ~30 direct-call test files and any other production call sites keep compiling and passing unchanged.
- Prove the seam is real by driving one genuinely IO-heavy path (`ArchitectureProjectDiscoveryService`) through a fake file system instead of the real disk.

**Non-Goals:**
- Converting the static classes into instance services themselves, or deleting them — only #142-adjacent future work would consider that, and doing it here would force rewriting the ~30 tests that call them directly, which the issue explicitly rules out.
- A true clock/`DateTime.Now` seam — audited and confirmed the only "clock" use in Core is `File.GetLastWriteTimeUtc` comparisons for build-output staleness, which is file-system IO, not wall-clock time. No `IArchitectureClock` is introduced.
- Namespace reorganization into `*.Abstractions` (issue #155's concern, not #139's).
- Any change to YAML schema, diagnostics, discovery semantics, or CLI-visible behavior.
- Performance work itself — #19 remains the umbrella for that; this change only avoids closing off future options (e.g. a caching `IArchitectureFileSystem` decorator could be swapped in later without touching call sites again).

## Decisions

**Optional trailing seam parameters on the existing static methods, defaulting to a shared real singleton, rather than converting the static classes to instance services.** E.g. `ArchitectureContractLoader.LoadFromPath(string contractPath, IArchitectureFileSystem? fileSystem = null)` with `fileSystem ??= ArchitectureFileSystem.Real` at the top. This is the only option that satisfies both "every existing call site keeps compiling and behaving identically" and "the DI-composed path can inject a fake" simultaneously, without a mechanical rewrite of ~30 test files. The alternative — converting each static class into a constructor-injected instance service, the same move #136 made for the setup pipeline — was considered and rejected here specifically because those call sites are the direct *unit-under-test* API for dozens of existing contract-family tests (e.g. `ArchitectureSourceScannerTests` calling `ArchitectureSourceScanner.FindMethodBodyViolations(...)` directly), not orchestration glue like `ArchitectureRunnerFactory` was; rewriting them to construct an instance and inject fakes everywhere is exactly the "rewriting all tests" the issue lists as a non-goal.

**One real singleton instance per seam interface, referenced via a static `Real`/similar field on the default implementation, rather than `new`-ing a fresh instance per default-parameter evaluation.** These implementations are stateless wrappers over BCL static calls, so a shared instance is safe and avoids an allocation on every call that doesn't pass an explicit seam.

**Four seam interfaces, not one big "IRuntime" interface.** `IArchitectureFileSystem`, `IArchitectureEnvironment`, `IArchitectureAssemblyLoader`, and `IRoslynCompilationFactory` are faked independently in different tests (a project-discovery test only needs a fake file system; an assembly-resolution test needs a fake loader plus environment). A single combined interface would force every fake to implement unrelated methods it doesn't exercise.

**`IArchitectureFileSystem` covers `GetLastWriteTimeUtc` — no separate clock abstraction.** The issue's "clock/time access for stale-output checks" is entirely `File.GetLastWriteTimeUtc` comparisons in `ArchitectureProjectDiscovery`; folding it into the file-system seam avoids introducing an unused `IArchitectureClock` for a scenario that doesn't exist in this codebase.

**Wrapper services get the seam constructor-injected; the static classes stay the extension point for direct callers.** `ArchitecturePolicyDocumentLoader.Load(string policyPath)` (already DI-registered) takes `IArchitectureFileSystem` via constructor and passes it into `ArchitectureContractLoader.LoadFromPath(policyPath, _fileSystem)`. Direct static callers (existing tests) get the real singleton by omitting the parameter. This means the container path and the direct-static path both go through the exact same method, so there's no behavior divergence between them — only whether the seam is the real singleton or an injected fake.

**`ArchitectureSourceScanner` and `ArchitectureAsmdefScanner` stay `internal`.** They already are (no public API change needed) and are only reached through contract handlers, so their seam parameters are added the same way without any accessibility change.

## Risks / Trade-offs

- [Optional trailing parameters on ~7 static classes touch many call sites across production code] → Each parameter is purely additive (nullable, defaulted); no existing call site needs to change. Verified by running the full existing test suite unchanged.
- [Real-singleton pattern could hide accidental shared mutable state if a future implementation isn't stateless] → All four real implementations are documented as stateless pass-throughs to BCL calls; if a future change needs per-run state (e.g. a caching decorator), it should be registered as a scoped/transient DI instance instead of extending the shared singleton.
- [Assembly loading in `ArchitectureAssemblyResolver` mixes probing (file/dir exists) and loading (`Assembly.Load`/`LoadFrom`) — two different seams in one class] → Inject both `IArchitectureFileSystem`/`IArchitectureEnvironment` and `IArchitectureAssemblyLoader` into the static method's parameter list; each fake test only needs to supply the ones it exercises, defaults cover the rest.
- [Roslyn compilation seam only covers the method-body contract path, not all Roslyn-touching code] → Confirmed by the earlier audit this is the only Roslyn compilation site in Core; scope matches the issue's "Roslyn compilation creation" bullet exactly.

## Migration Plan

Single behavior-preserving PR, no data migration. Land on the feature branch, verify via `make fmt` and `make acceptance` (full existing suite unchanged plus new seam/fake tests), then merge. No rollback concerns beyond a normal revert — no persisted state or schema changes are involved.
