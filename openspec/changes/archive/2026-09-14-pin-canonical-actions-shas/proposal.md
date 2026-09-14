## Why

Production GitHub Actions workflows in this repository already pin third-party actions to
immutable commit SHAs, but several canonical, copy/paste public documentation examples
(`docs/guides/ci-integration.md`, `docs/guides/reference-entrypoints.md`,
`docs/usage/output-formats.md`) still use mutable major-version tags such as
`actions/checkout@v4`. Adopters who copy these examples inherit a weaker supply-chain posture
than the repository's own CI, and nothing currently prevents a new mutable tag from being added
to canonical documentation again.

## What Changes

- Replace mutable third-party `uses:` refs in the canonical GitHub Actions documentation
  examples with the same full immutable commit SHAs already reviewed and used by the production
  workflows for the same action/version, keeping a human-readable version comment beside each
  pin.
- Add a deterministic documentation lint (`tools/scripts/check_canonical_actions_pinning.py`,
  wired into `make lint-docs`) that scans canonical workflow-example code fences in public docs
  and fails when a third-party `uses:` ref is not pinned to a full commit SHA, while leaving
  first-party local actions (`./...`) and reusable-workflow refs to this repository unaffected.
- Add focused regression tests proving the lint rejects a mutable canonical `uses:` ref and
  accepts a full-SHA pin, a local action, and non-workflow prose mentioning `@v4` descriptively.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `docs-site`: adds a new documentation lint requirement (canonical GitHub Actions example
  pinning) alongside the existing `lint-docs` make-target requirement, and a corresponding
  `make lint-canonical-actions-pinning` target folded into `make lint-docs`.

## Impact

- `docs/guides/ci-integration.md`, `docs/guides/reference-entrypoints.md`,
  `docs/usage/output-formats.md`: `uses:` refs re-pinned to commit SHAs.
- `tools/scripts/check_canonical_actions_pinning.py` (new): the lint script.
- `tools/scripts/tests/test_check_canonical_actions_pinning.py` (new): regression tests.
- `make/docs.mk`, `make/lint.mk` (test wiring): new lint target folded into `lint-docs` and
  `test-tooling-coverage`.
- No runtime C# code, architecture policy, or release authority changes.
