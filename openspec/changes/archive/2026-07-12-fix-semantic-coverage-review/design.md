## Context

The first semantic coverage implementation is complete but review found that its public contract schema and edge-case matching do not yet align with runtime semantics.

## Goals / Non-Goals

**Goals:** align public schema, validate semantic scope configuration, preserve combined layer matching, surface metadata failures, and test every corrected branch.

**Non-Goals:** add new classification sources or alter non-semantic coverage scopes.

## Decisions

- Reuse the runtime layer matcher for concrete semantic facts so namespace and selector conditions remain AND-combined.
- Surface metadata failures in the existing unknown semantic evidence bucket, alongside conflicts.
- Encode semantic syntax in the public JSON Schema and validate semantic roots using the existing namespace-root matcher shape.

## Risks / Trade-offs

- [Risk] Stricter validation can reject previously accepted malformed semantic policies. → The rejected forms had no defined behavior and now receive actionable errors.
