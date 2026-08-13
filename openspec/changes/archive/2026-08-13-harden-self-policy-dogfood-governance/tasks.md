# Tasks

## 1. Capability matrix (P0)

- [x] 1.1 Enumerate the contract groups the current schema supports and classify each against the real repository shape.
- [x] 1.2 Record adopt / already-covered / N-A / defer with rationale in `docs/internal/self-policy-capability-matrix.md`.
- [x] 1.3 Record engine limitations found during authoring (layer-glob grammar, rule-input-coverage family restriction, implicit `Microsoft.NETCore.App`, direct-only assembly depth, preflight under solution discovery).

## 2. Project discovery and coverage (P0)

- [x] 2.1 Declare `analysis.solution: ArchLinterNet.slnx` with `project_exclude` for `tests/**` and `benchmarks/**`.
- [x] 2.2 Add the `scope: project` coverage contract.
- [x] 2.3 Confirm assembly and namespace coverage are unchanged and project coverage complements them.

## 3. Direct assembly graph (P0)

- [x] 3.1 Add `strict_assembly_dependency` for CEL.
- [x] 3.2 Add `strict_assembly_allow_only` for Core and, via the `adapter_assemblies` source set, for Cli/Testing.
- [x] 3.3 Add `strict_assembly_independence` for Cli ⟂ Testing.

## 4. Reviewed public API surfaces (P0)

- [x] 4.1 Decide per assembly: adopt for Core/Testing/CEL, N-A for Cli, `surface_selector` evaluated and not adopted.
- [x] 4.2 Capture snapshots through `public-api capture --ensure-built` into `architecture/api/`.
- [x] 4.3 Declare the three contracts with `api_comparison: exact`.

## 5. Project metadata and friend assemblies (P0)

- [x] 5.1 Reviewed `allowed_friend_assemblies` per shipped project, including the empty set for Testing.
- [x] 5.2 Forbid shipped→test/benchmark project references through the `production_projects` project set.
- [x] 5.3 Forbid `IsTestProject` on shipped projects; freeze nothing else.

## 6. Package and FrameworkReference boundaries (P0)

- [x] 6.1 Declare `packages` and `framework_references` groups.
- [x] 6.2 Add package dependency/allow-only and framework allow-only contracts.
- [x] 6.3 Confirm the base runtime framework baseline rather than an empty allow-list.

## 7. Source-set cleanup and rule-input coverage (P0)

- [x] 7.1 Declare `all_declared_layers`, `shipped_assemblies`, `adapter_assemblies`, `production_projects`.
- [x] 7.2 Collapse 26 external rules into 2 authored rules with `exclude_sources`.
- [x] 7.3 Give the authored rules stable IDs and add them to `scope: rule_input` coverage.
- [x] 7.4 Record why the other new families cannot participate in rule-input coverage.

## 8. Post-refactor structural seams (P1)

- [x] 8.1 `strict_type_placement` for family checkers and diagnostics.
- [x] 8.2 `strict_interface_implementation` for diagnostic payloads and both policy-validator seams.
- [x] 8.3 Exclude the `ArchitectureContractChecker` delegate from the checker rule.

## 9. Developer workflow (P2)

- [x] 9.1 Make `lint-architecture` the canonical read-only CLI gate.
- [x] 9.2 Add `policy-check`, `public-api-check`, `public-api-update-preview`, `public-api-update`, `explain-architecture`.
- [x] 9.3 Add `--ensure-built` to the CI strict/audit JSON targets.
- [x] 9.4 Update `Makefile` help and `AGENTS.md`; preserve acceptance ordering.

## 10. Negative regression evidence

- [x] 10.1 Add `SelfPolicyRepository` and `SelfPolicyNegativeRegressionTests`.
- [x] 10.2 Cover assembly allow-only, project coverage, friend assemblies, forbidden project references, package allow-only, framework allow-only, both structural seams, public API addition and removal, stale source-set input, unresolved rule-input id, and a malformed policy.
- [x] 10.3 Assert a read-only run leaves snapshots byte-identical.
- [x] 10.4 Move both self-policy fixtures into the E2E bucket and gitignore the mutation artifacts.

## 10b. Production defect surfaced by the new self-policy

- [x] 10b.1 Switch `CheckAssemblyDependencyContract`/`CheckAssemblyAllowOnlyContract` to the expansion-aware `IsContractSelected` overload.
- [x] 10b.2 Add a pipeline regression asserting that selecting the authored id runs the expanded assembly allow-only instances and reports their violations.

## 11. Validation

- [x] 11.1 `make lint-architecture` passes.
- [x] 11.2 `make policy-check` passes.
- [x] 11.3 `make public-api-check` passes.
- [x] 11.4 Self-policy fixtures pass.
- [x] 11.5 `make fmt` and `openspec validate --all`.
