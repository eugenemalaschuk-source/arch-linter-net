## Why

The self-governance pull-request job currently prepares the candidate more than once and runs
independent policy, API, coverage, and report projections serially. That inflates the pinned 204 s
governance span and makes it difficult to prove that the published evidence came from one exact
candidate. This change normalizes the CI lane before any Core optimization is considered.

## What Changes

- Build and verify one exact PR candidate identity after restore, then make every architecture
  projection consume that candidate in `--no-build` mode.
- Run the strict policy gate, reviewed public-API check, architecture coverage projection, and
  Health/change/report projection concurrently after the shared preparation boundary, with
  disjoint logs and output ownership.
- Use the existing `health --change-snapshot` seam for the current-side change evidence so report
  rendering does not start a duplicate current analysis.
- Keep report, badge, and manifest steps render-only: they consume validated JSON/Markdown and do
  not invoke architecture analysis.
- Emit candidate identity and per-projection timing/DAG evidence with the producer artifacts so
  comparable standard-hosted runs can be recorded against the 204 s baseline and 60 s target.

## Capabilities

### New Capabilities

- `self-dogfood-ci-governance`: exact-candidate, build-once, fan-out architecture governance for
  the repository's pull-request CI lane.

### Modified Capabilities

## Impact

Affected surfaces are `.github/workflows/ci.yml`, the architecture CI Make targets, the small
candidate-identity/timing helper and its Python tests, the workflow contract tests, and internal
OpenSpec/evidence documentation. No analyzer algorithm, public CLI contract, policy semantics,
runner size, or release publication path changes.
