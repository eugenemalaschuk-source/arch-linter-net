# ArchLinterNet.CEL benchmark baseline (#168)

Baseline measurements from the suite in this directory, established for #330's packaging/
reconciliation pass and #163's Core integration guidance. Not a regression gate — see
[README.md](README.md) for why the suite is optional and excluded from `make acceptance`.

## Run configuration

- **Command:** `dotnet run -c Release --project benchmarks/ArchLinterNet.CEL.Benchmarks -- --filter '*' --job short`
- **BenchmarkDotNet:** v0.14.0
- **OS:** Windows 10 (10.0.19045.6456/22H2/2022Update)
- **CPU:** Intel Core i5-9600K @ 3.70GHz (Coffee Lake), 1 CPU, 6 logical/6 physical cores
- **.NET SDK:** 10.0.302 — Runtime: .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
- **Job:** `ShortRun` — `IterationCount=3, LaunchCount=1, WarmupCount=3`. This is a fast,
  lower-statistical-confidence job (wide `Error`/`StdDev` bands are visible below) chosen to keep
  the run under a few minutes for this baseline pass. Before using these numbers to gate or judge
  a specific optimization, rerun with the BenchmarkDotNet default job
  (`make benchmark-cel` with no `--job` override) for tighter confidence intervals.
- Expression source, profile version, and public API path: Profile v1 (`arch-linter/cel/v1`),
  exclusively through `CelEnvironment`/`CelCompiledPredicate`/`CelCompiledExpression` (see each
  class's remarks in source for the one exception — `PipelineStageBenchmarks`, which uses
  internal-only access to isolate stage cost; still never a public API surface).
- `CacheIdentityBenchmarks` and `ContextConstructionBenchmarks` were rerun (same run configuration
  above) after a review round fixed two measurement flaws in them — an unfair hit-vs-miss expression
  comparison, and object-value construction leaking into the timed context-builder methods. Their
  numbers below reflect the fixed versions; see each section's note for what changed and why.

## Results by class

### EnvironmentConstructionBenchmarks — one-time schema/environment construction

| Method | Mean | Allocated |
|---|---:|---:|
| Build source/target context schema (2 object-typed variables) | 459.0 ns | 1,520 B |
| Build the 'assembly' object schema (5 members) | 962.7 ns | 2,328 B |
| Build full CelEnvironment (schema + object schema + `Build()`) | 1,550.9 ns | 4,433 B |
| `CelEnvironmentBuilder` with `SafeDefaults` limits only, no object schemas | 119.5 ns | 600 B |

Object-schema construction cost roughly scales with declared-member count relative to
context-schema construction's variable count (5 members ≈ 2× the cost of 2 variables) — expected
builder-validation overhead, not a hotspot at process-lifetime, one-time cost.

### PipelineStageBenchmarks — internal-only stage isolation

| Method | Mean | Allocated |
|---|---:|---:|
| Tokenize representative predicate | 642.5 ns | 1,864 B |
| Parse pre-tokenized representative predicate | 343.1 ns | 656 B |
| Bind pre-parsed representative predicate against generic context schema | 765.9 ns | 1,920 B |
| Tokenize + parse + bind, staged (sum of the three isolated stages) | 1,984.4 ns | 4,440 B |

Binding is the single most expensive stage (~39% of the combined tokenize+parse+bind time),
followed by tokenizing (~32%) and parsing (~17%) — the staged sum (1,984.4 ns) is close to but not
exactly the three isolated numbers' sum (1,751.5 ns), the difference being repeated JIT/measurement
noise at this `ShortRun` confidence level, not a systematic cost this suite's design missed.

### CompilationBenchmarks — full public `CompilePredicate`/`Compile` pipeline

| Method | Mean | Allocated |
|---|---:|---:|
| `CompilePredicate`: full public pipeline, representative expression | 2.814 us | 6.33 KB |
| `CompilePredicate`: full public pipeline, longer expression | 8.000 us | 16.62 KB |
| `Compile`: general-expression path, same source required to type-check as Bool | 2.757 us | 6.33 KB |
| `CompilePredicate`: failing compilation (undeclared member), diagnostic overhead | 5.574 us | 4.79 KB |

