## 1. Evaluated manifest model and collection

- [x] 1.1 Add versioned manifest, artifact, context, eligibility, and invalidation-reason models in `Core.BuildState`.
- [x] 1.2 Implement bounded, portable static collection of project/import/compile/additional/config/reference/analyzer inputs with containment and unsupported-input refusal.
- [x] 1.3 Canonicalize manifest identity with deterministic ordinal ordering, SHA-256 content digests, and distinct configuration/TFM/platform/RID contexts.

## 2. Receipt and preflight integration

- [x] 2.1 Extend build receipt creation to record evaluated manifest identity and eligibility without weakening legacy ordinary-preflight compatibility.
- [x] 2.2 Recollect and verify manifests during preflight; expose cache eligibility and invalidation reasons without changing primary preflight categories.
- [x] 2.3 Guard receipt publication against input/output TOCTOU changes and preserve the existing build-versus-policy identity boundary.

## 3. Public projections and diagnostics

- [x] 3.1 Expose the same per-project eligibility result through Core, CLI, and Testing API projections.
- [x] 3.2 Add machine-readable diagnostic eligibility and invalidation reasons; `analysis-profile/v1` retains its reserved cache counters until #365 owns cache observations.

## 4. Tests and synchronization

- [x] 4.1 Add NUnit coverage for linked sources, custom imports, context/reference identity changes, unsupported inputs, portable paths, and receipt publication checks.
- [x] 4.2 Run focused tests, format, full acceptance, and OpenSpec validation; fix issue-related failures.
- [x] 4.3 Synchronize the final delta specs with implemented behavior and archive the change.
