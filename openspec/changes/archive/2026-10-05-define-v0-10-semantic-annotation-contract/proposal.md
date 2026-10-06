## Why

The v0.10 semantic-annotation wave has implementation issues ready to proceed, but its package shape, stable annotation identities, role set, context composition, conflict behavior, and version-skew contract are not yet fixed. The historical no-package decision predates the delivered role index and selectors, so the follow-up work needs one reviewed contract before package and Core changes begin.

## What Changes

- Approve an optional source-only `ArchLinterNet.Annotations` package for SDK-style projects, with internal injected types and no runtime annotation assembly, analyzer, or source generator.
- Fix the v0.10 canonical annotation FQNs, finite role set, type/assembly targets, bounded context metadata, conflict behavior, catalog generation, and package/tool compatibility boundary.
- Keep custom user-owned attributes/YAML mappings first-class, keep annotations as semantic evidence rather than policy authority, and preserve #525 reviewed API membership as an independent selector decision.
- Update the semantic role catalog with an explicit v0.10 annotation disposition for every listed role and add a versioned internal contract authoritative for #567–#574.
- Establish a repository-owned catalog manifest as the source of truth; follow-up package/Core/docs work must generate or validate its projections and fail on drift.

## Capabilities

### New Capabilities

- `semantic-annotation-contract`: Defines the optional source-only distribution, exact canonical identities, bounded annotation vocabulary and metadata, deterministic role/context resolution, compatibility behavior, and policy/public-API boundaries.

### Modified Capabilities

- `semantic-role-catalog`: Adds v0.10 first-class, custom-mapping-only, and deferred annotation dispositions while preserving the existing vocabulary and YAML-first catalog meanings.
- `semantic-classification-model`: Recognizes canonical annotation evidence through the existing role/index model, fixes canonical conflict behavior, and defines bounded context metadata composition without changing legacy custom-mapping-only resolution.

## Impact

This is a design and documentation change. It updates OpenSpec requirements, the semantic-role catalog, a versioned internal design contract, and a machine-readable canonical catalog manifest. It does not implement the NuGet package, Core recognition, new runtime behavior, or release publication.
