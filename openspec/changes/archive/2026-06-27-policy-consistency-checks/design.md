## Context

ArchLinterNet validates a YAML architecture policy against code through `ArchitectureValidationService.Validate`: `load_and_setup -> configuration_check -> contract_checks -> post_processing`. `configuration_check` (`ArchitectureContractRunner.CheckConfiguration`) already catches structural problems (missing assemblies, empty layers, unknown/invalid external groups) but never inspects whether the policy's own contracts agree with each other. Contracts are grouped by family (`ArchitectureContractGroups`: Dependency, Layer, AllowOnly, Cycle, MethodBody, Asmdef, Independence, Protected, External, LayerTemplate, AcyclicSibling) and split strict/audit; layer templates expand into concrete `ArchitectureLayerContract` instances with computed IDs before contract execution. Diagnostics follow a typed-envelope model (`ArchitectureDiagnostic` + `ArchitectureDiagnosticKind`), and severity-configurable behavior already has one precedent: `analysis.unmatched_ignored_violations: error|warn|off`, validated by `ArchitectureValidationService` when `EnforceUnmatchedIgnoredViolationsPolicy` is set.

## Goals / Non-Goals

**Goals:**
- Detect contradictions that exist purely within the policy document, independent of any code being scanned.
- Reuse the existing expansion (layer templates), matching (`ArchitectureLayerResolver.MatchNamespace`), and diagnostic/severity patterns rather than introducing parallel mechanisms.
- Run as an isolated, deterministic pass that produces stable diagnostics (no diagnostics whose presence/ordering depends on scan order or non-deterministic enumeration).
- Make severity configurable via `analysis.policy_consistency: error|warn|off`, mirroring `analysis.unmatched_ignored_violations`.

**Non-Goals:**
- Proving complete policy satisfiability for arbitrary future codebases (no SAT-style solver; checks are pairwise/structural).
- Replacing or modifying ordinary dependency-vs-code validation (`ArchitectureContractExecutor`/`ArchitectureContractRunner.CheckConfiguration` are unchanged in their existing checks).
- Semantic data-flow or DI-container validation.
- A pluggable/custom-rule system — checks are a fixed, built-in set matching the issue's acceptance criteria.

## Decisions

1. **New component, not an extension of `CheckConfiguration`.** Add `ArchitectureContractRunner.CheckPolicyConsistency` (same class as `CheckConfiguration`, new method) rather than folding checks into the existing method. Rationale: `CheckConfiguration` checks structural validity against assemblies/types already resolved; policy-consistency checks are pure contract-vs-contract comparisons over the expanded `ArchitectureContractGroups` and don't need a resolved runner for most checks (layer overlap is the exception — it needs resolved types, same as the existing empty-layer check, so it reuses that same resolution step). Keeping it a sibling method (not a new class/service) avoids introducing an unneeded abstraction while staying easy to test in isolation.

2. **Pipeline placement: new step between `configuration_check` and `contract_checks`.** `ArchitectureValidationService.Validate` gains a `policy_consistency_check` step. It runs after configuration_check (so a fundamentally broken policy is reported once, not twice) and before `contract_checks` (so consistency diagnostics are available without paying for a full contract execution against code). Mirrors the existing early-exit style already used for configuration violations.

3. **Operate on expanded contract groups.** Duplicate-ID and layer-template checks run after `LayerTemplateExpander.Expand()`, on the same `ArchitectureContractGroups` instance contract execution will use — so what's checked for consistency is exactly what will execute, including generated template IDs.

4. **Diagnostic shape: one new kind, one new record.** Add `ArchitectureDiagnosticKind.PolicyConsistency` and a `PolicyConsistencyDiagnostic` record (sibling to `ConfigurationDiagnostic`) carrying: `Reason` (free text), `ConflictingContractIds` (string[]), `ConflictingContractNames` (string[]), `Layers` (string[], the layer names involved), and an optional `RepresentativeType` (string) for layer-overlap findings. A single record (rather than one per check kind) keeps the model small; the `Reason` text plus a `CheckKind`-style discriminant (modeled as a string code, e.g. `"duplicate-id"`, `"allow-forbid-conflict"`, `"independence-conflict"`, `"protected-importer-conflict"`, `"layer-overlap"`, `"unreachable-contract"`) on the diagnostic itself makes findings machine-distinguishable without a new subtype per check.

5. **Severity wiring follows the `unmatched_ignored_violations` precedent exactly.** `analysis.policy_consistency: error|warn|off` (default `error` when absent, since acceptance criteria requires contradictory strict rules to fail by default). `ValidationRequest` does not need a new opt-in flag like `EnforceUnmatchedIgnoredViolationsPolicy` — policy-consistency findings are structural problems in the policy itself (not a deferred reporting toggle), so the check always runs; only whether it fails (`error`) vs. warns (`warn`) vs. is silent (`off`) is configurable. CLI `validate`, public `ArchitectureValidator`, and the Testing adapter all pick this up automatically since they share `ArchitectureValidationService.Validate`.

6. **Layer-overlap detection scope.** Reuses the same type/namespace enumeration and `ArchitectureLayerResolver.MatchNamespace` glob matching already used by the empty-layer check. For each discovered concrete type, collect all layers whose pattern matches it; if more than one layer matches and there is no explicit `external: true`/documented-allowance marker reconciling them (e.g. an external layer overlapping an internal one is expected, not a conflict — only internal-vs-internal overlap without an explicit allowance is flagged), emit one diagnostic with the matched layer names and that type as `RepresentativeType` (first match wins, deterministic by sort order, to avoid one diagnostic per overlapping type flooding output).

7. **Unreachable-contract detection scope.** A dependency/layer/independence/protected contract is flagged unreachable when its declared source or target layer's pattern set is structurally empty or mutually exclusive with itself (e.g. a layer template parameter producing an empty namespace, or a layer whose include/exclude patterns cancel out) — i.e. the check is static (over layer definitions), not a live "zero types currently match" check (that's already the existing empty-layer configuration check and is intentionally different: empty *today* vs. *structurally impossible*).

## Risks / Trade-offs

- **[Risk] Allow/forbid and independence-conflict checks could false-positive on intentionally narrow exceptions** (e.g. a forbidden rule with a documented exception elsewhere) → Mitigation: checks compare same-source/same-target pairs at the layer-pair granularity already used by existing contracts; anything narrower (per-type allow lists) is out of scope for v1 and explicitly callable out in test fixtures as a known boundary.
- **[Risk] Layer-overlap detection performance on large codebases** (re-running matcher per type per layer) → Mitigation: reuses the same matching path the empty-layer check already pays for; no new O(types × layers) cost class introduced.
- **[Risk] Default `error` severity breaks previously-"passing" policies that happened to contain contradictions** → Mitigation: explicitly called out in the proposal/acceptance criteria as intended (contradictory strict rules should fail by default); `warn`/`off` are available as an escape hatch, and existing valid (non-contradictory) policies are unaffected by construction.

## Open Questions

None blocking — defaults above resolve every issue acceptance-criterion choice point (severity default, diagnostic shape, pipeline placement).
