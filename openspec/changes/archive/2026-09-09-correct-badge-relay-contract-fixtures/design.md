## Context

The review found divergences between the v1 design fixtures and the current
CLI's serialized wire bytes, JWT protocol boundaries, and the ADR cache text.
See proposal.md for motivation and the existing Relay contract spec for scope.

## Goals / Non-Goals

**Goals:** preserve current producer-byte compatibility and make each reviewed
security/cache invariant executable with a single clear failure cause.

**Non-Goals:** changing the shipped CLI serializer, adding a Relay runtime, or
expanding the private publication product surface.

## Decisions

- `headline-only/v1` uses the current default `System.Text.Json` escaped bytes;
  this avoids an incompatible hidden serializer migration.
- JWT vectors separate protected JOSE headers from payload claims, include
  `nbf`, and use a 10-minute-or-less lifetime except in a time-specific test.
- Ready caching uses `public, max-age=N, must-revalidate`; non-ready responses
  use `no-store`. The ETag hashes a deterministic complete representation that
  includes generation/state/validity as well as the headline bytes.
- Health, not Gate, owns the color mapping in all profile schemas and vectors.

## Risks / Trade-offs

- [Producer serialization changes later] → introduce a new profile version and
  explicit promotion/digest migration rather than reinterpreting v1 bytes.
- [ETag implementation variance] → vectors assert the representation inputs,
  not a particular hash algorithm or digest value.
