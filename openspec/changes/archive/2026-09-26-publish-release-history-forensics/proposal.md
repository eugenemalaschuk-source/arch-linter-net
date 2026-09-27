## Why

The release pipeline already produces an immutable CLI candidate and deterministic history-forensics reports, but it does not bind release-to-release evidence to that candidate or retain it with the public release. Adding a read-only analysis lane gives maintainers reproducible architecture evidence while keeping scoring findings outside publication authorization.

## What Changes

- Add a candidate-bound `history-forensics` analysis job that verifies and runs the exact packed CLI package against the full Git history range ending at the candidate commit.
- Pin stable and preview range semantics, and emit a typed not-applicable result when no eligible predecessor exists.
- Produce canonical JSON and Markdown, a provenance manifest/checksums, separate operational observations, and a bounded Actions summary.
- Require a complete forensics bundle before package publication; attach it through the existing GitHub Release job and verify downloaded asset digests.
- Document dry-run, release-asset, and local reproduction procedures.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `manual-nuget-release`: require candidate-bound history evidence before publication and preserve it as verified GitHub Release assets without turning findings into a quality gate.
- `release-process-documentation`: document the history-forensics bundle, dry-run review, release asset location, and reproduction procedure.

## Impact

The manual release workflow, release tooling and tests, maintainer release documentation, and the existing OpenSpec release capabilities. No CLI behavior, scoring semantics, package contents, or PR gate changes are intended.
