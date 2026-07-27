## Context

The Core baseline application service already classifies comparison entries using
`ArchitectureViolationIdentity` and exposes command-specific outcomes. The CLI
currently renders those outcomes only as human-readable text or JSON, while the
Testing adapter is limited to validation requests. SARIF is currently a projection
of validation diagnostics only.

## Goals / Non-Goals

**Goals:**

- Reuse the existing Core comparison entries and identity model for every new
  projection.
- Make `diff`, `verify`, and `migrate` statuses machine-readable in SARIF and
  available as typed values to NUnit test code.
- Keep result ordering and identity serialization deterministic.

**Non-Goals:**

- Change existing human or JSON comparison output, baseline matching, or validate
  output.
- Add SARIF output for baseline writers (`generate`, `update`, or `prune`) or
  ordinary validation behavior to the new Testing comparison surface.

## Decisions

1. The Core comparison model remains the single source of identity and status.
   CLI, SARIF, and Testing will project it rather than reclassifying entries.
   This prevents divergent identity algorithms.
2. Baseline command format selection will accept `sarif` alongside existing
   human/JSON modes. A dedicated baseline-comparison SARIF formatter will emit
   one result per comparison entry with `baseline_status` and the canonical
   identity fields in result properties. This keeps the ordinary validation SARIF
   formatter and its exclusion rules unchanged.
3. The Testing adapter will add explicit `DiffBaseline`, `VerifyBaseline`, and
   `MigrateBaseline` operations returning a public typed comparison result. It
   will call the existing Core application service through the same composition
   boundary used by validation, preserving CLI semantics without a CLI dependency.

## Risks / Trade-offs

- [SARIF consumers expect conventional rule IDs] → use the stable contract ID as
  the rule ID and document additive baseline properties.
- [Comparison command outcomes differ slightly] → normalize their typed entries
  and statuses at the projection boundary, while retaining command-specific gate
  status (for example, `InSync` for verify).
- [Testing API could become a duplicate CLI] → expose only comparison operations
  in this issue and reuse Core request/outcome models.

## Migration Plan

The additions are opt-in: existing command formats and Testing validation calls do
not change. Consumers can request `--format sarif` for comparison commands or
call the new Testing methods. No persisted-format migration is required.

## Open Questions

None; the SARIF property names will use the established snake_case convention
already used by the formatter.
