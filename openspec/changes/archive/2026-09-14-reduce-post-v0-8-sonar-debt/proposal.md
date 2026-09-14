## Why

`main`'s SonarCloud quality gate is red: `new_reliability_rating` is C and `new_security_rating` is E
against an A threshold. The public project API (captured 2026-09-13 on revision
`103b2680511b702288a95ee4a16189bcec099027`) reports 460 open findings, 2368 minutes of estimated
debt, 0 security hotspots — and 458 of 460 findings inside the New Code period. The accumulated debt
is almost entirely post-v0.8.0 new code, so New-Code and Overall-Code remediation coincide for the
current tree.

Five findings drive the gate: one reliability bug and four security vulnerabilities. Source review
shows four of the five are analyzer false positives or test-only artifacts:

- `pythonsecurity:S2083` (`main_quality_coverage.py:121`) and `pythonsecurity:S8707`
  (`verify_restored_main_packages.py:23`) sit behind the repository's own
  `_release_workspace._safe_path` sanitizer. That sanitizer was added by the archived
  `fix-sonarcloud-new-code-debt` change, whose own design explicitly anticipated that Sonar's Python
  taint analysis "might not recognize `_safe_path` as a sanitizer and re-flag the same lines". The
  risk has materialized: the guarded sinks are reported again.
- `csharpsquid:S2583` (`ArchitectureHealthPublicationEvidenceProjector.cs:87`) flags
  `reasons.Count > 0` as always-false; the list is populated by a local function that mutates a
  captured variable, a pattern the analyzer does not model. The code path is reachable and correct.
- `python:S5443` (`test_create_release_scope_evidence.py:378`) is a negative test whose fixture
  literal is `"/tmp/elsewhere"`; the test asserts the path is *rejected*, and no temporary file is
  created in a public directory.

Only `pythonsecurity:S8707` in `tools/scripts/test_coverage_badge.py:68` is a genuine unguarded taint
sink, in a local developer script.

Separately, #795 requires a reproducible, revision-bound inventory and a triage separating real
findings from reviewed dispositions; none exists in-repository.

## What Changes

- **Reproducible debt inventory.** Add `tools/release/sonar_debt_inventory.py`, which queries the
  project's SonarCloud web API with full pagination and writes a deterministic inventory and
  triage report bound to a specific analysis revision, quality gate and measure set. Commit the
  captured post-v0.8 baseline under `docs/internal/` as the #795 evidence.
- **Resolve the five gate-blocking findings.**
  - Refactor `ArchitectureHealthPublicationEvidenceProjector.Project` so the reasons are collected
    without a closure-mutated local list, removing the `S2583` false positive without changing
    behavior.
  - Make the release-workspace confinement analyzer-recognizable where possible, and otherwise
    record an individually reviewed false-positive disposition for the residual `S2083`/`S8707`
    findings that name the guarding helper. No rule-wide, file-wide or directory-wide suppression.
  - Sanitize the `--reports-glob` input in `tools/scripts/test_coverage_badge.py` (the one real taint
    sink).
  - Restructure the `S5443` negative test so it does not carry a publicly-writable literal that the
    analyzer reads as an actual temporary-file use.
- **Behavior-preserving remediation of non-product debt.** Clean the accumulated findings in
  `tools/release/**`, `tools/badge_promotion/**`, `tools/scripts/**`, the relay TypeScript sources
  (mechanical rules only) and `tests/**`/`benchmarks/**`, with regression coverage.
- **Evidence and closure.** Record comparable before/after inventory and retain every individually
  reviewed false-positive/accepted decision in the report.

## Capabilities

### New Capabilities

- `sonarcloud-debt-inventory`: a reproducible, revision-bound inventory of SonarCloud findings,
  hotspots, measures and quality-gate status, plus a triage report that separates fixed code,
  reviewed dispositions and analyzed exclusions.

### Modified Capabilities

- `release-tooling-workspace-confinement`: confinement now also has to survive SonarCloud's Python
  security analysis — either by being recognizable to it, or by carrying an individually reviewed
  disposition that names the guarding helper, instead of allowing the same guarded sinks to be
  reported forever.

## Impact

- `tools/release/**`, `tools/badge_promotion/**`, `tools/scripts/**`, `relay/src/**` (mechanical
  rules only), `tests/**`, `benchmarks/**`, one Core projector method, and internal documentation.
- No public CLI/Core/Testing API change; no policy/schema, finding-identity, canonical ordering,
  cache/build-state or release-semantics change.
- No CI workflow change and no SonarCloud quality-profile/gate/exclusion weakening. Reviewed
  dispositions are recorded as evidence; they do not alter the gate definition.
- Deferred to focused owners (not this change): C#/CLI cognitive-complexity (`S3776`) and
  parameter-count (`S107`) debt, public-API-risky Roslyn rules (`CA1859`, `S3871`, `S2365`), and
  relay cognitive-complexity refactors, per #783's prohibition on Sonar-driven competing
  architecture decomposition.
