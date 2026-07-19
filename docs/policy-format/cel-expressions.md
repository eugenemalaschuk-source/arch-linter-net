# CEL Policy Expressions

This is the canonical guide to `when` expressions in ArchLinterNet policies. It
is the single entry point for CEL support — other pages link here instead of
repeating the language definition.

## Overview

A small set of YAML selector fields accept an optional `when` predicate written
in [CEL (Common Expression Language)](https://github.com/cel-expr/cel-spec).
`when` narrows an already-scoped literal `role`/`metadata` selector; it never
replaces one.

### Why standard CEL, not a proprietary DSL

ArchLinterNet does not invent its own expression syntax. `when` is standard
CEL, restricted to a documented, safe subset — **ArchLinter CEL Profile v1**
(`arch-linter/cel/v1`). This is a deliberate product decision, not an
implementation shortcut:

- **Standards alignment.** CEL is a real, independently specified language
  with an existing ecosystem, tooling, and documentation. ArchLinterNet does
  not maintain a second, undocumented grammar for policy authors to learn.
- **AI-first.** AI coding agents already have broad prior knowledge of CEL
  syntax and semantics from training on other CEL-based systems (e.g. Kubernetes
  admission policies, IAM conditions). An agent editing an ArchLinterNet policy
  can reuse that knowledge directly — it only needs to load the
  ArchLinterNet-specific *context* (which locations accept `when`, which
  variables are bound, which functions exist), not an entire new language.

This page and the rest of ArchLinterNet's documentation therefore separate
three distinct layers:

1. **Standard CEL** — the language itself, defined by the official
   [`cel-expr/cel-spec`](https://github.com/cel-expr/cel-spec) specification.
   ArchLinterNet does not redefine CEL syntax or semantics; it only decides
   which parts of standard CEL are available.
1. **ArchLinter CEL Profile v1** — a strict, safe *subset* of standard CEL.
   The [Profile v1 support matrix](#profile-v1-support-matrix) below is
   normative for what compiles.
1. **ArchLinterNet policy context** — the YAML locations that accept `when`,
   and the typed variables (`subject`, `source`, `target`, `dependency`)
   available at each location. This is ArchLinterNet-specific and documented
   in the [authoring reference](#authoring-reference) below.

> **Not full CEL conformance.** ArchLinter CEL Profile v1 pins the CEL
> Language Definition at commit
> [`59505c14f`](https://github.com/cel-expr/cel-spec/blob/59505c14f3187e6eb9684fbd3d07146f614c6148/doc/langdef.md)
> but implements only a bounded subset of it (see the support matrix). A
> `when` expression that is valid standard CEL can still fail to compile
> under Profile v1 if it uses a deferred feature — that failure is reported
> as an `UnsupportedFeature` diagnostic, distinct from a syntax error in
> invalid CEL.

## Authoring reference

### Locations that accept `when`

| YAML location | Root variable(s) | Result type |
|---|---|---|
| `layers.<name>.selector.when` | `subject` | `Bool` |
| `contracts.strict_context_dependencies[*].source.when` (and `audit_context_dependencies`) | `source` | `Bool` |
| `contracts.strict_context_dependencies[*].forbidden[*].when` (and audit) | `source`, `target`, `dependency`\* | `Bool` |
| `contracts.strict_context_dependencies[*].exclude[*].when` (and audit) | `source`, `target`, `dependency`\* | `Bool` |
| `contracts.strict_context_allow_only[*].source.when` (and `audit_context_allow_only`) | `source` | `Bool` |
| `contracts.strict_context_allow_only[*].allowed[*].when` (and audit) | `source`, `target`, `dependency`\* | `Bool` |
| `contracts.strict_context_allow_only[*].exclude[*].when` (and audit) | `source`, `target`, `dependency`\* | `Bool` |
| `contracts.strict_layout_conventions[*].files_matching.when` (and `audit_layout_conventions`) | `subject` | `Bool` |

\* `dependency` is schema-declared but currently **rejected at policy load** if
referenced anywhere in a `when` expression's source text (including inside a
string literal or comment). The scanning path behind contextual dependency and
allow-only matching does not yet track per-edge member/method-body facts, so
populating `dependency` today would let the expression compile and then never
behave as written. Compare against `source`/`target` facts instead until this
is implemented.

Every other YAML location — imports, `analysis.*`, `external_dependencies`/
`packages.*`, layer namespace fields, classification mappings, non-contextual
contract families, coverage contracts, `ignored_violations`, baseline entries,
and `id`/`name`/`reason` fields — **rejects `when`** at policy load. `when` is
never inferred from an ordinary string field anywhere else.

### The `subject`/`source`/`target` object

`subject`, `source`, and `target` share the identical, closed member set:

| Member | Type | Notes |
|---|---|---|
| `fullName` | `String` | Fully-qualified type name. |
| `simpleName` | `String` | Short type name without namespace. |
| `namespace` | `String` | Declared namespace, or empty string. |
| `assemblyName` | `String` | Resolved assembly simple name. |
| `projectName` | `String` | Discovered project name, or empty string. |
| `role` | `String` | Resolved semantic role. |
| `metadataText` | `Map[String]` | String-valued classification metadata by key. |
| `metadataBool` | `Map[Bool]` | Boolean-valued classification metadata by key. |
| `kind` | `String` | `class`, `interface`, `enum`, `struct`, `record`, or `delegate`. |
| `isAbstract` | `Bool` | Structural type fact. |
| `isSealed` | `Bool` | Structural type fact. |
| `baseTypeNames` | `List[String]` | Full names of direct/transitive base types. |
| `interfaceTypeNames` | `List[String]` | Full names of implemented interfaces. |
| `attributeTypeNames` | `List[String]` | Full names of discovered type-level attributes/markers. |
| `sourcePaths` | `List[String]` | Repository-relative source file paths known for the type. |
| `sourceDirectoryPrefixes` | `List[String]` | Every ancestor directory of every `sourcePaths` entry, `/`-separated, no trailing slash. |

Numeric metadata is **not** exposed to `when` — there is no lossless mapping
from ArchLinterNet's canonical `decimal` metadata domain onto CEL's `Int`/
`Float`. Match numeric metadata with a literal `metadata:` selector instead.

### The `dependency` object (schema-declared, not yet populated)

| Member | Type | Notes |
|---|---|---|
| `kind` | `String` | Edge kind, e.g. `type_reference`, `method_body_call`. |
| `viaMethodBody` | `Bool` | `true` when the edge comes from method-body evidence. |
| `sourceMemberName` | `String` | Declaring source member name, or empty string. |
| `targetMemberName` | `String` | Referenced target member name, or empty string. |

See the note above the location table — `dependency` currently rejects any
`when` that references it, by design, until real per-edge facts land.

### Missing values have no `null`

CEL Profile v1 has no `null`. A missing map key or an out-of-range list index
is an **evaluation failure**, not a silent `false`. Guard lookups explicitly:

```yaml
when: >
  subject.metadataText.containsKey("domain")
  && subject.metadataText["domain"] == "Sales"
```

## Profile v1 support matrix

ArchLinter CEL Profile v1 (`arch-linter/cel/v1`) is a deliberately bounded
subset of standard CEL, pinned at
[`cel-expr/cel-spec@59505c14f`](https://github.com/cel-expr/cel-spec/blob/59505c14f3187e6eb9684fbd3d07146f614c6148/doc/langdef.md).

### Supported types

`Bool`, `String`, `Int` (signed 64-bit), `Float` (IEEE 754 double), `List`
(homogeneous element type), `Map` (string-keyed, homogeneous value type), and
the schema-defined `Object` types described above (`subject`/`source`/
`target`/`dependency`).

### Supported operators

| Operator | Meaning |
|---|---|
| `!` | Logical negation (`Bool` only) |
| `&&`, `\|\|` | Logical conjunction/disjunction, CEL's error-aware commutative semantics |
| `==`, `!=` | Equality (same-type operands only, no implicit widening) |
| `<`, `<=`, `>`, `>=` | Ordered comparison (`Int`-to-`Int` or `Float`-to-`Float` only) |
| `in` | Set membership (`T in List<T>`) or map key membership (`String in Map<String, T>`) |
| `.` | Member access |
| `[]` | Map/list indexing |

Arithmetic (`+`, `-`, `*`, `/`), the conditional operator (`? :`), and every
other standard CEL operator are **deferred** and rejected at compile time.
Comparison operators are non-associative — `a < b < c` requires explicit
parentheses.

### Supported functions

| Function | Receiver | Arguments | Result |
|---|---|---|---|
| `startsWith` | `String` | `(String)` | `Bool` |
| `endsWith` | `String` | `(String)` | `Bool` |
| `contains` | `String` | `(String)` | `Bool` (substring test — list membership uses `in`, not `contains`) |
| `size()` | `String` | — | `Int` (Unicode code-point count) |
| `size()` | `List` | — | `Int` (element count) |
| `size()` | `Map` | — | `Int` (entry count) |
| `containsKey` | `Map` | `(String)` | `Bool` (returns `false`, never fails, on a missing key) |

`matches` (regex) and all other standard CEL built-ins — timestamp/duration,
protobuf, byte/string conversion, and any user-defined function — are
**deferred** and rejected at compile time.

### Limits

Every expression compiles and evaluates under bounded limits: maximum source
length, token count, AST node count, nesting depth, literal size, and
identifier count at compile time; maximum evaluation steps and abstract cost
units at evaluation time. Exceeding any limit produces a `BudgetExceeded`
diagnostic rather than an unbounded computation — this keeps CEL evaluation
safe to run in CI without a separate sandboxing layer.

## Examples

### Minimal first expression

```yaml
layers:
  sales_domain:
    selector:
      role: DomainLayer
      when: subject.metadataText["domain"] == "Sales"
```

### Semantic-role selector, guarded lookup

```yaml
layers:
  sales_domain:
    selector:
      role: DomainLayer
      when: >
        subject.metadataText.containsKey("domain")
        && subject.metadataText["domain"] == "Sales"
```

### Source/target contextual dependency

```yaml
contracts:
  strict_context_dependencies:
    - id: domain-cross-context
      name: domain-cross-context
      source:
        role: DomainLayer
        when: source.metadataText.containsKey("domain")
      forbidden:
        - role: DomainLayer
          when: >
            source.metadataText.containsKey("domain")
            && target.metadataText.containsKey("domain")
            && target.metadataText["domain"] != source.metadataText["domain"]
      reason: Domain types must not depend directly on another bounded context.
```

### Modular-monolith example

```yaml
layers:
  sales_domain:
    selector:
      role: DomainLayer
      metadata:
        domain: Sales
      when: >
        subject.metadataText.containsKey("domain")
        && subject.metadataText["domain"] == "Sales"
        && (
          subject.namespace == "Acme.Sales.Domain"
          || subject.namespace.startsWith("Acme.Sales.Domain.")
        )
```

The literal `role`/`metadata` boundary stays explicit; `when` adds only the
extra namespace refinement `role`/`metadata` alone cannot express.

### Unity/client example

```yaml
layers:
  unity_presenters:
    selector:
      role: PresentationLayer
      when: >
        "UnityEngine.MonoBehaviour" in subject.baseTypeNames
        && "Assets/Game/Client" in subject.sourceDirectoryPrefixes
```

Use `sourceDirectoryPrefixes` (not `sourcePaths`) for a directory-prefix
check — a directory string is never equal to a full file path, so
`"Assets/Game/Client/" in subject.sourcePaths` is always `false`.

### Layout convention example

```yaml
contracts:
  strict_layout_conventions:
    - id: handlers-forbid-when-narrowed-class
      name: handlers-when-narrowed-class-is-forbidden
      files_matching:
        folder_segment: Handlers
        when: subject.simpleName == "PortLayoutWhenNarrowedTarget"
      forbid_type_kind: class
      reason: Demonstrates a CEL when predicate narrowing which declared types are checked.
```

`when` on `files_matching` narrows *which discovered declared types* the rest
of the contract checks — the file must already match `folder_segment` (or
another literal selector field), and `when` further restricts the candidate
set within that match. This example is exercised end-to-end by
`tests/ArchLinterNet.Cli.Tests/LayoutConventionCliTests.cs`.

### Equivalent literal policy (prefer this when it suffices)

```yaml
# When the constraint is a fixed value, a literal metadata selector is
# clearer than `when` and needs no expression at all.
layers:
  sales_domain:
    selector:
      role: DomainLayer
      metadata:
        domain: Sales
```

### Invalid / unsupported-feature examples

```yaml
# Invalid CEL — SyntaxError.
when: subject.role ==

# Valid standard CEL, but `matches`/regex is deferred under Profile v1 —
# UnsupportedFeature, not a syntax error.
when: subject.namespace.matches("^Acme\\.Sales\\.")

# Valid standard CEL, but arithmetic is deferred under Profile v1.
when: subject.metadataText["priority"] + 1 > 5
```

### Anti-patterns and corrected alternatives

```yaml
# Bad: trivially true for almost every candidate. Adds no narrowing and
# defeats the point of a selector — will surface as a broad-match signal in
# coverage/explain output.
layers:
  everything:
    selector:
      role: DomainLayer
      when: "true"

# Corrected: express the actual constraint, or drop `when` and rely on the
# literal role/metadata selector alone if no expression is needed.
layers:
  sales_domain:
    selector:
      role: DomainLayer
      metadata:
        domain: Sales
```

```yaml
# Bad: an unguarded map lookup. Types without a "domain" key fail the run
# with an evaluation error instead of simply not matching.
when: subject.metadataText["domain"] == "Sales"

# Corrected: guard the lookup.
when: >
  subject.metadataText.containsKey("domain")
  && subject.metadataText["domain"] == "Sales"
```

```yaml
# Bad: an exclude selector with `when: true` is a blanket escape hatch —
# it excludes everything, silently hiding the contract's real boundary.
exclude:
  - role: DomainLayer
    when: "true"
```

## Diagnostics and troubleshooting

### Error categories

CEL diagnostics fall into four categories, reported with a stable
`CelDiagnosticCode` and (where applicable) a source span:

| Category | When it happens | Example |
|---|---|---|
| **Parse (`SyntaxError`)** | The expression text is not valid CEL at all. | `subject.role ==` (incomplete expression) |
| **Unsupported feature (`UnsupportedFeature`)** | The expression is valid standard CEL, but uses a feature Profile v1 defers (regex, arithmetic, `? :`, etc). | `subject.namespace.matches(...)` |
| **Type/binding (`TypeMismatch`, `BindingError`, `SchemaMismatch`)** | The expression references an unknown variable/member/function, or types don't line up (e.g. comparing `Int` to `Float`, or the predicate result isn't `Bool`). | `subject.metadata.domain` (unknown member — the schema is `metadataText`/`metadataBool`, not `metadata`) |
| **Evaluation (`EvaluationFailure`)** | The expression compiles, but fails at evaluation time for a specific candidate — typically a missing map key or out-of-range list index. | `subject.metadataText["domain"]` when `domain` is absent |

Parse, unsupported-feature, and type/binding diagnostics are **compile-time**:
they fail policy loading before any candidate is checked, for both strict and
audit contracts. Evaluation diagnostics happen **per candidate** at validation
time and are reported as a blocking configuration/evaluation error — never
silently treated as "no match," and never suppressed by a baseline.

### Source-span and YAML-location reporting

Every diagnostic message names the owning contract, the exact YAML path of the
`when` field (e.g. `contracts.strict_context_dependencies[0].forbidden[0]`),
and the expression's own source text — so a failure is traceable back to the
authored YAML without needing to reproduce it.

### `when_expressions` in JSON and SARIF output

When one or more `when`-bearing selectors participate in a context-dependency,
context-allow-only, or layout-convention violation, `--format json` includes a
`when_expressions` array on that violation. A single violation can carry more
than one entry — for example both `source.when` and the matched
`forbidden[*].when`:

```json
{
  "when_expressions": [
    {
      "location": "source",
      "source": "source.metadataText[\"tier\"] == \"premium\"",
      "result": "matched",
      "yaml_path": "contracts.strict_context_dependencies[0].source"
    },
    {
      "location": "forbidden",
      "source": "target.metadataText[\"domain\"] != source.metadataText[\"domain\"]",
      "result": "matched",
      "yaml_path": "contracts.strict_context_dependencies[0].forbidden[0]"
    }
  ]
}
```

`location` identifies which selector the expression came from (`source`,
`forbidden`, `allowed`, or `files_matching`). `result` is one of `matched`,
`not_matched` (a context-allow-only near-miss — a candidate's literal
`role`/`metadata` matched an allowed selector but its `when` evaluated
`false`), or `evaluation_failed`. The field is omitted entirely when no
`when`-bearing selector was involved — existing JSON output for literal-only
policies is unaffected. `--format sarif` adds the same information as one
related location per expression on the result.

### `explain` and CEL

The `explain` CLI verb reports CEL expression participation for hops on its
resolved path that come from a `when`-bearing contextual contract:

```
$ arch-linter-net explain --source Acme.Sales.Checkout --target Acme.Inventory.StockItem --policy architecture/arch.yml
Acme.Sales.Checkout -> Acme.Inventory.StockItem
Contract IDs: cross-domain-when
  [cross-domain-when] when: target.metadataText["domain"] != source.metadataText["domain"] (matched)
```

`--format json` adds the same data as an `expressionParticipation` array.

### Narrowing an overly broad predicate

If a coverage report or `explain` flags a selector as broad or stale, tighten
`when` (or the underlying `role`/`metadata` selector) rather than leaving a
`when: "true"`-shaped predicate in place — see the anti-patterns above.

## AI authoring guidance

Agents authoring or editing `when` expressions should follow the same public
contract this page documents — see
[AI policy authoring guide: CEL When Predicates](../ai/policy-authoring-guide.md#cel-when-predicates)
for agent-specific instructions (don't invent operators, don't weaken policy
to pass generated code, prefer literal selectors when they suffice). That
guidance intentionally uses the same terminology, profile version, and
examples as this page — there is no separate, hidden AI-only convention.

## See also

- [Layers and namespace patterns](layers-and-namespaces.md#selector-when-predicates)
- [Contextual dependency contracts](../contracts/context-dependency.md#cel-predicates)
- [Contextual allow-only contracts](../contracts/context-allow-only.md#cel-predicates)
- [Layout convention contracts](../contracts/layout-conventions.md)
- [YAML schema reference](../reference/yaml-schema.md)
- [Supported capabilities and non-goals](supported-capabilities.md)
- [Official CEL Language Definition](https://github.com/cel-expr/cel-spec/blob/59505c14f3187e6eb9684fbd3d07146f614c6148/doc/langdef.md)
