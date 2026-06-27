## Why

A normal ArchLinterNet contract answers "is this dependency allowed?" — but a policy that only declares dependency/layer/independence/protected contracts can be silently blind to entire namespaces, projects, assemblies, or dependency edges that no contract ever looks at. Today there is no vocabulary, YAML shape, or diagnostic identity for "this area of the codebase is not represented by the policy at all," distinct from "this area violates a forbidden-dependency rule." Story #57 needs this model designed and reviewed before any coverage contract family (#97–#103) is implemented, so the schema and diagnostics those tasks build on are stable instead of improvised per-task.

## What Changes

- Define the architecture-coverage vocabulary: `covered`, `excluded`, `uncovered`, `unknown`, `stale`, `empty-input`, and what counts as representative evidence for each.
- Design the strict/audit YAML shape for coverage contract families (`strict_coverage` / `audit_coverage`, scoped sub-shapes per coverage kind), following the existing strict/audit contract-family pattern (`ArchitectureContractGroups`).
- Decide how coverage scope is declared for namespaces, projects, assemblies, dependency edges, and rule inputs (the inputs that a normal dependency/layer/independence/protected contract reads), including how coverage scope interacts with layers, glob layers, layer template containers/`exhaustive`, discovered projects (`analysis.solution`/`analysis.projects`), and explicit exclusions.
- Define exclusion shape: every exclusion requires a `reason` string, mirroring the existing `reason` field already required on dependency/layer/independence/protected contracts.
- Define diagnostic identity for coverage findings (a new `ArchitectureDiagnosticKind.Coverage` and `CoverageDiagnostic` record shape), severity configuration (`analysis.coverage: error|warn|off`, default `error` — see Decisions), and backward-compatibility rules so existing policies without coverage contracts behave unchanged.
- Update `schema/dependencies.arch.schema.json` to accept the reviewed `strict_coverage`/`audit_coverage` shapes (schema acceptance only — no runtime engine, no checker).
- Produce design notes that explicitly state non-goals and the implementation order for #97–#103.

## Capabilities

### New Capabilities
- `architecture-coverage-model`: The coverage vocabulary, YAML contract shape, scope-declaration rules, exclusion rules, and diagnostic-identity/severity design that #97–#103 implement against. This change defines the *shape*, not the *engine* — no coverage contract actually runs or produces findings yet.

## Impact

- `schema/dependencies.arch.schema.json`: new `$defs` for coverage contract shapes and `analysis.coverage` severity setting; additive only.
- `src/ArchLinterNet.Core/Contracts/ArchitectureContractModels.cs`: minimal `ArchitectureCoverageContract` binding (`name`/`id`/`scope`/`reason` only) plus `StrictCoverage`/`AuditCoverage` lists on `ArchitectureContractGroups` and `Coverage` on `ArchitectureAnalysisConfiguration` — bound but never executed by any contract family, so a schema-valid coverage contract is detected rather than silently dropped by the loader's `IgnoreUnmatchedProperties` deserialization.
- `src/ArchLinterNet.Core/Execution/ArchitectureRunnerFactory.cs`: `LoadDocument` throws `InvalidOperationException` when any `strict_coverage`/`audit_coverage` contract is declared, since the coverage engine does not exist yet to enforce it. This is the only runtime behavior change in this PR — it exists to prevent a trust-boundary gap (schema accepts a contract the engine cannot run), not to implement coverage.
- No diagnostic kind, checker, or pipeline step is implemented by this change — those land in #97–#103 against this design.
- `docs/architecture/`: new design reference for the coverage model (vocabulary, YAML shape, scope/exclusion rules).
- No changes to runtime validation behavior for policies that declare no coverage contracts — they are unaffected.
