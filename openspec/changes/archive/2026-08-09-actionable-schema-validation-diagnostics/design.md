## Context

`ArchitecturePolicyEffectiveSchemaValidator` converts the composed YAML document to JSON and evaluates the embedded policy schema with JsonSchema.Net list output. The current projection joins every invalid top-level detail and independently chooses the deepest error-bearing location. Composite keywords therefore leak errors from alternatives that cannot describe the authored value.

## Goals / Non-Goals

**Goals:**

- Select a small, deterministic set of schema errors that describe the applicable invalid value.
- Retain every genuinely independent failure and continue to use the existing typed source-shape diagnostic and provenance mapping.
- Make the primary summary location follow the selected deepest actionable error.

**Non-Goals:**

- Change the embedded JSON Schema, the JsonSchema.Net engine, or whether a policy is valid.
- Suppress independent schema defects or redesign CLI/JSON diagnostic envelopes.

## Decisions

1. Traverse `EvaluationResults.Details` as an ordered tree and project invalid leaf errors, rather than using only the top-level list. This retains evaluator structure required to distinguish container/composite failures from concrete value failures.
2. Prune an alternative when the instance lacks its required discriminator or has a conflicting `const` discriminator. This is a schema-local applicability test: it removes known false alternatives without inventing a generic score for arbitrary schemas.
3. Rank remaining errors by instance-location depth, then preserve evaluator encounter order for ties. This makes a child property/type failure primary while keeping output reproducible.
4. Keep the existing discovery-coverage special message because it communicates runtime-compatible semantics that the general schema error text cannot express.

## Risks / Trade-offs

- [Schema forms can encode applicability without `required` or `const`] → Only prune branches proven inapplicable; keep uncertain alternatives so no real error is hidden.
- [Evaluator nesting can vary by JsonSchema.Net version] → Test nested `anyOf` and conditional shapes through the shipped schema/evaluator API rather than depending on undocumented serialized output.
- [Several independent defects can share a path depth] → Retain all selected leaves and use stable traversal order for rendering.
