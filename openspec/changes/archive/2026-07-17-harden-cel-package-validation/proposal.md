## Why

PR review on the `finalize-cel-package-release-readiness` work (#330) found that `.github/workflows/package-validation.yml`'s CEL checks were narrower than the acceptance criteria they were meant to enforce: the CEL package-content check only rejected NuGet `<dependency>` elements (not forbidden files, nor confirming `README.md`/`ArchLinterNet.CEL.xml` are actually present), and the Core → CEL edge check only confirmed a dependency with the right ID exists — not that its version matches the packed CEL package, and not that Core doesn't also embed `ArchLinterNet.CEL.dll` alongside declaring the dependency. Both gaps mean CI could stay green while publishing an invalid package graph.

## What Changes

- Extend the CEL package-content check to assert `README.md`, `lib/net10.0/ArchLinterNet.CEL.dll`, and `lib/net10.0/ArchLinterNet.CEL.xml` are all present in the packed `.nupkg`, and to reject any entry matching Core/CLI/Testing assemblies or YAML/JSON-schema/Buildalyzer/Roslyn assets.
- Extend the Core → CEL edge check to parse the actual dependency version out of Core's `.nuspec` and assert it equals the packed CEL package's own `<version>`, and to assert Core's package listing does not contain `lib/net10.0/ArchLinterNet.CEL.dll` (no embedding alongside the declared dependency).

## Capabilities

### Modified Capabilities

- `cel-package-release-readiness`: the "CEL package is dependency-clean" and the (previously undocumented) Core→CEL dependency requirements now cover package-content and version-matching validation, not just dependency-list emptiness/presence.

## Impact

- `.github/workflows/package-validation.yml` — stronger CEL package-content and Core→CEL version/embedding assertions
- No production code changes; CI-only hardening
