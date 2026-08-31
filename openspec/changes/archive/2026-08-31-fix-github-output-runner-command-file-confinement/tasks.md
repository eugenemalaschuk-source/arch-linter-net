## 1. Trust boundary

- [x] 1.1 Add `_github_command_file_path(value, description, env_var)` to
      `tools/release/_release_workspace.py`, validating that `value` resolves to exactly the
      current value of the `env_var` environment variable; verify with
      `test_github_command_file_path_accepts_the_exact_runner_provided_transport_path`,
      `test_github_command_file_path_rejects_arbitrary_path_not_matching_the_runner_env_var`, and
      `test_github_command_file_path_rejects_when_the_runner_env_var_is_not_set` in
      `tools/release/tests/test_main_quality_coverage.py`.

## 2. Wire up main_quality_coverage.py

- [x] 2.1 Replace `_safe_path(arguments.github_output, _GITHUB_OUTPUT_DESCRIPTION)` with
      `_github_command_file_path(arguments.github_output, _GITHUB_OUTPUT_DESCRIPTION,
      "GITHUB_OUTPUT")` at all 3 call sites (`_assemble`, `_verify_inventory_command`,
      `_verify_sonar`); verify with `grep -n _github_command_file_path
      tools/release/main_quality_coverage.py` showing 3 call sites and 0 remaining
      `_safe_path(arguments.github_output` occurrences.
- [x] 2.2 Confirm every other `_safe_path` call site in `tools/release/main_quality_coverage.py`
      (coverage/output/artifacts/inventory roots, manifest-derived report paths) is unchanged;
      verify with `git diff tools/release/main_quality_coverage.py` showing only the 3
      `--github-output` lines touched.

## 3. Tests

- [x] 3.1 Update existing tests that exercise `--github-output` to set
      `monkeypatch.setenv("GITHUB_OUTPUT", <matching path>)` before invoking the validated code
      path; verify with `make test-release-evidence` passing.
- [x] 3.2 Add `test_assemble_rejects_arbitrary_github_output_path_not_bound_to_the_runner_env` and
      `test_assemble_accepts_runner_shaped_github_output_path_outside_the_workspace` to
      `tools/release/tests/test_main_quality_coverage.py`, the latter using a path outside both
      the working tree and the repository root to reproduce the real CI failure shape; verify
      both tests pass under
      `pytest tools/release/tests/test_main_quality_coverage.py -q`.

## 4. Spec synchronization

- [x] 4.1 Author the `release-tooling-workspace-confinement` MODIFIED delta documenting the
      GitHub-runner-command-file exception and the new
      `## ADDED Requirements`-equivalent sibling requirement for the environment-anchored trust
      boundary; verify with `openspec validate fix-github-output-runner-command-file-confinement
      --strict`.
- [x] 4.2 Run `openspec archive fix-github-output-runner-command-file-confinement` and
      `openspec validate --all` to confirm the rebuilt main spec passes.
