## Why

ArchLinterNet's existing `role`/`metadata` selectors express only equality-based matching against discovered semantic roles. Complex real-world policies — multi-key conditions, namespace-prefix guards, same-assembly exclusions — require combinations that the four-operator system cannot express without enumerating every concrete case. CEL (Common Expressions Language) provides a safe, deterministic boolean predicate surface that can augment selectors without introducing scripting, host I/O, or implicit template expansion. The design must exist and be agreed on before issue #163 implements the runtime engine.

## What Changes

- A new `when` field is designed for `layers.<name>.selector` and for the `source`/`forbidden`/`allowed`/`exclude` selector positions inside `strict_context_dependencies`, `audit_context_dependencies`, `strict_context_allow_only`, and `audit_context_allow_only`.
- `when` is AND-combined with existing `role` and `metadata` constraints; it does not replace them.
- Two typed expression contexts are defined: source-position (candidate `type.*` facts only) and target-position (`type.*` plus matched `source.*` facts).
- Every existing literal field (`reason`, `name`, `id`, `namespace`, `role`, attribute names, metadata extraction syntax, analysis configuration) remains literal and is never evaluated as CEL.
- A policy-load validation contract is defined: parse errors, unknown identifiers, unsupported functions, and statically-inferrable non-boolean results fail at load time; `dyn`-typed metadata access may only fail at evaluation time.
- `when` is **not** added to the live JSON schema until #163 implements evaluation. No production schema change ships with this design task.
- Coverage contracts, graph traversal, path-based facts, external lookups, and runtime diagnostics are explicitly excluded from this wave.

## Capabilities

### New Capabilities

- `cel-expression-model`: The complete CEL expression model: allowed and forbidden YAML locations, two typed input contexts (source-position and target-position), validation lifecycle (guaranteed load-time vs. possible evaluation-time), safe function constraints (whitelist deferred to #166), backward-compatibility rules, and interaction with semantic layer selectors and contextual contracts.

### Modified Capabilities

*(none — no existing spec requirements change; the `selector` and `contextSelector` schema shapes gain a new optional `when` field, but that addition is deferred to #163 and requires no spec delta here)*

## Impact

- **Design documentation**: new `design.md` in this change directory.
- **Spec**: new `openspec/specs/cel-expression-model/spec.md`.
- **JSON schema**: no live changes in this wave (deferred to #163 per locked decision 8).
- **Runtime**: no runtime changes (deferred to #163).
- **Existing policies**: zero impact — policies without `when` are fully compatible.
- **Dependencies**: final function whitelist and library selection depend on #166; runtime evaluation depends on #163.
- **Examples**: modular-monolith and Unity/client examples documented in `design.md`.
