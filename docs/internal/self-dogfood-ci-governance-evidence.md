# Self-dogfood CI governance evidence

This record closes issue #992 for implementation candidate `5e4a842587019fc1fd7acfcd6431f55dd9e66aa1` and PR #997. The candidate manifest used by the hosted producer recorded the following identity:

- Source SHA: `5e4a842587019fc1fd7acfcd6431f55dd9e66aa1`
- Tree SHA: `aa36e94260e422156bc4df5d2aa7f76346d5ad22`
- Policy: `architecture/dependencies.arch.yml`, SHA-256 `3b2b2c6664eb9e598c0bcdbd29af50d63ec89db1f263dc6f910b92843dc3a7ae`
- CLI assembly SHA-256: `30506e794d8d581215f7498daa6a1118aab8851ece4bfb6f3960aa613e6c0bc7`
- Testing assembly SHA-256: `df4fdd1ed6cf1b526edf719cbba24357bd6a6b6a8a1260e3de6874475ff2ab66`
- Tool identity: `ArchLinterNet.Cli/ArchLinterNet.Testing`

## Hosted samples

All samples ran on the standard `ubuntu24` image in workflow `35646536730`, against the same source/tool identity. Every projection exited successfully and the projections overlapped: strict, public API, coverage, and report-input work started within the same few milliseconds.

| Attempt / producer job | Candidate prep | Base prep | Governance span | Projection command sum | Result |
| --- | ---: | ---: | ---: | ---: | ---: |
| 1 / [Architecture Coverage](https://github.com/eugenemalaschuk-source/arch-linter-net/actions/runs/35646536730?attempt=1) | 62.657 s | 28.845 s | 167.839 s | 222.757 s | GAP |
| 2 / [Architecture Coverage](https://github.com/eugenemalaschuk-source/arch-linter-net/actions/runs/35646536730?attempt=2) | 59.560 s | 32.323 s | 167.309 s | 217.582 s | GAP |
| 3 / [Architecture Coverage](https://github.com/eugenemalaschuk-source/arch-linter-net/actions/runs/35646536730?attempt=3) | 48.392 s | 25.792 s | 126.761 s | 152.649 s | GAP |

The governance-span median is **167.309 s**, with a range of **126.761–167.839 s**. The projection-command-sum median is **217.582 s**, with a range of **152.649–222.757 s**. Against the 204 s baseline and the `<=60 s` target, the measured result is a **GAP**: the normalized fan-out reduces the comparable wall-clock governance time by 36.691 s (17.99%), a **1.219x** speedup, but does not yet reach the target. The headline span starts at the earliest candidate/base preparation boundary and includes candidate receipt publication and base preparation.

## Process accounting and attribution

The candidate preparation performed exactly one restore process, one solution build process, one receipt-publication process, and one candidate-verification process. Base preparation performed one restore, one solution build, and one receipt-publication process. The independent projections then used these process counts:

| Projection | Projection processes | CLI processes |
| --- | ---: | ---: |
| Strict policy | 1 | 1 |
| Reviewed public API | 1 | 3 |
| Architecture coverage | 1 | 7 |
| Health/current/change/report inputs | 1 | 5 |

The report-input projection is the dominant wall-clock projection at 51.383–74.875 s; the strict, public-API, and coverage projections complete in parallel. Rendering and manifest steps consume the projection outputs and do not perform additional analysis.

This evidence authorizes no ArchLinterNet Core optimization. The next step belongs to the linked performance/adoption authority issues (#991 and #19), where this GAP and the bounded attribution should remain visible before any Core algorithm change is proposed.
