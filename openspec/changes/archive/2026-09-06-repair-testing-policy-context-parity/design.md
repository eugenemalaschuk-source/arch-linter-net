## Context

The CLI and Testing builder currently enter policy weakening through the Core formatter, but the import and validation logic is located beside the weakening result formatter. Issue #788 shows why the artifact reader must be an explicit shared policy-context boundary: the CLI-exported schema-5 document is the source artifact, and both consumers must bind to the same schema/kind and completeness rules.

The change is limited to the policy-context JSON boundary. The comparer already owns normalized weakening semantics, and the debt gate already keeps persistent debt and policy weakening independent.

## Goals / Non-Goals

**Goals:**

- Establish one Core-owned reader/validator for versioned policy-context JSON.
- Keep both CLI and Testing callers on that reader without duplicating schema knowledge.
- Prove the full serialized-artifact path with Testing regression coverage, including an error-level weakening and an unchanged control.
- Preserve fail-closed validation and existing public formatter behavior.

**Non-Goals:**

- No new policy-context schema version or migration of old artifacts.
- No change to weakening classifications, severity, identity, baseline lifecycle, or gate semantics.
- No runtime assembly analysis during context import.
- No change to package publication or release versioning rules.

## Decisions

1. **Keep import authority in Core.** Extract the JSON deserialization and effective-policy evidence validation into a focused internal policy-context reader. The public weakening formatter delegates to it, so existing CLI and Testing call sites retain their API while sharing the same implementation.

2. **Validate before comparison.** The reader continues to require the current schema version, stable kind, policy identity, guardrail severity, all required collections, and complete typed evidence. Unknown optional JSON fields remain harmless, but missing required evidence remains an error.

3. **Test the artifact boundary, not only records.** Build contexts through the same deterministic JSON formatter used by the CLI, write both artifacts, and pass their paths through `ArchitectureValidationBuilder`. The regression then exercises serialization, file loading, deserialization, comparison, and gate projection together.

4. **Use focused Testing fixtures.** The regression uses a small policy pair with the same identity and a strict-to-audit contract change, plus the same-context control. It avoids project or assembly analysis so a schema-parity failure cannot be masked by unrelated preflight state.

## Risks / Trade-offs

- [Risk] Moving validation code can accidentally loosen fail-closed checks. → Preserve the existing validation predicates and retain the incomplete-artifact regression.
- [Risk] A future schema change can update the exporter without updating the consumer path. → Keep the serialized schema-5 Testing regression and the shared-reader dependency visible in the tests and archived spec.
- [Risk] The focused fixture may not reproduce every external package-resolution problem. → Keep package-level parity as an explicit test boundary and report any remaining environment-specific resolution issue separately from source behavior.
