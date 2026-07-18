# Contextual Dependency Contracts

Contextual dependency contracts check that a discovered `(role, metadata)` selector match does not
reference another discovered `(role, metadata)` selector match — directly, without declaring a
`layers.<name>` for either side.

Groups:

- `strict_context_dependencies`
- `audit_context_dependencies`

Use this family when a boundary is a business-context distinction (e.g. "Sales" vs. "Inventory")
rather than a fixed namespace or a hand-declared layer, and the boundary is discovered through
`classification.attributes`/`classification.assembly_attributes` role/metadata extraction. See
[Semantic classification](../policy-format/semantic-classification.md) for how role/metadata is
discovered, and the comparison table there for when to use this family instead of
`layers.<name>.selector`.

## Example

```yaml
contracts:
  strict_context_dependencies:
    - id: sales-no-inventory
      name: sales-must-not-depend-on-inventory
      source:
        role: DomainLayer
        metadata:
          domain: Sales
      forbidden:
        - role: DomainLayer
          metadata:
            domain: "!{source.metadata.domain}"
      exclude:
        - role: SharedKernel
      reason: Bounded contexts must not depend on each other's domain types.
```

This contract matches every type whose resolved role/metadata satisfies `source`, then flags any of
its direct references that match a `forbidden` selector — here, any other `DomainLayer` type whose
`domain` differs from the currently-checked source type's own `domain` (the `not-equal-to-source`
operator). A candidate that also matches an `exclude` selector (here, `SharedKernel`) is removed from
consideration before violation evaluation.

## Fields

| Field | Required | Meaning |
|---|---|---|
| `name` | yes | Human-readable contract name. |
| `id` | no | Stable identifier for `--contract`, baselines, and ignored-violation matching. |
| `source` | yes | A [contextual selector](../policy-format/semantic-classification.md#contextual-selectors-context_dependencies-context_allow_only) matched against candidate source types. |
| `forbidden` | yes, non-empty | A list of contextual selectors; a source-matching type's reference to any matching target is a violation. |
| `exclude` | no | A list of contextual selectors; a candidate target matching any entry is removed from consideration before violation evaluation. |
| `ignored_violations` | no | Same shape and semantics as the existing `dependency` family: `source_type`/`forbidden_reference`/`reason`, matched by concrete type name. |
| `reason` | no | Human-readable rationale surfaced in diagnostics and generated baselines. |

## Contextual selectors and metadata operators

Every `source`/`forbidden`/`exclude` entry is a **contextual selector**: `role` (required,
exact-match) plus an optional `metadata` map. Each metadata value is interpreted by exactly one of
four fixed operators, checked in this order:

| Form | Operator | Meaning |
|---|---|---|
| YAML sequence | `in` | Matches if the type's resolved value equals any listed entry. |
| `"*"` | `any` | Matches any resolved value, provided the key is present. |
| `"!{source.metadata.<key>}"` | `not-equal-to-source` | Matches when the candidate's resolved value for `<key>` differs from the *current source type's own* resolved value for `<key>`. Only meaningful on `forbidden`/`exclude` — a `source` selector has no other source to reference. |
| anything else | `exact` | Literal scalar match. |

A missing role, or a missing constrained metadata key, is a non-match — never an error. See
[Semantic classification](../policy-format/semantic-classification.md#metadata-extraction-syntax)
for how metadata values themselves are extracted and canonicalized.

## CEL predicates

Contextual `source`/`forbidden`/`exclude` selectors accept an optional `when`
field. `when` is additive to `role`/`metadata`: a candidate matches only if
the literal constraints already match *and* `when` evaluates to `true`.

`source.when` compiles against a `source` context (the same subject shape
used by layer selectors). `forbidden[*].when`/`exclude[*].when` compile
against a context exposing `source`, `target`, and `dependency` — enabling
cross-context comparisons such as:

```yaml
contracts:
  strict_context_dependencies:
    - name: sales-must-not-depend-on-other-domain
      source:
        role: Domain
      forbidden:
        - role: Domain
          when: target.metadataText["domain"] != source.metadataText["domain"]
      reason: Bounded contexts must not depend on each other's domain types.
```

- existing `role` and `metadata` entries remain literal; only the explicit
  `when` field carries a CEL predicate;
- a selector with no `when` behaves exactly as before this field existed;
- a `when` evaluation failure fails the run as a policy/configuration error —
  for both `strict_context_dependencies` and `audit_context_dependencies` —
  rather than being treated as a non-match or silently ignored, and is never
  suppressed by baseline;
- when a matching `forbidden` selector declared `when`, the violation's
  evidence names that expression's source text alongside the existing
  role/metadata evidence.

## Exclude vs. ignored_violations

`exclude` is a **pre-match filter on candidate targets** — a target matching an `exclude` selector
never becomes a violation candidate at all. `ignored_violations` is the existing **post-violation
suppression** mechanism, keyed on concrete source/target type names, unchanged from the `dependency`
family. Use `exclude` for a structural exemption (e.g. "SharedKernel is always allowed"); use
`ignored_violations` for narrow, named debt.

## Diagnostics

A contextual dependency violation's diagnostic carries the source type's resolved role and metadata,
the target type's resolved role and metadata, and `matched_selector: forbidden` — evidence a
namespace/layer `dependency` violation does not carry. Human output tags the finding
`(kind: context_dependency, ...)`; JSON/CI-artifact output includes `source_role`, `source_metadata`,
`target_role`, `target_metadata`, and `matched_selector` fields, so a contextual violation is always
distinguishable from an existing namespace/layer dependency violation.

## Strict vs. audit

Same semantics as every other family: `strict_context_dependencies` fails the build,
`audit_context_dependencies` reports without failing (though the CLI process itself still exits
non-zero for audit findings under `--mode audit` — see
[Exit codes](../usage/exit-codes.md); CI opts out with `continue-on-error: true`, not by relying on
a zero exit code).

## Baselines

`strict_context_dependencies`/`audit_context_dependencies` participate in baseline generation,
merge, and comparison identically to the `dependency` family — see
[Migration baselines](../guides/migration-baselines.md).
