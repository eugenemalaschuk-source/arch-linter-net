## Context

The badge command handles read-only preflight together with publish/renew, including a flag that suppresses the publishing exception handler's unavailable snapshot. Runtime integration already has separate checks but remains in the same module. Six release jobs repeat both approved publisher commits before their independent transport verification. The approved pair is already maintained in `release_distribution.py`; workflow and action are separate identities.

## Goals / Non-Goals

**Goals:**

- Isolate read-only operation execution and failure reporting from publication code while preserving all command results and existing evidence/decision semantics.
- Fetch only the two existing reviewed commits through one implementation, at the same point in each release job, before that job's independent frozen-transport verification.

**Non-Goals:**

- Change pins, package or schema identities, permissions, publish/renew protocols, output formats, or release authority.
- Rework provider, temporal receipt, raw CAS, Relay, or the closed bundle allowlist; add a plugin registry; publish or prepare a release.

## Decisions

1. Keep a finite explicit CLI dispatcher. Give preflight and runtime integration operation-owned code outside the publication execution path, supplied only the configuration, read/evidence, decision and output dependencies they need. Preserve common error types and data without importing publishing functionality into read-only handlers. Publishing retains its own failure policy without a read-only suppression flag. This permits operation-specific evolution without involving publication failure logic.
2. Introduce a narrow acquisition command under `tools/release/` which uses the existing reviewed constants, invokes Git in the current repository with `fetch --no-tags --depth=1 origin` followed by the workflow and action commits, and fails the job on error. It accepts no caller-selected repository, refs, or inventory as authority. Each of the six existing job steps calls it and retains its own subsequent transport verification. Repeating literal pins in workflow steps or accepting inventory values would reintroduce the coordination or authority problem.
3. Preserve the composite action's module entry point and existing immutable source and blob validation, including Windows shallow-checkout behavior. Reuse existing suites as the behavior oracle and add direct negative boundary and acquisition/wiring tests.

## Risks / Trade-offs

- [Read-only extraction changes error or output behavior] → Exercise ready, unavailable, missing receipt and exception paths and assert no publisher capability is invoked; run existing publish/renew regressions.
- [Central fetch becomes mistaken for authorization] → Keep the two trusted constants independent and preserve each existing candidate-bound verification step and negative source tests.
- [New module is unavailable at the trusted action root] → Exercise the real `python -m tools.badge_promotion.cli` entry point with that root and preserve the action layout.
- [Workflow refactor misses a platform or job] → Check all six job call sites and their ordering, exercise shallow local Git acquisition, and run workflow lint; full cross-platform evidence remains in CI.

## Migration Plan

Changes take effect with the source commit containing them; revert the commit if necessary. No data migration, pin rotation, or publication is part of this change.

## Open Questions

None.
