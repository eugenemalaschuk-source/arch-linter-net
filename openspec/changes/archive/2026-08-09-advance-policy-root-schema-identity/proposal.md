## Why

Issue #442 added `layers.*.overlaps_with` to the policy-root/policy-fragment
schemas. Those schemas are also packaged byte-for-byte as the immutable,
release-qualified `0.5.1` schema-registry entries (`policy-root`,
`policy-fragment`). Changing their content while keeping the same `$id` would
have made two different package builds expose different bytes under one
immutable identity — a real regression caught during PR review for #442.
Freezing the `0.5.1` bytes unchanged (already done) is necessary but not
sufficient: the documented, release-qualified editor/runtime schema identity
for policy-root/policy-fragment must actually accept the new, currently
supported syntax, or docs/runtime/schema disagree — which is itself one of
#442's explicit acceptance criteria ("Schema, runtime, docs and diagnostics
agree").

## What Changes

- `policy-root` and `policy-fragment` advance to an independent
  `0.6.1` schema identity (new `$id`s, new packaged resource paths, new
  digests) that includes `overlaps_with` and is the schema editors/tooling
  should reference going forward.
- The frozen pre-`overlaps_with` `0.5.1` policy-root/policy-fragment bytes
  remain preserved in source control (`schema/0.5.1/...`) but are no longer
  packaged or registry-discoverable, since `schema list`/`schema print
  policy-root` now resolve to `0.6.1`.
- Every other packaged schema (baseline, api-snapshot, normalized-finding,
  analysis-build-state, analysis-cache, analysis-profile) is unaffected and
  stays at its existing `0.5.1` identity.
- Editor/release guidance (`docs/reference/yaml-schema.md`,
  `docs/guides/migration-to-0-5-1.md`) and the package-validation CI smoke
  test are updated to the new `0.6.1` policy-root/policy-fragment identity.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `packaged-schema-registry`: the "Release-matched packaged schema registry"
  requirement changes from "the whole registry is pinned at 0.5.1" to "each
  entry independently advances its own release-qualified identity when its
  schema shape changes; unaffected entries stay at their prior identity."

## Impact

- `schema/dependencies.arch.schema.json`,
  `schema/dependencies.arch.fragment.schema.json` — `$id`/`$ref`s advance to
  `0.6.1`.
- `schema/0.6.1/dependencies.arch.schema.json`,
  `schema/0.6.1/dependencies.arch.fragment.schema.json` — new frozen
  snapshots of the current (post-#442) schemas.
- `schema/0.5.1/dependencies.arch.schema.json`,
  `schema/0.5.1/dependencies.arch.fragment.schema.json` — unchanged, frozen
  pre-`overlaps_with` bytes, preserved but no longer registry-embedded.
- `schema/0.5.1/compatibility-manifest.json` — `policy-root`/`policy-fragment`
  entries repointed at the `0.6.1` resources.
- `src/ArchLinterNet.Core/ArchLinterNet.Core.csproj` — embeds/packs the new
  `0.6.1` resources instead of the (now-orphaned for packaging purposes)
  `0.5.1` root/fragment files.
- `.github/workflows/package-validation.yml`,
  `docs/reference/yaml-schema.md`, `docs/guides/migration-to-0-5-1.md` —
  updated to the `0.6.1` identity.
- Test updates: `PackagedSchemaRegistryTests.cs`,
  `ArchitecturePolicyImportSchemaTests.cs`,
  `CheckpointBReleaseGateTests.CandidatePackageFeed.cs`,
  `CheckpointBReleaseGateTests.PackagedCoverageSchema.cs`.
