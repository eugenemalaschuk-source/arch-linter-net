## Context

`ArchLinterNet.CEL` is packable and Core already declares it as a NuGet dependency (`cel-project-boundary`, already archived). #330 is the finishing pass before Core integration (#163) begins: consumers must be able to install the package standalone with real docs, and the internal blueprint (written pre-implementation by #324) must be reconciled against what #325–#329 actually shipped.

## Decisions

### One CEL-specific README, not a shared one
`Directory.Build.props` currently packs the single root `README.md` into all 4 packages via a repo-wide `<None Include>` item. The root README is entirely YAML/CLI-focused and never mentions CEL. Rather than editing the shared README to talk about two unrelated products (declarative YAML policies vs. an embeddable expression engine), `ArchLinterNet.CEL.csproj` gets its own `PackageReadmeFile`/`<None Include>` override that takes precedence for that one project, leaving the other 3 packages unaffected.

### CEL package must have zero NuGet dependencies, not just no forbidden ones
`cel-project-boundary` already prohibits YAML/JSON-schema/MSBuild/Roslyn dependencies. Since `ArchLinterNet.CEL` has no `PackageReference` items at all today (confirmed by reading `ArchLinterNet.CEL.csproj`), asserting zero dependencies in CI is strictly stronger than an allow/deny list and needs no maintenance as new forbidden packages might otherwise need enumerating.

### External-consumer smoke test runs against the packed artifact, not a ProjectReference
`CelExternalConsumerSampleTests.cs` already proves the public API is self-contained (no `ArchLinterNet.Core` reference) but consumes CEL via `ProjectReference`, which never exercises the actual `.nuspec`, packed assembly, or README a real consumer would get from NuGet. The new CI step packs, then creates a throwaway `dotnet new console` project, adds a `PackageReference` to the local `.nupkg` via a file-system NuGet source, and runs a minimal compile/evaluate program — proving the shipped artifact (not just the source tree) works standalone. This runs only in CI (`package-validation.yml`), not as a permanent test project, to avoid adding a 5th solution project whose only job is exercising packaging mechanics.

### Blueprint restructuring preserves content, changes framing
The existing "Extension-direction matrix" (7 rows) already contains accurate, implementation-verified content for language evolution, host adapters, function catalog, execution backends, tooling/AST, and caching — that content is kept verbatim and only renumbered into the issue's required section numbers (3–8). Two genuinely new sections are added: "Public API stability boundary" (the issue asks for the 9 exclusions framed as public/internal boundary + direction, distinct from the existing "Prohibited shortcuts" table which is framed as "pattern + reason" for enforcement, not future direction) and "Extension governance checklist" (a process checklist, not architecture content — nothing existing covers it). A "Versioning and compatibility policy" section is added because profile versioning was previously scattered per-row and package SemVer lived only in `docs/reference/release-process.md` with no cross-link from the engine blueprint.

## Non-goals

- No new adapter packages, execution backends, or language features — this change only finalizes packaging/docs for what #325–#329 already shipped.
- No `Microsoft.DotNet.ApiCompat` tooling adoption — the existing reflection-based `CelPublicApiSurfaceApprovalTests.cs` baseline is documented as the current mechanism, not replaced.
