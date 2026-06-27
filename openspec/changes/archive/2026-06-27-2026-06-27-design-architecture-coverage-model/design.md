## Context

ArchLinterNet's existing contract families (`ArchitectureContractGroups`: Dependency, Layer, AllowOnly, Cycle, MethodBody, Asmdef, Independence, Protected, External, LayerTemplate, AcyclicSibling — `src/ArchLinterNet.Core/Contracts/ArchitectureContractModels.cs`) all answer the same shape of question: "given code that some contract's source/target layer pattern matches, is the observed dependency/structure allowed?" None of them answer "does any contract's pattern match this code at all?" A namespace, project, assembly, or dependency edge that no layer pattern matches is invisible — it produces zero diagnostics today, indistinguishable from a namespace that is deliberately and correctly unconstrained.

Two existing mechanisms are partial precedents but are intentionally narrow:
- `ArchitectureLayerContract.Exhaustive` + `ContainerNamespace` (layer templates, `Execution/LayerTemplateExpander.cs`, runtime check in `Execution/ArchitectureContractRunner.Checking.cs:128-157`) already detects "unmapped sibling namespace" — but only for direct children of one declared container namespace, only as a side effect of a layer-template contract, and with no exclusion/reason vocabulary, no project/assembly/edge equivalent, and no severity control independent of the template contract itself.
- `analysis.unmatched_ignored_violations` and `analysis.policy_consistency` (`error|warn|off`, plain `string` fields on `ArchitectureAnalysisConfiguration`, validated in `Validation/ArchitectureValidationService.cs`) establish the severity-configuration pattern this design reuses, but neither answers a coverage question — one is about ignored-but-unmatched baseline entries, the other about contract-vs-contract contradictions.

Story #57's child tasks (#97 inventory engine, #98 namespace coverage, #99 project/assembly coverage, #100 dependency-edge coverage, #101 empty-input/stale-rule coverage, #102 reporting, #103 baseline/CI) all need one stable vocabulary and YAML shape to implement against. This change designs that shape. It does not implement any checker, diagnostic kind, or pipeline step — see Non-Goals.

## Goals / Non-Goals

**Goals:**
- Define a coverage vocabulary precise enough that #97–#103 can implement against it without re-deciding terminology.
- Design a YAML shape for coverage contracts that follows existing conventions exactly: strict/audit family split, `name`/`id`/`reason` fields, layer-pattern reuse, and severity via `analysis.<setting>: error|warn|off`.
- Make coverage scope orthogonal to layer matching: coverage answers "is this matched by *something* (a layer, template expansion, or explicit exclusion)," not "is this matched by the *right* something" (that remains the job of dependency/layer/independence/protected contracts).
- Keep coverage contracts additive and opt-in so existing policies without them are provably unaffected.
- Produce a reviewed JSON-schema acceptance shape (`schema/dependencies.arch.schema.json`) that #97–#103 can implement against without re-litigating field names.

