## Why

A second review pass on `finalize-cel-package-release-readiness` (#330) found two remaining gaps: the CEL package forbidden-content regex only matched forbidden content by *library name* (`YamlDotNet`, `JsonSchema.Net`, ...), so a raw `.yml`/`.yaml`/`.schema.json` file packed by accident (with no library name in its path) would pass CI undetected; and `docs/internal/cel-engine-architecture.md`'s versioning policy contradicted itself — "Profile versioning" declared `CelProfile.V1` permanently frozen, while "Package release versioning" said a Profile v1 semantics change was an allowed pre-1.0 minor-bump event, implying such a change was permitted at all.

## What Changes

- Extend the CEL package-content forbidden pattern in `.github/workflows/package-validation.yml` to also reject any packed entry ending in `.yml`, `.yaml`, or `.schema.json`, regardless of which library or process produced it.
- Rewrite the "Package release versioning" section of `docs/internal/cel-engine-architecture.md` to remove the contradictory claim that a Profile v1 semantics change is an allowed release event; Profile v1 semantics are never a version-bump lever, only introducing a new profile (e.g. v2) is.

## Capabilities

### Modified Capabilities

- `cel-package-release-readiness`: the CEL package-content requirement now also forbids raw YAML/JSON-schema files by extension, not just by known library name; the versioning-policy documentation requirement now requires internally consistent profile/package versioning guidance.

## Impact

- `.github/workflows/package-validation.yml` — broadened forbidden-content pattern
- `docs/internal/cel-engine-architecture.md` — corrected "Package release versioning" section