The predicate-convenience path (`CompilePredicate`) and the general-expression path (`Compile`)
cost the same (2.814 us vs 2.757 us, within measurement noise) for an expression that both compile
successfully — the predicate/general split adds no measurable overhead of its own. The longer,
6-operator `ComplexPredicateSource` costs ~2.8× the 2-operator representative expression, roughly
proportional to operator/member-access count. A failing compilation (undeclared member, fails at
the binder) costs ~2× the successful representative-expression compile, allocates less
(no bound-plan construction), but that extra time is largely `CelBindException` construction/catch
overhead — a real, CLR-exception-driven cost of the fail-fast diagnostic design (see the code
comment on `CompilationBenchmarks.CompilePredicateFailure`).

### ContextConstructionBenchmarks — stable-handle vs. name-based population

*(Rerun after a review fix: the source/target `CelValue` object instances are now precomputed once
in `GlobalSetup` and reused, instead of being constructed inside the timed methods — see
`ContextConstructionBenchmarks`'s class remarks. The two rows below now measure only
`CelEvaluationContextBuilder`'s own cost, not object-value construction cost; both dropped
substantially from the first baseline pass as a result.)*

| Method | Mean | Allocated |
|---|---:|---:|
| Build source/target context via stable variable handles (precomputed values) | 913.2 ns | 3.02 KB |
| Build source/target context via name-based `Set()` convenience overload (precomputed values) | 980.1 ns | 3.21 KB |
| Build context without object-schema catalog (`schema.CreateEvaluationContextBuilder()`) | 286.4 ns | 1.25 KB |

The name-based convenience overload costs ~7.3% more time and ~6.3% more allocation than the
stable-handle path for this two-variable, object-typed schema — the extra `FirstOrDefault` name
lookup is a real but modest fraction of total context-build cost. Skipping the object-schema
catalog (primitive-only schema, one `Bool` variable, no structural object validation) is ~3.2×
cheaper than the object-typed, catalog-validated schema — with value construction now excluded from
both sides, this comparison isolates `CelEvaluationContextBuilder.Set`'s own structural validation
cost (recursive member-by-member type checking against the registered object schema) from
handle-vs-name resolution and from object-value allocation. The documented guidance to prefer
handle-based `Set()` "in high-volume evaluation paths" remains correct directionally but modest in
magnitude for this shape — worth noting for #330's docs so the guidance isn't overstated.

### EvaluationBenchmarks — compile-once/evaluate-many, by operator category

| Method | Mean | Ratio vs. string equality | Allocated |
|---|---:|---:|---:|
| Evaluate: string equality (`a == b`) | 107.4 ns | 1.00 | 304 B |
| Evaluate: string `startsWith()` | 132.3 ns | 1.23 | 408 B |
| Evaluate: string `contains()` | 136.2 ns | 1.27 | 408 B |
| Evaluate: `list.size()` comparison | 135.8 ns | 1.26 | 408 B |
| Evaluate: list membership (`needle in names`) | 156.2 ns | 1.45 | 416 B |
| Evaluate: map membership (`key in lookup`) | 105.7 ns | 0.98 | 232 B |
| Evaluate: `map.containsKey()` | 132.1 ns | 1.23 | 336 B |
| Evaluate: map indexing (`lookup[key]`), general-expression path | 103.8 ns | 0.97 | 160 B |
| Evaluate: numeric comparison (`n > 0 && f < 10.5`) | 183.3 ns | 1.71 | 520 B |
| Evaluate: boolean combination (3-way `&&`, short-circuiting) | 287.1 ns | 2.67 | 696 B |
| Evaluate: explicit per-call limits vs. environment ceiling (budget-check overhead) | 110.9 ns | 1.03 | 304 B |

Every single-operator evaluation is sub-200ns and allocates under 500B — confirming repeated
evaluation of an immutable compiled expression is cheap, with cost scaling near-linearly with bound
node count (the 3-way boolean combination, which evaluates 3 sub-predicates, costs ~2.67× a single
equality check). Passing explicit per-call limits equal to the environment ceiling costs
effectively the same as the safe-default overload (1.03×, within noise) — confirming
`Evaluate(context)` adds no measurable hidden overhead over `Evaluate(context, limits)`.

