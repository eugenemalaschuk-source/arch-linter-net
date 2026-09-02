## Context

See [proposal.md](proposal.md) for the motivation. The delivered policy families already have bounded YAML syntax, shared recursive exposure evidence, selected public-API membership, and normalized output projections. Existing tests prove those capabilities separately, including the selected-surface snapshot lifecycle. This issue needs a realistic, fully declarative composition that makes their boundaries clear to adopters.

## Goals / Non-Goals

**Goals:**

- Prove a reference policy can use only existing contract families and selectors to govern DTO/domain/persistence leaks, marker placement, version isolation, reviewed API membership, and runtime/editor leakage.
- Make strict and audit behavior, recursive first-party path evidence, Human/JSON/SARIF/Testing projections, and selected-surface snapshot lifecycle evidence observable and deterministic.
- Give authors a short, linked composition guide that distinguishes each family’s responsibility.

**Non-Goals:**

- No production analyzer, schema, selector, public API, or snapshot-format change.
- No framework-specific preset, required built-in marker, runtime serialization/endpoint behavior, multi-role classification, or second snapshot lifecycle.

## Decisions

### Use a focused in-process reference scenario rather than a new analyzer feature

Add a dedicated NUnit fixture plus source-type fixture in `tests/ArchLinterNet.Core.Tests`. It will load a complete YAML policy and execute it through the normal Core runner, reporting strict and audit findings from actual reflected type signatures. This is the narrowest way to prove composition without introducing package/bootstrap cost or duplicating the existing packed-consumer selected-snapshot suite.

The reference types model two independently useful shapes:

- server-style v1/v2 contracts, DTO wrappers, domain entities, persistence types, and configured transport/serialization markers;
- library/runtime and editor-only types.

The test will select reviewed membership through an orthogonal `PublicApiContract` marker while preserving a `ValueObject` primary role. It will select a second surface via an existing namespace/interface/base/role selector. It will assert a recursive generic exposure path to an unselected first-party type and assert the existing snapshot selector’s fail-closed behavior, never auto-selecting an escaping type.

Alternative considered: extend one existing public-API-selector or exposure unit test. Rejected because it would leave the reference composition fragmented and make the authoring example difficult to find.

### Reuse current public-API snapshot evidence; test its composition rather than clone it

The existing `PublicApiSurfaceSelectorTests` establish selected capture semantics, material snapshot reduction, unselected first-party fail-closed behavior, and whole-assembly backward compatibility. The packed adoption fixture exercises the complete selected-surface lifecycle: exact capture/diff deltas, `public-api update --dry-run` without changing the reviewed snapshot, and ordinary update resynchronization. The new reference test consumes a selected public-API surface through `strict_contract_surface_exposure` and asserts its role-preserving, path-rich results. Together these tests avoid a parallel snapshot grammar while keeping the preview behavior observable at the CLI boundary.

Alternative considered: add a second snapshot harness to the reference fixture. Rejected because duplicate lifecycle coverage would add maintenance without new semantic evidence.

### Keep documentation at the existing family entry points

Extend the four existing contract pages with concise cross-links and a composition section. The composition example will show marker placement (`attribute_usage`), intentional membership (`public_api_surface`), recursive target governance (`contract_surface_exposure`), and local version groups (`versioned_contract_surface_isolation`). It will state that strict is for established boundaries and audit is for migration discovery.

Alternative considered: create a new top-level guide. Rejected because the existing contract pages are the documented authoring entry points and the guidance must be discovered alongside each family.

### Delegate independent test and documentation slices

After this design and task plan are complete, two Luna workers can write in parallel with disjoint boundaries:

1. the two new Core test files only; and
2. the four existing contract documentation files only.

Neither worker may change analyzer production code, schema, existing selected-snapshot tests, OpenSpec files, or the other worker’s boundary. Terra integrates, formats, runs the required validation, synchronizes the OpenSpec task state, archives the change, and completes the commit/PR lifecycle.

## Risks / Trade-offs

- [The in-process test may fail to exercise a CLI-only projection.] → It asserts the shared typed Core findings and formatter projections; existing packed tests remain the authoritative CLI/Testing lifecycle evidence and are referenced rather than reimplemented.
- [A synthetic marker could be mistaken for a product-provided attribute.] → Both fixture and docs call it user-owned and leave it out of semantic classification.
- [A long YAML string could obscure the composition.] → Keep policy sections ordered by responsibility and test named outputs rather than implementation details.
- [Documentation examples can drift from schema.] → Run docs lint and policy/schema-focused tests, and reuse only currently supported field names.
