## Context

`calculate_version.py` validates manual overrides against the NuGet-style package-version contract, while the history resolver currently models only stable and numbered `preview` versions. The transport verifier repeats that narrower assumption. The runner also starts `bundle_render_ms` before invoking the candidate CLI, although the CLI already reports its own process duration and render-phase timings.

## Goals / Non-Goals

**Goals:**

- Reuse the exact package-version parser used by `version_override` for candidate and tag versions.
- Select prerelease predecessors using SemVer precedence within one `X.Y.Z` line; preserve build metadata in identity and exclude it from ordering.
- Keep stable boundaries limited to lower stable release tags.
- Measure runner-side bundle rendering independently from candidate analyzer process time.

**Non-Goals:**

- Change automatic version calculation or the candidate package version contract.
- Make release findings a quality gate.
- Change the canonical analyzer report, transport manifest shape, or checksum inventory.

## Decisions

### Share package-version validation with the range resolver

Expose a small parser in `calculate_version.py` that returns the core, prerelease, and build-metadata portions for versions accepted by the existing override validator. The range resolver uses this parser instead of maintaining a narrower regular expression. The transport verifier uses the parsed version model as well, so it cannot reject a candidate that range selection accepted.

The `ReleaseVersion` model retains prerelease and build-metadata strings. Ordering follows SemVer: compare core components first; prereleases sort below stable versions; prerelease identifiers compare numeric-to-numeric numerically, numeric below nonnumeric, and nonnumeric identifiers lexically. Build metadata does not participate. Multiple reachable tags with the same precedence fail closed as ambiguous.

All prerelease identifiers for the same core version share the existing `preview:X.Y.Z` series identity. A prerelease candidate uses the highest lower prerelease in that line; if none exists, it falls back to the highest lower stable ancestor. Stable candidates continue to ignore all prerelease tags.

### Time runner rendering after analysis

For applicable candidates, start `bundle_render_ms` after `analyze` returns, then measure runner-side report identity/manifest preparation through manifest writing. For not-applicable candidates, start before the runner writes the typed JSON/Markdown result. Keep candidate CLI `process_wall_ms` and CLI phase timings under `analyzer`; do not copy process wall time into the orchestration map. Observation/checksum sealing remains outside the rendering duration because its timing is finalized into the observations artifact itself.

### Preserve the transport contract

Keep `preview` as the manifest kind for prerelease candidates and retain `preview:X.Y.Z` identities for compatibility with the current bundle format. The semantic scope is expanded to every accepted SemVer prerelease, while the output schema and candidate/tag binding remain unchanged.

## Risks / Trade-offs

- **Different tags can carry equal SemVer precedence because build metadata is ignored for ordering** → fail closed when equivalent-precedence tags are reachable, rather than select an arbitrary predecessor.
- **The unchanged `preview` label is broader than common labels such as `alpha` and `rc`** → document that it denotes the shared prerelease series, not a literal suffix requirement.
- **Bundle rendering timing excludes observation/checksum self-sealing** → keep that boundary explicit; total script elapsed still records the larger runner duration.

## Migration Plan

No package or bundle-schema migration is needed. Re-run the candidate-bound history job for the same PR commit after updating its implementation, tests, and documentation.
