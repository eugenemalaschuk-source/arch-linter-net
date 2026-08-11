## Why

`extract-family-validation-from-loader` moved post-deserialization family validation out of
`ArchitecturePolicyDocumentLoader`, but the `v0.4.0...v0.6.0` feature chain (policy imports, Core
CEL integration, source-set expansion, subtractive selector/layout/type-placement evolution) added
a second class of responsibility back into the same type: capability-specific **raw YAML** node
validation. The loader now owns six raw-node algorithms across four partial files (layers,
contextual/port-boundary contracts, semantic coverage, layout conventions, layer templates, and
`when` placement), each re-parsing the effective YAML and reaching into that capability's node
shapes.

The consequence is extension pressure, not a defect: adding one more raw policy-node rule means
editing a central all-capabilities validation method on the loader again, exactly the shape the
earlier extraction removed for post-deserialization validation.

## What Changes

- Introduce an internal, deterministic raw-policy-document validation pipeline
  (`IArchitecturePolicyRawDocumentValidator` + an ordered pipeline) mirroring the existing
  post-deserialization `IArchitecturePolicyDocumentValidator` seam, so raw node validation has a
  focused per-capability home.
- Move the layer, contextual-contract, port-boundary, semantic-coverage, layout-convention,
  layer-template and `when`-placement raw checks out of the loader into dedicated raw validators,
  preserving their exact order, diagnostics, and provenance/validation-subject behavior.
- Parse the effective YAML once into a shared raw-document context instead of once per raw check,
  keeping the parse at the same semantic point (after composition/effective-schema validation,
  before deserialization).
- Move the deferred `classification.path` raw-node detection and fallback-ID assignment into their
  own focused helpers so the loader body stays orchestration.
- Keep root resolution, import composition, effective-schema validation, deserialization,
  provenance binding, API-snapshot resolution, source-set expansion and the document-validator
  pipeline as explicit, unchanged, deterministically ordered loader stages.
- Add architecture-level regression coverage that fails when raw YAML node algorithms are
  reintroduced onto the loader or when a raw validator is not registered in the pipeline.

No policy syntax, schema, runtime semantics, or public API changes. Same policy bytes, imports and
filesystem state produce the same document, or the same failure category, message and location.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `policy-document-validation-pipeline`: Add the raw-YAML validation seam, its fixed order, and the
  loader-as-orchestrator boundary alongside the existing post-deserialization validator pipeline.

## Impact

Affected areas are internal to `ArchLinterNet.Core.Contracts`: the policy document loader and its
raw-validation partials, a new `Contracts/RawValidators` folder, and NUnit regression coverage in
`ArchLinterNet.Core.Tests`. No public API, schema, documentation-visible policy behavior, or
consumer-facing artifact changes.
