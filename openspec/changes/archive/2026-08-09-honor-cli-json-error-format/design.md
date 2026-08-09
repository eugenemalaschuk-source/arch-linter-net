## Context

The CLI currently lets each command handler decide how to report an early configuration, policy-load, or build-state failure. Several successful JSON projections are already strongly typed, while a subset of early returns writes human text to stderr regardless of the requested format. The affected handlers are the validation, baseline, policy-check, graph, explain, and public-API command families.

## Goals / Non-Goals

**Goals:**

- Emit exactly one JSON document to stdout when the affected baseline or public-API command terminates with an owned error after selecting JSON.
- Use a common, versioned error envelope with a stable category and a typed details object on those newly unified paths, preserving normalized policy and build-state diagnostic evidence where it exists.
- Keep existing success JSON payloads, human text, and exit codes unchanged.

**Non-Goals:**

- Change Core policy, validation, or build-state semantics.
- Convert command-line parser failures that occur before a command selects JSON format.
- Add JSON support to commands or formats that do not already expose it.
- Alter SARIF behavior beyond existing command-specific contracts.

## Decisions

### Shared CLI error formatter at handler boundaries

The baseline and public-API handlers will delegate their previously unstructured JSON failure rendering to one internal formatter rather than reimplementing serialization per early return. The envelope will have `schema_version`, `status`, `kind`, and an `error` object with a stable category, human-readable message, and typed diagnostic details when available. Existing validation, policy-check, graph, and explain serializers remain unchanged because they already return structured JSON failures.

Alternatives considered:

- Expand each successful command result model with error fields. Rejected because the output shapes are intentionally distinct and this would create broad, unrelated schema changes.
- Emit only a serialized message. Rejected because consumers need a stable category and owned policy/build-state evidence without parsing display text.

### Format decides stream ownership

For the legacy `--format json` path, the JSON error document is written to stdout; human errors remain on stderr. Commands retain their established exit code (normally `2`) for the same failure. This matches the existing one-document JSON convention and avoids contaminating stdout with prose.

### Preserve typed source diagnostics additively

When the exception or outcome supplies policy-location, normalized diagnostic, or build-state-preflight data, the shared formatter will expose it as structured details. Generic failures remain representable with their category and message; the formatter will not attempt to infer unsupported diagnostic fields.

## Risks / Trade-offs

- [Risk] A shared formatter can accidentally alter a command's existing success payload. → Mitigation: introduce it only on error paths and retain all existing success renderers.
- [Risk] An early return may be missed during the command-family audit. → Mitigation: enumerate all `--format json` handlers, add a parsed-output regression for each relevant error class, and inspect remaining direct stderr returns.
- [Risk] Consumers depend on current human diagnostics. → Mitigation: leave the human rendering and exit codes at their current call sites.

## Migration Plan

This is additive for JSON error consumers and requires no data migration. Rollback consists of reverting the CLI formatter and handler routing changes; no persisted artifacts or configuration files are modified.

## Open Questions

None. The current typed policy and build-state diagnostics define the evidence fields that can be preserved.
