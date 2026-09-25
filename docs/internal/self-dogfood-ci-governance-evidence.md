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

## Issue #998 decision — same-process Public API projection (2026-09-25)

**Outcome C — not material; no product implementation.** The normalized Server lane remains above
the `<=60 s` target, but eliminating the independent Public API command cannot materially close its
remaining gap. This is a measurement-only disposition: no Core, CLI, Testing, policy, or public API
behavior changed.

### Normalized Server evidence

The samples are the three exact-base verified runs accepted by firstice-server #525. Each used
ArchLinterNet `0.8.0-main.157`, base `4b061613127d963301ca98ee0f7e0089bb3dad5a`, and the same
workflow topology. The canonical governance span remained `GAP` against 60 seconds.

| Run | Governance span | Health command | Separate reviewed API command |
| --- | ---: | ---: | ---: |
| [35701418653](https://github.com/firstice-game/firstice-server/actions/runs/35701418653) | 589.875 s | 110.897 s | 16.849 s |
| [35702624555](https://github.com/firstice-game/firstice-server/actions/runs/35702624555) | 495.216 s | 92.335 s | 13.971 s |
| [35703969681](https://github.com/firstice-game/firstice-server/actions/runs/35703969681) | 593.964 s | 111.688 s | 17.216 s |
| **Median** | **589.875 s** | **110.897 s** | **16.849 s** |

The Public API process is a blocking authority on the serialized critical path. Its command is
`dotnet arch-linter-net --policy architecture/dependencies.arch.yml --mode strict --ensure-built --configuration Debug --framework net10.0`, followed by the selected reviewed/exported API
`--contract` arguments and the canonical JSON report destination. Across all three runs the full
profile recorded 11 ArchLinterNet invocations, two full preparation passes, two `dotnet build`
processes and two `dotnet restore` processes; both Health and the separate Public API process
requested `--ensure-built`. The strict command therefore repeats candidate preparation before its
contract-specific comparison. The API command's phase profile was not enabled in these hosted runs.

### Bounded effect and phase evidence

Even the unrealistically favorable bound that removes the **entire** 16.849-second median Public
API command would:

- reduce the 130.326-second median blocking authority path by at most **12.9%**;
- reduce the 589.875-second normalized governance span by at most **2.9%**;
- reduce the 529.875-second gap above target by at most **3.2%**;
- leave a best-case lane median of about **573.026 seconds**, still **513.026 seconds** above target.

That bound also counts work that must remain: API contract selection, snapshot comparison, result
construction and canonical output. It is therefore a ceiling, not an expected saving. The dominant
outer phase in run 35703969681 was architecture change at 330.143 seconds, which is outside the
Public API projection.

The hosted artifacts provide deterministic work attribution but not `analysis-profile/v1` phase
timings for the strict API command (`profileRequested=false`). A diagnostic replay used the current
ArchLinterNet source and the exact Server checkout/policy/contract selection on Windows because
restoring the pinned hosted package returned HTTP 403. That replay is not comparable to the hosted
timings. It showed the same candidate shape twice: 37 projects/assemblies and 423 source files,
with one snapshot, one fact index and one source scan in each process. In the Public API replay,
build-state preflight took 23.130 seconds of 31.339 seconds; Public API surface materialization took
0.207 seconds and aggregate contract checks 1.805 seconds. A Health replay reported the same
candidate counters, but its profile had no phase timings or memory measurements.

The Public API replay reported 583,437,488 allocated bytes and a 238,510,080-byte peak working
set. A comparable Health allocation/working-set value was unavailable, so no memory delta can be
claimed. There is no post-change measurement because outcome C makes no product change.

The lane-level kill criterion in #998 is met: the full command's measured duration is only a small
fraction of the normalized lane and target gap, and its whole duration is only an upper bound on
removable work. Reconsider this seam only if a future normalized canonical lane changes the measured
effect enough to materially improve its end-to-end result. The evidence and outcome are linked from
#19, #991 and firstice-server #525.
