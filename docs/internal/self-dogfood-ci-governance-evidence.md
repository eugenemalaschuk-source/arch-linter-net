# Self-dogfood CI governance evidence

This record is the canonical post-fix acceptance evidence for issue #992 and PR #997. The three
comparable samples below are all from the final build-once producer implementation and the same
candidate identity.

- Current implementation candidate: `f17e8ff1514549b6adfd5eb4c2fa7c303204499e`
- Validation workflow: [35706015057](https://github.com/eugenemalaschuk-source/arch-linter-net/actions/runs/35706015057), attempts 1, 2, and 3
- Source SHA: `f17e8ff1514549b6adfd5eb4c2fa7c303204499e`
- Tree SHA: `cea37b59f9d1ed11320b0bdbe11c0998f6ff1d19`
- Policy: `architecture/dependencies.arch.yml`, SHA-256 `3b2b2c6664eb9e598c0bcdbd29af50d63ec89db1f263dc6f910b92843dc3a7ae`
- CLI assembly SHA-256: `0902b9e134aa250bdf39764698fb6d074156c8edf36da2cc2eeb90459866e0b5`
- Testing assembly SHA-256: `e5fc02758cfea6abd10201a5353ba63670cc3e0e855c6b5f65f2061f11445eb9`
- Tool identity: `ArchLinterNet.Cli/ArchLinterNet.Testing`

## Canonical performance comparison

The canonical post-fix acceptance set is the three same-candidate `ubuntu24` samples from workflow
35706015057 attempts 1–3. All three use the same preparation-inclusive boundary as the pinned 204 s
baseline. Earlier timing rows and the earlier median are superseded and are not part of this
acceptance comparison.

| Comparable post-fix sample | Workflow attempt | Governance span | Result |
| --- | ---: | ---: | --- |
| Sample 1 | 1 | 144.831 s | GAP |
| Sample 2 | 2 | 90.085 s | GAP |
| Sample 3 | 3 | 158.152 s | GAP |

The governance-span median is **144.831 s**, with a range of **90.085–158.152 s**. Against the
204 s baseline and the `<=60 s` target, the measured result is a **GAP**: the normalized fan-out
reduces the comparable wall-clock governance time by **59.169 s (29.004%)**, a **1.409x** speedup,
but does not yet reach the target. The headline span starts at the earliest candidate/base
preparation boundary and includes candidate solution build/receipt publication and base
preparation.

## Process accounting and attribution

The corrected producer path performs exactly one restore process and one authoritative solution
build process, including the CLI launcher. That build emits nonce-bound per-project completion
markers; the already-built CLI then verifies those markers and publishes receipts without entering
Core's build-capable `EnsureBuilt` path. A missing, stale, or mismatched marker fails closed, so an
existing or fake artifact cannot be promoted by the verification-only hand-off. Base preparation
performed one restore and one authoritative graph-build/receipt path, reusing the already-built
candidate CLI host. The independent projections then used these process counts:

| Projection | Projection processes | CLI processes |
| --- | ---: | ---: |
| Strict policy | 1 | 1 |
| Reviewed public API | 1 | 3 |
| Architecture coverage | 1 | 7 |
| Health/current/change/report inputs | 1 | 5 |

The report-input projection is the dominant wall-clock projection at 41.967–74.374 s; the strict, public-API, and coverage projections complete in parallel. Rendering and manifest steps consume the projection outputs and do not perform additional analysis.

This evidence authorizes no ArchLinterNet Core optimization. The next step belongs to the linked performance/adoption authority issues (#991 and #19), where this GAP and the bounded attribution should remain visible before any Core algorithm change is proposed.
