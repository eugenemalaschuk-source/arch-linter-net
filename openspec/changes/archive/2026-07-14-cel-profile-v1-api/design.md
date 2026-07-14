## Context

`ArchLinterNet.CEL` was established by #323 as an empty, independently-buildable assembly. The parent story #322 plans a sequence of implementation tasks (#325 parser, #326 type system, #327 evaluator, #328 conformance, #329 performance) that will progressively fill the engine. Each of those tasks must build against a stable public API contract — otherwise they will diverge and collide.

The CEL language has a normative specification at `https://github.com/cel-expr/cel-spec/blob/master/doc/langdef.md`. Several C# and JVM ports exist (cel.net, cel-java, cel-go, TELUS cel-net, plaisted/cel-compiled, hbjydev/celdotnet). The issue mandates a comparative review and documents which patterns are adopted versus excluded.

`CelEngine` currently exists as a `public sealed class` with no members, flagged as a placeholder. It must be demoted to `internal` because it is an engine-implementation detail; the consumer-facing entry point is `CelEnvironment`.

## Goals / Non-Goals

**Goals:**

- Define every public type, method, and property name that will constitute the `ArchLinterNet.CEL` public API for all subsequent implementation tasks.
- Produce a normative ArchLinter CEL Profile v1 spec that pins the supported language subset.
- Produce the mandatory `docs/internal/cel-engine-architecture.md` blueprint before #325 begins.
- Ensure compile-and-pass API shape tests verify the contract at the time of merge.
- Ensure zero Core references can appear in external consumer code.

**Non-Goals:**

- Implementing the tokenizer, parser, AST, type checker, function catalog, evaluator, or any runtime behavior.
- Selecting or wrapping a third-party CEL runtime.
- Full CEL specification conformance.
- User-defined or host-registered functions in Profile v1.
- Public AST / tooling / serialization APIs.
- Built-in cache implementations.
- Arithmetic, conditional expressions, timestamps, durations, protobuf, bytes, uint, null/optional, macros, comprehensions, regex, or unknown/partial evaluation in Profile v1.

## Decisions

### D1: Logical file subdirectories with public sub-namespaces

Source files are organized into themed subdirectories (`Profile/`, `Schema/`, `Values/`,
`Compilation/`, `Evaluation/`, `Diagnostics/`) with matching public sub-namespaces
(`ArchLinterNet.CEL.Schema`, `ArchLinterNet.CEL.Compilation`, etc.).
`CelEnvironment` and `CelEnvironmentBuilder` remain in the root `ArchLinterNet.CEL` namespace
as the primary consumer entry points.

**Why**: The original decision (flat `ArchLinterNet.CEL`) was made before implementation. After
creating all 22 types, a flat root with no grouping was untenable: types from unrelated concerns
(diagnostics, compilation limits, schema builders) lived side-by-side with no navigational
structure. The code review for PR #334 identified that the design and the implemented namespace
structure were divergent. The sub-namespace organization was accepted as the correct trade-off
after the full type set was known.

**Impact on consumers**: Consumers using auto-import or IDE tooling are unaffected. Those writing
explicit `using` directives need `using ArchLinterNet.CEL.Schema;` etc. in addition to
`using ArchLinterNet.CEL;`. The `CelEnvironment.CreateBuilder` entry point stays in the
root namespace, so the primary happy-path builder chain requires only `using ArchLinterNet.CEL;`.

**Alternative considered**: Keep flat `ArchLinterNet.CEL` with all 22 types at root.
Rejected post-implementation: the root directory becomes hard to navigate, and grouping into
sub-namespaces matches how similar APIs (e.g., `Microsoft.CodeAnalysis`) are organized.

### D2: `CelEngine` demoted to `internal`

