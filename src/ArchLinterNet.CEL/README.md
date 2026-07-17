# ArchLinterNet.CEL

A reusable, host-agnostic [Common Expression Language](https://cel.dev/) (CEL) compilation and
evaluation engine for .NET, implementing the **ArchLinter CEL Profile v1** (`arch-linter/cel/v1`) —
a deliberately bounded subset of CEL chosen for safety, determinism, and predictable evaluation
cost.

`ArchLinterNet.CEL` has no dependency on `ArchLinterNet.Core`, `ArchLinterNet.Cli`, or
`ArchLinterNet.Testing`, and pulls in no external CEL runtime, YAML, or reflection-based binding
library. It can be referenced standalone by any .NET project that needs bounded expression
evaluation over an explicit, schema-declared data model.

> Status: early preview (`0.x`). The Profile v1 public API surface is stabilizing but is not yet
> guaranteed compatibility-frozen.

## Install

```bash
dotnet add package ArchLinterNet.CEL
```

## Quick start

```csharp
using ArchLinterNet.CEL;
using ArchLinterNet.CEL.Profile;
using ArchLinterNet.CEL.Schema;
using ArchLinterNet.CEL.Values;

// 1. Declare the variables a predicate expression may reference.
var schemaBuilder = CelContextSchema.CreateBuilder("assembly-predicate-v1");
var source = schemaBuilder.AddVariable("source", CelType.ObjectOf("assembly"));
var target = schemaBuilder.AddVariable("target", CelType.ObjectOf("assembly"));
var schema = schemaBuilder.Build();

// 2. Declare the shape of any object types referenced above, so member access
//    (e.g. `source.role`) type-checks without CLR reflection.
var assemblySchemaBuilder = CelObjectSchema.CreateBuilder("assembly");
assemblySchemaBuilder.AddMember("role", CelType.String);
assemblySchemaBuilder.AddMember("namespace", CelType.String);
var assemblySchema = assemblySchemaBuilder.Build();

// 3. Build an immutable environment (compile-once, evaluate-many).
var environment = CelEnvironment.CreateBuilder(CelProfile.V1)
    .WithContextSchema(schema)
    .WithObjectSchema(assemblySchema)
    .Build();

// 4. Compile an expression. Invalid user input produces structured diagnostics —
//    CelEnvironment never throws on malformed or unsupported CEL syntax.
var compilation = environment.CompilePredicate(
    "source.role == 'service' && target.namespace.startsWith('Example.')");

if (!compilation.IsSuccess)
{
    foreach (var diagnostic in compilation.Diagnostics)
    {
        Console.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
    }

    return;
}

// 5. Build an evaluation context using the typed handles from step 1 — no string-keyed lookups.
var context = environment.CreateEvaluationContextBuilder()
    .Set(source, CelValue.Object(new CelObjectValue("assembly", new Dictionary<string, CelValue>
    {
        ["role"] = CelValue.String("service"),
        ["namespace"] = CelValue.String("Example.Services"),
    })))
    .Set(target, CelValue.Object(new CelObjectValue("assembly", new Dictionary<string, CelValue>
    {
        ["role"] = CelValue.String("domain"),
        ["namespace"] = CelValue.String("Example.Domain"),
    })))
    .Build();

// 6. Evaluate. The compiled predicate is immutable and thread-safe — evaluate it
//    concurrently against many contexts without recompiling.
var result = compilation.Program!.Evaluate(context);
Console.WriteLine(result.IsSuccess ? result.Value : result.Diagnostics[0].Message);
```

## Compilation and evaluation lifecycle

1. **Compile once** — `CelEnvironment.CompilePredicate(...)` / `.Compile(...)` parses, type-checks,
   and binds an expression against the environment's schema, returning a
   `CelCompilationResult<T>` with either a compiled program (`CelCompiledPredicate` /
   `CelCompiledExpression`) or structured `CelDiagnostic`s. No exceptions are thrown for invalid
   user expressions.
2. **Evaluate many** — a compiled program is immutable and thread-safe. Call `.Evaluate(context)`
   (or `.Evaluate(context, limits)` for evaluation-time budget overrides) as many times as needed
   against different `CelEvaluationContext` instances built from the same schema.
3. **Cache identity** — `CelCompilationResult<T>.CompilationKey` is a deterministic, structurally
   comparable identity (source, profile, schema, result type, and both limit identities) usable to
   verify two compiled results are semantically equivalent. It cannot be constructed before a
   compile call — callers wanting a pre-compile "have I already compiled this?" cache should key by
   the raw expression source text instead, scoped to one compilation kind.

## Limits, diagnostics, and thread-safety

- Every compilation and evaluation is bounded: `CelCompilationLimits` and `CelEvaluationLimits`
  (with `SafeDefaults`) enforce maximum expression length, token/AST node/identifier counts,
  nesting depth, iteration count, and cost units. There is no unbounded evaluation path.
- Failures — both compile-time (syntax errors, unsupported Profile v1 features, unresolved
  identifiers/members, type mismatches) and evaluation-time (missing keys, invalid indices, budget
  exceeded, schema mismatch) — are reported as structured `CelDiagnostic`s with stable
  `CelDiagnosticCode`s and source spans, never as CLR exceptions from valid API usage.
- `CelEnvironment`, compiled programs, and all public value/type/schema types are immutable after
  `Build()`. Concurrent evaluation of one compiled program from multiple threads is safe.

## Non-goals (Profile v1)

Profile v1 is intentionally closed. It does **not** support: arithmetic operators, conditional
(`? :`) expressions, `uint`/bytes/timestamp/duration literals, list/map/message literals, macros or
comprehensions (`all`, `exists`, `map`, `filter`), regular expressions (`matches`), protobuf or
other schema-backed message types, user-registered functions, arbitrary CLR object binding or
reflection, or a public AST. These are tracked extension directions, not accidental omissions —
see the repository's internal architecture blueprint for the governed path each would take if
approved.

`ArchLinterNet.CEL` does not implement full CEL conformance and does not aim to.

## Links

- [Documentation](https://eugenemalaschuk-source.github.io/arch-linter-net/)
- [Repository](https://github.com/eugenemalaschuk-source/arch-linter-net)
