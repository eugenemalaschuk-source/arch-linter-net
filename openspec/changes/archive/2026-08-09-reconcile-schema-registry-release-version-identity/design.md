## Context

The released `0.6.0` product package continues to ship the immutable
`adoption-stabilization/v1` registry whose `$id` and resource paths are rooted
at `schema/0.5.1/`. The registry is a compatibility identity for the persisted
formats, not a copy of the package's SemVer. The package assets nevertheless
present `0.5.1` as the product release target, so consumers cannot distinguish
intentional schema compatibility from stale release metadata.

## Goals / Non-Goals

**Goals:**

- State the `0.6.0` product-to-`0.5.1` registry mapping at every public
  release-facing surface.
- Keep `$schema` examples pointed at an embedded, immutable identity.
- Verify the relationship against freshly packed CLI/Core artifacts.

**Non-Goals:**

- Change document versions, schema semantics, or historical packages.
- Generate a new schema registry for every package SemVer.
- Add network schema resolution or change the offline CLI commands.

## Decisions

### Keep schema identities independently versioned

`0.5.1` remains the immutable schema-registry identity for the compatibility
formats shipped by the `0.6.0` package. This preserves valid editor references
and avoids claiming a schema change where none exists. Re-stamping every schema
as `0.6.0` was rejected because it would create duplicate identities for
unchanged documents and force an unnecessary migration.

### Make the mapping a package contract

The packaged README and public schema/release reference will identify `0.6.0`
as the product release while explicitly naming the embedded `0.5.1` registry
and directing users to `schema list` as the executable authority. Package
regression tests will inspect the installed tool's version, schema list, and
the packed README rather than trusting source-tree text alone.

### Validate public release wording mechanically

The package-validation workflow will reject stale `0.5.1` release-target
wording and schema URLs that are not present in the packed Core registry. This
keeps release metadata reconciliation in the artifact path used by consumers.

## Risks / Trade-offs

- [A later package changes schema semantics without a registry identity bump]
  → The existing manifest digest and `$id` validation, plus package checks,
  require an explicit compatible extension or a new schema identity.
- [A reader mistakes a schema identity for the product version] → Public
  release-facing pages and the packaged README state both roles adjacent to
  the offline discovery command.
- [Static documentation drifts on a future package line] → Artifact tests
  exercise the exact packed README and installed CLI before release.

## Migration Plan

No consumer migration is required: existing `0.5.1` `$schema` URLs continue to
resolve to the bytes shipped by `0.6.0`. Consumers that install `0.6.0` use
`schema list` to verify the mapping offline. Rollback consists of restoring the
previous release documentation only; no persisted format is transformed.

## Open Questions

None.
