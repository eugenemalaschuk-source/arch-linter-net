## Context

The initial v0.7 self-dogfood reference retained the release-range identity and
digest, but did not retain the canonical JSON bytes. Its public commands also
resolved `dotnet arch-linter-net` through the analysed repository's local tool
manifest, which pins a different release. Finally, optional .NET enrichment is
serialized in the report, making an enriched digest depend on the local
worktree and build state.

## Goals / Non-Goals

**Goals:**

- Make the recorded consumer CLI unambiguous and independent of any analysed
  repository manifest.
- Retain canonical bytes and automatically verify their documented digest.
- Keep optional enrichment useful without letting its environment-specific
  outcome redefine reproducible Git-only evidence.

**Non-Goals:**

- Change release-forensics, enrichment, policy, or debt-gate product behavior.
- Rewrite the historical archived dogfood change.
- Require enrichment to be available or make it release authority.

## Decisions

### Use an exact isolated tool directory

The guide installs `ArchLinterNet.Cli` 0.7.0 with `dotnet tool install --tool-path` into a caller-owned directory and invokes its executable through
`ARCH_LINTER_NET` in every worktree. This prevents an analysed checkout's local
manifest, global tool, or sibling worktree manifest from selecting a different
CLI version.

An unversioned manifest install was rejected because it can resolve a reviewed
but incompatible tool version. Updating the analysed repository's manifest was
also rejected because the workflow must not mutate its consumer input.

### Retain and checksum the Git-only canonical JSON

The checked-in JSON is the raw stdout from the released 0.7.0 tool in a clean,
detached v0.7.0 worktree with the v0.6.5 and v0.7.0 operands. Its canonical
command omits `--enrich-dotnet`, producing a deterministic `not_requested`
projection. A lightweight Python checker streams the JSON bytes, extracts the
designated digest from the evidence record, and fails `lint-docs` if they do
not match.

Digest-only retention was rejected because it leaves reviewers unable to
verify the claimed bytes. An enriched canonical report was rejected because
enrichment includes build-state and worktree verification outcomes in its JSON
projection.

### Record enrichment separately as advisory evidence

The evidence record retains the observed `worktree_verification_failed`
enrichment result as a separate advisory observation. It is explicitly not the
canonical artifact and does not determine the artifact digest. The guide shows
how to request enrichment only after preparing an exact, clean target worktree
and asks adopters to record the bounded status they observe.

## Risks / Trade-offs

- [The JSON artifact is substantially larger than the prose record] → retain it
  only as internal evidence, link it from the concise record, and verify it by
  streaming hash rather than loading it into the documentation build.
- [A future evidence refresh updates bytes without prose] → the lint gate fails
  until the documented digest is updated through review.
- [A reader treats the exact v0.7.0 pin as a current-install recommendation]
  → label it as an immutable historical replay and keep the guide route and
  navigation release-neutral.

## Migration Plan

Add the artifact and verification guard with the revised evidence record, then
archive this repair change. Existing consumers receive corrected copy-paste
commands; no migration of product configuration or data is required.
