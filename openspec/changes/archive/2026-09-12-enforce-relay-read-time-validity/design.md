## Context

The Relay's private registry and atomic publication state already exist. The
worker routes a registered alias only to authenticated POST operations; public
GET/HEAD is intentionally rejected. `relay_state` already persists the exact
canonical bytes, profile, generation, status, and validity boundary, while the
ADR fixes the lease formula and disclosure policy. See `proposal.md` for the
motivation and the delta specs for externally observable requirements.

## Goals / Non-Goals

**Goals:**

- Put public reads behind registry lookup so unknown aliases never allocate a
  state object.
- Make one read authority decide ready vs unavailable before cache validators.
- Preserve exact approved JSON and add only a fixed SVG presentation layer.
- Keep output deterministic enough for exact test vectors and cache tests.

**Non-Goals:**

- No GitHub/tree/provenance verification, canonical Health evaluation, source
  reads, registration mutations, or shared publisher workflow changes.
- No attempt to revoke copies already stored by Camo, browsers, or offline
  clients; origin truth and observed cache delay remain separate.
- No custom renderer, arbitrary SVG/text configuration, or public metadata
  endpoint.

## Decisions

### One state-to-representation read seam

Add a Relay Durable Object read operation that receives a representation kind
(`json` or `svg`) after the worker has resolved a registered alias. It reads
the state transactionally, parses only closed-profile canonical payload bytes,
and compares a UTC clock value with `valid_until` before constructing any ETag
or body. Invalid persisted ready data is treated as unavailable without
mutating it; a read cannot repair, extend, or transition state.

This keeps storage-state validation and the clock comparison beside the atomic
state rather than duplicating them in the worker router. A worker-only read was
rejected because it would need a second state schema/serialization boundary.

### Fixed public route surface

Expose only `GET` and `HEAD` at registered aliases, with explicit JSON and SVG
suffixes. The root public representation defaults to the strict SVG only for
the freshness profile; headline-only retains JSON snapshot compatibility and
does not silently claim bounded-current display. Unsupported methods and
representations are fixed 404 responses. The outer worker performs private
registry lookup before `idFromName` for every public route.

### Closed outputs and rendering

JSON ready responses use the stored bytes verbatim. All non-ready cases use
the existing approved unavailable JSON constant. SVG is generated from a small
literal template and only accepts the already validated closed payload values;
XML escaping remains a defense-in-depth step. The SVG displays the canonical
label/message/color plus `verified at` and `valid until` as absolute UTC text,
and it has no URL-bearing or executable element types.

An SVG generated from arbitrary Shields JSON or an SVG supplied by a publisher
was rejected because either would turn a rendering boundary into a disclosure
or script injection surface.

### Cache and ETag semantics

The read seam computes `remaining = floor(valid_until - now)` only after the
strict `now < valid_until` predicate succeeds. Ready JSON/SVG both emit a
representation-specific ETag hashed from a versioned string containing
generation, state, profile, payload digest, validity boundary, and kind. A
matching `If-None-Match` returns 304 only while still ready; HEAD returns the
same status and headers as GET with no body. Every unavailable/uncertain result
is `no-store`, carries no ready ETag, and has fixed content type and `nosniff`.

## Risks / Trade-offs

- [A client retains an old image after origin expiry] → The SVG visibly records
  its UTC boundary and README language distinguishes origin behavior from cache
  recall.
- [Clock rounding causes a cache lifetime to cross the boundary] → Use the
  strict comparison then floor remaining seconds, with no minimum positive
  cache lifetime.
- [Corrupt legacy state appears ready] → Re-validate canonical bytes and
  profile on each read; return unavailable rather than stale last-good data.
- [Output variations leak private state] → Use only fixed unavailable bodies
  and headers on non-ready paths.

## Migration Plan

1. Add the read/render module and public route adapter with focused tests.
2. Add contract vectors for expiry, HEAD/304, cache bounds, ETag generation,
   and renderer injection cases.
3. Update the Relay ADR and README with the strict SVG vs snapshot distinction.
4. Deploy the versioned bundle through the existing later distribution workflow;
   rollback is bundle-version rollback, while public reads remain fail-closed
   whenever the chosen runtime cannot verify state.