`CelEngine` (introduced in #323 as a public placeholder) becomes `internal sealed class CelEngine`.

**Why**: The public entry point for consumers is `CelEnvironment`. `CelEngine` is the internal evaluation engine that `CelEnvironment` (and eventually `CelCompiledPredicate`) will delegate to. Exposing it as public would create two entry points and contradict "no public implementation AST / internals."

**Alternative considered**: Delete `CelEngine` now. Rejected: the class carries the SonarCloud suppression comment documenting why it is empty; future tasks will implement it as the internal engine. Keeping it internal avoids re-creation churn.

### D3: `CelVariable` as a typed handle (not a string)

`CelVariable` is a sealed, immutable handle object returned by `CelContextSchemaBuilder.AddVariable()`. Callers set context values via `builder.Set(variable, value)` rather than `builder.Set("name", value)`.

**Why**: The issue explicitly requires "stable variable/member handles should avoid repeated string lookup in high-volume paths." A handle object supports O(1) slot assignment by index at evaluation time (once the evaluator is implemented), without exposing the slot index in the public API.

**Alternative considered**: String-keyed `Set("name", value)`. Rejected: requires string hash lookup per evaluation, harms high-volume policy checking, and gives no compile-time safety on variable names.

### D4: `CompilePredicate` returns a structured result with a `NotYetImplemented` diagnostic

`CelEnvironment.CompilePredicate(string source)` returns `CelCompilationResult<CelCompiledPredicate>` with `IsSuccess = false` and one diagnostic with `Code = CelDiagnosticCode.NotYetImplemented` until the parser is implemented in #325.

**Why**: Tests must pass under `make acceptance`. A `throw NotImplementedException()` would fail all shape tests. A structured "not yet implemented" result is the honest representation: the API exists, user failures produce structured diagnostics, programmer misuse produces exceptions. This is also consistent with the stated contract: "expected CEL failures use structured diagnostics/results."

**Alternative considered**: Return a stub success result with a placeholder compiled predicate that always evaluates to `false`. Rejected: that would make a failing expression look like a successful compilation, which could mislead downstream tests as they are added.

### D5: `CelValue` as a sealed factory class with typed accessors

`CelValue` is a sealed class with static factory methods (`CelValue.Bool(bool)`, `CelValue.String(string)`, etc.) and typed accessor properties (`AsBool()`, `AsString()`, etc.) that throw `InvalidOperationException` on type mismatch.

**Why**: Avoids `object` boxing and dynamic dispatch. Consumers get IntelliSense on the exact type they're working with. This is how cel-go's `ref.Val` works conceptually, but without exposing a public interface hierarchy that becomes part of the contract.

**Alternative considered**: Discriminated union via abstract base + derived types. Rejected: C# discriminated unions require additional library dependencies or awkward patterns; sealed factory with `CelValueKind` property is idiomatic and avoids the fragile-base-class problem.

### D6: Immutability and thread-safety declared in XML doc, not enforced by type system

`CelEnvironment`, `CelCompiledPredicate`, `CelCompiledExpression`, `CelContextSchema`, and `CelValue` are documented as immutable and thread-safe in their XML summary comments. Enforcement is structural (no setters, all fields `readonly` or `init`), not via a dedicated `IImmutable` marker interface.

**Why**: A marker interface creates a public contract that future subtypes (if ever introduced) must honor — adding complexity for no consumer benefit at this stage. The immutability guarantee is documented where it matters: the XML doc shown by IDEs.

### D7: Internal blueprint excluded from MkDocs navigation

`docs/internal/cel-engine-architecture.md` is added to `docs/internal/` and linked only from `docs/internal/README.md`. No `mkdocs.yml` entry is added.

**Why**: The issue and `docs/internal/README.md` both document that internal docs must not appear in the published site navigation. The blueprint is for maintainers/AI agents, not end users.

### D8: Single OpenSpec capability `cel-profile-v1`

The entire deliverable maps to one new OpenSpec capability. No modification to `cel-project-boundary` is needed.

**Why**: The project-boundary spec governs assembly structure (already complete). The profile spec governs language subset, API surface, and extension directions — a distinct, additive concern.

## Risks / Trade-offs

- **[Risk] API names locked before language implementation** → Names chosen here become the contract for #325–#329. Mitigated by: the issue provides explicit names; the shape tests will catch regressions as tasks are implemented.
- **[Risk] Stub `NotYetImplemented` diagnostic propagates to Core** → When #163 integrates, it will see `IsSuccess = false` for every compile attempt until #325 ships. Mitigated by: #163 depends on #322 and #163 knows it cannot test live evaluation before the parser exists.
- **[Risk] `CelEngine` internal change breaks `InternalsVisibleTo`** → `ArchLinterNet.CEL.Tests` has `InternalsVisibleTo` from the CEL project. The test project can access `CelEngine` (internal). The issue acceptance criterion "Verify no `InternalsVisibleTo` is needed by Core" is satisfied since Core only uses the public API. Mitigated by: shape tests include a build-time assertion that no Core types appear in the test's using directives.
- **[Risk] Extension-direction matrix becomes stale** → The blueprint documents intent, not commitment. Mitigated by: each subsequent task (#325–#330) is required by the issue to maintain the blueprint; #330 reconciles/finalizes it against shipped code.
