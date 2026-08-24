## MODIFIED Requirements

### Requirement: Reproducible released-tool self-dogfood evidence

The repository SHALL retain a public-safe evidence record and canonical JSON
artifact for a real ArchLinterNet release range. The record SHALL identify the
released tool version, source commit, authored and resolved range operands,
policy/configuration identity, canonical artifact path, canonical artifact
digest, and the canonical .NET enrichment status. The canonical artifact digest
SHALL match the retained artifact bytes through a documentation lint or CI
check. The recorded tool SHALL be installed at its exact version in a
caller-owned isolated tool directory and invoked from that directory, without
relying on a local-tool manifest in an analysed repository or worktree. The
recorded canonical forensics command SHALL use separate `--from` and `--to`
operands, SHALL NOT represent a Git revision expression as a supported operand,
and SHALL omit `--enrich-dotnet` so its enrichment status is `not_requested`.
Any requested .NET enrichment observation SHALL be recorded separately as
advisory, environment-dependent evidence and SHALL NOT define the canonical
artifact digest.

#### Scenario: Maintainer reproduces the recorded run

- **WHEN** a maintainer follows the evidence record with the named isolated
  tool executable, repository revision, policy, and authored operands
- **THEN** they can recreate the canonical Git-only report and compare its
  digest without relying on a local-machine path, local-tool manifest, or
  private adopter data

#### Scenario: Retained artifact changes

- **WHEN** the canonical artifact bytes are altered without updating the
  documented digest
- **THEN** the documentation lint or CI check fails before the reference can be
  accepted
