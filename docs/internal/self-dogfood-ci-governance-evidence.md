# Self-dogfood CI governance evidence

This record closes issue #992 for implementation candidate `ea24e35fea0dc1793129d679e519c30a7c71d985` and PR #997. The final hosted producer recorded the following identity:

- Source SHA: `ea24e35fea0dc1793129d679e519c30a7c71d985`
- Tree SHA: `c82113e329d6d9d9b2976520577a830e709685d8`
- Policy: `architecture/dependencies.arch.yml`, SHA-256 `3b2b2c6664eb9e598c0bcdbd29af50d63ec89db1f263dc6f910b92843dc3a7ae`
- CLI assembly SHA-256: `2cea4b5e86d7164572cc32ee6d58c51248d22b1c8b06ddce72d77dac968a3b16`
- Testing assembly SHA-256: `a76f29328584bde197e5fbc8bdf7e596c2bc67a9cc5657a8016899bddefef31e`
- Tool identity: `ArchLinterNet.Cli/ArchLinterNet.Testing`

## Hosted samples

The three comparable samples below ran on the standard `ubuntu24` image after the receipt trust-boundary redesign. The policy, base revision, process topology, and preparation-inclusive timing boundary were held constant; the final row is the current PR head. Every projection exited successfully and the projections overlapped.

| Run / producer job | Candidate prep | Base prep | Governance span | Projection command sum | Result |
| --- | ---: | ---: | ---: | ---: | ---: |
| [35659656884](https://github.com/eugenemalaschuk-source/arch-linter-net/actions/runs/35659656884) | 21.371 s | 15.242 s | 92.708 s | 159.407 s | GAP |
| [35661691165](https://github.com/eugenemalaschuk-source/arch-linter-net/actions/runs/35661691165) | 30.068 s | 19.854 s | 127.923 s | 227.659 s | GAP |
| [35665388766](https://github.com/eugenemalaschuk-source/arch-linter-net/actions/runs/35665388766) | 29.526 s | 20.739 s | 127.708 s | 225.879 s | GAP |

The governance-span median is **127.708 s**, with a range of **92.708–127.923 s**. The projection-command-sum median is **225.879 s**, with a range of **159.407–227.659 s**. Against the 204 s baseline and the `<=60 s` target, the measured result is a **GAP**: the normalized fan-out reduces the comparable wall-clock governance time by 76.292 s (37.398%), a **1.597x** speedup, but does not yet reach the target. The headline span starts at the earliest candidate/base preparation boundary and includes candidate receipt publication and base preparation.

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
