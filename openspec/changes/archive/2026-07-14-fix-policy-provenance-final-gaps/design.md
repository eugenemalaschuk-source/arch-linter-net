## Context

Source ordinal alone cannot order nodes within one document. Exhaustive template validation currently occurs during expansion and must retain the template owner.

## Goals / Non-Goals

**Goals:** preserve node encounter order, complete exception metadata, and enrich template expansion failures.

**Non-Goals:** change template schema or import composition behavior.

## Decisions

Use the existing effective-node ordinal for stable ordering. Reuse the existing policy location JSON projection for all fields. Catch expansion validation where the source template is known and wrap it as a typed policy validation exception.

## Risks / Trade-offs

Existing human messages remain compatible; only duplicate provenance suffixes are suppressed when already present.
