## Why

The Badge Relay runtime and setup contract are now implemented, but the public
release workflow still proves and attaches only NuGet package subjects. A
consumer cannot independently obtain the frozen Relay deployment bytes,
approved publisher pins, and compatibility binding from the same reviewed
candidate, so the transport remains a source-tree-adjacent promise instead of
release evidence.

## What Changes

- Add a typed outer transport inventory linked to the existing immutable NuGet
  candidate manifest and release authority `#806`.
- Build a deterministic, frozen Relay distribution archive containing the
  deployable runtime, configuration schema, dependency/license notice, and
  approved reusable publisher workflow/action bytes.
- Emit versioned compatibility metadata binding the candidate CLI package set,
  Relay/protocol/schema/migration versions, approved publisher pins, source
  commit, and candidate manifest digest.
- Verify transport subjects, archive contents, dependency lock/license
  metadata, missing/tampered bytes, and incompatible candidate combinations
  before Checkpoint B, attestation, publication, and GitHub Release attachment.
- Extend the existing provenance inventory with transport and transport
  evidence subject classes without changing NuGet package identity semantics or
  creating a second release pipeline.
- Make the packaged CLI validate its Relay bundle manifest before resolving
  Relay assets for setup; malformed or altered shipped assets fail closed.
- Document the release asset set and the boundary between candidate bytes and
  post-publication consumer verification. Public publication and cloud
  deployment remain out of scope.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `openspec/specs/release-artifact-provenance/spec.md`: frozen transport
  subjects and their independent provenance become part of the existing
  candidate/release evidence boundary.
- `openspec/specs/badge-turnkey-setup/spec.md`: packaged Relay asset
  resolution must verify the shipped bundle manifest and compatibility pins
  before generating adopter output.

## Impact

The change affects `tools/release` manifest/verification tooling, the manual
`.github/workflows/release-nuget.yml` workflow, the Relay package assets and
CLI pack metadata, release documentation, and focused Python/C# tests. It does
not add a runtime dependency to Core, change Architecture Health semantics,
publish a release, deploy a provider resource, or alter the existing NuGet
package manifest authority.
