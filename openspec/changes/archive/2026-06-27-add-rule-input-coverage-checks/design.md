## Context

`architecture-coverage-model` (archived) already fixed the YAML shape for `scope: rule_input`:

```yaml
contracts:
  strict_coverage:
    - name: rule-input-coverage
      scope: rule_input
      contract_ids: [some-existing-contract-id]
      reason: Flag if this contract's matchers stop matching any code (stale rule).
```

`ArchitectureCoverageContract.ContractIds` is already bound on the model. `ArchitectureCoverageInventory` (#97) already exposes `Namespaces` (discovered namespace → representative type) and `DeclaredLayers`. `namespace-coverage-contracts` (#129) already wired severity end-to-end (`analysis.coverage`, `strict_coverage`/`audit_coverage` groups, `ValidationOutcome.CoverageFindings`/`CoverageConfig`). This change only needs to make `CheckCoverageContract` handle `scope: rule_input` instead of throwing, plus the loader validation specific to that scope.

A related but distinct existing check is `policy_consistency`'s `unreachable-contract` (#66): it flags layers whose namespace pattern is *structurally* impossible (e.g. a blank `namespace_suffix`) by static analysis of the pattern alone, never touching discovered code. This change's `empty-input` classification is the complementary, code-aware check: a structurally valid pattern that currently resolves to zero real namespaces in the analyzed codebase. The two checks do not duplicate each other and both can fire independently.

## Goals / Non-Goals

**Goals:**
- Implement `scope: rule_input` coverage contracts that classify each `contract_ids` entry as `unresolved` (dangling layer-name reference) or `empty-input` (pattern resolves, matches no first-party code), or otherwise not reported (it matched real code).
- Reuse the existing `GetReferencedLayerNames`-style per-contract-family field extraction already used by `policy_consistency` rather than re-deriving which fields are "rule inputs" per contract type.
- Provide an explicit, reason-required escape hatch for intentionally empty/unresolved contract references.
- Keep the existing `ArchitectureViolation` finding shape (no new diagnostic record), matching the precedent set by `namespace-coverage-contracts`.

**Non-Goals:**
- No historical staleness tracking (e.g. "this used to match 3 namespaces, now matches 0, when did that change"). "Stale" and "empty-input" are treated as the same current-state signal: a contract's resolved pattern set currently matches zero first-party code. There is no point-in-time comparison anywhere else in this codebase to build that on, and the issue's acceptance criteria do not require history.
- No implementation of `scope: project`, `scope: assembly`, or `scope: dependency_edge` (#99/#100) — those remain rejected as reserved.
- No coverage reporting/CLI rendering changes beyond what `namespace-coverage-contracts` already wired (#102 owns dedicated reporting work).
- No baseline/CI gate behavior changes (#103).

## Decisions

1. **`unresolved` vs `empty-input` are two distinct, separately-named findings, not one.** A dangling layer name (typo, deleted layer) is a configuration-authoring mistake; a structurally valid layer whose pattern currently matches no code is a code-drift signal (the rule went stale, or was always over-scoped). Conflating them would make the diagnostic message less actionable — an author fixing a typo needs a different fix than an author deciding whether to retire a rule. Both share the same `scope: rule_input` contract and the same exclusion escape hatch, so the distinction costs nothing extra in the YAML surface, only in the finding's reported reason text.

2. **Rule-input resolution reuses `GetReferencedLayerNames`'s per-family field mapping, relocated to a shared helper.** `ArchitectureContractRunner.PolicyConsistency.cs` already has the exact "which fields on which contract type are its layer/namespace inputs" switch (`Source`/`Forbidden`/`Allowed`/`Layers`/`Protected`/`AllowedImporters`/etc., per contract family). The coverage runner needs the identical mapping to resolve a referenced contract's inputs. Rather than duplicating the switch, it is extracted to an internal shared method (e.g. on a small static helper or kept as an internal static method visible to both partial-class files) and called from both `CheckCoverageContract` and `FindUnreachableContracts`. This avoids the two checks silently drifting apart on which fields count as "rule inputs" for a given contract family.

3. **A referenced field value is `unresolved` only when it is not a declared layer name AND does not parse as an inline namespace pattern.** Some contract families' "layer" fields are always named-layer references (e.g. `ArchitectureLayerContract.Layers`, `ArchitectureIndependenceContract.Layers`); others (`ArchitectureDependencyContract.Source`/`Forbidden`) are conventionally also named-layer references in this codebase (confirmed by `FindUnreachableContracts`'s existing `GetReferencedLayerNames` treating them identically). This change follows that same existing convention: every referenced field value is looked up against `document.Layers` by name; a value not found there is `unresolved`. This matches the existing `unreachable-contract` check's treatment of the same fields, so no new resolution rule is invented for this change — the only new step is layering an `empty-input` (code-resolution) check on top of layer names that DO resolve.

4. **`empty-input` resolution uses `ArchitectureCoverageInventory.Namespaces`, matched via `ArchitectureLayerResolver.MatchesNamespace`, identical to how `namespace-coverage-contracts` and `policy_consistency`'s layer-overlap check already match namespaces against layer patterns.** No new matcher is introduced. A referenced layer is `empty-input` when `Namespaces` contains zero entries matching that layer's pattern.

5. **The escape hatch extends `ArchitectureCoverageExclusion` with an optional `contract_id` field, additive and orthogonal to its existing `namespace`/`namespace_suffix`/`project`/`assembly` fields.** This mirrors the namespace-scope exclusion shape (a typed matcher plus mandatory `reason`) rather than inventing a separate "ignore" mechanism. An exclusion with `contract_id: some-id` suppresses both `unresolved` and `empty-input` findings for that specific referenced contract, requiring `reason` exactly like every other coverage exclusion.

6. **Loader validation for `scope: rule_input` mirrors the existing `scope: namespace` cross-field rejection pattern in `ValidateCoverageNamespaces`.** A `rule_input` contract must declare a non-empty `contract_ids` list and must not declare `roots` or `between` (those belong to other scopes); each `contract_ids` entry must resolve to a real declared contract ID across all contract families, or the loader throws an actionable `InvalidOperationException` identifying the unknown ID at load time — distinct from the runtime `unresolved`/`empty-input` findings, which are about a *referenced contract's own* layer inputs, not about whether the `contract_ids` list itself is well-formed.

7. **`ArchitectureRunnerFactory`'s unsupported-scope guard is narrowed from "anything but namespace" to "anything but namespace or rule_input."** This is the only change needed to stop rejecting `rule_input` contracts at the runner-factory level; `project`, `assembly`, and `dependency_edge` remain explicitly reserved per `CoverageContractReservedTests.UnsupportedCoverageScope_ThrowsActionableError`, which must keep passing for `scope: project`.

## Risks / Trade-offs

- **[Risk] Extracting the shared `GetReferencedLayerNames`-style helper touches code already covered by `policy_consistency` tests.** Mitigation: the extraction is a pure relocation (same switch, same cases), not a behavior change; existing `policy_consistency` tests act as a regression guard, and no new contract-family case is added by this change.
- **[Risk] Treating `Source`/`Forbidden`/`Allowed` fields as always named-layer references could misclassify a contract that (incorrectly) uses something else there.** Mitigation: this is the exact same assumption `FindUnreachableContracts` already makes today; this change does not introduce new risk beyond what `policy_consistency` already accepts.
- **[Risk] A `rule_input` contract referencing another `rule_input`/coverage contract by ID is a confusing self-referential case (a coverage contract checking another coverage contract's "inputs", which are themselves `contract_ids`, not layers).** Mitigation: per the open question this design's parent left to #101, `contract_ids` is scoped to layer-bearing contract families only (dependency, allow_only, cycle, method_body, independence, protected, external, layer, layer_template) — referencing a coverage contract by ID is rejected by loader validation as an unknown/unsupported reference, since coverage contracts have no `GetReferencedLayerNames` mapping.

## Open Questions

None outstanding — the parent design's open question ("should `rule_input` be limited to dependency/layer/independence/protected IDs, or also include coverage contracts' own IDs") is resolved by Decision 7 above: scoped to layer-bearing contract families only, excluding coverage contracts.
