# Self-dogfood CI governance evidence

This record closes issue #992 for implementation candidate `d4e38da578aa96e25a3201a059a41f9c4730d142` and PR #997. The final hosted producer recorded the following identity:

- Source SHA: `d4e38da578aa96e25a3201a059a41f9c4730d142`
- Tree SHA: `366828df1eaea01ea04f971f3c6916f618a4b6be`
- Policy: `architecture/dependencies.arch.yml`, SHA-256 `3b2b2c6664eb9e598c0bcdbd29af50d63ec89db1f263dc6f910b92843dc3a7ae`
- CLI assembly SHA-256: `caaa6b49e2e7b09d151d9d69cef3cce0e92ffd83538e4dbab89f986af47db1a1`
- Testing assembly SHA-256: `1f73b42e8db2233a0caff364d9b48b7bb08be83703ac938046cb5f6e76b87e2b`
- Tool identity: `ArchLinterNet.Cli/ArchLinterNet.Testing`

## Hosted samples

The four comparable samples below ran on the standard `ubuntu24` image after the receipt trust-boundary redesign. The policy, base revision, process topology, and preparation-inclusive timing boundary were held constant; the final row is the current PR head. Every projection exited successfully and the projections overlapped.

| Run / producer job | Candidate prep | Base prep | Governance span | Projection command sum | Result |
| --- | ---: | ---: | ---: | ---: | ---: |
| [35659656884](https://github.com/eugenemalaschuk-source/arch-linter-net/actions/runs/35659656884) | 21.371 s | 15.242 s | 92.708 s | 159.407 s | GAP |
| [35661691165](https://github.com/eugenemalaschuk-source/arch-linter-net/actions/runs/35661691165) | 30.068 s | 19.854 s | 127.923 s | 227.659 s | GAP |
| [35665388766](https://github.com/eugenemalaschuk-source/arch-linter-net/actions/runs/35665388766) | 29.526 s | 20.739 s | 127.708 s | 225.879 s | GAP |
| [35669501579](https://github.com/eugenemalaschuk-source/arch-linter-net/actions/runs/35669501579) | 28.815 s | 16.370 s | 120.941 s | 220.162 s | GAP |

The governance-span median is **124.325 s**, with a range of **92.708–127.923 s**. The projection-command-sum median is **223.021 s**, with a range of **159.407–227.659 s**. Against the 204 s baseline and the `<=60 s` target, the measured result is a **GAP**: the normalized fan-out reduces the comparable wall-clock governance time by 79.675 s (39.057%), a **1.641x** speedup, but does not yet reach the target. The headline span starts at the earliest candidate/base preparation boundary and includes the explicit CLI-host bootstrap, candidate receipt publication, and base preparation.

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
