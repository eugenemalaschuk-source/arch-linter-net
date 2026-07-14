## Context

Layer-template provenance is identity-based. The catalog owns the executable
expanded instances, so binding temporary expansion instances cannot work.
Policy exception output must match the established location schema and retain
both sides of a typed conflict in SARIF.

## Goals / Non-Goals

**Goals:**

- Bind catalog-expanded templates to their original template locations.
- Emit normalized JSON locations and SARIF related locations for policy errors.

**Non-Goals:**

- Change import grammar, contract DTOs, or non-policy SARIF results.

## Decisions

The catalog calls the provenance index as it materializes descriptors; the
index resolves an expanded template by its group and source template name.
CLI uses an explicit snake_case projection rather than serializing records.
SARIF emits primary and related physical locations from the typed diagnostic.

## Risks / Trade-offs

- [Duplicate template names in one group] → existing policy validation remains
  responsible for uniqueness; binding is limited to the matching group.
- [Schema drift] → CLI tests assert snake_case JSON and related SARIF output.
