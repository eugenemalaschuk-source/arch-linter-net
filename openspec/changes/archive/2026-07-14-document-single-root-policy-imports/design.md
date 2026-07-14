## Context

The loader already implements one graph-selected root, local nested fragments, deterministic composition, source provenance, and separate root/fragment schemas. The remaining gap is adoption: the approved material is internal, the existing public recipes are monolithic, and current import tests mostly construct temporary YAML rather than treating public examples as acceptance fixtures.

The change spans MkDocs content, repository samples, NUnit tests, and OpenSpec synchronization. Documentation must describe implemented behavior exactly, preserve the CLI's existing default-path documentation, and distinguish recommended naming from runtime semantics.

## Goals / Non-Goals

**Goals:**

- Give users one public import guide that is complete enough to author, migrate, troubleshoot, and configure editor validation without consulting internal design notes.
- Make both recommended and arbitrary naming visibly equivalent while recommending `architecture/arch.yml` and concern-specific `*.arch.yml` fragments.
- Turn realistic modular-monolith and Unity split policies into executable repository fixtures.
- Exercise public fixtures through NUnit so documentation drift is caught by `make acceptance`.
- Teach AI agents to edit the smallest concern-focused fragment and preserve global composition constraints.

**Non-Goals:**

- Change import resolution, composition, diagnostics, schemas, CLI defaults, or Testing adapter APIs.
- Implement semantic selectors/CEL beyond documenting how schema-approved scalar values compose.
- Add a new fixture framework, snapshot library, or general documentation test abstraction.
- Rewrite existing monolithic recipes or unrelated documentation.

## Decisions

### Use one canonical public imports guide

Add `docs/policy-format/imports.md` and link it from policy format, schema reference, troubleshooting, README, and MkDocs navigation. A single guide avoids duplicating subtle composition/path rules across several pages; the other pages provide concise, contextual links.

Alternative: distribute all details across policy format, schema reference, migration, and troubleshooting pages. Rejected because ordering and unsupported-behavior rules would drift and be harder to review as one contract.

### Keep realistic fixture trees under `samples/policies/imports`

Store modular-monolith and Unity/client monolithic and split variants with the public sample policies. The split variants use `architecture/arch.yml` plus concern-specific fragments and retain a small/shared section inline in the root. A compact arbitrary-name fixture proves filename independence without duplicating every realistic recipe.

Alternative: promote `docs/internal/policy-import-examples` directly. Rejected because those files are design handoff material, contain stale candidate-fixture framing, and are intentionally excluded from the public documentation boundary.

### Add a focused fixture acceptance test class

Add NUnit tests that load committed fixtures through `ArchitecturePolicyDocumentLoader`. Compare normalized behaviorally relevant projections rather than object equality, and assert stable `CompositionConflict` categories plus both source paths for root/fragment and fragment/fragment conflicts.

Alternative: extend temporary-file unit tests only. Rejected because issue #283 requires acceptance fixtures and public example drift must fail repository validation.

### Document schema selection explicitly

The root schema remains `schema/dependencies.arch.schema.json`; fragments use `schema/dependencies.arch.fragment.schema.json`. Show YAML language-server directives and editor file associations as optional conveniences. State that runtime chooses roles from the selected entry path/import graph and never from editor configuration or filenames.

### Treat CEL/semantic selector examples as availability-gated

Only include selector syntax already documented as active. If a semantic field is schema-approved but deferred, identify it as deferred and do not present it as an executable import feature. Imports carry owning scalar/node values; they do not add evaluation, filesystem, or interpolation powers.

## Risks / Trade-offs

- [Large fixture YAML becomes hard to compare] → Assert normalized layers, analysis inputs, contract family/ID/order, and loader outcomes rather than raw text or complete DTO serialization.
- [Documentation accidentally implies mandatory names] → Use explicit “recommended convention” language and pair it with an arbitrary-name fixture and acceptance assertion.
- [Root schema guidance conflicts with fragment-deferred sections] → Keep public root examples complete after composition and explain that runtime validates source roles first and the effective policy after composition; use the fragment schema for every imported source.
- [Examples use capabilities that are still deferred] → Base fixtures on existing executable policy recipes and label deferred semantic syntax instead of claiming enforcement.
- [Generated docs navigation or links drift] → Run formatting and the full acceptance target, which includes strict MkDocs validation.

## Migration Plan

This is additive documentation and test data. Existing monolithic policies and examples remain valid. Users migrate by keeping their current file as the sole root, moving one concern at a time into imported fragments, validating after each move, and preserving definitions and contract order. Rollback is to inline fragment sections back into the root and remove `imports` without changing the effective policy.

## Open Questions

None. Runtime semantics and schema locations are already fixed by issues #280–#282 and the `policy-import-composition` specification.
