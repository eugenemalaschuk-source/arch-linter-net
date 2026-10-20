## Context

The reviewed semantic annotation sources and manifest already exist under `src/ArchLinterNet.Annotations/contentFiles/cs/any` and `architecture/semantic-annotations.v1.json`. Core recognizes those exact identities. There is no package project yet, so the existing C# 9 consumer and Core fixtures compile the source files directly and do not prove NuGet packaging or external restore behavior.

The package contract from #566 fixes catalog generation 1, the `ArchLinterNet.Annotations` package identity, internal annotation definitions, package version alignment, and exact manifest FQNs. This change implements that contract without changing role or context semantics.

## Goals / Non-Goals

**Goals:**

- Pack the approved source set as C# compile content for all SDK-style consumer target frameworks.
- Keep the package free of runtime, binary, analyzer, source-generator, and consumer MSBuild target assets.
- Generate the per-assembly catalog identity source from the actual package version used by `dotnet pack`.
- Validate a freshly packed package through isolated external consumers, including central package management, multi-project use, public API, runtime closure, and removal.
- Include the package in stable release candidate inventories covered by the generation-1 range `[0.10.0,0.11.0)`, while preserving existing preview, development, earlier maintenance, and later-minor inventories.

**Non-Goals:**

- Changing canonical identities, role mappings, context composition, classifier behavior, or package/tool compatibility semantics.
- Shipping a runtime annotation assembly, analyzer, source generator, build target, or dependency on another ArchLinterNet package.
- Changing the existing main development package version or release scope.

## Decisions

1. **Use NuGet `contentFiles` as compile input.** Pack the reviewed `.cs` files at `contentFiles/cs/any` and declare them with `buildAction=Compile` and `copyToOutput=false`. The package project excludes those source files from its own compilation and suppresses build output and dependencies. This uses NuGet's ordinary PackageReference content behavior and needs no consumer-side `.props` or `.targets` file.

2. **Generate the catalog marker source at pack time.** The package project writes one `AnnotationCatalogIdentityAttribute.cs` source file into its intermediate directory before NuGet collects package files. The generated source carries catalog generation 1 and the exact `PackageVersion` supplied to the pack operation. It is then packed beside the reviewed sources and compile-injected into each directly referencing consumer assembly. No version discovery or generated files are needed in consumer projects.

3. **Keep the package's own build output out of the package.** The project exists to pack source and readme metadata. Its build output, symbols, dependencies, and default root README are excluded. The package readme states that the package is optional source-only convenience, requires an annotation-aware tool, and does not provide a runtime library.

4. **Version the release package inventory by the reviewed compatibility range.** Stable 0.10.x releases add the un-symbolled annotation `.nupkg` to their candidate manifest. Prereleases, earlier package lines, and later minor lines retain their reviewed package sets until a catalog contract covers those versions. Extend the manifest format to model a package without a symbol subject while retaining readers for existing immutable candidate manifests. Release workflows select the package set from the candidate version; the v0.8 main-build line and v0.10 previews do not publish the stable annotation package.

5. **Prove behavior from the packed artifact.** Core integration tests create an isolated local feed from a freshly packed package and build independent consumer projects through PackageReference. They inspect the `.nupkg`, compiled assemblies, `.deps.json`, and project tree after removal. The existing manifest parity checks remain authoritative for source/Core catalog equality; the new test also verifies that the package contains the same approved source files once each.

6. **Keep the pack-only project outside runtime project coverage.** The project builds locally so the solution and package tooling can validate it, but its compiled output is not included in the NuGet package and it defines no runtime architecture surface. The exact project is therefore excluded from assembly/project coverage, with the reason recorded in the self-policy capability matrix.

## Risks / Trade-offs

- **NuGet content inclusion varies with package metadata and client tooling.** → Assert the generated `.nuspec` compile/copy metadata and build consumers through the real package on every CI platform.
- **Package version and assembly marker could drift.** → Generate the marker from the same `PackageVersion` used by pack and assert the marker in a built external consumer.
- **The package has no useful symbol package.** → Represent the package subject without a `.snupkg` in the candidate manifest rather than publishing empty symbols for a pack-only project.
- **Only package versions covered by catalog generation 1 may receive the package.** → Select the inventory for stable 0.10.x candidates only and retain the current package sets for previews, earlier lines, and later minors.

## Migration Plan

No consumer migration is needed. Consumers opt in by adding a direct `PackageReference`, or a direct reference with a centralized version. Existing custom attributes and YAML mappings continue to work. Unity projects may import the package's C# source files through the documented deterministic source-import path when they do not consume NuGet `contentFiles`.

## Open Questions

None. The issue and approved #566 contract settle the package identity, source-only boundary, catalog version, and consumer-facing semantics.
