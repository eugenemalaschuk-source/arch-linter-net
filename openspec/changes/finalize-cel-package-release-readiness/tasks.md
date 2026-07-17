## 1. XML API documentation

- [ ] 1.1 Set `<GenerateDocumentationFile>true</GenerateDocumentationFile>` in `src/ArchLinterNet.CEL/ArchLinterNet.CEL.csproj`
- [ ] 1.2 Build the CEL project and confirm no new `CS1591` (or other doc-comment) warnings fail the build (`TreatWarningsAsErrors` is repo-wide)

## 2. CEL-specific package README

- [ ] 2.1 Write `src/ArchLinterNet.CEL/README.md`: Profile v1 subset summary, compile/evaluate code example, limits/diagnostics/thread-safety summary, explicit non-goals, link to NuGet/docs
- [ ] 2.2 Add `PackageReadmeFile` + `<None Include>` override in `ArchLinterNet.CEL.csproj` so this README (not the shared root README) is packed for this project only

## 3. CI package validation

- [ ] 3.1 Extend `.github/workflows/package-validation.yml`: unzip the CEL `.nuspec` and assert it has zero `<dependency>` elements
- [ ] 3.2 Add an external-consumer smoke-test step: pack, create a throwaway `dotnet new console` project, add a `PackageReference` to the local `.nupkg` via a file-system NuGet source, write a minimal schema/compile/evaluate program, run it, assert the expected result

## 4. Internal architecture blueprint reconciliation

- [ ] 4.1 Restructure `docs/internal/cel-engine-architecture.md`: wrap "Processing pipeline" + "Component ownership" under a new "1. Engine lifecycle and module graph" heading
- [ ] 4.2 Add new "2. Public API stability boundary" section: 9 v1 exclusions from #324, each with future architectural direction
- [ ] 4.3 Renumber existing extension-direction matrix rows 1–6 to sections 3–8 (language/profile evolution, host adapters, function catalog, execution backends, tooling/AST, caching/serialization); keep "Diagnostics and explainability" as an additional unnumbered section
- [ ] 4.4 Add new "9. Extension governance checklist" section with the issue's 10-question checklist
- [ ] 4.5 Add new "Versioning and compatibility policy" section: profile SemVer rules, package release SemVer (cross-link `docs/reference/release-process.md`), `CelPublicApiSurfaceApprovalTests.cs` baseline mechanism
- [ ] 4.6 Update the document's intro/status line (currently says "before any implementation tasks... begin") to reflect #325–#329 are shipped and #330 is the finalization pass

## 5. Validation

- [ ] 5.1 Run `rtk make fmt` and inspect formatting changes
- [ ] 5.2 Run `rtk make acceptance`
- [ ] 5.3 Run `rtk make pack` and manually inspect `nupkg/ArchLinterNet.CEL.*.nupkg` contents (README, XML doc, nuspec)
- [ ] 5.4 Run `rtk make lint-docs`

## 6. Spec synchronization and archive

- [ ] 6.1 Compare implementation against `specs/cel-package-release-readiness/spec.md` and update if behavior diverged
- [ ] 6.2 Run `openspec validate --all`
- [ ] 6.3 Run `openspec archive finalize-cel-package-release-readiness`
- [ ] 6.4 Run `openspec validate --all` again after archive
