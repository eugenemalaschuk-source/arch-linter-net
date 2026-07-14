## Context

ArchLinterNet evaluates architecture policies defined in YAML. The existing semantic model (issues #107, #111, #112) provides role and metadata-based selectors that match types by their discovered classification. Selectors today support four deterministic metadata operators — `exact`, `any` (`*`), `in` (array), and `not-equal-to-source` (`!{source.metadata.<key>}`) — applied to individual metadata keys.

Real policies occasionally need compound conditions that the four-operator system cannot express without enumerating every concrete case: testing two metadata keys together, restricting by namespace prefix within a selector, or expressing conditions that depend on properties the four-operator system cannot compose. CEL (Common Expressions Language) provides a safe, sandboxed, boolean-predicate surface designed precisely for embedded evaluation without host I/O or scripting.

This design document establishes the YAML shape, expression contexts, validation contract, and interaction rules before issue #163 begins implementing the runtime engine. Issue #166 (CEL dependency spike, currently open) must validate the implementation strategy; its conclusions feed into #163 but do not change the YAML shape locked here.

The existing policy schema is declared with `additionalProperties: false` throughout. Any new `when` field requires an explicit JSON schema change. Per Decision 8, that schema change is **deferred to #163** — this design task ships no live schema modification.

---

## Goals / Non-Goals

**Goals:**

- Define the `when` YAML field and its allowed locations precisely.
- Define two typed input contexts (source-position and target-position) with their complete variable sets.
- Define the policy-load validation contract (what is guaranteed at load time vs. what may fail at evaluation time).
- Identify every YAML field where CEL is allowed and every field where it is explicitly forbidden.
- Document backward compatibility: existing policies without `when` are unaffected.
- Document interaction with semantic layer selectors (#111) and contextual contracts (#112).
- Explicitly state that coverage contracts, graph traversal, path-based facts, and external lookups are out of scope for this wave.
- Include a modular-monolith example and a Unity/client-style example.
- Include negative examples (expressions that are stale, too broad, or otherwise problematic).
- Record open implementation questions that #166 and #163 must resolve.

**Non-Goals:**

- Implementing runtime CEL evaluation (deferred to #163).
- Adding `when` to the live JSON schema as an accepted field (deferred to #163; no silently-ignored schema acceptance in this wave).
- Selecting the .NET CEL library or finalizing the function whitelist (deferred to #166).
- CEL expressions in coverage contracts (`strict_coverage`, `audit_coverage`).
- CEL expressions in baseline, graph-export, or explain-command outputs beyond what #163 designs.
- Graph traversal, path-based source-file facts, or external service lookups as expression inputs.
- A warning for `when: "true"` (redundant no-op under AND semantics, but not a policy-weakening; deferred if needed).
- Replacing existing `role`, `metadata`, or the four metadata operators.

---

## Decisions

### Decision 1: `when` is additive — AND-combined with `role` and `metadata`

A selector with `role`, `metadata`, and `when` matches a type only if ALL three conditions hold: the type's resolved role equals the declared `role`, every `metadata` constraint passes under its operator, and the CEL expression returns `true`. `when` does not replace existing constraints; it augments them.

*Alternative considered*: `when` as a replacement for `metadata` constraints. Rejected because it would require migrating all existing four-operator `metadata` uses to CEL syntax, breaking existing policies. AND-composition preserves every existing policy unchanged.

*Alternative considered*: `when` overriding `role`/`metadata` when present (i.e., acting as a full selector predicate). Rejected because it removes the schema-structural guarantee that every selector declares a role — removing that guarantee makes coverage stale-selector detection harder and makes selectors less readable.

### Decision 2: `when` is allowed only in selector positions in this wave

`when` is valid exactly in:

1. `layers.<name>.selector.when` — the semantic layer selector for a named layer.
2. The `source`, `forbidden`, `allowed`, and `exclude` selector objects within `strict_context_dependencies`, `audit_context_dependencies`, `strict_context_allow_only`, and `audit_context_allow_only` contract entries.

`when` is **explicitly forbidden** in every other YAML location, including but not limited to:

| Field / Section | Why CEL is forbidden |
|---|---|
| `classification.*` | Extraction syntax; all values are literal patterns or type names |
| `layers.<name>.namespace` | Literal namespace prefix/glob |
| `layers.<name>.selector.role` | Literal role name string |
| `layers.<name>.selector.metadata` (values) | scalarMap with literal values only |
| `contracts.strict.*` / `contracts.audit.*` (non-contextual) | Existing namespace/layer family; no selector object |
| Any contract `reason` | Human-readable explanation; always literal |
| Any contract `name` or `id` | Literal identifiers |
| `ignored_violations.*` | Literal source/target type matchers |
| `analysis.*` | Configuration; always literal |
| `packages`, `external_dependencies`, `legacy_runtime_layers` | Literal names and prefixes |

*Rationale*: CEL is a narrow extension to selector matching. Making it available everywhere string fields appear would make literal strings ambiguous — a reader could not distinguish intent without evaluating every field. Explicit, schema-typed `when` fields are the only CEL surface.

### Decision 3: Two distinct input contexts

**Source-position context** — applies when `when` appears on the `source` selector or on `layers.<name>.selector`:

| Variable | Type | Description |
|---|---|---|
| `type.name` | `string` | Fully-qualified type name |
| `type.namespace` | `string` | Declared namespace |
| `type.assembly` | `string` | Assembly simple name |
| `type.role` | `string` | Resolved role, or `""` if unclassified |
| `type.metadata` | `map(string, dyn)` | Resolved metadata key/value pairs |

**Target-position context** — applies when `when` appears on `forbidden`, `allowed`, or `exclude` selectors:

All of the above as `type.*`, plus:

| Variable | Type | Description |
|---|---|---|
| `source.name` | `string` | Matched source type's fully-qualified name |
| `source.namespace` | `string` | Matched source type's namespace |
| `source.assembly` | `string` | Matched source type's assembly simple name |
| `source.role` | `string` | Matched source type's resolved role |
| `source.metadata` | `map(string, dyn)` | Matched source type's resolved metadata |

`source.*` is available on target-position selectors to enable the same cross-type comparisons that `!{source.metadata.<key>}` expresses in the four-operator system (e.g., `type.metadata['module'] != source.metadata['module']`). It is not available on source-position selectors because there is no "other" type to reference.

*Alternative considered*: Exposing the dependency graph (all dependents/dependencies of the type). Rejected — graph traversal would require graph-index coupling, makes expressions order-dependent, and violates the static-analysis-only product boundary.

*Alternative considered*: Exposing source file path facts. Rejected for this wave — path-based facts are defined by #171 and will be designed as a separate extension point. Introducing them here without #171's reviewed design would produce conflicting contracts.

### Decision 4: `when` must return `bool` — non-boolean result is a load-time error where statically inferrable

The expression in a `when` field MUST evaluate to `bool`. Where the CEL type-checker can statically infer a non-boolean result type, the policy-load validation MUST reject the expression with a configuration diagnostic.

Where the result type cannot be statically inferred (e.g., a `dyn`-typed sub-expression), the type failure surfaces at evaluation time rather than load time. This is a known limitation of CEL's `dyn` type, not a design flaw — it is documented as a possible evaluation-time failure rather than promised as a load-time guarantee.

### Decision 5: Load-time vs. evaluation-time validation split

**Guaranteed at load time (parse + type-check phase):**
- CEL parse errors (syntax errors in the expression string).
- References to variables outside the declared context for the selector position (e.g., `source.role` in a source-position `when` fails because `source` is not declared there).
- Calls to functions not in the approved whitelist.
- Non-boolean result type where statically inferrable.

**May occur only at evaluation time:**
- Type mismatches involving `dyn`-typed map values (e.g., `type.metadata['key'] > 5` — the key may be absent or hold a non-numeric value).
- Missing metadata keys accessed by subscript (produces `dyn` null-equivalent in CEL, not a type error).

Evaluation-time failures MUST produce a deterministic configuration diagnostic for the type that triggered the failure. They MUST NOT silently pass the predicate or silently suppress the type from matching.

### Decision 6: Function whitelist is an open implementation decision — deferred to #166

The safe set of CEL built-in functions is not locked by this design. The following are **candidate allows** (subject to #166 confirmation):

- String: `startsWith`, `endsWith`, `contains`, `size`
- Map: `containsKey`, `keys`
- List: `in`, `size`, `all`, `exists`, `filter`
- Logical and comparison operators: `&&`, `||`, `!`, `==`, `!=`, `<`, `>`, `<=`, `>=`, ternary `? :`

The following are **candidate excludes** regardless of library support:

- `timestamp()`, `duration()` — time-based predicates are non-deterministic across environments.
- Host-extension functions — no file system, network, process, reflection, or runtime plugin access.
- `type()` — runtime type introspection.

`matches()` / RE2 regex: whether this is included depends on whether the chosen .NET CEL library supports RE2 semantics deterministically. This is an **open question for #166** — see Open Questions.

*Rationale for deferring whitelist*: The YAML shape and contexts are library-agnostic. Prematurely locking a whitelist that the chosen library cannot implement would require either a design revision or accepting a library that does not match the spec. #166 resolves the library question; #163 locks the whitelist in its implementation spec.

### Decision 7: `when` omission is equivalent to `when: "true"`

A selector without `when` behaves exactly as it does today. The `when` field is optional; its absence does not change any existing behavior. This preserves full backward compatibility for every existing policy.

Under AND semantics, `when: "true"` is a valid but redundant no-op. It is not treated as a policy weakening (it adds no selectivity, but also removes none). No diagnostic is produced for `when: "true"` in this wave.

### Decision 8: No live JSON schema changes in this wave — `when` deferred to #163

The live `schema/dependencies.arch.schema.json` MUST NOT add a `when` field in this design task. Adding `when` as a schema-accepted field before the runtime evaluates it would allow policy authors to write `when` expressions that are silently accepted and silently ignored — a form of schema lying that previous design decisions (e.g., `path` and `overrides` in `classification`) have already created technical debt around.

The `when` field exists in this document as a specification of what the schema will contain once #163 ships evaluation. The JSON schema addition is part of #163's scope, not this design task.

### Decision 9: Existing policies are fully backward compatible

Adding `when` to a selector shape (once #163 ships) is purely additive: it is optional, defaults to absent (no predicate), and has no effect on selectors that do not declare it. Any existing policy file is valid after the schema change without modification.

---

## YAML Shape

### Layer selector with `when`

```yaml
layers:
  sales_domain:
    namespace: MyCompany.Product.Modules.Sales          # still required
    selector:
      role: DomainLayer                                  # still required
      metadata:                                          # still exact-match operators
        module: Sales
      when: "type.namespace.startsWith('MyCompany.Product.Modules.Sales') && type.metadata.containsKey('version')"
      # when is AND-combined: type must match role AND metadata AND this predicate
```

### Contextual dependency contract with `when`

```yaml
contracts:
  strict_context_dependencies:
    - id: domain-context-isolation
      name: Domain contexts must not depend across bounded contexts
      source:
        role: DomainLayer
        metadata:
          module: "*"
        when: "type.metadata.containsKey('module')"    # source-position: only type.* available
      forbidden:
        - role: DomainLayer
          when: "type.metadata['module'] != source.metadata['module']"
          # target-position: type.* and source.* both available
          # equivalent to the four-operator: metadata: { module: "!{source.metadata.module}" }
      exclude:
        - role: SharedKernel
      reason: Bounded-context domain types must not depend on other context domain types.
```

### Contextual allow-only contract with `when`

```yaml
contracts:
  strict_context_allow_only:
    - id: sales-domain-allowed-dependencies
      name: Sales domain may depend only on its own module or SharedKernel
      source:
        role: DomainLayer
        metadata:
          module: Sales
      allowed:
        - role: DomainLayer
          when: "type.metadata['module'] == source.metadata['module']"   # same context only
        - role: SharedKernel
      reason: Sales domain types may depend only on their own module or the shared kernel.
```

---

## Examples

### Modular-monolith: CEL augmenting contextual selectors

```yaml
# Issue #169 shape — cross-domain domain isolation
strict_context_dependencies:
  - id: domain-context-isolation
    name: Domain contexts are isolated
    source:
      role: DomainLayer
      metadata:
        module: "*"
      when: "type.metadata.containsKey('module')"
    forbidden:
      - role: DomainLayer
        when: "type.metadata['module'] != source.metadata['module']"
    exclude:
      - role: SharedKernel
    reason: Bounded-context domain types must not reference other context domain types.
```

### Unity/client: CEL on a layer selector to guard by namespace prefix

```yaml
layers:
  runtime_domain:
    namespace: MyGame.Runtime
    selector:
      role: RuntimeLayer
      when: "!type.namespace.contains('.Editor') && !type.namespace.contains('.Tests')"
```

### Negative example: stale `when` condition (metadata key never populated)

```yaml
# PROBLEMATIC: type.metadata['tier'] is never set by classification
# → selector matches zero types → stale selector diagnostic from #114
layers:
  core_types:
    selector:
      role: CoreLayer
      when: "type.metadata['tier'] == 'core'"   # 'tier' key not produced by any classification entry
```

### Negative example: expression that references an unavailable variable

```yaml
# INVALID at load time: source.* is not available in a source-position when
strict_context_dependencies:
  - source:
      role: DomainLayer
      when: "source.metadata['module'] == 'Sales'"   # ERROR: source.* not in source-position context
    forbidden:
      - role: InfrastructureLayer
    reason: Example of a load-time error.
```

### Negative example: expression with unsupported function

```yaml
# INVALID at load time if the function is not whitelisted
layers:
  external_only:
    selector:
      role: ExternalLayer
      when: "type.name.toLower().startsWith('external')"  # toLower() — not in candidate whitelist
```

---

## Interaction with Existing Features

### Semantic layer selectors (#111)

`layers.<name>.selector` gains an optional `when` field. The evaluation order is: namespace constraint (if present) → `role` match → `metadata` match → `when` predicate. All must pass. Coverage stale-selector detection (#114) continues to fire when the combination of `role`/`metadata`/`when` matches zero types.

### Contextual contracts (#112)

The four existing metadata operators (`exact`, `any`, `in`, `not-equal-to-source`) remain on the `metadata` map of every selector. `when` is an additional optional predicate ANDed with those operators. Authors can use `when` to express conditions the four operators cannot (multi-key, string-function, computed), or can continue to use the four operators for simple cases. Both are simultaneously valid on the same selector.

### Coverage diagnostics (#114)

CEL `when` does not introduce new coverage concepts. A layer selector with `when` that matches zero types produces the same stale-selector diagnostic it would produce without `when`. Coverage contract evaluation is unaffected — `when` is not available on coverage contract entries in this wave.

### Baseline

CEL-backed violations are suppressed by baseline using the existing `source_type`/`forbidden_reference` keying, identical to all other contract families. The CEL expression that produced the violation is not part of the baseline key.

### JSON output / explain

When a violation was produced by a contract entry that declares a `when` expression, the JSON diagnostic SHOULD (per #163's design) include the expression string. The input context values at evaluation time MAY be included in explain-mode output. These are implementation details for #163, not locked by this design.

### Strict / audit modes

`when` expressions behave identically in strict and audit. The strict/audit distinction controls whether a violation fails the build; it does not change which types match the selector.

---

## Risks / Trade-offs

**Risk: `dyn`-typed metadata map produces evaluation-time failures invisible at load time.**
→ Mitigation: Documented in Decision 5 and spec. Evaluation-time failures produce deterministic diagnostics. Authors should test expressions against real metadata by running analysis with verbose output before committing expressions to a strict contract.

**Risk: Function whitelist not finalized before #163 begins.**
→ Mitigation: #163 is explicitly blocked on #166 for the whitelist decision. The YAML shape and contexts are locked independently. #163 should not begin implementation of `when` evaluation until #166 closes.

**Risk: Authors write `when` in live policy files expecting runtime evaluation before #163 ships.**
→ Mitigation: Per Decision 8, `when` is not in the live JSON schema until #163 adds it. Schema validation will reject any policy that uses `when` until #163 ships. No silent acceptance.

**Risk: `source.*` variables on target-position `when` create a more complex mental model than the `!{source.metadata.key}` operator.**
→ Mitigation: The four existing operators are simpler for the cases they cover. Authors should prefer them for simple not-equal-to-source cases and use `when` only when needed. Documentation and examples should emphasize this.

**Trade-off: AND-only composition means `when` cannot be used to widen a selector (OR with `role`/`metadata`).**
→ This is intentional: selectors that widen by CEL would make static stale-selector detection unreliable and would make policy intent harder to read. Widening beyond what `role`/`metadata` covers should use a separate contract entry with a different selector.

---

## Open Questions

1. **`matches()` and RE2 regex**: Should CEL `matches()` (RE2 semantics) be included in the approved function whitelist? *Blocked on #166.* Risk: RE2 compile errors should produce load-time failures; large patterns could affect policy-load performance. Decision authority: #166 spike, then #163 implementation spec.

2. **Final function whitelist**: Exact set of allowed and forbidden CEL built-in functions. *Blocked on #166.* Candidate allows and excludes are documented in Decision 6.

3. **Evaluation-time failure behavior**: When a `when` predicate throws an evaluation-time error (e.g., `type.metadata['key']` is absent and the expression performs arithmetic on it), should the type be treated as non-matching (selector fails closed) or as an error diagnostic? *Decision for #163.* Recommendation: fail closed with a deterministic diagnostic — treating an evaluation failure as a pass would silently weaken the policy.

4. **JSON diagnostic schema for `when`**: What fields should appear in the JSON violation output when a `when` expression contributed to a match? *Decision for #163.*

5. **`when` on `exclude` selectors in contextual contracts**: Should `exclude` selectors support `when`? The current design allows it (exclude uses `contextSelector` which would gain `when`). Confirm during #163 schema authoring.
