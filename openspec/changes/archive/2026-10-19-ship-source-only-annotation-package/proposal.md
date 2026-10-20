## Why

The approved v0.10 annotation source set and Core catalog exist, but consumers still cannot install them as a NuGet package. A packed source-only package and external-consumer proof are needed before this capability can be distributed safely.

## What Changes

- Add `ArchLinterNet.Annotations` as an optional NuGet package that compile-includes the approved C# sources and contains no runtime assembly, analyzer, generator, or runtime dependency.
- Include the package only in stable release candidates covered by the generation-1 package range, and publish metadata that explains its source-only, compile-time role.
- Validate the freshly packed artifact with external consumer projects, including central package management, public API and runtime-closure checks, multi-project use, and clean removal.
- Keep source/Core/catalog identities aligned with the reviewed v0.10 manifest and document supported consumption, including the Unity-compatible source-import path.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `semantic-annotation-contract`: require packed-artifact proof for source inclusion, package contents, compatibility metadata, external-consumer behavior, and removal.

## Impact

Adds a pack-only project under `src/ArchLinterNet.Annotations`, package validation to Core tests, and updates to package workflows, release manifest expectations, and semantic-annotation guidance. No Core classification behavior or reviewed annotation identities change.
