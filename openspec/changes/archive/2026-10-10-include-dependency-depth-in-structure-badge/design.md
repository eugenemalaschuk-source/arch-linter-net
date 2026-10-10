## Context

The repository-metrics snapshot already exposes `Structure.MaxDependencyDepth`. `RepositoryMetricsBadgeProjector` currently emits only dependency count and largest SCC size for the grouped `Structure` badge.

## Goals / Non-Goals

**Goals:**
- Show the requested depth value in the existing compact Structure badge.
- Preserve the badge's neutral, absolute semantics and grouped shape.
- Fail closed when any required Structure badge value is absent.

**Non-Goals:**
- Add a separate dependency-depth badge.
- Change graph calculation, metric schema, or Health.

## Decisions

- Format the Structure message as `depth <n> · <dependencies> deps · SCC <size>` using the existing invariant count formatter.
- Require depth, dependency count, and largest SCC size for a complete Structure projection. If any value is unavailable, use the existing unavailable payload and exit code.
- Update the existing grouped-badge test to assert all values and add a missing-depth case.

## Risks / Trade-offs

The longer message modestly increases badge width. It remains one bounded grouped badge, and all values use the existing compact formatting.

## Migration Plan

No data migration is required. Existing consumers receive the same JSON payload shape with a more informative `message` value.
