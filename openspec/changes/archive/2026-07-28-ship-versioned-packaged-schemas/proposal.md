## Why

ArchLinterNet currently exposes three mutable, repository-relative schema files. An installed 0.5.1 CLI or NuGet package therefore cannot discover its exact contracts offline, and a later default-branch change can alter the apparent release contract.

## What Changes

- Ship an immutable `adoption-stabilization/v1` compatibility manifest and release-qualified JSON Schema resources with the CLI and Core NuGet packages.
- Add offline CLI discovery that lists every packaged schema and prints one exact packaged schema without a repository checkout or network access.
- Validate schema identity, document version, digest, package inclusion, source consistency, and documentation/capability-manifest coverage.
- Document the distinct policy-root, policy-fragment, baseline, identity, snapshot, normalized-finding, build-state, cache, and profile contracts and their supported versions.

## Capabilities

### New Capabilities

- `packaged-schema-registry`: Immutable version-matched schema registry, package resources, and offline discovery.

### Modified Capabilities

- `adoption-stabilization-compatibility`: Make the published 0.5.1 registry and release-qualified schema behavior executable.
- `cli-command-dispatch`: Add the schema discovery command surface.
- `docs-site`: Document offline schema discovery and release-qualified editor references.

## Impact

Affected areas include `schema/`, Core and CLI package projects, CLI command composition, capability metadata, release documentation, and new Core/CLI/package consistency tests. No remote registry or automatic policy rewriting is introduced.
