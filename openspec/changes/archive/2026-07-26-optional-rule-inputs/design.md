## Context

`scope: rule_input` resolves the layer-bearing fields of selected contracts and currently reports each empty layer as `empty-input`. Existing `exclude` entries operate at contract granularity, making them too broad for a planned layer in a contract that also has populated inputs. The approved 0.5.1 compatibility contract requires an exact, schema-backed lifecycle state with provenance and equivalent projections.

## Goals / Non-Goals

**Goals:**

- Record intentional emptiness for exactly one contract field/layer input with a mandatory reason.
- Keep optional emptiness visible and typed rather than suppressing it as baseline debt.
- Preserve ordinary empty, stale, and unknown behavior for every other input.
- Carry the authored declaration location through structured output.

**Non-Goals:**

- Automatically treating external layers, zero-match source sets, or any undeclared input as optional.
- Replacing coverage baselines or current exclusion semantics.
- Implementing source-set or glob expansion from #369.

## Decisions

- **Use `optional_inputs` on the rule-input coverage contract.** Each item contains `contract_id`, `input`, `layer`, and `reason`. `input` is the layer-bearing field name and `layer` disambiguates collection-valued fields. This gives the required exact identity without applying an exception to unrelated inputs. A contract-level `exclude` remains available for its current broader semantics.
- **Validate identities at policy load.** The coverage validator will enumerate the selected contract's layer-bearing inputs and reject missing contract IDs, unknown field/layer pairs, duplicate declarations, or blank reasons. This fails closed before analysis and makes stale declaration errors actionable.
- **Represent the state in the normalized coverage summary.** Add a distinct optional-empty count and evidence collection containing the contract/input/layer identity, reason, and provenance. Human, JSON, SARIF, explain, and Testing API use this common state rather than parsing diagnostic text.
- **Keep optional inputs non-blocking only while empty.** An optional declaration does not suppress unresolved input, does not change a populated input's covered state, and does not alter other empty inputs.

Alternatives rejected: using `exclude` would suppress whole-contract coverage; annotating layers would make optionality global rather than input-specific; baselines would misrepresent a planned absence as debt and lose lifecycle visibility.

## Risks / Trade-offs

- **[Risk] Layer-bearing fields differ by contract family.** → Mitigation: centralize input enumeration next to the existing rule-input resolver and test representative scalar and list fields.
- **[Risk] Output schemas could drift.** → Mitigation: project the same summary model in every formatter and assert structured output in tests.
- **[Risk] Imported declarations lose source origin.** → Mitigation: reuse composed-policy source-location capture and test imported fragments.

## Migration Plan

The syntax is additive. Existing `empty-input` findings continue unchanged; authors replace only a deliberately planned empty input with an exact `optional_inputs` entry. Removing the declaration is optional after code appears because the input becomes covered automatically.

## Open Questions

None; the compatibility spec and issue acceptance criteria define the required lifecycle.
