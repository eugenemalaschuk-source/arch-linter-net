## Context

The packaged-schema-registry capability (from `ship-versioned-packaged-schemas`
and `reconcile-schema-registry-release-version-identity`) established a
single, whole-registry `0.5.1` identity: one compatibility manifest, one
`productVersion`, every entry release-qualified under `.../0.5.1/...`. That
design assumed the registry as a unit stays frozen. PR #442 needed exactly one
schema (policy-root, and transitively policy-fragment via its `$ref`s) to gain
a new optional field, exposing a gap: the design had no mechanism for one
entry to evolve while the rest of the registry stays put.

## Goals / Non-Goals

**Goals:**
- Let policy-root/policy-fragment gain `overlaps_with` support in their
  packaged, release-qualified, editor-discoverable identity.
- Never mutate the bytes behind an already-published `$id`.
- Leave every unaffected packaged schema's identity untouched.

**Non-Goals:**
- Bumping the whole registry's `productVersion`/`compatibilityEnvelope` label
  just because two of eight entries changed — the other six are genuinely
  unchanged and re-stamping them would be misleading busywork.
- Keeping the old `0.5.1` policy-root/policy-fragment identity simultaneously
  registry-discoverable under a second logical id — not requested, and adds a
  permanent "legacy" entry with no current consumer.
- Redesigning `PackagedSchemaRegistry`/the manifest format to support
  multiple manifest generations at once. A per-entry `resourcePath`/`schemaId`
  bump within the existing single-manifest model is sufficient.

## Decisions

**Per-entry identity advancement, not whole-registry version bump.** Only
`policy-root` and `policy-fragment`'s `resourcePath`, `resourceName`,
`schemaId`, and `sha256` move to `0.6.1`; `productVersion`,
`compatibilityEnvelope`, and every other entry stay `0.5.1`. This mirrors how
independent package/library ecosystems version individual artifacts, not a
monolithic release train, and keeps the diff to exactly the two schemas that
actually changed.

**`0.6.1`, not a product-tied version.** Per the existing "schema registry
version SHALL NOT be represented as the product package version" rule, the
new identity is a plain next-patch bump within the schema registry's own
independent line, unrelated to the `0.6.0` product package version.

**Live top-level schema files stay the source of truth; frozen copies are
minted per release.** `schema/dependencies.arch(.fragment).schema.json`
continue to be what `ArchitecturePolicyEffectiveSchemaValidator` uses at
runtime and what real authors/editors should point at (their `$id`s now say
`0.6.1`). `schema/0.6.1/...` are byte-identical frozen snapshots, embedded
under their own `LogicalName`s and packaged at their own
`contentFiles/.../0.6.1/...` paths — mirroring the `0.5.1` frozen-snapshot
pattern PR #442 already established, so the next schema evolution repeats the
same "freeze what's changing, mint a new identity" ritual instead of silently
mutating a published one again.

## Risks / Trade-offs

- **Risk**: a consumer that hard-pinned `schema/0.5.1/dependencies.arch.schema.json`
  for `overlaps_with` support will not find it there.
  → **Mitigation**: `0.5.1` never claimed to support `overlaps_with` — it
  predates this PR. Anyone wanting the new syntax's schema needs `0.6.1`,
  discoverable via `schema list`/`schema print policy-root` like any other
  registry entry.
- **Risk**: readers of `docs/internal/adoption-stabilization-compatibility.md`
  (the original 0.5.1 blueprint) see a now-partially-stale historical
  snapshot.
  → **Mitigation**: left as-is deliberately — it's a point-in-time planning
  record analogous to an archived OpenSpec proposal, not a live reference
  page, and nothing tests its content against runtime state.

## Migration Plan

Purely additive at the file level (new `0.6.1` files, existing `0.5.1` files
untouched) plus a manifest/csproj rewiring. No data migration; no runtime
behavior change for anyone not using `overlaps_with`.

## Open Questions

None.
