## Context

The repository currently keeps policy root, policy fragment, and baseline schemas in `schema/`; only the root schema is embedded in Core. The 0.5.1 compatibility contract requires the complete `adoption-stabilization/v1` set to be immutable, digest-addressable package resources that an offline CLI or NuGet consumer can discover. The existing CLI already composes top-level modules, while package contents are built from SDK project items.

## Goals / Non-Goals

**Goals:**

- Define one checked-in 0.5.1 manifest containing metadata and SHA-256 digests for eight public machine-readable documents.
- Embed the manifest and exact schema files in Core, preserving them in its NuGet package; expose them through a small typed Core service.
- Add a `schema` CLI module with `list` and `print <logical-id>` commands that consume only the embedded resources.
- Make schema, package, capability-manifest, and documentation consistency executable in NUnit tests.

**Non-Goals:**

- A remote schema registry, runtime download, schema update mechanism, or policy migration writer.
- Replacing unversioned repository/editor aliases as a convenience for source contributors.
- Implementing future formats outside the eight 0.5.1 public documents.

## Decisions

### Single manifest is the registry authority

`schema/0.5.1/compatibility-manifest.json` will list every logical id, document version, resource path, immutable release-qualified `$id`, SHA-256 digest, read/write support, migration note, and owning OpenSpec capability. The registry validates the manifest's own structure and each resource before returning a descriptor. This makes package/resource validation deterministic and avoids parallel lists in CLI, docs, and project files.

An ad-hoc CLI map was rejected because it would duplicate release metadata and cannot verify package contents.

### Package resources live in Core and CLI consumes a typed Core seam

Core owns the format contracts and embeds every resource. `ArchLinterNet.Cli` already depends only on Core, so it receives the registry through composition without creating a reverse dependency or a new adapter assembly. The resource files are also packed under `contentFiles/any/any/schema/0.5.1/` for NuGet inspection and offline consumers.

Putting a second registry in CLI was rejected because the Core package would remain incomplete and the two registries could skew.

### Exact content and identity are verified at load time

The registry computes SHA-256 from embedded UTF-8 resource bytes and compares it to the manifest. It parses each schema and requires `$id` and declared format version to agree with its manifest entry. `list` produces stable ordinal metadata, while `print` writes the exact embedded schema bytes. Unknown ids are CLI usage errors and list/print do not access the network or source checkout.

Trusting the manifest alone was rejected because accidental package omissions or resource substitution would go undetected.

### Versioned schemas describe the published 0.5.1 contracts

The root and fragment schemas are copied into `schema/0.5.1/` with release-qualified `$id` values; source aliases retain their current convenience ids. Baseline v2/identity v1, API snapshot v1, finding v1, build-state v1, cache v1, and profile v1 receive conservative JSON Schema definitions matching the compatibility blueprint. The manifest names the identity version as baseline metadata rather than treating it as a ninth document.

## Risks / Trade-offs

- [Schema shapes owned by sibling slices can evolve] → schemas use the published #355 version identifiers and explicit, conservative object contracts; consistency tests require deliberate updates when those slices land.
- [SDK packaging rules omit linked files] → pack tests inspect generated `.nupkg` entries and compare bytes/digests.
- [Schema resource corruption makes CLI unusable] → registry fails with an actionable integrity error instead of printing mismatched contracts.
- [Version text in docs drifts] → tests check the public schema reference and capabilities metadata for every manifest id/version.

## Migration Plan

1. Add versioned source resources and manifest while retaining existing unversioned source aliases.
2. Embed and pack all versioned resources from Core.
3. Add Core registry and CLI discovery commands with offline integration tests.
4. Update public documentation, capabilities, and release notes.
5. Archive the OpenSpec change after package/source/docs validation passes. Rollback is a normal package version rollback: consumers retain their already installed immutable resource set.

## Open Questions

None. The #355 blueprint and #372 issue comment fix the registry version, resource set, metadata fields, and offline behavior.
