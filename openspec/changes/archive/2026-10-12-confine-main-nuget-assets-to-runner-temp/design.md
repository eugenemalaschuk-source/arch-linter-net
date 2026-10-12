## Context

The workflow creates restore output under the GitHub runner's temporary directory, which is outside
the checkout. `_safe_path` correctly rejects paths outside the release workspace, so it cannot
validate this explicitly runner-owned input.

## Decision

Use the existing `_github_runner_temp_path` helper for the CLI's `--assets` argument. This resolves
both the candidate and the process's `RUNNER_TEMP` value and rejects paths that escape the resolved
root. Keep the verifier's lower-level function available to unit tests and callers that already
provide a trusted `Path`; enforce the runner boundary at the CLI entry point where the external
argument enters.

## Risks

- A workflow relocation outside `RUNNER_TEMP` will fail closed. The workflow currently restores
  under `$RUNNER_TEMP`, and a regression test documents that contract.
- Symlink escapes remain rejected by the helper's resolved-path containment check.
