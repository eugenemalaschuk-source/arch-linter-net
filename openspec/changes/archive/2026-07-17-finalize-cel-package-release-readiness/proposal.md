## Why

`ArchLinterNet.CEL` (#325–#329, #167, #168) now has a shipped Profile v1 engine, a validated public API, and recorded performance baselines. Issue #330 finalizes the package boundary so Core integration (#163) can begin against a documented, release-ready package instead of an unfinished internal project: XML API docs and a package-specific README must actually ship in the `.nupkg`, the CEL package's own dependency metadata must be validated as clean, an external consumer must be proven able to install and use the packed artifact, and the internal architecture blueprint must be reconciled into the exact structure #330 requires (public API stability boundary, extension governance checklist, versioning/compatibility policy) so every v1 exclusion has a documented, non-ad-hoc extension direction.

## What Changes

- Enable `GenerateDocumentationFile` for `ArchLinterNet.CEL` so its public API XML doc comments (already authored) are packed and ship to consumers.
- Add a CEL-specific packaged README (`src/ArchLinterNet.CEL/README.md`) covering the Profile v1 subset, the compile/evaluate lifecycle, limits/diagnostics/thread-safety, and non-goals — replacing the shared root README as the CEL package's `PackageReadmeFile`.
- Extend `.github/workflows/package-validation.yml` to assert the CEL package's own `.nuspec` carries no dependencies (forbidden or otherwise — CEL SHALL remain a self-contained leaf package), and to run an external-consumer smoke test that restores the packed `ArchLinterNet.CEL` `.nupkg` into a throwaway console project, defines a schema, compiles a Profile v1 predicate, and evaluates it.
- Restructure `docs/internal/cel-engine-architecture.md` into the issue's required 9-section blueprint shape: fold the existing "Processing pipeline" + "Component ownership" content under a new "1. Engine lifecycle and module graph" section; add a new "2. Public API stability boundary" section enumerating the 9 v1 exclusions from #324 with each exclusion's future architectural direction; renumber the existing extension-direction matrix rows to sections 3–8; add a new "9. Extension governance checklist" section with the issue's 10-question checklist; add a new "Versioning and compatibility policy" section documenting profile SemVer rules, package release SemVer rules (cross-linking `docs/reference/release-process.md`), and the existing `CelPublicApiSurfaceApprovalTests.cs` baseline-compatibility mechanism.

## Capabilities

### New Capabilities

- `cel-package-release-readiness`: `ArchLinterNet.CEL` ships public API XML documentation and a package-specific README, its packed `.nuspec` is dependency-clean, an external consumer can install and use the packed artifact, and its internal architecture blueprint documents the public API stability boundary, versioning/compatibility policy, and extension governance process required before Core integration begins.

## Impact

- `src/ArchLinterNet.CEL/ArchLinterNet.CEL.csproj` — `GenerateDocumentationFile`, `PackageReadmeFile` override, README pack item
- `src/ArchLinterNet.CEL/README.md` — new file
- `.github/workflows/package-validation.yml` — new CEL-nuspec dependency assertion + external-consumer smoke-test step
- `docs/internal/cel-engine-architecture.md` — restructured sections, new content, no removed factual content
- `nupkg/ArchLinterNet.CEL.*.nupkg` — now contains an XML doc file and a CEL-specific README
