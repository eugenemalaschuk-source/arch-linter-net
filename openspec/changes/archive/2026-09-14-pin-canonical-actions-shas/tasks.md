## 1. Re-pin canonical documentation examples

- [x] 1.1 Replace `actions/checkout@v4`, `actions/setup-dotnet@v4`, and `actions/upload-artifact@v4`
      in `docs/guides/ci-integration.md` with the reviewed production commit SHAs and a trailing
      version comment; verify by grepping the file for `@v4` (zero matches) and for the expected
      SHAs.
- [x] 1.2 Apply the same re-pin to `docs/guides/reference-entrypoints.md` and verify the same way.
- [x] 1.3 Apply the same re-pin to both `actions/upload-artifact@v4` occurrences in
      `docs/usage/output-formats.md` and verify the same way.

## 2. Add the canonical-actions-pinning lint

- [x] 2.1 Add `tools/scripts/check_canonical_actions_pinning.py` that scans `docs/**/*.md` for
      `yaml`/`yml` fenced code blocks, extracts `uses:` refs, and fails on a third-party ref that
      is not a full 40-character commit SHA (excluding `./`-local actions and reusable-workflow
      refs to this repository); verify by running the script directly against the re-pinned docs
      and confirming a clean exit.
- [x] 2.2 Add `tools/scripts/tests/test_check_canonical_actions_pinning.py` covering: a mutable
      `@v4` ref inside a fence (violation), a full-SHA pin (no violation), a `./`-local action (no
      violation), a reusable-workflow ref with a placeholder token (no violation), and descriptive
      `@v4` prose outside a fence (no violation); verify with
      `pytest tools/scripts/tests/test_check_canonical_actions_pinning.py`.
- [x] 2.3 Wire the script into `make/docs.mk` as `lint-canonical-actions-pinning` and fold it into
      the `lint-docs` target; verify with `make lint-canonical-actions-pinning`.
- [x] 2.4 Add the new test file to the `test-tooling-coverage` target in `make/lint.mk`; verify
      with `make test-tooling-coverage`.

## 3. Validate and synchronize

- [x] 3.1 Run `make lint-docs` end to end and confirm it passes against the re-pinned docs.
- [x] 3.2 Run `openspec validate --all` and confirm the modified `docs-site` spec is valid.
