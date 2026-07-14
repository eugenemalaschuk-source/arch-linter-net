## Context

Specialized classification diagnostics bypass the common ordering invariant.
Generated template contracts preserve only a display name, which is ambiguous.

## Goals / Non-Goals

**Goals:** preserve source order and exact template ownership; share JSON mapping.

**Non-Goals:** alter template schema, IDs, or import behavior.

## Decisions

Carry a stable template owner identifier into generated contracts and resolve it
against the authored template. Reuse one policy-location dictionary mapper.

## Risks / Trade-offs

The new generated owner field is internal execution metadata and does not affect YAML DTOs.
