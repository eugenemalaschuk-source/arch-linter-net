## Why

The v0.10 semantic annotation package, classifier integration, role families, and context metadata are now implemented, but adopters need one accurate path through installation, classification, YAML governance, and migration. Without it, users may mistake annotations for permissions, conflate context or reviewed API membership with a primary role, or rely on a catalog reference that has drifted from the shipped manifest.

## What Changes

- Add public annotation-first adoption, compatibility, and behavior-preserving migration guidance with representative .NET and YAML examples.
- Keep the user-owned attribute plus `classification.attributes` / `classification.assembly_attributes` path prominent as a first-class alternative.
- Explain primary roles, context metadata, CLR visibility, and reviewed public-API membership as separate concepts; update AI authoring guidance to preserve those boundaries.
- Publish a complete annotation reference projected from or parity-tested against the canonical manifest, including exact identities, targets, metadata, assembly use, compatibility, and policy examples.

## Capabilities

### New Capabilities

### Modified Capabilities

- `semantic-annotation-contract`: require complete, parity-checked public adoption and compatibility guidance for canonical role and context annotations.

## Impact

Public MkDocs guides and reference pages, AI policy-authoring documentation, annotation catalog documentation, the existing `semantic-annotation-contract` spec, and focused documentation-parity validation. No runtime behavior, package contents, schema, or public API changes are intended.
