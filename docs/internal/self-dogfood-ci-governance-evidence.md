# Self-dogfood CI governance evidence

This record closes issue #992 for implementation candidate `caaf566c7e1a2db2b181cc1cb5b9298a4e340df2` and PR #997. The candidate manifest used by the hosted producer recorded the following identity:

- Source SHA: `caaf566c7e1a2db2b181cc1cb5b9298a4e340df2`
- Tree SHA: `42105e03b76a9677c709381c4dc519f13944eada`
- Policy: `architecture/dependencies.arch.yml`, SHA-256 `3b2b2c6664eb9e598c0bcdbd29af50d63ec89db1f263dc6f910b92843dc3a7ae`
- CLI assembly SHA-256: `fbcd24beb6c2b5dd262b4c9df7c36ba827aa477f5206110963a988610d7d2349`
- Testing assembly SHA-256: `240d90e8fb08ef5318cfe834950797b57f6692638396c89d5800f44a5c562a95`
- Tool identity: `ArchLinterNet.Cli/ArchLinterNet.Testing`

## Hosted samples

All samples ran on the standard `ubuntu24` image in workflow `35633228237`, against the same source/tool identity. Every projection exited successfully and the projections overlapped: strict, public API, coverage, and report-input work started within the same few milliseconds.

| Attempt / producer job | Governance span | Projection command sum | Base projection | Result |
| --- | ---: | ---: | ---: | --- |
| 1 / [106444255843](https://github.com/eugenemalaschuk-source/arch-linter-net/actions/runs/35633228237) | 107.590 s | 358.194 s | 82.318 s | GAP |
| 2 / [106449639835](https://github.com/eugenemalaschuk-source/arch-linter-net/actions/runs/35633228237?attempt=2) | 111.263 s | 369.192 s | 84.779 s | GAP |
| 3 / [106451209020](https://github.com/eugenemalaschuk-source/arch-linter-net/actions/runs/35633228237?attempt=3) | 106.189 s | 352.259 s | 81.010 s | GAP |

The governance-span median is **107.590 s**, with a range of **106.189–111.263 s**. The projection-command-sum median is **358.194 s**, with a range of **352.259–369.192 s**. Against the 204 s baseline and the `<=60 s` target, the measured result is a **GAP**: the normalized fan-out reduces wall-clock governance time by 96.410 s (47.3%) versus baseline, but does not yet reach the target.

## Process accounting and attribution

The candidate preparation performed exactly one restore process, one solution build process, and one candidate-verification process. The independent projections then used these process counts:

| Projection | Projection processes | CLI processes |
| --- | ---: | ---: |
| Strict policy | 1 | 1 |
| Reviewed public API | 1 | 3 |
| Architecture coverage | 1 | 7 |
| Health/current/change/report inputs | 1 | 5 |

The report-input projection is the dominant wall-clock projection at 106.185–111.261 s; the strict, public-API, and coverage projections complete in parallel within roughly 49–63 s. Rendering and manifest steps consume the projection outputs and do not perform additional analysis.

This evidence authorizes no ArchLinterNet Core optimization. The next step belongs to the linked performance/adoption authority issues (#991 and #19), where this GAP and the bounded attribution should remain visible before any Core algorithm change is proposed.
