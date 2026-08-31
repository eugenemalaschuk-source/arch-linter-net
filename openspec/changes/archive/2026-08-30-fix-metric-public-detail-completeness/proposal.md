## Why

Exact public API snapshot details may silently fall back when reflection metadata is unavailable.
Measure-first public-surface metrics must not report a trusted count if any selected exact signature
is incomplete.

## What Changes

- Propagate detail-enrichment reflection failures into the existing cached public-surface
  completeness signal.
- Keep best-effort legacy validation output unchanged while public-surface metrics fail closed.
- Add a regression for an unavailable custom attribute on a public value type.

## Capabilities

No capability specification delta is required. The existing metric-semantics contract already
requires complete selected public-surface evidence; this correction carries complete exact-detail
evidence through its implementation.

## Impact

- Internal Core public API signature detail helpers, scanner, and Core test fixture.
- No public API, policy syntax, report schema, or legacy validation result changes.
