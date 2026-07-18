# Contextual Allow-Only Contracts

Contextual allow-only contracts restrict a discovered `(role, metadata)` selector match's
references to an explicit `allowed` list of `(role, metadata)` selectors — whitelist semantics,
matching the existing `allow_only` family's shape but comparing discovered role/metadata directly
instead of resolving through a declared `layers.<name>`.

Groups:

- `strict_context_allow_only`
- `audit_context_allow_only`

See [Contextual dependency contracts](context-dependency.md) for the shared selector shape and
metadata operator vocabulary (`exact`, `any`, `in`, `not-equal-to-source`) — both contextual
families use the identical selector grammar.

## Example

```yaml
contracts:
  strict_context_allow_only:
    - id: sales-allow-only
      name: sales-may-depend-only-on-own-context
      source:
        role: DomainLayer
        metadata:
          domain: Sales
      allowed:
        - role: DomainLayer
          metadata:
            domain: Sales
        - role: SharedKernel
      reason: Sales may depend only on its own context or the shared kernel.
```

A type matching `source` may reference only types matching `allowed`; any other reference is a
violation.

## Fields

| Field | Required | Meaning |
|---|---|---|
| `name` | yes | Human-readable contract name. |
| `id` | no | Stable identifier for `--contract`, baselines, and ignored-violation matching. |
| `source` | yes | A contextual selector matched against candidate source types. |
| `allowed` | yes, non-empty | A list of contextual selectors; a source-matching type's reference to any *non*-matching, role-classified target is a violation. |
| `exclude` | no | A list of contextual selectors; a candidate target matching any entry is removed from consideration **before** `allowed`-list evaluation and before `ignored_violations`. |
| `ignored_violations` | no | Same shape and semantics as the existing `allow_only` family. |
| `reason` | no | Human-readable rationale surfaced in diagnostics and generated baselines. |

Only **role-classified** referenced types are considered violation candidates — an unclassified
type (framework/BCL types, primitives, etc.) can never match any selector and is not reported,
mirroring how the existing `allow_only` family only considers references already inside a declared
layer.

## CEL predicates

Contextual `source`/`allowed`/`exclude` selectors accept an optional `when`
field, evaluated the same way as the `context_dependencies` family: additive
to literal `role`/`metadata`, with `allowed[*].when`/`exclude[*].when`
compiling against a context exposing `source`, `target`, and `dependency` so
an `allowed` selector can restrict targets to the same context as their
source:

```yaml
contracts:
  strict_context_allow_only:
    - name: sales-same-domain-only
      source:
        role: Domain
      allowed:
        - role: Domain
          when: target.metadataText["domain"] == source.metadataText["domain"]
      reason: Sales may only depend on its own domain.
```

The rule stays the same as everywhere else in the model: existing selector
fields remain literal and only an explicit `when` field carries a CEL
predicate. A `when` evaluation failure fails the run as a policy/configuration
error — for both `strict_context_allow_only` and `audit_context_allow_only` —
rather than being treated as a non-match, and is never suppressed by
baseline. When no `allowed` selector matches but one came close (its literal
`role`/`metadata` matched and only `when` evaluated `false`), the violation's
evidence names that near-miss expression.

## Exclude vs. allowed vs. ignored_violations

Three mechanisms interact, applied in this order:

1. `exclude` — a pre-match filter; an excluded candidate is never evaluated against `allowed` at all.
1. `allowed` — a candidate surviving `exclude` that matches no `allowed` selector is a violation.
1. `ignored_violations` — post-violation suppression, keyed on concrete source/target type names.

## Diagnostics

A contextual allow-only violation's diagnostic carries the source type's resolved role and metadata
and the target type's resolved role and metadata — evidence a namespace/layer `allow_only` violation
does not carry. Human output tags the finding `(kind: context_allow_only, ...)`; JSON/CI-artifact
output includes `source_role`, `source_metadata`, `target_role`, and `target_metadata` fields.

## Strict vs. audit

Same semantics as every other family: `strict_context_allow_only` fails the build,
`audit_context_allow_only` reports without failing (the CLI process itself still exits non-zero for
audit findings under `--mode audit` — see [Exit codes](../usage/exit-codes.md)).

## Baselines

`strict_context_allow_only`/`audit_context_allow_only` participate in baseline generation, merge,
and comparison identically to the `allow_only` family — see
[Migration baselines](../guides/migration-baselines.md).
