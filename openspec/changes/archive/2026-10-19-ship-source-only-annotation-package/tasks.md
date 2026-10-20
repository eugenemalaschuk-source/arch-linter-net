## 1. Pack the reviewed source set

- [x] 1.1 Add the pack-only `ArchLinterNet.Annotations` project with explicit NuGet metadata and `contentFiles/cs/any` compile inclusion, excluding all build output and executable package assets.
- [x] 1.2 Generate the assembly catalog identity source from the exact package version and catalog generation, and include the package project in the solution.
- [x] 1.3 Add the package README with install, compatibility, runtime, and Unity source-import guidance.

## 2. Prove packed external consumption

- [x] 2.1 Pack the actual project in an isolated local feed and verify the package content set, compile metadata, and absence of binary or executable assets.
- [x] 2.2 Build external PackageReference consumers with canonical annotations; verify internal visibility, exact catalog marker, no runtime closure asset, and multiple-project recognition.
- [x] 2.3 Verify central package management and package removal/clean rebuild behavior.

## 3. Distribute the package with compatible releases

- [x] 3.1 Extend immutable package manifests to represent the stable 0.10.x source package without a symbol subject while preserving validation of historical manifests.
- [x] 3.2 Add version-aware package packing and publication to the release workflow; preserve the existing package set for earlier development and maintenance lines.
- [x] 3.3 Update release workflow and package-manifest tests for compatible and earlier product lines.

## 4. Synchronize guidance and contracts

- [x] 4.1 Update NuGet-visible and user documentation to describe the optional source-only package, minimum compatible tool version, and supported consumer shapes.
- [x] 4.2 Run focused package tests, format and directly affected checks, execute the repository acceptance gate, and record its result for the PR.
- [x] 4.3 Archive this OpenSpec change, inspect synchronized specs, and run strict OpenSpec validation.
