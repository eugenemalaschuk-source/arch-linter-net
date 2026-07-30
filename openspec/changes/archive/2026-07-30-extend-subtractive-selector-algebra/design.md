## Context

#356 delivered subtraction for declared layers; contextual selectors and coverage
already have their own compatible exclusion semantics. #404 reconciles the
remaining families after reusable source-set expansion (#369) introduced a
second bounded selection seam.

## Goals / Non-Goals

**Goals:**

- Use `effective = union(includes) - union(excludes)` only where the selected
  universe is already materialized and exclusion cannot add analysis inputs.
- Keep exact legacy forms intact, deterministic, and provenance-preserving.
- Report effective participation through the existing typed finding, explain,
  and coverage infrastructure.

**Non-Goals:**

- Boolean selector trees, baseline generation, or widening configured inputs.
- Retrofitting subtraction into exact identifiers that are not selectors.

## Decisions

### Compatibility inventory

| Family | Classification | Decision |
| --- | --- | --- |
| Direct layer selectors | Already supported | Keep #356 implementation and parity-test it. |
| Contextual selectors and coverage exclusions | Already supported | Preserve existing semantics and projections. |
| Layer-template containers | Compatible | Accept include/exclude containers before template expansion. |
| Type-placement selectors | Compatible | Add typed include/exclude matcher lists; subtract matched types after inclusion. |
| Layout conventions | Compatible | Add typed include/exclude file matchers; subtract files before `when` and expectation checks. |
| Package/framework/assembly sources | Compatible | Extend source expansion with bounded source exclusions after source/set expansion. |
| External/protected layer sources | Compatible | Use the same bounded layer source exclusion seam. |
| Reusable source sets | Compatible | Support ordered subtraction of the resolved set union without altering its universe. |
| Exact `forbidden`/`allowed` and placement expectation values | Intentionally incompatible | They are rule operands, not selection scopes; subtraction would change rule meaning. |

### Shared model at existing seams

Typed, family-local `exclude` declarations will reuse each family's existing
matcher and validation rules. A small internal resolver will receive ordered
included values and ordered excluded values, deduplicate ordinally, then return
the difference. It will record which exclusion items matched so stale evidence
can feed existing coverage/explain projections. This avoids a new untyped YAML
macro and keeps CEL evaluation in its current selector-specific call sites.

### Validation and provenance

The raw YAML shape validators and YamlDotNet models will reject unknown keys and
wrong exclusion shapes in roots and imports. Exclusions retain authored item
locations through the provenance index; expanded contracts retain their existing
authored origin plus effective source evidence.

## Risks / Trade-offs

- [Different matcher universes] → Keep exclusions family-local and test each
  resolution boundary rather than forcing a stringly generic selector.
- [Stale exclusions not observable for every fact kind] → emit typed evidence
  where a materialized fact universe exists; document intentionally unavailable
  cases rather than fabricating diagnostics.
- [Source expansion regression] → preserve the zero-exclusion code path and
  test exact/sources/source_sets combinations deterministically.