**Non-Goals:**
- Implementing the coverage inventory engine, any checker, diagnostic kind, or pipeline step (#97–#103).
- Runtime behavior validation of coverage contracts (no contract in this change ever executes).
- Semantic data-flow or security analysis of "covered" code.
- Unrestricted regex-based coverage matching — coverage scope reuses the same glob/namespace-suffix matchers layers already use (`namespace`, `namespace_suffix`), not a new pattern language.
- Code-ownership or review-ownership enforcement (explicitly out of scope per #57).
- Deciding final default severity behavior for production rollout beyond what's needed for backward compatibility (see Decision 7) — #103's baseline/CI design may refine onboarding defaults.

## Vocabulary

Coverage classifies every first-party unit in scope (namespace, project, assembly, or dependency edge) into exactly one of:

| Term | Meaning |
|---|---|
| **covered** | The unit matches a declared layer (namespace layer, glob layer, or layer-template container expansion) or an explicit coverage scope declaration. |
| **excluded** | The unit is deliberately out of scope via an explicit exclusion entry that carries a required `reason`. Excluded units are never reported as uncovered, but remain visible in coverage reports as a distinct count from `covered`. |
| **uncovered** | The unit matches neither a declared layer nor an explicit exclusion. This is the blind-spot case coverage exists to surface. |
| **unknown** | The unit could not be classified because required input (e.g. a project/assembly discovery result, per #56) was unavailable or ambiguous — distinct from `uncovered` because it reflects a tooling/input gap, not a policy gap. |
| **stale** | A coverage (or any) contract's declared pattern matches zero current first-party units — the inverse of `uncovered`: a rule that no longer has anything to govern. Tracked separately from `empty-input`. |
| **empty-input** | The set of units a coverage contract was asked to classify was empty before classification ran (e.g. zero discovered dependency edges of a given kind) — a configuration/discovery signal, not a finding about code structure. |

**Representative evidence**: every `uncovered` or `stale` finding must carry at least one concrete example — a namespace string, project path, assembly identity, or dependency-edge pair — so diagnostics are actionable rather than aggregate counts. This mirrors `ArchitectureLayerContract`'s existing "unmapped sibling namespace" violations, which already carry a concrete child namespace.

## Decisions

1. **Coverage is a new, separate contract family — not an extension of Dependency/Layer/Independence/Protected.** A coverage contract's job ("is this matched by anything?") is structurally different from every existing contract's job ("is this match allowed?"). Reusing `ArchitectureLayerContract` would conflate "no layer matches" with "a layer matches and the dependency is forbidden," which the #57 acceptance criteria explicitly require staying distinguishable ("The model distinguishes unmapped architecture areas from forbidden dependency violations"). New family: `ArchitectureCoverageContract` (shape only, no implementation — see Non-Goals), added to `ArchitectureContractGroups` as `StrictCoverage`/`AuditCoverage` lists alongside the existing 11 strict/audit pairs.

2. **One contract shape, a `scope` discriminant — not five separate contract types.** Namespace, project, assembly, dependency-edge, and rule-input coverage share every other field (`name`, `id`, `reason`, exclusions, severity wiring). Modeling them as five contract types would multiply the YAML surface for no behavioral gain. Instead, one `ArchitectureCoverageContract` shape carries a `scope: namespace | project | assembly | dependency_edge | rule_input` discriminant field, with scope-specific sub-fields populated only for the relevant scope (see YAML Shape below). This mirrors how `ArchitectureLayerTemplateContract` already varies its applicability via `Containers` rather than via a new contract type per container shape.

3. **Coverage scope reuses layer pattern matching; it does not introduce a new matcher.** A namespace-scope coverage contract's `roots` (the namespaces to classify) and the existing `layers` map use the same `namespace`/`namespace_suffix` glob syntax already validated by `ArchitectureLayerResolver.MatchNamespace`. This satisfies the explicit non-goal "Unrestricted regex-based coverage matching" and the issue's instruction to prefer constrained matchers over unrestricted regex. A unit is `covered` if it matches any declared layer's pattern (including layer-template-expanded containers) or any of the coverage contract's own `exclude` entries; otherwise `uncovered`.

4. **Coverage interacts with layer templates by scope, not by template identity.** A layer template's `exhaustive: true` already classifies direct children of one `ContainerNamespace` as covered/unmapped. Namespace-scope coverage generalizes this: when a coverage contract's `roots` includes a namespace also governed by an exhaustive layer template, the template's expanded layers count as coverage for that subtree, and the coverage contract does not duplicate the template's own exhaustiveness diagnostic — it only reports what the template does not already classify (e.g. namespaces outside any template container, or non-exhaustive templates). This keeps `exhaustive` and coverage complementary instead of producing two diagnostics for the same gap.

5. **Project/assembly coverage scope is declared, not auto-discovered, in this design — but is shaped to consume #56's discovery output.** `ArchitectureAnalysisConfiguration` already has `Solution`/`Projects`/`ProjectInclude`/`ProjectExclude` (#56, `ArchitectureContractModels.cs`). A project/assembly-scope coverage contract's `roots` field accepts the same include/exclude glob shape so #99's eventual implementation can resolve roots from discovered projects without a different config surface. Until #99 implements that resolution, a project/assembly coverage contract with no resolvable roots classifies as `unknown` (not `uncovered`) — this is exactly what the `unknown` vocabulary term exists for (Decision matches Goals: distinguishing policy gaps from tooling gaps).

6. **Dependency-edge and rule-input coverage are scope variants of the same contract, deferred to #100/#101's resolution logic.** This design fixes their YAML shape (`scope: dependency_edge` with `between: [layer, layer]` pairs to classify; `scope: rule_input` with a `contract_ids` list whose live-match status across all other strict/audit contracts is checked for `stale`/`empty-input`) but does not implement the edge-observation or rule-input-tracking logic itself (#100, #101 own that).

7. **Exclusions require `reason`; coverage severity defaults to `off`.** Every `exclude` entry under a coverage contract has a mandatory `reason: string`, identical in spirit to the `reason` already required on dependency/layer/independence/protected contracts. Unlike `policy_consistency` (defaults `error`, because contradictions are never intentional), `analysis.coverage` defaults to `off`. Rationale: an existing policy with zero coverage contracts must behave unchanged (#57 acceptance criteria), and turning coverage on by default the moment any future implementation lands would silently fail policies authored before coverage existed. Once an author adds a `strict_coverage` contract, that contract's own findings are subject to `analysis.coverage: error|warn|off` exactly like `policy_consistency`'s severity wiring — `off` only matters as the *no coverage contracts declared* default, not as a way to silence a contract an author explicitly wrote.

8. **Diagnostic identity: one new kind, one new record, mirroring `PolicyConsistencyDiagnostic`.** Add `ArchitectureDiagnosticKind.Coverage` and a `CoverageDiagnostic` record (shape only, not implemented by this change) carrying: `ContractName`, `ContractId?` (inherited from `ArchitectureDiagnostic`), `Scope` (the discriminant), `Status` (`uncovered | stale | empty-input | unknown`, the subset of the vocabulary that produces diagnostics — `covered`/`excluded` are report-only counts, never diagnostics), `RepresentativeUnit` (string — namespace, project path, assembly identity, or edge description), and `Reason?` (set when `Status == "excluded"` is being reported in audit/report mode rather than suppressed). This follows the precedent decision in `policy-consistency-checks` design.md ("one new kind, one new record... a `CheckKind`-style discriminant... rather than a new subtype per check") — here `Scope` plus `Status` plays that discriminant role.

9. **Backward compatibility is structural, not just default-driven.** A policy with no `strict_coverage`/`audit_coverage` entries has an empty `ArchitectureContractGroups.StrictCoverage`/`AuditCoverage` list; no coverage contract exists to classify anything, so zero coverage diagnostics can ever be produced regardless of `analysis.coverage`. This is the same backward-compatibility shape `policy_consistency` and `unmatched_ignored_violations` already rely on (a check with nothing to check produces nothing), so no policy migration step is required for this change to land.

## YAML Shape

```yaml
analysis:
  coverage: error   # error | warn | off — default: off (Decision 7)

contracts:
  strict_coverage:
    - name: domain-namespace-coverage
      id: cov-domain-ns           # optional, same explicit-ID convention as other contracts
      scope: namespace            # namespace | project | assembly | dependency_edge | rule_input
      roots:
        - namespace: MyApp.Domain
      exclude:
        - namespace_suffix: .Generated
          reason: Source-generated code is not hand-authored and is exempt from coverage.
      reason: All MyApp.Domain namespaces must map to a declared layer.

    - name: project-coverage
      scope: project
      roots:
        - include: ["src/**/*.csproj"]
          exclude: ["src/**/*.Generated.csproj"]
      exclude:
        - project: samples/LegacySample/LegacySample.csproj
          reason: Legacy sample retained for reference; not governed.
      reason: Every discovered project must be represented by the policy.

    - name: edge-coverage
      scope: dependency_edge
      between: [[domain, application], [application, infrastructure]]
      reason: Edges between core layers must be explicitly governed, not silently allowed.

    - name: rule-input-coverage
      scope: rule_input
      contract_ids: [cli-must-not-depend-on-testing]
      reason: Flag if this contract's matchers stop matching any code (stale rule).

  audit_coverage: []   # same shape, non-blocking per analysis.coverage severity
```

This shape is additive to `schema/dependencies.arch.schema.json`'s existing `$defs/contracts` (`strict_coverage`/`audit_coverage`, sibling to `strict_layer_templates`/`audit_layer_templates`) and `$defs/analysis` (`coverage` field, sibling to `policy_consistency`). No existing field changes shape or meaning.

## Risks / Trade-offs

- **[Risk] A single contract shape with a `scope` discriminant could grow unwieldy as #99–#101 add scope-specific fields.** Mitigation: each scope's fields are already namespaced under that scope's own sub-shape (`roots` for namespace/project/assembly, `between` for dependency_edge, `contract_ids` for rule_input) so scope-specific growth doesn't pollute other scopes' fields; if a scope's needs diverge enough during #99–#101 implementation, splitting it into its own contract type at that point is a backward-compatible additive change, not a breaking one.
- **[Risk] `unknown` vs `uncovered` is a meaningful distinction but easy to get wrong in #99's eventual implementation** (e.g. silently treating unresolved project roots as `uncovered`, which would flood reports with non-actionable findings before #56-based resolution matures). Mitigation: this design states the rule explicitly in Decision 5 so #99 has a documented contract to implement against and to test for.
- **[Risk] Defaulting `analysis.coverage` to `off` could mean authors silently get no signal even after adding coverage contracts, if they forget to set it.** Mitigation: explicit per #57/#96 acceptance criteria that existing policies must be unaffected; the cost of a wrong choice here (silently-off) is judged smaller than the cost of breaking every prior policy the moment the first coverage contract family ships, and audit-mode coverage contracts remain visible in reports regardless of severity.
- **[Risk] Schema-only changes in this design (no engine) mean the reviewed shape could still need revision once #97's inventory engine surfaces practical resolution gaps.** Mitigation: explicitly scoped as a non-goal that #97–#103 own engine-driven shape refinement; this design is the reviewed starting point, not a frozen contract — the schema change is additive so future revision is also additive.

## Open Questions

- Should `rule_input` coverage (`contract_ids`) be limited to dependency/layer/independence/protected contract IDs, or also include coverage contracts' own IDs (self-referential staleness)? Deferred to #101, which owns stale-rule detection logic.
- Should project/assembly coverage's `unknown` status escalate to `uncovered` once #56's discovery is mature enough that "unresolved" becomes rare? Deferred to #99/#103 — this is a rollout-maturity decision, not a shape decision.

## Implementation Order

This design does not implement anything; it fixes the shape that the following tasks build against, in the order #57 already recommends:

1. **#97** — build the architecture coverage inventory engine (resolves namespace/project/assembly/edge units from #56 discovery + existing layer resolution; classifies into the vocabulary above).
2. **#98** — add namespace architecture coverage contracts (`scope: namespace`, the first checker against this design).
3. **#101** — add empty-input and stale-rule coverage checks (`scope: rule_input`, plus `stale`/`empty-input` classification for all scopes).
4. **#102** — add architecture coverage reporting and CLI output (renders `CoverageDiagnostic` per Decision 8).
5. **#103** — add architecture coverage baseline and CI gate support (revisits the `analysis.coverage` default-`off` rollout question from Decision 7/Risk 3 with real adoption data).
6. **#99** — add project and assembly architecture coverage contracts (`scope: project`/`assembly`, strongest once #56/#51/#58 discovery matures further, per #57's stated dependency).
7. **#100** — add dependency-edge architecture coverage analysis (`scope: dependency_edge`, valuable once assembly/project/package/external allow-only families mature, per #57's stated dependency).
