## 1. XML API documentation

- [x] 1.1 Set `<GenerateDocumentationFile>true</GenerateDocumentationFile>` in `src/ArchLinterNet.CEL/ArchLinterNet.CEL.csproj`
- [x] 1.2 Build the CEL project and confirm no new `CS1591` (or other doc-comment) warnings fail the build (`TreatWarningsAsErrors` is repo-wide) — fixed 8 pre-existing `cref`/`paramref` doc-comment errors (`CS0419`/`CS1734`/`CS1574`) surfaced only once `GenerateDocumentationFile` was enabled

## 2. CEL-specific package README

- [x] 2.1 Write `src/ArchLinterNet.CEL/README.md`: Profile v1 subset summary, compile/evaluate code example, limits/diagnostics/thread-safety summary, explicit non-goals, link to NuGet/docs
- [x] 2.2 Add `PackageReadmeFile` + `<None Include>` override in `ArchLinterNet.CEL.csproj` so this README (not the shared root README) is packed for this project only — required an explicit `<None Remove>` of the Directory.Build.props-inherited root README to avoid a duplicate-file pack error (NU5118)

## 3. CI package validation

- [x] 3.1 Extend `.github/workflows/package-validation.yml`: unzip the CEL `.nuspec` and assert it has zero `<dependency>` elements
- [x] 3.2 Add an external-consumer smoke-test step: pack, create a throwaway `dotnet new console` project, add a `PackageReference` to the local `.nupkg` via a file-system NuGet source, write a minimal schema/compile/evaluate program, run it, assert the expected result — verified locally end-to-end against a real packed `.nupkg`

## 4. Internal architecture blueprint reconciliation

- [x] 4.1 Restructure `docs/internal/cel-engine-architecture.md`: wrap "Processing pipeline" + "Component ownership" under a new "1. Engine lifecycle and module graph" heading
- [x] 4.2 Add new "2. Public API stability boundary" section: 9 v1 exclusions from #324, each with future architectural direction
- [x] 4.3 Renumber existing extension-direction matrix rows 1–6 to sections 3–8 (language/profile evolution, host adapters, function catalog, execution backends, tooling/AST, caching/serialization); keep "Diagnostics and explainability" as an additional unnumbered section
- [x] 4.4 Add new "9. Extension governance checklist" section with the issue's 10-question checklist
- [x] 4.5 Add new "Versioning and compatibility policy" section: profile SemVer rules, package release SemVer (cross-link `docs/reference/release-process.md`), `CelPublicApiSurfaceApprovalTests.cs` baseline mechanism
- [x] 4.6 Update the document's intro/status line (currently says "before any implementation tasks... begin") to reflect #325–#329 are shipped and #330 is the finalization pass

## 5. Validation

- [x] 5.1 Run `rtk make fmt` and inspect formatting changes — required re-indenting embedded heredoc content in the new CI step to match the YAML block scalar's indentation (flush-left content broke YAML parsing)
- [x] 5.2 Run `rtk make acceptance`
- [x] 5.3 Run `rtk make pack` and manually inspect `nupkg/ArchLinterNet.CEL.*.nupkg` contents (README, XML doc, nuspec)
- [x] 5.4 Run `rtk make lint-docs`

## 6. Spec synchronization and archive

- [x] 6.1 Compare implementation against `specs/cel-package-release-readiness/spec.md` and update if behavior diverged — no divergence
- [x] 6.2 Run `openspec validate --all`
- [x] 6.3 Run `openspec archive finalize-cel-package-release-readiness`
- [x] 6.4 Run `openspec validate --all` again after archive
