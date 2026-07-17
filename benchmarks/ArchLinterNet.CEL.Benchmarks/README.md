# ArchLinterNet.CEL.Benchmarks

BenchmarkDotNet suite for `ArchLinterNet.CEL` (#168). Optional — not part of `make acceptance` or
`make test`. BenchmarkDotNet iterates until statistically stable, which takes minutes per class;
running it inside the normal acceptance gate would make CI slow and non-deterministic.

See [RESULTS.md](RESULTS.md) for the recorded baseline run (hardware/runtime, per-class results,
allocation findings, and conclusions feeding #330/#163).

## Running

```
rtk make benchmark-cel                                   # run every benchmark class
rtk make benchmark-cel BENCHMARK_FILTER='*EvaluationBenchmarks*'  # run one class
```

Equivalent direct invocation:

```
rtk dotnet run -c Release --project benchmarks/ArchLinterNet.CEL.Benchmarks -- --filter '*'
```

A `-c Release` build is required — BenchmarkDotNet refuses to run unoptimized builds by default.

## What each class measures

| Class | Scope |
|---|---|
| `EnvironmentConstructionBenchmarks` | One-time context-schema, object-schema, and `CelEnvironment` construction/freeze cost. |
| `PipelineStageBenchmarks` | Tokenize/parse/bind measured individually, using `InternalsVisibleTo`-granted access to the internal pipeline stages (see `ArchLinterNet.CEL.csproj`). Isolates costs the public `CompilePredicate`/`Compile` API only reports combined. |
| `CompilationBenchmarks` | The full public compilation pipeline via `CompilePredicate`/`Compile`; predicate vs. general-expression overhead; failing-compilation diagnostic overhead. |
| `ContextConstructionBenchmarks` | Stable-handle vs. name-based `Set()`; context construction with and without the object-schema catalog. |
| `EvaluationBenchmarks` | Compile-once/evaluate-many repeated evaluation, broken out by operator/built-in category (string, list, map, comparison, boolean) and by explicit vs. environment-ceiling limits. |
| `CacheIdentityBenchmarks` | `CelCompilationKey` creation/equality/hashing, and a caller-owned `Dictionary` cache hit/miss pattern (the library holds no process-global cache by design). |
| `ConcurrencyBenchmarks` | Sequential vs. concurrent (`Parallel.For`) evaluation of one immutable compiled predicate across independent, per-task evaluation contexts. |
| `ApiScenarioBenchmarks` | Deterministic `BudgetExceeded` and `SchemaMismatch` diagnostic paths, and the safe-default `Evaluate(context)` overload vs. the explicit-limits overload. |

## Design constraints this suite follows

- Every scenario benchmark uses the same public safe API real consumers use
  (`CelEnvironment`, `CelCompiledPredicate`/`CelCompiledExpression`, `CelEvaluationContextBuilder`).
  `PipelineStageBenchmarks` is the one exception, and only to isolate internal-stage cost that the
  public API deliberately does not expose separately — see its type-level remarks.
- No wall-clock assertions gate CI; this project produces measurements, not pass/fail checks.
- No `ArchLinterNet.Core`, YAML, Roslyn, or architecture-policy dependency — only
  `ArchLinterNet.CEL` and `BenchmarkDotNet`.
- Budget-exhaustion and schema-mismatch benchmarks are deterministic by construction: the
  budget-exhaustion cost ceiling is calibrated from the exact per-comparison cost math in
  `CelEvaluator` (see `ApiScenarioBenchmarks.SetupBudgetExhaustion`) to fail after roughly 200 of
  256 haystack elements — proving the failure genuinely depends on haystack length, not just the
  first comparison — and the schema-mismatch context is built from a structurally different
  schema. Neither depends on chance or timing.
