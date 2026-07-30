## Context

The repository already has focused tests for imports, baseline identity, selectors, diagnostics, FrameworkReference contracts, composition roots, build-state preflight, snapshots, report routing, and the Testing API. Those tests use separate local fixtures and do not provide a stable adopter-shaped inventory for profiling (#374), consistency review (#411), or final acceptance (#366).

## Goals / Non-Goals

**Goals:**

- Define a small, synthetic fixture corpus that each later acceptance task can reuse.
- Keep a machine-readable manifest as the single inventory of fixture ownership, required feature slices, expected entrypoints, and scenario status.
- Make the Checkpoint A runner a normal NUnit test fixture so it is executable through the existing test toolchain.
- Store only observed local macOS x86_64 evidence and distinguish it from broader support claims.

**Non-Goals:**

- Implement caching, profiling, parallelism, cancellation, packaged-artifact installation, or the final support matrix.
- Create a second validation engine, test framework, fixture DSL, report serializer, or release gate.
- Publish packages or authorize a release.

## Decisions

### Keep fixtures as synthetic test-project source trees

The corpus will live in a dedicated NUnit test project directory beside existing integration tests. Fixtures will be generated into isolated temporary directories and compiled with the installed .NET SDK only where a scenario requires real project artifacts. This exercises adopter inputs without introducing checked-in binary state. A handwritten fixture catalog is preferred over a generic fixture framework because the five required shapes are fixed and later tasks need recognizable paths to extend.

### Use a JSON manifest and Markdown inventory/evidence

The manifest is machine-readable and asserted by tests. It identifies each scenario, fixture shape, owners, required scenario slices, entrypoints, report projections, and checkpoint classification. Markdown documents explain fixture ownership and record an observed evidence run. JSON is selected over YAML to use the platform JSON reader and avoid treating policy parsing as part of the manifest test.

### Reuse public and existing seams

CLI tests will invoke the built CLI through `dotnet run --no-build`, as existing CLI integration tests do. Testing API scenarios will use `ArchitectureValidationBuilder` and its snapshot session. Policy, report, and identity assertions will consume the existing CLI/API outputs rather than recreating finding semantics.

### Checkpoint scope is explicitly non-release

The runner and evidence name Checkpoint A only. The manifest contains `release_gate: false`; the evidence document states that the local platform evidence does not create a platform-support or package-publication claim.

## Risks / Trade-offs

- [Real fixture builds increase test time] → build only the fixture shapes whose assertions need artifacts and keep projects small.
- [A local machine cannot substantiate all platforms] → record only observed macOS x86_64 evidence and leave expanded support validation to #366.
- [Existing feature behavior has narrow boundaries] → scenario records identify the implemented owner slices and unimplemented future work is excluded from the runner.
- [Manifest drifts from tests] → assert catalog IDs, required fixture classes, and non-release metadata in the executable entrypoint.

## Migration Plan

No user migration is required. The new test corpus and internal documents are additive. Later issues extend the same manifest and fixtures rather than replacing them.

## Open Questions

None. Later profiling and release-gate issues own their additional matrix dimensions and are intentionally not predesigned here.
