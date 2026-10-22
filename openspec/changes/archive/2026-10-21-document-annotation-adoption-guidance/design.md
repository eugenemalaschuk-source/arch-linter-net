## Context

The v0.10 annotation wave has merged its optional source-only package, exact-FQN classifier integration, 49 role attributes, and six context-only attributes. `architecture/semantic-annotations.v1.json` is the reviewed source of truth for identities, valid targets, metadata, and compatibility. Existing policy documentation already defines YAML as governance authority and keeps semantic role distinct from reviewed API membership, but adopters lack an end-to-end migration guide and a complete public reference tied to that manifest.

The package is prepared for the stable v0.10 line and is excluded from preview package sets. Documentation must describe this availability accurately and must not imply that preview or older released tools support canonical annotations.

## Goals / Non-Goals

**Goals:**

- Give adopters a minimal annotation-first path from package reference through semantic facts to authored YAML selectors/contracts and optional semantic-role coverage.
- Preserve and explain user-owned attribute mappings as an equally supported alternative.
- Make migration behavior-preserving and distinguish roles, context metadata, CLR visibility, and reviewed API membership.
- Keep AI guidance aligned with the canonical catalog, YAML authority, single-role model, and #525 membership boundary.
- Publish every canonical role and context-only annotation with manifest-backed reference details and drift validation.

**Non-Goals:**

- Changing classifier, policy, package, schema, compatibility, or public API behavior.
- Publishing the package or claiming preview artifacts support the annotations.
- Automatically rewriting source or policy, weakening policy during migration, or adding private adopter topology.
- Adding roles or broad framework-specific tutorials beyond the issue's examples.

## Decisions

1. **Add one public adoption guide and one public reference page.** Put workflow, compatibility, migration, role/context/API distinctions, and synthetic examples in `docs/guides/semantic-annotations.md`; put the exhaustive catalog in `docs/reference/semantic-annotation-catalog.md`. Link both from MkDocs navigation and the existing semantic-classification page.

2. **Use the versioned JSON manifest as catalog authority.** Document the 49 `roles[]`, six `contextAttributes[]`, and assembly compatibility marker. Keep the readable table in Markdown, but add a focused parity test that derives the expected catalog rows from `architecture/semantic-annotations.v1.json` and fails when identities, targets, metadata properties, assembly support, or compatibility values drift. Capability projection parity remains covered by existing tests.

3. **Show semantic facts feeding authored YAML.** Examples use established `role` and `metadata` selectors/contracts. Explain that attributes add evidence only; YAML continues to define permissions, enforcement, and reviewed membership.

4. **Preserve the custom mapping path and safe migration sequence.** Show user-owned type and assembly attributes mapped by exact full name. Migration guidance requires before/after fact comparison, intentional context preservation, and removing redundant mappings only after parity is proven; it discourages policy weakening and states that working custom mappings need not migrate.

5. **Use synthetic, representative examples.** Cover layered/DDD modules, CQRS/API, infrastructure adapters, Unity/client roles, a custom user-owned attribute, context conflict behavior, and a reviewed API selector composed with a semantic role. Do not annotate production code solely to showcase catalog entries.

6. **Update AI guidance where adopters already look.** Extend semantic-role governance and capability/policy-authoring guidance to prefer an exact canonical match, preserve custom mappings otherwise, use context metadata rather than role overloading, and retain one primary role plus orthogonal API membership.

## Risks / Trade-offs

- **A hand-maintained table could drift from the manifest** → Compare its catalog block to rows projected from the manifest in a focused automated test.
- **Examples could imply the package is already published or that annotations enforce boundaries** → State the package/tool availability boundary and repeat that policy must independently select the facts.
- **A new guide could duplicate detailed policy mechanics** → Link to the existing semantic-classification, coverage, and public-API contract pages for full syntax and behavior.

## Migration Plan

No runtime or package migration is required. Publish the documentation through the normal site build with the existing v0.10 release lifecycle. Existing custom mappings remain valid; adopters may keep them or migrate incrementally using the documented parity checks.

## Open Questions

None. The closed #566–#572 issues and the manifest settle the annotation contract; #525 settles reviewed API membership semantics.
