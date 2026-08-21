## Context

Policy-context facts can be a scalar set, a boolean flag, a predicate, or a
structured object. A `forbidden_` or `allowed_only_in_` prefix therefore does
not establish set semantics by itself.

## Decisions

- Use explicit supported lists for string-set prohibition inventories,
  permission inventories, and governed-scope inventories.
- Treat only `forbidden_legacy_runtime` and `forbidden_editor_refs` as known
  boolean prohibitions: `true` to `false` is semantic weakening; `false` to
  `true` is strengthening.
- A changed scalar predicate or structured value without a dedicated
  direction rule, including `forbidden_name_suffix`, `forbidden_properties`,
  and `allowed_only_in_types`, produces bounded `impact_not_proven` evidence.
- Exclude a fact from the fallback only when the specific fact shape is
  handled by a dedicated comparator.

## Non-Goals

- Infer containment between scalar predicates or structured allow-list items.
- Broaden the semantic contract-family coverage beyond the explicit matrix.
