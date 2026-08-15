# Change: Parallelize packed-artifact Checkpoint B without weakening release evidence

## Why

Checkpoint B has grown into a release acceptance pipeline hidden behind one NUnit test. On PR #585 the Windows packed-artifact job took about 7m38s, with 5m31s inside that single test, while Apple Silicon took 2m17s. The release workflow amplifies the same cost by invoking packed-artifact acceptance through generic repository acceptance before and after the immutable-candidate platform matrix.

## What Changes

- Split the Checkpoint B scenario oracle into eleven deterministic scenario shards while keeping the complete local `make test-packed-artifact` gate.
- Prepare one immutable, manifest-bound candidate per PR/release workflow and distribute it to isolated shard runners.
- Merge shard evidence fail-closed into the existing canonical one-record-per-platform evidence contract.
- Preserve the existing required PR check names as fan-in checks so the active `Main` ruleset remains authoritative.
- Add a repository-acceptance target that excludes packed-artifact proof for release workflow stages where the immutable candidate is validated separately.
- Build the release-version candidate after repository acceptance, so the `--no-build` package step embeds assemblies whose CLI version agrees with the manifest-bound package version.
- Make subprocess waits cancellation-aware and kill descendant process trees on timeout/cancellation.

## Scope

Related issue: #587. This changes test/CI/release topology and evidence orchestration only; product architecture-policy semantics and publication authorization criteria are unchanged.
