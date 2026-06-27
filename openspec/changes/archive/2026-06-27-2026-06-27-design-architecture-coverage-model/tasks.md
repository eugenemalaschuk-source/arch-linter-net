## 1. Vocabulary and model design

- [x] 1.1 Define the coverage vocabulary (`covered`, `excluded`, `uncovered`, `unknown`, `stale`, `empty-input`) with non-overlapping meanings.
- [x] 1.2 Define representative-evidence requirements for `uncovered`/`stale` findings.
- [x] 1.3 Decide coverage is a new contract family (`StrictCoverage`/`AuditCoverage`), not an extension of Dependency/Layer/Independence/Protected, and document why.

## 2. YAML shape design

- [x] 2.1 Design the single coverage contract shape with a `scope` discriminant (`namespace | project | assembly | dependency_edge | rule_input`).
- [x] 2.2 Design scope-specific sub-fields (`roots`, `between`, `contract_ids`) and confirm they reuse existing layer glob syntax rather than a new matcher.
- [x] 2.3 Design the exclusion shape with mandatory `reason`.
- [x] 2.4 Write the full worked YAML example covering all five scopes.

## 3. Interaction with existing model

- [x] 3.1 Define how namespace coverage interacts with declared layers, glob layers, and layer templates (including `exhaustive`) without duplicate diagnostics.
- [x] 3.2 Define how project/assembly coverage scope is shaped to consume `analysis.solution`/`analysis.projects`/`analysis.project_include`/`analysis.project_exclude` (#56) once #99 implements resolution.
- [x] 3.3 Define `unknown` vs `uncovered` for project/assembly scope when discovery input is unavailable or ambiguous.
- [x] 3.4 Define the dependency-edge (`between`) and rule-input (`contract_ids`) scope shapes, deferring their resolution logic to #100/#101.

## 4. Diagnostic identity and severity

- [x] 4.1 Define the `Coverage` diagnostic kind and diagnostic field shape (`Scope`, `Status`, `RepresentativeUnit`, `Reason?`), following the `PolicyConsistencyDiagnostic` one-kind/one-record precedent.
- [x] 4.2 Define `analysis.coverage: error|warn|off`, decide and justify the default (`off`), and document backward-compatibility guarantees for policies with no coverage contracts.

## 5. Schema update

- [x] 5.1 Add `strict_coverage`/`audit_coverage` `$defs` to `schema/dependencies.arch.schema.json`, additive and sibling to `strict_layer_templates`/`audit_layer_templates`.
- [x] 5.2 Add `analysis.coverage` to the `$defs/analysis` schema, sibling to `policy_consistency`.
- [x] 5.3 Confirm the schema change is additive only and does not alter validation of any existing field.

## 6. Spec and design sync

- [x] 6.1 Write `design.md` covering context, goals/non-goals, decisions, YAML shape, risks, open questions, and implementation order for #97–#103.
- [x] 6.2 Write the `architecture-coverage-model` spec capturing the reviewed shape as ADDED requirements.
- [x] 6.3 State explicit non-goals: no engine, no checker, no diagnostic implementation, no runtime behavior change (owned by #97–#103).
- [x] 6.4 Run `openspec validate --all` and confirm the change validates cleanly before archiving.
