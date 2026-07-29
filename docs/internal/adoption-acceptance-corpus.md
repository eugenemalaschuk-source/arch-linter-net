# Adoption acceptance corpus

This document owns the reusable, synthetic adopter-shaped fixture system introduced for issue #403. Its deterministic inventory is [Checkpoint A scenario manifest](../../tests/ArchLinterNet.Core.Tests/AdoptionAcceptance/CheckpointAScenarioManifest.json).

## Fixture inventory and ownership

| Fixture | Shape | Primary evidence | Owner |
|---|---|---|---|
| `minimal-single-project` | Small policy | include-minus-exclude selector behavior | #356 |
| `conventional-multi-project` | Ordinary multi-project solution | evaluated `FrameworkReference` behavior | #359 |
| `same-named-multi-host` | Multiple hosts with same-named roots | assembly-aware composition identity | #360 |
| `legacy-import-migration` | Imported root policy with a 0.5.0 baseline | provenance and legacy-baseline compatibility | #361 |
| `clean-checkout` | Project tree without `bin`/`obj` | deterministic build-state preflight | #362 |

All fixture names, source identities, report examples, and repository references are synthetic. The executable entrypoints are NUnit tests: `CheckpointAAdoptionAcceptanceTests` validates the manifest and cross-surface Core/Testing behavior, while `CheckpointACommandLineAcceptanceTests` validates redirected CLI output.

## Extension rule

Issues #374, #411, and #366 extend this manifest and the listed fixture roots. They must not construct independent fixture systems. New scenarios name their owning implementation slice and the exact NUnit entrypoint that proves them.

## Checkpoint boundary

Checkpoint A is internal safety and correctness evidence. It does not publish packages, declare a support matrix, authorize a public checkpoint release, or authorize version 0.5.1. Those claims remain exclusively with Checkpoint B and #366.
