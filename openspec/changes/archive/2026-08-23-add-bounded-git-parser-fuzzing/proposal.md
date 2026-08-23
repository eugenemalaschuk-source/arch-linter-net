## Why

ArchLinterNet directly parses repository-controlled Git object, index, pack, and
delta bytes. The completed input-robustness assessment selected these custom
binary parsers as the one current surface where coverage-guided fuzzing provides
material value beyond the deterministic corrupt-object fixtures.

## What Changes

- Add a small executable harness over synthetic byte-array seams for loose
  objects, version-2 pack indexes, pack-entry headers, and REF-delta decoding.
- Version a public-safe seed corpus, deterministic replay command, and NUnit
  regression coverage for the harness contract.
- Pin and verify the .NET 10 SharpFuzz/AFL++ Linux campaign toolchain, with
  fixed 1 MiB input, 100 ms per-case, and 512 MiB process limits.
- Add a manual/scheduled-only GitHub Actions campaign that retains only
  reviewed, public-safe minimized artifacts and never becomes a PR gate.
- Document corpus ownership, replay/minimization, triage, and the no-secrets,
  no-private-input contract.

## Capabilities

### New Capabilities

- `git-parser-fuzzing`: Bounded coverage-guided fuzzing of synthetic Git binary
  parser inputs, including corpus, replay, oracle, and triage guarantees.

### Modified Capabilities

- `github-actions-ci`: Add a pinned scheduled/manual fuzz campaign that is
  isolated from ordinary pull-request validation.

## Impact

The change adds internal Core parser seams, a non-shipping fuzz executable and
synthetic corpus, Core NUnit tests, internal documentation, and a GitHub Actions
workflow. It adds no shipped public API, does not access network or live Git
repositories at runtime, and leaves normal CI deterministic and bounded.