### CacheIdentityBenchmarks — `CelCompilationKey` and caller-owned caching

*(Rerun after a review fix: hit and miss now look up/compile the exact same expression
— `RepresentativePredicateSource` — differing only in cache *state* (a warm dictionary vs. a
permanently empty one), so the ratio below is an apples-to-apples same-expression comparison. The
first baseline pass had compared a hit on the short representative expression against a miss that
fell through to compiling the longer `ComplexPredicateSource`, which inflated the reported ratio.)*

| Method | Mean | Allocated |
|---|---:|---:|
| Isolated cache-key creation (schema/limits identity + ctor, no tokenize/parse/bind) | 341.4 ns | 1,832 B |
| `CelCompilationKey` via the only public path: full `CompilePredicate` call | 2,213.1 ns | 6,432 B |
| `CelCompilationKey.Equals` — equivalent schema/source/limits | 16.4 ns | 0 B |
| `CelCompilationKey.Equals` — different schema identity | 1.0 ns | 0 B |
| `CelCompilationKey.GetHashCode()` | 380.7 ns | 0 B |
| Caller-owned cache keyed by source text: hit for `RepresentativePredicateSource` (dictionary lookup only) | 15.6 ns | 0 B |
| Caller-owned cache keyed by source text: miss for `RepresentativePredicateSource` (lookup fails, falls through to compile) | 2,043.9 ns | 6,432 B |

Key construction in isolation (schema/limits identity computation + the `CelCompilationKey`
constructor) is ~15% of a full compile's time and ~28% of its allocation — meaningful but not the
dominant cost of `CompilePredicate`; tokenize/parse/bind still account for the remaining ~85%/72%.
Equality/hashing on an already-built key is effectively free (0 B allocated, low-nanosecond time).
The miss benchmark's mean (2,043.9 ns) and allocation (6,432 B) land close to the full-compile row
above (2,213.1 ns, 6,432 B) — expected, since a miss falls through to exactly that same
`CompilePredicate` call, and this closeness is itself a consistency check that the fix produced a
coherent measurement. The realistic caller-owned cache pattern — keyed by raw source text, not by
`CelCompilationKey` (see the class remarks in `CacheIdentityBenchmarks.cs` for why) — shows the
expected large payoff for the *same* expression: a hit is ~131× faster than a miss (15.6 ns vs.
2,043.9 ns) and allocates nothing, confirming that caching identical expressions inside a
long-lived `CelEnvironment` is worth doing whenever the same source text recurs.

### ConcurrencyBenchmarks — sequential vs. concurrent reuse of one compiled predicate

| Method | Mean | Ratio | Allocated |
|---|---:|---:|---:|
| Sequential evaluation across 8 independent contexts | 2.768 us | 1.00 | 5.44 KB |
| Concurrent evaluation across 8 independent contexts (`Parallel.For`) | 4.135 us | 1.49 | 7.91 KB |

At this granularity (8 tasks, each a single sub-200ns evaluation), `Parallel.For`'s own scheduling
overhead exceeds the work being parallelized, so the concurrent variant is slower in wall-clock
terms — expected at this scale and not a signal that concurrent evaluation is unsafe or poorly
optimized; the benchmark's purpose is confirming *correctness* of concurrent reuse (no shared
mutable state, no exceptions, no torn results — verified separately by the CEL test suite's
concurrency tests), not proving a parallelism speedup for single-evaluation-sized units of work.
Real Core workloads batch many more evaluations per task than this benchmark's minimal case; a
throughput comparison at realistic batch sizes is a candidate follow-up if #163 needs it.

### ApiScenarioBenchmarks — deterministic diagnostic paths, safe-default overload

| Method | Mean | Ratio | Allocated |
|---|---:|---:|---:|
| Deterministic `BudgetExceeded` (~200 of a 256-element scan) | 5,531.8 ns | 16.15 | 15,312 B |
| Deterministic `SchemaMismatch` rejection (context from a different schema) | 407.2 ns | 1.19 | 1,272 B |
| Successful predicate, zero diagnostics, safe-default overload | 342.6 ns | 1.00 | 696 B |
| Explicit-limits overload with the same ceiling value | 328.7 ns | 0.96 | 696 B |

