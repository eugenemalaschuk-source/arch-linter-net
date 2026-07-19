## Context

Semantic port-boundary (`strict_port_boundaries`/`audit_port_boundaries`) and
layout-convention (`strict_layout_conventions`/`audit_layout_conventions`)
contract families are fully implemented, with CEL `when` predicates wired into
both selector matching (contextual contracts) and `files_matching.when`
(layout conventions). Two distinct fixture styles already exist in the repo,
and this change must pick the right one for each acceptance criterion:

1. **`samples/policies/imports/{modular-monolith,unity-client}/`** — realistic,
   multi-fragment example policies with import composition. Their only current
   test (`ArchitecturePolicyImportAcceptanceTests`) proves the imported and
   monolithic forms compose to an equivalent model and that the documented
   recommended-fragment-name conventions load. `analysis.target_assemblies`
   references illustrative assembly names (`MyCompany.Product.Modules.Sales`,
   etc.) that do not exist as compiled assemblies in this repo, so these
   samples are **structurally validated only** — they prove the YAML shape is
   schema-valid and composes correctly, not that a violation is actually
   detected at runtime.
2. **CLI-level fixture pairs** (`ContextualContractCliFixtures.cs` +
   `ContextualContractCliTests.cs` in `ArchLinterNet.Cli.Tests`) — real
   compiled C# types with marker attributes, a YAML policy written to a temp
   file at test time targeting `ArchLinterNet.Cli.Tests` as the
   `target_assemblies` entry (the running test process's own already-loaded
   assembly), run through the real `ValidateCommandHandler`. This is the only
   existing pattern that proves an actual pass/fail/JSON-diagnostic outcome
   end-to-end through the production CLI.

#169's acceptance criteria mix both needs: "at least one modular-monolith CEL
policy fixture exists" (style 1) and "fixtures include both passing and
violating cases" / "JSON or explain diagnostics are covered" (only provable
with style 2). Prior art for exactly this port-boundary/ACL combination already
exists at the Core-unit level in `PortBoundaryContractTests.cs`
(`CheckPortBoundaryContract_AclScenario_...`), but that test builds the
`ArchitectureContractDocument` by hand in C# — it does not prove a YAML file
loads and runs correctly end-to-end, which is what #169 actually asks for.

## Goals / Non-Goals

**Goals:**
- Extend the modular-monolith sample with a `Catalog` context (approved port
  seam + forbidden direct reference) and a `LegacyCrm` context (ACL seam vs.
  forbidden direct infrastructure), and a Services/Interfaces layout-convention
  contract, using only vocabulary already reviewed in `semantic-role-catalog`.
- Extend the Unity-client sample with a layout-convention contract expressing
  the Runtime/Editor/Features classification already implied by that sample's
  existing fragment structure.
- Add one CLI-level fixture pair proving, through the real `ValidateCommandHandler`,
  that each new contract shape (port boundary, ACL, layout convention including
  a `when`-refined case) produces the expected pass/fail outcome, that strict
  fails the build while the audit equivalent does not, and that `--format json`
  output contains the expected diagnostic fields.
- Update `docs/contracts/port-boundary.md`, `docs/contracts/layout-conventions.md`,
  and `docs/ai/policy-authoring-guide.md` to point at the exact tested fixture
  files/paths rather than only a standalone prose snippet.

**Non-Goals:**
- Do not make the `samples/policies/imports/*` samples runnable against real
  compiled types — that would require inventing a full `MyCompany.Product.*`
  compiled solution, which is out of scope and not requested by #169's
  acceptance criteria (only "fixture exists" and "loadable", not "runs against
  real code").
- Do not add new CEL grammar, new contract fields, or new role/metadata names.
  Every selector, role, and metadata key used here must already exist in
  `semantic-role-catalog`, `semantic-port-boundary-contracts`, or
  `layout-convention-contracts`.
- Do not implement `#173`'s Sales/Catalog *adapter-to-port consistency*
  (`adapter_bindings`) example beyond what's already covered by
  `PortBoundaryContractTests.cs` — #169's acceptance criteria list does not
  require a new adapter-binding fixture, only port-seam, forbidden-direct, and
  ACL scenarios.

## Decisions

- **Two-tier fixture strategy** (see Context): keep the illustrative
  `samples/policies/imports/*` YAML for narrative/doc-facing realism, and add
  a *separate* fully-runnable CLI fixture pair for provable pass/fail/JSON
  behavior. Rejected alternative: making `samples/policies/imports/*` runnable
  by inventing compiled `MyCompany.Product.*` projects — too large a surface
  change for an examples/fixtures task and would require maintaining a second
  compiled sample solution indefinitely.
- **New bounded-context fragments, not edits to `sales.arch.yml`'s existing
  rule**: add `policy/bounded-contexts/catalog.arch.yml` and
  `policy/bounded-contexts/legacy-crm.arch.yml`, and add a new
  `strict_port_boundaries`/`strict_layout_conventions` block to `sales.arch.yml`
  and a new `application-services.arch.yml`-style fragment, rather than
  overloading the existing `sales-domain-does-not-use-other-domains` contextual
  rule. Keeps each fragment focused on one owning concern, per the existing
  `policy-authoring-guide.md` fragment-ownership rule.
- **CLI fixture naming mirrors `ContextualContractCliFixtures`**: new types
  named with a distinguishing prefix (e.g. `PortLayoutCliSalesCheckout`,
  `PortLayoutCliCatalogPort`) in a new `PortLayoutCliFixtures.cs` /
  `PortLayoutCliTests.cs` pair, kept in `ArchLinterNet.Cli.Tests` rather than
  shared from `ArchLinterNet.Core.Tests`, matching the documented rationale in
  `ContextualContractCliFixtures.cs` (the running test assembly must self-host
  the target types).
- **The CEL `when`-backed example lives in the layout-convention CLI fixture**,
  using `files_matching.when` (already implemented and tested at Core level in
  `LayoutConventionContractTests.cs`) rather than a contextual-contract
  `selector.when`, because the layout family's `when` semantics are the newer,
  less-exercised-at-CLI-level path and pairs naturally with the
  Services/Interfaces counterpart scenario already planned.
- **`sample-policy` spec is the modified capability**, not a new capability —
  the new fixtures extend exactly the requirements that spec already owns
  ("Modular-monolith import example is realistic and executable", "Unity
  client import example is realistic and executable") plus new CLI-fixture
  requirements that logically belong to the same "sample policy" capability
  rather than a separate one.

## Risks / Trade-offs

- **Illustrative samples could drift from actually-supported YAML shape** since
  they're never run against real types → mitigated by keeping every new
  `samples/policies/imports/*` contract byte-for-byte identical in shape to a
  CLI-fixture contract that *is* run, and by the existing schema-validation
  test (`Load_...ProduceEquivalentModels`) still catching structural breakage.
- **Two fixture styles could confuse future contributors about which to copy**
  → mitigated by a short doc note (in `docs/ai/policy-authoring-guide.md`)
  pointing at the CLI fixture as the proven/tested shape and the sample policy
  as the narrative/composition-focused shape.
- **New bounded-context fragments increase `modular-monolith` sample size** →
  kept deliberately small (one contract family per new fragment, matching
  existing `sales.arch.yml`/`inventory.arch.yml` size).

## Migration Plan

Additive only — no existing contract, fixture, or doc section is removed or
behaviorally changed. No rollback concerns beyond normal revert.

## Open Questions

None — all prerequisite capabilities are closed and confirmed implemented by
exploration of `PortBoundaryContractTests.cs`, `LayoutConventionContractTests.cs`,
and `ContextualContractCliTests.cs`.
