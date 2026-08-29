## Why

Architecture policies can declare local dependency contracts, but they cannot yet express a reviewed native component topology or prove that a declared topology accounts for the bounded first-party surface it claims to govern. Future topology evaluation must consume one deterministic, repository-local semantic model instead of inferring conventions from an external diagram language or from evaluator implementation details.

## What Changes

- Add an opt-in native `topology` policy section with stable component identities, explicit subject mappings, directional edges, declared completeness mode, bounded observed-subject scope, and reviewed out-of-scope declarations.
- Validate topology declarations deterministically, including identifiers, selector shapes, scope bounds, node/edge references, exact duplicate mappings, and configurations that would make an exhaustive claim vacuous.
- Define canonical mapping cardinality, declaration-drift, and completeness semantics for the later evaluator without implementing a topology evaluator or a new result envelope.
- Export topology policy facts and reviewed scope exclusions through the existing policy-context/weakening seam so a broadened exclusion remains visible to the delivered generic guardrail.
- Document the YAML model, compatibility guarantees, and the boundary between this schema work and later topology evaluation, diagram import, and normalized-output work.

## Capabilities

### New Capabilities

- `declared-topology-model`: Native, schema-backed topology declarations and their deterministic mapping/completeness semantics.

### Modified Capabilities

- `governance-applicability-evidence`: Define the topology family’s native mapping evidence and its composition with the existing applicability matrix.
- `policy-context-export`: Export deterministic effective topology facts and declaration provenance for policy comparison consumers.
- `policy-weakening-guardrails`: Recognize a statically provable broadening of reviewed topology scope exclusions without introducing a topology-specific weakening engine.

## Impact

The change affects the Core policy document model, policy validation and raw-schema validation pipelines, policy-context projection and generic weakening comparison, focused Core tests/fixtures, public policy-format documentation, OpenSpec specifications, and the reviewed Core public-API snapshot. It has no runtime topology evaluator, diagram parser, CLI command, or output-format change.