`SchemaMismatch` is rejected cheaply (1.19× baseline) — the evaluator checks schema identity before
walking the bound plan at all. `BudgetExceeded` costs 16× the successful baseline here because the
scenario is calibrated (see `ApiScenarioBenchmarks.SetupBudgetExhaustion`) to genuinely scan ~200
of 256 haystack elements before the cost ceiling trips, so the cost is dominated by real per-element
string-comparison work, not a fixed penalty — confirming the evaluator's cost accounting is real
(charged per unit of work, not a flat per-call tax).

## Allocation findings / actionable hotspots

- **Compile-once/evaluate-many is real and large**: compilation (2.8–8.0 us, 6.3–16.6 KB) is
  roughly 15–75× more expensive than a single evaluation of the resulting predicate (0.1–0.3 us,
  0.2–0.7 KB). This is the architecture's central promise and it holds — no action needed, but
  worth quoting concretely in #330's docs rather than only asserting it qualitatively.
- **Object-value structural validation, not handle-vs-name resolution, dominates context
  construction.** With value construction excluded from the measurement (see the rerun note under
  `ContextConstructionBenchmarks` above), the object-typed/catalog-validated schema still costs
  ~3.2× the primitive-only schema. If #163's Core integration needs faster context population at
  very high volume, the actionable lever is reducing per-object member count or the
  `MaxValidationDepth`/`MaxValidationCollectionSize` traversal cost in
  `CelEvaluationContextBuilder.Set`, not switching from name-based to handle-based `Set()` (that
  swap alone saves only ~7%).
- **`CelCompilationKey` cannot serve as a pre-compile cache key through the public API** (every
  identity component it's built from — schema/limits `ComputeIdentity()` — is internal). This is a
  real API characteristic, not a benchmark artifact: worth documenting explicitly in #330's package
  docs so Core integration keys its own cache by source text (as `CacheIdentityBenchmarks`
  demonstrates), not by attempting to precompute a `CelCompilationKey` before the first compile.
- **No unexpected allocation hotspot surfaced.** Every measured path's allocation is proportional
  to its own declared inputs (schema size, expression complexity, haystack size) — nothing here
  suggests premature optimization is warranted, consistent with the issue's stated purpose ("not to
  force zero allocation everywhere").
- **Concurrent-reuse correctness is confirmed by the test suite; this benchmark's speedup number at
  8-task granularity should not be read as a scalability verdict** — see the `ConcurrencyBenchmarks`
  note above.
- **"No repeated parsing/binding/type-checking during warm evaluation" is now instrumented, not
  just asserted.** `tests/ArchLinterNet.CEL.Tests/CelRepeatedEvaluationNoReparseTests.cs` proves
  this deterministically (no wall-clock dependence): `CelCompiledPredicate.Evaluate`'s only
  overloads take `(context)`/`(context, limits)` — neither carries a source string, token stream,
  or syntax node, so there is no data-flow path back into `CelTokenizer.Tokenize`/`CelParser.Parse`/
  `CelBinder.Bind`; the internal `Bound` plan property is get-only (compiler-enforced immutability
  post-construction); and the exact same `Bound` object reference is observed before and after
  1,000 evaluations of a compiled predicate.

## Feeding into #330 / #163

- #330 (packaging/reconciliation): the compile-once/evaluate-many magnitude above, the
  name-based-vs-handle-based finding, and the `CelCompilationKey`-is-not-a-pre-compile-key finding
  are all concrete enough to fold into the package's consumer-facing performance guidance.
- #163 (Core integration): the per-operator evaluation numbers (all sub-200ns) and the same-expression
  cache hit/miss numbers (15.6 ns vs. 2.04 us, ~131×) give Core a baseline to plan its own
  selector/architecture-fact evaluation batch sizes against, and confirm a source-text-keyed cache
  is the right caching primitive to reuse.
