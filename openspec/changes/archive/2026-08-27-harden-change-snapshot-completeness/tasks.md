## 1. Complete snapshot orchestration

- [x] 1.1 Make `change snapshot` fail without writing when validation preflight or requested baseline debt is blocked, and render baseline preflight evidence.
- [x] 1.2 Add a receipt-backed prepared build-state mode so graph and baseline contributors re-verify and materialize isolated post-build runners without rebuilding.
- [x] 1.3 Route one ensure-built preparation followed by prepared graph/baseline requests, preserving output context.

## 2. Regression coverage and API approval

- [x] 2.1 Add CLI orchestration regressions for blocked baseline debt, no output write, and exactly one ensure-built request for a complete snapshot.
- [x] 2.2 Add focused Core coverage for prepared graph and baseline routing without a second build.
- [x] 2.3 Update reviewed Core public API approval baselines for the intentional additive API.

## 3. Validation and archive

- [x] 3.1 Run focused and affected Core/CLI tests, formatting, public API checks, and relevant lint.
- [x] 3.2 Validate, synchronize, and archive the OpenSpec change.
