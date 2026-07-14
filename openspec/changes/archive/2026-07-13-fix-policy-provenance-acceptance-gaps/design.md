## Context

The provenance side index records locations by deserialized object reference.
This is appropriate for regular contract families, but layer-template expansion
creates executable `ArchitectureLayerContract` instances that do not share the
template reference. Separately, the CLI catches policy loading exceptions after
the normal violation formatter has been bypassed.

## Goals / Non-Goals

**Goals:**

- Retain a provenance owner when a layer template expands into executable
  contracts.
- Render typed import and policy-validation diagnostics consistently at the
  CLI boundary for human, JSON, and SARIF formats.
- Keep primary/related location order based on composer encounter order.

**Non-Goals:**

- Changing import grammar, graph limits, effective-policy schema, or contract
  DTOs.
- Reworking cycle-string diagnostics into a new typed model.
- Refactoring unrelated contract families or reporting formats.

## Decisions

1. Bind each expanded layer contract to its originating template in the side
   index. This is the smallest local adaptation of the existing identity-based
   design and keeps filesystem provenance out of executors and DTOs. Replacing
   catalog entries with a new wrapper was rejected because it changes common
   catalog APIs for a single synthetic family.
2. Add a format-aware CLI error formatter that consumes the typed diagnostic on
   `ArchitecturePolicyImportException` and `ArchitecturePolicyValidationException`.
   Human output retains the existing error message plus policy/root context;
   JSON and SARIF expose a versioned policy-error representation rather than
   serializing exception text.
3. Record composed encounter ordinal in the provenance index and use it as the
   sole stable related-location ordering key. YAML paths remain display values,
   not order keys. Composition conflicts use the original declaration as the
   primary location and the conflicting declaration as related.

## Risks / Trade-offs

- [Template binding misses an expansion path] → route every strict/audit
  expansion through one helper and cover runtime, consistency, reporting, and
  Testing adapter paths.
- [Machine error output compatibility] → use a distinct `architecture_policy_error`
  kind and retain human error text.
- [Ordering differs from legacy snapshots] → update only provenance-specific
  assertions and validate deterministic encounter order with double-digit
  collection indices.
