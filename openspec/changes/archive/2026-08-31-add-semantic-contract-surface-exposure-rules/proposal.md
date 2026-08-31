## Why

ArchLinterNet can already govern coarse dependencies, type placement, and attribute placement, but it cannot declare that a visible API or library contract must not expose a domain, persistence, framework, or other forbidden type through a nested signature or compiled contract metadata. The recursive, deterministic exposure facts delivered by #512 now make that policy decision possible without creating a competing public-API or semantic-role model.

## What Changes

- Add strict and audit semantic contract-surface exposure rules that select visible source surfaces with existing bounded structural, location, semantic-role, or reviewed-public-API evidence.
- Select forbidden exposed types with the existing type/semantic selector vocabulary and evaluate them against #512's cached, recursive visible-contract exposure records.
- Emit deterministic member/metadata-level findings containing the source surface, declaring member or type, stable exposure path, and assembly-qualified forbidden-target identity.
- Make configured source and target selections fail closed through the existing v0.8 applicability/evaluability projection when required evidence is incomplete or a required selector unexpectedly matches nothing.
- Integrate the family with the existing policy schema, strict/audit execution, canonical finding identity, baseline/ignore lifecycle, Human/JSON/SARIF/Testing outputs, and policy-authoring documentation.
- Reuse an effective #525 reviewed public-API surface when selected; consuming that membership neither recreates snapshot selection nor changes a type's primary semantic role.

## Capabilities

### New Capabilities

- `contract-surface-exposure-contracts`: Declarative strict/audit rules that prohibit selected visible contract surfaces from recursively exposing selected semantic or structural type targets.

### Modified Capabilities

- None. The existing `governance-applicability-evidence` matrix already defines the shared #92-family applicability contract; this change implements that established integration for the new capability.

## Impact

- **Core policy/evaluation:** a new Core contract family, validators, checker/evaluator, family registration, baseline identity dimensions, applicability evidence, and typed diagnostic payload projection.
- **Existing Core seams:** reuse `ArchitectureContractSurfaceExposureIndex`, `ArchitecturePublicApiSurface` materialization, type/role matchers, and normalized reporting; no public Core API or alternative snapshot grammar is introduced.
- **Schema and docs:** extend the 0.8 policy schema and document authoring, selectors, diagnostic paths, strict/audit behavior, and the distinction between API membership and semantic role.
- **Tests:** focused Core fixtures for direct and nested leaks, metadata paths, selector sources and targets, zero-match/incomplete evidence, strict/audit and normalized/baseline parity.
- **Non-goals:** runtime serialization/data flow, endpoint execution, DTO generation, semantic multi-role/tagging, or version-isolation policy specialization (#514).
