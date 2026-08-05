## Context

`CreateSnapshot` owns policy composition, preflight, project planning, and
identity evidence. CLR assembly loading and session construction happen only
after `Evaluate(mode)` misses its cache lookup. A snapshot may therefore finish
with cache-only outcomes and no session.

## Decision

The owner specification will use one model throughout: one immutable plan, zero
or one lazy runner/session, and no repeated composition, discovery, planning, or
materialization across modes. The scenario will describe both cache-only and
post-miss behaviour instead of assuming a session exists from creation.
