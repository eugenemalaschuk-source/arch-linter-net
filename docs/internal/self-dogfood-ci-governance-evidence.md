# Self-dogfood CI governance evidence

This record is the canonical post-fix acceptance evidence for issue #992 and PR #997. The current
PR head and latest green producer verification are recorded separately from the fixed
three-sample performance comparison:

- Current PR head: `a5c947e5d67b32e4098aed350711bcb1ae8343f3`
- Latest green producer run: [35670638922](https://github.com/eugenemalaschuk-source/arch-linter-net/actions/runs/35670638922)
- Latest green run status: 44/44 jobs successful

- Source SHA: `a5c947e5d67b32e4098aed350711bcb1ae8343f3`
- Tree SHA: `3c388a562c1846882a1d7a31d8dcd228a7f08e46`
- Policy: `architecture/dependencies.arch.yml`, SHA-256 `3b2b2c6664eb9e598c0bcdbd29af50d63ec89db1f263dc6f910b92843dc3a7ae`
- CLI assembly SHA-256: `9920e68cf79f9fdea1cc2b4bd292fe54d83b1f2df4f158b3edfa508fa5ee3a8d`
- Testing assembly SHA-256: `ce585e50fad7a2406499e0775798abf1501d89492f8a9d7db4801b5d894eecd3`
- Tool identity: `ArchLinterNet.Cli/ArchLinterNet.Testing`

## Canonical performance comparison

The canonical post-fix acceptance set is the three same-candidate `ubuntu24` samples recorded
in the performance authority updates on [#991](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/991)
and [#19](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/19). All three use the
same preparation-inclusive boundary as the pinned 204 s baseline. Earlier timing rows and the
earlier median are superseded and are not part of this acceptance comparison.

| Comparable post-fix sample | Governance span | Result |
| --- | ---: | --- |
| Sample 1 | 167.839 s | GAP |
| Sample 2 | 167.309 s | GAP |
| Sample 3 | 126.761 s | GAP |

The governance-span median is **167.309 s**, with a range of **126.761–167.839 s**. Against the
204 s baseline and the `<=60 s` target, the measured result is a **GAP**: the normalized fan-out
reduces the comparable wall-clock governance time by **36.691 s (17.99%)**, a **1.219x** speedup,
but does not yet reach the target. The headline span starts at the earliest candidate/base
preparation boundary and includes the explicit CLI-host bootstrap, candidate receipt publication,
and base preparation.

## Process accounting and attribution

The candidate preparation performed exactly one restore process and one explicit CLI-host
bootstrap build. The producer then invokes the CLI with `dotnet run --no-build`, so that bootstrap
cannot turn into an implicit second host build. The receipt-backed preparation performs exactly
one authoritative graph-build process and one candidate-verification process. The successful
`EnsureBuilt` path wrote and verified receipts inside that same Core preparation call; there is no
separate publisher and no detached proof sidecar that can promote an existing artifact. Base
preparation performed one restore and one authoritative graph-build/receipt path, reusing the
already-built candidate CLI host. The independent projections then used these process counts:

| Projection | Projection processes | CLI processes |
| --- | ---: | ---: |
| Strict policy | 1 | 1 |
| Reviewed public API | 1 | 3 |
| Architecture coverage | 1 | 7 |
| Health/current/change/report inputs | 1 | 5 |

The report-input projection is the dominant wall-clock projection at 51.383–74.875 s; the strict, public-API, and coverage projections complete in parallel. Rendering and manifest steps consume the projection outputs and do not perform additional analysis.

This evidence authorizes no ArchLinterNet Core optimization. The next step belongs to the linked performance/adoption authority issues (#991 and #19), where this GAP and the bounded attribution should remain visible before any Core algorithm change is proposed.
