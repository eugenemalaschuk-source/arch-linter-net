## Context

The normal architecture-diagnostic SARIF formatter already produces policy
evidence with portable source paths, source regions, role-and-path messages,
and composition encounter order. The policy-exception writer has a separate,
smaller mapper, so typed exceptions lose part of that evidence.

Policy-consistency diagnostics retain both participating contract names and
IDs. The provenance index currently combines the ID matches with unrestricted
name matches, which can select an unrelated contract that happens to share a
display name in another contract family.

## Goals / Non-Goals

**Goals:**

- Emit complete, ordered declaration evidence for typed policy exceptions in
  SARIF.
- Restrict consistency-diagnostic provenance to the contract IDs participating
  in the diagnostic whenever those IDs are present.
- Cover both regressions with focused NUnit tests.

**Non-Goals:**

- Change the human or CI JSON diagnostic schemas.
- Change contract DTOs, import-graph traversal, or policy composition.
- Redesign the SARIF result model.

## Decisions

### Reuse one policy-location SARIF mapper

Expose the existing policy-location mapper from `ArchitectureSarifFormatter`
and use it from the CLI policy-exception path. This small shared method solves
the concrete divergence between normal diagnostics and typed policy exceptions
without introducing a new formatter service or a second schema.

The mapper will include every available primary and related declaration in
`relatedLocations`, preserving the existing source-ordinal and encounter-order
ordering. It will continue to put the primary declaration in the result's
`locations` field, so existing consumers retain their primary location.

### Prefer diagnostic IDs for consistency provenance

When a consistency diagnostic has a primary ID or conflicting IDs, the index
will select entries only by that combined ID set. It will use the existing
name-based selection only for diagnostics that supply no participant IDs,
preserving provenance for programmatically created legacy diagnostics.

This makes the diagnostic's explicit identity the authority while retaining a
bounded compatibility fallback. No additional identity or index abstraction is
needed.

## Risks / Trade-offs

- The shared SARIF method becomes callable by the CLI, which is a narrow public
  API expansion in Core. It removes duplicate mapping logic and protects the
  two output paths from drifting again.
- Diagnostics without IDs still rely on names by necessity; loaded policies
  assign IDs before provenance binding, so the normal YAML path uses the
  precise branch.

## Migration Plan

No data or configuration migration is required.
