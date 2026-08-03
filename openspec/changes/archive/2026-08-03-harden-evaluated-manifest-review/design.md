## Context

The current static collector observes only a subset of MSBuild inputs. It must not classify that subset as complete cache evidence.

## Goals / Non-Goals

**Goals:** fail closed for every unit, preserve ordinary preflight categories, and bound untrusted-repository collection.

**Non-Goals:** executing MSBuild or implementing persistent caching.

## Decisions

- SDK-style projects and any unresolved import/reference/analyzer/artifact evidence are cache-ineligible.
- `Platform` and RID are carried in typed request/receipt context; absent output evidence remains ineligible.
- Symlinks/reparse points are rejected as authoritative inputs; count/byte limits stop hashing before unbounded work.
- A centralized result decoration assigns `cache-ineligible` to every non-current diagnostic.

## Risks / Trade-offs

- More projects are ineligible until a safe evaluated-MSBuild collector exists → this is intentional and preserves correctness.
