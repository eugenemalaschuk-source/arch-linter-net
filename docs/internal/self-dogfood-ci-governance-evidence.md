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

## Issue #998 decision — same-process Public API projection (refreshed 2026-09-25)

**Outcome C — not material; no product implementation.** This decision uses the Server
`0.9.0-preview.1` consumer and three comparable verified samples. The separate Public API command is
on the blocking path, but its entire measured duration is an upper bound on savings: eliminating it
would still leave the normalized lane more than 177 seconds above the `<=60 s` target. No Core,
CLI, Testing, policy, or public API behavior changed.

### Current normalized Server evidence

The samples use the current Server candidate from PR #549. The analyzed candidate SHA and workflow
topology are identical across attempts 1–3; the candidate tree matches the merged PR #549 tree.
Changes on current Server `main` after that merge are documentation-only. All three attempts used
ArchLinterNet `0.9.0-preview.1`, exact base
`55d1b543931a545113f6b4e79a6251c8358c2f57`, `Debug`, `net10.0`, and
`BASE_ARCHITECTURE_EVIDENCE_STATE=verified`.

| Sample | Run attempt / architecture job | Governance span | Blocking path | Health | Separate reviewed API |
| --- | --- | ---: | ---: | ---: | ---: |
| 1 | [36036073386 / 1 / 107756472302](https://github.com/firstice-game/firstice-server/actions/runs/36036073386/attempts/1) | 200.012 s | 100.374 s | 85.592 s | 12.602 s |
| 2 | [36036073386 / 2 / 108148853906](https://github.com/firstice-game/firstice-server/actions/runs/36036073386/attempts/2) | 254.843 s | 135.261 s | 115.129 s | 17.381 s |
| 3 | [36036073386 / 3 / 108150958321](https://github.com/firstice-game/firstice-server/actions/runs/36036073386/attempts/3) | 259.292 s | 140.269 s | 119.138 s | 18.377 s |
| **Median** | | **254.843 s** | **135.261 s** | **115.129 s** | **17.381 s** |

The governance span range is **200.012–259.292 s**; the Public API command range is
**12.602–18.377 s**. Immutable `lint-architecture` artifact IDs by attempt are
`10825376767`, `10874092968`, and `10874773431`. The analyzed candidate SHA in the artifacts is
`aee99dfb002bf7659ecdde6fa0f0066955f5461a`.

The separate command is `strict` with the same selected reviewed/exported API `--contract` set and
canonical JSON report across attempts. It requests `--ensure-built` on the same candidate as Health.
Each artifact records 11 ArchLinterNet invocations, two full preparation passes, one `dotnet build`
process and one `dotnet restore` process. Thus this version still repeats candidate preparation,
while the earlier `0.8.0-main.157` samples' two build/restore processes do not describe the current
lane. The API and Health invocations both have `profileRequested=false` in the hosted artifacts, so
there are no current hosted `analysis-profile/v1` phase timings to split from wall time.

The previously cited #525 samples (runs 35701418653, 35702624555 and 35703969681) remain historical
evidence for `0.8.0-main.157` and are excluded from this decision. The newer single runs for PRs
#548, #550 and #549 also do not form a median because their exact base SHAs differ; the three #549
attempts above hold the base and candidate fixed.

### Bounded effect and phase evidence

Even the upper bound that removes the **entire** 17.381-second median Public API process would:

- reduce the 135.261-second median blocking path by at most **12.85%**;
- reduce the 254.843-second normalized governance span by at most **6.82%**;
- reduce the 194.843-second gap above target by at most **8.92%**;
- leave a best-case lane median of **237.462 s**, still **177.462 s** above target.

This is a ceiling, not an expected saving: contract selection, snapshot comparison, result
construction and canonical API output remain per projection. The samples also show runner variance
within one base and candidate, so the three-sample median and full range are retained. Outcome C
therefore follows the lane-level kill criterion in #998 even though the API process is a visible
share of the blocking path.

The earlier Windows current-source replay remains diagnostic only because it did not use the pinned
consumer package or a comparable hosted Health profile. It measured 583,437,488 allocated bytes
and a 238,510,080-byte peak working set for that Public API process, but no comparable Health
allocation/working-set value exists. The current hosted samples provide no memory delta and there
is no post-change measurement because outcome C makes no product change.
