# Checkpoint A evidence

## Scope

This evidence records the reusable adoption corpus established by issue #403. It exercises only already implemented safety-critical slices and is intentionally narrower than the final packed-artifact acceptance matrix.

## Observed execution environment

| Field | Observed value |
|---|---|
| Platform | macOS x86_64 |
| Runtime | .NET 10 SDK from the repository toolchain |
| Corpus entrypoints | `CheckpointAAdoptionAcceptanceTests`, `CheckpointACommandLineAcceptanceTests`, and mapped existing NUnit scenarios |
| Scenario inventory | `adoption-acceptance-corpus/v1` |

## Scenarios exercised

The manifest records imports/provenance, exact baseline identity, subtractive selectors, package/framework evidence, assembly-aware composition roots, clean-checkout preflight, snapshot/report reuse, and redirected human/JSON/SARIF output. Each scenario is linked to its already implemented child slice and NUnit entrypoint.

## Non-release statement

Checkpoint A is implementation evidence only. This record does **not** publish packages, authorize a release, establish an intermediate stabilization version, or claim full platform, shell, offline, cache, parallelism, cancellation, or packaged-artifact support. Only Checkpoint B/#366 may authorize version 0.5.1.
