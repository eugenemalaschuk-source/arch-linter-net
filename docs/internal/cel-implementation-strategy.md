# CEL Implementation Strategy (#166)

This is internal project documentation for maintaining the `arch-linter-net` repository. It is intentionally excluded from the public MkDocs/GitHub Pages product site.

This document records the selected .NET CEL implementation strategy, rejected alternatives, risks and mitigations, the supported CEL subset, and implications for #162 (schema design) and #163 (runtime engine implementation).

## Decision

Use **`telus-oss/cel-net`** (NuGet: `Cel`, Apache 2.0) as the CEL evaluation engine accessed through a thin internal adapter service in `ArchLinterNet.Core`.

The adapter is not a public API. It is an internal composition-managed service owned by Core's execution infrastructure. It does not own CEL configuration globally; each evaluation context is constructed narrowly with declared variables and a registered-only function set.

## Context

CEL (#161) requires a .NET library that can:

- parse and compile boolean predicates at policy load time;
- evaluate compiled expressions against a small typed input context at contract-check time;
- reject unknown variables and unsupported functions deterministically;
- guarantee no host-side effects (file system, network, process, reflection, plugins);
- integrate with Core's composition model without introducing behavior-owning static services.

## Candidate evaluation

### A. telus-oss/cel-net (`Cel`, Apache 2.0) — **Selected**

| Property | Finding |
|---|---|
| NuGet package | `Cel` v0.3.3 |
| License | Apache 2.0 |
| Downloads | 463 K (as of 2026-07-14) |
| Stars | 25 |
| Last release | 2026-06-08 |
| Maintainer | Telus Labs OSS |
| Language | Native C# (83.8% C#, 15.3% Java test data) |
| .NET targets | net8.0; .NET Standard 2.0 |
| Dependencies | `Antlr4.Runtime.Standard`, `Google.Protobuf`, `System.Collections.Immutable`, `TimeZoneConverter` |
| Proto types required | No — empty `FileDescriptor[]` accepted; plain CLR types and primitives work |
| Variable binding | `IDictionary<string, object?>` at invocation time |
| Compile/cache | `celEnvironment.Compile(expr)` returns a reusable `CelProgramDelegate` |
| Custom functions | `RegisterFunction(name, argTypes[], delegate)` before first compile |
| Type environment | No static-analysis-phase check API exposed; unknown variable → `CelException` at runtime |

Key proof from the library's own CLR test suite: `CelEnvironment(new FileDescriptor[] {}, string.Empty)` runs with plain C# objects and primitive variables without requiring custom proto schemas. Boolean predicates, string equality, property access, and numeric comparison all work over plain `Dictionary<string, object?>` inputs.

### B. rayokota/cel.net (`Cel.NET`, Apache 2.0) — **Rejected**

Heavy transitive dependency set (Apache Avro, gRPC client/core, NodaTime, Newtonsoft.Json) makes this package unacceptable for a static-analysis CLI tool that must stay lean and deterministic. Only 9 stars; based on a Java port rather than native C#. Adoption is dominated by Confluent Kafka schema registry usage, which is an unrelated domain.

### C. Minimal internal CEL subset — **Deferred**

A handwritten recursive-descent parser covering only the boolean predicate subset needed by #162 is theoretically possible and would eliminate all CEL-library dependencies. It is deferred because:

- The `Cel` library already validates that no proto schema is needed for ArchLinterNet's use case.
- Maintaining a partial CEL implementation diverges from the spec over time.
- The library's compile-to-delegate caching and ANTLR4-backed parser remove the main reasons to write one from scratch.

This alternative remains open if the proto/ANTLR transitive dependency proves problematic in CI or packaging.

### D. Non-CEL expression engines (DynamicExpresso, NCalc, etc.) — **Out of scope**

These are not CEL-spec implementations. Issue #161 explicitly rules them out. They are not evaluated here.

## API surface (telus-oss/cel-net)

```csharp
// Empty descriptors — no proto schema required for primitive/CLR contexts.
var env = new CelEnvironment(fileDescriptors: [], messageNamespace: string.Empty);

// Optionally register allowlisted functions before first compile.
env.RegisterFunction("matches_glob", [typeof(string), typeof(string)], args => ...);

// Compile once per expression (cache the delegate).
CelProgramDelegate compiled = env.Compile("role == 'Domain' && namespace.startsWith('MyApp')");

// Evaluate per type/contract check.
var variables = new Dictionary<string, object?> { ["role"] = role, ["namespace"] = ns };
bool result = (bool)compiled(variables);
```

`CelProgramDelegate` is `Func<IDictionary<string, object?>, object?>`. The adapter wraps it behind a `bool Evaluate(IDictionary<string, object?> context)` method that casts and forwards exceptions as deterministic configuration errors.

## Typed input context design

For issue #163, the adapter will expose a small, fixed context per expression site. Context fields are declared in C# types that the adapter populates from Core's existing model objects — no schema-generated types, no protobuf messages.

Preliminary context shapes (final names are locked by #162):

**Layer selector** (`when` on a layer entry):

| CEL variable | Type | Source |
|---|---|---|
| `role` | `string` | discovered semantic role |
| `namespace` | `string` | type's declared namespace |
| `assembly` | `string` | assembly short name |
| `metadata` | `IDictionary<string,string>` | declared metadata key/values |

**Contextual contract** (`allow_when` on a dependency rule):

| CEL variable | Type | Source |
|---|---|---|
| `source.role` | `string` | source type's semantic role |
| `source.metadata` | `IDictionary<string,string>` | source type's metadata |
| `target.role` | `string` | target type's semantic role |
| `target.metadata` | `IDictionary<string,string>` | target type's metadata |

Variable names are passed to `CelEnvironment` as plain dictionary keys. Property access on C# objects (`.role`, `.metadata["domain"]`) uses the library's CLR reflection path. For the layer selector, top-level string/IDictionary values are sufficient without a wrapper object.

## Function allowlisting

The `Cel` library enforces function exposure by having no built-in host-call functions. ArchLinterNet's adapter will:

1. Start with the CEL built-in set (string predicates, arithmetic, collection membership, `matches()` RE2 regex).
1. Expose one optional helper via `RegisterFunction`: `glob(pattern, value)` backed by the existing `ProjectPathGlob` pure helper if namespace glob patterns are needed by #162.
1. Register nothing else — no file, network, reflection, process, or runtime-plugin functions exist to register.

The only unsafe exposure vector is the `matches()` built-in, which uses RE2 semantics. RE2 is bounded by design (no catastrophic backtracking). CEL's own spec mandates RE2; no additional mitigation is required.

Unknown variable references throw `CelUndeclaredReferenceException` (a `CelException` subtype) at invocation time. Unknown function calls throw `CelException` during `Compile()` or at first invocation. Both are deterministic typed exceptions, not silent passes or `NullReferenceException`s. The adapter catches the `CelException` base type and re-throws it as an ArchLinterNet-typed configuration error.

## Sandboxing guarantees

CEL is side-effect-free by specification: expressions compute a value from inputs and cannot loop forever, access memory outside their evaluation context, or call host services beyond registered functions. The `Cel` library's implementation inherits these guarantees because:

- The ANTLR4 parser rejects syntax not in the CEL grammar.
- `CelEnvironment` has no file, network, process, or reflection registration surface.
- `RegisterFunction` requires an explicit delegate — no dynamic host-call discovery.
- The library provides no escape hatch (reflection invocation, `dynamic` dispatch on host objects beyond property access, or plugin loading).

The only remaining risk is pathological inputs to `matches()`. RE2 eliminates the exponential-backtracking class of DoS; users of bounded-cost operations remain within CEL's specification guarantees.

## Cancellation and bounded execution

The `Cel` library does not expose a `CancellationToken` API or an expression step-count limiter in v0.3.3. CEL's termination guarantee (no loops, bounded comprehensions) means expression evaluation is inherently short for ArchLinterNet's boolean predicates over a small typed context. Issue #163 should add a wall-clock timeout on the outer evaluation call if the integration benchmarks in #168 reveal any surprising cost.

## Performance and caching implications for #163 and #168

The `Compile()` → `CelProgramDelegate` split is the primary caching boundary. Compilation is dominated by ANTLR4 parse + visitor construction. Evaluation of the returned delegate is expected to be in the microsecond range for ArchLinterNet's small predicate context.

**Recommended caching strategy for #163:**

Cache `CelProgramDelegate` instances keyed by the expression string, shared across all evaluation calls for a given policy load. A `ConcurrentDictionary<string, CelProgramDelegate>` with one `CelEnvironment` per adapter instance is the simplest correct model.

- Literal-only policies (no `when` field populated) never create a `CelEnvironment` instance at all; the adapter path is not entered.
- Policies with repeated identical expressions (common: same `when` on many layer entries) compile once and evaluate many times.
- A policy with N distinct expressions creates N delegates on first policy load, then reuses all of them for the duration of the validation session.

**For #168 benchmarks:**

- Baseline: compile + evaluate one boolean predicate (`role == 'Domain'`).
- Realistic: compile + evaluate 20 distinct predicates (full layer selector set for a medium policy).
- Large: evaluate the delegate 50,000 times with distinct variable inputs (simulating full type graph traversal).
- Pathological: `matches()` on a complex RE2 pattern against 10,000 namespace strings.

## Dependency risk

| Risk | Severity | Mitigation |
|---|---|---|
| `Google.Protobuf` transitive dep adds ~3 MB to tool output | Low | Acceptable for a CLI tool; the binary size delta is negligible against Roslyn |
| `Antlr4.Runtime.Standard` CVE track | Medium | Pin to patched version; existing v0.3.3 release already marks `Antlr4BuildTasks` as private/build-only (PR #10 in the library) |
| telus-oss/cel-net has 25 stars — small community | Medium | Apache 2.0 allows forking; adapter isolation means the dependency surface is one class; can swap to the minimal-wrapper alternative (option C) without touching contract families |
| No static type-checking phase | Low | ArchLinterNet's context is tiny and typed by construction; runtime `CelException` on unknown variable is deterministic and surfaced as a policy-load error before evaluation begins |

## CEL subset assumptions for #162

The final schema design in #162 must stay within the following validated subset:

**Primitives:** `bool`, `string`, `int`, `double`.

**Collections:** `list` (for `in` membership), `map` (for metadata access: `metadata["domain"]`).

**Operators:** `==`, `!=`, `<`, `<=`, `>`, `>=`, `&&`, `||`, `!`, `in`, `? :`.

**Built-in functions (safe):** `startsWith`, `endsWith`, `contains`, `matches` (RE2), `size`.

**Macros (safe):** `all(x, p)`, `exists(x, p)`, `filter(x, p)`. These are bounded by the size of the list they traverse; ArchLinterNet never passes unbounded lists to CEL.

**Out of scope for #162:**

- `has()` field-presence macro — not needed for ArchLinterNet's static-analysis context.
- Timestamp arithmetic (`duration`, `timestamp`) — architecture facts are not time-valued.
- `uint` / `bytes` — no architecture fact has these types.
- Protobuf `Any`, `Message` types — no proto schema in ArchLinterNet.

## Implications for #162

- CEL expressions must appear in explicitly named fields (e.g., `when`, `allow_when`). Implicit string parsing must not be added to existing literal fields.
- The schema must declare the exact set of variables available at each expression site. #162 should produce a variable-context table per expression location.
- The adapter validates and compiles expressions at policy-load time; #162 must ensure invalid expressions are reported as configuration errors, not as runtime mismatches.
- `metadata` access pattern (`metadata["key"]`) is supported without custom functions; #162 should prefer this over introducing a custom helper.

## Implications for #163

- One `CelEnvironment` instance per adapter (composition-managed, not static).
- One `ConcurrentDictionary<string, CelProgramDelegate>` cache inside the adapter.
- `RegisterFunction` is called once at adapter construction for the approved helper set.
- The adapter must not expose `CelEnvironment` to any code outside the expression infrastructure namespace.
- Unknown-variable and type-error paths must be caught and rethrown as ArchLinterNet-typed configuration errors before they reach contract family checkers.

## Proof-of-concept

A disposable NUnit test (`CelImplementationSpikeTests`) in `ArchLinterNet.Core.Tests` validates the following claims directly:

1. `CelEnvironment([], string.Empty)` compiles and evaluates a string-equality predicate with no proto schema.
1. Boolean predicates over primitive context variables (`role`, `namespace`) return the correct result.
1. A CLR object variable with property access (`.Domain`) works as a typed sub-context.
1. Map access (`metadata["domain"]`) works for `IDictionary<string, string>` variable values.
1. An expression referencing an undeclared variable produces a `CelException` deterministically.
1. A compiled delegate (`Compile(expr)`) returns the same result as the direct `Program(expr, vars)` call (cache correctness).

The spike test file is in `tests/ArchLinterNet.Core.Tests/CelImplementationSpikeTests.cs` and is labelled `[Category("Spike")]`. It may be removed once #163 produces the real integration tests.
