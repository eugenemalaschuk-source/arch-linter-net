# Self-dogfood CI governance evidence

This record closes issue #992 for implementation candidate `a6825cc4b5b9b828e25fd6dacd56efe1589fad46` and PR #997. The final hosted producer recorded the following identity:

- Source SHA: `a6825cc4b5b9b828e25fd6dacd56efe1589fad46`
- Tree SHA: `073394e9c2a20d0429b089fd7e341e933735ac78`
- Policy: `architecture/dependencies.arch.yml`, SHA-256 `3b2b2c6664eb9e598c0bcdbd29af50d63ec89db1f263dc6f910b92843dc3a7ae`
- CLI assembly SHA-256: `8ff549c9f7e9f663c53017d041f5cd5775ed71ecfd718b41adc4fc12be4e1690`
- Testing assembly SHA-256: `8b69f9bb9dbf53e4d17dae043f778dc347a95d65f61bff8f2d90138cad81a6fa`
- Tool identity: `ArchLinterNet.Cli/ArchLinterNet.Testing`

## Hosted samples

The three comparable samples below ran on the standard `ubuntu24` image after the receipt trust-boundary redesign. The policy, base revision, process topology, and preparation-inclusive timing boundary were held constant; the final row is the current PR head. Every projection exited successfully and the projections overlapped.

| Run / producer job | Candidate prep | Base prep | Governance span | Projection command sum | Result |
| --- | ---: | ---: | ---: | ---: | ---: |
| [35659656884](https://github.com/eugenemalaschuk-source/arch-linter-net/actions/runs/35659656884) | 21.371 s | 15.242 s | 92.708 s | 159.407 s | GAP |
| [35661691165](https://github.com/eugenemalaschuk-source/arch-linter-net/actions/runs/35661691165) | 30.068 s | 19.854 s | 127.923 s | 227.659 s | GAP |
| [35664256542](https://github.com/eugenemalaschuk-source/arch-linter-net/actions/runs/35664256542) | 31.047 s | 20.210 s | 128.830 s | 224.138 s | GAP |

The governance-span median is **127.923 s**, with a range of **92.708–128.830 s**. The projection-command-sum median is **224.138 s**, with a range of **159.407–227.659 s**. Against the 204 s baseline and the `<=60 s` target, the measured result is a **GAP**: the normalized fan-out reduces the comparable wall-clock governance time by 76.077 s (37.293%), a **1.595x** speedup, but does not yet reach the target. The headline span starts at the earliest candidate/base preparation boundary and includes candidate receipt publication and base preparation.

## Process accounting and attribution

The candidate preparation performed exactly one restore process, one authoritative graph-build
process, and one candidate-verification process. The successful `EnsureBuilt` path wrote and
verified receipts inside that same Core preparation call; there is no separate publisher and no
detached proof sidecar that can promote an existing artifact. Base preparation performed one
restore and one authoritative graph-build/receipt path. The independent projections then used
these process counts:

| Projection | Projection processes | CLI processes |
| --- | ---: | ---: |
| Strict policy | 1 | 1 |
| Reviewed public API | 1 | 3 |
| Architecture coverage | 1 | 7 |
| Health/current/change/report inputs | 1 | 5 |

The report-input projection is the dominant wall-clock projection at 51.383–74.875 s; the strict, public-API, and coverage projections complete in parallel. Rendering and manifest steps consume the projection outputs and do not perform additional analysis.

This evidence authorizes no ArchLinterNet Core optimization. The next step belongs to the linked performance/adoption authority issues (#991 and #19), where this GAP and the bounded attribution should remain visible before any Core algorithm change is proposed.
