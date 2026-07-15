## Why

`ArchLinterNet.CEL` exists as an empty assembly stub from #323. Before any parser, type-checker, or evaluator task (#325–#329) touches a single line of engine code, the normative language profile and the **exact public API surface** must be locked down — so every downstream task builds against a stable, reviewed contract rather than retrofitting incompatible designs mid-implementation. Additionally, the required internal architecture blueprint must exist before #325 begins, per the acceptance criteria of #324.

## What Changes

- New normative spec `cel-profile-v1` defining the ArchLinter CEL Profile v1: profile identity, pinned CEL spec baseline, supported subset (types/operators/functions), unsupported/deferred features, semantic/limit/diagnostic/cache behaviors, exact public type and member names, and an extension-direction traceability matrix.
- New public API stubs in `src/ArchLinterNet.CEL/` — all types in the flat `ArchLinterNet.CEL` namespace, with XML doc comments declaring immutability and thread-safety:
  - `CelProfile` / `CelProfileId`
  - `CelType` (static factory methods: Bool, String, Int, Float, ListOf, MapOf, ObjectOf)
  - `CelVariable` (typed variable handle)
  - `CelContextSchema` / `CelContextSchemaBuilder`
  - `CelEnvironment` / `CelEnvironmentBuilder`
  - `CelCompilationResult<T>` / `CelCompiledPredicate` / `CelCompiledExpression`
  - `CelEvaluationContext` / `CelEvaluationContextBuilder`
  - `CelEvaluationResult`
  - `CelValue` / `CelObjectValue` (immutable, no CLR reflection)
  - `CelDiagnostic` / `CelDiagnosticCode` / `CelSourceSpan`
  - `CelCompilationLimits` / `CelEvaluationLimits`
  - `CelCompilationKey`
- `CelEngine` demoted from `public` to `internal` (implementation detail, not public contract).
- New `docs/internal/cel-engine-architecture.md` with processing pipeline, component ownership table, and extension-direction matrix (all 7 required directions from the issue).
- `docs/internal/README.md` updated with link to the new blueprint.
- New test files in `tests/ArchLinterNet.CEL.Tests/`:
  - `CelApiShapeTests.cs` — compile-and-pass tests verifying builder chains and result type properties
  - `CelExternalConsumerSampleTests.cs` — zero `ArchLinterNet.Core` references, shows the documented happy path

## Capabilities

### New Capabilities

- `cel-profile-v1`: Normative ArchLinter CEL Profile v1 definition, public engine API surface, and extension-direction traceability. Governs every subsequent CEL engine implementation task (#325–#329) and Core integration (#163).

### Modified Capabilities

- `cel-project-boundary`: No requirement changes. `CelEngine` visibility change is an internal detail; the boundary contracts (no reverse deps, no Core references) are unaffected.

## Impact

- **`src/ArchLinterNet.CEL/`**: ~18 new public type files + 1 changed visibility on `CelEngine`.
- **`tests/ArchLinterNet.CEL.Tests/`**: 2 new test files.
- **`docs/internal/`**: 1 new blueprint file + 1 updated README.
- **`openspec/specs/cel-profile-v1/`**: New spec (created by archiving this change).
- **Downstream tasks #325–#330**: All implement against the public API locked here.
- **Issue #162 (policy expression model)**: Design can now reference exact type names from this spec.
- **Issue #163 (Core integration)**: Can now build `CelContextSchema` instances using public API only.
- No runtime behavior change — `CelEngine` internals remain unimplemented; stubs return structured "not yet implemented" results.
